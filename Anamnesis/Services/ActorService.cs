﻿// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis;

using Anamnesis.Actor.Refresh;
using Anamnesis.Core;
using Anamnesis.Core.Memory;
using Anamnesis.Memory;
using Anamnesis.Services;
using PropertyChanged;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Service for managing and refreshing actors.</summary>
[AddINotifyPropertyChangedInterface]
public class ActorService : ServiceBase<ActorService>
{
	private const int TickDelay = 10; // ms
	private const int ActorTableSize = 819;
	private const int GPoseIndexStart = 200;
	private const int GPoseIndexEnd = 440;
	private const int OverworldPlayerIndex = 0;
	private const int GPosePlayerIndex = 201;

	private readonly IntPtr[] actorTable = new IntPtr[ActorTableSize];
	private readonly ReaderWriterLockSlim actorTableLock = new();

	private readonly List<IActorRefresher> actorRefreshers =
	[
		new BrioActorRefresher(),
		new PenumbraActorRefresher(),
		new AnamnesisActorRefresher(),
	];

	/// <inheritdoc/>
	protected override IEnumerable<IService> Dependencies => [AddressService.Instance];

	/// <summary>Gets the actor table as a read-only collection.</summary>
	public ReadOnlyCollection<IntPtr> ActorTable
	{
		get
		{
			this.actorTableLock.EnterReadLock();
			try
			{
				return Array.AsReadOnly(this.actorTable);
			}
			finally
			{
				this.actorTableLock.ExitReadLock();
			}
		}
	}

	/// <summary>Determines if the actor is in GPose.</summary>
	/// <param name="objectIndex">The index of the actor.</param>
	/// <returns>True if the actor is in GPose, otherwise false.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsGPoseActor(int objectIndex) => objectIndex >= GPoseIndexStart && objectIndex < GPoseIndexEnd;

	/// <summary>Determines if the actor is in the overworld.</summary>
	/// <param name="objectIndex">The index of the actor.</param>
	/// <returns>True if the actor is in the overworld, otherwise false.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsOverworldActor(int objectIndex) => !IsGPoseActor(objectIndex);

	/// <summary>Determines if the actor is the local overworld player.</summary>
	/// <param name="objectIndex">The index of the actor.</param>
	/// <returns>True if the actor is the local overworld player, otherwise false.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsLocalOverworldPlayer(int objectIndex) => objectIndex == OverworldPlayerIndex;

	/// <summary>Determines if the actor is the local GPose player.</summary>
	/// <param name="objectIndex">The index of the actor.</param>
	/// <returns>True if the actor is the local GPose player, otherwise false.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsLocalGPosePlayer(int objectIndex) => objectIndex == GPosePlayerIndex;

	/// <summary>Determines if the actor is the local player.</summary>
	/// <param name="objectIndex">The index of the actor.</param>
	/// <returns>True if the actor is the local player, otherwise false.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsLocalPlayer(int objectIndex) => IsLocalOverworldPlayer(objectIndex) || IsLocalGPosePlayer(objectIndex);

	/// <summary>Determines if the actor can be refreshed.</summary>
	/// <param name="actor">The actor to check.</param>
	/// <returns>True if the actor can be refreshed, otherwise false.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool CanRefreshActor(ActorMemory actor)
	{
		if (!actor.IsValid)
			return false;

		foreach (IActorRefresher actorRefresher in this.actorRefreshers)
		{
			if (actorRefresher.CanRefresh(actor))
				return true;
		}

		return false;
	}

	/// <summary>Refreshes the specified actor.</summary>
	/// <param name="actor">The actor to refresh.</param>
	/// <returns>True if the actor was refreshed, otherwise false.</returns>
	public async Task<bool> RefreshActor(ActorMemory actor)
	{
		if (this.CanRefreshActor(actor))
		{
			foreach (IActorRefresher actorRefresher in this.actorRefreshers)
			{
				if (actorRefresher.CanRefresh(actor))
				{
					Log.Information($"Executing {actorRefresher.GetType().Name} refresh for actor address: {actor.Address}");
					await actorRefresher.RefreshActor(actor);
					return true;
				}
			}
		}

		return false;
	}

	/// <summary>Gets the index of the actor in the actor table.</summary>
	/// <param name="pointer">The pointer to the actor.</param>
	/// <param name="refresh">Whether to refresh the actor table.</param>
	/// <returns>The index of the actor in the actor table, or -1 if not found.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int GetActorTableIndex(IntPtr pointer, bool refresh = false)
	{
		if (pointer == IntPtr.Zero)
			return -1;

		if (refresh)
			this.UpdateActorTable();

		this.actorTableLock.EnterReadLock();
		try
		{
			return Array.IndexOf(this.actorTable, pointer);
		}
		finally
		{
			this.actorTableLock.ExitReadLock();
		}
	}

	/// <summary>Determines if the actor is in the actor table.</summary>
	/// <param name="ptr">The pointer to the actor.</param>
	/// <param name="refresh">Whether to refresh the actor table.</param>
	/// <returns>True if the actor is in the actor table, otherwise false.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool IsActorInTable(IntPtr ptr, bool refresh = false)
	{
		return this.GetActorTableIndex(ptr, refresh) != -1;
	}

