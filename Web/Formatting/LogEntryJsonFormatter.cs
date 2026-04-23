using Newtonsoft.Json;

namespace MainProject.Web.Formatting;

public static class LogEntryJsonFormatter
{
    public static string Format(object? extraData)
    {
        if (extraData is null)
        {
            return string.Empty;
        }

        return JsonConvert.SerializeObject(extraData, Newtonsoft.Json.Formatting.Indented);
    }
}
