﻿// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.Memory;

using Anamnesis.Core.Extensions;
using Anamnesis.Services;
using Anamnesis.Utils;
using PropertyChanged;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

public class ActorMemory : ActorBasicMemory
{
	private static readonly int RefreshDebounceTimeout = 200;
	private readonly System.Timers.Timer refreshDebounceTimer;
	private readonly FuncQueue backupQueue;
	private int isRefreshing = 0;

	public ActorMemory()
	{
		this.backupQueue = new(this.BackupAsync, 250);

		this.PropertyChanged += this.HandlePropertyChanged;

		// Initialize the debounce timer
		this.refreshDebounceTimer = new(RefreshDebounceTimeout) { AutoReset = false };
		this.refreshDebounceTimer.Elapsed += async (s, e) => { await this.Refresh(); };
	}

	public event EventHandler? Refreshed;

	public enum CharacterModes : byte
	{
		None = 0,
		Normal = 1,
		EmoteLoop = 3,
		Mounted = 4,
		AnimLock = 8,
		Carrying = 9,
		InPositionLoop = 11,
		Performance = 16,
	}

	[Flags]
	public enum CharacterFlagDefs : byte
	{
		None = 0,
		WeaponsVisible = 1 << 0,
		WeaponsDrawn = 1 << 2,
		VisorToggled = 1 << 4,
	}

	[Bind(0x008D)] public byte SubKind { get; set; }
	[Bind(0x00B4)] public float Scale { get; set; }
	[Bind(0x00F0, BindFlags.Pointer)] public ActorModelMemory? ModelObject { get; set; }
	[Bind(0x01BA)] public byte ClassJob { get; set; }
	[Bind(0x0660, BindFlags.Pointer)] public ActorMemory? Mount { get; set; }
	[Bind(0x0668)] public ushort MountId { get; set; }
	[Bind(0x06C8, BindFlags.Pointer)] public ActorMemory? Companion { get; set; }
	[Bind(0x06E8)] public WeaponMemory? MainHand { get; set; }
	[Bind(0x0758)] public WeaponMemory? OffHand { get; set; }
	[Bind(0x0838)] public ActorEquipmentMemory? Equipment { get; set; }
	[Bind(0x0888)] public ActorCustomizeMemory? Customize { get; set; }
	[Bind(0x08A6, BindFlags.ActorRefresh)] public bool HatHidden { get; set; }
	[Bind(0x08A7, BindFlags.ActorRefresh)] public CharacterFlagDefs CharacterFlags { get; set; }
	[Bind(0x08A8)] public GlassesMemory? Glasses { get; set; }
	[Bind(0x08E0, BindFlags.Pointer)] public ActorMemory? Ornament { get; set; }
	[Bind(0x08E8)] public ushort OrnamentId { get; set; }
	[Bind(0x09B0)] public AnimationMemory? Animation { get; set; }
	[Bind(0x19C8)] public byte Voice { get; set; }
	[Bind(0x1AA8, BindFlags.ActorRefresh)] public int ModelType { get; set; }
	[Bind(0x1B14)] public bool IsMotionDisabled { get; set; }
	[Bind(0x2258)] public float Transparency { get; set; }
	[Bind(0x22CC)] public byte CharacterModeRaw { get; set; }
	[Bind(0x22CD)] public byte CharacterModeInput { get; set; }
	[Bind(0x22EA)] public byte AttachmentPoint { get; set; }

	public PinnedActor? Pinned { get; set; }

	public History History { get; private set; } = new();

	public bool AutomaticRefreshEnabled { get; set; } = true;
	public bool IsRefreshing
	{
		get => Interlocked.CompareExchange(ref this.isRefreshing, 0, 0) == 1;
		set => Interlocked.Exchange(ref this.isRefreshing, value ? 1 : 0);
	}

	public bool IsWeaponDirty { get; set; } = false;

	[DependsOn(nameof(IsValid), nameof(IsOverworldActor), nameof(Name), nameof(RenderMode))]
	public bool CanRefresh => ActorService.Instance.CanRefreshActor(this);

	public bool IsHuman => this.ModelObject != null && this.ModelObject.IsHuman;

	[DependsOn(nameof(ModelType))]
	public bool IsChocobo => this.ModelType == 1;

	[DependsOn(nameof(CharacterModeRaw))]
	public CharacterModes CharacterMode
	{
		get
		{
			return (CharacterModes)this.CharacterModeRaw;
		}
		set
		{
			this.CharacterModeRaw = (byte)value;
		}
	}

	[DependsOn(nameof(MountId), nameof(Mount))]
	public bool IsMounted => this.MountId != 0 && this.Mount != null;

