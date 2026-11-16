using System.Globalization;
using BiobankApi.Domain;
using BiobankApi.Domain.Patients;
using TrainingSeeder.Vocabulary;

namespace TrainingSeeder.Generation;

/// <summary>
/// Builds biobank patients through the domain factories. Every coded value is drawn from the real
/// counts; what never left the server - identifiers, dates, birth year and month - is generated in
/// the export's own formats.
/// </summary>
internal sealed class BiobankGenerator
{
    // The years samples are dated in. The export carries no dates, so these are a choice: the
    // archive's span, inside the 2010-2029 the sequencing API reads predictive numbers for.
    internal const int FirstYear = 2012;
    internal const int LastYear = 2025;

    // The most samples one surgery yields: two fresh/RNAlater pairs.
    private const int TissuePerSurgery = 4;

    // Share of blood draws taken at one of the patient's surgeries, sharing its biopsy and predictive
    // number. Not measurable from counts, so a fixed choice.
    private const double SerumAtSurgery = 0.5;

    // Share of a patient's later diagnoses that repeat one they already have. A fixed choice that
    // keeps each code's overall frequency: the repeated code was itself drawn from the counts.
    private const double RepeatDiagnosis = 0.5;

    // A surgery's tissue is split into the fresh sample and its RNAlater copy (see MaterialTypes):
    // the real counts of codes 1 and 53, and of 4 and 54, are within one of each other.
    private static readonly Dictionary<string, string> RnaLaterCopy = new()
    {
        ["1"] = "53",
        ["2"] = "55",
        ["3"] = "56",
        ["4"] = "54",
    };

    private readonly SeededRandom _random;
    private readonly UniqueNumbers _predictiveNumbers;
    private readonly UniqueNumbers _patientIds = new(100_000, 999_999);
    private readonly UniqueNumbers _biopsies = new(1, 9_999);
    private readonly UniqueNumbers _tissueEvents = new(1, 9_999);
    private readonly UniqueNumbers _serumEvents = new(1, 9_999);
    private readonly UniqueNumbers _genomeEvents = new(1, 9_999);
    private readonly UniqueNumbers _specimenNumbers = new(1, 99_999);
    private readonly List<PredictiveNumber> _issued = [];

    private readonly Weighted<bool?> _consent;
    private readonly Weighted<string?> _sex;
    private readonly Weighted<string?> _biobank;
    private readonly double _birthYearFilled;
    private readonly double _birthMonthFilled;
    private readonly double _accessionsFilled;

    private readonly Weighted<int> _tissueCount;
    private readonly Weighted<int> _serumCount;
    private readonly Weighted<int> _genomeCount;
    private readonly Weighted<int> _specimenCount;

    private readonly Weighted<string?> _tissueBase;
    private readonly Weighted<string?> _tissueDiagnosis;
    private readonly Weighted<string?> _morphology;
    private readonly Weighted<string?> _pTnm;
    private readonly Weighted<string?> _tissueRetrieved;
    private readonly double _tissuePredictive;
    private readonly double _tissueBiopsyWithoutPredictive;
    private readonly double _cutTimeFilled;
    private readonly double _freezeTimeFilled;
    private readonly Aliquots _tissueAliquots;

    private readonly Weighted<string?> _serumMaterial;
    private readonly Weighted<string?> _serumDiagnosis;
    private readonly Weighted<string?> _serumRetrieved;
    private readonly double _serumPredictive;
    private readonly double _serumBiopsy;
    private readonly double _serumTakingDateFilled;
    private readonly Aliquots _serumAliquots;

    private readonly Weighted<string?> _genomeMaterial;
    private readonly Weighted<string?> _genomeRetrieved;
    private readonly double _genomePredictive;
    private readonly double _genomeBiopsy;
    private readonly double _genomeTakingDateFilled;
    private readonly Aliquots _genomeAliquots;

    private readonly Weighted<string?> _specimenMaterial;
    private readonly Weighted<string?> _specimenDiagnosis;
    private readonly Weighted<string?> _specimenRetrieved;
    private readonly double _specimenTakingDateFilled;

