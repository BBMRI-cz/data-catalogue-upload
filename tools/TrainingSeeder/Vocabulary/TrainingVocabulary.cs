using System.Text.Json;

namespace TrainingSeeder.Vocabulary;

/// <summary>The two exported vocabularies, read from the seeder's embedded copies.</summary>
internal sealed record TrainingVocabulary(BiobankVocabulary Biobank, SequencingVocabulary Sequencing)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        // A section the export no longer writes must fail here, not draw from nothing later.
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
    };

    public static TrainingVocabulary Load() => new(
        Read<BiobankVocabulary>("biobank-vocabulary.json"),
        Read<SequencingVocabulary>("sequencing-vocabulary.json"));

    private static T Read<T>(string name)
    {
        using var stream = typeof(TrainingVocabulary).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"{name} is not embedded in the seeder.");
        return JsonSerializer.Deserialize<T>(stream, Options)
            ?? throw new InvalidOperationException($"{name} is empty.");
    }
}
