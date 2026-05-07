using System.Data;
using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Infrastructure.Persistence;
using MainProject.Domain.Entities;
using MainProject.Web.ViewModels;

namespace MainProject.Application.UseCases.Surveys;

public sealed class SurveyArchiveService : ISurveyArchiveService
{
    private readonly IDbConnectionFactory _connectionFactory;

    public SurveyArchiveService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public UserSurveyArchivePageViewModel? GetUserArchivePage(
        int userId,
        int currentPage,
        string? searchTerm,
        string? date,
        string? dateFrom,
        string? dateTo,
        bool signedOnly)
    {
        using var connection = _connectionFactory.CreateConnection();

        var userOrganizationId = connection.ExecuteScalar<int?>(
            "SELECT id_organization FROM public.app_user WHERE id_user = @userId",
            new { userId });

        if (!userOrganizationId.HasValue)
        {
            return null;
        }

        const int pageSize = 10;
        var normalizedSearchTerm = searchTerm?.Trim() ?? string.Empty;
        var normalizedDate = date?.Trim() ?? string.Empty;
        var normalizedDateFrom = dateFrom?.Trim() ?? string.Empty;
        var normalizedDateTo = dateTo?.Trim() ?? string.Empty;

        var filters = new List<string>();
        var parameters = new DynamicParameters();
        parameters.Add("userOrganizationId", userOrganizationId.Value);
        parameters.Add("searchPattern", string.IsNullOrWhiteSpace(normalizedSearchTerm) ? null : $"%{normalizedSearchTerm}%");
        parameters.Add("offset", Math.Max(currentPage - 1, 0) * pageSize);
        parameters.Add("pageSize", pageSize);

        if (!string.IsNullOrWhiteSpace(normalizedSearchTerm))
        {
            filters.Add("archived.name_survey ILIKE @searchPattern");
        }

        if (DateOnly.TryParse(normalizedDate, out var exactDate))
        {
            filters.Add("archived.completion_date::date = @exactDate");
            parameters.Add("exactDate", exactDate.ToDateTime(TimeOnly.MinValue));
        }
        else
        {
            if (DateTime.TryParse(normalizedDateFrom, out var parsedDateFrom))
            {
                filters.Add("archived.completion_date >= @dateFrom");
                parameters.Add("dateFrom", parsedDateFrom);
            }

            if (DateTime.TryParse(normalizedDateTo, out var parsedDateTo))
            {
                filters.Add("archived.completion_date <= @dateTo");
                parameters.Add("dateTo", parsedDateTo);
            }
        }

        if (signedOnly)
        {
            filters.Add("COALESCE(archived.csp, '') <> ''");
        }

        var whereClause = filters.Count == 0
            ? string.Empty
            : "WHERE " + string.Join(" AND ", filters);

        const string archivedSql = @"
            FROM (
                SELECT
                    s.id_survey,
                    s.name_survey,
                    s.description,
                    COALESCE(os.date_begin, ss.date_begin) AS date_begin,
                    COALESCE(os.date_end, ss.date_end) AS date_end,
                    a.completion_date,
                    a.csp,
                    os.id_organization AS OrganizationId
                FROM public.survey s
                INNER JOIN public.organization_survey os
                    ON os.id_survey = s.id_survey
                INNER JOIN public.answer a
                    ON a.id_organization_survey = os.id_organization_survey
                LEFT JOIN public.survey_schedule ss
                    ON ss.id_survey = s.id_survey
                WHERE os.id_organization = @userOrganizationId
            ) AS archived";

        var totalCount = connection.ExecuteScalar<int>(
            $"SELECT COUNT(*) {archivedSql} {whereClause}",
            parameters);

        var archivedSurveys = connection.Query<Survey>(
            $@"SELECT
                    archived.id_survey,
                    archived.name_survey,
                    archived.description,
                    archived.date_begin,
                    archived.date_end,
                    archived.completion_date,
                    archived.csp,
                    archived.OrganizationId
               {archivedSql}
               {whereClause}
               ORDER BY archived.completion_date DESC
               OFFSET @offset
               LIMIT @pageSize",
            parameters).ToList();

        var totalPages = totalCount == 0
            ? 1
            : (int)Math.Ceiling((double)totalCount / pageSize);

        return new UserSurveyArchivePageViewModel
        {
            ArchivedSurveys = archivedSurveys,
            UserOrganizationId = userOrganizationId.Value,
            CurrentPage = Math.Max(currentPage, 1),
            TotalPages = totalPages,
            TotalCount = totalCount,
            SearchTerm = normalizedSearchTerm,
            DateFrom = normalizedDateFrom,
            DateTo = normalizedDateTo,
            SignedOnly = signedOnly
        };
    }

