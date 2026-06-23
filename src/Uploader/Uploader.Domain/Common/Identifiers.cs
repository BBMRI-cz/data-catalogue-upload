namespace Uploader.Domain.Common;

/// <summary>A strongly-typed identifier wrapping a single string value.</summary>
public interface IStronglyTypedId
{
    string Value { get; }
}

/// <summary>Identity of a patient aggregate (the biobank patient id).</summary>
public readonly record struct PatientId(string Value) : IStronglyTypedId
{
    public override string ToString() => Value;
}

/// <summary>Identity of a sample aggregate (the biobank sample id).</summary>
public readonly record struct SampleId(string Value) : IStronglyTypedId
{
    public override string ToString() => Value;
}

/// <summary>Identity of a sequencing aggregate (the predictive number).</summary>
public readonly record struct SequencingId(string Value) : IStronglyTypedId
{
    public override string ToString() => Value;
}

/// <summary>Identity of a WSI aggregate (the bioptic number).</summary>
public readonly record struct WsiId(string Value) : IStronglyTypedId
{
    public override string ToString() => Value;
}

/// <summary>Identity of an imaging-study aggregate (the radiology accession number).</summary>
public readonly record struct AccessionNumber(string Value) : IStronglyTypedId
{
    public override string ToString() => Value;
}

/// <summary>Reference to a FAIR Genomes fixed block, shared by sequencing and WSI.</summary>
public readonly record struct FixedBlockId(string Value) : IStronglyTypedId
{
    public override string ToString() => Value;
}
