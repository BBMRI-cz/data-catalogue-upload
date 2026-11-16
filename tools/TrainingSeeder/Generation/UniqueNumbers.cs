namespace TrainingSeeder.Generation;

/// <summary>Hands out random numbers that are never handed out twice for the same year.</summary>
internal sealed class UniqueNumbers
{
    private const int Attempts = 10_000;

    private readonly HashSet<(int Year, int Number)> _taken = [];
    private readonly int _min;
    private readonly int _max;

    public UniqueNumbers(int min, int max)
    {
        _min = min;
        _max = max;
    }

    public int Next(SeededRandom random, int year = 0)
    {
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            var number = random.Between(_min, _max);
            if (_taken.Add((year, number)))
            {
                return number;
            }
        }

        throw new InvalidOperationException($"No free number left between {_min} and {_max} for {year}.");
    }
}
