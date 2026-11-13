using System.Text.Json;
using System.Text.Json.Serialization;

namespace RefundPilot.Application.Validation;

/// <summary>Canonicalizes single-line text while the request is bound, so validation and storage see the same value.</summary>
public sealed class CanonicalTextConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString() is { } s ? TextRules.Canonicalize(s) : null;

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}

/// <summary>Like <see cref="CanonicalTextConverter"/> but keeps line breaks (notes).</summary>
public sealed class CanonicalMultilineTextConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString() is { } s ? TextRules.Canonicalize(s, multiline: true) : null;

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
