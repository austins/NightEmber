using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using NightEmber.Controls;
using static NightEmber.Tests.Unit.Controls.SpinnerTestHelper;
using NumericUpDown = NightEmber.Controls.NumericUpDown;

namespace NightEmber.Tests.Unit.Controls;

public sealed class SpinnerThemeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ThemeVariant_Changed_UpdatesSpinnerBrushesWithoutChangingItsSize(bool timeInput)
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            UserControl control = timeInput ? new TimeUpDown() : new NumericUpDown();
            var window = AvaloniaTestHelper.ShowInWindow(control);
            var results = new List<(IBrush? Foreground, double Height)>();

            // Act
            foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            {
                window.RequestedThemeVariant = variant;
                window.UpdateLayout();
                results.Add((Input(control).Foreground, control.Bounds.Height));

                // Assert
                SolidColor(Spinner(control).Background).Should().Be(Resource(window, "TextControlBackground", variant));
                SolidColor(Spinner(control).BorderBrush).Should().Be(Resource(window, "TextControlBorderBrush", variant));
                SolidColor(Input(control).Foreground).Should().Be(Resource(window, "TextControlForeground", variant));
            }

            results[0].Height.Should().Be(results[1].Height);
            SolidColor(results[0].Foreground).Should().NotBe(SolidColor(results[1].Foreground));
            window.Close();
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IsEnabled_DisabledSpinner_UsesDisabledThemeBrush(bool timeInput)
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            UserControl control = timeInput ? new TimeUpDown() : new NumericUpDown();
            var window = AvaloniaTestHelper.ShowInWindow(control);

            foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            {
                window.RequestedThemeVariant = variant;
                control.IsEnabled = true;
                window.UpdateLayout();
                var enabled = Input(control).Foreground;

                // Act
                control.IsEnabled = false;
                window.UpdateLayout();

                // Assert
                SolidColor(Input(control).Foreground).Should().Be(Resource(window, "TextControlForegroundDisabled", variant));
                Input(control).Foreground.Should().NotBeSameAs(enabled);
            }

            window.Close();
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidEdit_ThemeOutline_UsesErrorBrushWithoutMovingContent(bool timeInput)
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            UserControl control = timeInput ? new TimeUpDown() : new NumericUpDown();
            var window = AvaloniaTestHelper.ShowInWindow(control);
            var errorColors = new List<Color>();

            foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            {
                window.RequestedThemeVariant = variant;
                Input(control).Text = "valid-looking text is not required";
                window.UpdateLayout();
                var before = (Spinner(control).Bounds, Input(control).Bounds);

                // Act
                Input(control).Text = "invalid";
                Commit(control);
                window.UpdateLayout();
                var errorBrush = Spinner(control).BorderBrush;
                var after = (Spinner(control).Bounds, Input(control).Bounds);
                control.IsEnabled = false;
                window.UpdateLayout();
                var disabledBrush = Spinner(control).BorderBrush;
                control.IsEnabled = true;

                // Assert
                SolidColor(errorBrush).Should().Be(Resource(window, "InputErrorBrush", variant));
                SolidColor(disabledBrush).Should().Be(Resource(window, "InputErrorBrush", variant));
                after.Should().Be(before);
                HasError(control).Should().BeTrue();
                errorColors.Add(SolidColor(errorBrush));

                SolidColor(Spinner(control).BorderBrush).Should().Be(Resource(window, "InputErrorBrush", variant));
                control.Focus();
                Input(control).Focus();
                AvaloniaTestHelper.PressKey(window, Key.Escape);
                window.UpdateLayout();
                HasError(control).Should().BeFalse();
                SolidColor(Spinner(control).BorderBrush).Should().Be(Resource(window, "TextControlBorderBrush", variant));
            }

            errorColors[0].Should().NotBe(errorColors[1]);
            window.Close();
        });
    }

    private static Color Resource(Control scope, string key, ThemeVariant variant)
    {
        scope.TryFindResource(key, variant, out var resource).Should().BeTrue($"{key} should resolve for {variant}");
        return resource is ISolidColorBrush brush ? brush.Color : (Color)resource!;
    }

    private static Color SolidColor(IBrush? brush)
    {
        return ((ISolidColorBrush)brush!).Color;
    }
}
