namespace Application.Common;

public static class CountryCurrency
{
    public static string ForCountry(string? countryCode, string fallback = "GBP") =>
        countryCode?.Trim().ToUpperInvariant() switch
        {
            "NG" or "NGA" => "NGN",
            "CN" or "CHN" => "CNY",
            "GB" or "GBR" or "UK" => "GBP",
            "US" or "USA" => "USD",
            _ => fallback.Trim().ToUpperInvariant()
        };
}
