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
