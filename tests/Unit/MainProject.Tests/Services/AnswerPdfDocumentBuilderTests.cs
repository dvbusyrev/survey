using System.Text;
using MainProject.Application.UseCases.Answers;
using MainProject.Domain.Entities;

namespace MainProject.Tests.Services;

public sealed class AnswerPdfDocumentBuilderTests
{
    [Fact]
    public void BuildPdfContent_ForSameInput_ReturnsStableBytes()
    {
        var survey = new Survey
        {
            IdSurvey = 17,
            NameSurvey = "Информационные ресурсы",
            Description = "Тестовая анкета"
        };
        var answers = new[]
        {
            new AnswerRecord
            {
                IdAnswer = 1,
                IdSurvey = 17,
                OrganizationId = 2,
                OrganizationName = "Тестовая организация",
                Answers =
                {
                    new AnswerPayloadItem
                    {
                        QuestionText = "Первый вопрос",
                        Rating = 5,
                        Comment = "Все хорошо"
                    },
                    new AnswerPayloadItem
                    {
                        QuestionText = "Второй вопрос",
                        Rating = 4,
                        Comment = "Есть замечания"
                    }
                }
            }
        };

        var firstPdf = AnswerPdfDocumentBuilder.BuildPdfContent(survey, answers);
        var secondPdf = AnswerPdfDocumentBuilder.BuildPdfContent(survey, answers);

        Assert.NotEmpty(firstPdf);
        Assert.Equal(firstPdf, secondPdf);
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(firstPdf, 0, 4));
    }
}
