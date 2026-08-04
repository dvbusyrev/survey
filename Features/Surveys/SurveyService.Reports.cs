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
    public Task<IReadOnlyList<int>> GetAvailableReportYearsAsync(CancellationToken cancellationToken = default)
        => _surveyRepository.GetAvailableYearsAsync(cancellationToken);
}