	/// <summary>Determines if the actor is in the actor table.</summary>
	/// <param name="memory">The memory of the actor.</param>
	/// <param name="refresh">Whether to refresh the actor table.</param>
	/// <returns>True if the actor is in the actor table, otherwise false.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool IsActorInTable(MemoryBase memory, bool refresh = false) => this.IsActorInTable(memory.Address, refresh);

	/// <summary>Determines if the actor is in GPose.</summary>
	/// <param name="actorAddress">The address of the actor.</param>
	/// <returns>True if the actor is in GPose, otherwise false.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool IsGPoseActor(IntPtr actorAddress)
	{
		int objectIndex = this.GetActorTableIndex(actorAddress);

		if (objectIndex == -1)
			return false;

		return IsGPoseActor(objectIndex);
	}

	/// <summary>Determines if the actor is in the overworld.</summary>
	/// <param name="actorAddress">The address of the actor.</param>
	/// <returns>True if the actor is in the overworld, otherwise false.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool IsOverworldActor(IntPtr actorAddress) => !this.IsGPoseActor(actorAddress);

	/// <summary>Determines if the actor is the local overworld player.</summary>
	/// <param name="actorAddress">The address of the actor.</param>
	/// <returns>True if the actor is the local overworld player, otherwise false.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool IsLocalOverworldPlayer(IntPtr actorAddress)
	{
		int objectIndex = this.GetActorTableIndex(actorAddress);

		if (objectIndex == -1)
			return false;

		return IsLocalOverworldPlayer(objectIndex);
	}

	/// <summary>Determines if the actor is the local GPose player.</summary>
	/// <param name="actorAddress">The address of the actor.</param>
	/// <returns>True if the actor is the local GPose player, otherwise false.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool IsLocalGPosePlayer(IntPtr actorAddress)
	{
		int objectIndex = this.GetActorTableIndex(actorAddress);

		if (objectIndex == -1)
			return false;

		return IsLocalGPosePlayer(objectIndex);
	}

	/// <summary>Determines if the actor is the local player.</summary>
	/// <param name="actorAddress">The address of the actor.</param>
	/// <returns>True if the actor is the local player, otherwise false.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool IsLocalPlayer(IntPtr actorAddress) => this.IsLocalOverworldPlayer(actorAddress) || this.IsLocalGPosePlayer(actorAddress);

	/// <summary>Gets all actors from actor table.</summary>
	/// <param name="refresh">Whether to refresh the actor table.</param>
	/// <returns>A list of all actors.</returns>
	public List<ActorBasicMemory> GetAllActors(bool refresh = false)
	{
		if (refresh)
			this.UpdateActorTable();

		List<ActorBasicMemory> results = [];

		this.actorTableLock.EnterReadLock();
		try
		{
			foreach (var ptr in this.actorTable)
			{
				if (ptr == IntPtr.Zero)
					continue;

				try
				{
					ActorBasicMemory actor = new();
					actor.SetAddress(ptr);
					results.Add(actor);
				}
				catch (Exception ex)
				{
					Log.Warning(ex, $"Failed to create basic actor memory object from address: {ptr}");
				}
			}
		}
		finally
		{
			this.actorTableLock.ExitReadLock();
		}

		return results;
	}

	/// <inheritdoc/>
	protected override async Task OnStart()
	{
		this.CancellationTokenSource = new CancellationTokenSource();
		this.BackgroundTask = Task.Run(() => this.TickTask(this.CancellationToken));
		await base.OnStart();
	}

	/// <summary>Periodically refreshes the actor table.</summary>
	private async Task TickTask(CancellationToken cancellationToken)
	{
		while (this.IsInitialized && !cancellationToken.IsCancellationRequested)
		{
			try
			{
				this.UpdateActorTable();
				await Task.Delay(TickDelay, cancellationToken);
			}
			catch (TaskCanceledException)
			{
				// Task was canceled, exit the loop.
				break;
			}
		}
	}

	/// <summary>Updates the actor table by reading from memory.</summary>
	private void UpdateActorTable()
	{
		if (!GameService.Ready)
			return;

		int tableSizeInBytes = ActorTableSize * IntPtr.Size;
		byte[] buffer = ArrayPool<byte>.Shared.Rent(tableSizeInBytes);

		try
		{
			if (!MemoryService.Read(AddressService.ActorTable, buffer.AsSpan(0, tableSizeInBytes)))
				throw new Exception("Failed to read actor table from memory.");

			bool hasChanged = false;

			this.actorTableLock.EnterWriteLock();
			try
			{
				Span<IntPtr> currentSpan = this.actorTable.AsSpan();
				Span<IntPtr> newSpan = MemoryMarshal.Cast<byte, IntPtr>(buffer.AsSpan(0, tableSizeInBytes));

				if (!currentSpan.SequenceEqual(newSpan))
				{
					hasChanged = true;
					newSpan.CopyTo(currentSpan);
				}
			}
			finally
			{
				this.actorTableLock.ExitWriteLock();
			}

			if (hasChanged)
			{
				this.RaisePropertyChanged(nameof(this.ActorTable));
				Log.Verbose("[ActorService] Actor table updated.");
			}
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}
}
