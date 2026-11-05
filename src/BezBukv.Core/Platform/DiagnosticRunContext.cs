using System.Security.Cryptography;

namespace BezBukv.Core.Platform;

public sealed class DiagnosticRunContext
{
    private const int FingerprintSaltSizeInBytes = 32;

    private readonly byte[]? _fingerprintSalt;

    private DiagnosticRunContext(string runId, byte[]? fingerprintSalt)
    {
        RunId = runId;
        _fingerprintSalt = fingerprintSalt;
    }

    public static DiagnosticRunContext Disabled { get; } = new(string.Empty, null);

    public string RunId { get; }

    public bool IsEnabled => _fingerprintSalt is not null;

    public static DiagnosticRunContext Start() =>
        new(
            CreateCorrelationId(),
            RandomNumberGenerator.GetBytes(FingerprintSaltSizeInBytes));

    public string CreateClickId() => IsEnabled ? CreateCorrelationId() : string.Empty;

    public DiagnosticPayloadMetrics MeasurePayload(
        string payload,
        bool includeFingerprint = false) =>
        includeFingerprint && _fingerprintSalt is not null
            ? DiagnosticPayloadMetrics.Create(payload, _fingerprintSalt)
            : DiagnosticPayloadMetrics.Create(payload);

    private static string CreateCorrelationId() => Guid.NewGuid().ToString("N");
}
