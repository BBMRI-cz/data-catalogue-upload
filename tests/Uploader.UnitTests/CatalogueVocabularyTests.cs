using Uploader.Application.Mapping;
using Xunit;

namespace Uploader.UnitTests;

/// <summary>
/// The sequencing API's values turned into the catalogue's ontology terms. A term is a foreign key in
/// EMX2, so it has to match character for character. The sample's material-code rules moved to the
/// mapping table's stand-in and are tested in <see cref="RuleMappingTableTests"/>.
/// </summary>
public sealed class CatalogueVocabularyTests
{
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
