namespace NightEmber.Core.Models;

/// <summary>
/// Identifies how Night Ember determines when the display tint should be active.
/// </summary>
public enum ScheduleMode
{
    /// <summary>
    /// Applies no automatic schedule.
    /// </summary>
    Manual,

    /// <summary>
    /// Activates between the estimated local sunset and sunrise.
    /// </summary>
    Sunset,

    /// <summary>
    /// Activates between user-defined times.
    /// </summary>
    Custom
}
