using System.Globalization;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Application.UseCases.Answers;
using MainProject.Domain.Entities;

using Justification = DocumentFormat.OpenXml.Wordprocessing.Justification;
using Table = DocumentFormat.OpenXml.Wordprocessing.Table;
using TableCell = DocumentFormat.OpenXml.Wordprocessing.TableCell;
using TableProperties = DocumentFormat.OpenXml.Wordprocessing.TableProperties;
using TableRow = DocumentFormat.OpenXml.Wordprocessing.TableRow;
using TopBorder = DocumentFormat.OpenXml.Wordprocessing.TopBorder;
using BottomBorder = DocumentFormat.OpenXml.Wordprocessing.BottomBorder;
using LeftBorder = DocumentFormat.OpenXml.Wordprocessing.LeftBorder;
using RightBorder = DocumentFormat.OpenXml.Wordprocessing.RightBorder;
using InsideHorizontalBorder = DocumentFormat.OpenXml.Wordprocessing.InsideHorizontalBorder;
using InsideVerticalBorder = DocumentFormat.OpenXml.Wordprocessing.InsideVerticalBorder;
using Run = DocumentFormat.OpenXml.Wordprocessing.Run;
using Text = DocumentFormat.OpenXml.Wordprocessing.Text;
using RunProperties = DocumentFormat.OpenXml.Wordprocessing.RunProperties;
using ParagraphProperties = DocumentFormat.OpenXml.Wordprocessing.ParagraphProperties;

namespace MainProject.Application.UseCases.Surveys;

public sealed class SurveyReportService : ISurveyReportService
{
    private readonly ISurveyReportRepository _surveyReportRepository;
    private readonly IClock _clock;

    public SurveyReportService(
        ISurveyReportRepository surveyReportRepository,
        IClock clock)
    {
        _surveyReportRepository = surveyReportRepository;
        _clock = clock;
    }

    public Task<IReadOnlyList<int>> GetAvailableReportYearsAsync(CancellationToken cancellationToken = default)
        => _surveyReportRepository.GetAvailableYearsAsync(cancellationToken);

    public async Task<GeneratedFileResult> CreateSurveyMonthlyReportAsync(
        int surveyId,
        int organizationId,
        CancellationToken cancellationToken = default)
    {
        string surveyName = string.Empty;
        var criteriaList = new List<string>();
        var organizations = new List<string>();
        var ratings = new List<List<int>>();
        var comments = new List<List<string>>();

        surveyName = await _surveyReportRepository.GetSurveyNameAsync(surveyId, cancellationToken) ?? string.Empty;
        criteriaList = (await _surveyReportRepository.GetSurveyQuestionsAsync(surveyId, cancellationToken))
            .Select(question => question.Text)
            .ToList();

        var surveyAnswers = await _surveyReportRepository.GetSurveyAnswersAsync(
            surveyId,
            organizationId == 0 ? null : organizationId,
            cancellationToken);
        foreach (var answer in surveyAnswers)
        {
            organizations.Add(answer.OrganizationName ?? string.Empty);
            ratings.Add(answer.Answers.Select(item => item.Rating ?? 0).ToList());
            comments.Add(answer.Answers.Select(item => item.Comment ?? string.Empty).ToList());
        }

        var metrics = SurveyReportMetricsCalculator.Calculate(ratings, criteriaList.Count);

        string currentMonth = _clock.Now.ToString("MMMM yyyy").ToLower();
        string fileName = organizationId == 0
            ? $"Отчет по анкете {surveyName} ({currentMonth}).docx"
            : $"Отчет по анкете {surveyName} для {organizations.FirstOrDefault()} ({currentMonth}).docx";

        using var mem = new MemoryStream();
        using (var document = WordprocessingDocument.Create(mem, WordprocessingDocumentType.Document, true))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            var totalAvg = metrics.OverallAverage;

            body.AppendChild(new Paragraph(
                new Run(new Text($"Отчет по анкете \"{surveyName}\""))
                {
                    RunProperties = new RunProperties(
                        new Bold(),
                        new FontSize() { Val = "28" })
                })
            {
                ParagraphProperties = new ParagraphProperties(
                    new Justification() { Val = JustificationValues.Center },
                    new SpacingBetweenLines() { After = "200" })
            });

            body.AppendChild(new Paragraph(
                new Run(new Text($"за {currentMonth}"))
                {
                    RunProperties = new RunProperties(
                        new Italic(),
                        new FontSize() { Val = "22" })
                })
            {
                ParagraphProperties = new ParagraphProperties(
                    new Justification() { Val = JustificationValues.Center },
                    new SpacingBetweenLines() { After = "300" })
            });

            body.AppendChild(new Paragraph(
                new Run(new Text("Данный отчет содержит информацию об оценках удовлетворенности потребителей услуг, полученных в результате ежемесячного анкетирования."))
                {
                    RunProperties = new RunProperties(new FontSize() { Val = "20" })
                })
            {
                ParagraphProperties = new ParagraphProperties(new SpacingBetweenLines() { After = "200" })
            });

            body.AppendChild(new Paragraph(
                new Run(new Text("Критерии оценки:"))
                {
                    RunProperties = new RunProperties(
                        new Bold(),
                        new FontSize() { Val = "20" })
                })
            {
                ParagraphProperties = new ParagraphProperties(new SpacingBetweenLines() { After = "100" })
            });

            foreach (var criteria in criteriaList)
            {
                body.AppendChild(new Paragraph(
                    new Run(new Text($"• {criteria}"))
                    {
                        RunProperties = new RunProperties(new FontSize() { Val = "18" })
                    })
                {
                    ParagraphProperties = new ParagraphProperties(new SpacingBetweenLines() { After = "50" })
                });
            }

            var table = new Table();
            table.AppendChild(new TableProperties(
                new TableBorders(
                    new TopBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                    new BottomBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                    new LeftBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                    new RightBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                    new InsideHorizontalBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                    new InsideVerticalBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 }),
                new TableWidth() { Width = "100%", Type = TableWidthUnitValues.Auto },
                new TableLayout() { Type = TableLayoutValues.Fixed }));

