using BiobankApi.Domain.Common;
using BiobankApi.Domain.Patients;
using ErrorOr;

namespace BiobankApi.Infrastructure.Xml;

/// <summary>
/// Folds one patient's successive export files into a single <see cref="PatientAggregate"/>. Each
/// weekly file is a snapshot of the patient header (consent, sex, birth date, patient accession
/// numbers), but its <c>&lt;LTS&gt;</c> and <c>&lt;STS&gt;</c> list only the records of roughly the
/// last 60 days: a sample ages out of the window without being removed from the biobank. The newest
/// file alone therefore loses everything older, so the header comes from the newest file while
/// samples and specimens accumulate across all of them.
/// </summary>
internal sealed class PatientExportHistory
{
    // Keyed on sample type + id, and replaced as a group: one sampleId expands into a row per
    // biopsy × predictive number, and biopsy is often filled in later (`-` → `YYYY/N-k`), so a
    // whole-row union would keep the stale biopsy-less row next to its successor.
    private readonly OrderedDictionary<(Type Type, SampleId Id), IReadOnlyList<Sample>> _samples = [];
    private readonly OrderedDictionary<SpecimenId, DiagnosticSpecimen> _specimens = [];
    private PatientAggregate _newest;

    public PatientExportHistory(PatientAggregate first)
    {
        _newest = first;
        Collect(first);
    }

    /// <summary>Fold in the next file for this patient; files must arrive oldest first.</summary>
    public void Add(PatientAggregate newer)
    {
        _newest = newer;
        Collect(newer);
    }

    /// <summary>
    /// The merged patient: the newest header, and every sample and specimen ever listed, each in its
    /// newest version. A patient whose newest file withdraws consent keeps none of them.
    /// </summary>
    public ErrorOr<PatientAggregate> Build()
    {
        var withdrawn = _newest.Consent is false;
        return PatientAggregate.Create(
            _newest.Id.Value,
            _newest.Biobank,
            _newest.Consent,
            _newest.Sex,
            _newest.BirthYear,
            _newest.BirthMonth,
            withdrawn ? [] : _newest.AccessionNumbers,
            withdrawn ? [] : [.. _samples.Values.SelectMany(rows => rows)],
            withdrawn ? [] : [.. _specimens.Values]);
    }

    private void Collect(PatientAggregate patient)
    {
        foreach (var rows in patient.Samples.GroupBy(sample => (sample.GetType(), sample.Id)))
        {
            _samples[rows.Key] = [.. rows];
        }

        foreach (var specimen in patient.DiagnosticSpecimens)
        {
            _specimens[specimen.Id] = specimen;
        }
    }
}
