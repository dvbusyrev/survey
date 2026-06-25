using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Application.DTO.Configuration;
using Npgsql;

namespace MainProject.Infrastructure.Persistence;

public sealed class AutoCreationConfigRepository : IAutoCreationConfigRepository
{
    public Task<bool> HasCurrentStorageAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction = null,
        CancellationToken cancellationToken = default) =>
        connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT
                to_regclass('public.week_day') IS NOT NULL
                AND to_regclass('public.auto_creation_config') IS NOT NULL
                AND to_regclass('public.survey_auto_creation_config') IS NOT NULL
                AND EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = 'auto_creation_config'
                      AND column_name = 'working_period' AND is_nullable = 'YES'
                )
                AND EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = 'organization_survey'
                      AND column_name = 'date_end' AND is_nullable = 'YES'
                );
            """,
            transaction: transaction,
            cancellationToken: cancellationToken));

    public async Task<AutoCreationConfigRecord> GetOrCreateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int configId,
        int defaultCreationDayId,
        int defaultBeginDayId,
        int? defaultWorkingPeriod,
        bool lockRow,
        CancellationToken cancellationToken = default)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO public.auto_creation_config
                (id_config, id_creation_day, id_begin_day, working_period, is_enabled)
            VALUES (@ConfigId, @CreationDayId, @BeginDayId, @WorkingPeriod, FALSE)
            ON CONFLICT (id_config) DO NOTHING;
            """,
            new { ConfigId = configId, CreationDayId = defaultCreationDayId, BeginDayId = defaultBeginDayId, WorkingPeriod = defaultWorkingPeriod },
            transaction,
            cancellationToken: cancellationToken));

        var lockClause = lockRow ? "FOR UPDATE" : string.Empty;
        return await connection.QuerySingleAsync<AutoCreationConfigRecord>(new CommandDefinition(
            $"""
            SELECT
                c.id_config AS IdConfig,
                c.id_creation_day AS CreationDayId,
                c.id_begin_day AS BeginDayId,
                c.working_period AS WorkingPeriod,
                c.is_enabled AS IsEnabled,
                creation_day.en_name_day AS CreationDayName,
                creation_day.week_number AS CreationWeekNumber,
                begin_day.en_name_day AS BeginDayName,
                begin_day.week_number AS BeginWeekNumber,
                creation_day.week_number::text || '-' || lower(creation_day.en_name_day) AS CreationPattern,
                begin_day.week_number::text || '-' || lower(begin_day.en_name_day) AS StartPattern
            FROM public.auto_creation_config c
            INNER JOIN public.week_day creation_day ON creation_day.id_day = c.id_creation_day
            INNER JOIN public.week_day begin_day ON begin_day.id_day = c.id_begin_day
            WHERE c.id_config = @ConfigId
            {lockClause};
            """,
            new { ConfigId = configId },
            transaction,
            cancellationToken: cancellationToken));
    }

    public Task SetEnabledAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, int configId, bool isEnabled, CancellationToken cancellationToken = default) =>
        connection.ExecuteAsync(new CommandDefinition(
            "UPDATE public.auto_creation_config SET is_enabled = @IsEnabled WHERE id_config = @ConfigId;",
            new { ConfigId = configId, IsEnabled = isEnabled }, transaction, cancellationToken: cancellationToken));

    public async Task SaveAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, int configId, int creationDayId, int beginDayId, int? workingPeriod, bool isEnabled, IReadOnlyCollection<int> surveyIds, CancellationToken cancellationToken = default)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO public.auto_creation_config (id_config, id_creation_day, id_begin_day, working_period, is_enabled)
            VALUES (@ConfigId, @CreationDayId, @BeginDayId, @WorkingPeriod, @IsEnabled)
            ON CONFLICT (id_config) DO UPDATE SET
                id_creation_day = EXCLUDED.id_creation_day,
                id_begin_day = EXCLUDED.id_begin_day,
                working_period = EXCLUDED.working_period,
                is_enabled = EXCLUDED.is_enabled;
            """,
            new { ConfigId = configId, CreationDayId = creationDayId, BeginDayId = beginDayId, WorkingPeriod = workingPeriod, IsEnabled = isEnabled },
            transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM public.survey_auto_creation_config WHERE id_config = @ConfigId;",
            new { ConfigId = configId }, transaction, cancellationToken: cancellationToken));
        foreach (var surveyId in surveyIds)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO public.survey_auto_creation_config (id_config, id_survey) VALUES (@ConfigId, @SurveyId);",
                new { ConfigId = configId, SurveyId = surveyId }, transaction, cancellationToken: cancellationToken));
        }
    }

    public async Task<IReadOnlyList<int>> GetSelectedSurveyIdsAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, int configId, CancellationToken cancellationToken = default)
    {
        var ids = await connection.QueryAsync<int>(new CommandDefinition(
            "SELECT id_survey FROM public.survey_auto_creation_config WHERE id_config = @ConfigId ORDER BY id_survey;",
            new { ConfigId = configId }, transaction, cancellationToken: cancellationToken));
        return ids.ToArray();
    }

    public async Task<IReadOnlyList<SurveySelectionItem>> GetSelectedSurveysAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, int configId, CancellationToken cancellationToken = default)
    {
        var surveys = await connection.QueryAsync<SurveySelectionItem>(new CommandDefinition(
            """
            SELECT s.id_survey AS Id, s.name_survey AS Name
            FROM public.survey_auto_creation_config cs
            INNER JOIN public.survey s ON s.id_survey = cs.id_survey
            WHERE cs.id_config = @ConfigId
            ORDER BY lower(s.name_survey), s.id_survey;
            """,
            new { ConfigId = configId }, transaction, cancellationToken: cancellationToken));
        return surveys.ToArray();
    }

    public Task<int> GetSelectedSurveyCountAsync(NpgsqlConnection connection, int configId, CancellationToken cancellationToken = default) =>
        connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM public.survey_auto_creation_config WHERE id_config = @ConfigId;",
            new { ConfigId = configId }, cancellationToken: cancellationToken));

    public Task<int?> GetWeekDayIdAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, int weekNumber, string dayName, CancellationToken cancellationToken = default) =>
        connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            """
            SELECT id_day FROM public.week_day
            WHERE week_number = @WeekNumber AND lower(en_name_day) = lower(@DayName)
            LIMIT 1;
            """,
            new { WeekNumber = weekNumber, DayName = dayName }, transaction, cancellationToken: cancellationToken));
}
