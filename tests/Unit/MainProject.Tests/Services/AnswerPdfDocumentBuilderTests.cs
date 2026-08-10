using System.Text;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
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

    [Fact]
    public void BuildPdfContent_WithCyrillicText_PreservesReadableText()
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
                        Rating = 4,
                        Comment = "Есть замечания"
                    }
                }
            }
        };

        var pdf = AnswerPdfDocumentBuilder.BuildPdfContent(survey, answers);

        using var reader = new PdfReader(new MemoryStream(pdf));
        using var document = new PdfDocument(reader);
        var extractedText = string.Join(
            '\n',
            Enumerable.Range(1, document.GetNumberOfPages())
                .Select(pageNumber => PdfTextExtractor.GetTextFromPage(document.GetPage(pageNumber))));

        Assert.Contains("Анкета: Информационные ресурсы", extractedText);
        Assert.Contains("Вопрос", extractedText);
        Assert.Contains("Оценка", extractedText);
        Assert.Contains("Комментарий", extractedText);
        Assert.Contains("Первый вопрос", extractedText);
        Assert.Contains("Есть замечания", extractedText);
    }
}
