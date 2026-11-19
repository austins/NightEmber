using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;

namespace NightEmber.Controls;

internal static class EditorAutomation
{
    /// <summary>
    /// Mirrors the automation name source, accelerator, and help text set on a composite
    /// input onto its inner editor, which is the element that takes keyboard focus.
    /// </summary>
    /// <param name="change">The property change raised by the composite input.</param>
    /// <param name="editor">The inner editor that screen readers announce.</param>
    public static void Forward(AvaloniaPropertyChangedEventArgs change, TextBox editor)
    {
        if (change.Property == AutomationProperties.LabeledByProperty
            || change.Property == AutomationProperties.AcceleratorKeyProperty
            || change.Property == AutomationProperties.HelpTextProperty)
        {
            editor.SetCurrentValue(change.Property, change.NewValue);
        }
    }
}
