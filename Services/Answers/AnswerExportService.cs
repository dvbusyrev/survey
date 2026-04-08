using System.IO.Compression;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using main_project.Models;
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
namespace main_project.Services.Answers;

public sealed class AnswerExportService
{
    private readonly AnswerDataService _answerDataService;
    private readonly ILogger<AnswerExportService> _logger;
    private readonly string _templatePath;

    public AnswerExportService(
        AnswerDataService answerDataService,
        IWebHostEnvironment environment,
        ILogger<AnswerExportService> logger)
    {
        _answerDataService = answerDataService;
        _logger = logger;
        _templatePath = Path.Combine(environment.ContentRootPath, "wwwroot", "docx", "shablon_docx.docx");
    }

    public AnswerGeneratedFileResult? CreatePdfReport(int surveyId, int organizationId)
    {
        var survey = _answerDataService.GetSurveyInfo(surveyId);
        var answers = _answerDataService.GetHistoryAnswers(surveyId, organizationId).ToList();
        if (survey == null || answers.Count == 0)
        {
            return null;
        }

        var pdfBytes = GeneratePdfContent(survey, answers);
        return new AnswerGeneratedFileResult
        {
            Content = pdfBytes,
            ContentType = "application/pdf",
            FileName = $"{CleanFileName(survey.name_survey ?? "Анкета")}_ответы_{DateTime.Now:yyyyMMdd}.pdf"
        };
    }

    public AnswerGeneratedFileResult? CreateSignedArchive(int surveyId, int organizationId)
    {
        var survey = _answerDataService.GetSurveyInfo(surveyId);
        var answers = _answerDataService.GetHistoryAnswers(surveyId, organizationId).ToList();
        if (survey == null || answers.Count == 0)
        {
            return null;
        }

        var pdfBytes = GeneratePdfContent(survey, answers);
        var cleanName = CleanFileName(survey.name_survey ?? "Анкета");
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var pdfFileName = $"{cleanName}_ответы_{timestamp}.pdf";
        var zipFileName = $"{cleanName}_с_подписью_{timestamp}.zip";
        var signature = answers.FirstOrDefault()?.csp;

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

        if (!File.Exists(_templatePath))
        {
            throw new FileNotFoundException("Шаблон DOCX не найден", _templatePath);
        }

        var criteriaList = questions.Select(question => question.Text).ToList();
        var rows = _answerDataService.GetHistoryAnswers(surveyId, organizationId).ToList();
        if (rows.Count == 0)
        {
            return null;
        }

        var organizations = new List<string>();
        var ratings = new List<List<int>>();
        var comments = new List<List<string>>();

        foreach (var row in rows)
        {
            organizations.Add(row.organization_name ?? string.Empty);

            if (row.Answers.Count == 0)
            {
                ratings.Add(Enumerable.Repeat(0, criteriaList.Count).ToList());
                comments.Add(Enumerable.Repeat(string.Empty, criteriaList.Count).ToList());
                continue;
            }

            ratings.Add(row.Answers.Select(item => item.Rating ?? 0).ToList());
            comments.Add(row.Answers.Select(item => item.Comment ?? string.Empty).ToList());
        }

        var averages = new List<double>();
        for (int col = 0; col < criteriaList.Count; col++)
        {
            double sum = 0;
            int count = 0;
            for (int row = 0; row < ratings.Count; row++)
            {
                if (ratings[row].Count > col)
                {
                    sum += ratings[row][col];
                    count++;
                }
            }

            averages.Add(count > 0 ? sum / count : 0);
        }

        var now = DateTime.Now;
        var monthYear = now.ToString("MMMM yyyy", new System.Globalization.CultureInfo("ru-RU"));
        monthYear = char.ToUpper(monthYear[0]) + monthYear.Substring(1);

        var safeSurveyName = CleanFileName(survey.name_survey ?? "Анкета");
        var fileName = $"Отчет по анкете {safeSurveyName}.docx";
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_{fileName}");

        var data = new Dictionary<string, string>
        {
            { "##NAME_SURVEY##", survey.name_survey ?? string.Empty },
            { "##COUNT_CRITERIES##", criteriaList.Count.ToString() },
            { "##MASS_CRITERIES_LIST##", string.Join(Environment.NewLine, criteriaList.Select((criterion, index) => $" - {criterion}; ({index + 1})")) },
            { "##MASS_NAMES_CRITERIES_FOR_COMMENTS##", string.Join("     ", criteriaList) },
            { "##MASS_CRITERIES_FOR_TABLE##", string.Join("     ", criteriaList) },
            { "##DATE##", monthYear },
            { "##MASS_OrganizationS##", string.Join("\n", organizations) },
            { "##MASS_RATINGS##", string.Join("\n", ratings.Select(row => string.Join("     ", row))) },
            { "##MASS_COMMENTS##", string.Join("\n", comments.Select(row => string.Join("     ", row))) },
            { "##SREDNEE##", string.Join("     ", averages.Select(value => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture))) }
        };

