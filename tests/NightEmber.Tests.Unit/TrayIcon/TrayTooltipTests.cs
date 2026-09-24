using NightEmber.Models;
using NightEmber.TrayIcon;
using System.Globalization;

namespace NightEmber.Tests.Unit.TrayIcon;

public sealed class TrayTooltipTests
{
    private static readonly DateTime NextChange = new(2026, 9, 5, 7, 0, 0, DateTimeKind.Local);

    [Theory]
    [InlineData(true, (int)ScheduleMode.Sunset, true, "Night Ember on - 3400K\nSun schedule: off 07:00")]
    [InlineData(false, (int)ScheduleMode.Sunset, true, "Night Ember off\nSun schedule: on 07:00")]
    [InlineData(true, (int)ScheduleMode.Sunset, false, "Night Ember on - 3400K\nSunset to sunrise")]
    [InlineData(true, (int)ScheduleMode.Custom, true, "Night Ember on - 3400K\nSet hours: off 07:00")]
    [InlineData(false, (int)ScheduleMode.Custom, false, "Night Ember off\nSet hours")]
    [InlineData(false, (int)ScheduleMode.Manual, false, "Night Ember off\nManual only")]
    public void Format_Schedule_DescribesStateAndNextChange(
        bool isOn,
        int mode,
        bool hasNextChange,
        string expected)
    {
        // Act
        var result = TrayTooltip.Format(
            isOn,
            3400,
            false,
            (ScheduleMode)mode,
            hasNextChange ? NextChange : null,
            CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(true, "Night Ember on - 3400K\nManual until 07:00")]
    [InlineData(false, "Night Ember on - 3400K\nManual")]
    public void Format_ManualOverride_DescribesOverrideExpiry(bool hasNextChange, string expected)
    {
        // Act
        var result = TrayTooltip.Format(
            true,
            3400,
            true,
            ScheduleMode.Custom,
            hasNextChange ? NextChange : null,
            CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(expected);
    }
}
