using System.Globalization;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Application.UseCases.Answers;
using MainProject.Domain.Entities;
using MainProject.Infrastructure.Persistence;

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

public partial class SurveyService
{
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

        surveyName = await _surveyRepository.GetSurveyNameAsync(surveyId, cancellationToken) ?? string.Empty;
        criteriaList = (await _surveyRepository.GetSurveyQuestionsAsync(surveyId, cancellationToken))
            .Select(question => question.Text)
            .ToList();

        var surveyAnswers = await _surveyRepository.GetSurveyAnswersAsync(
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
        var allAnswers = (await _surveyRepository.GetAnswersAsync(cancellationToken))
            .Where(answer => answer.CompletionDate?.Month == month && answer.CompletionDate?.Year == year)
            .Where(answer => answer.Answers.Count > 0)
            .ToList();

        if (allAnswers.Count == 0)
        {
            throw new InvalidOperationException("За выбранный месяц и год записи для отчёта не найдены.");
        }

        var reportSections = (await _surveyRepository.GetSurveysAsync(cancellationToken))
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
}