	[DependsOn(nameof(OrnamentId), nameof(Ornament))]
	public bool IsUsingOrnament => this.Ornament != null && this.OrnamentId != 0;

	[DependsOn(nameof(Companion))]
	public bool HasCompanion => this.Companion != null;

	[DependsOn(nameof(CharacterFlags))]
	public bool VisorToggled
	{
		get => this.CharacterFlags.HasFlagUnsafe(CharacterFlagDefs.VisorToggled);
		set
		{
			if (value)
			{
				this.CharacterFlags |= CharacterFlagDefs.VisorToggled;
			}
			else
			{
				this.CharacterFlags &= ~CharacterFlagDefs.VisorToggled;
			}
		}
	}

	[DependsOn(nameof(IsMotionDisabled))]
	public bool IsMotionEnabled
	{
		get => !this.IsMotionDisabled;
		set => this.IsMotionDisabled = !value;
	}

	[DependsOn(nameof(ObjectIndex), nameof(CharacterMode))]
	public bool CanAnimate => this.CharacterMode == CharacterModes.Normal || this.CharacterMode == CharacterModes.AnimLock || !ActorService.IsLocalOverworldPlayer(this.ObjectIndex);

	[DependsOn(nameof(CharacterMode))]
	public bool IsAnimationOverridden => this.CharacterMode == CharacterModes.AnimLock;

	public override void Synchronize()
	{
		this.History.Tick();

		// Don't synchronize the actor during a refresh.
		if (this.IsRefreshing)
			return;

		base.Synchronize();
	}

	/// <summary>
	/// Asynchronously refresh the actor to force the game to reflect appearance changes.
	/// </summary>
	public async Task Refresh()
	{
		if (this.IsRefreshing)
			return;

		if (!this.CanRefresh)
			return;

		if (this.Address == IntPtr.Zero)
			return;

		try
		{
			Log.Information($"Attempting actor refresh for actor address: {this.Address}");

			this.IsRefreshing = true;

			if (await ActorService.Instance.RefreshActor(this))
			{
				Log.Information($"Completed actor refresh for actor address: {this.Address}");
			}
			else
			{
				Log.Information($"Could not refresh actor: {this.Address}");
			}
		}
		catch (Exception ex)
		{
			Log.Error(ex, $"Error refreshing actor: {this.Address}");
		}
		finally
		{
			this.IsRefreshing = false;
			this.IsWeaponDirty = false;
		}

		this.OnPropertyChanged(nameof(this.IsHuman));
		this.OnRefreshed();
	}

	public async Task BackupAsync()
	{
		while (this.IsRefreshing)
			await Task.Delay(10);

		this.Pinned?.CreateCharacterBackup(PinnedActor.BackupModes.Gpose);
	}

	public void RaiseRefreshChanged()
	{
		this.OnPropertyChanged(nameof(this.CanRefresh));
	}

	protected virtual void OnRefreshed()
	{
		this.Refreshed?.Invoke(this, EventArgs.Empty);
	}

	private void HandlePropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e is not MemObjPropertyChangedEventArgs memObjEventArgs)
			return;

		var change = memObjEventArgs.Context;

		// Do not not refresh the actor if the change originated from the game
		if (change.Origin == PropertyChange.Origins.Game)
			return;

		// Only record changes that originate from the user
		if (!change.OriginBind.Flags.HasFlagUnsafe(BindFlags.DontRecordHistory) && !HistoryService.Instance.IsRestoring)
		{
			if (change.Origin == PropertyChange.Origins.User)
			{
				// Big hack to keep bone change history names short.
				if (change.OriginBind.Memory.ParentBind?.Type == typeof(TransformMemory))
				{
					change.Name = (PoseService.SelectedBonesText == null) ?
						LocalizationService.GetStringFormatted("History_ChangeBone", LocalizationService.GetString("Pose_OtherUnknown")) :
						LocalizationService.GetStringFormatted("History_ChangeBone", PoseService.SelectedBonesText);
				}

				this.History.Record(change);
			}
		}

		// Create backup
		this.backupQueue.Invoke();

		// Refresh the actor
		if (this.AutomaticRefreshEnabled && change.OriginBind.Flags.HasFlagUnsafe(BindFlags.ActorRefresh))
		{
			// Don't refresh because of a refresh
			if (this.IsRefreshing && (change.OriginBind.Name == nameof(this.ObjectKind) || change.OriginBind.Name == nameof(this.RenderMode)))
				return;

			if (change.OriginBind.Flags.HasFlagUnsafe(BindFlags.WeaponRefresh))
				this.IsWeaponDirty = true;

			// Restart the debounce timer if it's already running, otherwise start it
			this.refreshDebounceTimer.Stop();
			this.refreshDebounceTimer.Start();
		}
	}
}
