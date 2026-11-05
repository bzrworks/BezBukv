using System.Text.Json;
using BezBukv.Core.Platform;

namespace BezBukv.Core.Tests;

public sealed class DiagnosticPayloadMetricsTests
{
    [Theory]
    [InlineData("", 0, 0, 0)]
    [InlineData("abc", 3, 3, 1)]
    [InlineData("я😊\r\nx\n", 7, 10, 3)]
    [InlineData("first\rsecond", 12, 12, 2)]
    public void CreateReportsUtf16CharCountUtf8ByteCountAndLogicalLineCount(
        string payload,
        int expectedCharCount,
        int expectedByteCount,
        int expectedLineCount)
    {
        var metrics = DiagnosticPayloadMetrics.Create(payload);

        Assert.Equal(expectedCharCount, metrics.CharCount);
        Assert.Equal(expectedByteCount, metrics.ByteCount);
        Assert.Equal(expectedLineCount, metrics.LineCount);
        Assert.Null(metrics.Fingerprint);
    }

    [Fact]
    public void CreateDoesNotRetainOrReturnRawPayload()
    {
        const string payload = "sensitive raw text";
        var metrics = DiagnosticPayloadMetrics.Create(payload, [1, 2, 3, 4]);
        var serializedMetrics = JsonSerializer.Serialize(metrics);

        Assert.DoesNotContain(payload, metrics.ToString());
        Assert.DoesNotContain(payload, serializedMetrics);
        Assert.NotEqual(payload, metrics.Fingerprint);
    }

    [Fact]
    public void FingerprintIsStableForSameSaltAndDistinguishesPayloads()
    {
        byte[] salt = [10, 20, 30, 40];

        var first = DiagnosticPayloadMetrics.Create("payload one", salt);
        var repeated = DiagnosticPayloadMetrics.Create("payload one", salt);
        var differentPayload = DiagnosticPayloadMetrics.Create("payload two", salt);

        Assert.NotNull(first.Fingerprint);
        Assert.Equal(first.Fingerprint, repeated.Fingerprint);
        Assert.NotEqual(first.Fingerprint, differentPayload.Fingerprint);
    }
}
