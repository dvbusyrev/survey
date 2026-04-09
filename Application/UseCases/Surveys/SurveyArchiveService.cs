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
            "SELECT organization_id FROM public.app_user WHERE id_user = @userId",
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
                    s.date_open,
                    GREATEST(COALESCE(os.extended_until, s.date_close), s.date_close) AS date_close,
                    a.completion_date,
                    a.csp,
                    a.organization_id
                FROM public.survey s
                INNER JOIN public.answer a
                    ON s.id_survey = a.id_survey
                LEFT JOIN public.organization_survey os
                    ON os.organization_id = a.organization_id
                   AND os.id_survey = a.id_survey
                WHERE a.organization_id = @userOrganizationId
            ) AS archived";

        var totalCount = connection.ExecuteScalar<int>(
            $"SELECT COUNT(*) {archivedSql} {whereClause}",
            parameters);

        var archivedSurveys = connection.Query<Survey>(
            $@"SELECT
                    archived.id_survey,
                    archived.name_survey,
                    archived.description,
                    archived.date_open,
                    archived.date_close,
                    archived.completion_date,
                    archived.csp,
                    archived.organization_id
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
                s.date_open AS date_begin,
                s.date_close AS date_end,
                s.name_survey,
                s.description
            FROM public.survey s
            WHERE s.date_close < NOW()
            ORDER BY id_survey DESC";

        var surveys = connection.Query<ArchivedSurvey>(sql).ToList();
        AttachArchivedSurveyQuestions(connection, surveys);
        return surveys;
    }

    public async Task<int> CopyArchiveSurveyAsync(ArchiveSurveyCopyRequest request)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var transaction = connection.BeginTransaction();

        var archivedSurvey = await connection.QueryFirstOrDefaultAsync<ArchivedSurvey>(
            @"SELECT
                  id_survey,
                  date_open AS date_begin,
                  date_close AS date_end,
                  name_survey,
                  description
              FROM public.survey
              WHERE id_survey = @surveyId
                AND date_close < NOW()",
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
                (name_survey, description, date_create, date_open, date_close)
              VALUES
                (@nameSurvey, @description, @dateCreate, @dateOpen, @dateClose)
              RETURNING id_survey;",
            new
            {
                nameSurvey = archivedSurvey.NameSurvey,
                description = archivedSurvey.Description ?? string.Empty,
                dateCreate = DateTime.Now,
                dateOpen = archivedSurvey.DateBegin.Date,
                dateClose = archivedSurvey.DateEnd.Date
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

    private static void AttachArchivedSurveyQuestions(
        IDbConnection connection,
        IEnumerable<ArchivedSurvey> surveys)
    {
        var surveyList = surveys.ToList();
        if (surveyList.Count == 0)
        {
            return;
        }

        var surveyIds = surveyList.Select(s => s.IdSurvey).Distinct().ToArray();
        var rows = connection.Query<ArchiveQuestionLookupRow>(
            @"SELECT
                  id_survey AS SurveyId,
                  question_order AS QuestionOrder,
                  question_text AS QuestionText
              FROM public.survey_question
              WHERE id_survey = ANY(@surveyIds)
              ORDER BY id_survey, question_order",
            new { surveyIds });

        var questionLookup = rows
            .GroupBy(row => row.SurveyId)
            .ToDictionary(
                group => group.Key,
                group => (List<SurveyQuestionItem>)group
                    .Select(row => new SurveyQuestionItem
                    {
                        Id = row.QuestionOrder,
                        Text = row.QuestionText
                    })
                    .ToList());

        foreach (var survey in surveyList)
        {
            survey.Questions = questionLookup.GetValueOrDefault(survey.IdSurvey, new List<SurveyQuestionItem>());
        }
    }

    private sealed class ArchiveQuestionLookupRow
    {
        public int SurveyId { get; init; }
        public int QuestionOrder { get; init; }
        public string QuestionText { get; init; } = string.Empty;
    }
}