    public IReadOnlyList<ArchivedSurvey> GetAdminArchivedSurveys()
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT
                s.id_survey,
                ss.date_begin AS date_begin,
                ss.date_end AS date_end,
                s.name_survey,
                COALESCE(
                    (
                        SELECT string_agg(
                            COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name),
                            ', '
                            ORDER BY COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name)
                        )
                        FROM public.organization_survey os
                        INNER JOIN public.organization o
                            ON o.id_organization = os.id_organization
                        WHERE os.id_survey = s.id_survey
                    ),
                    'Не указано'
                ) AS organization_name,
                s.description
            FROM public.survey s
            LEFT JOIN public.survey_schedule ss
                ON ss.id_survey = s.id_survey
            WHERE EXISTS (
                    SELECT 1
                    FROM public.organization_survey os
                    WHERE os.id_survey = s.id_survey
                )
              AND EXISTS (
                    SELECT 1
                    FROM public.answer a
                    INNER JOIN public.organization_survey aos
                        ON aos.id_organization_survey = a.id_organization_survey
                    WHERE aos.id_survey = s.id_survey
                )
              AND NOT EXISTS (
                    SELECT 1
                    FROM public.organization_survey os
                    WHERE os.id_survey = s.id_survey
                      AND (os.date_end IS NULL OR os.date_end >= CURRENT_DATE)
                )
            ORDER BY id_survey DESC";

        return connection.Query<ArchivedSurvey>(sql).ToList();
    }

    public async Task<int> CopyArchiveSurveyAsync(ArchiveSurveyCopyRequest request)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var transaction = connection.BeginTransaction();

        var archivedSurvey = await connection.QueryFirstOrDefaultAsync<ArchivedSurvey>(
            @"SELECT
                  s.id_survey,
                  ss.date_begin AS date_begin,
                  ss.date_end AS date_end,
                  s.name_survey,
                  s.description
              FROM public.survey s
              LEFT JOIN public.survey_schedule ss
                ON ss.id_survey = s.id_survey
              WHERE s.id_survey = @surveyId
                AND EXISTS (
                    SELECT 1
                    FROM public.organization_survey os
                    WHERE os.id_survey = s.id_survey
                )
                AND EXISTS (
                    SELECT 1
                    FROM public.answer a
                    INNER JOIN public.organization_survey aos
                        ON aos.id_organization_survey = a.id_organization_survey
                    WHERE aos.id_survey = s.id_survey
                )
                AND NOT EXISTS (
                    SELECT 1
                    FROM public.organization_survey os
                    WHERE os.id_survey = s.id_survey
                      AND (os.date_end IS NULL OR os.date_end >= CURRENT_DATE)
                )",
            new { surveyId = request.SurveyId },
            transaction);

        if (archivedSurvey == null)
        {
            throw new InvalidOperationException("Архивная анкета не найдена.");
        }

        archivedSurvey.Questions = connection.Query<SurveyQuestionItem>(
            @"SELECT
                  question_order AS Id,
                  question_text AS Text
              FROM public.survey_question
              WHERE id_survey = @surveyId
              ORDER BY question_order",
            new { surveyId = request.SurveyId },
            transaction).ToList();

        var newSurveyId = await connection.ExecuteScalarAsync<int>(
            @"INSERT INTO public.survey
                (name_survey, description)
              VALUES
                (@nameSurvey, @description)
              RETURNING id_survey;",
            new
            {
                nameSurvey = archivedSurvey.NameSurvey,
                description = archivedSurvey.Description ?? string.Empty
            },
            transaction);

        foreach (var question in archivedSurvey.Questions.OrderBy(q => q.Id))
        {
            await connection.ExecuteAsync(
                @"INSERT INTO public.survey_question (id_survey, question_order, question_text)
                  VALUES (@idSurvey, @questionOrder, @questionText);",
                new
                {
                    idSurvey = newSurveyId,
                    questionOrder = question.Id,
                    questionText = question.Text
                },
                transaction);
        }

        transaction.Commit();
        return newSurveyId;
    }
}
