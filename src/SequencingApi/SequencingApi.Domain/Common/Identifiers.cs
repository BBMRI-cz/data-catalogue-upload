namespace SequencingApi.Domain.Common;

/// <summary>A strongly-typed identifier wrapping a single string value.</summary>
public interface IStronglyTypedId
{
    string Value { get; }
}

/// <summary>
/// Identity of a sequenced sample — the opaque <c>external_id</c> the source biobank knows it by,
/// paired with an <c>id_scheme</c> on the aggregate that says which naming system it belongs to.
/// MMCI's instance is the pseudonymized predictive number (<c>mmci_predictive_&lt;uuid&gt;</c>).
/// The surrogate <c>sample_id</c> primary key proposed by the data report is a persistence concern
/// and is not modelled here.
/// </summary>
public readonly record struct SampleId(string Value) : IStronglyTypedId
{
    public override string ToString() => Value;
}

/// <summary>
/// Identity of a sequencing run / flowcell. MMCI's instance is the Illumina run-folder name
/// (<c>&lt;YYMMDD&gt;_&lt;instrument&gt;_&lt;run#&gt;_&lt;flowcell&gt;</c>). The same run id can be
/// discovered more than once in a source tree, so it doubles as the de-duplication key.
/// </summary>
public readonly record struct SequencingRunId(string Value) : IStronglyTypedId
{
    public override string ToString() => Value;
}

/// <summary>
/// Identity of a target panel — the gene set and target regions a sample was enriched for. Shared
/// by many samples, hence its own aggregate referenced by id.
/// </summary>
public readonly record struct PanelId(string Value) : IStronglyTypedId
{
    public override string ToString() => Value;
}