            var headerRow = new TableRow();
            headerRow.Append(CreateTableCell("Наименование организации", true, true));
            foreach (var criteria in criteriaList)
            {
                headerRow.Append(CreateTableCell(criteria, true, true));
            }
            headerRow.Append(CreateTableCell("Средний балл", true, true));
            foreach (var criteria in criteriaList)
            {
                headerRow.Append(CreateTableCell($"Комментарий ({criteria})", true, true));
            }
            table.Append(headerRow);

            for (int i = 0; i < organizations.Count; i++)
            {
                var dataRow = new TableRow();
                dataRow.Append(CreateTableCell(organizations[i], false, false));

                for (int j = 0; j < criteriaList.Count; j++)
                {
                    string ratingValue = (ratings.Count > i && ratings[i].Count > j) ? ratings[i][j].ToString() : "-";
                    dataRow.Append(CreateTableCell(ratingValue, false, true));
                }

                var organizationAverage = metrics.OrganizationAverages.ElementAtOrDefault(i);
                string avgValue = organizationAverage.HasValue ? organizationAverage.Value.ToString("F1") : "-";
                dataRow.Append(CreateTableCell(avgValue, false, true));

                for (int j = 0; j < criteriaList.Count; j++)
                {
                    string comment = (comments.Count > i && comments[i].Count > j) ? comments[i][j] : "-";
                    dataRow.Append(CreateTableCell(comment, false, false));
                }

                table.Append(dataRow);
            }

            var totalRow = new TableRow();
            totalRow.Append(CreateTableCell("Итого:", true, false));
            for (int i = 0; i < criteriaList.Count; i++)
            {
                string avgValue = metrics.CriterionAverages[i].ToString("F1");
                totalRow.Append(CreateTableCell(avgValue, false, true));
            }

            totalRow.Append(CreateTableCell(totalAvg.ToString("F1"), false, true));
            for (int i = 0; i < criteriaList.Count; i++)
            {
                totalRow.Append(CreateTableCell(string.Empty, false, false));
            }
            table.Append(totalRow);

            body.AppendChild(table);

            body.AppendChild(new Paragraph(
                new Run(new Text($"Общая оценка удовлетворенности: {totalAvg:F1} из 5"))
                {
                    RunProperties = new RunProperties(
                        new Bold(),
                        new FontSize() { Val = "20" })
                })
            {
                ParagraphProperties = new ParagraphProperties(
                    new Justification() { Val = JustificationValues.Right },
                    new SpacingBetweenLines() { Before = "300", After = "200" })
            });

