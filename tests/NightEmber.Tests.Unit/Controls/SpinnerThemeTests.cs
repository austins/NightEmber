using NightEmber.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace NightEmber.Tests.Unit.Controls;

public sealed class SpinnerThemeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ThemeResources_ChangedPalette_UpdatesSpinnerWithoutChangingItsSize(bool timeInput)
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            UserControl input = timeInput ? new TimeUpDown() : new NumericUpDown();
            var host = new Grid();
            host.Children.Add(input);
            var border = (Border)input.Content;
            var grid = (Grid)border.Child;
            var textBox = (TextBox)grid.Children[0];
            textBox.FontSize = 14;
            textBox.Text = timeInput ? "12:59 PM" : "1440";
            var buttons = ((Grid)grid.Children[1]).Children.OfType<RepeatButton>().ToArray();
            var results = new List<(Brush Background, Brush Foreground, double Height)>();
            string[] themes = ["Light", "Dark", "HC"];

            // Act
            foreach (var theme in themes)
            {
                host.Resources.MergedDictionaries.Clear();
                host.Resources.MergedDictionaries.Add(
                    new ResourceDictionary
                    {
                        Source = new Uri(
                            $"/PresentationFramework.Fluent;component/Themes/Fluent.{theme}.xaml",
                            UriKind.Relative)
                    });
                host.Measure(new Size(110, 28));
                host.Arrange(new Rect(0, 0, 110, 28));
                host.UpdateLayout();
                results.Add((textBox.Background, textBox.Foreground, input.ActualHeight));

                // Assert
                border.Background.Should().BeSameAs(host.FindResource("TextControlBackground"));
                border.BorderBrush.Should().BeSameAs(host.FindResource("ControlStrongStrokeColorDefaultBrush"));
                textBox.Foreground.Should().BeSameAs(host.FindResource("TextControlForeground"));
                textBox.MinHeight.Should().Be(0);
                textBox.ActualHeight.Should().BeLessThanOrEqualTo(28);
                textBox.Template.FindName("DeleteButton", textBox).Should().BeNull();
                textBox.Template.FindName("ContentBorder", textBox).Should().BeNull();
                var contentHost = (ScrollViewer)textBox.Template.FindName("PART_ContentHost", textBox);
                contentHost.ActualWidth.Should().Be(textBox.ActualWidth);
                contentHost.ExtentWidth.Should().BeLessThanOrEqualTo(contentHost.ViewportWidth);
                contentHost.Focusable.Should().BeFalse();
                contentHost.IsTabStop.Should().BeFalse();
                foreach (var button in buttons)
                {
                    button.Template.Should().NotBeNull();
                    button.Background.Should().BeSameAs(host.FindResource("RepeatButtonBackground"));
                    button.HorizontalAlignment.Should().Be(HorizontalAlignment.Stretch);
                    button.VerticalAlignment.Should().Be(VerticalAlignment.Stretch);
                    ((System.Windows.Shapes.Path)button.Content)
                        .Fill
                        .Should()
                        .BeSameAs(host.FindResource("TextFillColorPrimaryBrush"));
                }
            }

            results.Should().AllSatisfy(static result => result.Height.Should().Be(28));
            results[0].Foreground.Should().NotBeSameAs(results[1].Foreground);
        });
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [InlineData("HC")]
    public async Task IsEnabled_DisabledSpinner_UsesDisabledThemeBrush(string theme)
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            var input = new NumericUpDown();
            input.Resources.MergedDictionaries.Add(
                new ResourceDictionary
                {
                    Source = new Uri(
                        $"/PresentationFramework.Fluent;component/Themes/Fluent.{theme}.xaml",
                        UriKind.Relative)
                });
            var grid = (Grid)((Border)input.Content).Child;
            var textBox = (TextBox)grid.Children[0];
            var buttons = ((Grid)grid.Children[1]).Children.OfType<RepeatButton>().ToArray();

            // Act
            input.IsEnabled = false;
            input.Measure(new Size(110, 28));
            input.Arrange(new Rect(0, 0, 110, 28));
            input.UpdateLayout();

            // Assert
            textBox.Foreground.Should().BeSameAs(input.FindResource("TextControlForegroundDisabled"));
            foreach (var button in buttons)
            {
                ((System.Windows.Shapes.Path)button.Content)
                    .Fill
                    .Should()
                    .BeSameAs(input.FindResource("TextFillColorDisabledBrush"));
            }
        });
    }
}
