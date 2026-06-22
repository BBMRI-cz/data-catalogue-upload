using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Uploader.Domain.Services;

/// <summary>Domain service computing a stable SHA-256 fingerprint of one or more value objects.</summary>
public interface IFingerprintCalculator
{
    /// <summary>Fingerprint the non-null arguments together as an ordered JSON array.</summary>
    string Compute(params object?[] objects);
}

/// <inheritdoc cref="IFingerprintCalculator"/>
public sealed class FingerprintCalculator : IFingerprintCalculator
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public string Compute(params object?[] objects)
    {
        // Serialize each object by its run-time type (so derived records keep their members),
        // then hash the canonical array. Deterministic within the service.
        var parts = objects
            .Where(value => value is not null)
            .Select(value => JsonSerializer.Serialize(value, value!.GetType(), Options));

        var serialized = "[" + string.Join(",", parts) + "]";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(serialized));
        return Convert.ToHexStringLower(hash);
    }
}
