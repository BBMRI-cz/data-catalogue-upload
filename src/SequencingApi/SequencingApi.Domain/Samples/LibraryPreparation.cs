using ErrorOr;
using SequencingApi.Domain.Common;

namespace SequencingApi.Domain.Samples;

/// <summary>
/// How a sample was turned into a sequencing library before it went on the instrument, and which
/// target panel it was enriched for.
/// </summary>
/// <remarks>
/// Every field is optional, including the panel. This information is not recorded in the run
/// itself — it comes from a separately maintained libraries table whose columns differ between
/// versions, matched to a sample after the fact by rules that regularly fail to resolve. A model
/// that demanded any of it would reject data that legitimately exists.
/// </remarks>
public sealed record LibraryPreparation : ValueObject
{
    // Internal (not private) so the persistence mapper can reconstitute loaded rows; the public
    // creation path stays Create. A record's implicit parameterless constructor would otherwise be
    // public and let callers bypass Create entirely.
    internal LibraryPreparation()
    {
    }

    /// <summary>The panel this library was enriched for, referenced by id. Null when unresolved.</summary>
    public PanelId? PanelId { get; init; }

    /// <summary>Amount of material the preparation started from.</summary>
    public int? InputAmount { get; init; }

    public string? LibraryPrepKit { get; init; }

    /// <summary>Whether the preparation avoided a PCR amplification step.</summary>
    public bool? PcrFree { get; init; }

    public string? TargetEnrichmentKit { get; init; }

    /// <summary>Whether unique molecular identifiers were used to distinguish duplicate reads.</summary>
    public bool? UmiPresent { get; init; }

    /// <summary>Fragment length the preparation aimed for.</summary>
    public int? IntendedInsertSize { get; init; }

    /// <summary>Read length the preparation was designed for, as opposed to what was observed.</summary>
    public int? IntendedReadLength { get; init; }

    public static ErrorOr<LibraryPreparation> Create(
        PanelId? panelId = null,
        int? inputAmount = null,
        string? libraryPrepKit = null,
        bool? pcrFree = null,
        string? targetEnrichmentKit = null,
        bool? umiPresent = null,
        int? intendedInsertSize = null,
        int? intendedReadLength = null)
    {
        if (inputAmount is < 0)
        {
            return Error.Validation(
                "LibraryPreparation.InputAmount",
                $"input_amount must not be negative, got {inputAmount}");
        }

        if (intendedInsertSize is < 1)
        {
            return Error.Validation(
                "LibraryPreparation.IntendedInsertSize",
                $"intended_insert_size must be positive, got {intendedInsertSize}");
        }

        if (intendedReadLength is < 1)
        {
            return Error.Validation(
                "LibraryPreparation.IntendedReadLength",
                $"intended_read_length must be positive, got {intendedReadLength}");
        }

        return new LibraryPreparation
        {
            PanelId = panelId,
            InputAmount = inputAmount,
            LibraryPrepKit = Normalize.Collapse(libraryPrepKit),
            PcrFree = pcrFree,
            TargetEnrichmentKit = Normalize.Collapse(targetEnrichmentKit),
            UmiPresent = umiPresent,
            IntendedInsertSize = intendedInsertSize,
            IntendedReadLength = intendedReadLength,
        };
    }
}
