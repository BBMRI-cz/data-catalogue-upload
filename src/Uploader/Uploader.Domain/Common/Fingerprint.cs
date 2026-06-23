using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Uploader.Domain.Common;

/// <summary>
/// A stable SHA-256 fingerprint of an aggregate's catalogue-relevant content. Change detection is
/// domain behaviour: each aggregate root computes its own fingerprint, and the sync planner only
/// compares them.
/// </summary>
public readonly record struct Fingerprint
{
    private static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private Fingerprint(string value) => Value = value;

    public string Value { get; }

    /// <summary>Rehydrate a fingerprint previously persisted as a string in the sync state.</summary>
    public static Fingerprint FromStored(string value) => new(value);

    /// <summary>
    /// Fingerprint the non-null arguments together as an ordered JSON array. Each value is
    /// serialized by its run-time type, so derived records keep their members.
    /// </summary>
    public static Fingerprint Of(params object?[] values)
    {
        var parts = values
            .Where(value => value is not null)
            .Select(value => JsonSerializer.Serialize(value, value!.GetType(), CanonicalOptions));

        var serialized = "[" + string.Join(",", parts) + "]";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(serialized));
        return new Fingerprint(Convert.ToHexStringLower(hash));
    }

    public override string ToString() => Value;
}
