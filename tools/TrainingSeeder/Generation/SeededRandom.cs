using TrainingSeeder.Vocabulary;

namespace TrainingSeeder.Generation;

/// <summary>
/// The dataset's only source of randomness. Seeded, so one seed always yields the same dataset:
/// <see cref="Random"/>'s seeded algorithm is frozen for compatibility, unlike the unseeded one.
/// </summary>
internal sealed class SeededRandom
{
    private const string Alphanumeric = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    private readonly Random _random;

    public SeededRandom(int seed) => _random = new Random(seed);

    public int Between(int minInclusive, int maxInclusive) => _random.Next(minInclusive, maxInclusive + 1);

    public long Between(long minInclusive, long maxInclusive) => _random.NextInt64(minInclusive, maxInclusive + 1);

    public long Below(long maxExclusive) => _random.NextInt64(maxExclusive);

    public double Fraction() => _random.NextDouble();

    public bool Chance(double probability) => _random.NextDouble() < probability;

    public T Pick<T>(IReadOnlyList<T> items) => items[_random.Next(items.Count)];

    public void Shuffle<T>(T[] items) => _random.Shuffle(items);

    public DateOnly Date(DateOnly from, DateOnly to) => from.AddDays(_random.Next(to.DayNumber - from.DayNumber + 1));

    /// <summary>A moment on a working day's hours somewhere in <paramref name="year"/>.</summary>
    public DateTime Moment(int year) =>
        new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Unspecified)
            .AddDays(_random.Next(365))
            .AddMinutes(Between(7 * 60, 16 * 60));

    public string Characters(int length)
    {
        var characters = new char[length];
        for (var i = 0; i < length; i++)
        {
            characters[i] = Alphanumeric[_random.Next(Alphanumeric.Length)];
        }

        return new string(characters);
    }

    /// <summary>A version-4 UUID made from this generator's bytes, so it is as repeatable as the rest.</summary>
    public Guid Guid()
    {
        Span<byte> bytes = stackalloc byte[16];
        _random.NextBytes(bytes);
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }

    /// <summary>
    /// A number inside <paramref name="range"/> that keeps its median: half the draws fall below it,
    /// half above, bunched towards the median with a long tail out to min and max. The export gives
    /// only those three numbers, and read depths run from 0 to fifty times the median.
    /// </summary>
    public double AroundMedian(NumberRange range)
    {
        if (range is not { Min: { } min, Median: { } median, Max: { } max })
        {
            throw new InvalidOperationException("A range with no values has nothing to draw from.");
        }

        var spread = Math.Pow(_random.NextDouble(), 4);
        return _random.Next(2) == 0 ? median - ((median - min) * spread) : median + ((max - median) * spread);
    }
}
