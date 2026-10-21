using System.Globalization;
using YourOrderWeDeliver.Core.Domain;

namespace YourOrderWeDeliver.Core.Common;

/// <summary>Formats dates, times and money consistently (Australian English) for user-facing text.</summary>
public static class DisplayFormat
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-AU");

    /// <summary>Gets the culture used for all user-facing formatting.</summary>
    public static CultureInfo DisplayCulture => Culture;

    /// <summary>Formats a date and time, for example "Tue 27 Oct 2026, 9:30 am".</summary>
    public static string DateAndTime(DateTime value) => value.ToString("ddd d MMM yyyy, h:mm tt", Culture);

    /// <summary>Formats a time of day, for example "9:30 am".</summary>
    public static string TimeOfDay(DateTime value) => value.ToString("h:mm tt", Culture);

    /// <summary>Formats a calendar date, for example "Tue 27 Oct 2026".</summary>
    public static string Day(DateOnly value) => value.ToString("ddd d MMM yyyy", Culture);

    /// <summary>Formats an amount in Australian dollars.</summary>
    public static string Money(decimal value) => value.ToString("C", Culture);

    /// <summary>Formats a window, omitting the second date when both ends fall on the same day.</summary>
    public static string Window(DeliveryWindow window)
    {
        return window.Start.Date == window.End.Date
            ? $"{DateAndTime(window.Start)} to {TimeOfDay(window.End)}"
            : $"{DateAndTime(window.Start)} to {DateAndTime(window.End)}";
    }
}
