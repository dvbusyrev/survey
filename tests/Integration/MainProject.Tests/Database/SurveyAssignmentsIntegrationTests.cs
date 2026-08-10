using System.Text;
using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Application.DTO.Email;
using MainProject.Application.DTO.Read;
using MainProject.Application.DTO.Theme;
using MainProject.Application.UseCases.Admin;
using MainProject.Application.UseCases.Answers;
using MainProject.Application.UseCases.Surveys;
using MainProject.Application.UseCases;
using MainProject.Domain.Entities;
using MainProject.Infrastructure.External.Email;
using MainProject.Infrastructure.External.Calendar;
using MainProject.Infrastructure.Persistence;
using MainProject.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using Npgsql;

namespace MainProject.Tests.Integration.Database;

[Collection(PostgreSqlIntegrationCollection.Name)]
public sealed class SurveyAssignmentsIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlIntegrationFixture _fixture;
    private readonly TestNpgsqlConnectionFactory _connectionFactory;
    private readonly IClock _clock = new FixedClock(DateTime.Today);
    private readonly SurveyRepository _surveyRepository;
    private readonly AnswerRepository _answerRepository;

    public SurveyAssignmentsIntegrationTests(PostgreSqlIntegrationFixture fixture)
    {
        _fixture = fixture;
        _connectionFactory = new TestNpgsqlConnectionFactory(fixture);
        _surveyRepository = new SurveyRepository(_clock);
        _answerRepository = new AnswerRepository(
            _connectionFactory,
            _surveyRepository,
            new FixedClock(DateTime.Today));
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [RequiresPostgresFact]
    public async Task Migrations_CreateCurrentSchemaWithoutLegacyThemeColumns()
    {
        await using var connection = _fixture.CreateConnection();

        var versions = (await connection.QueryAsync<string>(
            "SELECT version FROM public.schema_migrations ORDER BY version;")).ToArray();
        var themeColumns = (await connection.QueryAsync<string>(
            """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'theme_config';
            """)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("037", versions);
        Assert.Null(await connection.ExecuteScalarAsync<string?>("SELECT to_regclass('public.week_day')::text;"));
        var auditColumnsWithoutGenerator = (await connection.QueryAsync<string>(
            """
            SELECT table_name
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = ANY(@AuditTables)
              AND column_name = 'id_audit'
              AND is_identity = 'NO'
              AND column_default IS NULL;
            """,
            new
            {
                AuditTables = new[]
                {
                    "answer_l",
                    "answer_item_l",
                    "app_user_l",
                    "auto_creation_config_l",
                    "email_config_l",
                    "organization_l",
                    "organization_survey_l",
                    "survey_auto_creation_config_l",
                    "survey_l",
                    "survey_question_l",
                    "theme_config_l"
                }
            })).ToArray();
        Assert.Empty(auditColumnsWithoutGenerator);
        var userOrganizationIsNullable = await connection.ExecuteScalarAsync<string>(
            """
            SELECT is_nullable
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'app_user'
              AND column_name = 'id_organization';
            """);
        var userOrganizationDeleteAction = await connection.ExecuteScalarAsync<string>(
            """
            SELECT delete_rule
            FROM information_schema.referential_constraints
            WHERE constraint_schema = 'public'
              AND constraint_name = 'app_user_id_organization_fkey';
            """);
        var protectedDeleteRules = (await connection.QueryAsync<ConstraintDeleteRule>(
            """
            SELECT constraint_name AS ConstraintName, delete_rule AS DeleteRule
            FROM information_schema.referential_constraints
            WHERE constraint_schema = 'public'
              AND constraint_name = ANY(@ConstraintNames);
            """,
            new
            {
                ConstraintNames = new[]
                {
                    "organization_survey_id_organization_fkey",
                    "organization_survey_id_survey_fkey",
                    "answer_id_organization_survey_fkey",
                    "answer_draft_id_organization_survey_fkey",
                    "answer_participant_id_user_fkey",
                    "answer_draft_participant_id_user_fkey"
                }
            })).ToDictionary(row => row.ConstraintName, row => row.DeleteRule, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("background_image", themeColumns);
        Assert.Equal("NO", userOrganizationIsNullable);
        Assert.Equal("RESTRICT", userOrganizationDeleteAction);
        Assert.Equal(6, protectedDeleteRules.Count);
        Assert.All(protectedDeleteRules.Values, deleteRule => Assert.Equal("RESTRICT", deleteRule));
        Assert.DoesNotContain("gradient_enabled", themeColumns);
        Assert.DoesNotContain("background_image_data_url", themeColumns);
        Assert.DoesNotContain("soft_lighten_percent", themeColumns);
        Assert.DoesNotContain("button_strong_darken_percent", themeColumns);
    }

    [RequiresPostgresFact]
    public async Task Migrations_ReconcileMetadataDefaultsIndexesAndLegacyNames()
    {
        await using var connection = _fixture.CreateConnection();

        var answerColumns = (await connection.QueryAsync<string>(
            """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'answer';
            """)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var autoCreationColumns = (await connection.QueryAsync<string>(
            """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'auto_creation_config';
            """)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var themeDefaults = await connection.QuerySingleAsync<(string FontColor, string BackgroundColor)>(
            """
            SELECT
                MAX(column_default) FILTER (WHERE column_name = 'font_color') AS FontColor,
                MAX(column_default) FILTER (WHERE column_name = 'background_color') AS BackgroundColor
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'theme_config';
            """);
        var legacyObjects = await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM pg_class
            WHERE relnamespace = 'public'::regnamespace
              AND relname IN (
                  'organization_organization_id_seq',
                  'email_template_l_id_audit_seq',
                  'l_survey_auto_creation_config_id_audit_seq',
                  'idx_answer_id_organization_survey',
                  'idx_answer_item_id_answer'
              );
            """);

        Assert.Contains("date_update", answerColumns);
        Assert.Contains("user_update", answerColumns);
        Assert.DoesNotContain("date_update", autoCreationColumns);
        Assert.DoesNotContain("user_update", autoCreationColumns);
        Assert.Contains("#343D4B", themeDefaults.FontColor);
        Assert.Contains("#B2A8FF", themeDefaults.BackgroundColor);
        Assert.Equal(0, legacyObjects);
    }

    [RequiresPostgresFact]
    public async Task CreateSurvey_CreatesQuestionsAssignmentsAndStructuredAuditRows()
    {
        var organizationIds = await CreateOrganizationsAsync(2);
        var result = await CreateSurveyAsync(organizationIds);

        await using var connection = _fixture.CreateConnection();
        var questionCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM public.survey_question WHERE id_survey = @SurveyId;",
            new { SurveyId = result.SurveyId });
        var assignmentCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM public.organization_survey WHERE id_survey = @SurveyId;",
            new { SurveyId = result.SurveyId });
        var auditCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM public.survey_l WHERE id_survey = @SurveyId AND operation = 'INSERT';",
            new { SurveyId = result.SurveyId });

        Assert.True(result.Success);
        Assert.Equal(2, questionCount);
        Assert.Equal(organizationIds.Count, assignmentCount);
        Assert.Equal(1, auditCount);
    }

    [RequiresPostgresFact]
    public async Task AuditLogRepository_MapsCrudSnapshotsChainsAndPagedEvents()
    {
        var organizationIds = await CreateOrganizationsAsync(1);
        var survey = await CreateSurveyAsync(organizationIds);

        await using var connection = _fixture.CreateConnection();
        var auditOrganizationId = await connection.ExecuteScalarAsync<int>(
            """
            INSERT INTO public.organization (organization_name, organization_short_name, date_begin)
            VALUES ('Журнал: исходное имя', 'Журнал', CURRENT_DATE)
            RETURNING id_organization;
            """);
        await connection.ExecuteAsync(
            "UPDATE public.organization SET organization_name = 'Журнал: новое имя' WHERE id_organization = @OrganizationId;",
            new { OrganizationId = auditOrganizationId });
        var updateAuditId = await connection.ExecuteScalarAsync<long>(
            """
            SELECT id_audit
            FROM public.organization_l
            WHERE id_organization = @OrganizationId AND operation = 'UPDATE'
            ORDER BY id_audit DESC
            LIMIT 1;
            """,
            new { OrganizationId = auditOrganizationId });
        var surveyAuditId = await connection.ExecuteScalarAsync<long>(
            """
            SELECT id_audit
            FROM public.survey_l
            WHERE id_survey = @SurveyId AND operation = 'INSERT'
            ORDER BY id_audit DESC
            LIMIT 1;
            """,
            new { SurveyId = survey.SurveyId });
        await connection.ExecuteAsync(
            "DELETE FROM public.organization WHERE id_organization = @OrganizationId;",
            new { OrganizationId = auditOrganizationId });

        var repository = new AuditLogRepository(_connectionFactory);
        var service = new AuditLogService(repository);
        var rawRows = (await repository.GetAllAsync()).Rows;
        var updateRow = Assert.Single(rawRows, row => row.IdAudit == updateAuditId);
        var page = await service.GetLogsPageAsync(1, 1, null, null);
        var nextPage = await service.GetLogsPageAsync(2, 1, null, null);
        var updateDetails = await service.GetLogDetailsAsync(updateAuditId, "organization", 1, 1, null, null);
        var surveyDetails = await service.GetLogDetailsAsync(surveyAuditId, "survey", 1, 1, null, null);

        Assert.NotNull(updateRow.ParentAuditId);
        Assert.True(page.TotalCount >= 3);
        Assert.Single(page.Logs);
        Assert.Single(nextPage.Logs);
        Assert.NotEqual(page.Logs[0].IdLog, nextPage.Logs[0].IdLog);

        var updateExtra = Assert.IsType<JObject>(updateDetails!.ExtraData);
        Assert.Equal("Журнал: исходное имя", updateExtra["old_row_data"]?["organization_name"]?.Value<string>());
        Assert.Equal("Журнал: новое имя", updateExtra["new_row_data"]?["organization_name"]?.Value<string>());

        var surveyExtra = Assert.IsType<JObject>(surveyDetails!.ExtraData);
        var chainItems = Assert.IsType<JArray>(surveyExtra["items"]);
        Assert.Contains(chainItems.OfType<JObject>(), item => item.Value<string>("source_table") == "survey_question");
        Assert.Contains(chainItems.OfType<JObject>(), item => item.Value<string>("source_table") == "organization_survey");
    }

    [RequiresPostgresFact]
    public async Task AuditLogPagination_DeduplicatesBeforeApplyingPageWindow()
    {
        var repository = new AuditLogRepository(_connectionFactory);
        var service = new AuditLogService(repository);
        var initialCount = await repository.GetEventCountAsync();

        await using var connection = _fixture.CreateConnection();
        await connection.ExecuteAsync(
            """
            INSERT INTO public.organization (organization_name, organization_short_name, date_begin)
            SELECT
                'Пагинация журнала ' || item::text,
                'ПЖ ' || item::text,
                CURRENT_DATE
            FROM generate_series(1, 11) AS item;
            """);

        var sourceAuditId = await connection.ExecuteScalarAsync<long>(
            """
            SELECT id_audit
            FROM public.organization_l
            WHERE organization_name = 'Пагинация журнала 1'
              AND operation = 'INSERT'
            ORDER BY id_audit DESC
            LIMIT 1;
            """);
        await connection.ExecuteAsync(
            """
            INSERT INTO public.organization_l
            (
                operation,
                changed_at,
                changed_by_user_id,
                parent_audit_id,
                id_organization,
                organization_name,
                date_begin,
                date_end,
                email,
                organization_short_name
            )
            SELECT
                operation,
                changed_at,
                changed_by_user_id,
                parent_audit_id,
                id_organization,
                organization_name,
                date_begin,
                date_end,
                email,
                organization_short_name
            FROM public.organization_l
            WHERE id_audit = @SourceAuditId;

            INSERT INTO public.organization_l
            (
                operation,
                changed_at,
                changed_by_user_id,
                parent_audit_id,
                id_organization,
                organization_name,
                date_begin,
                date_end,
                email,
                organization_short_name
            )
            SELECT
                operation,
                changed_at,
                changed_by_user_id,
                parent_audit_id,
                id_organization,
                organization_name,
                date_begin,
                date_end,
                email,
                organization_short_name
            FROM public.organization_l
            WHERE id_audit = @SourceAuditId;
            """,
            new { SourceAuditId = sourceAuditId });

        const int pageSize = 10;
        var firstPage = await service.GetLogsPageAsync(1, pageSize, null, null);

        Assert.Equal(initialCount + 11, firstPage.TotalCount);
        for (var pageNumber = 1; pageNumber <= firstPage.TotalPages; pageNumber++)
        {
            var page = pageNumber == 1
                ? firstPage
                : await service.GetLogsPageAsync(pageNumber, pageSize, null, null);
            var expectedCount = pageNumber < firstPage.TotalPages
                ? pageSize
                : Math.Max(1, firstPage.TotalCount - ((pageNumber - 1) * pageSize));

            Assert.Equal(expectedCount, page.Logs.Count);
        }
    }

    [RequiresPostgresFact]
    public async Task ConfigurationAndManagementPersistence_PersistsCurrentSchemaContracts()
    {
        var organizationService = new OrganizationManagementService(_connectionFactory, _clock);
        var userService = new UserManagementService(_connectionFactory, _clock);
        var emailService = new EmailTemplateService(
            _connectionFactory,
            new SmtpEmailSender());
        var themeService = new ThemeSettingsService(
            _connectionFactory,
            NullLogger<ThemeSettingsService>.Instance);

        await using (var seedConnection = _fixture.CreateConnection())
        {
            await seedConnection.ExecuteAsync(
                "INSERT INTO public.email_config (id_config) VALUES (7);");
        }

        var createOrganization = await organizationService.CreateOrganizationAsync(new OrganizationSaveRequest
        {
            Name = "Репозиторий организация",
            ShortName = "Репо",
            Email = "repository@example.test",
            DateBegin = DateTime.Today.ToString("yyyy-MM-dd")
        });
        var organizationId = createOrganization.EntityId!.Value;
        var createUser = await userService.CreateUserAsync(new UserSaveRequest
        {
            OrganizationId = organizationId.ToString(),
            Username = "repository-user",
            FullName = "Пользователь репозитория",
            Role = "user",
            Password = "RepositoryPass1!",
            DateBegin = DateTime.Today.ToString("yyyy-MM-dd")
        });
        await emailService.SaveMessageAsync(new EmailMessageSettings
        {
            To = "recipient@example.test",
            Subject = "Тест",
            Content = "Содержание"
        });
        await emailService.SaveSenderAsync(new EmailSenderSettings
        {
            SmtpHost = "smtp.example.test",
            SmtpPort = 587,
            SmtpEnableSsl = true,
            SmtpUserName = "smtp-user",
            SmtpPassword = "smtp-password",
            FromAddress = "sender@example.test",
            FromDisplayName = "Отправитель"
        });
        await emailService.SaveSenderAsync(new EmailSenderSettings
        {
            SmtpHost = "smtp.example.test",
            SmtpPort = 587,
            SmtpEnableSsl = true,
            SmtpUserName = "smtp-user",
            SmtpPassword = string.Empty,
            FromAddress = "sender@example.test",
            FromDisplayName = "Отправитель"
        });
        await emailService.SaveMessageAsync(new EmailMessageSettings
        {
            To = "recipient@example.test",
            Subject = "Обновлённое письмо",
            Content = "Новое содержание"
        });
        await themeService.SaveAsync(new ThemeSettings
        {
            FontColor = "#343D4B",
            BackgroundColor = "#B2A8FF",
            EffectSnow = true,
            BackgroundImageOpacity = 45,
            HeaderDarkenPercent = 50,
            FooterDarkenPercent = 50,
            ButtonDarkenPercent = 50,
            SurfaceTintOpacityPercent = 50
        });

        var emailMessage = await emailService.GetMessageAsync();
        var emailSender = await emailService.GetSenderAsync();
        var theme = await themeService.GetAsync();
        var organization = await organizationService.GetOrganizationByIdAsync(organizationId);
        await using var connection = _fixture.CreateConnection();
        var storedEmailConfig = await connection.QuerySingleAsync<(int IdConfig, string SmtpPassword)>(
            "SELECT id_config AS IdConfig, smtp_password AS SmtpPassword FROM public.email_config;");
        await connection.ExecuteAsync(
            "UPDATE public.email_config SET smtp_password = 'CfDJ8LegacyPassword';");
        var senderWithLegacyPassword = await emailService.GetSenderAsync();
        var users = (await userService.GetActiveUsersPageAsync(1, "name", "asc")).Users;
        var user = Assert.Single(users, item => item.NameUser == "repository-user");
        var deletion = await userService.DeleteUserAsync(user.IdUser);
        var archiveAfterUserDeletion = await organizationService.ArchiveOrganizationAsync(organizationId);
        var createEmptyOrganization = await organizationService.CreateOrganizationAsync(new OrganizationSaveRequest
        {
            Name = "Организация без истории",
            DateBegin = DateTime.Today.ToString("yyyy-MM-dd")
        });
        var archiveEmptyOrganization = await organizationService.ArchiveOrganizationAsync(createEmptyOrganization.EntityId!.Value);

        Assert.True(createOrganization.Success);
        Assert.True(createUser.Success);
        Assert.Equal("Репозиторий организация", organization!.OrganizationName);
        Assert.Equal(organizationId, user.OrganizationId);
        Assert.Equal("recipient@example.test", emailMessage.To);
        Assert.Equal("Обновлённое письмо", emailMessage.Subject);
        Assert.Equal("Новое содержание", emailMessage.Content);
        Assert.Equal("smtp.example.test", emailSender.SmtpHost);
        Assert.Empty(emailSender.SmtpPassword);
        Assert.Equal(7, storedEmailConfig.IdConfig);
        Assert.Equal("smtp-password", storedEmailConfig.SmtpPassword);
        Assert.Empty(senderWithLegacyPassword.SmtpPassword);
        Assert.Equal("#B2A8FF", theme.BackgroundColor);
        Assert.True(deletion.Success);
        Assert.True(archiveAfterUserDeletion.Success);
        Assert.True(archiveEmptyOrganization.Success);
    }

    [RequiresPostgresFact]
    public async Task DeletionProtection_BlocksReferencedRecordsInServicesAndDatabase()
    {
        var organizationId = Assert.Single(await CreateOrganizationsAsync(1));
        var userId = await CreateUserAsync(organizationId, "deletion-protection-client");
        var survey = await CreateSurveyAsync([organizationId]);
        var surveyId = survey.SurveyId!.Value;

        var workflow = CreateAnswerService(userId);
        var submission = await workflow.InsertAnswerAsync(BuildAnswerRecord(surveyId, organizationId, 4));
        Assert.True(submission.Success);

        await using var connection = _fixture.CreateConnection();
        await connection.OpenAsync();
        var assignmentId = await connection.ExecuteScalarAsync<int>(
            """
            SELECT id_organization_survey
            FROM public.organization_survey
            WHERE id_survey = @SurveyId AND id_organization = @OrganizationId;
            """,
            new { SurveyId = surveyId, OrganizationId = organizationId });
        var participantTypes = (await connection.QueryAsync<string>(
            """
            SELECT participant.participation_type
            FROM public.answer_participant participant
            INNER JOIN public.answer answer ON answer.id_answer = participant.id_answer
            WHERE answer.id_organization_survey = @AssignmentId
              AND participant.id_user = @UserId;
            """,
            new { AssignmentId = assignmentId, UserId = userId })).ToArray();

        var surveyDeletion = await new SurveyService(_connectionFactory, _surveyRepository, _clock)
            .DeleteSurveyAsync(surveyId);
        var userDeletion = await new UserManagementService(_connectionFactory, _clock)
            .DeleteUserAsync(userId);
        var organizationDeletion = await new OrganizationManagementService(_connectionFactory, _clock)
            .ArchiveOrganizationAsync(organizationId);
        var userDeletionHttpResult = await new UserController(new UserManagementService(_connectionFactory, _clock))
            .DeleteUser(userId, CancellationToken.None);

        Assert.False(surveyDeletion.Success);
        Assert.Equal("survey_in_use", surveyDeletion.Code);
        Assert.Contains("Интеграционная анкета", surveyDeletion.Message);
        Assert.Contains("Орг 1", surveyDeletion.Message);
        Assert.Contains("по ней есть ответы", surveyDeletion.Message);

        Assert.False(userDeletion.Success);
        Assert.Contains("submitted", participantTypes);
        Assert.Equal("user_in_use", userDeletion.Code);
        Assert.Contains("Тестовый клиент", userDeletion.Message);
        Assert.Contains("Связанные анкеты", userDeletion.Message);
        Assert.IsType<ConflictObjectResult>(userDeletionHttpResult);

        Assert.False(organizationDeletion.Success);
        Assert.Equal("organization_in_use", organizationDeletion.Code);
        Assert.Contains("Анкеты:", organizationDeletion.Message);
        Assert.Contains("Пользователи:", organizationDeletion.Message);

        var assignmentDeleteException = await Assert.ThrowsAsync<PostgresException>(() => connection.ExecuteAsync(
            "DELETE FROM public.organization_survey WHERE id_organization_survey = @AssignmentId;",
            new { AssignmentId = assignmentId }));
        var surveyDeleteException = await Assert.ThrowsAsync<PostgresException>(() => connection.ExecuteAsync(
            "DELETE FROM public.survey WHERE id_survey = @SurveyId;",
            new { SurveyId = surveyId }));
        var organizationDeleteException = await Assert.ThrowsAsync<PostgresException>(() => connection.ExecuteAsync(
            "DELETE FROM public.organization WHERE id_organization = @OrganizationId;",
            new { OrganizationId = organizationId }));
        var userDeleteException = await Assert.ThrowsAsync<PostgresException>(() => connection.ExecuteAsync(
            "DELETE FROM public.app_user WHERE id_user = @UserId;",
            new { UserId = userId }));

        var protectedDeletionSqlStates = new[]
        {
            PostgresErrorCodes.ForeignKeyViolation,
            PostgresErrorCodes.RestrictViolation
        };
        Assert.Contains(assignmentDeleteException.SqlState, protectedDeletionSqlStates);
        Assert.Contains(surveyDeleteException.SqlState, protectedDeletionSqlStates);
        Assert.Contains(organizationDeleteException.SqlState, protectedDeletionSqlStates);
        Assert.Contains(userDeleteException.SqlState, protectedDeletionSqlStates);
    }

    [RequiresPostgresFact]
    public async Task DeletionHierarchy_AllowsParentsAfterAnswerIsRemoved()
    {
        var organizationId = Assert.Single(await CreateOrganizationsAsync(1));
        var userId = await CreateUserAsync(organizationId, "deletion-hierarchy-client");
        var survey = await CreateSurveyAsync([organizationId]);
        var surveyId = survey.SurveyId!.Value;
        var answerService = CreateAnswerService(userId);
        Assert.True((await answerService.InsertAnswerAsync(BuildAnswerRecord(surveyId, organizationId, 4))).Success);

        var answerPage = await _answerRepository.GetListAsync(new AnswerListReadRequest(
            [],
            [],
            null,
            null,
            AnswerReadSortFields.Date,
            "desc",
            1,
            10));
        var answer = Assert.Single(answerPage.Rows);
        var surveyService = new SurveyService(_connectionFactory, _surveyRepository, _clock);
        var userService = new UserManagementService(_connectionFactory, _clock);
        var organizationService = new OrganizationManagementService(_connectionFactory, _clock);

        Assert.False((await surveyService.DeleteSurveyAsync(surveyId)).Success);
        Assert.False((await userService.DeleteUserAsync(userId)).Success);
        Assert.False((await organizationService.ArchiveOrganizationAsync(organizationId)).Success);

        Assert.True((await answerService.DeleteAnswerAsync(answer.IdAnswer)).Success);
        Assert.True((await surveyService.DeleteSurveyAsync(surveyId)).Success);
        Assert.True((await userService.DeleteUserAsync(userId)).Success);
        Assert.True((await organizationService.ArchiveOrganizationAsync(organizationId)).Success);

        await using var connection = _fixture.CreateConnection();
        Assert.False(await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM public.answer WHERE id_answer = @AnswerId);",
            new { AnswerId = answer.IdAnswer }));
        Assert.False(await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM public.survey WHERE id_survey = @SurveyId);",
            new { SurveyId = surveyId }));
        Assert.False(await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM public.app_user WHERE id_user = @UserId);",
            new { UserId = userId }));
    }

    [RequiresPostgresFact]
    public async Task DeleteUser_AllowsDraftWithoutSubmittedAnswer()
    {
        var organizationId = Assert.Single(await CreateOrganizationsAsync(1));
        var userId = await CreateUserAsync(organizationId, "draft-only-deletion-client");
        var survey = await CreateSurveyAsync([organizationId]);
        var surveyId = survey.SurveyId!.Value;
        var answerService = CreateAnswerService(userId);
        Assert.True((await answerService.SaveDraftAnswerAsync(BuildAnswerRecord(surveyId, organizationId, 4))).Success);

        var deletion = await new UserManagementService(_connectionFactory, _clock).DeleteUserAsync(userId);

        Assert.True(deletion.Success);
        await using var connection = _fixture.CreateConnection();
        Assert.True(await connection.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS (
                SELECT 1
                FROM public.answer_draft draft
                INNER JOIN public.organization_survey assignment
                    ON assignment.id_organization_survey = draft.id_organization_survey
                WHERE assignment.id_survey = @SurveyId
                  AND assignment.id_organization = @OrganizationId
            );
            """,
            new { SurveyId = surveyId, OrganizationId = organizationId }));
        Assert.False(await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM public.answer_draft_participant WHERE id_user = @UserId);",
            new { UserId = userId }));

        var surveyDeletion = await new SurveyService(_connectionFactory, _surveyRepository, _clock)
            .DeleteSurveyAsync(surveyId);

        Assert.True(surveyDeletion.Success);
        Assert.False(await connection.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS (
                SELECT 1
                FROM public.answer_draft draft
                INNER JOIN public.organization_survey assignment
                    ON assignment.id_organization_survey = draft.id_organization_survey
                WHERE assignment.id_survey = @SurveyId
            );
            """,
            new { SurveyId = surveyId }));
    }

    [RequiresPostgresFact]
    public async Task AssignmentRepository_ReplacesAssignmentSetAndSchedule()
    {
        var organizationIds = await CreateOrganizationsAsync(2);
        var survey = await CreateSurveyAsync(organizationIds);

        await using var connection = _fixture.CreateConnection();
        await connection.OpenAsync();
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await _surveyRepository.ReplaceSurveyAssignmentsAsync(
                connection,
                transaction,
                survey.SurveyId!.Value,
                [organizationIds[1]],
                new DateTime(2026, 5, 4),
                new DateTime(2026, 5, 15));
            await transaction.CommitAsync();
        }

        var assignments = (await connection.QueryAsync<AssignmentDateRow>(
            """
            SELECT id_organization AS OrganizationId, date_begin AS DateBegin, date_end AS DateEnd
            FROM public.organization_survey
            WHERE id_survey = @SurveyId;
            """,
            new { SurveyId = survey.SurveyId })).ToArray();

        var assignment = Assert.Single(assignments);
        Assert.Equal(organizationIds[1], assignment.OrganizationId);
        Assert.Equal(new DateTime(2026, 5, 4), assignment.DateBegin);
        Assert.Equal(new DateTime(2026, 5, 15), assignment.DateEnd);
    }

    [RequiresPostgresFact]
    public async Task SurveyExtension_UpdatesExistingAndCreatesNewAssignments()
    {
        var organizationIds = await CreateOrganizationsAsync(2);
        var survey = await CreateSurveyAsync([organizationIds[0]]);
        var extendedUntil = DateTime.Today.AddDays(30);
        var service = new SurveyService(_connectionFactory, _surveyRepository, _clock);

        var result = await service.SaveExtensionsAsync(new SurveyExtensionRequest
        {
            SurveyId = survey.SurveyId!.Value,
            Extensions =
            [
                new SurveyExtensionItemRequest
                {
                    OrganizationId = organizationIds[0],
                    ExtendedUntil = extendedUntil.ToString("yyyy-MM-dd")
                },
                new SurveyExtensionItemRequest
                {
                    OrganizationId = organizationIds[1],
                    ExtendedUntil = extendedUntil.ToString("yyyy-MM-dd")
                }
            ]
        });

        await using var connection = _fixture.CreateConnection();
        var assignments = (await connection.QueryAsync<AssignmentDateRow>(
            """
            SELECT id_organization AS OrganizationId, date_begin AS DateBegin, date_end AS DateEnd
            FROM public.organization_survey
            WHERE id_survey = @SurveyId
            ORDER BY id_organization;
            """,
            new { SurveyId = survey.SurveyId })).ToArray();

        Assert.True(result.Success, result.Message);
        Assert.Equal(2, assignments.Length);
        Assert.Equal(DateTime.Today.AddDays(-1), assignments[0].DateBegin);
        Assert.Equal(DateTime.Today, assignments[1].DateBegin);
        Assert.All(assignments, assignment => Assert.Equal(extendedUntil, assignment.DateEnd));
    }

    [RequiresPostgresFact]
    public async Task AssignmentRepository_SeparatesActiveAndArchivedSurveyPages()
    {
        var organizationIds = await CreateOrganizationsAsync(1);
        var survey = await CreateSurveyAsync(organizationIds);
        var activePage = await new SurveyService(_connectionFactory, _surveyRepository, _clock)
            .GetSurveysPageAsync(1, null, null, null);

        Assert.Contains(activePage.SurveyRows, row => row.IdSurvey == survey.SurveyId);

        var workflow = CreateAnswerService();
        Assert.True((await workflow.InsertAnswerAsync(BuildAnswerRecord(survey.SurveyId!.Value, organizationIds[0], 5))).Success);

        await using (var connection = _fixture.CreateConnection())
        {
            await connection.ExecuteAsync(
                """
                UPDATE public.organization_survey
                SET date_end = CURRENT_DATE - 1
                WHERE id_survey = @SurveyId;
                """,
                new { SurveyId = survey.SurveyId });
        }

        var archivePage = await new SurveyService(_connectionFactory, _surveyRepository, _clock)
            .GetAdminArchivedSurveysPageAsync(1, null, null, null, null, null, null, null, null);
        var activeAfterArchive = await new SurveyService(_connectionFactory, _surveyRepository, _clock)
            .GetSurveysPageAsync(1, null, null, null);

        Assert.DoesNotContain(activeAfterArchive.SurveyRows, row => row.IdSurvey == survey.SurveyId);
        Assert.Contains(archivePage.SurveyRows, row => row.IdSurvey == survey.SurveyId);
    }

    [RequiresPostgresFact]
    public async Task SubmitAnswer_WhenAssignmentExpiredAfterOpening_DoesNotPersistAnswer()
    {
        var organizationIds = await CreateOrganizationsAsync(1);
        var survey = await CreateSurveyAsync(organizationIds);
        var workflow = CreateAnswerService();

        await using (var connection = _fixture.CreateConnection())
        {
            await connection.ExecuteAsync(
                """
                UPDATE public.organization_survey
                SET date_end = CURRENT_DATE - 1
                WHERE id_survey = @SurveyId
                  AND id_organization = @OrganizationId;
                """,
                new { SurveyId = survey.SurveyId, OrganizationId = organizationIds[0] });
        }

        var exception = await Assert.ThrowsAsync<AnswerSubmissionClosedException>(() =>
            workflow.InsertAnswerAsync(BuildAnswerRecord(survey.SurveyId!.Value, organizationIds[0], 5)));

        await using var verificationConnection = _fixture.CreateConnection();
        var answerCount = await verificationConnection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM public.answer answer
            INNER JOIN public.organization_survey assignment
                ON assignment.id_organization_survey = answer.id_organization_survey
            WHERE assignment.id_survey = @SurveyId
              AND assignment.id_organization = @OrganizationId;
            """,
            new { SurveyId = survey.SurveyId, OrganizationId = organizationIds[0] });

        Assert.Equal(AnswerSubmissionClosedException.UserMessage, exception.Message);
        Assert.Equal(0, answerCount);
    }

    [RequiresPostgresFact]
    public async Task ActiveUserSurveyPage_ShowsOnlyUnansweredActiveAssignments()
    {
        var organizationIds = await CreateOrganizationsAsync(1);
        var survey = await CreateSurveyAsync(organizationIds);
        var userId = await CreateUserAsync(organizationIds[0], "active-client");
        var surveyUserService = new SurveyService(
            _connectionFactory,
            _surveyRepository,
            _clock,
            _answerRepository);

        var beforeSubmission = await surveyUserService.GetActiveSurveysPageAsync(userId, 1, "Интеграционная");

        Assert.NotNull(beforeSubmission);
        Assert.Contains(beforeSubmission!.AccessibleSurveys, item => item.IdSurvey == survey.SurveyId);

        var workflow = CreateAnswerService();
        Assert.True((await workflow.InsertAnswerAsync(BuildAnswerRecord(survey.SurveyId!.Value, organizationIds[0], 5))).Success);

        var afterSubmission = await surveyUserService.GetActiveSurveysPageAsync(userId, 1, null);

        Assert.NotNull(afterSubmission);
        Assert.DoesNotContain(afterSubmission!.AccessibleSurveys, item => item.IdSurvey == survey.SurveyId);
    }

    [RequiresPostgresFact]
    public async Task SurveyService_UsesAssignmentRepositoryForEditAndWorkPeriod()
    {
        var organizationIds = await CreateOrganizationsAsync(2);
        var survey = await CreateSurveyAsync(organizationIds);
        var service = new SurveyService(_connectionFactory, _surveyRepository, _clock);

        var editPage = await service.GetSurveyEditPageAsync(survey.SurveyId!.Value);
        var result = await service.UpdateActiveSurveysWorkPeriodAsync(new SurveyWorkPeriodRequest
        {
            DateBegin = DateTime.Today.AddDays(1),
            DateEnd = DateTime.Today.AddDays(10)
        });

        await using var connection = _fixture.CreateConnection();
        var assignments = (await connection.QueryAsync<AssignmentDateRow>(
            """
            SELECT id_organization AS OrganizationId, date_begin AS DateBegin, date_end AS DateEnd
            FROM public.organization_survey
            WHERE id_survey = @SurveyId
            ORDER BY id_organization;
            """,
            new { SurveyId = survey.SurveyId })).ToArray();

        Assert.NotNull(editPage);
        Assert.Equal(organizationIds, editPage!.SelectedOrganizationIds);
        Assert.True(result.Success);
        Assert.All(assignments, row =>
        {
            Assert.Equal(DateTime.Today.AddDays(1).Date, row.DateBegin);
            Assert.Equal(DateTime.Today.AddDays(10).Date, row.DateEnd);
        });
    }

    [RequiresPostgresFact]
    public async Task UpdateSurvey_PersistsAllEditableFields()
    {
        var organizationIds = await CreateOrganizationsAsync(2);
        var survey = await CreateSurveyAsync([organizationIds[0]]);
        var service = new SurveyService(_connectionFactory, _surveyRepository, _clock);
        var startDate = DateTime.Today;
        var endDate = DateTime.Today.AddDays(30);

        var result = await service.UpdateSurveyAsync(survey.SurveyId!.Value, new SurveyUpdateRequest
        {
            Title = "Отредактированная анкета",
            Description = "Новое описание",
            StartDate = startDate,
            EndDate = endDate,
            Organizations = [organizationIds[1]],
            Criteria = ["Новый первый критерий", "Новый второй критерий"]
        });
        var editPage = await service.GetSurveyEditPageAsync(survey.SurveyId.Value);

        Assert.True(result.Success, result.Message);
        Assert.NotNull(editPage);
        Assert.Equal("Отредактированная анкета", editPage!.Survey.NameSurvey);
        Assert.Equal("Новое описание", editPage.Survey.Description);
        Assert.Equal(startDate.Date, editPage.Survey.DateBegin.Date);
        Assert.Equal(endDate.Date, editPage.Survey.DateEnd?.Date);
        Assert.Equal(new[] { organizationIds[1] }, editPage.SelectedOrganizationIds);
        Assert.Equal(new[] { "Новый первый критерий", "Новый второй критерий" }, editPage.Criteria);
    }

    [RequiresPostgresFact]
    public async Task ArchiveCopy_UsesArchivedAssignmentLookup()
    {
        var organizationIds = await CreateOrganizationsAsync(1);
        var survey = await CreateSurveyAsync(organizationIds);
        var workflow = CreateAnswerService();
        Assert.True((await workflow.InsertAnswerAsync(BuildAnswerRecord(survey.SurveyId!.Value, organizationIds[0], 5))).Success);

        await using (var connection = _fixture.CreateConnection())
        {
            await connection.ExecuteAsync(
                "UPDATE public.organization_survey SET date_end = CURRENT_DATE - 1 WHERE id_survey = @SurveyId;",
                new { SurveyId = survey.SurveyId });
        }

        var archiveService = new SurveyService(_connectionFactory, _surveyRepository, _clock);
        var archive = await archiveService.GetAdminArchivedSurveysAsync();
        var copiedSurveyId = await archiveService.CopyArchiveSurveyAsync(new ArchiveSurveyCopyRequest
        {
            SurveyId = survey.SurveyId.Value
        });

        await using var verificationConnection = _fixture.CreateConnection();
        var copiedQuestionCount = await verificationConnection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM public.survey_question WHERE id_survey = @SurveyId;",
            new { SurveyId = copiedSurveyId });

        Assert.Contains(archive, item => item.IdSurvey == survey.SurveyId);
        Assert.Equal(2, copiedQuestionCount);
    }

    [RequiresPostgresFact]
    public async Task Drafts_AreSeparatedByOrganization_AndSubmissionRemovesOnlySubmittedDraft()
    {
        var organizationIds = await CreateOrganizationsAsync(2);
        var survey = await CreateSurveyAsync(organizationIds);
        var workflow = CreateAnswerService();

        Assert.True((await workflow.SaveDraftAnswerAsync(BuildAnswerRecord(survey.SurveyId!.Value, organizationIds[0], 4))).Success);
        Assert.True((await workflow.SaveDraftAnswerAsync(BuildAnswerRecord(survey.SurveyId!.Value, organizationIds[1], 5))).Success);

        var submitted = await workflow.InsertAnswerAsync(BuildAnswerRecord(survey.SurveyId.Value, organizationIds[0], 5));

        await using var connection = _fixture.CreateConnection();
        var remainingDraftOrganizationIds = (await connection.QueryAsync<int>(
            """
            SELECT os.id_organization
            FROM public.answer_draft draft
            INNER JOIN public.organization_survey os ON os.id_organization_survey = draft.id_organization_survey
            WHERE os.id_survey = @SurveyId;
            """,
            new { SurveyId = survey.SurveyId })).ToArray();
        var answerCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM public.answer WHERE id_organization_survey IN (SELECT id_organization_survey FROM public.organization_survey WHERE id_survey = @SurveyId);",
            new { SurveyId = survey.SurveyId });

        Assert.True(submitted.Success);
        Assert.Equal(new[] { organizationIds[1] }, remainingDraftOrganizationIds);
        Assert.Equal(1, answerCount);
    }

    [RequiresPostgresFact]
    public async Task TopRatingComments_AreRemovedFromDraftsAndSubmittedAnswers()
    {
        var organizationIds = await CreateOrganizationsAsync(1);
        var survey = await CreateSurveyAsync(organizationIds);
        var workflow = CreateAnswerService();
        var answerRecord = BuildAnswerRecord(survey.SurveyId!.Value, organizationIds[0], 5);
        answerRecord.Answers.ForEach(answer => answer.Comment = "Комментарий для оценки 5");

        var draftResult = await workflow.SaveDraftAnswerAsync(answerRecord);

        await using var connection = _fixture.CreateConnection();
        var draftCommentCount = await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM public.answer_draft_item item
            INNER JOIN public.answer_draft draft ON draft.id_answer_draft = item.id_answer_draft
            INNER JOIN public.organization_survey assignment
                ON assignment.id_organization_survey = draft.id_organization_survey
            WHERE assignment.id_survey = @SurveyId
              AND assignment.id_organization = @OrganizationId
              AND item.comment IS NOT NULL;
            """,
            new { SurveyId = survey.SurveyId, OrganizationId = organizationIds[0] });

        answerRecord.Answers.ForEach(answer => answer.Comment = "Комментарий для оценки 5");
        var submissionResult = await workflow.InsertAnswerAsync(answerRecord);
        var answerCommentCount = await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM public.answer_item item
            INNER JOIN public.answer answer ON answer.id_answer = item.id_answer
            INNER JOIN public.organization_survey assignment
                ON assignment.id_organization_survey = answer.id_organization_survey
            WHERE assignment.id_survey = @SurveyId
              AND assignment.id_organization = @OrganizationId
              AND item.comment IS NOT NULL;
            """,
            new { SurveyId = survey.SurveyId, OrganizationId = organizationIds[0] });

        Assert.True(draftResult.Success);
        Assert.True(submissionResult.Success);
        Assert.NotNull(submissionResult.Model);
        Assert.All(submissionResult.Model.Answers, answer => Assert.Null(answer.Comment));
        Assert.Equal(0, draftCommentCount);
        Assert.Equal(0, answerCommentCount);
    }

    [RequiresPostgresFact]
    public async Task Signature_CanBeSavedOnlyOnce()
    {
        var organizationIds = await CreateOrganizationsAsync(1);
        var survey = await CreateSurveyAsync(organizationIds);
        var workflow = CreateAnswerService();
        Assert.True((await workflow.InsertAnswerAsync(BuildAnswerRecord(survey.SurveyId!.Value, organizationIds[0], 5))).Success);

        var signing = CreateAnswerService();
        var request = new AnswerSignatureSaveRequest
        {
            Signature = Convert.ToBase64String(Encoding.UTF8.GetBytes("integration-signature"))
        };

        Assert.True(await signing.SaveSignatureAsync(survey.SurveyId.Value, organizationIds[0], request));
        await Assert.ThrowsAsync<AnswerAlreadySignedException>(
            () => signing.SaveSignatureAsync(survey.SurveyId.Value, organizationIds[0], request));
    }

    [RequiresPostgresFact]
    public async Task ConcurrentSignatureAttempts_LeaveExactlyOneSavedSignature()
    {
        var organizationIds = await CreateOrganizationsAsync(1);
        var survey = await CreateSurveyAsync(organizationIds);
        var workflow = CreateAnswerService();
        Assert.True((await workflow.InsertAnswerAsync(BuildAnswerRecord(survey.SurveyId!.Value, organizationIds[0], 5))).Success);

        var signing = CreateAnswerService();
        var request = new AnswerSignatureSaveRequest
        {
            Signature = Convert.ToBase64String(Encoding.UTF8.GetBytes("concurrent-signature"))
        };

        var attempts = await Task.WhenAll(
            TrySaveSignatureAsync(signing, survey.SurveyId.Value, organizationIds[0], request),
            TrySaveSignatureAsync(signing, survey.SurveyId.Value, organizationIds[0], request));

        await using var connection = _fixture.CreateConnection();
        var signatureCount = await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM public.answer answer
            INNER JOIN public.organization_survey assignment
                ON assignment.id_organization_survey = answer.id_organization_survey
            WHERE assignment.id_survey = @SurveyId
              AND assignment.id_organization = @OrganizationId
              AND COALESCE(answer.csp, '') <> '';
            """,
            new
            {
                SurveyId = survey.SurveyId,
                OrganizationId = organizationIds[0]
            });

        Assert.Equal(1, attempts.Count(static result => result));
        Assert.Equal(1, signatureCount);
    }

    [RequiresPostgresFact]
    public async Task SubmittedSurvey_IsReturnedInOrganizationArchive()
    {
        var organizationIds = await CreateOrganizationsAsync(1);
        var survey = await CreateSurveyAsync(organizationIds);
        var workflow = CreateAnswerService();
        Assert.True((await workflow.InsertAnswerAsync(BuildAnswerRecord(survey.SurveyId!.Value, organizationIds[0], 5))).Success);

        await using var connection = _fixture.CreateConnection();
        var userId = await connection.ExecuteScalarAsync<int>(
            """
            INSERT INTO public.app_user (id_organization, login, full_name, role, password, date_begin)
            VALUES (@OrganizationId, 'archive-client', 'Клиент архива', 'user', 'hash', CURRENT_DATE)
            RETURNING id_user;
            """,
            new { OrganizationId = organizationIds[0] });

        var archive = await new SurveyService(_connectionFactory, _surveyRepository, _clock)
            .GetUserArchivePageAsync(userId, 1, null, null, null, null, signedOnly: false);

        Assert.NotNull(archive);
        Assert.Contains(archive!.ArchivedSurveys, item => item.IdSurvey == survey.SurveyId);
    }

    [RequiresPostgresFact]
    public async Task AutoCreation_SavePersistsScheduleAndSelectedTemplate()
    {
        var organizationIds = await CreateOrganizationsAsync(1);
        var survey = await CreateSurveyAsync(organizationIds);
        var autoCreation = new SurveyService(
            _connectionFactory,
            _surveyRepository,
            new FixedClock(new DateTime(2026, 4, 20)),
            logger: NullLogger<SurveyService>.Instance);

        var result = await autoCreation.SaveAsync(new SurveyAutoCreationSettingsRequest
        {
            ReportingPeriod = "quarter",
            ReportingOffsetBusinessDays = 16,
            ActivePeriodBusinessDays = 20,
            SurveyIds = [survey.SurveyId!.Value]
        });

        await using var connection = _fixture.CreateConnection();
        var stored = await connection.QuerySingleAsync<(string ReportingPeriod, int ReportingOffset, int WorkingPeriod, bool IsEnabled)>(
            """
            SELECT
                reporting_period AS ReportingPeriod,
                reporting_offset_business_days AS ReportingOffset,
                working_period AS WorkingPeriod,
                is_enabled AS IsEnabled
            FROM public.auto_creation_config
            WHERE id_config = 1;
            """);
        var selectedSurveyId = await connection.ExecuteScalarAsync<int>(
            "SELECT id_survey FROM public.survey_auto_creation_config WHERE id_config = 1;");

        Assert.True(result.Success);
        Assert.Equal("quarter", stored.ReportingPeriod);
        Assert.Equal(16, stored.ReportingOffset);
        Assert.Equal(20, stored.WorkingPeriod);
        Assert.False(stored.IsEnabled);
        Assert.Equal(survey.SurveyId, selectedSurveyId);
    }

    [RequiresPostgresFact]
    public async Task AutoCreation_SurveyOptionsContainOneLatestTemplatePerName()
    {
        await using var connection = _fixture.CreateConnection();
        var surveyIds = (await connection.QueryAsync<int>(
            """
            INSERT INTO public.survey (name_survey, description)
            VALUES
                ('Повторяющаяся анкета', 'Первая версия'),
                ('  повторяющаяся АНКЕТА  ', 'Последняя версия')
            RETURNING id_survey;
            """)).ToArray();

        var service = new SurveyService(_connectionFactory, _surveyRepository, _clock);
        var options = await service.GetSurveyOptionsAsync();
        var matchingOptions = options
            .Where(static option => string.Equals(
                option.Name.Trim(),
                "Повторяющаяся анкета",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var option = Assert.Single(matchingOptions);
        Assert.Equal(surveyIds.Max(), option.Id);
    }

    [RequiresPostgresFact]
    public async Task AutoCreation_PreviewUsesTheSameBusinessDayScheduleAsRunPending()
    {
        var service = new SurveyService(
            _connectionFactory,
            _surveyRepository,
            new FixedClock(new DateTime(2026, 6, 1)),
            logger: NullLogger<SurveyService>.Instance,
            productionCalendar: CreateWeekdayProductionCalendar());

        var result = await service.GetSchedulePreviewAsync(new SurveyAutoCreationPreviewRequest
        {
            ReportingPeriod = "quarter",
            ReportingOffsetBusinessDays = 5,
            ActivePeriodBusinessDays = 5,
            TargetYear = 2026,
            TargetMonth = 6
        });

        Assert.True(result.Success);
        Assert.Equal("2026-06-17", result.StartDate);
        Assert.Equal("2026-06-23", result.EndDate);
        Assert.Collection(
            result.Periods,
            period =>
            {
                Assert.Equal(6, period.Month);
                Assert.Equal("2026-06-17", period.StartDate);
                Assert.Equal("2026-06-23", period.EndDate);
            },
            period =>
            {
                Assert.Equal(7, period.Month);
                Assert.Equal("2026-07-27", period.StartDate);
                Assert.Equal("2026-07-31", period.EndDate);
            });
    }

    [RequiresPostgresFact]
    public async Task AutoCreation_RunPending_CreatesScheduledCopyOnlyOnce()
    {
        var organizationIds = await CreateOrganizationsAsync(2);
        var survey = await CreateSurveyAsync(organizationIds);
        var autoCreation = new SurveyService(
            _connectionFactory,
            _surveyRepository,
            new FixedClock(new DateTime(2026, 4, 27)),
            logger: NullLogger<SurveyService>.Instance,
            productionCalendar: CreateWeekdayProductionCalendar());

        var startResult = await autoCreation.StartAsync(new SurveyAutoCreationSettingsRequest
        {
            ReportingPeriod = "quarter",
            ReportingOffsetBusinessDays = 5,
            ActivePeriodBusinessDays = 5,
            SurveyIds = [survey.SurveyId!.Value]
        });
        var repeatedRun = await autoCreation.RunPendingAsync();

        await using var connection = _fixture.CreateConnection();
        var copy = await connection.QuerySingleAsync<SurveyCopyRow>(
            """
            SELECT id_survey AS IdSurvey
            FROM public.survey
            WHERE name_survey = 'Интеграционная анкета'
              AND id_survey <> @OriginalSurveyId;
            """,
            new { OriginalSurveyId = survey.SurveyId });
        var copiedQuestionCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM public.survey_question WHERE id_survey = @SurveyId;",
            new { SurveyId = copy.IdSurvey });
        var copiedAssignments = (await connection.QueryAsync<AssignmentDateRow>(
            """
            SELECT id_organization AS OrganizationId, date_begin AS DateBegin, date_end AS DateEnd
            FROM public.organization_survey
            WHERE id_survey = @SurveyId
            ORDER BY id_organization;
            """,
            new { SurveyId = copy.IdSurvey })).ToArray();

        Assert.True(startResult.Success);
        Assert.Equal(2, copiedQuestionCount);
        Assert.Equal(organizationIds, copiedAssignments.Select(static row => row.OrganizationId));
        Assert.All(copiedAssignments, row =>
        {
            Assert.Equal(new DateTime(2026, 4, 24), row.DateBegin);
            Assert.Equal(new DateTime(2026, 4, 30), row.DateEnd);
        });
        Assert.True(repeatedRun.Processed);
        Assert.Equal(0, repeatedRun.CreatedSurveyCount);
    }

    [RequiresPostgresFact]
    public async Task ClockDrivenRepositories_UseInjectedDateInsteadOfDatabaseCurrentDate()
    {
        const string organizationName = "Организация тестовых часов";
        const string userLogin = "clock-driven-user";
        var dateEnd = new DateTime(2026, 1, 15);

        await using var connection = _fixture.CreateConnection();
        var organizationId = await connection.ExecuteScalarAsync<int>(
            """
            INSERT INTO public.organization (organization_name, organization_short_name, date_begin, date_end)
            VALUES (@Name, 'Часы', '2026-01-01', @DateEnd)
            RETURNING id_organization;
            """,
            new { Name = organizationName, DateEnd = dateEnd });
        var userId = await connection.ExecuteScalarAsync<int>(
            """
            INSERT INTO public.app_user (id_organization, login, full_name, role, password, date_begin, date_end)
            VALUES (@OrganizationId, @Login, 'Пользователь часов', 'user', 'hash', '2026-01-01', @DateEnd)
            RETURNING id_user;
            """,
            new { OrganizationId = organizationId, Login = userLogin, DateEnd = dateEnd });
        var surveyId = await connection.ExecuteScalarAsync<int>(
            "INSERT INTO public.survey (name_survey, description) VALUES ('Анкета тестовых часов', '') RETURNING id_survey;");
        await connection.ExecuteAsync(
            """
            INSERT INTO public.organization_survey (id_organization, id_survey, date_begin, date_end)
            VALUES (@OrganizationId, @SurveyId, '2026-01-01', @DateEnd);
            """,
            new { OrganizationId = organizationId, SurveyId = surveyId, DateEnd = dateEnd });

        var beforeEndClock = new FixedClock(dateEnd);
        var afterEndClock = new FixedClock(dateEnd.AddDays(1));

        var activeOrganizations = await new OrganizationManagementService(_connectionFactory, beforeEndClock).GetOrganizationOptionsAsync();
        var archivedOrganizations = await new OrganizationManagementService(_connectionFactory, afterEndClock).GetOrganizationOptionsAsync();
        var activeUsers = (await new UserManagementService(_connectionFactory, beforeEndClock)
            .GetActiveUsersPageAsync(1, "name", "asc")).Users;
        var archivedUsers = (await new UserManagementService(_connectionFactory, afterEndClock)
            .GetActiveUsersPageAsync(1, "name", "asc")).Users;
        var activeSurveys = await new SurveyRepository(beforeEndClock).GetActiveSurveySummariesAsync(connection);
        var archivedSurveys = await new SurveyRepository(afterEndClock).GetActiveSurveySummariesAsync(connection);

        Assert.Contains(activeOrganizations, item => item.Id == organizationId);
        Assert.DoesNotContain(archivedOrganizations, item => item.Id == organizationId);
        Assert.Contains(activeUsers, item => item.IdUser == userId);
        Assert.DoesNotContain(archivedUsers, item => item.IdUser == userId);
        Assert.Contains(activeSurveys, item => item.IdSurvey == surveyId);
        Assert.DoesNotContain(archivedSurveys, item => item.IdSurvey == surveyId);
    }

    [RequiresPostgresFact]
    public async Task AuthAndUserChromeContext_ReturnCurrentUserContext()
    {
        await using var connection = _fixture.CreateConnection();
        var organizationId = await connection.ExecuteScalarAsync<int>(
            """
            INSERT INTO public.organization (organization_name, organization_short_name, date_begin)
            VALUES ('Организация входа', 'Вход', CURRENT_DATE)
            RETURNING id_organization;
            """);
        const string login = "read-auth-user";
        const string password = "integration-password";
        var passwordHash = new PasswordHasher<string>().HashPassword(login, password);
        var userId = await connection.ExecuteScalarAsync<int>(
            """
            INSERT INTO public.app_user (id_organization, login, full_name, role, password, date_begin)
            VALUES (@OrganizationId, @Login, 'Пользователь входа', 'user', @PasswordHash, CURRENT_DATE)
            RETURNING id_user;
            """,
            new { OrganizationId = organizationId, Login = login, PasswordHash = passwordHash });

        var accessStatusService = new UserAccessStatusService(_connectionFactory, _clock);
        var loginResult = await new AuthService(_connectionFactory, accessStatusService).AuthenticateAsync(login, password);
        var chrome = await new UserChromeContextService(
                _connectionFactory,
                new FixedCurrentUserService(userId, login, "user", "Вход"))
            .GetCurrentContextAsync();

        Assert.True(loginResult.Success);
        Assert.Equal(userId, loginResult.UserId);
        Assert.Equal("user", loginResult.Role);
        Assert.Equal("Вход", loginResult.OrganizationName);
        Assert.Equal(userId, chrome.UserId);
        Assert.Equal(login, chrome.UserName);
        Assert.Equal("Вход", chrome.OrganizationName);
    }

    [RequiresPostgresFact]
    public async Task Auth_RejectsArchivedUserAndUserFromArchivedOrganization()
    {
        await using var connection = _fixture.CreateConnection();
        var today = _clock.Today.Date;
        var activeOrganizationId = await connection.ExecuteScalarAsync<int>(
            """
            INSERT INTO public.organization (organization_name, organization_short_name, date_begin)
            VALUES ('Действующая организация входа', 'Действующая', @DateBegin)
            RETURNING id_organization;
            """,
            new { DateBegin = today.AddDays(-10) });
        var archivedOrganizationId = await connection.ExecuteScalarAsync<int>(
            """
            INSERT INTO public.organization (organization_name, organization_short_name, date_begin, date_end)
            VALUES ('Архивная организация входа', 'Архивная', @DateBegin, @DateEnd)
            RETURNING id_organization;
            """,
            new { DateBegin = today.AddDays(-10), DateEnd = today.AddDays(-1) });

        const string password = "blocked-password";
        const string archivedUserLogin = "archived-auth-user";
        const string archivedOrganizationUserLogin = "archived-organization-auth-user";
        var passwordHasher = new PasswordHasher<string>();
        await connection.ExecuteAsync(
            """
            INSERT INTO public.app_user (id_organization, login, full_name, role, password, date_begin, date_end)
            VALUES
                (@ActiveOrganizationId, @ArchivedUserLogin, 'Архивный пользователь', 'user', @ArchivedUserPassword, @DateBegin, @DateEnd),
                (@ArchivedOrganizationId, @ArchivedOrganizationUserLogin, 'Пользователь архивной организации', 'user', @ArchivedOrganizationUserPassword, @DateBegin, NULL);
            """,
            new
            {
                ActiveOrganizationId = activeOrganizationId,
                ArchivedOrganizationId = archivedOrganizationId,
                ArchivedUserLogin = archivedUserLogin,
                ArchivedOrganizationUserLogin = archivedOrganizationUserLogin,
                ArchivedUserPassword = passwordHasher.HashPassword(archivedUserLogin, password),
                ArchivedOrganizationUserPassword = passwordHasher.HashPassword(archivedOrganizationUserLogin, password),
                DateBegin = today.AddDays(-10),
                DateEnd = today.AddDays(-1)
            });

        var accessStatusService = new UserAccessStatusService(_connectionFactory, _clock);
        var authService = new AuthService(_connectionFactory, accessStatusService);

        var archivedUserResult = await authService.AuthenticateAsync(archivedUserLogin, password);
        var archivedOrganizationUserResult = await authService.AuthenticateAsync(archivedOrganizationUserLogin, password);

        Assert.False(archivedUserResult.Success);
        Assert.Equal(StatusCodes.Status403Forbidden, archivedUserResult.StatusCode);
        Assert.Equal(AuthService.BlockedUserMessage, archivedUserResult.ErrorMessage);
        Assert.False(archivedOrganizationUserResult.Success);
        Assert.Equal(StatusCodes.Status403Forbidden, archivedOrganizationUserResult.StatusCode);
        Assert.Equal(AuthService.BlockedUserMessage, archivedOrganizationUserResult.ErrorMessage);
    }

    [RequiresPostgresFact]
    public async Task AnswerAndReportReadRepositories_ReturnSubmittedAnswersAndReportSources()
    {
        var organizationIds = await CreateOrganizationsAsync(1);
        var survey = await CreateSurveyAsync(organizationIds);
        var workflow = CreateAnswerService();
        Assert.True((await workflow.InsertAnswerAsync(BuildAnswerRecord(survey.SurveyId!.Value, organizationIds[0], 4))).Success);

        var answerRepository = _answerRepository;
        var answerPage = await answerRepository.GetListAsync(new AnswerListReadRequest(
            [],
            [],
            null,
            null,
            AnswerReadSortFields.Date,
            "desc",
            1,
            10));
        var answerItems = await answerRepository.GetSurveyAnswersAsync(survey.SurveyId.Value);
        var signatures = await answerRepository.GetSignatureStatusAsync(survey.SurveyId.Value);
        var statistics = await answerRepository.GetStatisticsAsync();

        var reportRepository = new SurveyRepository(_connectionFactory, _clock);
        var reportName = await reportRepository.GetSurveyNameAsync(survey.SurveyId.Value);
        var reportQuestions = await reportRepository.GetSurveyQuestionsAsync(survey.SurveyId.Value);
        var reportAnswers = await reportRepository.GetSurveyAnswersAsync(survey.SurveyId.Value, organizationIds[0]);
        var reportSurveys = await reportRepository.GetSurveysAsync();
        var allReportAnswers = await reportRepository.GetAnswersAsync();

        Assert.Contains(answerPage.Rows, item => item.IdSurvey == survey.SurveyId);
        Assert.Single(answerItems);
        Assert.Equal(2, answerItems[0].Answers.Count);
        Assert.Contains(signatures.Rows, item => item.IsCompleted && item.OrganizationName == "Орг 1");
        Assert.NotEmpty(statistics.ByYear);
        Assert.Equal("Интеграционная анкета", reportName);
        Assert.Equal(2, reportQuestions.Count);
        Assert.Single(reportAnswers);
        Assert.Equal(2, reportAnswers[0].Answers.Count);
        Assert.Contains(reportSurveys, item => item.IdSurvey == survey.SurveyId && item.Questions.Count == 2);
        Assert.Contains(allReportAnswers, item => item.IdSurvey == survey.SurveyId);
    }

    [RequiresPostgresFact]
    public async Task DeleteAnswer_AllowsActiveAssignmentAndRejectsExpiredAssignment()
    {
        var organizationIds = await CreateOrganizationsAsync(2);
        var survey = await CreateSurveyAsync(organizationIds);
        var surveyId = survey.SurveyId!.Value;
        var workflow = CreateAnswerService();
        Assert.True((await workflow.InsertAnswerAsync(BuildAnswerRecord(surveyId, organizationIds[0], 4))).Success);
        Assert.True((await workflow.InsertAnswerAsync(BuildAnswerRecord(surveyId, organizationIds[1], 4))).Success);

        await using var connection = _fixture.CreateConnection();
        await connection.ExecuteAsync(
            """
            UPDATE public.organization_survey
            SET date_end = @DateEnd
            WHERE id_survey = @SurveyId
              AND id_organization = @OrganizationId;
            """,
            new
            {
                SurveyId = surveyId,
                OrganizationId = organizationIds[0],
                DateEnd = _clock.Today.AddDays(-1)
            });

        var answerPage = await _answerRepository.GetListAsync(new AnswerListReadRequest(
            [],
            [],
            null,
            null,
            AnswerReadSortFields.Date,
            "desc",
            1,
            10));
        var expiredAnswer = Assert.Single(answerPage.Rows, row => row.IdOrganization == organizationIds[0]);
        var activeAnswer = Assert.Single(answerPage.Rows, row => row.IdOrganization == organizationIds[1]);

        Assert.False(expiredAnswer.CanDelete);
        Assert.True(activeAnswer.CanDelete);

        var rejected = await workflow.DeleteAnswerAsync(expiredAnswer.IdAnswer);
        var deleted = await workflow.DeleteAnswerAsync(activeAnswer.IdAnswer);

        Assert.False(rejected.Success);
        Assert.Equal("survey_inactive", rejected.Code);
        Assert.Equal("Нельзя удалить ответ: анкета больше не активна.", rejected.Message);
        Assert.True(deleted.Success);
        Assert.Equal("Ответ успешно удалён.", deleted.Message);
        Assert.Equal(1, await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM public.answer WHERE id_answer = ANY(@AnswerIds);",
            new { AnswerIds = new[] { expiredAnswer.IdAnswer, activeAnswer.IdAnswer } }));
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM public.answer_item WHERE id_answer = @AnswerId;",
            new { AnswerId = activeAnswer.IdAnswer }));
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM public.answer_participant WHERE id_answer = @AnswerId;",
            new { AnswerId = activeAnswer.IdAnswer }));
    }

    private AnswerService CreateAnswerService(int? userId = null)
    {
        var participantUserId = userId ?? GetOrCreateAnswerParticipantUserId();
        return new AnswerService(
            _connectionFactory,
            _surveyRepository,
            _answerRepository,
            new FixedCurrentUserService(participantUserId),
            new FixedClock(DateTime.Today));
    }

    private int GetOrCreateAnswerParticipantUserId()
    {
        using var connection = _fixture.CreateConnection();
        connection.Open();
        var existingUserId = connection.ExecuteScalar<int?>(
            "SELECT id_user FROM public.app_user ORDER BY id_user LIMIT 1;");
        if (existingUserId.HasValue)
        {
            return existingUserId.Value;
        }

        var organizationId = connection.ExecuteScalar<int>(
            "SELECT id_organization FROM public.organization ORDER BY id_organization LIMIT 1;");
        return connection.ExecuteScalar<int>(
            """
            INSERT INTO public.app_user (id_organization, login, full_name, role, password, date_begin)
            VALUES (@OrganizationId, 'integration-answer-user', 'Пользователь ответов', 'admin', 'hash', CURRENT_DATE)
            RETURNING id_user;
            """,
            new { OrganizationId = organizationId });
    }

    private static ProductionCalendarService CreateWeekdayProductionCalendar()
    {
        var handler = new StaticCalendarHandler();
        return new ProductionCalendarService(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://calendar.test/")
        });
    }

    private async Task<List<int>> CreateOrganizationsAsync(int count)
    {
        await using var connection = _fixture.CreateConnection();
        var ids = new List<int>(count);
        for (var index = 1; index <= count; index++)
        {
            ids.Add(await connection.ExecuteScalarAsync<int>(
                """
                INSERT INTO public.organization (organization_name, organization_short_name, date_begin)
                VALUES (@Name, @ShortName, CURRENT_DATE)
                RETURNING id_organization;
                """,
                new
                {
                    Name = $"Организация {index}",
                    ShortName = $"Орг {index}"
                }));
        }

        return ids;
    }

    private async Task<int> CreateUserAsync(int organizationId, string login)
    {
        await using var connection = _fixture.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(
            """
            INSERT INTO public.app_user (id_organization, login, full_name, role, password, date_begin)
            VALUES (@OrganizationId, @Login, 'Тестовый клиент', 'user', 'hash', CURRENT_DATE)
            RETURNING id_user;
            """,
            new
            {
                OrganizationId = organizationId,
                Login = login
            });
    }

    private async Task<SurveyCommandResult> CreateSurveyAsync(IReadOnlyList<int> organizationIds)
    {
        var result = await new SurveyService(_connectionFactory, _surveyRepository, _clock).CreateSurveyAsync(new SurveyAddRequest
        {
            Title = "Интеграционная анкета",
            Description = "Проверка сценария назначений",
            StartDate = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd"),
            EndDate = DateTime.Today.AddDays(14).ToString("yyyy-MM-dd"),
            Organizations = organizationIds.ToList(),
            Criteria = ["Первый вопрос", "Второй вопрос"]
        });

        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.SurveyId);
        return result;
    }

    private static AnswerRecord BuildAnswerRecord(int surveyId, int organizationId, int firstRating)
        => new()
        {
            IdSurvey = surveyId,
            OrganizationId = organizationId,
            Answers =
            [
                new AnswerPayloadItem
                {
                    QuestionId = "1",
                    QuestionText = "Первый вопрос",
                    Rating = firstRating,
                    Comment = firstRating == 5 ? null : "Нужен комментарий"
                },
                new AnswerPayloadItem
                {
                    QuestionId = "2",
                    QuestionText = "Второй вопрос",
                    Rating = 5
                }
            ]
        };

    private static async Task<bool> TrySaveSignatureAsync(
        AnswerService signing,
        int surveyId,
        int organizationId,
        AnswerSignatureSaveRequest request)
    {
        try
        {
            return await signing.SaveSignatureAsync(surveyId, organizationId, request);
        }
        catch (AnswerAlreadySignedException)
        {
            return false;
        }
    }

    private sealed class FixedClock(DateTime today) : IClock
    {
        public DateTime Today { get; } = today.Date;
        public DateTime Now { get; } = today;
    }

    private sealed class FixedCurrentUserService : ICurrentUserService
    {
        public FixedCurrentUserService(
            int userId = 1,
            string userName = "Integration test",
            string role = "admin",
            string organizationName = "Test organization")
        {
            UserId = userId;
            UserName = userName;
            Role = role;
            OrganizationName = organizationName;
        }

        public bool IsAuthenticated => true;
        public int? UserId { get; }
        public string UserName { get; }
        public string Role { get; }
        public string OrganizationName { get; }
        public bool IsAdmin => AppRoles.Normalize(Role) == AppRoles.Admin;
    }

    private sealed class SurveyCopyRow
    {
        public int IdSurvey { get; init; }
    }

    private sealed class AssignmentDateRow
    {
        public int OrganizationId { get; init; }
        public DateTime DateBegin { get; init; }
        public DateTime? DateEnd { get; init; }
    }

    private sealed class ConstraintDeleteRule
    {
        public string ConstraintName { get; init; } = string.Empty;
        public string DeleteRule { get; init; } = string.Empty;
    }

    private sealed class StaticCalendarHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var yearParameter = (request.RequestUri?.Query ?? string.Empty)
                .TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .SingleOrDefault(static parameter => parameter.StartsWith("year=", StringComparison.OrdinalIgnoreCase));
            var year = int.Parse(yearParameter?.Split('=', 2)[1]
                ?? throw new InvalidOperationException("Год календаря не передан."));
            var days = DateTime.IsLeapYear(year) ? 366 : 365;
            var calendar = new char[days];
            for (var day = 1; day <= days; day++)
            {
                var date = new DateTime(year, 1, 1).AddDays(day - 1);
                calendar[day - 1] = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? '1' : '0';
            }

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(new string(calendar))
            });
        }
    }
}
