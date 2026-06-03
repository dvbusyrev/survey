using Dapper;
using MainProject.Application.DTO;
using MainProject.Application.UseCases.Surveys;
using MainProject.Infrastructure.Persistence;
using MainProject.Domain.Entities;

namespace MainProject.Application.UseCases.Answers;

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
            "SELECT id_organization FROM public.app_user WHERE id_user = @userId",
            new { userId });
    }

    public bool IsSurveyAssignedToOrganization(int surveyId, int organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();

        return connection.ExecuteScalar<bool>(
            @"SELECT EXISTS (
                  SELECT 1
                  FROM public.organization_survey os
                  WHERE os.id_survey = @surveyId
                    AND os.id_organization = @organizationId
                    AND os.date_begin <= CURRENT_DATE
                    AND (os.date_end IS NULL OR os.date_end >= CURRENT_DATE)
              )",
            new { surveyId, organizationId });
    }

    public bool AnswerRecordExists(int surveyId, int organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();

        return connection.ExecuteScalar<bool>(
            @"SELECT EXISTS (
                  SELECT 1
                  FROM public.answer a
                  INNER JOIN public.organization_survey os
                      ON os.id_organization_survey = a.id_organization_survey
                  WHERE os.id_survey = @surveyId
                    AND os.id_organization = @organizationId
              )",
            new { surveyId, organizationId });
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

    public AnswerRecord? GetAnswerRecord(int surveyId, int organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();

        var answerRecord = connection.QueryFirstOrDefault<AnswerRecord>(
            @"SELECT
                  a.id_answer,
                  a.id_organization_survey AS IdOrganizationSurvey,
                  os.id_organization AS OrganizationId,
                  os.id_survey,
                  a.completion_date,
                  a.csp,
                  a.signed_content AS SignedContent
              FROM public.answer a
              INNER JOIN public.organization_survey os
                  ON os.id_organization_survey = a.id_organization_survey
              WHERE os.id_survey = @surveyId
                AND os.id_organization = @organizationId",
            new { surveyId, organizationId });

        if (answerRecord == null)
        {
            return null;
        }

        AttachAnswerItems(connection, new[] { answerRecord });
        return answerRecord;
    }

    public IReadOnlyList<AnswerRecord> GetAnswerRecords(int surveyId, int? organizationId = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        if (organizationId.HasValue)
        {
            var answers = connection.Query<AnswerRecord>(
                @"SELECT
                      ha.id_answer,
                      ha.id_organization_survey AS IdOrganizationSurvey,
                      os.id_organization AS OrganizationId,
                      os.id_survey,
                      ha.csp,
                      ha.signed_content AS SignedContent,
                      ha.completion_date,
                      COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name) AS organization_name
                  FROM public.answer ha
                  INNER JOIN public.organization_survey os
                      ON os.id_organization_survey = ha.id_organization_survey
                  LEFT JOIN public.organization o
                      ON o.id_organization = os.id_organization
                  WHERE os.id_survey = @surveyId
                    AND os.id_organization = @organizationId
                  ORDER BY ha.completion_date DESC",
                new { surveyId, organizationId }).ToList();

            AttachAnswerItems(connection, answers);
            return answers;
        }

        var allAnswers = connection.Query<AnswerRecord>(
            @"SELECT
                  ha.id_answer,
                  ha.id_organization_survey AS IdOrganizationSurvey,
                  os.id_organization AS OrganizationId,
                  os.id_survey,
                  ha.csp,
                  ha.signed_content AS SignedContent,
                  ha.completion_date,
                  COALESCE(NULLIF(o.organization_short_name, ''), o.organization_name) AS organization_name
              FROM public.answer ha
              INNER JOIN public.organization_survey os
                  ON os.id_organization_survey = ha.id_organization_survey
              LEFT JOIN public.organization o
                  ON o.id_organization = os.id_organization
              WHERE os.id_survey = @surveyId
              ORDER BY ha.completion_date DESC",
            new { surveyId }).ToList();

        AttachAnswerItems(connection, allAnswers);
        return allAnswers;
    }

    public int InsertAnswerRecord(AnswerRecord answerRecord)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var transaction = connection.BeginTransaction();

        var items = BuildNormalizedAnswerItems(connection, transaction, answerRecord.IdSurvey, answerRecord.Answers);

        var assignmentId = GetAssignmentIdForUpdate(
            connection,
            transaction,
            answerRecord.IdSurvey,
            answerRecord.OrganizationId);

        if (!assignmentId.HasValue)
        {
            transaction.Rollback();
            throw new InvalidOperationException("Назначение анкеты для организации не найдено.");
        }

        var existingSignature = connection.ExecuteScalar<string?>(
            @"SELECT csp
              FROM public.answer
              WHERE id_organization_survey = @assignmentId
              FOR UPDATE",
            new
            {
                assignmentId = assignmentId.Value
            },
            transaction);

        if (!string.IsNullOrWhiteSpace(existingSignature))
        {
            transaction.Rollback();
            throw new AnswerAlreadySignedException();
        }

        var idAnswer = connection.ExecuteScalar<int>(
            @"INSERT INTO public.answer (
                  id_organization_survey,
                  completion_date
              )
              VALUES (
                  @assignmentId,
                  @completionDate
              )
              ON CONFLICT (id_organization_survey) DO UPDATE
              SET completion_date = EXCLUDED.completion_date,
                  csp = NULL,
                  signed_content = NULL
              RETURNING id_answer",
            new
            {
                assignmentId = assignmentId.Value,
                completionDate = DateTime.Now
            },
            transaction);

        ReplaceAnswerItems(connection, transaction, idAnswer, items);
        transaction.Commit();

        return idAnswer;
    }

    public bool UpdateAnswerRecord(AnswerRecord answerRecord)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var transaction = connection.BeginTransaction();

        var assignmentId = GetAssignmentIdForUpdate(
            connection,
            transaction,
            answerRecord.IdSurvey,
            answerRecord.OrganizationId);

        if (!assignmentId.HasValue)
        {
            transaction.Rollback();
            return false;
        }

        var existingAnswer = connection.QueryFirstOrDefault<ExistingAnswerRow>(
            @"SELECT id_answer AS IdAnswer, csp AS Csp
              FROM public.answer
              WHERE id_organization_survey = @assignmentId
              FOR UPDATE",
            new
            {
                assignmentId = assignmentId.Value
            },
            transaction);

        if (existingAnswer == null)
        {
            transaction.Rollback();
            return false;
        }

        if (!string.IsNullOrWhiteSpace(existingAnswer.Csp))
        {
            transaction.Rollback();
            throw new AnswerAlreadySignedException();
        }

        var items = BuildNormalizedAnswerItems(connection, transaction, answerRecord.IdSurvey, answerRecord.Answers);

        var rowsAffected = connection.Execute(
            @"UPDATE public.answer
              SET completion_date = @completionDate,
                  csp = NULL,
                  signed_content = NULL
              WHERE id_answer = @answerId",
            new
            {
                answerId = existingAnswer.IdAnswer,
                completionDate = DateTime.Now
            },
            transaction);

        if (rowsAffected == 0)
        {
            transaction.Rollback();
            return false;
        }

        ReplaceAnswerItems(connection, transaction, existingAnswer.IdAnswer, items);
        transaction.Commit();

        return true;
    }

    public bool UpdateSignature(int surveyId, int organizationId, string signature, byte[]? signedContent)
    {
        using var connection = _connectionFactory.CreateConnection();

        var rowsAffected = connection.Execute(
            @"UPDATE public.answer a
              SET csp = @signature,
                  signed_content = @signedContent
              FROM public.organization_survey os
              WHERE os.id_organization_survey = a.id_organization_survey
                AND os.id_organization = @organizationId
                AND os.id_survey = @surveyId
                AND COALESCE(BTRIM(a.csp), '') = ''",
            new { signature, signedContent, organizationId, surveyId });

        return rowsAffected > 0;
    }

    private static IReadOnlyList<AnswerItemRow> BuildNormalizedAnswerItems(
        global::Npgsql.NpgsqlConnection connection,
        global::Npgsql.NpgsqlTransaction transaction,
        int surveyId,
        IReadOnlyList<AnswerPayloadItem>? answers)
    {
        var parsedItems = answers ?? Array.Empty<AnswerPayloadItem>();
        if (parsedItems.Count == 0)
        {
            return Array.Empty<AnswerItemRow>();
        }

        var questionLookup = connection.Query<SurveyQuestionRow>(
            @"SELECT question_order AS QuestionOrder, question_text AS QuestionText
              FROM public.survey_question
              WHERE id_survey = @surveyId
              ORDER BY question_order",
            new { surveyId },
            transaction)
            .ToDictionary(q => q.QuestionOrder, q => q.QuestionText);

        var normalizedItems = new List<AnswerItemRow>();
        foreach (var item in parsedItems)
        {
            var questionOrder = ParseQuestionOrder(item.QuestionId, normalizedItems.Count + 1);
            var questionText = !string.IsNullOrWhiteSpace(item.DisplayQuestion)
                ? item.DisplayQuestion.Trim()
                : questionLookup.GetValueOrDefault(questionOrder, $"Вопрос {questionOrder}");

            normalizedItems.Add(new AnswerItemRow
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
        IEnumerable<AnswerRecord> answers)
    {
        var answerList = answers.ToList();
        if (answerList.Count == 0)
        {
            return;
        }

        var answerIds = answerList.Select(a => a.IdAnswer).Distinct().ToArray();
        var rows = connection.Query<AnswerItemLookupRow>(
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

    private static void ReplaceAnswerItems(
        global::Npgsql.NpgsqlConnection connection,
        global::Npgsql.NpgsqlTransaction transaction,
        int answerId,
        IReadOnlyList<AnswerItemRow> items)
    {
        connection.Execute(
            "DELETE FROM public.answer_item WHERE id_answer = @answerId",
            new { answerId },
            transaction);

        foreach (var item in items)
        {
            connection.Execute(
                @"INSERT INTO public.answer_item (id_answer, question_order, question_text, rating, comment)
                  VALUES (@answerId, @questionOrder, @questionText, @rating, @comment)
                  ON CONFLICT (id_answer, question_order) DO UPDATE
                  SET question_text = EXCLUDED.question_text,
                      rating = EXCLUDED.rating,
                      comment = EXCLUDED.comment",
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

    private static int? GetAssignmentIdForUpdate(
        global::Npgsql.NpgsqlConnection connection,
        global::Npgsql.NpgsqlTransaction transaction,
        int surveyId,
        int organizationId)
    {
        return connection.ExecuteScalar<int?>(
            @"SELECT id_organization_survey
              FROM public.organization_survey
              WHERE id_organization = @organizationId
                AND id_survey = @surveyId
              FOR UPDATE",
            new
            {
                organizationId,
                surveyId
            },
            transaction);
    }

    private static int ParseQuestionOrder(string? rawQuestionId, int fallbackOrder)
    {
        return int.TryParse(rawQuestionId, out var parsedQuestionId) && parsedQuestionId > 0
            ? parsedQuestionId
            : fallbackOrder;
    }

    private sealed class AnswerItemRow
    {
        public int QuestionOrder { get; init; }
        public string QuestionText { get; init; } = string.Empty;
        public int? Rating { get; init; }
        public string? Comment { get; init; }
    }

    private sealed class AnswerItemLookupRow
    {
        public int AnswerId { get; init; }
        public int QuestionOrder { get; init; }
        public string QuestionText { get; init; } = string.Empty;
        public int? Rating { get; init; }
        public string? Comment { get; init; }
    }

    private sealed class ExistingAnswerRow
    {
        public int IdAnswer { get; init; }
        public string? Csp { get; init; }
    }
}
