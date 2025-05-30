﻿// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.Memory;

using Anamnesis.Actor.Utilities;
using Anamnesis.Core.Extensions;
using Anamnesis.GameData;
using PropertyChanged;
using System;
using System.Numerics;

public class WeaponMemory : MemoryBase, IEquipmentItemMemory
{
	[Flags]
	public enum WeaponFlagDefs : byte
	{
		WeaponHidden = 1 << 1,
	}

	[Bind(0x000, BindFlags.ActorRefresh | BindFlags.WeaponRefresh)] public ushort Set { get; set; }
	[Bind(0x002, BindFlags.ActorRefresh | BindFlags.WeaponRefresh)] public ushort Base { get; set; }
	[Bind(0x004, BindFlags.ActorRefresh | BindFlags.WeaponRefresh)] public ushort Variant { get; set; }
	[Bind(0x006, BindFlags.ActorRefresh | BindFlags.WeaponRefresh)] public byte Dye { get; set; }
	[Bind(0x007, BindFlags.ActorRefresh | BindFlags.WeaponRefresh)] public byte Dye2 { get; set; }
	[Bind(0x018, BindFlags.Pointer)] public WeaponModelMemory? Model { get; set; }
	[Bind(0x040)] public bool IsSheathed { get; set; }
	[Bind(0x060)] public WeaponFlagDefs WeaponFlags { get; set; }

	[DependsOn(nameof(WeaponFlags), nameof(IsSheathed))]
	public bool WeaponHidden
	{
		get => (this.IsSheathed && this.WeaponFlags.HasFlagUnsafe(WeaponFlagDefs.WeaponHidden)) || (!this.IsSheathed && this.Model?.Transform?.Scale == Vector3.Zero);
		set
		{
			if (value)
			{
				this.WeaponFlags |= WeaponFlagDefs.WeaponHidden;
			}
			else
			{
				this.WeaponFlags &= ~WeaponFlagDefs.WeaponHidden;
			}

			if (this.Model?.Transform == null)
				return;

			// If the weapon is unsheathed (in hands) the visibility flag won't work,
			// so fall back to setting the weapons scale to 0.
			if (!this.IsSheathed)
			{
				this.Model.Transform.Scale = value ? Vector3.Zero : Vector3.One;
			}

			// Special handling for a weapon with 0 scale that has been sheathed attempting to un-hide
			else if (!value && this.Model.Transform.Scale == Vector3.Zero)
			{
				this.Model.Transform.Scale = Vector3.One;
			}
		}
	}

	public void Clear(bool isPlayer)
	{
		bool useEmperorsFists = true;

		if (this.parent is ActorMemory actor)
		{
			if (actor.OffHand == this && actor.MainHand != null)
			{
				IItem? mainHandItem = ItemUtility.GetItem(ItemSlots.MainHand, actor.MainHand.Set, actor.MainHand.Base, actor.MainHand.Variant, actor.IsChocobo);

				if (mainHandItem != null &&
					(mainHandItem.EquipableClasses.HasFlagUnsafe(Classes.Pugilist) ||
					mainHandItem.EquipableClasses.HasFlagUnsafe(Classes.Monk)))
				{
					useEmperorsFists = true;
				}
				else
				{
					useEmperorsFists = false;
				}
			}
		}

		this.Set = useEmperorsFists ? ItemUtility.EmperorsNewFists.ModelSet : (ushort)0;
		this.Base = useEmperorsFists ? ItemUtility.EmperorsNewFists.ModelBase : (ushort)0;
		this.Variant = useEmperorsFists ? ItemUtility.EmperorsNewFists.ModelVariant : (ushort)0;
		this.Dye = 0;
		this.Dye2 = 0;
	}

	public void ApplyDye1(IDye dye)
	{
		this.Dye = (dye == null) ? DyeUtility.NoneDye.Id : dye.Id;
	}

	public void ApplyDye2(IDye dye)
	{
		this.Dye2 = (dye == null) ? DyeUtility.NoneDye.Id : dye.Id;
	}
}
