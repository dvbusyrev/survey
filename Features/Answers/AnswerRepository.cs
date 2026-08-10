using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Application.DTO.Read;
using MainProject.Application.Support;
using MainProject.Application.UseCases.Answers;
using MainProject.Domain.Entities;
using Npgsql;

namespace MainProject.Infrastructure.Persistence;

public sealed class AnswerRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly SurveyRepository _surveyRepository;
    private readonly IClock _clock;

    public AnswerRepository(
        IDbConnectionFactory connectionFactory,
        SurveyRepository surveyRepository,
        IClock clock)
    {
        _connectionFactory = connectionFactory;
        _surveyRepository = surveyRepository;
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

    public async Task<Survey?> GetSurveyAsync(int surveyId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<Survey>(new CommandDefinition(
            """
            SELECT
                s.id_survey,
                s.name_survey,
                s.description,
                ss.date_begin,
                ss.date_end
            FROM public.survey s
            LEFT JOIN public.survey_schedule ss
                ON ss.id_survey = s.id_survey
            WHERE s.id_survey = @SurveyId;
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

    public async Task<IReadOnlyList<AnswerRecord>> GetSurveyAnswersAsync(
        int surveyId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var answers = (await connection.QueryAsync<AnswerRecord>(new CommandDefinition(
            """
            SELECT
                answer.id_answer,
                answer.id_organization_survey AS IdOrganizationSurvey,
                assignment.id_survey,
                assignment.id_organization AS OrganizationId,
                COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name) AS organization_name,
                answer.completion_date,
                answer.csp
            FROM public.answer answer
            INNER JOIN public.organization_survey assignment
                ON assignment.id_organization_survey = answer.id_organization_survey
            INNER JOIN public.organization organization
                ON organization.id_organization = assignment.id_organization
            WHERE assignment.id_survey = @SurveyId
            ORDER BY answer.completion_date DESC;
            """,
            new { SurveyId = surveyId },
            cancellationToken: cancellationToken))).ToList();

        await AttachAnswerItemsAsync(connection, answers, cancellationToken);
        return answers;
    }

    public async Task<AnswerListReadData> GetListAsync(
        AnswerListReadRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var parameters = new DynamicParameters();
        parameters.Add("SelectedOrganizationIds", request.OrganizationIds.ToArray());
        parameters.Add("HasOrganizationFilter", request.OrganizationIds.Count > 0);
        parameters.Add("SelectedSurveyIds", request.SurveyIds.ToArray());
        parameters.Add("HasSurveyFilter", request.SurveyIds.Count > 0);
        parameters.Add("HasDateFilter", request.DateStart.HasValue && request.DateEnd.HasValue);
        parameters.Add("DateStart", request.DateStart);
        parameters.Add("DateEnd", request.DateEnd);
        parameters.Add("Today", _clock.Today.Date);

        var organizationOptions = BuildSelectionOptions(await connection.QueryAsync<SelectionOption>(new CommandDefinition(
            """
            SELECT DISTINCT
                assignment.id_organization AS Id,
                COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name, 'Нет данных') AS Name
            FROM public.answer answer
            INNER JOIN public.organization_survey assignment
                ON assignment.id_organization_survey = answer.id_organization_survey
            LEFT JOIN public.organization organization
                ON organization.id_organization = assignment.id_organization;
            """,
            cancellationToken: cancellationToken)));
        var surveyOptions = BuildSelectionOptions(await connection.QueryAsync<SelectionOption>(new CommandDefinition(
            """
            SELECT DISTINCT
                assignment.id_survey AS Id,
                COALESCE(survey.name_survey, 'Нет данных') AS Name
            FROM public.answer answer
            INNER JOIN public.organization_survey assignment
                ON assignment.id_organization_survey = answer.id_organization_survey
            LEFT JOIN public.survey survey
                ON survey.id_survey = assignment.id_survey;
            """,
            cancellationToken: cancellationToken)));

        var totalCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"{AnswerRowsCte} SELECT COUNT(*) FROM answer_rows WHERE {AnswerFilterPredicate};",
            parameters,
            cancellationToken: cancellationToken));

        var pageWindow = AppListPaging.CreateWindow(totalCount, request.CurrentPage, request.PageSize);
        parameters.Add("PageSize", pageWindow.PageSize);
        parameters.Add("Offset", pageWindow.Offset);
        var rows = (await connection.QueryAsync<AnswerListReadRow>(new CommandDefinition(
            $"""
            {AnswerRowsCte}
            SELECT
                id_answer AS IdAnswer,
                id_organization AS IdOrganization,
                id_survey AS IdSurvey,
                organization_name AS OrganizationName,
                survey_name AS SurveyName,
                completion_date AS CompletionDate,
                is_signed AS IsSigned,
                can_delete AS CanDelete
            FROM answer_rows
            WHERE {AnswerFilterPredicate}
            ORDER BY {BuildOrderBy(request.SortBy, request.SortDirection)}
            LIMIT @PageSize OFFSET @Offset;
            """,
            parameters,
            cancellationToken: cancellationToken))).ToList();

        return new AnswerListReadData(
            totalCount,
            pageWindow.CurrentPage,
            pageWindow.TotalPages,
            pageWindow.PageSize,
            rows,
            organizationOptions,
            surveyOptions);
    }

    public async Task<AnswerDeleteStorageResult> DeleteIfSurveyActiveAsync(
        int answerId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var candidate = await connection.QueryFirstOrDefaultAsync<AnswerDeleteCandidate>(new CommandDefinition(
                """
                SELECT
                    answer.id_answer AS AnswerId,
                    assignment.date_begin <= @Today
                        AND (assignment.date_end IS NULL OR assignment.date_end >= @Today) AS SurveyIsActive
                FROM public.answer answer
                INNER JOIN public.organization_survey assignment
                    ON assignment.id_organization_survey = answer.id_organization_survey
                WHERE answer.id_answer = @AnswerId
                FOR UPDATE OF answer, assignment;
                """,
                new
                {
                    AnswerId = answerId,
                    Today = _clock.Today.Date
                },
                transaction,
                cancellationToken: cancellationToken));

            if (candidate == null)
            {
                await transaction.CommitAsync(cancellationToken);
                return new AnswerDeleteStorageResult();
            }

            if (!candidate.SurveyIsActive)
            {
                await transaction.CommitAsync(cancellationToken);
                return new AnswerDeleteStorageResult
                {
                    Found = true
                };
            }

            var deleted = await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM public.answer WHERE id_answer = @AnswerId;",
                new { AnswerId = answerId },
                transaction,
                cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);

            return new AnswerDeleteStorageResult
            {
                Found = true,
                SurveyIsActive = true,
                Deleted = deleted > 0
            };
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<SurveySignatureReadData> GetSignatureStatusAsync(
        int surveyId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var surveyName = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT name_survey FROM public.survey WHERE id_survey = @SurveyId;",
            new { SurveyId = surveyId },
            cancellationToken: cancellationToken)) ?? "Неизвестная анкета";
        var rows = (await connection.QueryAsync<SurveySignatureReadRow>(new CommandDefinition(
            """
            SELECT
                COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name) AS OrganizationName,
                (answer.completion_date IS NOT NULL) AS IsCompleted,
                (COALESCE(answer.csp, '') <> '') AS IsSigned,
                answer.completion_date AS CompletionDate
            FROM public.organization organization
            INNER JOIN public.organization_survey assignment
                ON assignment.id_organization = organization.id_organization
            LEFT JOIN public.answer answer
                ON answer.id_organization_survey = assignment.id_organization_survey
            WHERE assignment.id_survey = @SurveyId
            ORDER BY
                (answer.completion_date IS NOT NULL) DESC,
                answer.completion_date ASC NULLS LAST,
                COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name);
            """,
            new { SurveyId = surveyId },
            cancellationToken: cancellationToken))).ToList();

        return new SurveySignatureReadData(surveyName, rows);
    }

    public async Task<AnswerStatisticsReadData> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        var byYear = (await connection.QueryAsync<AverageByYearReadRow>(new CommandDefinition(
            """
            SELECT EXTRACT(YEAR FROM answer.completion_date)::int AS Year,
                   AVG(answer_item.rating::double precision) AS AverageRating
            FROM public.answer answer
            INNER JOIN public.answer_item answer_item ON answer_item.id_answer = answer.id_answer
            WHERE answer.completion_date IS NOT NULL AND answer_item.rating IS NOT NULL
            GROUP BY 1 ORDER BY 1;
            """,
            cancellationToken: cancellationToken))).ToList();
        var byQuarter = (await connection.QueryAsync<AverageByQuarterReadRow>(new CommandDefinition(
            """
            SELECT EXTRACT(QUARTER FROM answer.completion_date)::int AS Quarter,
                   AVG(answer_item.rating::double precision) AS AverageRating
            FROM public.answer answer
            INNER JOIN public.answer_item answer_item ON answer_item.id_answer = answer.id_answer
            WHERE answer.completion_date IS NOT NULL AND answer_item.rating IS NOT NULL
            GROUP BY 1 ORDER BY 1;
            """,
            cancellationToken: cancellationToken))).ToList();
        var byOrganization = (await connection.QueryAsync<OrganizationAverageReadRow>(new CommandDefinition(
            """
            SELECT COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name) AS OrganizationName,
                   AVG(answer_item.rating::double precision) AS AverageRating
            FROM public.answer answer
            INNER JOIN public.organization_survey assignment
                ON assignment.id_organization_survey = answer.id_organization_survey
            INNER JOIN public.organization organization
                ON organization.id_organization = assignment.id_organization
            INNER JOIN public.answer_item answer_item ON answer_item.id_answer = answer.id_answer
            WHERE answer.completion_date IS NOT NULL AND answer_item.rating IS NOT NULL
            GROUP BY 1 ORDER BY 1;
            """,
            cancellationToken: cancellationToken))).ToList();

        return new AnswerStatisticsReadData(byYear, byQuarter, byOrganization);
    }

    public async Task<AnswerStorageResult> SubmitAnswerAsync(
        AnswerRecord answerRecord,
        int userId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var items = await BuildNormalizedAnswerItemsAsync(
            connection, transaction, answerRecord.IdSurvey, answerRecord.Answers, cancellationToken);
        var assignmentId = await _surveyRepository.GetAssignmentIdForUpdateAsync(
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

        var assignmentIsActive = await _surveyRepository.IsAssignmentActiveAsync(
            connection,
            transaction,
            assignmentId.Value,
            cancellationToken);
        if (!assignmentIsActive)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AnswerStorageResult
            {
                Found = true,
                SubmissionClosed = true
            };
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

        await RecordAnswerParticipationAsync(
            connection, transaction, answerId, userId, "submitted", cancellationToken);
        await CopyDraftParticipationAsync(
            connection, transaction, assignmentId.Value, answerId, cancellationToken);
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
        int userId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var assignmentId = await _surveyRepository.GetAssignmentIdForUpdateAsync(
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

        await RecordAnswerParticipationAsync(
            connection, transaction, existingAnswer.AnswerId, userId, "edited", cancellationToken);
        await ReplaceAnswerItemsAsync(connection, transaction, existingAnswer.AnswerId, items, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AnswerStorageResult
        {
            Found = true,
            AnswerId = existingAnswer.AnswerId
        };
    }

    public async Task<bool> SaveDraftAsync(
        AnswerRecord answerRecord,
        int userId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var items = await BuildNormalizedAnswerItemsAsync(
            connection, transaction, answerRecord.IdSurvey, answerRecord.Answers, cancellationToken);
        var assignmentId = await _surveyRepository.GetAssignmentIdForUpdateAsync(
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

        await RecordDraftParticipationAsync(
            connection, transaction, draftId, userId, "saved", cancellationToken);
        await ReplaceDraftItemsAsync(connection, transaction, draftId, items, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> TrySaveAnswerSignatureAsync(
        int surveyId,
        int organizationId,
        string signature,
        byte[]? signedContent,
        int userId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
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
            transaction,
            cancellationToken: cancellationToken));

        if (affected > 0)
        {
            var answerId = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                """
                SELECT answer.id_answer
                FROM public.answer answer
                INNER JOIN public.organization_survey assignment
                    ON assignment.id_organization_survey = answer.id_organization_survey
                WHERE assignment.id_organization = @OrganizationId
                  AND assignment.id_survey = @SurveyId;
                """,
                new { SurveyId = surveyId, OrganizationId = organizationId },
                transaction,
                cancellationToken: cancellationToken));
            await RecordAnswerParticipationAsync(
                connection, transaction, answerId, userId, "signed", cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return affected > 0;
    }

    public async Task<bool> TrySaveDraftSignatureAsync(
        int surveyId,
        int organizationId,
        string signature,
        byte[]? signedContent,
        int userId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
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
            transaction,
            cancellationToken: cancellationToken));

        if (affected > 0)
        {
            var draftId = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                """
                SELECT draft.id_answer_draft
                FROM public.answer_draft draft
                INNER JOIN public.organization_survey assignment
                    ON assignment.id_organization_survey = draft.id_organization_survey
                WHERE assignment.id_organization = @OrganizationId
                  AND assignment.id_survey = @SurveyId;
                """,
                new { SurveyId = surveyId, OrganizationId = organizationId },
                transaction,
                cancellationToken: cancellationToken));
            await RecordDraftParticipationAsync(
                connection, transaction, draftId, userId, "signed", cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
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
                Comment = item.Rating == 5 || string.IsNullOrWhiteSpace(item.Comment)
                    ? null
                    : item.Comment.Trim()
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

    private static Task RecordAnswerParticipationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int answerId,
        int userId,
        string participationType,
        CancellationToken cancellationToken)
    {
        return connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO public.answer_participant (id_answer, id_user, participation_type)
            VALUES (@AnswerId, @UserId, @ParticipationType)
            ON CONFLICT DO NOTHING;
            """,
            new { AnswerId = answerId, UserId = userId, ParticipationType = participationType },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static Task RecordDraftParticipationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int draftId,
        int userId,
        string participationType,
        CancellationToken cancellationToken)
    {
        return connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO public.answer_draft_participant (id_answer_draft, id_user, participation_type)
            VALUES (@DraftId, @UserId, @ParticipationType)
            ON CONFLICT DO NOTHING;
            """,
            new { DraftId = draftId, UserId = userId, ParticipationType = participationType },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static Task CopyDraftParticipationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int assignmentId,
        int answerId,
        CancellationToken cancellationToken)
    {
        return connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO public.answer_participant (id_answer, id_user, participation_type)
            SELECT
                @AnswerId,
                participant.id_user,
                CASE WHEN participant.participation_type = 'signed' THEN 'signed' ELSE 'submitted' END
            FROM public.answer_draft_participant participant
            INNER JOIN public.answer_draft draft
                ON draft.id_answer_draft = participant.id_answer_draft
            WHERE draft.id_organization_survey = @AssignmentId
            ON CONFLICT DO NOTHING;
            """,
            new { AnswerId = answerId, AssignmentId = assignmentId },
            transaction,
            cancellationToken: cancellationToken));
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

    private static IReadOnlyList<SelectionOption> BuildSelectionOptions(IEnumerable<SelectionOption> options)
    {
        return options
            .Where(option => option.Id > 0 && !string.IsNullOrWhiteSpace(option.Name))
            .GroupBy(option => option.Id)
            .Select(group => group.First())
            .OrderBy(option => option.Name, AppListPaging.RuStringComparer)
            .ThenBy(option => option.Id)
            .ToList();
    }

    private static string BuildOrderBy(string sortBy, string sortDirection)
    {
        var direction = string.Equals(sortDirection, "desc", StringComparison.Ordinal) ? "DESC" : "ASC";
        return sortBy switch
        {
            AnswerReadSortFields.Organization => $"organization_name {direction}, id_answer DESC",
            AnswerReadSortFields.Survey => $"survey_name {direction}, id_answer DESC",
            AnswerReadSortFields.Signed => $"is_signed {direction}, id_answer DESC",
            _ => $"completion_date {direction} NULLS LAST, id_answer DESC"
        };
    }

    private const string AnswerFilterPredicate = """
        (@HasOrganizationFilter = false OR id_organization = ANY(@SelectedOrganizationIds))
        AND (@HasSurveyFilter = false OR id_survey = ANY(@SelectedSurveyIds))
        AND (@HasDateFilter = false OR (completion_date IS NOT NULL AND completion_date >= @DateStart AND completion_date <= @DateEnd))
        """;

    private const string AnswerRowsCte = """
        WITH answer_rows AS (
            SELECT
                answer.id_answer,
                assignment.id_organization,
                assignment.id_survey,
                COALESCE(NULLIF(organization.organization_short_name, ''), organization.organization_name, 'Нет данных') AS organization_name,
                COALESCE(survey.name_survey, 'Нет данных') AS survey_name,
                answer.completion_date,
                (COALESCE(answer.csp, '') <> '') AS is_signed,
                assignment.date_begin <= @Today
                    AND (assignment.date_end IS NULL OR assignment.date_end >= @Today) AS can_delete
            FROM public.answer answer
            INNER JOIN public.organization_survey assignment
                ON assignment.id_organization_survey = answer.id_organization_survey
            LEFT JOIN public.organization organization ON organization.id_organization = assignment.id_organization
            LEFT JOIN public.survey survey ON survey.id_survey = assignment.id_survey
        )
        """;

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

    private sealed class AnswerDeleteCandidate
    {
        public int AnswerId { get; init; }
        public bool SurveyIsActive { get; init; }
    }
}
