using Uploader.Application.Abstractions;
using Uploader.Application.Mapping;
using Xunit;

namespace Uploader.UnitTests;

/// <summary>
/// The stand-in for the mapping table: today's hand-written rules, behind the port the ontology-mapping
/// student's approved table will plug into. Every rule answers one of three ways - a term, "no term" for
/// a value deliberately left empty, or not found for a value nobody has decided on - and only the last
/// puts a value on the unmapped list. A term is a foreign key in EMX2, so the expected strings match it
/// character for character.
/// </summary>
public sealed class RuleMappingTableTests
{
    private static readonly IMappingSnapshot Rules = new RuleMappingSnapshot();

    private static MappingAnswer Lookup(OntologyField field, string value) =>
        Rules.Lookup(field, value, TermContext.Empty);

    [Theory]
    [InlineData("male", "assigned male at birth")]
    [InlineData("female", "assigned female at birth")]
    [InlineData("Female", "assigned female at birth")]   // a term must not hinge on the source's casing
    public void SexBecomesTheGssoAssignmentAtBirth(string sex, string term) =>
        Assert.Equal(MappingAnswer.Mapped(term), Lookup(OntologyFields.GenderAtBirth, sex));

    [Theory]
    [InlineData("unknown")]
    [InlineData("x")]
    public void ASexNoRuleKnowsIsNotFound(string sex) =>
        Assert.Equal(MappingAnswer.NotFound, Lookup(OntologyFields.GenderAtBirth, sex));

    [Theory]
    [InlineData("MOU", "Masaryk Memorial Cancer Institute")]
    [InlineData("MMCI", "Masaryk Memorial Cancer Institute")]
    [InlineData("mou", "Masaryk Memorial Cancer Institute")]
    [InlineData("BBM", "Bank of Biological Material, Masaryk Memorial Cancer Institute")]
    public void ABiobankCodeBecomesItsInstitute(string code, string term) =>
        Assert.Equal(MappingAnswer.Mapped(term), Lookup(OntologyFields.PrimaryAffiliatedInstitute, code));

    /// <summary>The managing biobank shares the patient's institute list, keyed on the same code.</summary>
    [Theory]
    [InlineData("MOU", "Masaryk Memorial Cancer Institute")]
    [InlineData("bbm", "Bank of Biological Material, Masaryk Memorial Cancer Institute")]
    public void ABiobankCodeBecomesTheManagingBiobank(string code, string term) =>
        Assert.Equal(MappingAnswer.Mapped(term), Lookup(OntologyFields.ManagingBiobank, code));

    /// <summary>Not guessed at, and no longer silently dropped: it lands on the unmapped list.</summary>
    [Theory]
    [InlineData("Personal", "primaryAffiliatedInstitute")]
    [InlineData("Biospecimens", "managingBiobank")]
    public void AnUnknownBiobankCodeIsNotFound(string table, string column) =>
        Assert.Equal(MappingAnswer.NotFound, Lookup(new OntologyField(table, column), "XYZ"));

    /// <summary>The biobank exports ICD-10 without the dot; the Diagnosis ontology spells it out.</summary>
    [Theory]
    [InlineData("C504", "C50.4")]      // four characters: the last one is the subcategory
    [InlineData(" C774 ", "C77.4")]    // trimmed first
    [InlineData(" C50 ", "C50")]       // trimmed, with no subcategory to separate
    public void DiagnosisGetsTheIcd10Dot(string code, string term) =>
        Assert.Equal(MappingAnswer.Mapped(term), Lookup(OntologyFields.Diagnosis, code));

    /// <summary>
    /// A code the rule would hand back unchanged has nothing translated: if it is a term the resolver
    /// already used it as one, and if it is not, saying otherwise would only hide it.
    /// </summary>
    [Theory]
    [InlineData("C50")]      // already the ontology's spelling
    [InlineData("C5091")]    // five characters: no rule says where the dot goes
    public void ADiagnosisTheDotRuleLeavesAloneIsNotFound(string code) =>
        Assert.Equal(MappingAnswer.NotFound, Lookup(OntologyFields.Diagnosis, code));

    private static MappingAnswer Material(OntologyField field, string sampleType, string code) =>
        Rules.Lookup(field, code, TermContext.Material(sampleType, label: null));

