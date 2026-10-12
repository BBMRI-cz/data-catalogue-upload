using Uploader.Application.Mapping;
using Xunit;

namespace Uploader.UnitTests;

/// <summary>
/// Codes turned into the catalogue's ontology terms. A term is a foreign key in EMX2, so it has to
/// match character for character - the expected strings spell their non-ASCII characters as escapes,
/// which is what makes a hyphen typed in place of an en dash fail here.
/// </summary>
public sealed class CatalogueVocabularyTests
{
    private const string Cryotube = "Cryotube 1–2mL Programmable freezing to <-135°C";
    private const string PpTube = "PP tube 0.5–2mL (-35) to (-18)°C";

    [Theory]
    [InlineData("tissue", "1", Cryotube)]
    [InlineData("tissue", "2", Cryotube)]
    [InlineData("tissue", "3", Cryotube)]
    [InlineData("tissue", "4", Cryotube)]
    [InlineData("tissue", "5", Cryotube)]
    [InlineData("tissue", "7", Cryotube)]
    [InlineData("serum", "C", Cryotube)]
    [InlineData("serum", "K", Cryotube)]
    [InlineData("serum", "L", Cryotube)]
    [InlineData("serum", "PD", Cryotube)]
    [InlineData("serum", "SD", Cryotube)]
    [InlineData("serum", "T", Cryotube)]
    [InlineData("serum", "PR", Cryotube)]
    [InlineData("genome", "gD", PpTube)]
    [InlineData("genome", "PK", PpTube)]
    [InlineData("tissue", "53", null)]   // RNAlater: the previous uploader named no tube
    [InlineData("tissue", "56", null)]
    [InlineData("serum", "S", null)]
    [InlineData("serum", "PS", null)]
    [InlineData("genome", "X", null)]
    [InlineData(null, "1", null)]
    public void StorageConditionsFollowThePreviousUploadersMapping(string? type, string? code, string? expected) =>
        Assert.Equal(expected, CatalogueVocabulary.StorageConditions(type, code));

    [Theory]
    [InlineData("NB552710", "Illumina NextSeq 550")]
    [InlineData("nb552710", "Illumina NextSeq 550")]   // the source upper-cases it, but a term must not hinge on that
    [InlineData("NS500123", "NextSeq 500")]
    [InlineData("NB501234", "NextSeq 500")]
    [InlineData(" M02340 ", "MiSeq")]
    [InlineData("A00123", null)]                        // a NovaSeq: no term chosen for it
    [InlineData("", null)]
    [InlineData(null, null)]
    public void InstrumentModelComesFromTheSerialPrefix(string? instrumentId, string? expected) =>
        Assert.Equal(expected, CatalogueVocabulary.SequencingInstrumentModel(instrumentId));
}
