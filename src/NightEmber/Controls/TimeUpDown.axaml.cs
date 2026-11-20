using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Globalization;

namespace NightEmber.Controls;

/// <summary>
/// Provides a culture-aware time input with segment-based spinner behavior.
/// </summary>
/// <remarks>
/// The text uses the current Windows short-time pattern. Users can type a full
/// time or select the hour, minute, or day-period segment and adjust it with the
/// spinner buttons, arrow keys, or mouse wheel. Avalonia's <see cref="TimePicker" /> is flyout-only and has
/// no typed entry or segment selection, so this composes the built-in <see cref="ButtonSpinner" /> and
/// <see cref="DataValidationErrors" /> around a <see cref="TextBox" /> instead.
/// </remarks>
public sealed partial class TimeUpDown : UserControl
{
    /// <summary>
    /// Identifies the <see cref="MinuteIncrement" /> styled property.
    /// </summary>
    public static readonly StyledProperty<int> MinuteIncrementProperty =
        AvaloniaProperty.Register<TimeUpDown, int>(
            nameof(MinuteIncrement),
            5,
            coerce: static (_, value) => Math.Clamp(value, 1, 59));

    /// <summary>
    /// Identifies the <see cref="TimeValue" /> styled property.
    /// </summary>
    public static readonly StyledProperty<TimeSpan> TimeValueProperty =
        AvaloniaProperty.Register<TimeUpDown, TimeSpan>(
            nameof(TimeValue),
            TimeSpan.Zero,
            defaultBindingMode: BindingMode.TwoWay,
            coerce: static (_, value) => CoerceTimeValue(value));

    /// <summary>
    /// Identifies the <see cref="ErrorMessage" /> styled property.
    /// </summary>
    public static readonly StyledProperty<string> ErrorMessageProperty =
        AvaloniaProperty.Register<TimeUpDown, string>(nameof(ErrorMessage), string.Empty);

