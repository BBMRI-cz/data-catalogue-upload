namespace Uploader.Application.Abstractions;

/// <summary>
/// The identifiers this uploader pseudonymizes itself. The predictive number is deliberately absent:
/// the sequencing API already answers with the pseudonymized id the run tree is named by, so minting
/// a second one here would name a folder that does not exist.
/// </summary>
public enum PseudonymKind
{
    Patient,
    Sample,
}

/// <summary>
/// Real identifier -> the pseudonym published in its place. Stable across runs: a pseudonym minted
/// once is stored and returned again, so a second run updates a catalogue record rather than
/// duplicating it.
/// </summary>
public interface IPseudonymMap
{
    Task<string> PseudonymizeAsync(PseudonymKind kind, string realId, CancellationToken cancellationToken);
}
