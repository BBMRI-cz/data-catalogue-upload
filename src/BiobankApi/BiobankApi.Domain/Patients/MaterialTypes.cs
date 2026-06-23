using System.Collections.Frozen;

namespace BiobankApi.Domain.Patients;

/// <summary>
/// Material-type code lookups for biobank samples. A code's meaning depends on the sample type
/// (tissue / serum / genome / diagnostic specimen). Codes are intentionally not hard-validated by
/// the models (new codes may appear); these maps are reference data plus the <see cref="Label"/>
/// convenience lookup.
/// </summary>
public static class MaterialTypes
{
    /// <summary>Discriminator for tissue samples.</summary>
    public const string Tissue = "tissue";

    /// <summary>Discriminator for serum samples.</summary>
    public const string Serum = "serum";

    /// <summary>Discriminator for genome samples.</summary>
    public const string Genome = "genome";

    /// <summary>Discriminator for diagnostic specimens.</summary>
    public const string DiagnosisMaterial = "diagnosis_material";

    // Tissue material codes (surgical / tumour tissue).
    public static readonly FrozenDictionary<string, string> TissueMaterialTypes =
        new Dictionary<string, string>
        {
            ["1"] = "Malignant tumour",
            ["2"] = "Metastasis",
            ["3"] = "Benign tumour",
            ["4"] = "Healthy/normal tissue",
            ["5"] = "Premalignant tissue",
            ["7"] = "Peripheral blood mononuclear cells (PBMNC)",
            ["53"] = "Malignant tumour (RNAlater)",
            ["54"] = "Healthy tissue (RNAlater)",
            ["55"] = "Metastasis (RNAlater)",
            ["56"] = "Benign tumour (RNAlater)",
        }.ToFrozenDictionary();

    // Serum material codes (blood-derived liquid samples).
    public static readonly FrozenDictionary<string, string> SerumMaterialTypes =
        new Dictionary<string, string>
        {
            ["K"] = "K3EDTA plasma, nitrogen-stored",
            ["SD"] = "Serum, nitrogen-stored",
            ["S"] = "Serum (room temperature / short-term)",
            ["PD"] = "Plasma, nitrogen-stored",
            ["L"] = "Li-heparin plasma, nitrogen-stored",
            ["T"] = "CTAD plasma, nitrogen-stored",
            ["C"] = "Plasma with DNA stabiliser",
            ["PR"] = "Primary cell cultures",
        }.ToFrozenDictionary();

    // Genome material codes (DNA / nucleic-acid samples).
    public static readonly FrozenDictionary<string, string> GenomeMaterialTypes =
        new Dictionary<string, string>
        {
            ["PK"] = "Whole blood (for DNA extraction)",
            ["PS"] = "Whole blood with DNA stabiliser",
            ["gD"] = "Extracted genomic DNA",
        }.ToFrozenDictionary();

    // Diagnostic-specimen material codes.
    public static readonly FrozenDictionary<string, string> DiagnosisMaterialTypes =
        new Dictionary<string, string> { ["S"] = "Serum / surgical specimen" }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, FrozenDictionary<string, string>> Lookups =
        new Dictionary<string, FrozenDictionary<string, string>>
        {
            [Tissue] = TissueMaterialTypes,
            [Serum] = SerumMaterialTypes,
            [Genome] = GenomeMaterialTypes,
            [DiagnosisMaterial] = DiagnosisMaterialTypes,
        }.ToFrozenDictionary();

    /// <summary>Returns the English label for a material-type code, or <c>null</c> if unknown.</summary>
    /// <param name="sampleType">One of <see cref="Tissue"/>, <see cref="Serum"/>,
    /// <see cref="Genome"/> or <see cref="DiagnosisMaterial"/>.</param>
    /// <param name="code">The material-type code.</param>
    public static string? Label(string sampleType, string? code)
    {
        if (code is null)
        {
            return null;
        }

        return Lookups.TryGetValue(sampleType, out var table) && table.TryGetValue(code, out var label)
            ? label
            : null;
    }
}
