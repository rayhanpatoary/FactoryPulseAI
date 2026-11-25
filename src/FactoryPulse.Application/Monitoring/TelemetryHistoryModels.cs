namespace FactoryPulse.Application.Monitoring;

public sealed record TelemetryHistoryFilter(int Page, int PageSize, DateTime? FromUtc, DateTime? ToUtc)
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;
    public const int MaxPageNumber = 10_000;
}

public sealed record TelemetryHistoryPage(int Page, int PageSize, bool HasMore, IReadOnlyList<MachineTelemetry> Items);
