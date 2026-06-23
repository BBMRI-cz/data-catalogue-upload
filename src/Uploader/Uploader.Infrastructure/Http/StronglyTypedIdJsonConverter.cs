using System.Text.Json;
using System.Text.Json.Serialization;
using Uploader.Domain.Common;

namespace Uploader.Infrastructure.Http;

/// <summary>
/// Serializes strongly-typed identifiers as their plain string value in outgoing catalogue
/// payloads (so <c>PatientId</c> becomes <c>"P1"</c>, not <c>{ "value": "P1" }</c>).
/// </summary>
internal sealed class StronglyTypedIdJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeof(IStronglyTypedId).IsAssignableFrom(typeToConvert);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(
            typeof(StronglyTypedIdJsonConverter<>).MakeGenericType(typeToConvert))!;
}

internal sealed class StronglyTypedIdJsonConverter<T> : JsonConverter<T>
    where T : struct, IStronglyTypedId
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException("Strongly-typed identifiers are only written, never read, by the catalogue gateway.");

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
