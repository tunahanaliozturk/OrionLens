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

    /// <summary>Lowercase hex digits, indexed by nibble value, for span-based formatting.</summary>
    private static ReadOnlySpan<char> HexLower => "0123456789abcdef";

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

        // Version 00 is exactly four fields; only a later version may append more. Reject extra
        // trailing segments under 00 rather than silently parsing a malformed header.
        if (fields[0] == "00" && fields.Length != 4)
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
    public static string? Format(string correlationId, Activity? activity) =>
        Format(correlationId, activity, ToTraceId(correlationId));

    /// <summary>
    /// As <see cref="Format(string, Activity?)"/>, but accepts the correlation id's derived trace-id
    /// when the caller has already computed it (the propagator computes it to decide whether an
    /// ambient activity is aligned), so it is not hashed a second time on the inject hot path.
    /// </summary>
    /// <param name="correlationId">The correlation id to align the trace-id with.</param>
    /// <param name="activity">The current activity, if any.</param>
    /// <param name="derivedTraceId">The result of <see cref="ToTraceId(string)"/> for the id.</param>
    public static string? Format(string correlationId, Activity? activity, string? derivedTraceId)
    {
        if (activity is not null && activity.IdFormat == ActivityIdFormat.W3C)
        {
            var traceId = activity.TraceId.ToHexString();
            if (!IsAllZeros(traceId))
            {
                return $"00-{traceId}-{activity.SpanId.ToHexString()}-{FlagsFor(activity)}";
            }
        }

        if (derivedTraceId is null)
        {
            return null;
        }

        var spanId = ActivitySpanId.CreateRandom().ToHexString();
        return $"00-{derivedTraceId}-{spanId}-00";
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

        // Hash the id's UTF-8 bytes into a stable 16-byte trace-id. Encode the source into a stack
        // buffer when it fits (the common case for ids of a few dozen chars) so no byte[] is rented;
        // fall back to the heap only for pathologically long ids.
        var maxByteCount = System.Text.Encoding.UTF8.GetMaxByteCount(correlationId.Length);
        byte[]? rented = null;
        Span<byte> utf8 = maxByteCount <= 256
            ? stackalloc byte[256]
            : (rented = System.Buffers.ArrayPool<byte>.Shared.Rent(maxByteCount));

        Span<byte> hash = stackalloc byte[32];
        try
        {
            var written = System.Text.Encoding.UTF8.GetBytes(correlationId, utf8);
            System.Security.Cryptography.SHA256.HashData(utf8[..written], hash);
        }
        finally
        {
            if (rented is not null)
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(rented);
            }
        }

        // Take the first 16 bytes as the trace-id; guard the all-zero edge case. Format lowercase hex
        // directly into a stack buffer to avoid the uppercase-then-lower double string allocation.
        Span<char> hex = stackalloc char[TraceIdHexLength];
        if (!TryWriteHexLower(hash[..16], hex))
        {
            return null;
        }

        return new string(hex);
    }

    /// <summary>
    /// Write the lowercase hex encoding of <paramref name="bytes"/> into <paramref name="destination"/>.
    /// Returns false when every nibble is zero (an all-zero trace-id, which is invalid).
    /// </summary>
    private static bool TryWriteHexLower(ReadOnlySpan<byte> bytes, Span<char> destination)
    {
        var hexLower = HexLower;
        var allZero = true;
        for (var i = 0; i < bytes.Length; i++)
        {
            var b = bytes[i];
            destination[(i * 2) + 0] = hexLower[b >> 4];
            destination[(i * 2) + 1] = hexLower[b & 0xF];
            allZero &= b == 0;
        }

        return !allZero;
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
