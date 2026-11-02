namespace PlcApp.Models;

/// <summary>
/// Unitatea in care PLC-ul tine contoarele de timp.
/// Se poate schimba din interfata, fara a reporni aplicatia.
/// </summary>
public enum TimeUnitKind
{
    Secunde = 0,
    Minute = 1,
    Ore = 2,
    Brut = 3
}

public static class TimeFormat
{
    public static readonly string[] Options = { "Secunde", "Minute", "Ore", "Valoare bruta" };

    public static TimeSpan ToTimeSpan(int raw, TimeUnitKind unit) => unit switch
    {
        TimeUnitKind.Secunde => TimeSpan.FromSeconds(raw),
        TimeUnitKind.Minute => TimeSpan.FromMinutes(raw),
        TimeUnitKind.Ore => TimeSpan.FromHours(raw),
        _ => TimeSpan.Zero
    };

    public static string Format(int raw, TimeUnitKind unit)
    {
        if (unit == TimeUnitKind.Brut)
            return raw.ToString();

        var span = ToTimeSpan(raw, unit);

        if (span.TotalDays >= 1)
            return $"{(int)span.TotalDays}z {span.Hours:00}h {span.Minutes:00}m";

        if (span.TotalHours >= 1)
            return $"{(int)span.TotalHours}h {span.Minutes:00}m";

        if (span.TotalMinutes >= 1)
            return $"{(int)span.TotalMinutes}m {span.Seconds:00}s";

        return $"{(int)span.TotalSeconds}s";
    }
}
