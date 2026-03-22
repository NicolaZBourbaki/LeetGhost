namespace LeetGhost.Configuration;

/// <summary>
/// Schedule and automation timing settings.
/// </summary>
public class ScheduleSettings
{
    public const string SectionName = "Schedule";

    /// <summary>
    /// Cron expression for when to check streak status.
    /// Default: every 30 minutes.
    /// </summary>
    public string CheckCronExpression { get; set; } = "*/30 * * * *";

    /// <summary>
    /// Hour of the day (0-23) after which automation should start if no submission exists.
    /// Default: 23 (11 PM).
    /// </summary>
    public int AutomationStartHour { get; set; } = 23;

    /// <summary>
    /// Minute of the hour (0-59) after which automation should start.
    /// Default: 0.
    /// </summary>
    public int AutomationStartMinute { get; set; } = 0;

    /// <summary>
    /// Default timezone for new users.
    /// Default: UTC.
    /// </summary>
    public string TimeZone { get; set; } = "UTC";
}
