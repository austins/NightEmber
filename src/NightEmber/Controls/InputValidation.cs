using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace NightEmber.Controls;

internal static class InputValidation
{
    /// <summary>
    /// Identifies the message explaining an input's validation error.
    /// </summary>
    public static readonly DependencyProperty ErrorMessageProperty = DependencyProperty.RegisterAttached(
        "ErrorMessage",
        typeof(string),
        typeof(InputValidation),
        new PropertyMetadata(string.Empty));

    /// <summary>
    /// Identifies whether an input has a validation error.
    /// </summary>
    public static readonly DependencyProperty HasErrorProperty = DependencyProperty.RegisterAttached(
        "HasError",
        typeof(bool),
        typeof(InputValidation),
        new PropertyMetadata(false));

    /// <summary>
    /// Gets the input's validation message, or an empty string when valid.
    /// </summary>
    /// <param name="element">The input whose message is requested.</param>
    /// <returns>The current validation message.</returns>
    public static string GetErrorMessage(DependencyObject element)
    {
        return (string)element.GetValue(ErrorMessageProperty);
    }

    /// <summary>
    /// Gets whether the input has a validation error.
    /// </summary>
    /// <param name="element">The input to inspect.</param>
    /// <returns>Whether its current edit is invalid.</returns>
    public static bool GetHasError(DependencyObject element)
    {
        return (bool)element.GetValue(HasErrorProperty);
    }

    internal static void SetError(UserControl control, TextBox input, string message, string validStatus = "")
    {
        control.SetValue(ErrorMessageProperty, message);
        control.SetValue(HasErrorProperty, message.Length > 0);
        UpdateStatus(input, message.Length > 0 ? message : validStatus, message.Length > 0);
    }

    internal static void UpdateStatus(TextBox input, string status, bool announceError = false)
    {
        var previous = AutomationProperties.GetItemStatus(input);
        if (previous == status)
        {
            return;
        }

        AutomationProperties.SetItemStatus(input, status);
        var peer = UIElementAutomationPeer.FromElement(input);
        peer?.RaisePropertyChangedEvent(AutomationElementIdentifiers.ItemStatusProperty, previous, status);
        if ((announceError || input.IsKeyboardFocusWithin)
            && AutomationPeer.ListenerExists(AutomationEvents.Notification))
        {
            peer?.RaiseNotificationEvent(
                AutomationNotificationKind.ActionCompleted,
                AutomationNotificationProcessing.MostRecent,
                status,
                "InputStatus");
        }
    }

    internal static void FocusEditor(UserControl control)
    {
        var input = (TextBox)control.FindName("ValueTextBox");
        input.Focus();
        input.BringIntoView();
    }
}
