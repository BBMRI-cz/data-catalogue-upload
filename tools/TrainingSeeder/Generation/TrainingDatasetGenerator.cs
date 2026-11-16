using TrainingSeeder.Vocabulary;

namespace TrainingSeeder.Generation;

/// <summary>Builds the whole training dataset, the same one for the same settings every time.</summary>
internal static class TrainingDatasetGenerator
{
    public static TrainingDataset Generate(TrainingVocabulary vocabulary, SeedSettings settings)
    {
        var random = new SeededRandom(settings.Seed);

        // Shared, so a sequencing case the biobank does not know never takes a number a patient has.
        var predictiveNumbers = new UniqueNumbers(1, 9_999);

        var patients = settings.Patients ?? vocabulary.Biobank.Patients;
        var biobank = new BiobankGenerator(vocabulary.Biobank, random, predictiveNumbers).Generate(patients);

        var scale = (double)patients / vocabulary.Biobank.Patients;
        var sequencing = new SequencingGenerator(vocabulary.Sequencing, random, predictiveNumbers)
            .Generate(scale, biobank.PredictiveNumbers);

        return new TrainingDataset(biobank.Patients, sequencing.Panels, sequencing.Runs, sequencing.Samples);
    }
}
