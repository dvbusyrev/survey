using System.Data;
using System.Text.Json;
using ClosedXML.Excel;
using Dapper;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Application.UseCases.Answers;
using MainProject.Infrastructure.Persistence;
using MainProject.Domain.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<SurveyReportService> _logger;
    private readonly string _downloadsPath;

    public SurveyReportService(IDbConnectionFactory connectionFactory, ILogger<SurveyReportService> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
        _downloadsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");
    }

    public GeneratedFileResult CreateSurveyMonthlyReport(int surveyId, int organizationId)
    {
        string surveyName = string.Empty;
        var criteriaList = new List<string>();
        var organizations = new List<string>();
        var ratings = new List<List<int>>();
        var comments = new List<List<string>>();
        var srednee = new List<double>();

        using (var connection = _connectionFactory.CreateConnection())
        {
            surveyName = connection.ExecuteScalar<string?>(
                @"SELECT name_survey
                  FROM public.survey
                  WHERE id_survey = @surveyId",
                new { surveyId }) ?? string.Empty;

            criteriaList = LoadSurveyQuestions(connection, surveyId)
                .Select(question => question.Text)
                .ToList();

            var surveyAnswers = LoadSurveyAnswers(connection, surveyId, organizationId == 0 ? null : organizationId);
            foreach (var answer in surveyAnswers)
            {
                organizations.Add(answer.OrganizationName ?? string.Empty);
                ratings.Add(answer.Answers.Select(item => item.Rating ?? 0).ToList());
                comments.Add(answer.Answers.Select(item => item.Comment ?? string.Empty).ToList());
            }

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
                srednee.Add(count > 0 ? sum / count : 0);
            }
        }

        string currentMonth = DateTime.Now.ToString("MMMM yyyy").ToLower();
        string fileName = organizationId == 0
            ? $"Отчет по анкете {surveyName} ({currentMonth}).docx"
            : $"Отчет по анкете {surveyName} для {organizations.FirstOrDefault()} ({currentMonth}).docx";

        using var mem = new MemoryStream();
        using (var document = WordprocessingDocument.Create(mem, WordprocessingDocumentType.Document, true))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            var totalAvg = srednee.Count > 0 ? srednee.Average() : 0;

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

                string avgValue = (srednee.Count > i) ? srednee[i].ToString("F1") : "-";
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
                double sum = 0;
                int count = 0;
                for (int row = 0; row < ratings.Count; row++)
                {
                    if (ratings[row].Count > i)
                    {
                        sum += ratings[row][i];
                        count++;
                    }
                }

                string avgValue = count > 0 ? (sum / count).ToString("F1") : "-";
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

    public GeneratedFileResult CreateAllMonthlyReport()
    {
        var surveyIds = new List<int>();
        using (var connection = _connectionFactory.CreateConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = @"
                SELECT id_survey FROM public.survey";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                surveyIds.Add(reader.GetInt32(0));
            }
        }

        string currentMonth = DateTime.Now.ToString("MMMM yyyy").ToLower();
        string fileName = $"Сводный отчет по всем анкетам ({currentMonth}).docx";

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
                new Run(new Text("Данный отчет содержит сводную информацию по всем анкетам за указанный месяц."))
                {
                    RunProperties = new RunProperties(new FontSize() { Val = "20" })
                })
            {
                ParagraphProperties = new ParagraphProperties(new SpacingBetweenLines() { After = "200" })
            });

            foreach (var surveyId in surveyIds)
            {
                string surveyName;
                bool isArchive;
                List<string> criteriaList;
                var organizations = new List<string>();
                var ratings = new List<List<int>>();
                var srednee = new List<double>();

                using (var connection = _connectionFactory.CreateConnection())
                {
                    isArchive = connection.ExecuteScalar<bool>(
                        "SELECT date_close < NOW() FROM public.survey WHERE id_survey = @surveyId",
                        new { surveyId });

                    surveyName = connection.ExecuteScalar<string?>(
                        "SELECT name_survey FROM public.survey WHERE id_survey = @surveyId",
                        new { surveyId }) ?? string.Empty;

                    criteriaList = LoadSurveyQuestions(connection, surveyId)
                        .Select(question => question.Text)
                        .ToList();

                    var surveyAnswers = LoadSurveyAnswers(connection, surveyId);
                    foreach (var answer in surveyAnswers)
                    {
                        organizations.Add(answer.OrganizationName ?? string.Empty);
                        ratings.Add(answer.Answers.Select(item => item.Rating ?? 0).ToList());
                    }

                    for (int i = 0; i < criteriaList.Count; i++)
                    {
                        double sum = 0;
                        int count = 0;
                        for (int j = 0; j < ratings.Count; j++)
                        {
                            if (ratings[j].Count > i)
                            {
                                sum += ratings[j][i];
                                count++;
                            }
                        }
                        srednee.Add(count > 0 ? sum / count : 0);
                    }
                }

                string surveyTitle = surveyName + (isArchive ? " (архивная)" : "");
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

                for (int i = 0; i < criteriaList.Count; i++)
                {
                    var row = new TableRow();
                    row.Append(new TableCell(new Paragraph(new Run(new Text((i + 1).ToString())))));
                    row.Append(new TableCell(new Paragraph(new Run(new Text(criteriaList[i])))));
                    row.Append(new TableCell(new Paragraph(new Run(new Text(srednee[i].ToString("F1"))))));
                    questionsTable.Append(row);
                }
                body.AppendChild(questionsTable);

                if (organizations.Count > 0)
                {
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

                    for (int i = 0; i < organizations.Count; i++)
                    {
                        var row = new TableRow();
                        row.Append(new TableCell(new Paragraph(new Run(new Text(organizations[i])))));
                        row.Append(new TableCell(new Paragraph(new Run(new Text(ratings[i].Count > 0 ? ratings[i].Average().ToString("F1") : "0")))));
                        row.Append(new TableCell(new Paragraph(new Run(new Text(ratings[i].Count.ToString())))));
                        orgsTable.Append(row);
                    }

                    var totalRow = new TableRow();
                    totalRow.Append(new TableCell(new Paragraph(new Run(new Text("Итого:")))));
                    totalRow.Append(new TableCell(new Paragraph(new Run(new Text(srednee.Count > 0 ? srednee.Average().ToString("F1") : "0")))));
                    totalRow.Append(new TableCell(new Paragraph(new Run(new Text(ratings.Sum(r => r.Count).ToString())))));
                    orgsTable.Append(totalRow);

                    body.AppendChild(orgsTable);
                }
                else
                {
                    body.AppendChild(new Paragraph(
                        new Run(new Text("Нет данных по ответам организаций"))
                        {
                            RunProperties = new RunProperties(
                                new Italic(),
                                new FontSize() { Val = "16" })
                        })
                    {
                        ParagraphProperties = new ParagraphProperties(new SpacingBetweenLines() { Before = "100", After = "100" })
                    });
                }

                if (surveyId != surveyIds.Last())
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

    public GeneratedFileResult CreateQuarterlyReport(int quarter, int year)
    {
        if (year == 0)
        {
            year = DateTime.Now.Year;
        }

        var quarterNames = new Dictionary<int, string>
        {
            { 1, "I" },
            { 2, "II" },
            { 3, "III" },
            { 4, "IV" }
        };

        string quarterName = quarterNames.ContainsKey(quarter)
            ? quarterNames[quarter]
            : $"{quarter} квартал";

        var answers = GetAnswersFromDatabase();
        var surveys = GetSurveysForReport();

        using var workbook = new XLWorkbook();
        foreach (var survey in surveys)
        {
            var surveyAnswers = answers.Where(a => a.IdSurvey == survey.IdSurvey).ToList();
            if (surveyAnswers.Count == 0)
            {
                continue;
            }

            string sheetName = new string((survey.NameSurvey ?? "Опрос")
                .Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || c == '-' || c == '_')
                .Take(31)
                .ToArray());

            var worksheet = workbook.Worksheets.Add(sheetName);
            var questions = survey.Questions;

            if (questions == null || questions.Count == 0)
            {
                continue;
            }

            BuildWorksheetHeaders(worksheet, questions);

            int currentRow = 3;
            var months = GetMonthsForQuarter(quarter);
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
                    .Where(a => a.CreateDateSurvey?.Month == month.Number && a.CreateDateSurvey?.Year == year)
                    .GroupBy(a => a.OrganizationName)
                    .OrderBy(g => g.Key);

                foreach (var orgGroup in monthAnswers)
                {
                    worksheet.Cell(currentRow, 1).Value = orgGroup.Key;
                    worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                    var answersData = orgGroup.First().Answers ?? new List<MainProject.Application.UseCases.Answers.AnswerPayloadItem>();

                    var orgRatings = new List<double>();
                    for (int i = 0; i < questions.Count; i++)
                    {
                        string questionId = questions[i].Id.ToString();
                        var answer = answersData.FirstOrDefault(a => a.QuestionId == questionId);
                        if (answer?.Rating.HasValue == true)
                        {
                            var rating = answer.Rating.Value;
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
                        var answer = answersData.FirstOrDefault(a => a.QuestionId == questionId);
                        worksheet.Cell(currentRow, 2 + questions.Count + 1 + i).Value = answer?.Comment ?? string.Empty;
                    }

                    worksheet.Row(currentRow).AdjustToContents();
                    currentRow++;
                }

                if (!monthAnswers.Any())
                {
                    worksheet.Cell(currentRow, 1).Value = "Нет данных";
                    worksheet.Range(currentRow, 1, currentRow, 2 + questions.Count * 2 + 1).Merge();
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

                if (questionRatings.Any(q => q.Value.Count > 0))
                {
                    worksheet.Cell(currentRow, 2 + questions.Count).Value =
                        questionRatings.Where(q => q.Value.Count > 0).Average(q => q.Value.Average());
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
        }

        string safeQuarterName = string.Join("_", quarterName.Split(Path.GetInvalidFileNameChars()));
        string fileName = $"quarterly_report_{safeQuarterName}_{year}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

        Directory.CreateDirectory(_downloadsPath);
        string filePath = Path.Combine(_downloadsPath, fileName);
        workbook.SaveAs(filePath);

        return new GeneratedFileResult
        {
            Content = File.ReadAllBytes(filePath),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            FileName = fileName
        };
    }

    private static IReadOnlyList<SurveyQuestionItem> LoadSurveyQuestions(
        IDbConnection connection,
        int surveyId)
    {
        return connection.Query<SurveyQuestionItem>(
            @"SELECT
                  question_order AS Id,
                  question_text AS Text
              FROM public.survey_question
              WHERE id_survey = @surveyId
              ORDER BY question_order",
            new { surveyId }).ToList();
    }

    private static IReadOnlyList<AnswerRecord> LoadSurveyAnswers(
        IDbConnection connection,
        int surveyId,
        int? organizationId = null)
    {
        var answers = connection.Query<AnswerRecord>(
            @"SELECT
                  ha.id_answer,
                  ha.organization_id,
                  ha.id_survey,
                  ha.csp,
                  ha.completion_date,
                  ha.create_date_survey,
                  o.organization_name
              FROM public.answer ha
              LEFT JOIN public.organization o
                  ON o.organization_id = ha.organization_id
              WHERE ha.id_survey = @surveyId
                AND (@organizationId IS NULL OR ha.organization_id = @organizationId)
                AND EXISTS (
                    SELECT 1
                    FROM public.answer_item hai
                    WHERE hai.id_answer = ha.id_answer
                )
              ORDER BY ha.completion_date DESC",
            new { surveyId, organizationId }).ToList();

        AttachAnswerItems(connection, answers);
        return answers;
    }

    private IReadOnlyList<Survey> GetSurveysForReport()
    {
        using var connection = _connectionFactory.CreateConnection();

        var surveys = connection.Query<Survey>(
            @"SELECT
                  s.id_survey,
                  s.name_survey,
                  COALESCE(
                      (
                          SELECT string_agg(o.organization_name, ', ')
                          FROM public.organization_survey os
                          INNER JOIN public.organization o
                              ON o.organization_id = os.organization_id
                          WHERE os.id_survey = s.id_survey
                      ),
                      'Не указано'
                  ) AS organization_name
              FROM public.survey s").ToList();

        AttachSurveyQuestions(connection, surveys);
        return surveys;
    }

    private List<AnswerRecord> GetAnswersFromDatabase()
    {
        using var connection = _connectionFactory.CreateConnection();

        var answers = connection.Query<AnswerRecord>(
            @"SELECT
                  a.organization_id,
                  o.organization_name,
                  a.csp,
                  a.id_answer,
                  a.id_survey,
                  s.name_survey,
                  a.completion_date,
                  a.create_date_survey
              FROM public.answer a
              LEFT JOIN public.organization o
                  ON o.organization_id = a.organization_id
              LEFT JOIN public.survey s
                  ON s.id_survey = a.id_survey").ToList();

        AttachAnswerItems(connection, answers);
        return answers;
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

    private static void AttachSurveyQuestions(
        IDbConnection connection,
        IEnumerable<Survey> surveys)
    {
        var surveyList = surveys.ToList();
        if (surveyList.Count == 0)
        {
            return;
        }

        var surveyIds = surveyList.Select(s => s.IdSurvey).Distinct().ToArray();

        var activeRows = connection.Query<SurveyQuestionLookupRow>(
            @"SELECT
                  id_survey AS SurveyId,
                  question_order AS QuestionOrder,
                  question_text AS QuestionText
              FROM public.survey_question
              WHERE id_survey = ANY(@surveyIds)
              ORDER BY id_survey, question_order",
            new { surveyIds });

        var questionLookup = activeRows
            .GroupBy(row => row.SurveyId)
            .ToDictionary(
                group => group.Key,
                group => (List<SurveyQuestionItem>)group
                    .Select(row => new SurveyQuestionItem
                    {
                        Id = row.QuestionOrder,
                        Text = row.QuestionText
                    })
                    .OrderBy(question => question.Id)
                    .ToList());

        foreach (var survey in surveyList)
        {
            survey.Questions = questionLookup.GetValueOrDefault(survey.IdSurvey, new List<SurveyQuestionItem>());
        }
    }

    private static void AttachAnswerItems(
        IDbConnection connection,
        IEnumerable<AnswerRecord> answers)
    {
        var answerList = answers.ToList();
        if (answerList.Count == 0)
        {
            return;
        }

        var answerIds = answerList.Select(a => a.IdAnswer).Distinct().ToArray();
        var rows = connection.Query<AnswerItemLookupRow>(
            @"SELECT
                  id_answer AS AnswerId,
                  question_order AS QuestionOrder,
                  question_text AS QuestionText,
                  rating AS Rating,
                  comment AS Comment
              FROM public.answer_item
              WHERE id_answer = ANY(@answerIds)
              ORDER BY id_answer, question_order",
            new { answerIds });

        var answerLookup = rows
            .GroupBy(row => row.AnswerId)
            .ToDictionary(
                group => group.Key,
                group => (List<AnswerPayloadItem>)group
                    .Select(row => new AnswerPayloadItem
                    {
                        QuestionId = row.QuestionOrder.ToString(),
                        QuestionText = row.QuestionText,
                        Rating = row.Rating,
                        Comment = row.Comment
                    })
                    .ToList());

        foreach (var answer in answerList)
        {
            answer.Answers = answerLookup.GetValueOrDefault(answer.IdAnswer, new List<AnswerPayloadItem>());
        }
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

    private sealed class SurveyQuestionLookupRow
    {
        public int SurveyId { get; init; }
        public int QuestionOrder { get; init; }
        public string QuestionText { get; init; } = string.Empty;
    }

    private sealed class AnswerItemLookupRow
    {
        public int AnswerId { get; init; }
        public int QuestionOrder { get; init; }
        public string QuestionText { get; init; } = string.Empty;
        public int? Rating { get; init; }
        public string? Comment { get; init; }
    }
}