    public BiobankGenerator(BiobankVocabulary vocabulary, SeededRandom random, UniqueNumbers predictiveNumbers)
    {
        _random = random;
        _predictiveNumbers = predictiveNumbers;
        var values = vocabulary.Values;
        var filled = vocabulary.Filled;

        _consent = values.Booleans("patient.consent");
        _sex = values.Strings("patient.sex");
        _biobank = values.Strings("patient.biobank");
        _birthYearFilled = filled.Field("patient.birth_year").Share;
        _birthMonthFilled = filled.Field("patient.birth_month").Share;

        // A patient without consent carries no clinical data at all (PatientAggregate.Create), so the
        // per-patient counts are re-based on the consented: their zeros are the real zeros less every
        // patient who withdrew.
        var withoutConsent = values.CountOf("patient.consent", false);
        long Consented(int k, long patients) => k == 0 ? Math.Max(0, patients - withoutConsent) : patients;
        var accessions = filled.Field("patient.accession_numbers");
        _accessionsFilled = Math.Min(1, (double)accessions.Filled / Math.Max(1, accessions.Total - withoutConsent));
        _tissueCount = vocabulary.PerPatient.Histogram("tissue_sample", Consented);
        _serumCount = vocabulary.PerPatient.Histogram("serum_sample", Consented);
        _genomeCount = vocabulary.PerPatient.Histogram("genome_sample", Consented);
        _specimenCount = vocabulary.PerPatient.Histogram("diagnostic_specimen", Consented);

        // The RNAlater copies are added with their fresh sample, so only the fresh codes are drawn.
        _tissueBase = values.Strings("tissue_sample.material_type")
            .Where(code => code is not null && !RnaLaterCopy.ContainsValue(code));
        _tissueDiagnosis = values.Strings("tissue_sample.diagnosis");
        _morphology = values.Strings("tissue_sample.morphology");
        _pTnm = values.Strings("tissue_sample.p_tnm");
        _tissueRetrieved = values.Strings("tissue_sample.retrieved");
        // A surgery with a predictive number always has a biopsy number; the rest of the biopsy
        // share falls on the surgeries without one.
        _tissuePredictive = filled.Field("tissue_sample.predictive_number").Share;
        var tissueBiopsy = filled.Field("tissue_sample.biopsy").Share;
        _tissueBiopsyWithoutPredictive = _tissuePredictive >= 1
            ? 0
            : Math.Clamp((tissueBiopsy - _tissuePredictive) / (1 - _tissuePredictive), 0, 1);
        _cutTimeFilled = filled.Field("tissue_sample.cut_time").Share;
        _freezeTimeFilled = filled.Field("tissue_sample.freeze_time").Share;
        _tissueAliquots = Aliquots.From(vocabulary.Ranges, "tissue_sample");

        _serumMaterial = values.Strings("serum_sample.material_type");
        _serumDiagnosis = values.Strings("serum_sample.diagnosis");
        _serumRetrieved = values.Strings("serum_sample.retrieved");
        _serumPredictive = filled.Field("serum_sample.predictive_number").Share;
        _serumBiopsy = filled.Field("serum_sample.biopsy").Share;
        _serumTakingDateFilled = filled.Field("serum_sample.taking_date").Share;
        _serumAliquots = Aliquots.From(vocabulary.Ranges, "serum_sample");

        _genomeMaterial = values.Strings("genome_sample.material_type");
        _genomeRetrieved = values.Strings("genome_sample.retrieved");
        _genomePredictive = filled.Field("genome_sample.predictive_number").Share;
        _genomeBiopsy = filled.Field("genome_sample.biopsy").Share;
        _genomeTakingDateFilled = filled.Field("genome_sample.taking_date").Share;
        _genomeAliquots = Aliquots.From(vocabulary.Ranges, "genome_sample");

        _specimenMaterial = values.Strings("diagnostic_specimen.material_type");
        _specimenDiagnosis = values.Strings("diagnostic_specimen.diagnosis");
        _specimenRetrieved = values.Strings("diagnostic_specimen.retrieved");
        _specimenTakingDateFilled = filled.Field("diagnostic_specimen.taking_date").Share;
    }

    public BiobankDataset Generate(int patientCount)
    {
        var patients = new List<PatientAggregate>(patientCount);
        for (var i = 0; i < patientCount; i++)
        {
            patients.Add(Patient());
        }

        return new BiobankDataset(patients, [.. _issued]);
    }

