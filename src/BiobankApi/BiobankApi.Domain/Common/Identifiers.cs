namespace BiobankApi.Domain.Common;

/// <summary>A strongly-typed identifier wrapping a single string value.</summary>
public interface IStronglyTypedId
{
    string Value { get; }
}

/// <summary>Identity of a patient aggregate.</summary>
public readonly record struct PatientId(string Value) : IStronglyTypedId
{
    public override string ToString() => Value;
}

/// <summary>Identity of a sample entity.</summary>
public readonly record struct SampleId(string Value) : IStronglyTypedId
{
    public override string ToString() => Value;
}

/// <summary>Identity of a diagnostic specimen entity.</summary>
public readonly record struct SpecimenId(string Value) : IStronglyTypedId
{
    public override string ToString() => Value;
}
