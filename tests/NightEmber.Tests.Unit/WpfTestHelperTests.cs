using System.Windows.Controls;
using System.Windows.Input;

namespace NightEmber.Tests.Unit;

public sealed class WpfTestHelperTests
{
    [Theory]
    [InlineData(ModifierKeys.None)]
    [InlineData(ModifierKeys.Control)]
    [InlineData(ModifierKeys.Alt)]
    [InlineData(ModifierKeys.Shift)]
    [InlineData(ModifierKeys.Control | ModifierKeys.Shift)]
    public async Task PressKey_Modifiers_UsesIndependentKeyboardState(ModifierKeys modifiers)
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            var input = new TextBox();

            // Act
            var args = WpfTestHelper.PressKey(input, Key.Up, modifiers);

            // Assert
            args.KeyboardDevice.Should().NotBeSameAs(Keyboard.PrimaryDevice);
            args.KeyboardDevice.Modifiers.Should().Be(modifiers);
            args.IsDown.Should().BeTrue();
        });
    }

    [Fact]
    public async Task PressKey_UnmodifiedAfterModified_DoesNotRetainPreviousModifiers()
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            var input = new TextBox();
            WpfTestHelper.PressKey(input, Key.Up, ModifierKeys.Control);

            // Act
            var args = WpfTestHelper.PressKey(input, Key.Down);

            // Assert
            args.KeyboardDevice.Modifiers.Should().Be(ModifierKeys.None);
            args.KeyboardDevice.IsKeyDown(Key.Up).Should().BeFalse();
            args.IsDown.Should().BeTrue();
        });
    }
}
