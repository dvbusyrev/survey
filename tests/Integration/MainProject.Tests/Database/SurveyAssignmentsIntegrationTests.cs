using System.Text;
using Dapper;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Application.DTO.Configuration;
using MainProject.Application.DTO.Organization;
using MainProject.Application.DTO.User;
using MainProject.Application.UseCases.Admin;
using MainProject.Application.UseCases.Answers;
using MainProject.Application.UseCases.Surveys;
using MainProject.Domain.Entities;
using MainProject.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;

namespace MainProject.Tests.Integration.Database;

[Collection(PostgreSqlIntegrationCollection.Name)]
public sealed class SurveyAssignmentsIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlIntegrationFixture _fixture;
    private readonly TestNpgsqlConnectionFactory _connectionFactory;
    private readonly ISurveyAssignmentRepository _assignmentRepository = new SurveyAssignmentRepository();
    private readonly IAnswerRepository _answerRepository;

    public SurveyAssignmentsIntegrationTests(PostgreSqlIntegrationFixture fixture)
    {
        _fixture = fixture;
        _connectionFactory = new TestNpgsqlConnectionFactory(fixture);
        _answerRepository = new AnswerRepository(
            _connectionFactory,
            _assignmentRepository,
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

        Assert.Contains("028", versions);
        Assert.Contains("background_image", themeColumns);
        Assert.DoesNotContain("gradient_enabled", themeColumns);
        Assert.DoesNotContain("background_image_data_url", themeColumns);
        Assert.DoesNotContain("soft_lighten_percent", themeColumns);
        Assert.DoesNotContain("button_strong_darken_percent", themeColumns);
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
        var rawRows = repository.GetAll().Rows;
        var updateRow = Assert.Single(rawRows, row => row.IdAudit == updateAuditId);
        var page = service.GetLogsPage(1, 1, null, null);
        var nextPage = service.GetLogsPage(2, 1, null, null);
        var updateDetails = service.GetLogDetails(updateAuditId, "organization", 1, 1, null, null);
        var surveyDetails = service.GetLogDetails(surveyAuditId, "survey", 1, 1, null, null);

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
    public async Task ConfigurationAndManagementRepositories_PersistCurrentSchemaContracts()
    {
        var organizationRepository = new OrganizationRepository(_connectionFactory);
        var userRepository = new UserRepository(_connectionFactory);
        var emailRepository = new EmailConfigRepository(_connectionFactory);
        var themeRepository = new ThemeConfigRepository(_connectionFactory);

        var organizationId = organizationRepository.Create(new OrganizationWriteModel(
            "Репозиторий организация", "Репо", "repository@example.test", DateTime.Today, null));
        var userRows = userRepository.Create(new UserWriteModel(
            organizationId, "repository-user", "Пользователь репозитория", "user", "hash", null, DateTime.Today, null));
        await emailRepository.SaveAsync(1, new EmailConfigRecord
        {
            To = "recipient@example.test",
            Subject = "Тест",
            Content = "Содержание",
            SmtpHost = "smtp.example.test",
            SmtpPort = 587,
            SmtpEnableSsl = true,
            SmtpUserName = "smtp-user",
            SmtpPasswordEncrypted = "encrypted",
            FromAddress = "sender@example.test",
            FromDisplayName = "Отправитель"
        });
        await themeRepository.SaveAsync(1, new ThemeConfigRecord
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

        var email = await emailRepository.GetAsync(1);
        var theme = await themeRepository.GetAsync(1);
        var organization = organizationRepository.GetById(organizationId);
        var user = Assert.Single(userRepository.GetPage(false, "name", "asc", 10, 0), item => item.NameUser == "repository-user");
        var deletion = userRepository.DeleteIfAllowed(user.IdUser);
        var archiveBlockedByHistory = organizationRepository.ArchiveIfUnused(organizationId);
        var emptyOrganizationId = organizationRepository.Create(new OrganizationWriteModel(
            "Организация без истории", null, null, DateTime.Today, null));
        var archiveEmptyOrganization = organizationRepository.ArchiveIfUnused(emptyOrganizationId);

        Assert.Equal(1, userRows);
        Assert.Equal("Репозиторий организация", organization!.OrganizationName);
        Assert.Equal(organizationId, user.OrganizationId);
        Assert.Equal("smtp.example.test", email!.SmtpHost);
        Assert.Equal("#B2A8FF", theme!.BackgroundColor);
        Assert.True(deletion.Deleted);
        Assert.False(archiveBlockedByHistory.Archived);
        Assert.NotEmpty(archiveBlockedByHistory.UserNames);
        Assert.True(archiveEmptyOrganization.Archived);
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
            await _assignmentRepository.ReplaceSurveyAssignmentsAsync(
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
    public async Task AssignmentRepository_SeparatesActiveAndArchivedSurveyPages()
    {
        var organizationIds = await CreateOrganizationsAsync(1);
        var survey = await CreateSurveyAsync(organizationIds);
        var activePage = new SurveyAdminService(_connectionFactory, _assignmentRepository)
            .GetSurveysPage(1, null, null, null);

        Assert.Contains(activePage.SurveyRows, row => row.IdSurvey == survey.SurveyId);

        var workflow = CreateAnswerWorkflowService();
        Assert.True(workflow.InsertAnswer(BuildAnswerRecord(survey.SurveyId!.Value, organizationIds[0], 5)).Success);

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

        var archivePage = new SurveyArchiveService(_connectionFactory, _assignmentRepository)
            .GetAdminArchivedSurveysPage(1, null, null, null, null, null, null, null, null);
        var activeAfterArchive = new SurveyAdminService(_connectionFactory, _assignmentRepository)
            .GetSurveysPage(1, null, null, null);

        Assert.DoesNotContain(activeAfterArchive.SurveyRows, row => row.IdSurvey == survey.SurveyId);
        Assert.Contains(archivePage.SurveyRows, row => row.IdSurvey == survey.SurveyId);
    }

    [RequiresPostgresFact]
    public async Task ActiveUserSurveyPage_ShowsOnlyUnansweredActiveAssignments()
    {
        var organizationIds = await CreateOrganizationsAsync(1);
        var survey = await CreateSurveyAsync(organizationIds);
        var userId = await CreateUserAsync(organizationIds[0], "active-client");
        var surveyUserService = new SurveyUserService(_connectionFactory, _assignmentRepository);

        var beforeSubmission = surveyUserService.GetActiveSurveysPage(userId, 1, "Интеграционная");

        Assert.NotNull(beforeSubmission);
        Assert.Contains(beforeSubmission!.AccessibleSurveys, item => item.IdSurvey == survey.SurveyId);

        var workflow = CreateAnswerWorkflowService();
        Assert.True(workflow.InsertAnswer(BuildAnswerRecord(survey.SurveyId!.Value, organizationIds[0], 5)).Success);

        var afterSubmission = surveyUserService.GetActiveSurveysPage(userId, 1, null);

        Assert.NotNull(afterSubmission);
        Assert.DoesNotContain(afterSubmission!.AccessibleSurveys, item => item.IdSurvey == survey.SurveyId);
    }

    [RequiresPostgresFact]
    public async Task SurveyAdminService_UsesAssignmentRepositoryForEditAndWorkPeriod()
    {
        var organizationIds = await CreateOrganizationsAsync(2);
        var survey = await CreateSurveyAsync(organizationIds);
        var service = new SurveyAdminService(_connectionFactory, _assignmentRepository);

        var editPage = service.GetSurveyEditPage(survey.SurveyId!.Value);
        var result = service.UpdateActiveSurveysWorkPeriod(new SurveyWorkPeriodRequest
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
    public async Task ArchiveCopy_UsesArchivedAssignmentLookup()
    {
        var organizationIds = await CreateOrganizationsAsync(1);
        var survey = await CreateSurveyAsync(organizationIds);
        var workflow = CreateAnswerWorkflowService();
        Assert.True(workflow.InsertAnswer(BuildAnswerRecord(survey.SurveyId!.Value, organizationIds[0], 5)).Success);

        await using (var connection = _fixture.CreateConnection())
        {
            await connection.ExecuteAsync(
                "UPDATE public.organization_survey SET date_end = CURRENT_DATE - 1 WHERE id_survey = @SurveyId;",
                new { SurveyId = survey.SurveyId });
        }

        var archiveService = new SurveyArchiveService(_connectionFactory, _assignmentRepository);
        var archive = archiveService.GetAdminArchivedSurveys();
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
        var workflow = CreateAnswerWorkflowService();

        Assert.True(workflow.SaveDraftAnswer(BuildAnswerRecord(survey.SurveyId!.Value, organizationIds[0], 4)).Success);
        Assert.True(workflow.SaveDraftAnswer(BuildAnswerRecord(survey.SurveyId!.Value, organizationIds[1], 5)).Success);

        var submitted = workflow.InsertAnswer(BuildAnswerRecord(survey.SurveyId.Value, organizationIds[0], 5));

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
    public async Task Signature_CanBeSavedOnlyOnce()
    {
        var organizationIds = await CreateOrganizationsAsync(1);
        var survey = await CreateSurveyAsync(organizationIds);
        var workflow = CreateAnswerWorkflowService();
        Assert.True(workflow.InsertAnswer(BuildAnswerRecord(survey.SurveyId!.Value, organizationIds[0], 5)).Success);

        var signing = new AnswerSigningService(
            new AnswerDataService(_connectionFactory, _assignmentRepository, _answerRepository),
            new FixedClock(DateTime.Today));
        var request = new AnswerSignatureSaveRequest
        {
            Signature = Convert.ToBase64String(Encoding.UTF8.GetBytes("integration-signature"))
        };

        Assert.True(signing.SaveSignature(survey.SurveyId.Value, organizationIds[0], request));
        Assert.Throws<AnswerAlreadySignedException>(
            () => signing.SaveSignature(survey.SurveyId.Value, organizationIds[0], request));
    }

    [RequiresPostgresFact]
    public async Task ConcurrentSignatureAttempts_LeaveExactlyOneSavedSignature()
    {
        var organizationIds = await CreateOrganizationsAsync(1);
        var survey = await CreateSurveyAsync(organizationIds);
        var workflow = CreateAnswerWorkflowService();
        Assert.True(workflow.InsertAnswer(BuildAnswerRecord(survey.SurveyId!.Value, organizationIds[0], 5)).Success);

        var signing = new AnswerSigningService(
            new AnswerDataService(_connectionFactory, _assignmentRepository, _answerRepository),
            new FixedClock(DateTime.Today));
        var request = new AnswerSignatureSaveRequest
        {
            Signature = Convert.ToBase64String(Encoding.UTF8.GetBytes("concurrent-signature"))
        };

        var attempts = await Task.WhenAll(
            Task.Run(() => TrySaveSignature(signing, survey.SurveyId.Value, organizationIds[0], request)),
            Task.Run(() => TrySaveSignature(signing, survey.SurveyId.Value, organizationIds[0], request)));

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
        var workflow = CreateAnswerWorkflowService();
        Assert.True(workflow.InsertAnswer(BuildAnswerRecord(survey.SurveyId!.Value, organizationIds[0], 5)).Success);

        await using var connection = _fixture.CreateConnection();
        var userId = await connection.ExecuteScalarAsync<int>(
            """
            INSERT INTO public.app_user (id_organization, login, full_name, role, password, date_begin)
            VALUES (@OrganizationId, 'archive-client', 'Клиент архива', 'user', 'hash', CURRENT_DATE)
            RETURNING id_user;
            """,
            new { OrganizationId = organizationIds[0] });

        var archive = new SurveyArchiveService(_connectionFactory, _assignmentRepository)
            .GetUserArchivePage(userId, 1, null, null, null, null, signedOnly: false);

        Assert.NotNull(archive);
        Assert.Contains(archive!.ArchivedSurveys, item => item.IdSurvey == survey.SurveyId);
    }

    [RequiresPostgresFact]
    public async Task AutoCreation_SavePersistsScheduleAndSelectedTemplate()
    {
        var organizationIds = await CreateOrganizationsAsync(1);
        var survey = await CreateSurveyAsync(organizationIds);
        var autoCreation = new SurveyAutoCreationService(
            _connectionFactory,
            NullLogger<SurveyAutoCreationService>.Instance,
            new FixedClock(new DateTime(2026, 4, 20)),
            _assignmentRepository);

        var result = await autoCreation.SaveAsync(new SurveyAutoCreationSettingsRequest
        {
            CreationPattern = "1-monday",
            StartPattern = "2-friday",
            EndOffsetBusinessDays = 10,
            SurveyIds = [survey.SurveyId!.Value]
        });

        await using var connection = _fixture.CreateConnection();
        var stored = await connection.QuerySingleAsync<(int WorkingPeriod, bool IsEnabled)>(
            "SELECT working_period, is_enabled FROM public.auto_creation_config WHERE id_config = 1;");
        var selectedSurveyId = await connection.ExecuteScalarAsync<int>(
            "SELECT id_survey FROM public.survey_auto_creation_config WHERE id_config = 1;");

        Assert.True(result.Success);
        Assert.Equal(10, stored.WorkingPeriod);
        Assert.False(stored.IsEnabled);
        Assert.Equal(survey.SurveyId, selectedSurveyId);
    }

    [RequiresPostgresFact]
    public async Task AutoCreation_RunPending_CreatesScheduledCopyOnlyOnce()
    {
        var organizationIds = await CreateOrganizationsAsync(2);
        var survey = await CreateSurveyAsync(organizationIds);
        var autoCreation = new SurveyAutoCreationService(
            _connectionFactory,
            NullLogger<SurveyAutoCreationService>.Instance,
            new FixedClock(new DateTime(2026, 4, 20)),
            _assignmentRepository);

        var startResult = await autoCreation.StartAsync(new SurveyAutoCreationSettingsRequest
        {
            CreationPattern = "3-monday",
            StartPattern = "3-monday",
            EndOffsetBusinessDays = 5,
            SurveyIds = [survey.SurveyId!.Value]
        });
        var repeatedRun = await autoCreation.RunPendingAsync();

        await using var connection = _fixture.CreateConnection();
        var copy = await connection.QuerySingleAsync<SurveyCopyRow>(
            """
            SELECT id_survey AS IdSurvey
            FROM public.survey
            WHERE name_survey = 'Интеграционная анкета (Копия)';
            """);
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
            Assert.Equal(new DateTime(2026, 4, 20), row.DateBegin);
            Assert.Equal(new DateTime(2026, 4, 27), row.DateEnd);
        });
        Assert.True(repeatedRun.Processed);
        Assert.Equal(0, repeatedRun.CreatedSurveyCount);
    }

    private AnswerWorkflowService CreateAnswerWorkflowService()
        => new(new AnswerDataService(_connectionFactory, _assignmentRepository, _answerRepository));

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
        var result = await new SurveyAdminService(_connectionFactory, _assignmentRepository).CreateSurveyAsync(new SurveyAddRequest
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

    private static bool TrySaveSignature(
        AnswerSigningService signing,
        int surveyId,
        int organizationId,
        AnswerSignatureSaveRequest request)
    {
        try
        {
            return signing.SaveSignature(surveyId, organizationId, request);
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
}
