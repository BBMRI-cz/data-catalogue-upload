namespace Uploader.Application.Abstractions;

/// <summary>
/// The single study this deployment publishes under, from configuration rather than any source.
/// <para>
/// EMX2's <c>Personal</c> table references a study, so the rows need one to point at, and a
/// biobank uploading its own patients is one study by definition. A second biobank is a second
/// deployment with a different identifier, the same way the pseudonym prefix works.
/// </para>
/// </summary>
public sealed record CatalogueStudy(string Identifier, string? Name);
