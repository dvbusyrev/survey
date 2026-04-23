using MainProject.Web.Formatting;
using Newtonsoft.Json.Linq;

namespace MainProject.Tests.Web.Formatting;

public sealed class LogEntryJsonFormatterTests
{
    [Fact]
    public void Format_ReturnsEmptyString_WhenExtraDataIsNull()
    {
        var result = LogEntryJsonFormatter.Format(null);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Format_SerializesNewtonsoftJObjectToReadableJson()
    {
        var payload = new JObject
        {
            ["operation"] = "UPDATE",
            ["changed_fields"] = new JArray(
                new JObject
                {
                    ["field"] = "name_survey",
                    ["new_value"] = "Новая анкета"
                })
        };

        var result = LogEntryJsonFormatter.Format(payload);

        Assert.Contains("\"operation\": \"UPDATE\"", result);
        Assert.Contains("\"changed_fields\"", result);
        Assert.Contains("\"name_survey\"", result);
    }
}
