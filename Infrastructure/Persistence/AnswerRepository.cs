using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Application.UseCases.Answers;
using MainProject.Domain.Entities;
using Npgsql;

namespace MainProject.Infrastructure.Persistence;

public sealed class AnswerRepository : IAnswerRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ISurveyAssignmentRepository _assignmentRepository;
    private readonly IClock _clock;

    public AnswerRepository(
        IDbConnectionFactory connectionFactory,
        ISurveyAssignmentRepository assignmentRepository,
        IClock clock)
    {
        _connectionFactory = connectionFactory;
        _assignmentRepository = assignmentRepository;
        _clock = clock;
    }

    public bool AnswerRecordExists(int surveyId, int organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return connection.ExecuteScalar<bool>(
            """
            SELECT EXISTS (
                SELECT 1
                FROM public.answer answer
                INNER JOIN public.organization_survey assignment
                    ON assignment.id_organization_survey = answer.id_organization_survey
                WHERE assignment.id_survey = @SurveyId
                  AND assignment.id_organization = @OrganizationId
            );
            """,
            new
            {
                SurveyId = surveyId,
                OrganizationId = organizationId
            });
    }

    public Survey? GetSurveyInfo(int surveyId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return connection.QueryFirstOrDefault<Survey>(
            """
            SELECT
                id_survey AS IdSurvey,
                name_survey AS NameSurvey,
                description AS Description
            FROM public.survey
            WHERE id_survey = @SurveyId;
            """,
            new { SurveyId = surveyId });
    }

    public IReadOnlyList<SurveyQuestionItem> GetSurveyQuestions(int surveyId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return connection.Query<SurveyQuestionItem>(
            """
            SELECT
                question_order AS Id,
                question_text AS Text
            FROM public.survey_question
            WHERE id_survey = @SurveyId
            ORDER BY question_order;
            """,
            new { SurveyId = surveyId }).ToList();
    }

    public AnswerRecord? GetAnswerRecord(int surveyId, int organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var answer = connection.QueryFirstOrDefault<AnswerRecord>(
            """
            SELECT
                answer.id_answer AS IdAnswer,
                answer.id_organization_survey AS IdOrganizationSurvey,
                assignment.id_organization AS OrganizationId,
                assignment.id_survey AS IdSurvey,
                answer.completion_date AS CompletionDate,
                answer.csp AS Csp,
                answer.signed_content AS SignedContent
            FROM public.answer answer
            INNER JOIN public.organization_survey assignment
                ON assignment.id_organization_survey = answer.id_organization_survey
            WHERE assignment.id_survey = @SurveyId
              AND assignment.id_organization = @OrganizationId;
            """,
            new
            {
                SurveyId = surveyId,
                OrganizationId = organizationId
            });

        if (answer == null)
        {
            return null;
        }

        AttachAnswerItems(connection, [answer]);
        return answer;
    }

    public AnswerRecord? GetDraftRecord(int surveyId, int organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var draft = connection.QueryFirstOrDefault<AnswerRecord>(
            """
            SELECT
                draft.id_answer_draft AS IdAnswer,
                draft.id_organization_survey AS IdOrganizationSurvey,
                assignment.id_organization AS OrganizationId,
                assignment.id_survey AS IdSurvey,
                draft.draft_date AS CompletionDate,
                draft.csp AS Csp,
                draft.signed_content AS SignedContent
            FROM public.answer_draft draft
            INNER JOIN public.organization_survey assignment
                ON assignment.id_organization_survey = draft.id_organization_survey
            WHERE assignment.id_survey = @SurveyId
              AND assignment.id_organization = @OrganizationId;
            """,
            new
            {
                SurveyId = surveyId,
                OrganizationId = organizationId
            });

        if (draft == null)
        {
            return null;
        }

        AttachDraftItems(connection, [draft]);
        return draft;
    }

    public IReadOnlyList<AnswerRecord> GetAnswerRecords(int surveyId, int? organizationId = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        var answers = connection.Query<AnswerRecord>(
            """
            SELECT
                answer.id_answer AS IdAnswer,
                answer.id_organization_survey AS IdOrganizationSurvey,
                assignment.id_organization AS OrganizationId,
                assignment.id_survey AS IdSurvey,
                answer.csp AS Csp,
                answer.signed_content AS SignedContent,
                answer.completion_date AS CompletionDate,
                COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name) AS OrganizationName
            FROM public.answer answer
            INNER JOIN public.organization_survey assignment
                ON assignment.id_organization_survey = answer.id_organization_survey
            LEFT JOIN public.organization organization
                ON organization.id_organization = assignment.id_organization
            WHERE assignment.id_survey = @SurveyId
              AND (@OrganizationId IS NULL OR assignment.id_organization = @OrganizationId)
            ORDER BY answer.completion_date DESC;
            """,
            new
            {
                SurveyId = surveyId,
                OrganizationId = organizationId
            }).ToList();

        AttachAnswerItems(connection, answers);
        return answers;
    }

    public AnswerStorageResult SubmitAnswer(AnswerRecord answerRecord)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var transaction = connection.BeginTransaction();
        var items = BuildNormalizedAnswerItems(connection, transaction, answerRecord.IdSurvey, answerRecord.Answers);
        var assignmentId = _assignmentRepository.GetAssignmentIdForUpdate(
            connection,
            transaction,
            answerRecord.IdSurvey,
            answerRecord.OrganizationId);
        if (!assignmentId.HasValue)
        {
            transaction.Rollback();
            return new AnswerStorageResult();
        }

        var existingSignature = connection.ExecuteScalar<string?>(
            """
            SELECT csp
            FROM public.answer
            WHERE id_organization_survey = @AssignmentId
            FOR UPDATE;
            """,
            new { AssignmentId = assignmentId.Value },
            transaction);
        if (!string.IsNullOrWhiteSpace(existingSignature))
        {
            transaction.Rollback();
            return new AnswerStorageResult
            {
                Found = true,
                AlreadySigned = true
            };
        }

        var answerId = connection.ExecuteScalar<int>(
            """
            INSERT INTO public.answer (
                id_organization_survey,
                completion_date,
                csp,
                signed_content
            )
            VALUES (
                @AssignmentId,
                @CompletionDate,
                @Signature,
                @SignedContent
            )
            ON CONFLICT (id_organization_survey) DO UPDATE
            SET completion_date = EXCLUDED.completion_date,
                csp = EXCLUDED.csp,
                signed_content = EXCLUDED.signed_content
            RETURNING id_answer;
            """,
            new
            {
                AssignmentId = assignmentId.Value,
                CompletionDate = _clock.Now,
                Signature = string.IsNullOrWhiteSpace(answerRecord.Csp) ? null : answerRecord.Csp,
                SignedContent = answerRecord.SignedContent
            },
            transaction);

        ReplaceAnswerItems(connection, transaction, answerId, items);
        DeleteDraft(connection, transaction, answerRecord.IdSurvey, answerRecord.OrganizationId);
        transaction.Commit();

        return new AnswerStorageResult
        {
            Found = true,
            AnswerId = answerId
        };
    }

    public AnswerStorageResult UpdateAnswer(AnswerRecord answerRecord)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var transaction = connection.BeginTransaction();
        var assignmentId = _assignmentRepository.GetAssignmentIdForUpdate(
            connection,
            transaction,
            answerRecord.IdSurvey,
            answerRecord.OrganizationId);
        if (!assignmentId.HasValue)
        {
            transaction.Rollback();
            return new AnswerStorageResult();
        }

        var existingAnswer = connection.QueryFirstOrDefault<ExistingAnswerRow>(
            """
            SELECT id_answer AS AnswerId, csp AS Csp
            FROM public.answer
            WHERE id_organization_survey = @AssignmentId
            FOR UPDATE;
            """,
            new { AssignmentId = assignmentId.Value },
            transaction);
        if (existingAnswer == null)
        {
            transaction.Rollback();
            return new AnswerStorageResult();
        }

        if (!string.IsNullOrWhiteSpace(existingAnswer.Csp))
        {
            transaction.Rollback();
            return new AnswerStorageResult
            {
                Found = true,
                AlreadySigned = true
            };
        }

        var items = BuildNormalizedAnswerItems(connection, transaction, answerRecord.IdSurvey, answerRecord.Answers);
        var updated = connection.Execute(
            """
            UPDATE public.answer
            SET completion_date = @CompletionDate,
                csp = NULL,
                signed_content = NULL
            WHERE id_answer = @AnswerId;
            """,
            new
            {
                AnswerId = existingAnswer.AnswerId,
                CompletionDate = _clock.Now
            },
            transaction);
        if (updated == 0)
        {
            transaction.Rollback();
            return new AnswerStorageResult();
        }

        ReplaceAnswerItems(connection, transaction, existingAnswer.AnswerId, items);
        transaction.Commit();

        return new AnswerStorageResult
        {
            Found = true,
            AnswerId = existingAnswer.AnswerId
        };
    }

    public bool SaveDraft(AnswerRecord answerRecord)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var transaction = connection.BeginTransaction();
        var items = BuildNormalizedAnswerItems(connection, transaction, answerRecord.IdSurvey, answerRecord.Answers);
        var assignmentId = _assignmentRepository.GetAssignmentIdForUpdate(
            connection,
            transaction,
            answerRecord.IdSurvey,
            answerRecord.OrganizationId);
        if (!assignmentId.HasValue)
        {
            transaction.Rollback();
            return false;
        }

        var draftId = connection.ExecuteScalar<int>(
            """
            INSERT INTO public.answer_draft (
                id_organization_survey,
                draft_date,
                csp,
                signed_content
            )
            VALUES (
                @AssignmentId,
                @DraftDate,
                NULL,
                NULL
            )
            ON CONFLICT (id_organization_survey) DO UPDATE
            SET draft_date = EXCLUDED.draft_date,
                csp = NULL,
                signed_content = NULL
            RETURNING id_answer_draft;
            """,
            new
            {
                AssignmentId = assignmentId.Value,
                DraftDate = _clock.Now
            },
            transaction);

        ReplaceDraftItems(connection, transaction, draftId, items);
        transaction.Commit();
        return true;
    }

    public bool TrySaveAnswerSignature(int surveyId, int organizationId, string signature, byte[]? signedContent)
    {
        using var connection = _connectionFactory.CreateConnection();
        var affected = connection.Execute(
            """
            UPDATE public.answer answer
            SET csp = @Signature,
                signed_content = @SignedContent
            FROM public.organization_survey assignment
            WHERE assignment.id_organization_survey = answer.id_organization_survey
              AND assignment.id_organization = @OrganizationId
              AND assignment.id_survey = @SurveyId
              AND COALESCE(BTRIM(answer.csp), '') = '';
            """,
            new
            {
                SurveyId = surveyId,
                OrganizationId = organizationId,
                Signature = signature,
                SignedContent = signedContent
            });

        return affected > 0;
    }

    public bool TrySaveDraftSignature(int surveyId, int organizationId, string signature, byte[]? signedContent)
    {
        using var connection = _connectionFactory.CreateConnection();
        var affected = connection.Execute(
            """
            UPDATE public.answer_draft draft
            SET csp = @Signature,
                signed_content = @SignedContent
            FROM public.organization_survey assignment
            WHERE assignment.id_organization_survey = draft.id_organization_survey
              AND assignment.id_organization = @OrganizationId
              AND assignment.id_survey = @SurveyId
              AND COALESCE(BTRIM(draft.csp), '') = '';
            """,
            new
            {
                SurveyId = surveyId,
                OrganizationId = organizationId,
                Signature = signature,
                SignedContent = signedContent
            });

        return affected > 0;
    }

    public void DeleteDraft(int surveyId, int organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();
        DeleteDraft(connection, null, surveyId, organizationId);
    }

    private static IReadOnlyList<AnswerItemRow> BuildNormalizedAnswerItems(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        IReadOnlyList<AnswerPayloadItem>? answers)
    {
        var parsedItems = answers ?? Array.Empty<AnswerPayloadItem>();
        if (parsedItems.Count == 0)
        {
            return Array.Empty<AnswerItemRow>();
        }

        var questionLookup = connection.Query<QuestionRow>(
            """
            SELECT
                question_order AS QuestionOrder,
                question_text AS QuestionText
            FROM public.survey_question
            WHERE id_survey = @SurveyId
            ORDER BY question_order;
            """,
            new { SurveyId = surveyId },
            transaction)
            .ToDictionary(question => question.QuestionOrder, question => question.QuestionText);

        var normalizedItems = new List<AnswerItemRow>();
        foreach (var item in parsedItems)
        {
            var questionOrder = ParseQuestionOrder(item.QuestionId, normalizedItems.Count + 1);
            normalizedItems.Add(new AnswerItemRow
            {
                QuestionOrder = questionOrder,
                QuestionText = !string.IsNullOrWhiteSpace(item.DisplayQuestion)
                    ? item.DisplayQuestion.Trim()
                    : questionLookup.GetValueOrDefault(questionOrder, $"Вопрос {questionOrder}"),
                Rating = item.Rating,
                Comment = string.IsNullOrWhiteSpace(item.Comment) ? null : item.Comment.Trim()
            });
        }

        return normalizedItems
            .OrderBy(item => item.QuestionOrder)
            .ToList();
    }

    private static void AttachAnswerItems(NpgsqlConnection connection, IEnumerable<AnswerRecord> answers)
    {
        var answerList = answers.ToList();
        if (answerList.Count == 0)
        {
            return;
        }

        var answerIds = answerList.Select(answer => answer.IdAnswer).Distinct().ToArray();
        var rows = connection.Query<AnswerItemLookupRow>(
            """
            SELECT
                id_answer AS AnswerId,
                question_order AS QuestionOrder,
                question_text AS QuestionText,
                rating AS Rating,
                comment AS Comment
            FROM public.answer_item
            WHERE id_answer = ANY(@AnswerIds)
            ORDER BY id_answer, question_order;
            """,
            new { AnswerIds = answerIds });
        AttachItems(answerList, rows);
    }

    private static void AttachDraftItems(NpgsqlConnection connection, IEnumerable<AnswerRecord> drafts)
    {
        var draftList = drafts.ToList();
        if (draftList.Count == 0)
        {
            return;
        }

        var draftIds = draftList.Select(draft => draft.IdAnswer).Distinct().ToArray();
        var rows = connection.Query<AnswerItemLookupRow>(
            """
            SELECT
                id_answer_draft AS AnswerId,
                question_order AS QuestionOrder,
                question_text AS QuestionText,
                rating AS Rating,
                comment AS Comment
            FROM public.answer_draft_item
            WHERE id_answer_draft = ANY(@DraftIds)
            ORDER BY id_answer_draft, question_order;
            """,
            new { DraftIds = draftIds });
        AttachItems(draftList, rows);
    }

    private static void AttachItems(IEnumerable<AnswerRecord> records, IEnumerable<AnswerItemLookupRow> rows)
    {
        var itemLookup = rows
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

        foreach (var record in records)
        {
            record.Answers = itemLookup.GetValueOrDefault(record.IdAnswer, new List<AnswerPayloadItem>());
        }
    }

    private static void ReplaceAnswerItems(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int answerId,
        IReadOnlyList<AnswerItemRow> items)
    {
        connection.Execute(
            "DELETE FROM public.answer_item WHERE id_answer = @AnswerId;",
            new { AnswerId = answerId },
            transaction);

        foreach (var item in items)
        {
            connection.Execute(
                """
                INSERT INTO public.answer_item (id_answer, question_order, question_text, rating, comment)
                VALUES (@AnswerId, @QuestionOrder, @QuestionText, @Rating, @Comment)
                ON CONFLICT (id_answer, question_order) DO UPDATE
                SET question_text = EXCLUDED.question_text,
                    rating = EXCLUDED.rating,
                    comment = EXCLUDED.comment;
                """,
                new
                {
                    AnswerId = answerId,
                    item.QuestionOrder,
                    item.QuestionText,
                    item.Rating,
                    item.Comment
                },
                transaction);
        }
    }

    private static void ReplaceDraftItems(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int draftId,
        IReadOnlyList<AnswerItemRow> items)
    {
        connection.Execute(
            "DELETE FROM public.answer_draft_item WHERE id_answer_draft = @DraftId;",
            new { DraftId = draftId },
            transaction);

        foreach (var item in items)
        {
            connection.Execute(
                """
                INSERT INTO public.answer_draft_item (id_answer_draft, question_order, question_text, rating, comment)
                VALUES (@DraftId, @QuestionOrder, @QuestionText, @Rating, @Comment)
                ON CONFLICT (id_answer_draft, question_order) DO UPDATE
                SET question_text = EXCLUDED.question_text,
                    rating = EXCLUDED.rating,
                    comment = EXCLUDED.comment;
                """,
                new
                {
                    DraftId = draftId,
                    item.QuestionOrder,
                    item.QuestionText,
                    item.Rating,
                    item.Comment
                },
                transaction);
        }
    }

    private static void DeleteDraft(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int surveyId,
        int organizationId)
    {
        connection.Execute(
            """
            DELETE FROM public.answer_draft draft
            USING public.organization_survey assignment
            WHERE assignment.id_organization_survey = draft.id_organization_survey
              AND assignment.id_organization = @OrganizationId
              AND assignment.id_survey = @SurveyId;
            """,
            new
            {
                SurveyId = surveyId,
                OrganizationId = organizationId
            },
            transaction);
    }

    private static int ParseQuestionOrder(string? rawQuestionId, int fallbackOrder)
    {
        return int.TryParse(rawQuestionId, out var questionOrder) && questionOrder > 0
            ? questionOrder
            : fallbackOrder;
    }

    private sealed class QuestionRow
    {
        public int QuestionOrder { get; init; }
        public string QuestionText { get; init; } = string.Empty;
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
        public int AnswerId { get; init; }
        public string? Csp { get; init; }
    }
}
