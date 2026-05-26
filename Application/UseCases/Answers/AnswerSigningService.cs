using System.Globalization;
using System.Text;
using MainProject.Application.Contracts;

namespace MainProject.Application.UseCases.Answers;

public sealed class AnswerSigningService : IAnswerSigningService
{
    private readonly AnswerDataService _answerDataService;

    public AnswerSigningService(AnswerDataService answerDataService)
    {
        _answerDataService = answerDataService;
    }

    public string GetSigningData(int surveyId, int organizationId)
    {
        var survey = _answerDataService.GetSurveyInfo(surveyId)
            ?? throw new InvalidOperationException("Анкета для подписи не найдена.");
        var answerRecord = _answerDataService.GetAnswerRecords(surveyId, organizationId).FirstOrDefault()
            ?? throw new InvalidOperationException("Ответы для подписи не найдены.");

        var builder = new StringBuilder();
        builder.AppendLine("АИС Анкетирование");
        builder.AppendLine($"Анкета: {survey.NameSurvey}");
        builder.AppendLine($"ID анкеты: {surveyId.ToString(CultureInfo.InvariantCulture)}");
        builder.AppendLine($"Организация: {answerRecord.OrganizationName ?? organizationId.ToString(CultureInfo.InvariantCulture)}");
        builder.AppendLine($"ID организации: {organizationId.ToString(CultureInfo.InvariantCulture)}");

        if (answerRecord.CompletionDate.HasValue)
        {
            builder.AppendLine($"Дата заполнения: {answerRecord.CompletionDate.Value:dd.MM.yyyy HH:mm:ss}");
        }

        if (!string.IsNullOrWhiteSpace(survey.Description))
        {
            builder.AppendLine($"Описание анкеты: {survey.Description}");
        }

        builder.AppendLine();
        builder.AppendLine("Ответы:");

        for (var index = 0; index < answerRecord.Answers.Count; index++)
        {
            var answer = answerRecord.Answers[index];
            builder.AppendLine($"{index + 1}. {answer.DisplayQuestion}");
            builder.AppendLine($"   Оценка: {answer.DisplayRating}");
            builder.AppendLine($"   Комментарий: {FormatComment(answer.Comment)}");
        }

        return builder.ToString().TrimEnd();
    }

    public bool SaveSignature(int surveyId, int organizationId, string signature)
    {
        return _answerDataService.UpdateSignature(surveyId, organizationId, signature);
    }

    private static string FormatComment(string? comment)
    {
        return string.IsNullOrWhiteSpace(comment) ? "Без комментария" : comment.Trim();
    }
}
