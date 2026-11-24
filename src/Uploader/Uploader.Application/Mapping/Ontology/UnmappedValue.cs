using Uploader.Application.Abstractions;

namespace Uploader.Application.Mapping;

/// <summary>
/// A source value the resolver could not fill its field from - neither a live term nor answered by the
/// mapping table - recorded exactly as the source served it, with the context it was looked up in.
/// Field, value and context are the mapping table's whole key, so this is what an entry would have to
/// be keyed on to fill it. It carries no identifier and no record.
/// </summary>
public sealed record UnmappedValue(OntologyField Field, string Value, TermContext Context);