    private PatientAggregate Patient()
    {
        var id = Invariant($"{_patientIds.Next(_random)}");
        var consent = _consent.Draw(_random);
        var sex = Parse<Sex>(_sex.Draw(_random));
        int? birthYear = _random.Chance(_birthYearFilled) ? _random.Between(1930, 2004) : null;
        int? birthMonth = _random.Chance(_birthMonthFilled) ? _random.Between(1, 12) : null;
        var biobank = _biobank.Draw(_random);

        if (consent is false)
        {
            return Generated.Valid(PatientAggregate.Create(id, biobank, consent, sex, birthYear, birthMonth));
        }

        var samples = new List<Sample>();
        var surgeries = new List<Surgery>();
        var diagnoses = new List<string>();
        AddTissue(samples, surgeries, diagnoses, _tissueCount.Draw(_random));
        AddSerum(samples, surgeries, diagnoses, _serumCount.Draw(_random));
        AddGenome(samples, _genomeCount.Draw(_random));
        var specimens = Specimens(diagnoses, _specimenCount.Draw(_random));
        var accessions = _random.Chance(_accessionsFilled) ? Accessions() : [];

        return Generated.Valid(PatientAggregate.Create(
            id, biobank, consent, sex, birthYear, birthMonth, accessions, samples, specimens));
    }

    // One surgery is one event: its samples share the event number, year, biopsy and predictive
    // number, and the tumour's diagnosis, morphology and staging.
    private void AddTissue(List<Sample> samples, List<Surgery> surgeries, List<string> diagnoses, int count)
    {
        for (var remaining = count; remaining > 0; remaining -= TissuePerSurgery)
        {
            var year = Year();
            var predictive = _random.Chance(_tissuePredictive) ? NewPredictive(year) : (PredictiveNumber?)null;
            var biopsy = predictive is not null || _random.Chance(_tissueBiopsyWithoutPredictive) ? NewBiopsy(year) : null;
            var surgery = new Surgery(year, biopsy, predictive);
            surgeries.Add(surgery);

            var eventNumber = _tissueEvents.Next(_random, year);
            var diagnosis = Diagnosis(_tissueDiagnosis, diagnoses);
            var morphology = _morphology.Draw(_random);
            var pTnm = _pTnm.Draw(_random);
            var retrieved = Parse<Retrieved>(_tissueRetrieved.Draw(_random));
            var cut = _random.Moment(year);

            foreach (var material in TissueMaterials(Math.Min(remaining, TissuePerSurgery)))
            {
                var (samplesNo, available) = _tissueAliquots.Draw(_random);
                samples.Add(Generated.Valid(TissueSample.Create(
                    sampleId: Invariant($"BBM:{year}:{eventNumber}:{material}"),
                    materialType: material,
                    eventNumber: eventNumber,
                    collectionYear: year,
                    biopsy: biopsy,
                    predictiveNumber: predictive?.Biobank,
                    samplesNo: samplesNo,
                    availableSamplesNo: available,
                    diagnosis: diagnosis,
                    pTnm: pTnm,
                    morphology: morphology,
                    cutTime: _random.Chance(_cutTimeFilled) ? cut : null,
                    freezeTime: _random.Chance(_freezeTimeFilled) ? cut.AddMinutes(_random.Between(2, 30)) : null,
                    retrieved: retrieved)));
            }
        }
    }

    // Fresh codes drawn without repeats, each followed by its RNAlater copy while there is room.
    private List<string> TissueMaterials(int size)
    {
        var materials = new List<string>(size);
        while (materials.Count < size)
        {
            var remaining = _tissueBase.Where(code => !materials.Contains(code!));
            if (remaining.IsEmpty)
            {
                break;
            }

            var material = remaining.Draw(_random)!;
            materials.Add(material);
            if (materials.Count < size && RnaLaterCopy.TryGetValue(material, out var copy))
            {
                materials.Add(copy);
            }
        }

        return materials;
    }

