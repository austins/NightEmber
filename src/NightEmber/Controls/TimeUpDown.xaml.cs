using System.Globalization;
using System.Windows;
using System.Windows.Input;

namespace NightEmber.Controls;

/// <summary>
/// Provides a culture-aware time input with segment-based spinner behavior.
/// </summary>
/// <remarks>
/// The text uses the current Windows short-time pattern. Users can type a full
/// time or select the hour, minute, or day-period segment and adjust it with the
/// spinner buttons, arrow keys, or mouse wheel.
/// </remarks>
public sealed partial class TimeUpDown : System.Windows.Controls.UserControl
{
    /// <summary>
    /// Identifies the <see cref="MinuteIncrement" /> dependency property.
    /// </summary>
    public static readonly DependencyProperty MinuteIncrementProperty = DependencyProperty.Register(
        nameof(MinuteIncrement),
        typeof(int),
        typeof(TimeUpDown),
        new FrameworkPropertyMetadata(5, null, CoerceMinuteIncrement));

    /// <summary>
    /// Identifies the <see cref="TimeValue" /> dependency property.
    /// </summary>
    public static readonly DependencyProperty TimeValueProperty = DependencyProperty.Register(
        nameof(TimeValue),
        typeof(TimeSpan),
        typeof(TimeUpDown),
        new FrameworkPropertyMetadata(
            TimeSpan.Zero,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnTimeValueChanged,
            CoerceTimeValue));

    private TimeSegment _selectedSegment = TimeSegment.Hour;
    private IReadOnlyList<SegmentRange> _segments = [];
    private bool _updatingText;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeUpDown" /> class.
    /// </summary>
    public TimeUpDown()
    {
        InitializeComponent();
        UpdateText();
    }

    /// <summary>
    /// Gets or sets the number of minutes applied when the minute segment is adjusted.
    /// </summary>
    public int MinuteIncrement
    {
        get => (int)GetValue(MinuteIncrementProperty);
        set => SetValue(MinuteIncrementProperty, value);
    }

    /// <summary>
    /// Gets or sets the current time of day.
    /// </summary>
    public TimeSpan TimeValue
    {
        get => (TimeSpan)GetValue(TimeValueProperty);
        set => SetValue(TimeValueProperty, value);
    }

    /// <summary>
    /// Parses the current text using the user's Windows time format.
    /// </summary>
    /// <returns><see langword="true" /> when the edit contained a valid time.</returns>
    public bool CommitEdit()
    {
        if (DateTime.TryParse(
                ValueTextBox.Text,
                CultureInfo.CurrentCulture,
                DateTimeStyles.NoCurrentDateDefault,
                out var parsed))
        {
            SetCurrentValue(TimeValueProperty, parsed.TimeOfDay);
            UpdateText();
            return true;
        }

        UpdateText();
        return false;
    }

    private static object CoerceMinuteIncrement(DependencyObject element, object value)
    {
        return Math.Clamp((int)value, 1, 59);
    }

    private static object CoerceTimeValue(DependencyObject element, object value)
    {
        var time = (TimeSpan)value;
        var ticks = time.Ticks % TimeSpan.TicksPerDay;
        if (ticks < 0)
        {
            ticks += TimeSpan.TicksPerDay;
        }

        return TimeSpan.FromTicks(ticks);
    }

    private static void OnTimeValueChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        ((TimeUpDown)element).UpdateText();
    }

    private void ChangeValue(int direction)
    {
        CommitEdit();

        var adjustment = _selectedSegment switch
        {
            TimeSegment.Hour => TimeSpan.FromHours(direction),
            TimeSegment.Minute => TimeSpan.FromMinutes(direction * MinuteIncrement),
            TimeSegment.Period => TimeSpan.FromHours(direction * 12),
            _ => TimeSpan.Zero
        };

        SetCurrentValue(TimeValueProperty, TimeValue + adjustment);
        ValueTextBox.Focus();
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
        if (_segments.Count == 0)
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
        ValueTextBox.Select(range.Start, range.Length);
    }

    private void UpdateText()
    {
        if (_updatingText || ValueTextBox is null)
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
    }

    private static IReadOnlyList<SegmentRange> FindSegments(
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

    private void IncreaseButton_Click(object sender, RoutedEventArgs e)
    {
        ChangeValue(1);
    }

    private void DecreaseButton_Click(object sender, RoutedEventArgs e)
    {
        ChangeValue(-1);
    }

    private void ValueTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        SelectSegmentAt(ValueTextBox.CaretIndex);
    }

    private void ValueTextBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        SelectSegmentAt(ValueTextBox.CaretIndex);
    }

    private void ValueTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
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
            case Key.Left:
                SelectAdjacentSegment(-1);
                e.Handled = true;
                break;
            case Key.Right:
                SelectAdjacentSegment(1);
                e.Handled = true;
                break;
            case Key.Enter:
                CommitEdit();
                SelectSegment(_selectedSegment);
                e.Handled = true;
                break;
            case Key.Escape:
                UpdateText();
                SelectSegment(_selectedSegment);
                e.Handled = true;
                break;
        }
    }

    private void ValueTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        CommitEdit();
    }

    private void TimeUpDown_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!IsKeyboardFocusWithin)
        {
            return;
        }

        ChangeValue(e.Delta > 0 ? 1 : -1);
        e.Handled = true;
    }

    private enum TimeSegment
    {
        Hour,
        Minute,
        Period
    }

    private readonly record struct SegmentRange(TimeSegment Segment, int Start, int Length);
}
