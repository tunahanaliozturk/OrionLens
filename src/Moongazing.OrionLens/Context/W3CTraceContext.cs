namespace Moongazing.OrionLens.Context;

using System.Diagnostics;

/// <summary>
/// Helpers for bridging an OrionLens correlation id to the W3C trace context (a <c>traceparent</c>
/// header of the form <c>version-traceid-spanid-flags</c>, for example
/// <c>00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01</c>). The trace-id is 32 lowercase hex
/// characters, the span-id 16. See <see href="https://www.w3.org/TR/trace-context/"/>.
/// </summary>
internal static class W3CTraceContext
{
    private const int TraceIdHexLength = 32;

    /// <summary>
    /// Parse the 32-hex trace-id out of a <c>traceparent</c> header value, or return null when the
    /// value is absent or malformed. Validation is deliberately lenient about the version and flags
    /// fields, requiring only that the trace-id is 32 hex digits and is not all zeros.
    /// </summary>
    /// <param name="traceParent">The inbound <c>traceparent</c> header value.</param>
    public static string? TryGetTraceId(string? traceParent)
    {
        if (string.IsNullOrEmpty(traceParent))
        {
            return null;
        }

        var fields = traceParent.Split('-');
        if (fields.Length < 4)
        {
            return null;
        }

        var traceId = fields[1];
        if (traceId.Length != TraceIdHexLength || !IsLowerHex(traceId) || IsAllZeros(traceId))
        {
            return null;
        }

        return traceId;
    }

    /// <summary>
    /// Build a <c>traceparent</c> value that carries the given correlation id as its trace-id. When
    /// an <see cref="Activity"/> with W3C ids is supplied its trace-id and span-id are used verbatim
    /// (so the emitted value matches the live trace); otherwise the correlation id is coerced into a
    /// valid 32-hex trace-id and a fresh span-id is generated. Returns null when no usable trace-id
    /// can be formed.
    /// </summary>
    /// <param name="correlationId">The correlation id to align the trace-id with.</param>
    /// <param name="activity">The current activity, if any.</param>
    public static string? Format(string correlationId, Activity? activity)
    {
        if (activity is not null && activity.IdFormat == ActivityIdFormat.W3C)
        {
            var traceId = activity.TraceId.ToHexString();
            if (!IsAllZeros(traceId))
            {
                return $"00-{traceId}-{activity.SpanId.ToHexString()}-{FlagsFor(activity)}";
            }
        }

        var derived = ToTraceId(correlationId);
        if (derived is null)
        {
            return null;
        }

        var spanId = ActivitySpanId.CreateRandom().ToHexString();
        return $"00-{derived}-{spanId}-00";
    }

    /// <summary>
    /// Coerce an arbitrary correlation id into a valid 32-hex W3C trace-id. An id that is already 32
    /// lowercase hex digits (for example a <c>Guid.ToString("N")</c> or an upstream trace-id) is used
    /// as-is; anything else is hashed into a stable 16-byte id so the same correlation id always maps
    /// to the same trace-id. Returns null when no non-zero id can be formed.
    /// </summary>
    /// <param name="correlationId">The correlation id to convert.</param>
    public static string? ToTraceId(string correlationId)
    {
        if (string.IsNullOrEmpty(correlationId))
        {
            return null;
        }

        if (correlationId.Length == TraceIdHexLength && IsLowerHex(correlationId) && !IsAllZeros(correlationId))
        {
            return correlationId;
        }

        Span<byte> hash = stackalloc byte[32];
        System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(correlationId), hash);

        // Take the first 16 bytes as the trace-id; guard the all-zero edge case.
        // Convert.ToHexStringLower is net9+, so use the net8-compatible uppercase overload then lower.
        var traceId = Convert.ToHexString(hash[..16]).ToLowerInvariant();
        return IsAllZeros(traceId) ? null : traceId;
    }

    private static string FlagsFor(Activity activity) =>
        (activity.ActivityTraceFlags & ActivityTraceFlags.Recorded) != 0 ? "01" : "00";

    private static bool IsLowerHex(string value)
    {
        foreach (var c in value)
        {
            var isLowerHex = c is >= '0' and <= '9' or >= 'a' and <= 'f';
            if (!isLowerHex)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAllZeros(string value)
    {
        foreach (var c in value)
        {
            if (c != '0')
            {
                return false;
            }
        }

        return true;
    }
}