    private void AddSerum(List<Sample> samples, List<Surgery> surgeries, List<string> diagnoses, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var surgery = surgeries.Count > 0 && _random.Chance(SerumAtSurgery) ? _random.Pick(surgeries) : null;
            var year = surgery?.Year ?? Year();
            var biopsy = _random.Chance(_serumBiopsy) ? surgery?.Biopsy ?? NewBiopsy(year) : null;
            PredictiveNumber? predictive = _random.Chance(_serumPredictive) ? surgery?.Predictive ?? NewPredictive(year) : null;
            var eventNumber = _serumEvents.Next(_random, year);
            var material = _serumMaterial.Draw(_random)!;
            var (samplesNo, available) = _serumAliquots.Draw(_random);
            samples.Add(Generated.Valid(SerumSample.Create(
                sampleId: Invariant($"BBMs:{year}:{eventNumber}:{material}"),
                materialType: material,
                eventNumber: eventNumber,
                collectionYear: year,
                biopsy: biopsy,
                predictiveNumber: predictive?.Biobank,
                samplesNo: samplesNo,
                availableSamplesNo: available,
                diagnosis: Diagnosis(_serumDiagnosis, diagnoses),
                takingDate: _random.Chance(_serumTakingDateFilled) ? _random.Moment(year) : null,
                retrieved: Parse<Retrieved>(_serumRetrieved.Draw(_random)))));
        }
    }

    private void AddGenome(List<Sample> samples, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var year = Year();
            var eventNumber = _genomeEvents.Next(_random, year);
            var material = _genomeMaterial.Draw(_random)!;
            var (samplesNo, available) = _genomeAliquots.Draw(_random);
            samples.Add(Generated.Valid(GenomeSample.Create(
                sampleId: Invariant($"BBMd:{year}:{eventNumber}:{material}"),
                materialType: material,
                eventNumber: eventNumber,
                collectionYear: year,
                biopsy: _random.Chance(_genomeBiopsy) ? NewBiopsy(year) : null,
                predictiveNumber: _random.Chance(_genomePredictive) ? NewPredictive(year).Biobank : null,
                samplesNo: samplesNo,
                availableSamplesNo: available,
                takingDate: _random.Chance(_genomeTakingDateFilled) ? _random.Moment(year) : null,
                retrieved: Parse<Retrieved>(_genomeRetrieved.Draw(_random)))));
        }
    }

    private List<DiagnosticSpecimen> Specimens(List<string> diagnoses, int count)
    {
        var specimens = new List<DiagnosticSpecimen>(count);
        for (var i = 0; i < count; i++)
        {
            var year = Year();
            var number = _specimenNumbers.Next(_random, year);
            specimens.Add(Generated.Valid(DiagnosticSpecimen.Create(
                sampleId: Invariant($"&:{year}:{number}"),
                specimenNumber: number,
                year: year,
                materialType: _specimenMaterial.Draw(_random),
                diagnosis: Diagnosis(_specimenDiagnosis, diagnoses),
                takingDate: _random.Chance(_specimenTakingDateFilled) ? _random.Moment(year) : null,
                retrieved: Parse<Retrieved>(_specimenRetrieved.Draw(_random)))));
        }

        return specimens;
    }

    // Radiology accession numbers, in the export's RDG + year + six digits form.
    private List<string> Accessions()
    {
        var count = _random.Chance(0.8) ? 1 : _random.Between(2, 3);
        return [.. Enumerable.Range(0, count).Select(_ => Invariant($"RDG{_random.Between(2004, LastYear)}{_random.Between(0, 999_999):D6}"))];
    }

    private string? Diagnosis(Weighted<string?> codes, List<string> seen)
    {
        var code = codes.Draw(_random);
        if (code is null)
        {
            return null;
        }

        if (seen.Count > 0 && _random.Chance(RepeatDiagnosis))
        {
            return _random.Pick(seen);
        }

        seen.Add(code);
        return code;
    }

    private PredictiveNumber NewPredictive(int year)
    {
        var number = new PredictiveNumber(year, _predictiveNumbers.Next(_random, year));
        _issued.Add(number);
        return number;
    }

    private string NewBiopsy(int year) => Invariant($"{year}/{_biopsies.Next(_random, year)}-1");

    private int Year() => _random.Between(FirstYear, LastYear);

    private static T? Parse<T>(string? value)
        where T : struct, Enum =>
        value is null ? null : Enum.Parse<T>(value, ignoreCase: true);

    private static string Invariant(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);

    private sealed record Surgery(int Year, string? Biopsy, PredictiveNumber? Predictive);

    /// <summary>How many aliquots a sample was split into, and how many are left.</summary>
    private sealed record Aliquots(NumberRange Total, NumberRange Available)
    {
        // Share of samples at the median count; the rest spread evenly over min to max.
        private const double AtMedian = 0.6;

        public static Aliquots From(IReadOnlyDictionary<string, NumberRange> ranges, string table) =>
            new(ranges.Field($"{table}.samples_no"), ranges.Field($"{table}.available_samples_no"));

        public (int? SamplesNo, int? AvailableSamplesNo) Draw(SeededRandom random)
        {
            int? total = random.Chance(Total.FilledShare) ? Count(Total, random) : null;
            int? available = random.Chance(Available.FilledShare) ? Count(Available, random) : null;
            return (total, available is { } left && total is { } all ? Math.Min(left, all) : available);
        }

        private static int Count(NumberRange range, SeededRandom random) =>
            range is { Min: { } min, Median: { } median, Max: { } max }
                ? random.Chance(AtMedian) ? (int)Math.Round(median) : random.Between((int)min, (int)max)
                : 1;
    }
}
