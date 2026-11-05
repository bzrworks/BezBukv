using System.Security.Cryptography;
using System.Text;

namespace BezBukv.Core.Platform;

public sealed record DiagnosticPayloadMetrics(
    int CharCount,
    int ByteCount,
    int LineCount,
    string? Fingerprint)
{
    public static DiagnosticPayloadMetrics Create(string payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return CreateCore(payload, fingerprintSalt: default);
    }

    public static DiagnosticPayloadMetrics Create(
        string payload,
        ReadOnlySpan<byte> fingerprintSalt)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (fingerprintSalt.IsEmpty)
        {
            throw new ArgumentException(
                "The fingerprint salt must not be empty.",
                nameof(fingerprintSalt));
        }

        return CreateCore(payload, fingerprintSalt);
    }

    private static DiagnosticPayloadMetrics CreateCore(
        string payload,
        ReadOnlySpan<byte> fingerprintSalt)
    {
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var fingerprint = fingerprintSalt.IsEmpty
            ? null
            : CreateFingerprint(payloadBytes, fingerprintSalt);

        return new DiagnosticPayloadMetrics(
            payload.Length,
            payloadBytes.Length,
            CountLines(payload),
            fingerprint);
    }

    private static int CountLines(string payload)
    {
        if (payload.Length == 0)
        {
            return 0;
        }

        var lineCount = 1;

        for (var index = 0; index < payload.Length; index++)
        {
            if (payload[index] == '\r')
            {
                lineCount++;

                if (index + 1 < payload.Length && payload[index + 1] == '\n')
                {
                    index++;
                }
            }
            else if (payload[index] == '\n')
            {
                lineCount++;
            }
        }

        return lineCount;
    }

    private static string CreateFingerprint(
        byte[] payloadBytes,
        ReadOnlySpan<byte> fingerprintSalt)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(fingerprintSalt);
        hash.AppendData(payloadBytes);

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}
