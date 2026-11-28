using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Globalization;

namespace NightEmber.Controls;

/// <summary>
/// Provides an integer input that supports direct text entry and incremental adjustment.
/// </summary>
/// <remarks>
/// Avalonia's <see cref="Avalonia.Controls.NumericUpDown" /> replaces invalid text with the last value when focus
/// leaves and has no Escape-to-restore, so this control keeps the edit visible with an inline error instead.
/// It composes the built-in <see cref="ButtonSpinner" /> and <see cref="DataValidationErrors" />.
/// </remarks>
public sealed partial class NumericUpDown : UserControl
{
    private readonly bool _ready;
    private bool _updatingText;
    private bool _validateEdits;
    private bool _ignoreSpin;

    /// <summary>
    /// Initializes a new instance of the <see cref="NumericUpDown" /> class.
    /// </summary>
    public NumericUpDown()
    {
        InitializeComponent();
        Spinner.AddHandler(KeyDownEvent, Spinner_PreviewKeyDown, RoutingStrategies.Tunnel);
        Spinner.AddHandler(KeyDownEvent, Spinner_KeyDown, RoutingStrategies.Bubble, handledEventsToo: true);
        ValueTextBox.AddHandler(KeyDownEvent, ValueTextBox_KeyDown, RoutingStrategies.Tunnel);
        ValueTextBox.TextChanging += ValueTextBox_TextChanging;
        ValueTextBox.LostFocus += ValueTextBox_LostFocus;
        _ready = true;
        UpdateText();
    }

    /// <summary>
    /// Gets or sets the lowest accepted value.
    /// </summary>
    [GeneratedStyledProperty]
    public partial int Minimum { get; set; }

    /// <summary>
    /// Gets or sets the highest accepted value.
    /// </summary>
    [GeneratedStyledProperty(DefaultValue = 100)]
    public partial int Maximum { get; set; }

    /// <summary>
    /// Gets or sets the amount applied by the buttons, arrow keys, and mouse wheel.
    /// </summary>
    /// <remarks>Values below 1 are coerced to 1.</remarks>
    [GeneratedStyledProperty(DefaultValue = 1, CoerceMethodName = nameof(CoerceIncrement))]
    public partial int Increment { get; set; }

    /// <summary>
    /// Gets or sets the current numeric value.
    /// </summary>
    /// <remarks>Binds two-way by default and is clamped to <see cref="Minimum" /> and <see cref="Maximum" />.</remarks>
    [GeneratedStyledProperty(DefaultBindingMode = BindingMode.TwoWay, CoerceMethodName = nameof(CoerceNumericValue))]
    public partial int NumericValue { get; set; }

    /// <summary>
    /// Gets the message explaining the current edit's validation error, or an empty string when valid.
    /// </summary>
    [GeneratedDirectProperty(UnsetValue = "")]
    public partial string ErrorMessage { get; private set; } = string.Empty;

    /// <summary>
    /// Commits an integer within the configured range, preserving invalid text
    /// with a validation message until corrected or cancelled.
    /// </summary>
    /// <returns>Whether the edit was valid and committed.</returns>
    public bool CommitEdit()
    {
        if (!ValidateEdit(out var value))
        {
            _validateEdits = true;
            return false;
        }

        SetCurrentValue(NumericValueProperty, value);
        UpdateText();
        return true;
    }

    internal void FocusEditor()
    {
        ValueTextBox.Focus();
        ValueTextBox.BringIntoView();
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);

        // The label's access key focuses this wrapper; the editor inside owns keyboard input.
        if (ReferenceEquals(e.Source, this))
        {
            ValueTextBox.Focus();
            ValueTextBox.SelectAll();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (!_ready)
        {
            return;
        }

        if (change.Property == NumericValueProperty)
        {
            UpdateText();
        }
        else if (change.Property == MinimumProperty || change.Property == MaximumProperty)
        {
            CoerceValue(NumericValueProperty);
            UpdateText();
        }
        else
        {
            EditorAutomation.Forward(change, ValueTextBox);
        }
    }

    private static int CoerceIncrement(AvaloniaObject sender, int value)
    {
        return Math.Max(1, value);
    }

    private static int CoerceNumericValue(AvaloniaObject sender, int value)
    {
        var control = (NumericUpDown)sender;
        return Math.Clamp(value, control.Minimum, control.Maximum);
    }

    private void ChangeValue(int direction)
    {
        // The arrow button takes focus when clicked, so return it to the editor even when the edit is invalid.
        ValueTextBox.Focus();
        if (!CommitEdit())
        {
            return;
        }

        SetCurrentValue(NumericValueProperty, Math.Clamp(NumericValue + direction * Increment, Minimum, Maximum));
        ValueTextBox.SelectAll();
    }

    private void UpdateText()
    {
        if (_updatingText)
        {
            return;
        }

        _updatingText = true;
        ValueTextBox.Text = NumericValue.ToString(CultureInfo.CurrentCulture);
        _updatingText = false;
        _validateEdits = false;
        SetError(string.Empty);
    }

    private bool ValidateEdit(out int value)
    {
        var valid = int.TryParse(ValueTextBox.Text, NumberStyles.None, CultureInfo.CurrentCulture, out value)
                    && value >= Minimum
                    && value <= Maximum;
        SetError(valid ? string.Empty : $"Enter a whole number from {Minimum} to {Maximum}.");
        return valid;
    }

    private void SetError(string message)
    {
        ErrorMessage = message;
        DataValidationErrors.SetErrors(Spinner, message.Length > 0 ? [message] : null);
        Avalonia.Automation.AutomationProperties.SetItemStatus(ValueTextBox, message.Length > 0 ? message : null);
    }

    private void ValueTextBox_TextChanging(object? sender, TextChangingEventArgs e)
    {
        if (!_updatingText && _validateEdits)
        {
            ValidateEdit(out _);
        }
    }

    private void Spinner_PreviewKeyDown(object? sender, KeyEventArgs e)
    {
        // ButtonSpinner spins on Up and Down whatever the modifiers; modified arrows keep their text-editing meaning.
        _ignoreSpin = e.KeyModifiers != KeyModifiers.None && e.Key is Key.Up or Key.Down;
    }

    private void Spinner_KeyDown(object? sender, KeyEventArgs e)
    {
        _ignoreSpin = false;
    }

    private void Spinner_Spin(object? sender, SpinEventArgs e)
    {
        if (_ignoreSpin)
        {
            return;
        }

        ChangeValue(e.Direction == SpinDirection.Increase ? 1 : -1);
    }

    private void ValueTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Up:
                ChangeValue(1);
                e.Handled = true;
                break;
            case Key.Down:
                ChangeValue(-1);
                e.Handled = true;
                break;
            case Key.Enter:
                CommitEdit();
                e.Handled = true;
                break;
            case Key.Escape:
                UpdateText();
                ValueTextBox.SelectAll();
                e.Handled = true;
                break;
        }
    }

    private void ValueTextBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        CommitEdit();
    }
}
