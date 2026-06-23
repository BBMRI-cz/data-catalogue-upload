namespace BiobankApi.Domain;

/// <summary>Biological sex of a patient.</summary>
public enum Sex
{
    Male,
    Female,
}

/// <summary>
/// Whether a sample was retrieved during a clinical/surgical procedure (<c>Operational</c>) or its
/// context was not recorded (<c>Unknown</c>).
/// </summary>
public enum Retrieved
{
    Operational,
    Unknown,
}