        GenerateDocxFromTemplate(_templatePath, tempPath, data);

        try
        {
            var archiveOutput = string.Equals(type, "archiv", StringComparison.OrdinalIgnoreCase);
            if (archiveOutput)
            {
                using var zipStream = new MemoryStream();
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
                {
                    var entry = archive.CreateEntry(fileName, CompressionLevel.Optimal);
                    using var entryStream = entry.Open();
                    using var fileStream = File.OpenRead(tempPath);
                    fileStream.CopyTo(entryStream);
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
                Content = File.ReadAllBytes(tempPath),
                ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                FileName = fileName
            };
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
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

    private static byte[] GeneratePdfContent(Survey survey, IReadOnlyList<HistoryAnswer> answers)
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
                    .Text($"Анкета: {survey.name_survey}")
                    .Bold()
                    .FontSize(18);

                page.Content()
                    .Column(column =>
                    {
                        if (!string.IsNullOrWhiteSpace(survey.description))
                        {
                            column.Item()
                                .Border(1)
                                .BorderColor(Colors.Grey.Medium)
                                .Padding(10)
                                .Text(survey.description);
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

    private static void GenerateDocxFromTemplate(string templatePath, string outputPath, IReadOnlyDictionary<string, string> data)
    {
        File.Copy(templatePath, outputPath, true);
        using var document = WordprocessingDocument.Open(outputPath, true);
        var body = document.MainDocumentPart?.Document.Body;
        if (body == null)
        {
            throw new InvalidOperationException("DOCX шаблон не содержит body");
        }

        ReplacePlaceholdersInBody(body, data);
        ReplacePlaceholdersInTable(body, data);
        document.MainDocumentPart?.Document.Save();
    }

    private static void ReplacePlaceholdersInBody(Body body, IReadOnlyDictionary<string, string> data)
    {
        foreach (var text in body.Descendants<Text>())
        {
            foreach (var key in data.Keys)
            {
                if (!text.Text.Contains(key) || (key != "##NAME_SURVEY##" && key != "##COUNT_CRITERIES##" && key != "##MASS_CRITERIES_LIST##"))
                {
                    continue;
                }

                if (key == "##MASS_CRITERIES_LIST##")
                {
                    var values = data[key].Split(new[] { "\n" }, StringSplitOptions.None);
                    foreach (var value in values)
                    {
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            text.Parent?.Append(new Run(new Text(value)));
                            text.Parent?.Append(new Break());
                        }
                    }

                    text.Text = string.Empty;
                }
                else
                {
                    text.Text = text.Text.Replace(key, data[key]);
                }
            }
        }
    }

    private static void ReplacePlaceholdersInTable(Body body, IReadOnlyDictionary<string, string> data)
    {
        var tables = body.Descendants<Table>();
        foreach (var table in tables)
        {
            foreach (var text in table.Descendants<Text>())
            {
                foreach (var key in data.Keys)
                {
                    if (!text.Text.Contains(key))
                    {
                        continue;
                    }

                    if (key == "##MASS_CRITERIES_FOR_TABLE##")
                    {
                        var cell = text.Ancestors<TableCell>().FirstOrDefault();
                        if (cell == null)
                        {
                            continue;
                        }

                        var values = data[key].Split(new[] { "     " }, StringSplitOptions.None);
                        text.Text = values[0];

                        for (var valueIndex = values.Length - 1; valueIndex >= 1; valueIndex--)
                        {
                            var newCell = new TableCell(
                                new Paragraph(
                                    new Run(
                                        new Text(values[valueIndex]))));
                            cell.InsertAfterSelf(newCell);
                        }
                    }

                    if (key == "##DATE##")
                    {
                        var cell = text.Ancestors<TableCell>().FirstOrDefault();
                        var row = cell?.Ancestors<TableRow>().FirstOrDefault();
                        if (cell == null || row == null)
                        {
                            continue;
                        }

                        var criteria = data["##MASS_CRITERIES_FOR_TABLE##"].Split(new[] { "     " }, StringSplitOptions.None);
                        var comments = data["##MASS_NAMES_CRITERIES_FOR_COMMENTS##"].Split(new[] { "     " }, StringSplitOptions.None);
                        text.Text = data[key];
                        cell.TableCellProperties = new TableCellProperties(
                            new GridSpan() { Val = (criteria.Length + comments.Length) * 2 });

                        foreach (var extraCell in row.Elements<TableCell>().Where(existingCell => existingCell != cell).ToList())
                        {
                            extraCell.Remove();
                        }
                    }

                    if (key == "##SREDNEE##")
                    {
                        var cell = text.Ancestors<TableCell>().FirstOrDefault();
                        var row = cell?.Ancestors<TableRow>().FirstOrDefault();
                        if (cell == null || row == null)
                        {
                            continue;
                        }

                        var criteria = data["##MASS_CRITERIES_FOR_TABLE##"].Split(new[] { "     " }, StringSplitOptions.None);
                        var values = data[key].Split(new[] { "     " }, StringSplitOptions.None);
                        text.Text = values[0];

                        for (var valueIndex = 1; valueIndex < criteria.Length + 1; valueIndex++)
                        {
                            var newCell = new TableCell(
                                new Paragraph(
                                    new Run(
                                        new Text(valueIndex < values.Length ? values[valueIndex] : string.Empty))));
                            row.Append(newCell);
                        }

                        for (var emptyIndex = 0; emptyIndex < criteria.Length - 1; emptyIndex++)
                        {
                            row.Append(new TableCell(
                                new Paragraph(
                                    new Run(
                                        new Text(string.Empty)))));
                        }

                        var cells = row.Elements<TableCell>().ToList();
                        if (cells.Count > 2)
                        {
                            cells[2].Remove();
                        }
                    }

                    if (key == "##MASS_OrganizationS##")
                    {
                        var cell = text.Ancestors<TableCell>().FirstOrDefault();
                        var row = cell?.Ancestors<TableRow>().FirstOrDefault();
                        if (cell == null || row == null)
                        {
                            continue;
                        }

                        var organizations = data[key].Split(new[] { "\n" }, StringSplitOptions.None);
                        var ratings = data["##MASS_RATINGS##"].Split(new[] { "\n" }, StringSplitOptions.None);
                        var comments = data["##MASS_COMMENTS##"].Split(new[] { "\n" }, StringSplitOptions.None);

                        text.Text = organizations[0];
                        var baseCells = row.Elements<TableCell>().ToList();
                        if (baseCells.Count > 1)
                        {
                            baseCells[0].Remove();
                            baseCells[1].Remove();
                        }

                        foreach (var rating in ratings[0].Split(new[] { "     " }, StringSplitOptions.None))
                        {
                            row.Append(new TableCell(new Paragraph(new Run(new Text(rating)))));
                        }

                        foreach (var comment in comments[0].Split(new[] { "     " }, StringSplitOptions.None))
                        {
                            row.Append(new TableCell(new Paragraph(new Run(new Text(comment)))));
                        }

                        var currentRow = row;
                        for (var organizationIndex = 1; organizationIndex < organizations.Length; organizationIndex++)
                        {
                            var newRow = new TableRow();
                            newRow.Append(new TableCell(new Paragraph(new Run(new Text(organizations[organizationIndex])))));

                            foreach (var rating in ratings[organizationIndex].Split(new[] { "     " }, StringSplitOptions.None))
                            {
                                newRow.Append(new TableCell(new Paragraph(new Run(new Text(rating)))));
                            }

                            foreach (var comment in comments[organizationIndex].Split(new[] { "     " }, StringSplitOptions.None))
                            {
                                newRow.Append(new TableCell(new Paragraph(new Run(new Text(comment)))));
                            }

                            currentRow.InsertAfterSelf(newRow);
                            currentRow = newRow;
                        }
                    }

                    if (key == "##MASS_NAMES_CRITERIES_FOR_COMMENTS##")
                    {
                        var cell = text.Ancestors<TableCell>().FirstOrDefault();
                        if (cell == null)
                        {
                            continue;
                        }

                        var criteria = data[key].Split(new[] { "     " }, StringSplitOptions.None);
                        text.Text = "Комментарии: " + criteria[0];

                        for (var criteriaIndex = criteria.Length - 1; criteriaIndex >= 1; criteriaIndex--)
                        {
                            var newCell = new TableCell(
                                new Paragraph(
                                    new Run(
                                        new Text(criteria[criteriaIndex]))));
                            cell.InsertAfterSelf(newCell);
                        }
                    }
                }
            }
        }
    }
}
