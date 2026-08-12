using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Application.Support;
using MainProject.Infrastructure.Persistence;
using MainProject.Domain.Entities;
using MainProject.Web.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace MainProject.Application.UseCases.Surveys;

public partial class SurveyService
{
    private static SurveyTableRowViewModel MapSurveyTablePageRow(SurveyAssignmentTableRow row)
    {
        return new SurveyTableRowViewModel
        {
            IdSurvey = row.IdSurvey,
            NameSurvey = row.NameSurvey ?? string.Empty,
            OriginalNameSurvey = row.OriginalNameSurvey ?? row.NameSurvey ?? string.Empty,
            DateBegin = row.DateBegin,
            DateEnd = row.DateEnd,
            BaseDateEnd = row.BaseDateEnd,
            OrganizationIds = row.OrganizationIds ?? Array.Empty<int>(),
            OrganizationNames = row.OrganizationNames ?? Array.Empty<string>(),
            IsExtension = row.IsExtension,
            ExtensionOrganizationId = row.ExtensionOrganizationId
        };
    }

    private static IReadOnlyList<int> ParseSelectedIds(string? rawValue)
    {
        return string.IsNullOrWhiteSpace(rawValue)
            ? Array.Empty<int>()
            : rawValue
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => int.TryParse(value, out var id) ? id : 0)
                .Where(id => id > 0)
                .Distinct()
                .OrderBy(id => id)
                .ToArray();
    }

    private static IReadOnlyList<SelectionOption> BuildSelectionOptions(IEnumerable<SelectionOption> options)
    {
        return options
            .Where(option => option.Id > 0 && !string.IsNullOrWhiteSpace(option.Name))
            .GroupBy(option => option.Id)
            .Select(group => group.First())
            .OrderBy(option => option.Name, AppListPaging.RuStringComparer)
            .ThenBy(option => option.Id)
            .ToList();
    }

    private static string NormalizeSurveySortField(string? sortBy)
    {
        return sortBy?.Trim() switch
        {
            SurveyListSortFields.Name => SurveyListSortFields.Name,
            SurveyListSortFields.DateBegin => SurveyListSortFields.DateBegin,
            SurveyListSortFields.DateEnd => SurveyListSortFields.DateEnd,
            _ => SurveyListSortFields.Default
        };
    }

    private static string NormalizeSurveySortDirection(string? sortDirection, string sortField)
    {
        if (string.Equals(sortDirection?.Trim(), "asc", StringComparison.OrdinalIgnoreCase))
        {
            return "asc";
        }

        if (string.Equals(sortDirection?.Trim(), "desc", StringComparison.OrdinalIgnoreCase))
        {
            return "desc";
        }

        return sortField switch
        {
            SurveyListSortFields.Name => "asc",
            _ => "desc"
        };
    }
}
