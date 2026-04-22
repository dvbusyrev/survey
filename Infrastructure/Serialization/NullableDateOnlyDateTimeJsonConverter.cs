using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MainProject.Infrastructure.Serialization;

public sealed class NullableDateOnlyDateTimeJsonConverter : JsonConverter<DateTime?>
{
    private const string DateFormat = "yyyy-MM-dd";

    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        var rawValue = reader.GetString();
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        if (DateTime.TryParseExact(
                rawValue,
                DateFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsedDate))
        {
            return parsedDate.Date;
        }

        if (DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out parsedDate))
        {
            return parsedDate.Date;
        }

        throw new JsonException($"Некорректный формат даты: {rawValue}");
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (!value.HasValue)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Value.ToString(DateFormat, CultureInfo.InvariantCulture));
    }
}
