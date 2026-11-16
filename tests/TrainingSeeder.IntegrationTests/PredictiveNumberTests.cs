using SequencingApi.Domain.Samples;
using TrainingSeeder.Generation;
using Xunit;

namespace TrainingSeeder.IntegrationTests;

/// <summary>
/// Every form the seeder writes a predictive number in must reduce to the same key the sequencing
/// API matches the biobank's form on - checked against the API's own reading of it.
/// </summary>
public sealed class PredictiveNumberTests
{
    [Theory]
    [InlineData(2020, 5)]
    [InlineData(2020, 15)]
    [InlineData(2020, 35)]
    [InlineData(2020, 150)]
    [InlineData(2020, 1052)]
    [InlineData(2013, 2015)]
    [InlineData(2029, 2030)]
    [InlineData(2012, 9999)]
    public void EveryFormReadsAsTheSameNumber(int year, int number)
    {
        var predictive = new PredictiveNumber(year, number);
        string?[] forms = [predictive.Biobank, predictive.SampleSheet(rna: false), predictive.SampleSheet(rna: true), predictive.MiSeq];

        foreach (var form in forms.OfType<string>())
        {
            var sample = SampleAggregate.Create("sample", "scheme", form).Value;
            Assert.Equal($"{year}-{number}", sample.PredictiveKey);
        }
    }

    [Theory]
    [InlineData(15)]
    [InlineData(2015)]
    public void HasNoMiSeqFormForANumberThatReadsAsAYear(int number) =>
        Assert.Null(new PredictiveNumber(2020, number).MiSeq);
}
