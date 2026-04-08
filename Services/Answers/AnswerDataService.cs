using Dapper;
using MainProject.Infrastructure.Database;
using MainProject.Models;
using MainProject.Services.Surveys;

namespace MainProject.Services.Answers;

public sealed class AnswerDataService
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AnswerDataService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public int? GetUserOrganizationId(int userId)
    {
        using var connection = _connectionFactory.CreateConnection();

        return connection.ExecuteScalar<int?>(
            "SELECT organization_id FROM public.app_user WHERE id_user = @userId",
            new { userId });
    }

    public Survey? GetSurveyInfo(int surveyId)
    {
        using var connection = _connectionFactory.CreateConnection();

        return connection.QueryFirstOrDefault<Survey>(
            @"SELECT
                  id_survey,
                  name_survey,
                  description
              FROM public.survey
              WHERE id_survey = @surveyId",
            new { surveyId });
    }

    public IReadOnlyList<SurveyQuestionItem> GetSurveyQuestions(int surveyId)
    {
        using var connection = _connectionFactory.CreateConnection();

        return connection.Query<SurveyQuestionItem>(
            @"SELECT
                  question_order AS Id,
                  question_text AS Text
              FROM public.survey_question
              WHERE id_survey = @surveyId
              ORDER BY question_order",
            new { surveyId }).ToList();
    }

    public HistoryAnswer? GetHistoryAnswer(int surveyId, int organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var historyAnswer = connection.QueryFirstOrDefault<HistoryAnswer>(
            @"SELECT
                  id_answer,
                  organization_id,
                  id_survey,
                  completion_date,
                  create_date_survey,
                  csp
              FROM public.answer
              WHERE id_survey = @surveyId
                AND organization_id = @organizationId",
            new { surveyId, organizationId });

        if (historyAnswer == null)
        {
            return null;
        }

        AttachAnswerItems(connection, new[] { historyAnswer });
        return historyAnswer;
    }

    public IReadOnlyList<HistoryAnswer> GetHistoryAnswers(int surveyId, int? organizationId = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        if (organizationId.HasValue)
        {
            var answers = connection.Query<HistoryAnswer>(
                @"SELECT
                      ha.id_answer,
                      ha.organization_id,
                      ha.id_survey,
                      ha.csp,
                      ha.completion_date,
                      ha.create_date_survey,
                      o.organization_name
                  FROM public.answer ha
                  LEFT JOIN public.organization o
                      ON o.organization_id = ha.organization_id
                  WHERE ha.id_survey = @surveyId
                    AND ha.organization_id = @organizationId
                  ORDER BY ha.completion_date DESC",
                new { surveyId, organizationId }).ToList();

            AttachAnswerItems(connection, answers);
            return answers;
        }

        var allAnswers = connection.Query<HistoryAnswer>(
            @"SELECT
                  ha.id_answer,
                  ha.organization_id,
                  ha.id_survey,
                  ha.csp,
                  ha.completion_date,
                  ha.create_date_survey,
                  o.organization_name
              FROM public.answer ha
              LEFT JOIN public.organization o
                  ON o.organization_id = ha.organization_id
              WHERE ha.id_survey = @surveyId
              ORDER BY ha.completion_date DESC",
            new { surveyId }).ToList();

        AttachAnswerItems(connection, allAnswers);
        return allAnswers;
    }

    public int InsertHistoryAnswer(HistoryAnswer historyAnswerData)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var transaction = connection.BeginTransaction();

        var items = BuildNormalizedAnswerItems(connection, historyAnswerData.IdSurvey, historyAnswerData.Answers);

        var idAnswer = connection.ExecuteScalar<int>(
            @"INSERT INTO public.answer (
                  organization_id,
                  id_survey,
                  completion_date,
                  create_date_survey
              )
              VALUES (
                  @idOrganization,
                  @idSurvey,
                  @completionDate,
                  (SELECT date_create FROM public.survey WHERE id_survey = @idSurvey)
              )
              RETURNING id_answer",
            new
            {
                idOrganization = historyAnswerData.OrganizationId,
                idSurvey = historyAnswerData.IdSurvey,
                completionDate = DateTime.Now
            },
            transaction);

        ReplaceHistoryAnswerItems(connection, transaction, idAnswer, items);
        transaction.Commit();

        return idAnswer;
    }

    public bool UpdateHistoryAnswer(HistoryAnswer historyAnswerData)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var transaction = connection.BeginTransaction();

        var answerId = connection.ExecuteScalar<int?>(
            @"SELECT id_answer
              FROM public.answer
              WHERE organization_id = @idOrganization
                AND id_survey = @idSurvey",
            new
            {
                idOrganization = historyAnswerData.OrganizationId,
                idSurvey = historyAnswerData.IdSurvey
            },
            transaction);

        if (!answerId.HasValue)
        {
            transaction.Rollback();
            return false;
        }

        var items = BuildNormalizedAnswerItems(connection, historyAnswerData.IdSurvey, historyAnswerData.Answers);

        var rowsAffected = connection.Execute(
            @"UPDATE public.answer
              SET completion_date = @completionDate
              WHERE organization_id = @idOrganization
                AND id_survey = @idSurvey",
            new
            {
                idOrganization = historyAnswerData.OrganizationId,
                idSurvey = historyAnswerData.IdSurvey,
                completionDate = DateTime.Now
            },
            transaction);

        if (rowsAffected == 0)
        {
            transaction.Rollback();
            return false;
        }

        ReplaceHistoryAnswerItems(connection, transaction, answerId.Value, items);
        transaction.Commit();

        return true;
    }

    public bool UpdateSignature(int surveyId, int organizationId, string signature)
    {
        using var connection = _connectionFactory.CreateConnection();

        var rowsAffected = connection.Execute(
            @"UPDATE public.answer
              SET csp = @signature
              WHERE organization_id = @organizationId
                AND id_survey = @surveyId",
            new { signature, organizationId, surveyId });

        return rowsAffected > 0;
    }

    public void ClearSurveyExtension(int organizationId, int surveyId)
    {
        using var connection = _connectionFactory.CreateConnection();

        connection.Execute(
            @"UPDATE public.organization_survey
              SET extended_until = NULL
              WHERE organization_id = @organizationId
                AND id_survey = @surveyId",
            new { organizationId, surveyId });
    }

    private static IReadOnlyList<HistoryAnswerItemRow> BuildNormalizedAnswerItems(
        global::Npgsql.NpgsqlConnection connection,
        int surveyId,
        IReadOnlyList<AnswerPayloadItem>? answers)
    {
        var parsedItems = answers ?? Array.Empty<AnswerPayloadItem>();
        if (parsedItems.Count == 0)
        {
            return Array.Empty<HistoryAnswerItemRow>();
        }

        var questionLookup = connection.Query<SurveyQuestionRow>(
            @"SELECT question_order AS QuestionOrder, question_text AS QuestionText
              FROM public.survey_question
              WHERE id_survey = @surveyId
              ORDER BY question_order",
            new { surveyId })
            .ToDictionary(q => q.QuestionOrder, q => q.QuestionText);

        var normalizedItems = new List<HistoryAnswerItemRow>();
        foreach (var item in parsedItems)
        {
            var questionOrder = ParseQuestionOrder(item.QuestionId, normalizedItems.Count + 1);
            var questionText = !string.IsNullOrWhiteSpace(item.DisplayQuestion)
                ? item.DisplayQuestion.Trim()
                : questionLookup.GetValueOrDefault(questionOrder, $"Вопрос {questionOrder}");

            normalizedItems.Add(new HistoryAnswerItemRow
            {
                QuestionOrder = questionOrder,
                QuestionText = questionText,
                Rating = item.Rating,
                Comment = string.IsNullOrWhiteSpace(item.Comment) ? null : item.Comment.Trim()
            });
        }

        return normalizedItems
            .OrderBy(i => i.QuestionOrder)
            .ToList();
    }

    private static void AttachAnswerItems(
        global::Npgsql.NpgsqlConnection connection,
        IEnumerable<HistoryAnswer> answers)
    {
        var answerList = answers.ToList();
        if (answerList.Count == 0)
        {
            return;
        }

        var answerIds = answerList.Select(a => a.IdAnswer).Distinct().ToArray();
        var rows = connection.Query<HistoryAnswerItemLookupRow>(
            @"SELECT
                  id_answer AS AnswerId,
                  question_order AS QuestionOrder,
                  question_text AS QuestionText,
                  rating AS Rating,
                  comment AS Comment
              FROM public.answer_item
              WHERE id_answer = ANY(@answerIds)
              ORDER BY id_answer, question_order",
            new { answerIds });

        var answerLookup = rows
            .GroupBy(row => row.AnswerId)
            .ToDictionary(
                group => group.Key,
                group => (List<AnswerPayloadItem>)group
                    .Select(row => new AnswerPayloadItem
                    {
                        QuestionId = row.QuestionOrder.ToString(),
                        QuestionText = row.QuestionText,
                        Rating = row.Rating,
                        Comment = row.Comment
                    })
                    .ToList());

        foreach (var answer in answerList)
        {
            answer.Answers = answerLookup.GetValueOrDefault(answer.IdAnswer, new List<AnswerPayloadItem>());
        }
    }

    private static void ReplaceHistoryAnswerItems(
        global::Npgsql.NpgsqlConnection connection,
        global::Npgsql.NpgsqlTransaction transaction,
        int answerId,
        IReadOnlyList<HistoryAnswerItemRow> items)
    {
        connection.Execute(
            "DELETE FROM public.answer_item WHERE id_answer = @answerId",
            new { answerId },
            transaction);

        foreach (var item in items)
        {
            connection.Execute(
                @"INSERT INTO public.answer_item (id_answer, question_order, question_text, rating, comment)
                  VALUES (@answerId, @questionOrder, @questionText, @rating, @comment)",
                new
                {
                    answerId,
                    questionOrder = item.QuestionOrder,
                    questionText = item.QuestionText,
                    rating = item.Rating,
                    comment = item.Comment
                },
                transaction);
        }
    }

    private static int ParseQuestionOrder(string? rawQuestionId, int fallbackOrder)
    {
        return int.TryParse(rawQuestionId, out var parsedQuestionId) && parsedQuestionId > 0
            ? parsedQuestionId
            : fallbackOrder;
    }

    private sealed class HistoryAnswerItemRow
    {
        public int QuestionOrder { get; init; }
        public string QuestionText { get; init; } = string.Empty;
        public int? Rating { get; init; }
        public string? Comment { get; init; }
    }

    private sealed class HistoryAnswerItemLookupRow
    {
        public int AnswerId { get; init; }
        public int QuestionOrder { get; init; }
        public string QuestionText { get; init; } = string.Empty;
        public int? Rating { get; init; }
        public string? Comment { get; init; }
    }
}
