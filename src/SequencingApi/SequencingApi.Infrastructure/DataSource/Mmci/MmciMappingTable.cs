namespace SequencingApi.Infrastructure.DataSource.Mmci;

/// <summary>
/// The pseudonymized → real predictive-number mapping, read from the pseudonymizer's
/// <c>mapping_table/predictive.json</c>.
/// </summary>
/// <remarks>
/// MMCI has two values both called "predictive number": the pseudonymized one
/// (<c>mmci_predictive_&lt;uuid&gt;</c>) that names the sample folders in the run tree, and the real
/// one the biobank knows the sample by. Only the pseudonymized one is present in the tree, so this
/// table is the sole route to the real number — which is exactly the value the patient API stores as
/// a sample's <c>predictive_number</c>, and therefore the only thing that lets the two services be
/// joined. It becomes <c>SampleAggregate.SubjectRef</c>.
/// <para>
/// MMCI vocabulary is deliberate here: this is adapter-side, and the adapter boundary is what keeps
/// it out of the domain, where the same value is an opaque <c>subject_ref</c>.
/// </para>
/// </remarks>
internal sealed class MmciMappingTable
{
    /// <summary>The empty table — used when no mapping file could be read, so lookups just miss.</summary>
    public static MmciMappingTable Empty { get; } = new(new Dictionary<string, string>(StringComparer.Ordinal));

    private readonly IReadOnlyDictionary<string, string> _realByPseudonymized;

    public MmciMappingTable(IReadOnlyDictionary<string, string> realByPseudonymized)
    {
        _realByPseudonymized = realByPseudonymized;
    }

    public int Count => _realByPseudonymized.Count;

    /// <summary>
    /// The real predictive number for a pseudonymized one, or null when the table does not cover it.
    /// A miss is normal, not an error: NextSeq samples in particular are routinely absent.
    /// </summary>
    public string? RealPredictiveNumber(string pseudonymizedNumber) =>
        _realByPseudonymized.GetValueOrDefault(pseudonymizedNumber);
}
