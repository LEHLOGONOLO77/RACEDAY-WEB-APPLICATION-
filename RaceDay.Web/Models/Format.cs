namespace RaceDay.Web.Models;

/// <summary>Display helpers shared by the views.</summary>
public static class Format
{
    public static string Date(DateTime d) => d.ToString("ddd, d MMM yyyy");
    public static string DateTimeLong(DateTime d) => d.ToString("ddd, d MMM yyyy · HH:mm");
    public static string Money(decimal amount) => amount == 0 ? "Free" : $"R{amount:0.00}";
    public static string Km(decimal km) => $"{km:0.##} km";
    public static string Time(TimeSpan t) => $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";

    /// <summary>47 → "47th". Handles 11th, 12th, 13th.</summary>
    public static string Ordinal(int n)
    {
        var suffix = (n % 100) is >= 11 and <= 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return $"{n}{suffix}";
    }

    /// <summary>CSS class for the colour-coded status pills (green Confirmed, amber Pending, ...).</summary>
    public static string StatusClass(string status) => status switch
    {
        "Confirmed" or "Paid" or "Open" or "Completed" => "status-green",
        "Pending" or "Draft" or "Closed" => "status-amber",
        "Cancelled" or "Refunded" => "status-red",
        _ => "status-grey"
    };

    public static bool TryParseFinishTime(string? input, out TimeSpan time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(input)) return false;
        var parts = input.Trim().Split(':');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var h) || !int.TryParse(parts[1], out var m) || !int.TryParse(parts[2], out var s))
            return false;
        if (h < 0 || m is < 0 or > 59 || s is < 0 or > 59) return false;
        time = new TimeSpan(h, m, s);
        return time > TimeSpan.Zero;
    }
}
