using System.ComponentModel.DataAnnotations;
using System.Globalization;
using FactoryPulse.Application.Monitoring;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace FactoryPulse.Api.Monitoring;

public sealed class TelemetryHistoryRequest
{
    private const string InvalidTimestamp = "Use an ISO 8601 UTC timestamp ending in Z or +00:00.";
    private static readonly string[] UtcFormats = ["yyyy-MM-dd'T'HH:mm:ssK", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK"];

    [Range(1, TelemetryHistoryFilter.MaxPageNumber)]
    public int Page { get; init; } = 1;

    [Range(1, TelemetryHistoryFilter.MaxPageSize)]
    public int PageSize { get; init; } = TelemetryHistoryFilter.DefaultPageSize;

    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? FromUtc { get; init; }

    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? ToUtc { get; init; }

    internal TelemetryHistoryFilter? GetFilter(ModelStateDictionary errors)
    {
        if (!TryUtc(FromUtc, out DateTime? from))
        {
            errors.AddModelError("fromUtc", InvalidTimestamp);
        }

        if (!TryUtc(ToUtc, out DateTime? to))
        {
            errors.AddModelError("toUtc", InvalidTimestamp);
        }

        if (from >= to)
        {
            errors.AddModelError("toUtc", "toUtc must be later than fromUtc.");
        }

        return errors.IsValid ? new TelemetryHistoryFilter(Page, PageSize, from, to) : null;
    }

    private static bool TryUtc(string? value, out DateTime? utc)
    {
        utc = null;
        if (value is null)
        {
            return true;
        }

        if (!(value.EndsWith('Z') || value.EndsWith("+00:00", StringComparison.Ordinal))
            || !DateTimeOffset.TryParseExact(value, UtcFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset parsed))
        {
            return false;
        }

        utc = parsed.UtcDateTime;
        return true;
    }
}
