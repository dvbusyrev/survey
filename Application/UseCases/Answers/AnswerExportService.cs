using System.IO.Compression;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using Body = DocumentFormat.OpenXml.Wordprocessing.Body;
using GridSpan = DocumentFormat.OpenXml.Wordprocessing.GridSpan;
using Paragraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using Run = DocumentFormat.OpenXml.Wordprocessing.Run;
using Table = DocumentFormat.OpenXml.Wordprocessing.Table;
using TableCell = DocumentFormat.OpenXml.Wordprocessing.TableCell;
using TableCellProperties = DocumentFormat.OpenXml.Wordprocessing.TableCellProperties;
using TableRow = DocumentFormat.OpenXml.Wordprocessing.TableRow;
using Text = DocumentFormat.OpenXml.Wordprocessing.Text;
namespace MainProject.Application.UseCases.Answers;

public sealed class AnswerExportService : IAnswerExportService
{
    private readonly AnswerDataService _answerDataService;

    public AnswerExportService(AnswerDataService answerDataService)
    {
        _answerDataService = answerDataService;
    }

    public AnswerGeneratedFileResult? CreatePdfReport(int surveyId, int organizationId)
    {
        var survey = _answerDataService.GetSurveyInfo(surveyId);
        var answers = _answerDataService.GetAnswerRecords(surveyId, organizationId).ToList();
        if (survey == null || answers.Count == 0)
        {
            return null;
        }

        var pdfBytes = GeneratePdfContent(survey, answers);
        return new AnswerGeneratedFileResult
        {
            Content = pdfBytes,
            ContentType = "application/pdf",
            FileName = $"{CleanFileName(survey.NameSurvey ?? "Анкета")}_ответы_{DateTime.Now:yyyyMMdd}.pdf"
        };
    }

    public AnswerGeneratedFileResult? CreateSignedArchive(int surveyId, int organizationId)
    {
        var survey = _answerDataService.GetSurveyInfo(surveyId);
        var answers = _answerDataService.GetAnswerRecords(surveyId, organizationId).ToList();
        if (survey == null || answers.Count == 0)
        {
            return null;
        }

        var pdfBytes = GeneratePdfContent(survey, answers);
        var cleanName = CleanFileName(survey.NameSurvey ?? "Анкета");
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var pdfFileName = $"{cleanName}_ответы_{timestamp}.pdf";
        var zipFileName = $"{cleanName}_с_подписью_{timestamp}.zip";
        var signature = answers.FirstOrDefault()?.Csp;

        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            var pdfEntry = archive.CreateEntry(pdfFileName, CompressionLevel.Optimal);
            using (var entryStream = pdfEntry.Open())
            {
                entryStream.Write(pdfBytes, 0, pdfBytes.Length);
            }

            if (!string.IsNullOrWhiteSpace(signature))
            {
                var sigEntry = archive.CreateEntry($"Подпись_{pdfFileName}.sig");
                using var entryStream = sigEntry.Open();
                using var writer = new StreamWriter(entryStream, Encoding.UTF8);
                writer.Write(signature);
            }
        }