    /// <summary>
    /// Why the material code carries its sample type: one code says something different in each of
    /// its four columns, and the same code on another sample type is another material altogether.
    /// </summary>
    [Fact]
    public void OneCodeResolvesPerColumnAndPerSampleType()
    {
        Assert.Equal(MappingAnswer.Mapped("Solid Tissue Specimen"), Material(OntologyFields.MaterialType, "tissue", "53"));
        Assert.Equal(MappingAnswer.Mapped("Tumor"), Material(OntologyFields.PathologicalState, "tissue", "53"));
        Assert.Equal(MappingAnswer.Mapped("Frozen Tissue"), Material(OntologyFields.BiospecimenForm, "tissue", "53"));
        Assert.Equal(MappingAnswer.NoTerm, Material(OntologyFields.StorageConditions, "tissue", "53"));

        Assert.Equal(MappingAnswer.Mapped("Serum or Plasma"), Material(OntologyFields.BiospecimenForm, "serum", "PS"));
        Assert.Equal(MappingAnswer.Mapped("Whole Blood"), Material(OntologyFields.BiospecimenForm, "genome", "PS"));
    }

    /// <summary>
    /// What was taken from the patient. Tissue is excised and everything blood-derived is a blood
    /// draw - including the mononuclear cells, which are filed as tissue but were never cut out.
    /// </summary>
    [Theory]
    [InlineData("tissue", "1", "Solid Tissue Specimen")]
    [InlineData("tissue", "56", "Solid Tissue Specimen")]
    [InlineData("tissue", "7", "Peripheral Blood")]
    [InlineData("serum", "SD", "Peripheral Blood")]
    [InlineData("genome", "gD", "Peripheral Blood")]
    public void AMaterialCodeBecomesWhatWasCollected(string sampleType, string code, string term) =>
        Assert.Equal(MappingAnswer.Mapped(term), Material(OntologyFields.MaterialType, sampleType, code));

    /// <summary>A primary cell culture is grown, not collected, so it has no material type on purpose.</summary>
    [Fact]
    public void APrimaryCellCultureHasNoMaterialTypeOnPurpose() =>
        Assert.Equal(MappingAnswer.NoTerm, Material(OntologyFields.MaterialType, "serum", "PR"));

    [Fact]
    public void AMaterialCodeOfAnUnknownSampleTypeIsNotFound() =>
        Assert.Equal(MappingAnswer.NotFound, Material(OntologyFields.MaterialType, "plasma", "SD"));

    [Theory]
    [InlineData("1", "Tumor")]     // malignant tumour
    [InlineData("53", "Tumor")]    // malignant tumour, RNAlater
    [InlineData("2", "Tumor")]     // metastasis
    [InlineData("55", "Tumor")]
    [InlineData("4", "Normal")]    // healthy tissue
    [InlineData("54", "Normal")]
    public void ATissueCodeBecomesItsPathologicalState(string code, string term) =>
        Assert.Equal(MappingAnswer.Mapped(term), Material(OntologyFields.PathologicalState, "tissue", code));

    /// <summary>
    /// The ontology offers only Normal, Tumor, Organoid and Tumoroid. Calling benign or premalignant
    /// tissue "Tumor" would assert a malignancy the biobank did not record, and a blood draw records
    /// no pathological state at all - each a decision, so none of them is reported as unmapped.
    /// </summary>
    [Theory]
    [InlineData("tissue", "3")]    // benign tumour
    [InlineData("tissue", "56")]   // benign tumour, RNAlater
    [InlineData("tissue", "5")]    // premalignant
    [InlineData("tissue", "7")]    // mononuclear cells
    [InlineData("serum", "SD")]
    [InlineData("genome", "PK")]
    public void APathologicalStateTheOntologyCannotSayHonestlyIsNoTerm(string sampleType, string code) =>
        Assert.Equal(MappingAnswer.NoTerm, Material(OntologyFields.PathologicalState, sampleType, code));

    [Theory]
    [InlineData("tissue", "8")]    // a tissue code nobody has seen
    [InlineData("plasma", "1")]    // a sample type nobody has seen
    public void APathologicalStateNoRuleKnowsIsNotFound(string sampleType, string code) =>
        Assert.Equal(MappingAnswer.NotFound, Material(OntologyFields.PathologicalState, sampleType, code));

    /// <summary>
    /// The form the biobank stores. Tissue is frozen - the export carries a freeze time for every
    /// tissue sample - and the plasma and serum codes differ only in what the ontology does not tell
    /// apart.
    /// </summary>
    [Theory]
    [InlineData("tissue", "1", "Frozen Tissue")]
    [InlineData("tissue", "53", "Frozen Tissue")]
    [InlineData("serum", "SD", "Serum or Plasma")]
    [InlineData("serum", "K", "Serum or Plasma")]
    [InlineData("genome", "gD", "Blood DNA")]
    [InlineData("genome", "PK", "Whole Blood")]
    public void AMaterialCodeBecomesTheFormTheBiobankStores(string sampleType, string code, string term) =>
        Assert.Equal(MappingAnswer.Mapped(term), Material(OntologyFields.BiospecimenForm, sampleType, code));

