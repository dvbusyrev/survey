using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MainProject.Infrastructure.Serialization;

public sealed class DateOnlyDateTimeJsonConverter : JsonConverter<DateTime>
{
    private const string DateFormat = "yyyy-MM-dd";

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var rawValue = reader.GetString();
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            throw new JsonException("Дата не может быть пустой.");
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

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(DateFormat, CultureInfo.InvariantCulture));
    }
}
