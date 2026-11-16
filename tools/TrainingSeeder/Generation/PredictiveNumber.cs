using System.Globalization;

namespace TrainingSeeder.Generation;

/// <summary>
/// A predictive number - the key both databases carry, and the join between them. The biobank writes one
/// form, sample sheets two more, and the sequencing API matches all three on their year and number.
/// </summary>
internal readonly record struct PredictiveNumber(int Year, int Number)
{
    /// <summary>The biobank's form, <c>2020/1052</c>.</summary>
    public string Biobank => string.Create(CultureInfo.InvariantCulture, $"{Year}/{Number}");

    /// <summary>A NextSeq sample sheet's form, <c>2020_1052_DNA</c>.</summary>
    public string SampleSheet(bool rna) =>
        string.Create(CultureInfo.InvariantCulture, $"{Year}_{Number}_{(rna ? "RNA" : "DNA")}");

    /// <summary>
    /// The older MiSeq form, <c>1052-20</c> - or null for a number that reads as a year in it: the
    /// sequencing API takes <c>15-20</c> for 2015 number 20 and <c>2015-20</c> for 2015 number 20,
    /// so neither would find the patient.
    /// </summary>
    public string? MiSeq => Number is (>= 10 and <= 29) or (>= 2010 and <= 2029)
        ? null
        : string.Create(CultureInfo.InvariantCulture, $"{Number}-{Year % 100:00}");
}