        return new AnswerGeneratedFileResult
        {
            Content = memoryStream.ToArray(),
            ContentType = "application/zip",
            FileName = zipFileName
        };
    }

    public AnswerGeneratedFileResult? CreateSurveyReport(int surveyId, int organizationId, string? type)
    {
        var survey = _answerDataService.GetSurveyInfo(surveyId);
        var questions = _answerDataService.GetSurveyQuestions(surveyId);
        if (survey == null || questions.Count == 0)
        {
            return null;
        }
        var rows = _answerDataService.GetAnswerRecords(surveyId, organizationId).ToList();
        if (rows.Count == 0)
        {
            return null;
        }

        var now = DateTime.Now;
        var safeSurveyName = CleanFileName(survey.NameSurvey ?? "Анкета");
        var fileName = $"Отчет по анкете {safeSurveyName}.docx";
        var docxBytes = GenerateDocxContent(survey, rows, questions, now);

        var archiveOutput = string.Equals(type, "archive", StringComparison.OrdinalIgnoreCase);
        if (archiveOutput)
        {
            using var zipStream = new MemoryStream();
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
            {
                var entry = archive.CreateEntry(fileName, CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                entryStream.Write(docxBytes, 0, docxBytes.Length);
            }

            return new AnswerGeneratedFileResult
            {
                Content = zipStream.ToArray(),
                ContentType = "application/zip",
                FileName = $"Отчет по анкете {safeSurveyName}.zip"
            };
        }

        return new AnswerGeneratedFileResult
        {
            Content = docxBytes,
            ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            FileName = fileName
        };
    }

    private static string CleanFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "Анкета";
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Concat(fileName.Split(invalidChars));
    }

    private static byte[] GeneratePdfContent(Survey survey, IReadOnlyList<AnswerRecord> answers)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        QuestPDF.Settings.License = LicenseType.Community;

        var document = QuestPDF.Fluent.Document.Create(container =>
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
        });

        using var stream = new MemoryStream();
        document.GeneratePdf(stream);
        return stream.ToArray();
    }

    private static byte[] GenerateDocxContent(
        Survey survey,
        IReadOnlyList<AnswerRecord> answers,
        IReadOnlyList<SurveyQuestionItem> questions,
        DateTime generatedAt)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
            var body = mainPart.Document.AppendChild(new Body());

            body.Append(
                CreateParagraph(
                    $"Отчет по анкете \"{survey.NameSurvey}\"",
                    bold: true,
                    fontSize: "28",
                    justification: JustificationValues.Center,
                    afterSpacing: "200"),
                CreateParagraph(
                    $"Сформирован: {generatedAt:dd.MM.yyyy HH:mm}",
                    fontSize: "20",
                    justification: JustificationValues.Center,
                    afterSpacing: "240"));

            if (!string.IsNullOrWhiteSpace(survey.Description))
            {
                body.Append(CreateParagraph($"Описание: {survey.Description}", fontSize: "22", afterSpacing: "200"));
            }

            if (questions.Count > 0)
            {
                body.Append(CreateParagraph("Критерии оценки:", bold: true, fontSize: "22", afterSpacing: "100"));
                foreach (var question in questions.OrderBy(item => item.Id))
                {
                    body.Append(CreateParagraph($"• {question.Text}", fontSize: "20", afterSpacing: "40"));
                }
            }

            body.Append(CreateParagraph("Ответы:", bold: true, fontSize: "22", afterSpacing: "120"));
            body.Append(CreateAnswerTable(answers));

            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    private static Paragraph CreateParagraph(
        string text,
        bool bold = false,
        string fontSize = "22",
        JustificationValues? justification = null,
        string afterSpacing = "120")
    {
        var runProperties = new RunProperties(new FontSize { Val = fontSize });
        if (bold)
        {
            runProperties.Append(new Bold());
        }

        return new Paragraph(
            new Run(runProperties, new Text(text) { Space = SpaceProcessingModeValues.Preserve }))
        {
            ParagraphProperties = new ParagraphProperties(
                new Justification { Val = justification ?? JustificationValues.Left },
                new SpacingBetweenLines { After = afterSpacing })
        };
    }

    private static Table CreateAnswerTable(IReadOnlyList<AnswerRecord> answers)
    {
        var table = new Table(
            new TableProperties(
                new TableBorders(
                    new TopBorder { Val = BorderValues.Single, Size = 4 },
                    new BottomBorder { Val = BorderValues.Single, Size = 4 },
                    new LeftBorder { Val = BorderValues.Single, Size = 4 },
                    new RightBorder { Val = BorderValues.Single, Size = 4 },
                    new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                    new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 }),
                new TableWidth { Width = "0", Type = TableWidthUnitValues.Auto },
                new TableLayout { Type = TableLayoutValues.Autofit }));

        table.Append(new TableRow(
            CreateTableCell("Организация", isHeader: true),
            CreateTableCell("Дата", isHeader: true),
            CreateTableCell("Вопрос", isHeader: true),
            CreateTableCell("Оценка", isHeader: true),
            CreateTableCell("Комментарий", isHeader: true)));

        foreach (var answer in answers)
        {
            var answerItems = answer.Answers.Count > 0
                ? answer.Answers
                : new List<AnswerPayloadItem> { new() { QuestionText = "Нет данных", Rating = null, Comment = string.Empty } };

            foreach (var item in answerItems)
            {
                table.Append(new TableRow(
                    CreateTableCell(string.IsNullOrWhiteSpace(answer.OrganizationName) ? "Не указано" : answer.OrganizationName),
                    CreateTableCell(answer.CompletionDate?.ToString("dd.MM.yyyy HH:mm") ?? "Не указана"),
                    CreateTableCell(item.DisplayQuestion),
                    CreateTableCell(item.Rating?.ToString() ?? "-"),
                    CreateTableCell(string.IsNullOrWhiteSpace(item.Comment) ? "Нет комментария" : item.Comment)));
            }
        }

        return table;
    }

    private static TableCell CreateTableCell(string text, bool isHeader = false)
    {
        var runProperties = new RunProperties(new FontSize { Val = "20" });
        if (isHeader)
        {
            runProperties.Append(new Bold());
        }

        return new TableCell(
            new TableCellProperties(
                new TableCellWidth { Type = TableWidthUnitValues.Auto }),
            new Paragraph(
                new Run(runProperties, new Text(text ?? string.Empty) { Space = SpaceProcessingModeValues.Preserve })));
    }
}
