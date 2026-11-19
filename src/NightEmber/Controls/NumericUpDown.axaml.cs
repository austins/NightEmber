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
    /// <summary>
    /// Identifies the <see cref="Minimum" /> styled property.
    /// </summary>
    public static readonly StyledProperty<int> MinimumProperty =
        AvaloniaProperty.Register<NumericUpDown, int>(nameof(Minimum));

    /// <summary>
    /// Identifies the <see cref="Maximum" /> styled property.
    /// </summary>
    public static readonly StyledProperty<int> MaximumProperty =
        AvaloniaProperty.Register<NumericUpDown, int>(nameof(Maximum), 100);

    /// <summary>
    /// Identifies the <see cref="Increment" /> styled property.
    /// </summary>
    public static readonly StyledProperty<int> IncrementProperty =
        AvaloniaProperty.Register<NumericUpDown, int>(
            nameof(Increment),
            1,
            coerce: static (_, value) => Math.Max(1, value));

    /// <summary>
    /// Identifies the <see cref="NumericValue" /> styled property.
    /// </summary>
    public static readonly StyledProperty<int> NumericValueProperty =
        AvaloniaProperty.Register<NumericUpDown, int>(
            nameof(NumericValue),
            defaultBindingMode: BindingMode.TwoWay,
            coerce: static (element, value) =>
            {
                var control = (NumericUpDown)element;
                return Math.Clamp(value, control.Minimum, control.Maximum);
            });

    /// <summary>
    /// Identifies the <see cref="ErrorMessage" /> styled property.
    /// </summary>
    public static readonly StyledProperty<string> ErrorMessageProperty =
        AvaloniaProperty.Register<NumericUpDown, string>(nameof(ErrorMessage), string.Empty);

    private readonly bool _ready;
    private bool _updatingText;
    private bool _validateEdits;

    /// <summary>
    /// Initializes a new instance of the <see cref="NumericUpDown" /> class.
    /// </summary>
    public NumericUpDown()
    {
        InitializeComponent();
        ValueTextBox.AddHandler(KeyDownEvent, ValueTextBox_KeyDown, RoutingStrategies.Tunnel);
        ValueTextBox.TextChanged += ValueTextBox_TextChanged;
        ValueTextBox.LostFocus += ValueTextBox_LostFocus;
        _ready = true;
        UpdateText();
    }

    /// <summary>
    /// Gets or sets the lowest accepted value.
    /// </summary>
    public int Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    /// <summary>
    /// Gets or sets the highest accepted value.
    /// </summary>
    public int Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>
    /// Gets or sets the amount applied by the buttons, arrow keys, and mouse wheel.
    /// </summary>
    public int Increment
    {
        get => GetValue(IncrementProperty);
        set => SetValue(IncrementProperty, value);
    }

    /// <summary>
    /// Gets or sets the current numeric value.
    /// </summary>
    public int NumericValue
    {
        get => GetValue(NumericValueProperty);
        set => SetValue(NumericValueProperty, value);
    }

    /// <summary>
    /// Gets the message explaining the current edit's validation error, or an empty string when valid.
    /// </summary>
    public string ErrorMessage
    {
        get => GetValue(ErrorMessageProperty);
        private set => SetValue(ErrorMessageProperty, value);
    }

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

    private void ChangeValue(int direction)
    {
        if (!CommitEdit())
        {
            return;
        }

        SetCurrentValue(NumericValueProperty, Math.Clamp(NumericValue + direction * Increment, Minimum, Maximum));
        ValueTextBox.Focus();
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

    private void ValueTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_updatingText && _validateEdits)
        {
            ValidateEdit(out _);
        }
    }

    private void Spinner_Spin(object? sender, SpinEventArgs e)
    {
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
