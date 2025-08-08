﻿// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.Actor.Items;

using Anamnesis.GameData;
using Anamnesis.GameData.Sheets;
using Anamnesis.Services;
using System.Windows.Media;

public class DummyNoneDye : IDye
{
	public uint RowId => 0;
	public byte Id => 0;
	public string Name => "None";
	public string? Description => null;
	public ImgRef? Icon => null;
	public Brush? Color => null;

	public bool IsFavorite
	{
		get => FavoritesService.IsFavorite<IDye>(this);
		set => FavoritesService.SetFavorite<IDye>(this, value);
	}
}
