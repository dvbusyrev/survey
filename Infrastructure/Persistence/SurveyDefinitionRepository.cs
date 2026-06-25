using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Domain.Entities;
using Npgsql;

namespace MainProject.Infrastructure.Persistence;

public sealed class SurveyDefinitionRepository : ISurveyDefinitionRepository
{
    public Task<int> CreateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string name,
        string? description,
        CancellationToken cancellationToken = default) =>
        connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO public.survey (name_survey, description)
            VALUES (@Name, @Description)
            RETURNING id_survey;
            """,
            new { Name = name, Description = description },
            transaction,
            cancellationToken: cancellationToken));

    public Task<int> UpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        string name,
        string? description,
        CancellationToken cancellationToken = default) =>
        connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE public.survey
            SET name_survey = @Name, description = @Description
            WHERE id_survey = @SurveyId;
            """,
            new { SurveyId = surveyId, Name = name, Description = description },
            transaction,
            cancellationToken: cancellationToken));

    public async Task<bool> DeleteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        CancellationToken cancellationToken = default)
    {
        var deletedId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "DELETE FROM public.survey WHERE id_survey = @SurveyId RETURNING id_survey;",
            new { SurveyId = surveyId },
            transaction,
            cancellationToken: cancellationToken));
        return deletedId.HasValue;
    }

    public Task<Survey?> GetByIdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int surveyId,
        CancellationToken cancellationToken = default) =>
        connection.QueryFirstOrDefaultAsync<Survey>(new CommandDefinition(
            """
            SELECT
                id_survey AS IdSurvey,
                name_survey AS NameSurvey,
                description AS Description
            FROM public.survey
            WHERE id_survey = @SurveyId;
            """,
            new { SurveyId = surveyId },
            transaction,
            cancellationToken: cancellationToken));

    public async Task<IReadOnlyList<string>> GetCriteriaAsync(
        NpgsqlConnection connection,
        int surveyId,
        CancellationToken cancellationToken = default)
    {
        var criteria = await connection.QueryAsync<string>(new CommandDefinition(
            """
            SELECT question_text
            FROM public.survey_question
            WHERE id_survey = @SurveyId
            ORDER BY question_order;
            """,
            new { SurveyId = surveyId },
            cancellationToken: cancellationToken));
        return criteria.AsList();
    }

    public async Task AttachQuestionsAsync(
        NpgsqlConnection connection,
        IEnumerable<Survey> surveys,
        CancellationToken cancellationToken = default)
    {
        var surveyList = surveys.ToList();
        if (surveyList.Count == 0)
        {
            return;
        }

        var questionRows = await connection.QueryAsync<SurveyQuestionRow>(new CommandDefinition(
            """
            SELECT
                id_survey AS SurveyId,
                question_order AS QuestionOrder,
                question_text AS QuestionText
            FROM public.survey_question
            WHERE id_survey = ANY(@SurveyIds)
            ORDER BY id_survey, question_order;
            """,
            new { SurveyIds = surveyList.Select(survey => survey.IdSurvey).Distinct().ToArray() },
            cancellationToken: cancellationToken));
        var lookup = questionRows
            .GroupBy(row => row.SurveyId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(row => new SurveyQuestionItem
                {
                    Id = row.QuestionOrder,
                    Text = row.QuestionText
                }).ToList());

        foreach (var survey in surveyList)
        {
            survey.Questions = lookup.GetValueOrDefault(survey.IdSurvey, []);
        }
    }

    public async Task<IReadOnlyList<SurveyQuestionItem>> GetQuestionsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        CancellationToken cancellationToken = default)
    {
        var rows = await connection.QueryAsync<SurveyQuestionItem>(new CommandDefinition(
            """
            SELECT question_order AS Id, question_text AS Text
            FROM public.survey_question
            WHERE id_survey = @SurveyId
            ORDER BY question_order;
            """,
            new { SurveyId = surveyId },
            transaction,
            cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    public async Task ReplaceQuestionsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        IReadOnlyCollection<SurveyQuestionItem> questions,
        CancellationToken cancellationToken = default)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM public.survey_question WHERE id_survey = @SurveyId;",
            new { SurveyId = surveyId },
            transaction,
            cancellationToken: cancellationToken));

        foreach (var question in questions.OrderBy(question => question.Id))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO public.survey_question (id_survey, question_order, question_text)
                VALUES (@SurveyId, @QuestionOrder, @QuestionText);
                """,
                new { SurveyId = surveyId, QuestionOrder = question.Id, QuestionText = question.Text },
                transaction,
                cancellationToken: cancellationToken));
        }
    }

    public Task CopyQuestionsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int sourceSurveyId,
        int targetSurveyId,
        CancellationToken cancellationToken = default) =>
        connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO public.survey_question (id_survey, question_order, question_text)
            SELECT @TargetSurveyId, question_order, question_text
            FROM public.survey_question
            WHERE id_survey = @SourceSurveyId
            ON CONFLICT (id_survey, question_order) DO UPDATE
            SET question_text = EXCLUDED.question_text;
            """,
            new { SourceSurveyId = sourceSurveyId, TargetSurveyId = targetSurveyId },
            transaction,
            cancellationToken: cancellationToken));

    public async Task<IReadOnlyList<SurveySelectionItem>> GetSelectionOptionsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        var surveys = await connection.QueryAsync<SurveySelectionItem>(new CommandDefinition(
            """
            SELECT id_survey AS Id, name_survey AS Name
            FROM public.survey
            ORDER BY lower(name_survey), id_survey;
            """,
            transaction: transaction,
            cancellationToken: cancellationToken));
        return surveys.ToArray();
    }

    public async Task<IReadOnlySet<int>> GetExistingIdsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        IReadOnlyCollection<int> surveyIds,
        CancellationToken cancellationToken = default)
    {
        if (surveyIds.Count == 0)
        {
            return new HashSet<int>();
        }

        var ids = await connection.QueryAsync<int>(new CommandDefinition(
            "SELECT id_survey FROM public.survey WHERE id_survey = ANY(@SurveyIds);",
            new { SurveyIds = surveyIds.ToArray() },
            transaction,
            cancellationToken: cancellationToken));
        return ids.ToHashSet();
    }

    private sealed class SurveyQuestionRow
    {
        public int SurveyId { get; init; }
        public int QuestionOrder { get; init; }
        public string QuestionText { get; init; } = string.Empty;
    }
}
