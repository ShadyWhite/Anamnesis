﻿// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.Styles.Controls;

using Anamnesis.Services;
using PropertyChanged;
using System;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using XivToolsWpf.DependencyProperties;
using DrawPoint = System.Drawing.Point;
using WindowsCursor = System.Windows.Forms.Cursor;

/// <summary>
/// Represents an interactive input box that allows the user to slide horizontally to change the value.
/// Inspired by the components in Blender.
/// </summary>
/// <remarks>
/// An interactive input box that allows the user to slide horizontally to change the value.
/// Inspired by the components in Blender.
/// </remarks>
[AddINotifyPropertyChangedInterface]
public partial class SliderInputBox : UserControl
{
	/// <summary>The key for the dragging background color.</summary>
	const string DRAG_BG_KEY = "DraggingBackgroundColor";

	/// <summary>The key for the hover background color.</summary>
	const string HOVER_BG_KEY = "HoverBackgroundColor";

	/// <summary>The key for the normal background color.</summary>
	const string NORMAL_BG_KEY = "NormalBackgroundColor";

	/// <summary>The key for the disabled background color.</summary>
	const string DISABLED_BG_KEY = "DisabledBackgroundColor";

	/// <summary>Minimum distance for a drag action to be recognized.</summary>
	const decimal DRAG_MIN_DISTANCE = 0.01m;

	/// <summary>Multiplier for the key modifiers.</summary>
	const int KEY_MODIFIER_MULTIPLIER = 10;

	/// <summary>Delay for key repeat in milliseconds.</summary>
	const int KEY_REPEAT_DELAY = 10;

	/// <summary>The delay before the key repeat starts in milliseconds.</summary>
	const int KEY_REPEAT_WAIT_DELAY = 250;

	/// <summary>Margin for the tick marks to be drawn on the slider.</summary>
	const int TICK_VISUAL_OFFSET = 2;

	/// <summary>Constant for floating-point number rounding to a whole number.</summary>
	const int INT_ROUNDING = 0;

	/// <summary>The width of the slider thumb rectangle.</summary>
	const double TICK_RECT_WIDTH = 10.0;

	/// <summary> The minimum value limit for this user control.</summary>
	/// <remarks>
	/// Set to half of the decimal's minimum value to avoid overflow issues.
	/// </remarks>
	const decimal MinValueLimit = (decimal.MinValue / 2) + 1;

	/// <summary>The maximum value limit for this user control.</summary>
	/// <remarks>
	/// Set to half of the decimal's maximum value to avoid overflow issues.
	/// </remarks>
	const decimal MaxValueLimit = (decimal.MaxValue / 2) - 1;

	/// <summary>The half-width of the slider thumb rectangle.</summary>
	const double TICK_RECT_HALF_WIDTH = TICK_RECT_WIDTH / 2;

	/// <summary>Dependency property for the slider value.</summary>
	public static readonly IBind<decimal> ValueDp = Binder.Register<decimal, SliderInputBox>(nameof(Value), OnValueChanged, BindMode.TwoWay);

	/// <summary>Dependency property for the default slider value.</summary>
	public static readonly IBind<decimal?> DefaultValueDp = Binder.Register<decimal?, SliderInputBox>(nameof(DefaultValue), BindMode.OneWay);

	/// <summary>Dependency property for the suffix text.</summary>
	public static readonly IBind<string> SuffixDp = Binder.Register<string, SliderInputBox>(nameof(Suffix), OnSuffixChanged, BindMode.OneWay);

	/// <summary>Dependency property for the minimum value.</summary>
	public static readonly IBind<string?> MinDp = Binder.Register<string?, SliderInputBox>(nameof(Minimum), OnMinimumChanged, BindMode.TwoWay);

	/// <summary>Dependency property for the maximum value.</summary>
	public static readonly IBind<string?> MaxDp = Binder.Register<string?, SliderInputBox>(nameof(Maximum), OnMaximumChanged, BindMode.TwoWay);

	/// <summary>Dependency property for the number of decimal places.</summary>
	public static readonly IBind<int?> DecimalPlacesDp = Binder.Register<int?, SliderInputBox>(nameof(DecimalPlaces), OnDecimalPlacesChanged, BindMode.OneWay);

	/// <summary>Dependency property for the slider mode.</summary>
	public static readonly IBind<SliderModes> SliderModeDp = Binder.Register<SliderModes, SliderInputBox>(nameof(SliderMode), BindMode.OneWay);

	/// <summary>Dependency property for the overflow behavior.</summary>
	public static readonly IBind<OverflowModes> OverflowDp = Binder.Register<OverflowModes, SliderInputBox>(nameof(OverflowBehavior), OnOverflowBehaviorChanged, BindMode.OneWay);

	/// <summary>Dependency property for the locked state.</summary>
	public static readonly IBind<bool> LockedDp = Binder.Register<bool, SliderInputBox>(nameof(Locked), BindMode.OneWay);

	/// <summary>Dependency property for the tick frequency.</summary>
	public static readonly IBind<decimal> TickFrequencyDp = Binder.Register<decimal, SliderInputBox>(nameof(TickFrequency), OnTickFrequencyChanged, BindMode.OneWay);

	/// <summary>Dependency property for the border color.</summary>
	public static readonly IBind<string?> BorderColorDp = Binder.Register<string?, SliderInputBox>(nameof(BorderColor), BindMode.OneWay);

	/// <summary>Dependency property for the visibility of the decrease and increase buttons.</summary>
	public static readonly IBind<bool> EnableStepButtonsDp = Binder.Register<bool, SliderInputBox>(nameof(EnableStepButtons), BindMode.OneWay);

