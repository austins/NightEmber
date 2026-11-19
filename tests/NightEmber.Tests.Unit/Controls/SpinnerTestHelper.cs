using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using NightEmber.Controls;
using NumericUpDown = NightEmber.Controls.NumericUpDown;

namespace NightEmber.Tests.Unit.Controls;

/// <summary>
/// Reaches the inner parts of <see cref="NumericUpDown" /> and <see cref="TimeUpDown" /> that tests drive like a user.
/// </summary>
internal static class SpinnerTestHelper
{
    public static TextBox Input(UserControl control)
    {
        return control.FindControl<TextBox>("ValueTextBox")!;
    }

    public static ButtonSpinner Spinner(UserControl control)
    {
        return control.FindControl<ButtonSpinner>("Spinner")!;
    }

    /// <summary>
    /// Gets one of the spinner's arrow buttons. The template exists only after the control is shown.
    /// </summary>
    /// <param name="control">The shown control.</param>
    /// <param name="increase">Whether to get the increase button instead of the decrease button.</param>
    /// <returns>The button.</returns>
    public static Button ArrowButton(UserControl control, bool increase)
    {
        var name = increase ? "PART_IncreaseButton" : "PART_DecreaseButton";
        return Spinner(control).GetVisualDescendants().OfType<Button>().Single(button => button.Name == name);
    }

    public static string ErrorMessage(UserControl control)
    {
        return control switch
        {
            TimeUpDown time => time.ErrorMessage,
            NumericUpDown numeric => numeric.ErrorMessage,
            _ => throw new ArgumentException("Not a spinner input.", nameof(control))
        };
    }

    /// <summary>
    /// Reports whether the control shows an error, requiring its message, validation state, and outline to agree.
    /// </summary>
    /// <param name="control">The control to inspect.</param>
    /// <returns>Whether an error is shown.</returns>
    public static bool HasError(UserControl control)
    {
        var spinner = Spinner(control);
        var message = ErrorMessage(control).Length > 0;
        (DataValidationErrors.GetHasErrors(spinner), spinner.Classes.Contains(":error")).Should().Be((message, message));
        return message;
    }

    public static bool Commit(UserControl control)
    {
        return control is TimeUpDown time ? time.CommitEdit() : ((NumericUpDown)control).CommitEdit();
    }

    /// <summary>
    /// Gives the editor keyboard focus the way tabbing into it would.
    /// </summary>
    /// <param name="control">The shown control.</param>
    public static void Focus(UserControl control)
    {
        Input(control).Focus();
        AvaloniaTestHelper.FlushDispatcher();
    }

    /// <summary>
    /// Hosts the control next to a button, so tests can move focus away from the editor.
    /// </summary>
    /// <param name="control">The control to host.</param>
    /// <param name="other">The button that can take focus from the control.</param>
    /// <returns>The shown window; the caller closes it.</returns>
    public static Window ShowWithNeighbor(UserControl control, out Button other)
    {
        other = new Button { Content = "Other" };
        var panel = new StackPanel();
        panel.Children.Add(control);
        panel.Children.Add(other);
        return AvaloniaTestHelper.ShowInWindow(panel);
    }

    public static Point Center(Control control, TopLevel window)
    {
        return control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
    }

    /// <summary>
    /// Reports whether the window's pointer-wheel event stayed unhandled while it was routed.
    /// </summary>
    /// <param name="window">The window to wheel over.</param>
    /// <param name="control">The control under the pointer.</param>
    /// <returns>Whether a handler consumed the wheel event.</returns>
    public static bool WheelOver(Window window, Control control)
    {
        var handled = false;
        void Capture(object? sender, PointerWheelEventArgs e)
        {
            handled = e.Handled;
        }

        window.AddHandler(
            InputElement.PointerWheelChangedEvent,
            Capture,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
        window.MouseWheel(Center(control, window), new Vector(0, 1));
        window.RemoveHandler(InputElement.PointerWheelChangedEvent, Capture);
        return handled;
    }
}
