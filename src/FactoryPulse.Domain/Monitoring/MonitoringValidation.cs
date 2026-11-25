using System.Runtime.CompilerServices;

namespace FactoryPulse.Domain.Monitoring;

internal static class MonitoringValidation
{
    internal static string RequiredText(string value, int maxLength, [CallerArgumentExpression(nameof(value))] string? parameter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameter);
        string trimmed = value.Trim();
        ArgumentOutOfRangeException.ThrowIfGreaterThan(trimmed.Length, maxLength, parameter);
        return trimmed;
    }

    internal static void RequireUtc(DateTime value, [CallerArgumentExpression(nameof(value))] string? parameter = null)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The timestamp must be UTC.", parameter);
        }
    }
}
