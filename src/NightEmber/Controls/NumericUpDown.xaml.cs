using System.Globalization;
using System.Windows;
using System.Windows.Input;

namespace NightEmber.Controls;

/// <summary>
/// Provides an integer input that supports direct text entry and incremental adjustment.
/// </summary>
public sealed partial class NumericUpDown : System.Windows.Controls.UserControl
{
    /// <summary>
    /// Identifies the <see cref="Minimum" /> dependency property.
    /// </summary>
    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum),
        typeof(int),
        typeof(NumericUpDown),
        new FrameworkPropertyMetadata(0, OnRangeChanged));

    /// <summary>
    /// Identifies the <see cref="Maximum" /> dependency property.
    /// </summary>
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum),
        typeof(int),
        typeof(NumericUpDown),
        new FrameworkPropertyMetadata(100, OnRangeChanged));

    /// <summary>
    /// Identifies the <see cref="Increment" /> dependency property.
    /// </summary>
    public static readonly DependencyProperty IncrementProperty = DependencyProperty.Register(
        nameof(Increment),
        typeof(int),
        typeof(NumericUpDown),
        new FrameworkPropertyMetadata(1, null, CoerceIncrement));

    /// <summary>
    /// Identifies the <see cref="NumericValue" /> dependency property.
    /// </summary>
    public static readonly DependencyProperty NumericValueProperty = DependencyProperty.Register(
        nameof(NumericValue),
        typeof(int),
        typeof(NumericUpDown),
        new FrameworkPropertyMetadata(
            0,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnValueChanged,
            CoerceValue));

    private bool _updatingText;

    /// <summary>
    /// Initializes a new instance of the <see cref="NumericUpDown" /> class.
    /// </summary>
    public NumericUpDown()
    {
        InitializeComponent();
        DataObject.AddPastingHandler(ValueTextBox, OnPaste);
        UpdateText();
    }

    /// <summary>
    /// Gets or sets the lowest accepted value.
    /// </summary>
    public int Minimum
    {
        get => (int)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    /// <summary>
    /// Gets or sets the highest accepted value.
    /// </summary>
    public int Maximum
    {
        get => (int)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>
    /// Gets or sets the amount applied by the buttons, arrow keys, and mouse wheel.
    /// </summary>
    public int Increment
    {
        get => (int)GetValue(IncrementProperty);
        set => SetValue(IncrementProperty, value);
    }

    /// <summary>
    /// Gets or sets the current numeric value.
    /// </summary>
    public int NumericValue
    {
        get => (int)GetValue(NumericValueProperty);
        set => SetValue(NumericValueProperty, value);
    }

    /// <summary>
    /// Parses the current text, clamps it to the configured range, and updates
    /// <see cref="NumericValue" />.
    /// </summary>
    public void CommitEdit()
    {
        if (int.TryParse(ValueTextBox.Text, NumberStyles.None, CultureInfo.CurrentCulture, out var value))
        {
            SetCurrentValue(NumericValueProperty, Math.Clamp(value, Minimum, Maximum));
        }

        UpdateText();
    }

    protected override void OnAccessKey(AccessKeyEventArgs e)
    {
        ValueTextBox.Focus();
        ValueTextBox.SelectAll();
    }

    private static object CoerceIncrement(DependencyObject element, object value)
    {
        return Math.Max(1, (int)value);
    }

    private static object CoerceValue(DependencyObject element, object value)
    {
        var control = (NumericUpDown)element;
        return Math.Clamp((int)value, control.Minimum, control.Maximum);
    }

    private static void OnRangeChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        var control = (NumericUpDown)element;
        control.CoerceValue(NumericValueProperty);
        control.UpdateText();
    }

    private static void OnValueChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        ((NumericUpDown)element).UpdateText();
    }

    private void ChangeValue(int direction)
    {
        CommitEdit();
        SetCurrentValue(NumericValueProperty, Math.Clamp(NumericValue + direction * Increment, Minimum, Maximum));
        ValueTextBox.Focus();
        ValueTextBox.SelectAll();
    }

    private void UpdateText()
    {
        if (_updatingText || ValueTextBox is null)
        {
            return;
        }

        _updatingText = true;
        ValueTextBox.Text = NumericValue.ToString(CultureInfo.CurrentCulture);
        _updatingText = false;
    }

    private void IncreaseButton_Click(object sender, RoutedEventArgs e)
    {
        ChangeValue(1);
    }

    private void DecreaseButton_Click(object sender, RoutedEventArgs e)
    {
        ChangeValue(-1);
    }

    private void ValueTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !e.Text.All(char.IsDigit);
    }

    private void ValueTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.None)
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

    private void ValueTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        CommitEdit();
    }

    private void NumericUpDown_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!IsKeyboardFocusWithin)
        {
            return;
        }

        ChangeValue(e.Delta > 0 ? 1 : -1);
        e.Handled = true;
    }

    private static void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText))
        {
            e.CancelCommand();
            return;
        }

        var text = e.SourceDataObject.GetData(DataFormats.UnicodeText) as string;
        if (string.IsNullOrEmpty(text) || !text.All(char.IsDigit))
        {
            e.CancelCommand();
        }
    }
}
