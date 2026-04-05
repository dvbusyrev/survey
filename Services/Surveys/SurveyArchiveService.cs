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
                id_survey,
                date_begin,
                date_end,
                file_questions,
                name_survey,
                description
            FROM public.history_surveys
            ORDER BY id_survey DESC";

        return connection.Query<HistorySurvey>(sql).ToList();
    }

    public async Task<int> CopyArchiveSurveyAsync(ArchiveSurveyCopyRequest request)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var command = connection.CreateCommand();
        var dateOpen = ParseArchiveDate(request.DateOpen);
        var dateClose = ParseArchiveDate(request.DateClose);

        command.CommandText = @"
            INSERT INTO public.surveys
                (name_survey, description, date_create, date_open, date_close, questions)
            VALUES
                (@name_survey, @description, @date_create, @date_open, @date_close, CAST(@questions AS jsonb))
            RETURNING id_survey;";

        command.Parameters.Add(new Npgsql.NpgsqlParameter("@name_survey", request.NameSurvey.Trim()));
        command.Parameters.Add(new Npgsql.NpgsqlParameter("@description", request.Description ?? string.Empty));
        command.Parameters.Add(new Npgsql.NpgsqlParameter("@date_create", DateTime.Now));
        command.Parameters.Add(new Npgsql.NpgsqlParameter("@date_open", (object?)dateOpen?.Date ?? DBNull.Value));
        command.Parameters.Add(new Npgsql.NpgsqlParameter("@date_close", (object?)dateClose?.Date ?? DBNull.Value));
        command.Parameters.Add(new Npgsql.NpgsqlParameter("@questions", request.Questions ?? "{}"));

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    private static DateTime? ParseArchiveDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var formats = new[] { "dd.MM.yyyy H:mm:ss", "dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy", "yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ss.FFFFFFFK" };
        if (DateTime.TryParseExact(
                value,
                formats,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var parsed))
        {
            return parsed;
        }

        if (DateTime.TryParse(value, out parsed))
        {
            return parsed;
        }

        throw new FormatException($"Неверный формат даты: {value}");
    }
}
