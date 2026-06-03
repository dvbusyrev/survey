using System.Text;
using MainProject.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MainProject.Application.UseCases.Answers;

public static class AnswerPdfDocumentBuilder
{
    private static readonly DateTime StablePdfTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static byte[] BuildPdfContent(Survey survey, IReadOnlyList<AnswerRecord> answers)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(style => style
                    .FontSize(12)
                    .FontFamily("Arial")
                    .Fallback(x => x.FontFamily("Times New Roman")));

                page.Header()
                    .AlignCenter()
                    .PaddingBottom(15)
                    .Text($"Анкета: {survey.NameSurvey}")
                    .Bold()
                    .FontSize(18);

                page.Content()
                    .Column(column =>
                    {
                        if (!string.IsNullOrWhiteSpace(survey.Description))
                        {
                            column.Item()
                                .Border(1)
                                .BorderColor(Colors.Grey.Medium)
                                .Padding(10)
                                .Text(survey.Description);
                        }

                        column.Item()
                            .PaddingTop(15)
                            .Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(3);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Text("Вопрос").Bold();
                                    header.Cell().AlignCenter().Text("Оценка").Bold();
                                    header.Cell().Text("Комментарий").Bold();
                                });

                                foreach (var answer in answers)
                                {
                                    foreach (var item in answer.Answers)
                                    {
                                        table.Cell()
                                            .BorderBottom(1)
                                            .Padding(5)
                                            .Text(item.DisplayQuestion);

                                        table.Cell()
                                            .BorderBottom(1)
                                            .AlignCenter()
                                            .Padding(5)
                                            .Text(item.DisplayRating);

                                        table.Cell()
                                            .BorderBottom(1)
                                            .Padding(5)
                                            .Text(item.Comment ?? "Нет комментария");
                                    }
                                }
                            });
                    });
            });
        })
        .WithMetadata(new DocumentMetadata
        {
            Title = survey.NameSurvey,
            Author = "АИС Анкетирование",
            Creator = "АИС Анкетирование",
            Producer = "QuestPDF",
            CreationDate = StablePdfTimestamp,
            ModifiedDate = StablePdfTimestamp
        });

        using var stream = new MemoryStream();
        document.GeneratePdf(stream);
        return stream.ToArray();
    }
}
