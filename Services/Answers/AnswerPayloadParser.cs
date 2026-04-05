using Newtonsoft.Json;

namespace main_project.Services.Answers;

public static class AnswerPayloadParser
{
    public static IReadOnlyList<AnswerPayloadItem> Parse(string? jsonAnswers)
    {
        if (string.IsNullOrWhiteSpace(jsonAnswers))
        {
            return Array.Empty<AnswerPayloadItem>();
        }

        try
        {
            var items = JsonConvert.DeserializeObject<List<AnswerPayloadItem>>(jsonAnswers);
            if (items != null && items.Count > 0)
            {
                return items;
            }

            var wrapper = JsonConvert.DeserializeObject<AnswerPayloadWrapper>(jsonAnswers);
            IReadOnlyList<AnswerPayloadItem>? wrappedAnswers = wrapper?.Answers;
            return wrappedAnswers ?? Array.Empty<AnswerPayloadItem>();
        }
        catch
        {
            return Array.Empty<AnswerPayloadItem>();
        }
    }

    private sealed class AnswerPayloadWrapper
    {
        [JsonProperty("answers")]
        public List<AnswerPayloadItem>? Answers { get; set; }
    }
}