    [Theory]
    [InlineData("tissue", "7")]    // mononuclear cells are not a listed form
    [InlineData("serum", "PR")]    // primary cell cultures
    public void AFormTheOntologyDoesNotListIsNoTerm(string sampleType, string code) =>
        Assert.Equal(MappingAnswer.NoTerm, Material(OntologyFields.BiospecimenForm, sampleType, code));

    [Fact]
    public void AFormOfAnUnknownSampleTypeIsNotFound() =>
        Assert.Equal(MappingAnswer.NotFound, Material(OntologyFields.BiospecimenForm, "plasma", "SD"));

    private const string Cryotube = "Cryotube 1–2mL Programmable freezing to <-135°C";
    private const string PpTube = "PP tube 0.5–2mL (-35) to (-18)°C";

    /// <summary>
    /// The ontology names a tube and a temperature together and the export names neither, so this is
    /// the previous uploader's mapping from the material code. The terms carry an en dash, not a
    /// hyphen.
    /// </summary>
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
    public void StorageConditionsFollowThePreviousUploadersMapping(string sampleType, string code, string term) =>
        Assert.Equal(MappingAnswer.Mapped(term), Material(OntologyFields.StorageConditions, sampleType, code));

    /// <summary>RNAlater tissue: the previous uploader deliberately named no tube for it.</summary>
    [Theory]
    [InlineData("53")]
    [InlineData("54")]
    [InlineData("55")]
    [InlineData("56")]
    public void RnaLaterStorageIsNoTerm(string code) =>
        Assert.Equal(MappingAnswer.NoTerm, Material(OntologyFields.StorageConditions, "tissue", code));

    /// <summary>
    /// Codes the previous uploader never named a tube for and nobody has decided on since - which
    /// is what the unmapped list is for.
    /// </summary>
    [Theory]
    [InlineData("serum", "S")]
    [InlineData("serum", "PS")]
    [InlineData("genome", "PS")]
    [InlineData("genome", "X")]
    [InlineData("tissue", "8")]
    [InlineData("plasma", "1")]
    public void StorageNobodyHasDecidedOnIsNotFound(string sampleType, string code) =>
        Assert.Equal(MappingAnswer.NotFound, Material(OntologyFields.StorageConditions, sampleType, code));

    /// <summary>The ontology suffixes every vendor; a term must not hinge on the source's casing.</summary>
    [Theory]
    [InlineData("Illumina", "Illumina platform")]
    [InlineData(" illumina ", "Illumina platform")]
    [InlineData("Ion Torrent", "Ion Torrent platform")]
    [InlineData("PacBio", "PacBio platform")]
    public void APlatformBecomesItsOntologyPlatform(string platform, string term) =>
        Assert.Equal(MappingAnswer.Mapped(term), Lookup(OntologyFields.SequencingPlatform, platform));

    [Fact]
    public void APlatformNoRuleKnowsIsNotFound() =>
        Assert.Equal(MappingAnswer.NotFound, Lookup(OntologyFields.SequencingPlatform, "Oxford Nanopore"));

    /// <summary>
    /// The model comes from the instrument's serial number. The source's own model is the folder the
    /// run was filed under (<c>NextSeq</c>), and the ontology has no bare NextSeq: it tells the 500
    /// from the 550, and so does the serial prefix.
    /// </summary>
    [Theory]
    [InlineData("NB552710", "Illumina NextSeq 550")]
    [InlineData("nb552710", "Illumina NextSeq 550")]   // the source upper-cases it, but a term must not hinge on that
    [InlineData("NS500123", "NextSeq 500")]
    [InlineData("NB501234", "NextSeq 500")]
    [InlineData(" M02340 ", "MiSeq")]
    public void AnInstrumentSerialBecomesItsModel(string serial, string term) =>
        Assert.Equal(MappingAnswer.Mapped(term), Lookup(OntologyFields.SequencingInstrumentModel, serial));

    /// <summary>A NovaSeq: no rule names a model for its serial, so it is reported rather than guessed.</summary>
    [Fact]
    public void ASerialNoRuleKnowsIsNotFound() =>
        Assert.Equal(MappingAnswer.NotFound, Lookup(OntologyFields.SequencingInstrumentModel, "A00123"));

    /// <summary>
    /// A file role names what the file is for; the ontology names what it is encoded as, so several
    /// roles collapse onto one format.
    /// </summary>
    [Theory]
    [InlineData("fastq", "FASTQ")]
    [InlineData("bam", "BAM")]
    [InlineData("bam_index", "BAI")]
    [InlineData("vcf", "VCF")]
    [InlineData("vcf_filtered", "VCF")]
    [InlineData("variant_report", "PDF")]
    [InlineData("coverage_report", "PDF")]
    [InlineData("summary_report", "PDF")]
    [InlineData("VCF_Filtered", "VCF")]   // the source writes them lower-case, but a term must not hinge on that
    public void AFileRoleBecomesTheFormatItIsStoredIn(string role, string term) =>
        Assert.Equal(MappingAnswer.Mapped(term), Lookup(OntologyFields.DataFormatsStored, role));