            body.AppendChild(new Paragraph(
                new Run(new Text("Отчет сформирован автоматически"))
                {
                    RunProperties = new RunProperties(
                        new Italic(),
                        new FontSize() { Val = "16" })
                })
            {
                ParagraphProperties = new ParagraphProperties(
                    new Justification() { Val = JustificationValues.Right })
            });
        }

        return new GeneratedFileResult
        {
            Content = mem.ToArray(),
            ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            FileName = fileName
        };
    }

    public async Task<GeneratedFileResult> CreateAllMonthlyReportAsync(
        int month,
        int year,
        CancellationToken cancellationToken = default)
    {
        ValidateMonthlyPeriod(month, year);

        var periodLabel = FormatRussianMonthYear(month, year);
        var allAnswers = (await _surveyReportRepository.GetAnswersAsync(cancellationToken))
            .Where(answer => answer.CompletionDate?.Month == month && answer.CompletionDate?.Year == year)
            .Where(answer => answer.Answers.Count > 0)
            .ToList();

        if (allAnswers.Count == 0)
        {
            throw new InvalidOperationException("За выбранный месяц и год записи для отчёта не найдены.");
        }

        var reportSections = (await _surveyReportRepository.GetSurveysAsync(cancellationToken))
            .Select(survey => new
            {
                Survey = survey,
                Criteria = survey.Questions?.Select(question => question.Text).ToList() ?? new List<string>(),
                Answers = allAnswers
                    .Where(answer => answer.IdSurvey == survey.IdSurvey)
                    .ToList()
            })
            .Where(section => section.Criteria.Count > 0 && section.Answers.Count > 0)
            .ToList();

        if (reportSections.Count == 0)
        {
            throw new InvalidOperationException("За выбранный месяц и год записи для отчёта не найдены.");
        }

        string fileName = $"Сводный отчет по всем анкетам ({periodLabel}).docx";

        using var mem = new MemoryStream();
        using (var document = WordprocessingDocument.Create(mem, WordprocessingDocumentType.Document, true))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            body.AppendChild(new Paragraph(
                new Run(new Text("Сводный отчет по всем анкетам"))
                {
                    RunProperties = new RunProperties(
                        new Bold(),
                        new FontSize() { Val = "28" })
                })
            {
                ParagraphProperties = new ParagraphProperties(
                    new Justification() { Val = JustificationValues.Center },
                    new SpacingBetweenLines() { After = "200" })
            });

            body.AppendChild(new Paragraph(
                new Run(new Text($"за {periodLabel}"))
                {
                    RunProperties = new RunProperties(
                        new Italic(),
                        new FontSize() { Val = "22" })
                })
            {
                ParagraphProperties = new ParagraphProperties(
                    new Justification() { Val = JustificationValues.Center },
                    new SpacingBetweenLines() { After = "300" })
            });

            body.AppendChild(new Paragraph(
                new Run(new Text("Данный отчет содержит сводную информацию по всем анкетам за выбранный месяц."))
                {
                    RunProperties = new RunProperties(new FontSize() { Val = "20" })
                })
            {
                ParagraphProperties = new ParagraphProperties(new SpacingBetweenLines() { After = "200" })
            });

            for (int surveyIndex = 0; surveyIndex < reportSections.Count; surveyIndex++)
            {
                var section = reportSections[surveyIndex];
                var organizations = new List<string>();
                var ratings = new List<List<int>>();
                var srednee = new List<double>();

                foreach (var answer in section.Answers)
                {
                    organizations.Add(answer.OrganizationName ?? string.Empty);
                    ratings.Add(answer.Answers.Select(item => item.Rating ?? 0).ToList());
                }

                for (int criteriaIndex = 0; criteriaIndex < section.Criteria.Count; criteriaIndex++)
                {
                    double sum = 0;
                    int count = 0;
                    for (int ratingIndex = 0; ratingIndex < ratings.Count; ratingIndex++)
                    {
                        if (ratings[ratingIndex].Count > criteriaIndex)
                        {
                            sum += ratings[ratingIndex][criteriaIndex];
                            count++;
                        }
                    }

                    srednee.Add(count > 0 ? sum / count : 0);
                }

                var isArchived = section.Survey.DateEnd.HasValue && section.Survey.DateEnd.Value < _clock.Today;
                string surveyTitle = section.Survey.NameSurvey + (isArchived ? " (архивная)" : string.Empty);
                body.AppendChild(new Paragraph(
                    new Run(new Text(surveyTitle))
                    {
                        RunProperties = new RunProperties(
                            new Bold(),
                            new FontSize() { Val = "22" })
                    })
                {
                    ParagraphProperties = new ParagraphProperties(new SpacingBetweenLines() { Before = "400", After = "100" })
                });

                var questionsTable = new Table();
                questionsTable.AppendChild(new TableProperties(
                    new TableBorders(
                        new TopBorder { Val = BorderValues.Single, Size = 4 },
                        new BottomBorder { Val = BorderValues.Single, Size = 4 },
                        new LeftBorder { Val = BorderValues.Single, Size = 4 },
                        new RightBorder { Val = BorderValues.Single, Size = 4 },
                        new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                        new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 }),
                    new TableWidth() { Width = "100%", Type = TableWidthUnitValues.Auto }));

                var qHeaderRow = new TableRow();
                qHeaderRow.Append(new TableCell(new Paragraph(new Run(new Text("№")) { RunProperties = new RunProperties(new Bold()) })));
                qHeaderRow.Append(new TableCell(new Paragraph(new Run(new Text("Вопрос")) { RunProperties = new RunProperties(new Bold()) })));
                qHeaderRow.Append(new TableCell(new Paragraph(new Run(new Text("Средняя оценка")) { RunProperties = new RunProperties(new Bold()) })));
                questionsTable.Append(qHeaderRow);

                for (int criteriaIndex = 0; criteriaIndex < section.Criteria.Count; criteriaIndex++)
                {
                    var row = new TableRow();
                    row.Append(new TableCell(new Paragraph(new Run(new Text((criteriaIndex + 1).ToString())))));
                    row.Append(new TableCell(new Paragraph(new Run(new Text(section.Criteria[criteriaIndex])))));
                    row.Append(new TableCell(new Paragraph(new Run(new Text(srednee[criteriaIndex].ToString("F1"))))));
                    questionsTable.Append(row);
                }

                body.AppendChild(questionsTable);

                var orgsTable = new Table();
                orgsTable.AppendChild(new TableProperties(
                    new TableBorders(
                        new TopBorder { Val = BorderValues.Single, Size = 4 },
                        new BottomBorder { Val = BorderValues.Single, Size = 4 },
                        new LeftBorder { Val = BorderValues.Single, Size = 4 },
                        new RightBorder { Val = BorderValues.Single, Size = 4 },
                        new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                        new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 }),
                    new TableWidth() { Width = "100%", Type = TableWidthUnitValues.Auto }));

                var oHeaderRow = new TableRow();
                oHeaderRow.Append(new TableCell(new Paragraph(new Run(new Text("Организация")) { RunProperties = new RunProperties(new Bold()) })));
                oHeaderRow.Append(new TableCell(new Paragraph(new Run(new Text("Средняя оценка")) { RunProperties = new RunProperties(new Bold()) })));
                oHeaderRow.Append(new TableCell(new Paragraph(new Run(new Text("Кол-во ответов")) { RunProperties = new RunProperties(new Bold()) })));
                orgsTable.Append(oHeaderRow);

                for (int answerIndex = 0; answerIndex < organizations.Count; answerIndex++)
                {
                    var row = new TableRow();
                    row.Append(new TableCell(new Paragraph(new Run(new Text(organizations[answerIndex])))));
                    row.Append(new TableCell(new Paragraph(new Run(new Text(ratings[answerIndex].Count > 0 ? ratings[answerIndex].Average().ToString("F1") : "0")))));
                    row.Append(new TableCell(new Paragraph(new Run(new Text(ratings[answerIndex].Count.ToString())))));
                    orgsTable.Append(row);
                }

                var totalRow = new TableRow();
                totalRow.Append(new TableCell(new Paragraph(new Run(new Text("Итого:")))));
                totalRow.Append(new TableCell(new Paragraph(new Run(new Text(srednee.Count > 0 ? srednee.Average().ToString("F1") : "0")))));
                totalRow.Append(new TableCell(new Paragraph(new Run(new Text(ratings.Sum(rating => rating.Count).ToString())))));
                orgsTable.Append(totalRow);

                body.AppendChild(orgsTable);

                if (surveyIndex < reportSections.Count - 1)
                {
                    body.AppendChild(new Paragraph(new Run(new Break() { Type = BreakValues.Page })));
                }
            }

            body.AppendChild(new Paragraph(
                new Run(new Text("Отчет сформирован автоматически"))
                {
                    RunProperties = new RunProperties(
                        new Italic(),
                        new FontSize() { Val = "16" })
                })
            {
                ParagraphProperties = new ParagraphProperties(
                    new Justification() { Val = JustificationValues.Right },
                    new SpacingBetweenLines() { Before = "300" })
            });
        }

        return new GeneratedFileResult
        {
            Content = mem.ToArray(),
            ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            FileName = fileName
        };
    }

    public async Task<GeneratedFileResult> CreateQuarterlyReportAsync(
        int quarter,
        int year,
        CancellationToken cancellationToken = default)
    {
        ValidateQuarterlyPeriod(quarter, year);

        var months = GetMonthsForQuarter(quarter);
        var monthNumbers = months.Select(month => month.Number).ToHashSet();
        var quarterName = quarter switch
        {
            1 => "I",
            2 => "II",
            3 => "III",
            4 => "IV",
            _ => quarter.ToString(CultureInfo.InvariantCulture)
        };

        var answers = (await _surveyReportRepository.GetAnswersAsync(cancellationToken))
            .Where(answer => answer.CompletionDate.HasValue)
            .Where(answer => answer.CompletionDate!.Value.Year == year)
            .Where(answer => monthNumbers.Contains(answer.CompletionDate!.Value.Month))
            .Where(answer => answer.Answers.Count > 0)
            .ToList();

        if (answers.Count == 0)
        {
            throw new InvalidOperationException("За выбранный квартал и год записи для отчёта не найдены.");
        }

        var surveys = await _surveyReportRepository.GetSurveysAsync(cancellationToken);

        using var workbook = new XLWorkbook();
        int worksheetsCreated = 0;

        foreach (var survey in surveys)
        {
            var questions = survey.Questions;
            if (questions == null || questions.Count == 0)
            {
                continue;
            }

            var surveyAnswers = answers
                .Where(answer => answer.IdSurvey == survey.IdSurvey)
                .ToList();

            if (surveyAnswers.Count == 0)
            {
                continue;
            }

            var worksheet = workbook.Worksheets.Add(BuildUniqueWorksheetName(workbook, survey.NameSurvey));
            BuildWorksheetHeaders(worksheet, questions);

            int currentRow = 3;
            var orgAverages = new List<double>();
            var questionRatings = new Dictionary<int, List<double>>();

            for (int i = 0; i < questions.Count; i++)
            {
                questionRatings[i] = new List<double>();
            }

            foreach (var month in months)
            {
                string monthHeader = $"{month.Name} {year} г.";
                worksheet.Cell(currentRow, 1).Value = monthHeader;
                worksheet.Range(currentRow, 1, currentRow, 2 + questions.Count * 2 + 1).Merge();
                worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                currentRow++;

                var monthAnswers = surveyAnswers
                    .Where(answer => answer.CompletionDate?.Month == month.Number && answer.CompletionDate?.Year == year)
                    .GroupBy(answer => answer.OrganizationName)
                    .OrderBy(group => group.Key)
                    .ToList();

                if (monthAnswers.Count == 0)
                {
                    worksheet.Cell(currentRow, 1).Value = "Нет данных";
                    worksheet.Range(currentRow, 1, currentRow, 2 + questions.Count * 2 + 1).Merge();
                    currentRow++;
                    continue;
                }

                foreach (var orgGroup in monthAnswers)
                {
                    worksheet.Cell(currentRow, 1).Value = orgGroup.Key ?? "Не указано";
                    worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                    var answersData = orgGroup.First().Answers ?? new List<AnswerPayloadItem>();
                    var orgRatings = new List<double>();

                    for (int i = 0; i < questions.Count; i++)
                    {
                        string questionId = questions[i].Id.ToString();
                        var answer = answersData.FirstOrDefault(item => item.QuestionId == questionId);
                        if (answer?.Rating.HasValue == true)
                        {
                            double rating = answer.Rating.Value;
                            worksheet.Cell(currentRow, 2 + i).Value = rating;
                            orgRatings.Add(rating);
                            questionRatings[i].Add(rating);
                        }
                        else
                        {
                            worksheet.Cell(currentRow, 2 + i).Value = string.Empty;
                        }
                    }

                    worksheet.Cell(currentRow, 2 + questions.Count).Value = orgRatings.Count > 0
                        ? orgRatings.Average()
                        : string.Empty;

                    if (orgRatings.Count > 0)
                    {
                        orgAverages.Add(orgRatings.Average());
                    }

                    for (int i = 0; i < questions.Count; i++)
                    {
                        string questionId = questions[i].Id.ToString();
                        var answer = answersData.FirstOrDefault(item => item.QuestionId == questionId);
                        worksheet.Cell(currentRow, 2 + questions.Count + 1 + i).Value = answer?.Comment ?? string.Empty;
                    }

                    worksheet.Row(currentRow).AdjustToContents();
                    currentRow++;
                }
            }

            if (currentRow > 3)
            {
                worksheet.Cell(currentRow, 1).Value = "Итого:";
                worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                for (int i = 0; i < questions.Count; i++)
                {
                    worksheet.Cell(currentRow, 2 + i).Value = questionRatings[i].Count > 0
                        ? questionRatings[i].Average()
                        : string.Empty;
                }

                if (questionRatings.Any(entry => entry.Value.Count > 0))
                {
                    worksheet.Cell(currentRow, 2 + questions.Count).Value =
                        questionRatings.Where(entry => entry.Value.Count > 0).Average(entry => entry.Value.Average());
                }

                currentRow++;
                worksheet.Cell(currentRow, 1).Value = "Всего среднее";
                worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                if (orgAverages.Count > 0)
                {
                    worksheet.Cell(currentRow, 2 + questions.Count).Value = orgAverages.Average();
                }
            }

            FormatWorksheet(worksheet, questions.Count);
            worksheetsCreated++;
        }

        if (worksheetsCreated == 0)
        {
            throw new InvalidOperationException("За выбранный квартал и год записи для отчёта не найдены.");
        }

        string safeQuarterName = string.Join("_", quarterName.Split(Path.GetInvalidFileNameChars()));
        string fileName = $"Отчет_за_{safeQuarterName}_квартал_{year}_{_clock.Now:yyyyMMdd_HHmmss}.xlsx";

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return new GeneratedFileResult
        {
            Content = stream.ToArray(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            FileName = fileName
        };
    }

    private static void ValidateMonthlyPeriod(int month, int year)
    {
        if (month is < 1 or > 12)
        {
            throw new InvalidOperationException("Выберите корректный месяц для формирования отчёта.");
        }

        if (year <= 0)
        {
            throw new InvalidOperationException("Выберите корректный год для формирования отчёта.");
        }
    }

    private static void ValidateQuarterlyPeriod(int quarter, int year)
    {
        if (quarter is < 1 or > 4)
        {
            throw new InvalidOperationException("Выберите корректный квартал для формирования отчёта.");
        }

        if (year <= 0)
        {
            throw new InvalidOperationException("Выберите корректный год для формирования отчёта.");
        }
    }

    private static string FormatRussianMonthYear(int month, int year)
    {
        var culture = CultureInfo.GetCultureInfo("ru-RU");
        return new DateTime(year, month, 1).ToString("MMMM yyyy", culture).ToLower(culture);
    }

    private static string BuildUniqueWorksheetName(XLWorkbook workbook, string? surveyName)
    {
        var baseName = new string((surveyName ?? "Опрос")
            .Where(character => char.IsLetterOrDigit(character) || char.IsWhiteSpace(character) || character == '-' || character == '_')
            .ToArray())
            .Trim();

        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "Опрос";
        }

        if (baseName.Length > 31)
        {
            baseName = baseName[..31];
        }

        var worksheetName = baseName;
        int suffix = 2;

        while (workbook.Worksheets.Any(worksheet => string.Equals(worksheet.Name, worksheetName, StringComparison.OrdinalIgnoreCase)))
        {
            var suffixText = $"_{suffix}";
            var prefixLength = Math.Max(1, 31 - suffixText.Length);
            worksheetName = $"{baseName[..Math.Min(baseName.Length, prefixLength)]}{suffixText}";
            suffix++;
        }

        return worksheetName;
    }

    private TableCell CreateTableCell(string text, bool isHeader, bool centerAlign)
    {
        var runProperties = new RunProperties(
            new FontSize() { Val = isHeader ? "18" : "16" });

        if (isHeader)
        {
            runProperties.AppendChild(new Bold());
        }

        var cell = new TableCell(
            new Paragraph(
                new Run(new Text(text))
                {
                    RunProperties = runProperties
                }));

        cell.TableCellProperties = new TableCellProperties(
            new Justification() { Val = centerAlign ? JustificationValues.Center : JustificationValues.Left },
            new TableCellVerticalAlignment() { Val = TableVerticalAlignmentValues.Center },
            new TableCellWidth() { Type = TableWidthUnitValues.Auto });

        return cell;
    }

    private void BuildWorksheetHeaders(IXLWorksheet worksheet, IReadOnlyList<SurveyQuestionItem> questions)
    {
        var orgHeader = worksheet.Cell(1, 1);
        orgHeader.Value = "Наименование организации";
        orgHeader.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        worksheet.Range(1, 1, 2, 1).Merge();

        var criteriaHeader = worksheet.Cell(1, 2);
        criteriaHeader.Value = "Название критериев";
        criteriaHeader.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        worksheet.Range(1, 2, 1, 1 + questions.Count).Merge();

        for (int i = 0; i < questions.Count; i++)
        {
            var cell = worksheet.Cell(2, 2 + i);
            cell.Value = questions[i].Text;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        var avgHeader = worksheet.Cell(1, 2 + questions.Count);
        avgHeader.Value = "Средний балл";
        avgHeader.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        worksheet.Range(1, 2 + questions.Count, 2, 2 + questions.Count).Merge();

        var commentsHeader = worksheet.Cell(1, 2 + questions.Count + 1);
        commentsHeader.Value = "Комментарии";
        commentsHeader.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        worksheet.Range(1, 2 + questions.Count + 1, 1, 1 + questions.Count * 2 + 1).Merge();

        for (int i = 0; i < questions.Count; i++)
        {
            var cell = worksheet.Cell(2, 2 + questions.Count + 1 + i);
            cell.Value = $"Комментарий к {questions[i].Text}";
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
    }

    private static List<(string Name, int Number)> GetMonthsForQuarter(int quarter)
    {
        return quarter switch
        {
            1 => new List<(string, int)> { ("Январь", 1), ("Февраль", 2), ("Март", 3) },
            2 => new List<(string, int)> { ("Апрель", 4), ("Май", 5), ("Июнь", 6) },
            3 => new List<(string, int)> { ("Июль", 7), ("Август", 8), ("Сентябрь", 9) },
            4 => new List<(string, int)> { ("Октябрь", 10), ("Ноябрь", 11), ("Декабрь", 12) },
            _ => new List<(string, int)>()
        };
    }

    private void FormatWorksheet(IXLWorksheet worksheet, int questionsCount)
    {
        var usedRange = worksheet.RangeUsed();
        if (usedRange == null)
        {
            return;
        }

        usedRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        usedRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        foreach (var cell in usedRange.CellsUsed())
        {
            if (cell.DataType == XLDataType.Number ||
                (cell.DataType == XLDataType.Text &&
                 !string.IsNullOrEmpty(cell.GetString()) &&
                 double.TryParse(cell.GetString(), out _)))
            {
                cell.Style.NumberFormat.Format = "0.00";
            }
        }

        worksheet.Column(1).Width = 30;
        for (int col = 2; col <= 1 + questionsCount; col++)
        {
            worksheet.Column(col).Width = 20;
        }

        worksheet.Column(2 + questionsCount).Width = 15;
        for (int col = 2 + questionsCount + 1; col <= 1 + questionsCount * 2 + 1; col++)
        {
            worksheet.Column(col).Width = 25;
        }

        usedRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        usedRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        worksheet.Rows(1, 2).AdjustToContents();

        var lastRow = worksheet.LastRowUsed();
        if (lastRow == null)
        {
            return;
        }

        for (int row = 3; row <= lastRow.RowNumber(); row++)
        {
            var cell = worksheet.Cell(row, 1);
            if (cell.Value.ToString() != "Нет данных")
            {
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            }
        }
    }

}
