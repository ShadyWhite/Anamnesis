﻿// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.Actor.Items;

using Anamnesis.GameData;
using Anamnesis.GameData.Sheets;
using Anamnesis.Services;
using Anamnesis.TexTools;

public class EmperorsEquipItem : IItem
{
	public string Name => LocalizationService.GetString("Item_EmperorsBody");
	public string Description => LocalizationService.GetString("Item_EmperorsBodyDesc");
	public ImageReference? Icon => GameDataService.Items.GetRow(10033).Icon;
	public ulong Model => (ulong)279 | ((ulong)1 << 16);
	public ushort ModelBase => 279;
	public ushort ModelVariant => 1;
	public ushort ModelSet => 0;
	public uint RowId => 0;
	public bool IsWeapon => false;
	public bool HasSubModel => false;
	public ulong SubModel => 0;
	public ushort SubModelBase => 0;
	public ushort SubModelVariant => 0;
	public ushort SubModelSet => 0;
	public Classes EquipableClasses => Classes.All;
	public Mod? Mod => TexToolsService.GetMod(this.Name);
	public byte EquipLevel => 0;

	public bool IsFavorite
	{
		get => FavoritesService.IsFavorite<IItem>(this);
		set => FavoritesService.SetFavorite<IItem>(this, nameof(FavoritesService.Favorites.Items), value);
	}

	public bool CanOwn => false;
	public bool IsOwned { get; set; }

	public ItemCategories Category => ItemCategories.Standard;

	public bool FitsInSlot(ItemSlots slot)
	{
		return slot == ItemSlots.Head || slot == ItemSlots.Body || slot == ItemSlots.Feet || slot == ItemSlots.Hands || slot == ItemSlots.Legs;
	}
}
