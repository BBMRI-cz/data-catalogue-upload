using BiobankApi.Domain.Patients;
using SequencingApi.Domain.Panels;
using SequencingApi.Domain.Runs;
using SequencingApi.Domain.Samples;

namespace TrainingSeeder.Generation;

/// <summary>Everything the seeder writes: one database's patients, the other's panels, runs and samples.</summary>
internal sealed record TrainingDataset(
    IReadOnlyList<PatientAggregate> Patients,
    IReadOnlyList<PanelAggregate> Panels,
    IReadOnlyList<SequencingRunAggregate> Runs,
    IReadOnlyList<SampleAggregate> Samples);

/// <param name="PredictiveNumbers">Every predictive number the patients' samples carry, in the order issued.</param>
internal sealed record BiobankDataset(
    IReadOnlyList<PatientAggregate> Patients,
    IReadOnlyList<PredictiveNumber> PredictiveNumbers);

internal sealed record SequencingDataset(
    IReadOnlyList<PanelAggregate> Panels,
    IReadOnlyList<SequencingRunAggregate> Runs,
    IReadOnlyList<SampleAggregate> Samples);
