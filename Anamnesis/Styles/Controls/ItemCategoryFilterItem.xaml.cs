﻿// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.Styles.Controls;

using Anamnesis.Core.Extensions;
using Anamnesis.GameData;
using PropertyChanged;
using System.ComponentModel;
using System.Windows.Controls;
using XivToolsWpf.DependencyProperties;

/// <summary>
/// Interaction logic for ItemCategoryFilterItem.xaml.
/// </summary>
[AddINotifyPropertyChangedInterface]
public partial class ItemCategoryFilterItem : UserControl, INotifyPropertyChanged
{
	public static readonly DependencyProperty<ItemCategories> ValueDp = Binder.Register<ItemCategories, ItemCategoryFilterItem>(nameof(ItemCategoryFilterItem.Value), OnValueChanged);
	public static readonly DependencyProperty<ItemCategories> CategoryDp = Binder.Register<ItemCategories, ItemCategoryFilterItem>(nameof(ItemCategoryFilterItem.Category));

	public ItemCategoryFilterItem()
	{
		this.InitializeComponent();
		this.ContentArea.DataContext = this;
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	public string? Text { get; set; }
	public string? ToolText { get; set; }

	public ItemCategories Value
	{
		get => ValueDp.Get(this);
		set => ValueDp.Set(this, value);
	}

	public ItemCategories Category
	{
		get => CategoryDp.Get(this);
		set => CategoryDp.Set(this, value);
	}

	public bool IsSelected
	{
		get => this.Value.HasFlagUnsafe(this.Category);
		set => this.Value = this.Value.SetFlag(this.Category, value);
	}

	private static void OnValueChanged(ItemCategoryFilterItem sender, ItemCategories value)
	{
		sender.PropertyChanged?.Invoke(sender, new PropertyChangedEventArgs(nameof(ItemCategoryFilterItem.IsSelected)));
	}
}
