namespace TrainingSeeder.Generation;

/// <summary>Values to draw from, each as often as its weight says: a column's real value counts.</summary>
internal sealed class Weighted<T>
{
    private readonly T[] _values;
    private readonly long[] _cumulative;

    public Weighted(IEnumerable<(T Value, long Weight)> items)
    {
        var values = new List<T>();
        var cumulative = new List<long>();
        long total = 0;
        foreach (var (value, weight) in items)
        {
            if (weight <= 0)
            {
                continue;
            }

            total += weight;
            values.Add(value);
            cumulative.Add(total);
        }

        _values = [.. values];
        _cumulative = [.. cumulative];
        Total = total;
    }

    public long Total { get; }

    public bool IsEmpty => _values.Length == 0;

    public T Draw(SeededRandom random)
    {
        if (IsEmpty)
        {
            throw new InvalidOperationException($"No {typeof(T).Name} to draw from.");
        }

        var index = Array.BinarySearch(_cumulative, random.Below(Total) + 1);
        return _values[index < 0 ? ~index : index];
    }

    public Weighted<T> Where(Func<T, bool> predicate) =>
        new(_values.Select((value, i) => (value, Weight(i))).Where(item => predicate(item.value)));

    private long Weight(int index) => _cumulative[index] - (index == 0 ? 0 : _cumulative[index - 1]);
}
