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

    public async Task<bool> AnswerRecordExistsAsync(
        int surveyId,
        int organizationId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
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
            new { SurveyId = surveyId, OrganizationId = organizationId },
            cancellationToken: cancellationToken));
    }

    public async Task<Survey?> GetSurveyInfoAsync(int surveyId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<Survey>(new CommandDefinition(
            """
            SELECT
                id_survey AS IdSurvey,
                name_survey AS NameSurvey,
                description AS Description
            FROM public.survey
            WHERE id_survey = @SurveyId;
            """,
            new { SurveyId = surveyId },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<SurveyQuestionItem>> GetSurveyQuestionsAsync(
        int surveyId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<SurveyQuestionItem>(new CommandDefinition(
            """
            SELECT
                question_order AS Id,
                question_text AS Text
            FROM public.survey_question
            WHERE id_survey = @SurveyId
            ORDER BY question_order;
            """,
            new { SurveyId = surveyId },
            cancellationToken: cancellationToken))).ToList();
    }

    public async Task<AnswerRecord?> GetAnswerRecordAsync(
        int surveyId,
        int organizationId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var answer = await connection.QueryFirstOrDefaultAsync<AnswerRecord>(new CommandDefinition(
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
            new { SurveyId = surveyId, OrganizationId = organizationId },
            cancellationToken: cancellationToken));

        if (answer == null)
        {
            return null;
        }

        await AttachAnswerItemsAsync(connection, [answer], cancellationToken);
        return answer;
    }

    public async Task<AnswerRecord?> GetDraftRecordAsync(
        int surveyId,
        int organizationId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var draft = await connection.QueryFirstOrDefaultAsync<AnswerRecord>(new CommandDefinition(
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
            new { SurveyId = surveyId, OrganizationId = organizationId },
            cancellationToken: cancellationToken));

        if (draft == null)
        {
            return null;
        }

        await AttachDraftItemsAsync(connection, [draft], cancellationToken);
        return draft;
    }

    public async Task<IReadOnlyList<AnswerRecord>> GetAnswerRecordsAsync(
        int surveyId,
        int? organizationId = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var answers = (await connection.QueryAsync<AnswerRecord>(new CommandDefinition(
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
            new { SurveyId = surveyId, OrganizationId = organizationId },
            cancellationToken: cancellationToken))).ToList();

        await AttachAnswerItemsAsync(connection, answers, cancellationToken);
        return answers;
    }

    public async Task<AnswerStorageResult> SubmitAnswerAsync(
        AnswerRecord answerRecord,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var items = await BuildNormalizedAnswerItemsAsync(
            connection, transaction, answerRecord.IdSurvey, answerRecord.Answers, cancellationToken);
        var assignmentId = await _assignmentRepository.GetAssignmentIdForUpdateAsync(
            connection,
            transaction,
            answerRecord.IdSurvey,
            answerRecord.OrganizationId,
            cancellationToken);
        if (!assignmentId.HasValue)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AnswerStorageResult();
        }

        var existingSignature = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            """
            SELECT csp
            FROM public.answer
            WHERE id_organization_survey = @AssignmentId
            FOR UPDATE;
            """,
            new { AssignmentId = assignmentId.Value },
            transaction,
            cancellationToken: cancellationToken));
        if (!string.IsNullOrWhiteSpace(existingSignature))
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AnswerStorageResult
            {
                Found = true,
                AlreadySigned = true
            };
        }

        var answerId = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
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
            transaction,
            cancellationToken: cancellationToken));

        await ReplaceAnswerItemsAsync(connection, transaction, answerId, items, cancellationToken);
        await DeleteDraftAsync(connection, transaction, answerRecord.IdSurvey, answerRecord.OrganizationId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AnswerStorageResult
        {
            Found = true,
            AnswerId = answerId
        };
    }

    public async Task<AnswerStorageResult> UpdateAnswerAsync(
        AnswerRecord answerRecord,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var assignmentId = await _assignmentRepository.GetAssignmentIdForUpdateAsync(
            connection,
            transaction,
            answerRecord.IdSurvey,
            answerRecord.OrganizationId,
            cancellationToken);
        if (!assignmentId.HasValue)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AnswerStorageResult();
        }

        var existingAnswer = await connection.QueryFirstOrDefaultAsync<ExistingAnswerRow>(new CommandDefinition(
            """
            SELECT id_answer AS AnswerId, csp AS Csp
            FROM public.answer
            WHERE id_organization_survey = @AssignmentId
            FOR UPDATE;
            """,
            new { AssignmentId = assignmentId.Value },
            transaction,
            cancellationToken: cancellationToken));
        if (existingAnswer == null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AnswerStorageResult();
        }

        if (!string.IsNullOrWhiteSpace(existingAnswer.Csp))
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AnswerStorageResult
            {
                Found = true,
                AlreadySigned = true
            };
        }

        var items = await BuildNormalizedAnswerItemsAsync(
            connection, transaction, answerRecord.IdSurvey, answerRecord.Answers, cancellationToken);
        var updated = await connection.ExecuteAsync(new CommandDefinition(
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
            transaction,
            cancellationToken: cancellationToken));
        if (updated == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AnswerStorageResult();
        }

        await ReplaceAnswerItemsAsync(connection, transaction, existingAnswer.AnswerId, items, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AnswerStorageResult
        {
            Found = true,
            AnswerId = existingAnswer.AnswerId
        };
    }

    public async Task<bool> SaveDraftAsync(AnswerRecord answerRecord, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var items = await BuildNormalizedAnswerItemsAsync(
            connection, transaction, answerRecord.IdSurvey, answerRecord.Answers, cancellationToken);
        var assignmentId = await _assignmentRepository.GetAssignmentIdForUpdateAsync(
            connection,
            transaction,
            answerRecord.IdSurvey,
            answerRecord.OrganizationId,
            cancellationToken);
        if (!assignmentId.HasValue)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        var draftId = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
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
            transaction,
            cancellationToken: cancellationToken));

        await ReplaceDraftItemsAsync(connection, transaction, draftId, items, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> TrySaveAnswerSignatureAsync(
        int surveyId,
        int organizationId,
        string signature,
        byte[]? signedContent,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
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
            },
            cancellationToken: cancellationToken));

        return affected > 0;
    }

    public async Task<bool> TrySaveDraftSignatureAsync(
        int surveyId,
        int organizationId,
        string signature,
        byte[]? signedContent,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
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
            },
            cancellationToken: cancellationToken));

        return affected > 0;
    }

    public async Task DeleteDraftAsync(int surveyId, int organizationId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await DeleteDraftAsync(connection, null, surveyId, organizationId, cancellationToken);
    }

    private static async Task<IReadOnlyList<AnswerItemRow>> BuildNormalizedAnswerItemsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        IReadOnlyList<AnswerPayloadItem>? answers,
        CancellationToken cancellationToken)
    {
        var parsedItems = answers ?? Array.Empty<AnswerPayloadItem>();
        if (parsedItems.Count == 0)
        {
            return Array.Empty<AnswerItemRow>();
        }

        var questionLookup = (await connection.QueryAsync<QuestionRow>(new CommandDefinition(
            """
            SELECT
                question_order AS QuestionOrder,
                question_text AS QuestionText
            FROM public.survey_question
            WHERE id_survey = @SurveyId
            ORDER BY question_order;
            """,
            new { SurveyId = surveyId },
            transaction,
            cancellationToken: cancellationToken)))
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

    private static async Task AttachAnswerItemsAsync(
        NpgsqlConnection connection,
        IEnumerable<AnswerRecord> answers,
        CancellationToken cancellationToken)
    {
        var answerList = answers.ToList();
        if (answerList.Count == 0)
        {
            return;
        }

        var answerIds = answerList.Select(answer => answer.IdAnswer).Distinct().ToArray();
        var rows = await connection.QueryAsync<AnswerItemLookupRow>(new CommandDefinition(
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
            new { AnswerIds = answerIds },
            cancellationToken: cancellationToken));
        AttachItems(answerList, rows);
    }

    private static async Task AttachDraftItemsAsync(
        NpgsqlConnection connection,
        IEnumerable<AnswerRecord> drafts,
        CancellationToken cancellationToken)
    {
        var draftList = drafts.ToList();
        if (draftList.Count == 0)
        {
            return;
        }

        var draftIds = draftList.Select(draft => draft.IdAnswer).Distinct().ToArray();
        var rows = await connection.QueryAsync<AnswerItemLookupRow>(new CommandDefinition(
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
            new { DraftIds = draftIds },
            cancellationToken: cancellationToken));
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

    private static async Task ReplaceAnswerItemsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int answerId,
        IReadOnlyList<AnswerItemRow> items,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM public.answer_item WHERE id_answer = @AnswerId;",
            new { AnswerId = answerId },
            transaction,
            cancellationToken: cancellationToken));

        foreach (var item in items)
        {
            await connection.ExecuteAsync(new CommandDefinition(
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
                transaction,
                cancellationToken: cancellationToken));
        }
    }

    private static async Task ReplaceDraftItemsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int draftId,
        IReadOnlyList<AnswerItemRow> items,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM public.answer_draft_item WHERE id_answer_draft = @DraftId;",
            new { DraftId = draftId },
            transaction,
            cancellationToken: cancellationToken));

        foreach (var item in items)
        {
            await connection.ExecuteAsync(new CommandDefinition(
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
                transaction,
                cancellationToken: cancellationToken));
        }
    }

    private static Task DeleteDraftAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int surveyId,
        int organizationId,
        CancellationToken cancellationToken)
    {
        return connection.ExecuteAsync(new CommandDefinition(
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
            transaction,
            cancellationToken: cancellationToken));
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
