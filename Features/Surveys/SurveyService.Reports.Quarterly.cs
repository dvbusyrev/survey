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

        var answers = (await _surveyRepository.GetAnswersAsync(cancellationToken))
            .Where(answer => answer.CompletionDate.HasValue)
            .Where(answer => answer.CompletionDate!.Value.Year == year)
            .Where(answer => monthNumbers.Contains(answer.CompletionDate!.Value.Month))
            .Where(answer => answer.Answers.Count > 0)
            .ToList();

        if (answers.Count == 0)
        {
            throw new InvalidOperationException("За выбранный квартал и год записи для отчёта не найдены.");
        }

        var surveys = await _surveyRepository.GetSurveysAsync(cancellationToken);

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
}
