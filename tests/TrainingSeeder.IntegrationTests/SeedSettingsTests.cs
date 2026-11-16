using Microsoft.Extensions.Configuration;
using Xunit;

namespace TrainingSeeder.IntegrationTests;

public sealed class SeedSettingsTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("250", 250)]
    public void ABlankPatientCountMeansTheRealOne(string? raw, int? expected)
    {
        var settings = SeedSettings.FromConfiguration(Configuration(("SEED_PATIENTS", raw)));

        Assert.False(settings.IsError);
        Assert.Equal(expected, settings.Value.Patients);
        Assert.Equal(SeedSettings.DefaultSeed, settings.Value.Seed);
    }

    [Theory]
    [InlineData("SEED_PATIENTS", "0")]
    [InlineData("SEED_PATIENTS", "-3")]
    [InlineData("SEED_PATIENTS", "many")]
    [InlineData("SEED", "random")]
    public void RejectsWhatIsNotAWholeNumber(string name, string raw)
    {
        var settings = SeedSettings.FromConfiguration(Configuration((name, raw)));

        Assert.True(settings.IsError);
        Assert.Equal(name, settings.FirstError.Code);
    }

    [Fact]
    public void ReadsTheSeed()
    {
        var settings = SeedSettings.FromConfiguration(Configuration(("SEED", "7")));

        Assert.Equal(7, settings.Value.Seed);
    }

    private static IConfiguration Configuration(params (string Key, string? Value)[] settings) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(setting => new KeyValuePair<string, string?>(setting.Key, setting.Value)))
            .Build();
}
