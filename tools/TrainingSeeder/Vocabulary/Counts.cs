using System.Text.Json;

namespace TrainingSeeder.Vocabulary;

/// <summary>One value of a column and how many rows hold it. The value is a string, a number, a
/// boolean or null, as the database stored it.</summary>
internal sealed record ValueCount(JsonElement Value, long Count);

/// <summary>The spread of a measured number: min, median and max over the rows that state it.</summary>
internal sealed record NumberRange(double? Min, double? Median, double? Max, long Filled, long Total)
{
    /// <summary>How often a row states the number at all.</summary>
    public double FilledShare => Total == 0 ? 0 : (double)Filled / Total;
}

/// <summary>How many rows have a value in a column whose values stay on the server.</summary>
internal sealed record FilledCount(long Filled, long Total)
{
    public double Share => Total == 0 ? 0 : (double)Filled / Total;
}
