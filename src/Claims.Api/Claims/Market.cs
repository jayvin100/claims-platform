namespace Claims.Api.Claims;

/// <summary>The six fixed Markets (D7). Each has exactly one currency.</summary>
public enum Market
{
    SG,
    MY,
    HK,
    TH,
    ID,
    PH,
}

public static class Markets
{
    // One row per Market, so adding a Market is one line. The time zone is the capital's.
    private static readonly Dictionary<Market, (string Currency, string TimeZoneId)> Table = new()
    {
        [Market.SG] = ("SGD", "Asia/Singapore"),
        [Market.MY] = ("MYR", "Asia/Kuala_Lumpur"),
        [Market.HK] = ("HKD", "Asia/Hong_Kong"),
        [Market.TH] = ("THB", "Asia/Bangkok"),
        [Market.ID] = ("IDR", "Asia/Jakarta"),
        [Market.PH] = ("PHP", "Asia/Manila"),
    };

    /// <summary>The Market's currency. Never taken from input, so amounts in different currencies can't mix.</summary>
    public static string CurrencyOf(Market market) => Table[market].Currency;

    /// <summary>Today's date on the Market's own clock, so a Claimant just after local midnight isn't refused.</summary>
    public static DateOnly TodayIn(Market market, DateTimeOffset now)
    {
        var local = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(Table[market].TimeZoneId));
        return DateOnly.FromDateTime(local.DateTime);
    }
}