	/// <summary>Dependency property for the slider type.</summary>
	public static readonly IBind<SliderTypes> SliderTypeDp = Binder.Register<SliderTypes, SliderInputBox>(nameof(SliderType), OnSliderTypeChanged, BindMode.OneWay);

	/// <summary>Dependency property for the visibility of the tick visualizer.</summary>
	/// <remarks>Applies only to the standard slider mode.</remarks>
	public static readonly IBind<bool> ShowSliderThumbDp = Binder.Register<bool, SliderInputBox>(nameof(ShowSliderThumb), OnShowSliderThumbChanged, BindMode.OneWay);

	/// <summary>Timer for the initial delay before the increase button starts repeating.</summary>
	private readonly DispatcherTimer increaseButtonInitialDelayTimer;

	/// <summary>Timer for the initial delay before the decrease button starts repeating.</summary>
	private readonly DispatcherTimer decreaseButtonInitialDelayTimer;

	/// <summary>Timer for repeating the increase button action.</summary>
	private readonly DispatcherTimer increaseButtonRepeatTimer;

	/// <summary>Timer for repeating the decrease button action.</summary>
	private readonly DispatcherTimer decreaseButtonRepeatTimer;

	/// <summary>The starting point of the mouse drag.</summary>
	private Point startPoint;

	/// <summary>An internal property that indicates whether the slider is currently being dragged.</summary>
	private bool isDragging = false;

	/// <summary>The original position of the mouse when dragging started.</summary>
	private Point originalMousePosition;

	/// <summary>The key that is currently held down.</summary>
	private Key keyHeld = Key.None;

	/// <summary>Indicates whether the value is being set internally to avoid recursive updates.</summary>
	private bool isInternalSet = false;

	/// <summary>The starting value of the slider when the mouse drag started.</summary>
	private double relativeSliderStart;

	/// <summary>Cached minimum of parsed dependency property value.</summary>
	private decimal? parsedMinimum;

	/// <summary>Cached maximum of parsed dependency property value.</summary>
	private decimal? parsedMaximum;

	/// <summary>Preresents the minimum value the user control is using.</summary>
	private decimal EffectiveMinimum => this.parsedMinimum ?? MinValueLimit;

	/// <summary>Preresents the maximum value the user control is using.</summary>
	private decimal EffectiveMaximum => this.parsedMaximum ?? MaxValueLimit;

	/// <summary>
	/// Gets a value that indicates whether the user control has valid lower and upper value bounds.
	/// </summary>
	private bool HasValidBounds => this.EffectiveMinimum < this.EffectiveMaximum;

	/// <summary>
	/// Initializes a new instance of the <see cref="SliderInputBox"/> class and sets up default property values and event handlers.
	/// </summary>
	public SliderInputBox()
	{
		this.InitializeComponent();
		this.ContentArea.DataContext = this;

		// Default property configuration
		this.TickFrequency = 0.01m;
		this.EnableStepButtons = true;
		this.Suffix = string.Empty;
		this.BorderColor = "#00000000";
		this.SliderType = SliderTypes.Modern;
		this.ShowSliderThumb = false;
		this.TickRectangle.Visibility = Visibility.Collapsed;
		this.SliderMode = SliderModes.Absolute;

		this.InputField.IsReadOnly = true;
		this.InputField.IsReadOnlyCaretVisible = false;

		this.OnPropertyChanged(nameof(this.Label));

		// Initialize timers
		this.increaseButtonInitialDelayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(KEY_REPEAT_WAIT_DELAY) };
		this.increaseButtonInitialDelayTimer.Tick += (s, e) =>
		{
			this.increaseButtonInitialDelayTimer.Stop();
			this.increaseButtonRepeatTimer?.Start();
		};

		this.decreaseButtonInitialDelayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(KEY_REPEAT_WAIT_DELAY) };
		this.decreaseButtonInitialDelayTimer.Tick += (s, e) =>
		{
			this.decreaseButtonInitialDelayTimer.Stop();
			this.decreaseButtonRepeatTimer?.Start();
		};

		this.increaseButtonRepeatTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(KEY_REPEAT_DELAY) };
		this.increaseButtonRepeatTimer.Tick += (s, e) => this.TickValue(true);

		this.decreaseButtonRepeatTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(KEY_REPEAT_DELAY) };
		this.decreaseButtonRepeatTimer.Tick += (s, e) => this.TickValue(false);
	}

	/// <summary>Defines the modes for handling value overflow in the slider control.</summary>
	public enum OverflowModes
	{
		/// <summary>
		/// Clamps the value within the minimum and maximum values.
		/// </summary>
		Clamp,

		/// <summary>
		/// Loops the value around when it exceeds the minimum or maximum value.
		/// </summary>
		Loop,
	}

	/// <summary>Enum for slider modes.</summary>
	public enum SliderModes
	{
		/// <summary>
		/// The standard slider mode. The minimum and maximum values are true bounds.
		/// If the user control has no valid bounds, the slider will default to relative mode.
		/// </summary>
		/// <remarks>This is the default mode.</remarks>
		Absolute,

		/// <summary>
		/// The relative slider mode. The minimum and maximum values are relative to the current value.
		/// </summary>
		Relative,
	}

	/// <summary>Enum for the slider types.</summary>
	public enum SliderTypes
	{
		/// <summary>
		/// A modern seemless slider that allows for endless scrolling. Commonly used in 3D modelling software.
		/// </summary>
		Modern,

		/// <summary>
		/// A classic slider that is more akin to the standard slider control in WPF.
		/// </summary>
		Classic
	}

	/// <summary>Gets the width of the tick rectangle.</summary>
	public static double TickRectangleWidth => TICK_RECT_WIDTH;

	/// <summary>Gets or sets the slider value.</summary>
	public decimal Value
	{
		get => this.RoundValue(ValueDp.Get(this));
		set
		{
			// Don't allow the value to be set if the control is locked
			if (this.Locked)
				return;

			decimal newVal = this.Validate(value);
			if (ValueDp.Get(this) == newVal)
				return;

			this.isInternalSet = true;
			ValueDp.Set(this, newVal);
			this.UpdateTickPosition();
			this.isInternalSet = false;
		}
	}

	/// <summary>Gets or sets the default slider value.</summary>
	public decimal? DefaultValue
	{
		get => DefaultValueDp.Get(this);
		set => DefaultValueDp.Set(this, value);
	}

	/// <summary>Gets the label text combining the value and suffix.</summary>
	[DependsOn(nameof(Value), nameof(Suffix), nameof(DecimalPlaces))]
	public string Label => $"{this.FormatValue(this.Value)} {this.Suffix}";


	/// <summary>Gets or sets the suffix text.</summary>
	public string Suffix
	{
		get => SuffixDp.Get(this);
		set
		{
			this.isInternalSet = true;
			SuffixDp.Set(this, value);
			this.isInternalSet = false;
		}
	}


	/// <summary>Gets or sets a value indicating whether the input field is active.</summary>
	public bool IsInputFieldActive
	{
		get => !this.InputField.IsReadOnly;
		set
		{
			this.InputField.IsReadOnly = !value;
			this.ContentArea.Background = new SolidColorBrush((Color)this.FindResource(value || !this.IsMouseHovered ? NORMAL_BG_KEY : HOVER_BG_KEY));
		}
	}

	/// <summary>Gets or sets the tick frequency.</summary>
	public decimal TickFrequency
	{
		get => TickFrequencyDp.Get(this);
		set
		{
			this.isInternalSet = true;
			TickFrequencyDp.Set(this, value);
			this.isInternalSet = false;
		}
	}

	/// <summary>Gets or sets the minimum value.</summary>
	public string? Minimum
	{
		get => MinDp.Get(this);
		set
		{
			this.isInternalSet = true;

			var parsed = ParseBoundValue(value);
			if (parsed != null && parsed > this.EffectiveMaximum)
			{
				this.parsedMinimum = this.parsedMaximum;
				MinDp.Set(this, this.Maximum);
			}
			else
			{
				this.parsedMinimum = parsed;
				MinDp.Set(this, value);
			}

			this.isInternalSet = false;
		}
	}

	/// <summary>Gets or sets the maximum value.</summary>
	public string? Maximum
	{
		get => MaxDp.Get(this);
		set
		{
			this.isInternalSet = true;

			var parsed = ParseBoundValue(value);
			if (parsed != null && parsed < this.EffectiveMinimum)
			{
				this.parsedMaximum = this.parsedMinimum;
				MaxDp.Set(this, this.Minimum);
			}
			else
			{
				this.parsedMaximum = parsed;
				MaxDp.Set(this, value);
			}

			this.isInternalSet = false;
		}
	}

	/// <summary>
	/// Gets or sets the number of decimal places to round to. Use "0" to round to the nearest whole number.
	/// </summary>
	public int? DecimalPlaces
	{
		get => DecimalPlacesDp.Get(this);
		set
		{
			Debug.Assert(value >= 0, "You cannot set a negative number to the decimal places property");
			this.isInternalSet = true;
			DecimalPlacesDp.Set(this, value);
			this.isInternalSet = false;
			this.Value = Math.Round(this.Value, value ?? INT_ROUNDING);
		}
	}

	/// <summary>Gets or sets the slider mode.</summary>
	public SliderModes SliderMode
	{
		get => SliderModeDp.Get(this);
		set
		{
			this.isInternalSet = true;
			SliderModeDp.Set(this, value);
			this.isInternalSet = false;
		}
	}

	/// <summary>Gets or sets the overflow behavior.</summary>
	public OverflowModes OverflowBehavior
	{
		get => OverflowDp.Get(this);
		set
		{
			this.isInternalSet = true;
			OverflowDp.Set(this, value);
			this.RefreshStepButtonState();
			this.isInternalSet = false;
		}
	}

	/// <summary>Gets or sets a value indicating whether the control is locked.</summary>
	public bool Locked
	{
		get => LockedDp.Get(this);
		set => LockedDp.Set(this, value);
	}

	/// <summary>Gets or sets the border color.</summary>
	public string? BorderColor
	{
		get => BorderColorDp.Get(this);
		set => BorderColorDp.Set(this, value);
	}

	/// <summary>Gets or sets a value indicating whether to enable the decrease and increase buttons.</summary>
	public bool EnableStepButtons
	{
		get => EnableStepButtonsDp.Get(this);
		set => EnableStepButtonsDp.Set(this, value);
	}

	/// <summary>
	/// Gets a value indicating whether the mouse is hovered over the user control.
	/// </summary>
	public bool IsMouseHovered { get; private set; } = false;

	/// <summary>
	/// Gets a value indicating whether to show the decrease and increase buttons.
	/// </summary>
	[DependsOn(nameof(IsMouseHovered), nameof(IsInputFieldActive), nameof(SliderType), nameof(ShowSliderThumb))]
	public bool ShowStepButtons => this.EnableStepButtons && (this.IsMouseHovered || this.ShowSliderThumb || this.SliderType == SliderTypes.Classic) && !this.IsInputFieldActive;

	/// <summary>Gets or sets a value indicating whether the control is in Classic slider mode.</summary>
	public SliderTypes SliderType
	{
		get => SliderTypeDp.Get(this);
		set
		{
			this.isInternalSet = true;
			SliderTypeDp.Set(this, value);
			this.isInternalSet = false;
		}
	}

	/// <summary>Gets or sets a value indicating whether to show the tick visualizer.</summary>
	/// <remarks>Applies only to the standard slider mode.</remarks>
	public bool ShowSliderThumb
	{
		get => ShowSliderThumbDp.Get(this);
		set => ShowSliderThumbDp.Set(this, value);
	}

	/// <summary>
	/// Programmatically gives focus to the control.
	/// </summary>
	/// <param name="enableInputField">
	/// True to activate the input field (editable text mode); false to focus the control without enabling direct text input.
	/// </param>
	public void GainFocus(bool enableInputField)
	{
		if (!this.IsEnabled)
			return;

		if (enableInputField)
		{
			this.IsInputFieldActive = true;
			this.InputField.Focus();
			this.InputField.SelectAll();
		}
		else
		{
			// Focus the user control without enabling text edit mode
			this.Focus();
			Keyboard.Focus(this);
		}

		this.UpdateMouseCursor();
	}

	/// <summary>
	/// Programmatically commits text input (if text field is active) and releases focus.
	/// </summary>
	public void LoseFocus()
	{
		if (!this.IsEnabled)
			return;

		if (this.IsInputFieldActive)
		{
			this.ParseInputField();
			this.IsInputFieldActive = false;
		}

		// Clear focus
		this.OnWindowEvent(this, EventArgs.Empty);

		// Ensure that we update the cursor state if the mouse leaves the control
		this.UpdateMouseCursor();
	}

	/// <summary>Handles changes to the slider value.</summary>
	/// <param name="sender">The sender.</param>
	/// <param name="val">The new value.</param>
	private static void OnValueChanged(SliderInputBox sender, decimal val)
	{
		if (sender.isInternalSet)
			return;

		sender.Value = sender.Validate(val);
		sender.UpdateTickPosition();
		sender.OnPropertyChanged(nameof(Label));
	}

	/// <summary>Handles changes to the tick frequency.</summary>
	/// <param name="sender">The sender.</param>
	/// <param name="value">The new value.</param>
	private static void OnTickFrequencyChanged(SliderInputBox sender, decimal value)
	{
		if (sender.isInternalSet)
			return;

		Debug.Assert(value >= 0, "Tick frequency must be non-negative.");
		sender.TickFrequency = value;
	}

	/// <summary>Handles changes to the minimum value.</summary>
	/// <param name="sender">The sender.</param>
	/// <param name="value">The new value.</param>
	private static void OnMinimumChanged(SliderInputBox sender, string? value)
	{
		if (sender.isInternalSet)
			return;

		var parsed = ParseBoundValue(value);
		if (parsed != null && sender.parsedMaximum != null && parsed > sender.parsedMaximum)
		{
			sender.parsedMinimum = sender.parsedMaximum;
			sender.Minimum = sender.Maximum;
		}
		else
		{
			sender.parsedMinimum = parsed;
			sender.Minimum = value;
		}

		sender.UpdateTickPosition();
	}

	/// <summary>Handles changes to the maximum value.</summary>
	/// <param name="sender">The sender.</param>
	/// <param name="value">The new value.</param>
	private static void OnMaximumChanged(SliderInputBox sender, string? value)
	{
		if (sender.isInternalSet)
			return;

		var parsed = ParseBoundValue(value);
		if (parsed != null && sender.parsedMinimum != null && parsed < sender.parsedMinimum)
		{
			sender.parsedMaximum = sender.parsedMinimum;
			sender.Maximum = sender.Minimum;
		}
		else
		{
			sender.parsedMaximum = parsed;
			sender.Maximum = value;
		}

		sender.UpdateTickPosition();
	}

	/// <summary>
	/// Handles changes to the amount of decimal places that should be visible.
	/// </summary>
	/// <param name="sender">The sender.</param>
	/// <param name="value">The new value.</param>
	private static void OnDecimalPlacesChanged(SliderInputBox sender, int? value)
	{
		if (sender.isInternalSet)
			return;

		Debug.Assert(value >= 0, "You cannot set a negative number to the decimal places property");
		sender.DecimalPlaces = value;
		sender.OnPropertyChanged(nameof(Label));
	}

	/// <summary>
	/// Handles changes to the overflow behavior property.
	/// </summary>
	/// <param name="sender">The sender.</param>
	/// <param name="value">The new value.</param>
	private static void OnOverflowBehaviorChanged(SliderInputBox sender, OverflowModes value)
	{
		if (sender.isInternalSet)
			return;

		sender.OverflowBehavior = value;
		sender.RefreshStepButtonState();
	}

	/// <summary>
	/// Handles changes to the suffix text.
	/// </summary>
	/// <param name="sender">The sender.</param>
	/// <param name="value">The new value.</param>
	private static void OnSuffixChanged(SliderInputBox sender, string value)
	{
		if (sender.isInternalSet)
			return;

		sender.OnPropertyChanged(nameof(Label));
	}

	/// <summary>
	/// Handles changes to the slider type.
	/// </summary>
	/// <param name="sender">The sender.</param>
	/// <param name="value">The new value.</param>
	private static void OnSliderTypeChanged(SliderInputBox sender, SliderTypes value)
	{
		if (sender.isInternalSet)
			return;

		sender.OnPropertyChanged(nameof(ShowStepButtons));
		sender.UpdateTickPosition();
	}

	/// <summary>
	/// Handles changes to the show tick visualizer property.
	/// </summary>
	/// <param name="sender">The sender.</param>
	/// <param name="value">The new value.</param>
	private static void OnShowSliderThumbChanged(SliderInputBox sender, bool value)
	{
		if (sender.isInternalSet)
			return;

		sender.OnPropertyChanged(nameof(ShowStepButtons));
		sender.UpdateTickPosition();
	}

	/// <summary>
	/// Applies the key modifier to the provided value.
	/// </summary>
	/// <param name="value"></param>
	/// <returns></returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static decimal ApplyKeyModifiers(decimal value)
	{
		if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
			value *= KEY_MODIFIER_MULTIPLIER;

		if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
			value /= KEY_MODIFIER_MULTIPLIER;

		return value;
	}

	/// <summary>
	/// Parses the lower/upper bound value from the dependency property string.
	/// </summary>
	/// <param name="value">The string value to parse.</param>
	/// <returns>The parsed decimal value, or null if parsing fails.</returns>
	private static decimal? ParseBoundValue(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var dec))
			return dec;

		// Attempt to parse symbolic values (e.g., "System.float.MaxValue")
		var parts = value.Split('.');
		if (parts.Length != 2)
			return null; // Invalid format

		try
		{
			var type = Type.GetType("System." + parts[0], false, true);
			if (type != null)
			{
				// Assert for unsupported types
				Debug.Assert(type != typeof(double), "SliderInputBox does not support double min/max range.");
				Debug.Assert(type != typeof(ulong), "SliderInputBox does not support ulong min/max range.");

				var field = type.GetField(parts[1], System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.IgnoreCase);
				if (field != null)
				{
					var val = field.GetValue(null);
					return Convert.ToDecimal(val, CultureInfo.InvariantCulture);
				}
			}
		}
		catch
		{
			// Ignore exceptions
		}

		return null; // Fallback
	}

	/// <summary>Handles the control's loaded event.</summary>
	/// <param name="sender">The sender.</param>
	/// <param name="e">The event arguments.</param>
	private void OnLoaded(object sender, RoutedEventArgs e) => this.AttachWindowEvents(true);

	/// <summary>Handles the control's unloaded event.</summary>
	/// <param name="sender">The sender.</param>
	/// <param name="e">The event arguments.</param>
	private void OnUnloaded(object sender, RoutedEventArgs e) => this.AttachWindowEvents(false);

	/// <summary>Handles the preview mouse button down event.</summary>
	/// <param name="sender">The sender.</param>
	/// <param name="e">The event arguments.</param>
	private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
	{
		if (e.LeftButton == MouseButtonState.Pressed)
		{
			this.startPoint = e.GetPosition(this);
			this.originalMousePosition = this.PointToScreen(this.startPoint);
			this.isDragging = false;

			if (!this.IsInputFieldActive)
			{
				if (this.SliderType == SliderTypes.Classic)
				{
					// Open the input field if the control key is held down
					if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
					{
						this.GainFocus(true);
						e.Handled = true;
						return;
					}
					else
					{
						if (this.SliderMode == SliderModes.Absolute && this.HasValidBounds)
						{
							Point relPointInputArea = this.TranslatePoint(this.startPoint, this.InputArea);

							// Else, calculate the new value based on the tick position
							double clampedMouseX = Math.Max(0, relPointInputArea.X - TICK_VISUAL_OFFSET - TICK_RECT_HALF_WIDTH);
							double relVal = clampedMouseX / (this.InputArea.ActualWidth - (2 * TICK_VISUAL_OFFSET) - TICK_RECT_WIDTH);
							this.Value = Math.Clamp(((decimal)relVal * (this.EffectiveMaximum - this.EffectiveMinimum)) + this.EffectiveMinimum, this.EffectiveMinimum, this.EffectiveMaximum);

							this.UpdateTickPosition();
						}
						else if (this.SliderMode == SliderModes.Relative || !this.HasValidBounds)
						{
							this.relativeSliderStart = (double)this.Value;
						}
					}
				}

				this.InputArea.CaptureMouse();

				e.Handled = true;
				return;
			}
		}
		else if (e.MiddleButton == MouseButtonState.Pressed && this.DefaultValue != null)
		{
			if (!this.IsInputFieldActive)
			{
				this.Value = (decimal)this.DefaultValue;

				e.Handled = true;
				return;
			}
		}
	}

	/// <summary>Handles the preview mouse move event.</summary>
	/// <param name="sender">The sender.</param>
	/// <param name="e">The event arguments.</param>
	private void OnPreviewMouseMove(object sender, MouseEventArgs e)
	{
		if (e.LeftButton == MouseButtonState.Pressed && !this.IsInputFieldActive)
		{
			Point currentPoint = e.GetPosition(this);
			Vector diff = currentPoint - this.startPoint;

			if (this.SliderType == SliderTypes.Classic)
			{
				this.isDragging = true;

				if (this.SliderMode == SliderModes.Absolute && this.HasValidBounds)
				{
					Point relPointInputArea = this.TranslatePoint(currentPoint, this.InputArea);

					// Calculate the new value based on the tick position
					double clampedMouseX = Math.Max(0, relPointInputArea.X - TICK_VISUAL_OFFSET - TICK_RECT_HALF_WIDTH);
					double relVal = clampedMouseX / (this.InputArea.ActualWidth - (2 * TICK_VISUAL_OFFSET) - TICK_RECT_WIDTH);
					this.Value = Math.Clamp(((decimal)relVal * (this.EffectiveMaximum - this.EffectiveMinimum)) + this.EffectiveMinimum, this.EffectiveMinimum, this.EffectiveMaximum);

					// Handle cursor overflow for classic slider type
					this.HandleCursorOverflow();
				}
				else if (this.SliderMode == SliderModes.Relative || !this.HasValidBounds)
				{
					if (diff.Length >= (double)DRAG_MIN_DISTANCE)
					{
						decimal delta = (decimal)diff.X * this.TickFrequency * (decimal)SettingsService.Current.SliderSensitivity;
						this.Value = (decimal)this.relativeSliderStart + ApplyKeyModifiers(delta);
					}
				}
			}
			else
			{
				if (diff.Length >= (double)DRAG_MIN_DISTANCE)
				{
					if (!this.isDragging)
					{
						Mouse.OverrideCursor = Cursors.None;
						this.ContentArea.Background = new SolidColorBrush((Color)this.FindResource(DRAG_BG_KEY));
					}

					this.isDragging = true;

					// Adjust the value based on horizontal movement
					decimal delta = (decimal)diff.X * this.TickFrequency * (decimal)SettingsService.Current.SliderSensitivity;
					this.Value += ApplyKeyModifiers(delta);

					// Restore the cursor position
					WindowsCursor.Position = new DrawPoint((int)this.originalMousePosition.X, (int)this.originalMousePosition.Y);
				}
			}
		}
	}

	/// <summary>Handles the preview mouse left button up event.</summary>
	/// <param name="sender">The sender.</param>
	/// <param name="e">The event arguments.</param>
	private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (!this.isDragging && !this.IsInputFieldActive && this.SliderType == SliderTypes.Modern)
		{
			this.GainFocus(true);
		}
		else if (this.isDragging)
		{
			this.UpdateMouseCursor();
			this.ContentArea.Background = new SolidColorBrush((Color)this.FindResource(this.IsMouseHovered ? HOVER_BG_KEY : NORMAL_BG_KEY));
		}

		this.isDragging = false;

		// Release the mouse capture
		this.InputArea.ReleaseMouseCapture();
	}

	/// <summary>Handles the mouse enter event for the content area.</summary>
	/// <param name="sender">The sender.</param>
	/// <param name="e">The event arguments.</param>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void OnMouseEnter(object sender, MouseEventArgs e)
	{
		this.IsMouseHovered = true;
		if (this.IsMouseHovered && !this.IsInputFieldActive)
		{
			this.ContentArea.Background = new SolidColorBrush((Color)this.FindResource(HOVER_BG_KEY));
		}
	}

	/// <summary>Handles the mouse leave event for the content area.</summary>
	/// <param name="sender">The sender.</param>
	/// <param name="e">The event arguments.</param>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void OnMouseLeave(object sender, MouseEventArgs e)
	{
		this.IsMouseHovered = false;
		if (!this.IsInputFieldActive)
		{
			this.ContentArea.Background = new SolidColorBrush((Color)this.FindResource(NORMAL_BG_KEY));
		}
	}

	/// <summary>Handles the lost focus event for the input field.</summary>
	/// <param name="sender">The sender.</param>
	/// <param name="e">The event arguments.</param>
	private void OnLostFocus(object sender, RoutedEventArgs e) => this.LoseFocus();

	/// <summary>Handles the preview key down event.</summary>
	/// <param name="sender">The sender.</param>
	/// <param name="e">The event arguments.</param>
	private void OnPreviewKeyDown(object sender, KeyEventArgs e)
	{
		base.OnPreviewKeyDown(e);

		if (this.IsInputFieldActive && (e.Key == Key.Enter || e.Key == Key.Escape))
		{
			// Commit the changes
			this.ParseInputField();

			// Close the input field
			if (e.Key == Key.Escape || !SettingsService.Current.SliderKeepInputOpenOnCommit)
			{
				Keyboard.ClearFocus();
				this.IsInputFieldActive = false;
			}

			e.Handled = true;
		}

		if (e.Key == Key.Up || e.Key == Key.Down)
		{
			e.Handled = true;

			if (e.IsRepeat)
			{
				if (this.keyHeld == e.Key)
					return;

				this.keyHeld = e.Key;
				Task.Run(this.TickHeldKey);
			}
			else
			{
				this.TickKey(e.Key);
			}
		}
	}

	/// <summary>Handles the decrease button click event.</summary>
	/// <param name="sender">The sender.</param>
	/// <param name="e">The event arguments.</param>
	private void OnDecreaseButtonClicked(object sender, RoutedEventArgs e)
	{
		if (this.isDragging)
			return;

		this.TickValue(false);
	}

	/// <summary>
	/// Handles the preview mouse down event for the decrease button.
	/// </summary>
	/// <param name="sender">The sender.</param>
	/// <param name="e">The event arguments.</param>
	private void OnDecreaseButtonPreviewMouseDown(object sender, MouseButtonEventArgs e)
	{
		this.decreaseButtonInitialDelayTimer.Start();
	}

	/// <summary>Handles the increase button click event.</summary>
	/// <param name="sender">The sender.</param>
	/// <param name="e">The event arguments.</param>
	private void OnIncreaseButtonClicked(object sender, RoutedEventArgs e)
	{
		if (this.isDragging)
			return;

		this.TickValue(true);
	}

	/// <summary>
	/// Handles the preview mouse down event for the increase button.
	/// </summary>
	/// <param name="sender">The sender.</param>
	/// <param name="e">The event arguments.</param>
	private void OnIncreaseButtonPreviewMouseDown(object sender, MouseButtonEventArgs e)
	{
		this.increaseButtonInitialDelayTimer.Start();
	}

	/// <summary>
	/// Handles the preview mouse up event for the UserControl.
	/// </summary>
	/// <param name="sender">The sender.</param>
	/// <param name="e">The event arguments.</param>
	private void OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
	{
		this.increaseButtonInitialDelayTimer.Stop();
		this.increaseButtonRepeatTimer.Stop();
		this.decreaseButtonInitialDelayTimer.Stop();
		this.decreaseButtonRepeatTimer.Stop();
	}

	/// <summary>Handles the mouse enter event for the input area.</summary>
	/// <param name="sender">The sender.</param>
	/// <param name="e">The event arguments.</param>
	private void OnInputAreaMouseEnter(object sender, MouseEventArgs e)
	{
		if (!this.IsInputFieldActive &&
			(this.SliderType == SliderTypes.Modern || (this.SliderType == SliderTypes.Classic && this.SliderMode == SliderModes.Relative)))
		{
			Mouse.OverrideCursor = Cursors.SizeWE;
		}
	}

	/// <summary>Handles the mouse leave event for the input area.</summary>
	/// <param name="sender">The sender.</param>
	/// <param name="e">The event arguments.</param>
	private void OnInputAreaMouseLeave(object sender, MouseEventArgs e)
	{
		Mouse.OverrideCursor = null;
	}

	/// <summary>Handles the preview key up event.</summary>
	/// <param name="sender">The sender.</param>
	/// <param name="e">The event arguments.</param>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void OnPreviewKeyUp(object sender, KeyEventArgs e)
	{
		base.OnPreviewKeyUp(e);

		if (this.keyHeld == e.Key)
		{
			e.Handled = true;
			this.keyHeld = Key.None;
		}
	}

	/// <summary>Handles the mouse wheel event.</summary>
	/// <param name="sender">The sender.</param>
	/// <param name="e">The event arguments.</param>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void OnMouseWheel(object sender, MouseWheelEventArgs e)
	{
		this.TickValue(e.Delta > 0);
		e.Handled = true;
	}

	/// <summary>Handles the is enabled changed event to update the background color.</summary>
	/// <param name="sender">The sender.</param>
	/// <param name="e">The event arguments.</param>
	private void OnIsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		if ((bool)e.NewValue)
			this.ContentArea.Background = new SolidColorBrush((Color)this.FindResource(this.IsMouseHovered ? HOVER_BG_KEY : NORMAL_BG_KEY));
		else
			this.ContentArea.Background = new SolidColorBrush((Color)this.FindResource(DISABLED_BG_KEY));
	}

	/// <summary>
	/// Handles the tick value change based on the direction.
	/// </summary>
	/// <param name="increase">If set to <c>true</c> increases the value; otherwise, decreases it.</param>
	private void TickValue(bool increase)
	{
		this.Value += ApplyKeyModifiers(increase ? this.TickFrequency : -this.TickFrequency);
	}

	/// <summary>
	/// Continuously ticks the held key to change the value.
	/// </summary>
	/// <returns>A task representing the asynchronous operation.</returns>
	private async Task TickHeldKey()
	{
		while (this.keyHeld != Key.None)
		{
			await Application.Current.Dispatcher.InvokeAsync(() =>
			{
				this.TickKey(this.keyHeld);
			});

			await Task.Delay(KEY_REPEAT_DELAY);
		}
	}

	/// <summary>
	/// Adjusts the value based on the pressed key.
	/// </summary>
	/// <param name="key">The key that was pressed.</param>
	private void TickKey(Key key)
	{
		if (key == Key.Up)
		{
			this.TickValue(true);
		}
		else if (key == Key.Down)
		{
			this.TickValue(false);
		}
	}

	/// <summary>
	/// Rounds the value to the specified number of decimal places.
	/// </summary>
	/// <param name="val">The value to round.</param>
	/// <returns>The rounded value.</returns>
	private decimal RoundValue(decimal val) => Math.Round(val, this.DecimalPlaces ?? INT_ROUNDING, MidpointRounding.AwayFromZero);

	/// <summary>
	/// Validates and adjusts the new value based on the overflow behavior and decimal places.
	/// </summary>
	/// <param name="val">The new value to validate.</param>
	/// <returns>The validated value.</returns>
	private decimal Validate(decimal val)
	{
		decimal min = this.EffectiveMinimum;
		decimal max = this.EffectiveMaximum;

		if (this.OverflowBehavior == OverflowModes.Clamp)
		{
			val = Math.Clamp(val, min, max);

			this.DecreaseButton.IsEnabled = val > min;
			this.IncreaseButton.IsEnabled = val < max;
		}
		else if (this.OverflowBehavior == OverflowModes.Loop && this.Minimum != null && this.Maximum != null)
		{
			if (val < min)
				val = max;
			else if (val > max)
				val = min;
		}

		return val;
	}

	/// <summary>
	/// Formats the value to a string with the specified number of decimal places.
	/// </summary>
	/// <param name="value">The value to format.</param>
	/// <returns>The the formatted value as string.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private string FormatValue(decimal value)
	{
		int decimalPlaces = this.DecimalPlaces ?? INT_ROUNDING;
		string format = $"0.{new string('#', decimalPlaces)}";
		return value.ToString(format, CultureInfo.InvariantCulture);
	}

	/// <summary>
	/// Updates the position of the tick mark based on the current value.
	/// </summary>
	private void UpdateTickPosition()
	{
		// Update visibility based on the slider thumb property and IsClassicSliderMode
		this.TickRectangle.Visibility = this.ShowSliderThumb || this.SliderType == SliderTypes.Classic ? Visibility.Visible : Visibility.Collapsed;

		// Update the tick position based on the value
		// Skip update if component is hidden
		if (this.EffectiveMaximum > this.EffectiveMinimum && this.TickRectangle.Visibility == Visibility.Visible)
		{
			decimal range = this.EffectiveMaximum - this.EffectiveMinimum;

			// Prevent division by zero or very small range that would result in overflow
			if (range == 0 || decimal.Abs(range) < 1e-28m)
				return;

			decimal safeValue = Math.Clamp(this.Value, this.EffectiveMinimum, this.EffectiveMaximum);
			decimal relativeValue = (safeValue - this.EffectiveMinimum) / range;
			double tickPosition = TICK_VISUAL_OFFSET + ((double)relativeValue * (this.InputArea.ActualWidth - TICK_RECT_WIDTH - (2 * TICK_VISUAL_OFFSET)));

			this.TickRectangle.Margin = new Thickness(tickPosition, 0, 0, 0);
			this.TickRectangle.UpdateLayout();
		}
	}

	/// <summary>
	/// Handles the cursor overflow behavior for the classic slider type.
	/// </summary>
	private void HandleCursorOverflow()
	{
		if (this.SliderType == SliderTypes.Classic && this.OverflowBehavior == OverflowModes.Loop)
		{
			Point rightEdge = this.InputArea.PointToScreen(new Point(this.InputArea.ActualWidth - TICK_RECT_HALF_WIDTH, this.InputArea.ActualHeight / 2));
			Point leftEdge = this.InputArea.PointToScreen(new Point(TICK_RECT_HALF_WIDTH, this.InputArea.ActualHeight / 2));

			if (WindowsCursor.Position.X > rightEdge.X)
			{
				WindowsCursor.Position = new DrawPoint((int)leftEdge.X, (int)leftEdge.Y);
			}

			if (WindowsCursor.Position.X < leftEdge.X)
			{
				WindowsCursor.Position = new DrawPoint((int)rightEdge.X, (int)rightEdge.Y);
			}
		}
	}

	/// <summary>
	/// Parses the input field text and updates the slider value.
	/// </summary>
	private void ParseInputField()
	{
		string inputText = this.InputField.Text;

		if (decimal.TryParse(inputText, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal newValue))
		{
			this.Value = newValue;
		}
		else
		{
			try
			{
				double result = Convert.ToDouble(new DataTable().Compute(inputText.Replace(',', '.'), null));
				this.Value = (decimal)result;
			}
			catch (Exception)
			{
				// Ignore failed parsing and keep the current value
			}
		}

		this.InputField.Text = this.Value.ToString(CultureInfo.InvariantCulture);
	}

	/// <summary>
	/// Enables or disables the step buttons based on the overflow behavior.
	/// </summary>
	private void RefreshStepButtonState()
	{
		if (this.OverflowBehavior == OverflowModes.Clamp)
		{
			this.DecreaseButton.IsEnabled = this.Value > this.EffectiveMinimum;
			this.IncreaseButton.IsEnabled = this.Value < this.EffectiveMaximum;
		}
		else
		{
			this.DecreaseButton.IsEnabled = true;
			this.IncreaseButton.IsEnabled = true;
		}
	}

	/// <summary>
	/// Refreshes the mouse cursor based on the current state.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void UpdateMouseCursor()
	{
		Mouse.OverrideCursor = (this.IsMouseHovered && (this.SliderType == SliderTypes.Modern || (this.SliderType == SliderTypes.Classic && this.SliderMode == SliderModes.Relative))) ? Cursors.SizeWE : null;
	}

	/// <summary>
	/// Handles the context menu opening event to open the input field.
	/// </summary>
	/// <param name="sender">The sender.</param>
	/// <param name="e">The event arguments.</param>
	private void OnPromptInputFieldClick(object sender, RoutedEventArgs e)
	{
		this.IsInputFieldActive = true;
		this.InputField.Focus();
		this.InputField.SelectAll();
	}

	/// <summary>
	/// Handles the input field size changed event to update the tick position.
	/// </summary>
	/// <param name="sender">The sender.</param>
	/// <param name="e">The event arguments.</param>
	private void OnInputFieldSizeChanged(object sender, SizeChangedEventArgs e)
	{
		// Wait for the layout to fully load before updating the tick position
		this.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(this.UpdateTickPosition));
	}

	/// <summary>
	/// Attaches or detaches window events.
	/// </summary>
	/// <param name="attach">If set to <c>true</c> attaches the events; otherwise, detaches them.</param>
	private void AttachWindowEvents(bool attach)
	{
		Window window = Window.GetWindow(this);
		if (window != null)
		{
			if (attach)
			{
				window.MouseDown += this.OnWindowEvent;
				window.Deactivated += this.OnWindowEvent;
			}
			else
			{
				window.MouseDown -= this.OnWindowEvent;
				window.Deactivated -= this.OnWindowEvent;
			}
		}
	}

	/// <summary>Handles window events to clear focus.</summary>
	/// <param name="sender">The sender.</param>
	/// <param name="e">The event arguments.</param>
	private void OnWindowEvent(object? sender, EventArgs e)
	{
		FocusManager.SetFocusedElement(FocusManager.GetFocusScope(this), null);
		Keyboard.ClearFocus();
	}
}
