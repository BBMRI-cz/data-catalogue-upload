using System.Text.Json;
using Xunit;

namespace Uploader.IntegrationTests;

/// <summary>
/// The both-directions key comparison the source-contract parity tests share: every key a source API
/// serves has a DTO property to land on, and every DTO property has a key. Both directions matter — a
/// renamed field shows up as one missing key and one orphaned property, and a field the source adds
/// shows up as a key with nowhere to go.
/// </summary>
internal static class ContractParity
{
    public static void AssertKeysMatch<T>(JsonElement served)
    {
        var wireKeys = served.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        var dtoKeys = typeof(T)
            .GetProperties()
            .Select(property => JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Name))
            .ToHashSet(StringComparer.Ordinal);

        var unmapped = wireKeys.Except(dtoKeys).Order(StringComparer.Ordinal);
        var orphaned = dtoKeys.Except(wireKeys).Order(StringComparer.Ordinal);

        Assert.Equal(string.Empty, string.Join(", ", unmapped));  // served but no DTO property
        Assert.Equal(string.Empty, string.Join(", ", orphaned));  // DTO property but never served
    }
}