    /// <summary>
    /// <c>other</c> is the sequencing API's escape hatch for an artifact it does not recognise, so it
    /// says nothing about how the file is encoded: no format, on purpose.
    /// </summary>
    [Fact]
    public void TheOtherRoleHasNoFormatOnPurpose() =>
        Assert.Equal(MappingAnswer.NoTerm, Lookup(OntologyFields.DataFormatsStored, "other"));

    [Fact]
    public void AFileRoleNoRuleKnowsIsNotFound() =>
        Assert.Equal(MappingAnswer.NotFound, Lookup(OntologyFields.DataFormatsStored, "cram"));

    /// <summary>
    /// The ontology carries the bare builds, but the pipelines write aliases (<c>hg19</c>,
    /// <c>hg38</c>) that are not terms.
    /// </summary>
    [Theory]
    [InlineData("hg19", "GRCh37")]
    [InlineData("grch37", "GRCh37")]
    [InlineData(" HG38 ", "GRCh38")]
    [InlineData("GRCh38", "GRCh38")]
    public void AReferenceGenomeAliasBecomesItsBuild(string alias, string term) =>
        Assert.Equal(MappingAnswer.Mapped(term), Lookup(OntologyFields.ReferenceGenomeUsed, alias));

    [Fact]
    public void AReferenceGenomeNoRuleKnowsIsNotFound() =>
        Assert.Equal(MappingAnswer.NotFound, Lookup(OntologyFields.ReferenceGenomeUsed, "T2T-CHM13"));

    /// <summary>
    /// The libraries table stars a gene whose coding regions are all covered, and the sequencing API
    /// keeps the star in the symbol. Which list the gene goes to is the mapper's business; the star
    /// itself is not part of the HGNC symbol, so the table takes it off.
    /// </summary>
    [Theory]
    [InlineData("SamplePreparation", "fullySequencedGenes", "BRCA1*", "BRCA1")]
    [InlineData("SamplePreparation", "fullySequencedGenes", " ALK* ", "ALK")]
    [InlineData("SamplePreparation", "partiallySequencedGenes", "TP53*", "TP53")]
    public void AStarredGeneBecomesItsSymbol(string table, string column, string gene, string term) =>
        Assert.Equal(MappingAnswer.Mapped(term), Lookup(new OntologyField(table, column), gene));

    /// <summary>
    /// A symbol with no star has nothing for the rule to take off: if it is a term the resolver already
    /// used it as one, and if it is not, saying otherwise would only hide it.
    /// </summary>
    [Theory]
    [InlineData("SamplePreparation", "fullySequencedGenes")]
    [InlineData("SamplePreparation", "partiallySequencedGenes")]
    public void AGeneTheStarRuleLeavesAloneIsNotFound(string table, string column) =>
        Assert.Equal(MappingAnswer.NotFound, Lookup(new OntologyField(table, column), "NOTAGENE1"));

    [Fact]
    public void AStrayStarIsNotAGene() =>
        Assert.Equal(MappingAnswer.NoTerm, Lookup(OntologyFields.FullySequencedGenes, "*"));

    /// <summary>
    /// Kit names have no rule: there is no code list to translate them from, so a kit is used only
    /// when it is already a live term, and any other is reported.
    /// </summary>
    [Theory]
    [InlineData("SamplePreparation", "libraryPreparationKit", "KAPA HyperPlus Kit")]
    [InlineData("SamplePreparation", "libraryPreparationKit", "TruSight Oncology 500 Assay")]
    [InlineData("SamplePreparation", "targetEnrichmentKit", "KAPA HyperCap")]
    public void AKitIsNotFound(string table, string column, string kit) =>
        Assert.Equal(MappingAnswer.NotFound, Lookup(new OntologyField(table, column), kit));

    /// <summary>
    /// The sequencing method is a constant our code computes rather than a source value, so the table
    /// has nothing to say about it.
    /// </summary>
    [Fact]
    public void TheSequencingMethodIsNotFound() =>
        Assert.Equal(MappingAnswer.NotFound, Lookup(OntologyFields.SequencingMethod, "Illumina Sequencing"));

    /// <summary>Loading does nothing: every run is handed the rules' one snapshot.</summary>
    [Fact]
    public async Task LoadingHandsBackTheRules()
    {
        var loaded = await new RuleMappingTable().LoadAsync(CancellationToken.None);

        Assert.Equal(
            MappingAnswer.Mapped("C50.4"),
            loaded.Value.Lookup(OntologyFields.Diagnosis, "C504", TermContext.Empty));
    }
}
