namespace BiobankApi.Domain;

/// <summary>Patient biological sex as exported in the <c>&lt;patient sex&gt;</c> attribute.</summary>
public enum Sex
{
    Male,
    Female,
}

/// <summary>
/// The <c>&lt;retrieved&gt;</c> state of a sample: <c>Operational</c> = taken during a
/// surgical/clinical procedure; <c>Unknown</c> = context not recorded.
/// </summary>
public enum Retrieved
{
    Operational,
    Unknown,
}