    private readonly bool _ready;
    private TimeSegment _selectedSegment = TimeSegment.Hour;
    private IReadOnlyList<SegmentRange> _segments = [];
    private bool _updatingText;
    private bool _validateEdits;
    private bool _isTextDirty;
    private bool _ignoreSpin;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeUpDown" /> class.
    /// </summary>
    public TimeUpDown()
    {
        InitializeComponent();
        Spinner.AddHandler(KeyDownEvent, Spinner_PreviewKeyDown, RoutingStrategies.Tunnel);
        Spinner.AddHandler(KeyDownEvent, Spinner_KeyDown, RoutingStrategies.Bubble, handledEventsToo: true);
        ValueTextBox.AddHandler(KeyDownEvent, ValueTextBox_KeyDown, RoutingStrategies.Tunnel);
        ValueTextBox.AddHandler(
            PointerReleasedEvent,
            ValueTextBox_PointerReleased,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
        ValueTextBox.TextChanging += ValueTextBox_TextChanging;
        ValueTextBox.GotFocus += ValueTextBox_GotFocus;
        ValueTextBox.LostFocus += ValueTextBox_LostFocus;
        _ready = true;
        UpdateText();
    }

    /// <summary>
    /// Gets or sets the number of minutes applied when the minute segment is adjusted.
    /// </summary>
    public int MinuteIncrement
    {
        get => GetValue(MinuteIncrementProperty);
        set => SetValue(MinuteIncrementProperty, value);
    }

    /// <summary>
    /// Gets or sets the current time of day.
    /// </summary>
    public TimeSpan TimeValue
    {
        get => GetValue(TimeValueProperty);
        set => SetValue(TimeValueProperty, value);
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
    /// Parses the current text using the user's Windows time format.
    /// </summary>
    /// <remarks>Invalid text is preserved with an error until corrected or cancelled.</remarks>
    /// <returns><see langword="true" /> when the edit contained a valid time.</returns>
    public bool CommitEdit()
    {
        if (!ValidateEdit(out var time))
        {
            _validateEdits = true;
            return false;
        }

        SetCurrentValue(TimeValueProperty, time);
        UpdateText();
        return true;
    }

    internal bool TryGetEditedTime(out TimeSpan time)
    {
        var culture = CultureInfo.CurrentCulture;
        var text = ValueTextBox.Text;
        var valid = TimeOnly.TryParseExact(
                        text,
                        culture.DateTimeFormat.ShortTimePattern,
                        culture,
                        DateTimeStyles.AllowWhiteSpaces,
                        out var parsed)
                    || TimeOnly.TryParse(text, culture, DateTimeStyles.AllowWhiteSpaces, out parsed);
        time = parsed.ToTimeSpan();
        return valid;
    }

    internal void FocusEditor()
    {
        ValueTextBox.Focus();
        ValueTextBox.BringIntoView();
    }

    /// <inheritdoc />
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);

        // The label's access key focuses this wrapper; the editor inside owns keyboard input.
        if (ReferenceEquals(e.Source, this))
        {
            ValueTextBox.Focus();
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (!_ready)
        {
            return;
        }

        if (change.Property == TimeValueProperty)
        {
            UpdateText();
        }
        else
        {
            EditorAutomation.Forward(change, ValueTextBox);
        }
    }

    private static TimeSpan CoerceTimeValue(TimeSpan time)
    {
        var ticks = time.Ticks % TimeSpan.TicksPerDay;
        if (ticks < 0)
        {
            ticks += TimeSpan.TicksPerDay;
        }

        return TimeSpan.FromTicks(ticks);
    }

    private static List<SegmentRange> FindSegments(
        DateTime date,
        string pattern,
        string formatted,
        CultureInfo culture)
    {
        var segments = new List<SegmentRange>(3);
        var hourToken = TimeFormat.FindToken(pattern, 'H', 'h');
        var minuteToken = TimeFormat.FindToken(pattern, 'm');
        var searchIndex = 0;

        if (hourToken is not null)
        {
            var hourText = TimeFormat.FormatToken(date, hourToken, culture);
            var start = formatted.IndexOf(hourText, searchIndex, StringComparison.CurrentCulture);
            if (start >= 0)
            {
                segments.Add(new SegmentRange(TimeSegment.Hour, start, hourText.Length));
                searchIndex = start + hourText.Length;
            }
        }

        if (minuteToken is not null)
        {
            var minuteText = TimeFormat.FormatToken(date, minuteToken, culture);
            var start = formatted.IndexOf(minuteText, searchIndex, StringComparison.CurrentCulture);
            if (start >= 0)
            {
                segments.Add(new SegmentRange(TimeSegment.Minute, start, minuteText.Length));
            }
        }

        var periodToken = TimeFormat.FindToken(pattern, 't');
        var periodText = periodToken is null ? string.Empty : TimeFormat.FormatToken(date, periodToken, culture);
        if (!string.IsNullOrEmpty(periodText))
        {
            var start = formatted.IndexOf(periodText, StringComparison.CurrentCulture);
            if (start >= 0)
            {
                segments.Add(new SegmentRange(TimeSegment.Period, start, periodText.Length));
            }
        }

        return segments;
    }

    private void ChangeValue(int direction)
    {
        // The arrow button takes focus when clicked, so return it to the editor even when the edit is invalid.
        ValueTextBox.Focus();
        if (!CommitEdit())
        {
            return;
        }

        var adjustment = _selectedSegment switch
        {
            TimeSegment.Hour => TimeSpan.FromHours(direction),
            TimeSegment.Minute => TimeSpan.FromMinutes(direction * MinuteIncrement),
            TimeSegment.Period => TimeSpan.FromHours(direction * 12),
            _ => TimeSpan.Zero
        };

        SetCurrentValue(TimeValueProperty, TimeValue + adjustment);
        SelectSegment(_selectedSegment);
    }

    private void SelectAdjacentSegment(int direction)
    {
        if (_segments.Count == 0)
        {
            return;
        }

        var currentIndex = _segments
            .Select((segment, index) => (segment, index))
            .FirstOrDefault(item => item.segment.Segment == _selectedSegment)
            .index;

        var nextIndex = (currentIndex + direction + _segments.Count) % _segments.Count;

        SelectSegment(_segments[nextIndex].Segment);
    }

    private void SelectSegmentAt(int characterIndex)
    {
        if (_isTextDirty || _segments.Count == 0)
        {
            return;
        }

        var segment = _segments.FirstOrDefault(candidate =>
            characterIndex >= candidate.Start && characterIndex <= candidate.Start + candidate.Length);

        if (segment.Length == 0)
        {
            segment = _segments.MinBy(candidate => Math.Abs(characterIndex - candidate.Start));
        }

        SelectSegment(segment.Segment);
    }

    private void SelectSegment(TimeSegment segment)
    {
        var range = _segments.FirstOrDefault(candidate => candidate.Segment == segment);
        if (range.Length == 0)
        {
            return;
        }

        _selectedSegment = segment;
        ValueTextBox.SelectionStart = range.Start;
        ValueTextBox.SelectionEnd = range.Start + range.Length;
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        AutomationProperties.SetItemStatus(
            ValueTextBox,
            ErrorMessage.Length > 0 ? ErrorMessage : SelectedSegmentStatus());
    }

    private string SelectedSegmentStatus()
    {
        return _selectedSegment switch
        {
            TimeSegment.Hour => "Hour selected",
            TimeSegment.Minute => "Minute selected",
            TimeSegment.Period => "AM/PM selected",
            _ => throw new InvalidOperationException("Unknown time segment.")
        };
    }

    private bool ValidateEdit(out TimeSpan time)
    {
        var valid = TryGetEditedTime(out time);
        var example = DateTime
            .Today
            .AddHours(21)
            .ToString(CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern, CultureInfo.CurrentCulture);
        SetError(valid ? string.Empty : $"Enter a valid time, for example {example}.");
        return valid;
    }

    private void SetError(string message)
    {
        ErrorMessage = message;
        DataValidationErrors.SetErrors(Spinner, message.Length > 0 ? [message] : null);
        UpdateStatus();
    }

    private void UpdateText()
    {
        if (_updatingText)
        {
            return;
        }

        _updatingText = true;
        var culture = CultureInfo.CurrentCulture;
        var date = DateTime.Today.Add(TimeValue);
        var pattern = culture.DateTimeFormat.ShortTimePattern;
        var text = date.ToString(pattern, culture);
        ValueTextBox.Text = text;
        _segments = FindSegments(date, pattern, text, culture);
        _updatingText = false;
        _isTextDirty = false;
        _validateEdits = false;
        SetError(string.Empty);
    }

    private void ValueTextBox_TextChanging(object? sender, TextChangingEventArgs e)
    {
        if (_updatingText)
        {
            return;
        }

        _isTextDirty = true;
        if (_validateEdits)
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

    private void ValueTextBox_GotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (!_isTextDirty)
        {
            SelectSegment(_selectedSegment);
        }
    }

    private void ValueTextBox_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        SelectSegmentAt(ValueTextBox.CaretIndex);
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
            case Key.Left when !_isTextDirty:
                SelectAdjacentSegment(-1);
                e.Handled = true;
                break;
            case Key.Right when !_isTextDirty:
                SelectAdjacentSegment(1);
                e.Handled = true;
                break;
            case Key.Enter:
                if (CommitEdit())
                {
                    SelectSegment(_selectedSegment);
                }

                e.Handled = true;
                break;
            case Key.Escape:
                UpdateText();
                SelectSegment(_selectedSegment);
                e.Handled = true;
                break;
        }
    }

    private void ValueTextBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        CommitEdit();
    }

    private enum TimeSegment
    {
        Hour,
        Minute,
        Period
    }

    private readonly record struct SegmentRange(TimeSegment Segment, int Start, int Length);
}
