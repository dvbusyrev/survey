using System.Data;
using Dapper;
using main_project.Infrastructure.Database;
using main_project.Models;

namespace main_project.Services.Surveys;

public sealed class SurveyArchiveService
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

        var userOmsuId = connection.ExecuteScalar<int?>(
            "SELECT id_omsu FROM public.users WHERE id_user = @userId",
            new { userId });

        if (!userOmsuId.HasValue)
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
        parameters.Add("userOmsuId", userOmsuId.Value);
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
                    s.date_close,
                    ha.completion_date,
                    ha.csp,
                    ha.id_omsu
                FROM public.surveys s
                INNER JOIN public.history_answer ha
                    ON s.id_survey = ha.id_survey
                WHERE ha.id_omsu = @userOmsuId

                UNION

                SELECT
                    hs.id_survey,
                    hs.name_survey,
                    hs.description,
                    hs.date_begin AS date_open,
                    hs.date_end AS date_close,
                    ha.completion_date,
                    ha.csp,
                    ha.id_omsu
                FROM public.history_surveys hs
                INNER JOIN public.history_answer ha
                    ON hs.id_survey = ha.id_survey
                WHERE ha.id_omsu = @userOmsuId
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
                    archived.id_omsu
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
            UserOmsuId = userOmsuId.Value,
            CurrentPage = Math.Max(currentPage, 1),
            TotalPages = totalPages,
            TotalCount = totalCount,
            SearchTerm = normalizedSearchTerm,
            DateFrom = normalizedDateFrom,
            DateTo = normalizedDateTo,
            SignedOnly = signedOnly
        };
    }

    public IReadOnlyList<HistorySurvey> GetAdminArchiveSurveys()
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT
                hs.id_survey,
                hs.date_begin,
                hs.date_end,
                hs.name_survey,
                hs.description
            FROM public.history_surveys hs
            ORDER BY id_survey DESC";

        var surveys = connection.Query<HistorySurvey>(sql).ToList();
        AttachArchiveQuestions(connection, surveys);
        return surveys;
    }

    public async Task<int> CopyArchiveSurveyAsync(ArchiveSurveyCopyRequest request)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var transaction = connection.BeginTransaction();

        var archiveSurvey = await connection.QueryFirstOrDefaultAsync<HistorySurvey>(
            @"SELECT
                  id_survey,
                  date_begin,
                  date_end,
                  name_survey,
                  description
              FROM public.history_surveys
              WHERE id_survey = @surveyId",
            new { surveyId = request.SurveyId },
            transaction);

        if (archiveSurvey == null)
        {
            throw new InvalidOperationException("Архивная анкета не найдена.");
        }

        archiveSurvey.Questions = connection.Query<SurveyQuestionItem>(
            @"SELECT
                  question_order AS Id,
                  question_text AS Text
              FROM public.history_survey_questions
              WHERE id_survey = @surveyId
              ORDER BY question_order",
            new { surveyId = request.SurveyId },
            transaction).ToList();

        var newSurveyId = await connection.ExecuteScalarAsync<int>(
            @"INSERT INTO public.surveys
                (name_survey, description, date_create, date_open, date_close)
              VALUES
                (@nameSurvey, @description, @dateCreate, @dateOpen, @dateClose)
              RETURNING id_survey;",
            new
            {
                nameSurvey = archiveSurvey.name_survey,
                description = archiveSurvey.description ?? string.Empty,
                dateCreate = DateTime.Now,
                dateOpen = archiveSurvey.date_begin.Date,
                dateClose = archiveSurvey.date_end.Date
            },
            transaction);

        foreach (var question in archiveSurvey.Questions.OrderBy(q => q.Id))
        {
            await connection.ExecuteAsync(
                @"INSERT INTO public.survey_questions (id_survey, question_order, question_text)
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

    private static void AttachArchiveQuestions(
        IDbConnection connection,
        IEnumerable<HistorySurvey> surveys)
    {
        var surveyList = surveys.ToList();
        if (surveyList.Count == 0)
        {
            return;
        }

        var surveyIds = surveyList.Select(s => s.id_survey).Distinct().ToArray();
        var rows = connection.Query<ArchiveQuestionLookupRow>(
            @"SELECT
                  id_survey AS SurveyId,
                  question_order AS QuestionOrder,
                  question_text AS QuestionText
              FROM public.history_survey_questions
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
            survey.Questions = questionLookup.GetValueOrDefault(survey.id_survey, new List<SurveyQuestionItem>());
        }
    }

    private sealed class ArchiveQuestionLookupRow
    {
        public int SurveyId { get; init; }
        public int QuestionOrder { get; init; }
        public string QuestionText { get; init; } = string.Empty;
    }
}
