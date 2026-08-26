using System.Text;
using Dapper;
using DocumentFormat.OpenXml.Packaging;
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
using MainProject.Web.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
        Assert.Contains("038", versions);
        Assert.Contains("039", versions);
        Assert.Contains("040", versions);
        Assert.Contains("042", versions);
        Assert.Contains("043", versions);
        Assert.Contains("044", versions);
        Assert.Contains("047", versions);
        Assert.Contains("049", versions);
        Assert.Contains("050", versions);
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
                    "survey_template_auto_creation_config_l",
                    "survey_l",
                    "survey_question_l",
                    "survey_template_l",
                    "survey_template_question_l",
                    "organization_survey_template_l",
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
                    "answer_id_user_fkey"
                }
            })).ToDictionary(row => row.ConstraintName, row => row.DeleteRule, StringComparer.OrdinalIgnoreCase);
        var obsoleteParticipantTableCount = await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM pg_class table_definition
            INNER JOIN pg_namespace table_namespace
                ON table_namespace.oid = table_definition.relnamespace
            WHERE table_namespace.nspname = 'public'
              AND table_definition.relname IN ('answer_participant', 'answer_draft_participant');
            """);
        Assert.Contains("background_image", themeColumns);
        Assert.Equal("NO", userOrganizationIsNullable);
        Assert.Equal("RESTRICT", userOrganizationDeleteAction);
        Assert.Equal(5, protectedDeleteRules.Count);
        Assert.All(protectedDeleteRules.Values, deleteRule => Assert.Equal("RESTRICT", deleteRule));
        Assert.Equal(0, obsoleteParticipantTableCount);
        Assert.DoesNotContain("gradient_enabled", themeColumns);
        Assert.DoesNotContain("background_image_data_url", themeColumns);
        Assert.DoesNotContain("soft_lighten_percent", themeColumns);
        Assert.DoesNotContain("button_strong_darken_percent", themeColumns);
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'survey'
              AND column_name = 'is_template';
            """));
        Assert.Equal("survey_template_auto_creation_config", await connection.ExecuteScalarAsync<string>(
            "SELECT to_regclass('public.survey_template_auto_creation_config')::text;"));
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'organization_survey_template'
              AND column_name IN ('date_begin', 'date_end');
            """));
        Assert.Equal("YES", await connection.ExecuteScalarAsync<string>(
            """
            SELECT is_nullable
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'survey_template'
              AND column_name = 'ancestor_id';
            """));
        Assert.Equal("RESTRICT", await connection.ExecuteScalarAsync<string>(
            """
            SELECT delete_rule
            FROM information_schema.referential_constraints
            WHERE constraint_schema = 'public'
              AND constraint_name = 'survey_template_ancestor_id_fkey';
            """));
    }

    [RequiresPostgresFact]
    public async Task SurveyTemplates_AreStoredAndListedSeparatelyFromSurveys()
    {
        var organizationId = (await CreateOrganizationsAsync(1)).Single();
        var service = new SurveyService(_connectionFactory, _surveyRepository, _clock);
        var survey = await CreateSurveyAsync([organizationId]);
        var template = await service.CreateSurveyTemplateAsync(new SurveyAddRequest
        {
            Title = "Интеграционный шаблон",
            Description = "Проверка отдельного раздела шаблонов",
            StartDate = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd"),
            EndDate = string.Empty,
            Organizations = [organizationId],
            Criteria = ["Первый вопрос", "Второй вопрос"],
            IsAutoCreationEnabled = true
        });

        Assert.True(template.Success, template.Message);
        Assert.NotNull(template.SurveyId);
        Assert.Contains("Шаблон успешно добавлен в автосоздание.", template.Message);

        var surveyPage = await service.GetSurveysPageAsync(1, null, null, null);
        var templatePage = await service.GetSurveyTemplatesPageAsync(1, null, null, null);
        var activeTemplateOptions = await service.GetActiveSurveyTemplateOptionsAsync();

        Assert.Contains(surveyPage.SurveyRows, row => row.IdSurvey == survey.SurveyId);
        Assert.DoesNotContain(surveyPage.SurveyRows, row => row.NameSurvey == "Интеграционный шаблон");
        Assert.Contains(templatePage.SurveyRows, row => row.IdSurvey == template.SurveyId && row.NameSurvey == "Интеграционный шаблон");
        Assert.Contains(templatePage.SurveyRows, row => row.IdSurvey == template.SurveyId && row.DateEnd == null);
        Assert.Contains(templatePage.SurveyRows, row => row.IdSurvey == template.SurveyId && row.IsAutoCreationEnabled);
        Assert.DoesNotContain(templatePage.SurveyRows, row => row.IdSurvey == survey.SurveyId && row.NameSurvey == "Интеграционная анкета");
        Assert.True(templatePage.IsTemplateSection);
        Assert.Equal("/survey-templates", templatePage.FilterState.BasePath);
        Assert.Contains(activeTemplateOptions, option => option.Id == template.SurveyId && option.Name == "Интеграционный шаблон");

        await using var connection = _fixture.CreateConnection();
        Assert.True(await connection.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS (
                SELECT 1
                FROM public.survey_template
                WHERE id_survey_template = @SurveyId
            );
            """,
            new { SurveyId = template.SurveyId }));
        Assert.True(await connection.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS (
                SELECT 1
                FROM public.survey_template_auto_creation_config
                WHERE id_config = 1
                  AND id_survey_template = @SurveyId
            );
            """,
            new { SurveyId = template.SurveyId }));
        Assert.True(await connection.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS (
                SELECT 1
                FROM public.survey_template_l
                WHERE id_survey_template = @SurveyId
            );
            """,
            new { SurveyId = template.SurveyId }));

        await connection.ExecuteAsync(
            """
            UPDATE public.survey_template
            SET date_begin = CURRENT_DATE - 10,
                date_end = CURRENT_DATE - 1
            WHERE id_survey_template = @SurveyId;
            """,
            new { SurveyId = template.SurveyId });

        var surveyArchive = await service.GetAdminArchivedSurveysPageAsync(
            1, null, null, null, null, null, null, null, null);
        var templateArchive = await service.GetAdminArchivedSurveyTemplatesPageAsync(
            1, null, null, null, null, null, null, null, null);
        var activeTemplateOptionsAfterArchiving = await service.GetActiveSurveyTemplateOptionsAsync();
        var autoCreationPage = await service.GetPageModelAsync();

        Assert.DoesNotContain(surveyArchive.SurveyRows, row => row.NameSurvey == "Интеграционный шаблон");
        Assert.Contains(templateArchive.SurveyRows, row => row.IdSurvey == template.SurveyId && row.NameSurvey == "Интеграционный шаблон");
        Assert.DoesNotContain(activeTemplateOptionsAfterArchiving, option => option.Id == template.SurveyId);
        Assert.DoesNotContain(autoCreationPage.SelectedTemplates, item => item.Id == template.SurveyId);
        Assert.False(await connection.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS (
                SELECT 1
                FROM public.survey_template_auto_creation_config
                WHERE id_survey_template = @SurveyId
            );
            """,
            new { SurveyId = template.SurveyId }));
        Assert.True(templateArchive.IsTemplateSection);
        Assert.Equal("/survey-templates/archive", templateArchive.FilterState.BasePath);
        Assert.False(templateArchive.FilterState.EnableSurveyFilter);
        Assert.Empty(templateArchive.FilterState.SurveyOptions);
        Assert.Empty(templateArchive.FilterState.SelectedSurveyIds);
    }

    [RequiresPostgresFact]
    public async Task SurveyTemplateUpdate_ReportsAutoCreationSelectionChanges()
    {
        var organizationId = (await CreateOrganizationsAsync(1)).Single();
        var service = new SurveyService(_connectionFactory, _surveyRepository, _clock);
        var template = await service.CreateSurveyTemplateAsync(new SurveyAddRequest
        {
            Title = "Шаблон для изменения автосоздания",
            Description = "Проверка уведомлений",
            StartDate = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd"),
            EndDate = string.Empty,
            Organizations = [organizationId],
            Criteria = ["Критерий"],
            IsAutoCreationEnabled = false
        });

        Assert.True(template.Success, template.Message);
        Assert.NotNull(template.SurveyId);

        var updateRequest = new SurveyUpdateRequest
        {
            Title = "Шаблон для изменения автосоздания",
            Description = "Проверка уведомлений",
            StartDate = DateTime.Today.AddDays(-1),
            EndDate = null,
            Organizations = [organizationId],
            Criteria = ["Критерий"],
            IsAutoCreationEnabled = true
        };
        var addedResult = await service.UpdateSurveyTemplateAsync(template.SurveyId.Value, updateRequest);

        updateRequest.IsAutoCreationEnabled = false;
        var removedResult = await service.UpdateSurveyTemplateAsync(template.SurveyId.Value, updateRequest);

        Assert.True(addedResult.Success, addedResult.Message);
        Assert.Contains("Шаблон успешно добавлен в автосоздание.", addedResult.Message);
        Assert.True(removedResult.Success, removedResult.Message);
        Assert.Contains("Шаблон успешно удалён из автосоздания.", removedResult.Message);
    }

    [RequiresPostgresFact]
    public async Task SurveyTemplates_CanBeSortedByAutoCreationSelection()
    {
        var organizationId = Assert.Single(await CreateOrganizationsAsync(1));
        var service = new SurveyService(_connectionFactory, _surveyRepository, _clock);
        var selectedTemplate = await service.CreateSurveyTemplateAsync(new SurveyAddRequest
        {
            Title = "Шаблон в автосоздании",
            StartDate = DateTime.Today.ToString("yyyy-MM-dd"),
            Organizations = [organizationId],
            Criteria = ["Критерий"],
            IsAutoCreationEnabled = true
        });
        var unselectedTemplate = await service.CreateSurveyTemplateAsync(new SurveyAddRequest
        {
            Title = "Шаблон вне автосоздания",
            StartDate = DateTime.Today.ToString("yyyy-MM-dd"),
            Organizations = [organizationId],
            Criteria = ["Критерий"],
            IsAutoCreationEnabled = false
        });

        var descendingPage = await service.GetSurveyTemplatesPageAsync(
            1,
            SurveyListSortFields.AutoCreation,
            "desc",
            null);
        var ascendingPage = await service.GetSurveyTemplatesPageAsync(
            1,
            SurveyListSortFields.AutoCreation,
            "asc",
            null);

        Assert.True(selectedTemplate.Success, selectedTemplate.Message);
        Assert.True(unselectedTemplate.Success, unselectedTemplate.Message);
        Assert.True(descendingPage.SurveyRows.First().IsAutoCreationEnabled);
        Assert.False(ascendingPage.SurveyRows.First().IsAutoCreationEnabled);
        Assert.Equal(SurveyListSortFields.AutoCreation, descendingPage.SortBy);
        Assert.Equal("desc", descendingPage.SortDirection);
        Assert.Equal(SurveyListSortFields.AutoCreation, ascendingPage.SortBy);
        Assert.Equal("asc", ascendingPage.SortDirection);
    }

    [RequiresPostgresFact]
    public async Task PlannedSurveyTemplate_PromotesAndArchivesItsParentOnStartDate()
    {
        var today = DateTime.Today.Date;
        var organizationId = Assert.Single(await CreateOrganizationsAsync(1));
        var service = new SurveyService(_connectionFactory, _surveyRepository, _clock);
        var parent = await service.CreateSurveyTemplateAsync(new SurveyAddRequest
        {
            Title = "Действующий родительский шаблон",
            StartDate = today.ToString("yyyy-MM-dd"),
            Organizations = [organizationId],
            Criteria = ["Критерий родителя"],
            IsAutoCreationEnabled = true
        });
        var plannedStart = today.AddDays(2);
        var planned = await service.CreatePlannedSurveyTemplateAsync(new SurveyAddRequest
        {
            Title = "Плановый шаблон",
            StartDate = plannedStart.ToString("yyyy-MM-dd"),
            EndDate = plannedStart.AddDays(20).ToString("yyyy-MM-dd"),
            Organizations = [organizationId],
            Criteria = ["Критерий планового шаблона"],
            IsAutoCreationEnabled = true,
            AncestorId = parent.SurveyId
        });

        Assert.True(parent.Success, parent.Message);
        Assert.True(planned.Success, planned.Message);
        Assert.NotNull(parent.SurveyId);
        Assert.NotNull(planned.SurveyId);

        var plannedPage = await service.GetPlannedSurveyTemplatesPageAsync(1, null, null, null);
        var archiveBeforeStart = await service.GetAdminArchivedSurveyTemplatesPageAsync(
            1, null, null, null, null, null, null, null, null);
        Assert.Contains(plannedPage.SurveyRows, row => row.IdSurvey == planned.SurveyId
            && row.AncestorId == parent.SurveyId
            && row.IsAutoCreationEnabled);
        Assert.DoesNotContain(archiveBeforeStart.SurveyRows, row => row.IdSurvey == planned.SurveyId);

        await using (var beforePromotionConnection = _fixture.CreateConnection())
        {
            Assert.Equal(plannedStart.AddDays(-1), await beforePromotionConnection.ExecuteScalarAsync<DateTime?>(
                "SELECT date_end FROM public.survey_template WHERE id_survey_template = @TemplateId;",
                new { TemplateId = parent.SurveyId }));
            Assert.DoesNotContain(
                planned.SurveyId!.Value,
                await _surveyRepository.GetSelectedAutoCreationTemplateIdsAsync(
                    beforePromotionConnection,
                    null,
                    1));
            Assert.Contains(
                await _surveyRepository.GetSelectedAutoCreationTemplatesAsync(
                    beforePromotionConnection,
                    null,
                    1),
                item => item.Id == planned.SurveyId);
        }

        var promotionClock = new FixedClock(plannedStart);
        var promotionRepository = new SurveyRepository(promotionClock);
        var promotionService = new SurveyService(_connectionFactory, promotionRepository, promotionClock);
        var promotedCount = await promotionService.PromotePlannedSurveyTemplatesAsync();
        var activeAfterStart = await promotionService.GetSurveyTemplatesPageAsync(1, null, null, null);
        var archiveAfterStart = await promotionService.GetAdminArchivedSurveyTemplatesPageAsync(
            1, null, null, null, null, null, null, null, null);

        Assert.Equal(1, promotedCount);
        Assert.Contains(activeAfterStart.SurveyRows, row => row.IdSurvey == planned.SurveyId
            && row.AncestorId == null
            && row.IsAutoCreationEnabled);
        Assert.Contains(archiveAfterStart.SurveyRows, row => row.IdSurvey == parent.SurveyId
            && row.DateEnd == plannedStart.AddDays(-1));
        Assert.DoesNotContain(activeAfterStart.SurveyRows, row => row.IdSurvey == parent.SurveyId);

        await using var connection = _fixture.CreateConnection();
        Assert.Null(await connection.ExecuteScalarAsync<int?>(
            "SELECT ancestor_id FROM public.survey_template WHERE id_survey_template = @TemplateId;",
            new { TemplateId = planned.SurveyId }));
        Assert.False(await connection.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS (
                SELECT 1
                FROM public.survey_template_auto_creation_config
                WHERE id_survey_template = @TemplateId
            );
            """,
            new { TemplateId = parent.SurveyId }));
        Assert.True(await connection.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS (
                SELECT 1
                FROM public.survey_template_auto_creation_config
                WHERE id_survey_template = @TemplateId
            );
            """,
            new { TemplateId = planned.SurveyId }));
    }

    [RequiresPostgresFact]
    public async Task PlannedSurveyTemplate_RejectsOverlapAndReportsGapAfterParent()
    {
        var today = DateTime.Today.Date;
        var organizationId = Assert.Single(await CreateOrganizationsAsync(1));
        var service = new SurveyService(_connectionFactory, _surveyRepository, _clock);
        var parentEnd = today.AddDays(4);
        var parent = await service.CreateSurveyTemplateAsync(new SurveyAddRequest
        {
            Title = "Ограниченный родительский шаблон",
            StartDate = today.ToString("yyyy-MM-dd"),
            EndDate = parentEnd.ToString("yyyy-MM-dd"),
            Organizations = [organizationId],
            Criteria = ["Критерий родителя"]
        });

        var overlap = await service.CreatePlannedSurveyTemplateAsync(new SurveyAddRequest
        {
            Title = "Пересекающийся плановый шаблон",
            StartDate = parentEnd.ToString("yyyy-MM-dd"),
            EndDate = parentEnd.AddDays(10).ToString("yyyy-MM-dd"),
            Organizations = [organizationId],
            Criteria = ["Критерий"],
            AncestorId = parent.SurveyId
        });
        var plannedStart = parentEnd.AddDays(4);
        var withGap = await service.CreatePlannedSurveyTemplateAsync(new SurveyAddRequest
        {
            Title = "Плановый шаблон с промежутком",
            StartDate = plannedStart.ToString("yyyy-MM-dd"),
            EndDate = plannedStart.AddDays(10).ToString("yyyy-MM-dd"),
            Organizations = [organizationId],
            Criteria = ["Критерий"],
            AncestorId = parent.SurveyId
        });

        Assert.True(parent.Success, parent.Message);
        Assert.False(overlap.Success);
        Assert.Equal(
            "Дата начала планового шаблона должна быть позже даты окончания шаблона-родителя.",
            overlap.Message);
        Assert.True(withGap.Success, withGap.Message);
        Assert.Contains("будет автоматически перенесён в активные шаблоны", withGap.Message);
        Assert.Contains("промежуток: 3 дня", withGap.Message);
    }

    [RequiresPostgresFact]
    public async Task PlannedSurveyTemplate_EditRejectsAncestorOverlapAndReportsChangedGap()
    {
        var today = DateTime.Today.Date;
        var organizationId = Assert.Single(await CreateOrganizationsAsync(1));
        var service = new SurveyService(_connectionFactory, _surveyRepository, _clock);
        var parentEnd = today.AddDays(4);
        var parent = await service.CreateSurveyTemplateAsync(new SurveyAddRequest
        {
            Title = "Родитель редактируемого планового шаблона",
            StartDate = today.ToString("yyyy-MM-dd"),
            EndDate = parentEnd.ToString("yyyy-MM-dd"),
            Organizations = [organizationId],
            Criteria = ["Критерий родителя"]
        });
        var initialStart = parentEnd.AddDays(1);
        var planned = await service.CreatePlannedSurveyTemplateAsync(new SurveyAddRequest
        {
            Title = "Редактируемый плановый шаблон",
            StartDate = initialStart.ToString("yyyy-MM-dd"),
            EndDate = initialStart.AddDays(10).ToString("yyyy-MM-dd"),
            Organizations = [organizationId],
            Criteria = ["Критерий планового шаблона"],
            AncestorId = parent.SurveyId
        });

        Assert.True(parent.Success, parent.Message);
        Assert.True(planned.Success, planned.Message);
        Assert.NotNull(parent.SurveyId);
        Assert.NotNull(planned.SurveyId);

        SurveyUpdateRequest BuildPlannedUpdate(DateTime startDate, DateTime endDate) => new()
        {
            Title = "Редактируемый плановый шаблон",
            StartDate = startDate,
            EndDate = endDate,
            Organizations = [organizationId],
            Criteria = ["Критерий планового шаблона"],
            AncestorId = parent.SurveyId
        };

        var overlap = await service.UpdatePlannedSurveyTemplateAsync(
            planned.SurveyId.Value,
            BuildPlannedUpdate(parentEnd, parentEnd.AddDays(10)));
        var movedStart = parentEnd.AddDays(4);
        var moved = await service.UpdatePlannedSurveyTemplateAsync(
            planned.SurveyId.Value,
            BuildPlannedUpdate(movedStart, movedStart.AddDays(10)));

        Assert.False(overlap.Success);
        Assert.Equal(
            "Дата начала планового шаблона должна быть позже даты окончания шаблона-родителя.",
            overlap.Message);
        Assert.True(moved.Success, moved.Message);
        Assert.Contains("После изменения даты начала", moved.Message);
        Assert.Contains("разрыв между шаблоном-родителем и плановым шаблоном составляет: 3 дня", moved.Message);

        await using var connection = _fixture.CreateConnection();
        var updatedPeriod = await connection.QuerySingleAsync<(DateTime DateBegin, DateTime? DateEnd)>(
            """
            SELECT date_begin AS DateBegin, date_end AS DateEnd
            FROM public.survey_template
            WHERE id_survey_template = @TemplateId;
            """,
            new { TemplateId = planned.SurveyId });
        Assert.Equal(movedStart, updatedPeriod.DateBegin);
        Assert.Equal(movedStart.AddDays(10), updatedPeriod.DateEnd);
    }

    [RequiresPostgresFact]
    public async Task ParentSurveyTemplate_EditProtectsPlannedDescendantPeriodAndReportsNewGap()
    {
        var today = DateTime.Today.Date;
        var organizationId = Assert.Single(await CreateOrganizationsAsync(1));
        var service = new SurveyService(_connectionFactory, _surveyRepository, _clock);
        var parent = await service.CreateSurveyTemplateAsync(new SurveyAddRequest
        {
            Title = "Редактируемый родительский шаблон",
            StartDate = today.ToString("yyyy-MM-dd"),
            Organizations = [organizationId],
            Criteria = ["Критерий родителя"]
        });
        var plannedStart = today.AddDays(10);
        var planned = await service.CreatePlannedSurveyTemplateAsync(new SurveyAddRequest
        {
            Title = "Плановый потомок",
            StartDate = plannedStart.ToString("yyyy-MM-dd"),
            EndDate = plannedStart.AddDays(10).ToString("yyyy-MM-dd"),
            Organizations = [organizationId],
            Criteria = ["Критерий потомка"],
            AncestorId = parent.SurveyId
        });

        Assert.True(parent.Success, parent.Message);
        Assert.True(planned.Success, planned.Message);
        Assert.NotNull(parent.SurveyId);

        SurveyUpdateRequest BuildParentUpdate(DateTime? endDate) => new()
        {
            Title = "Редактируемый родительский шаблон",
            StartDate = today,
            EndDate = endDate,
            Organizations = [organizationId],
            Criteria = ["Критерий родителя"]
        };

        var openEnded = await service.UpdateSurveyTemplateAsync(
            parent.SurveyId.Value,
            BuildParentUpdate(null));
        var overlap = await service.UpdateSurveyTemplateAsync(
            parent.SurveyId.Value,
            BuildParentUpdate(plannedStart));
        var shortenedEnd = plannedStart.AddDays(-4);
        var shortened = await service.UpdateSurveyTemplateAsync(
            parent.SurveyId.Value,
            BuildParentUpdate(shortenedEnd));

        Assert.False(openEnded.Success);
        Assert.Equal(
            "Укажите дату конца: у шаблона есть плановые шаблоны-потомки.",
            openEnded.Message);
        Assert.False(overlap.Success);
        Assert.Equal(
            $"Дата конца шаблона-родителя должна быть раньше даты начала планового шаблона «Плановый потомок» ({plannedStart:dd.MM.yyyy}).",
            overlap.Message);
        Assert.True(shortened.Success, shortened.Message);
        Assert.Contains("Шаблон успешно обновлён.", shortened.Message);
        Assert.Contains("плановым шаблоном-потомком «Плановый потомок»", shortened.Message);
        Assert.Contains("разрыв в работе: 3 дня", shortened.Message);

        await using var connection = _fixture.CreateConnection();
        Assert.Equal(shortenedEnd, await connection.ExecuteScalarAsync<DateTime?>(
            "SELECT date_end FROM public.survey_template WHERE id_survey_template = @TemplateId;",
            new { TemplateId = parent.SurveyId }));
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
        var userUpdateColumnCount = await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND column_name = 'user_update';
            """);
        var dateUpdateTables = (await connection.QueryAsync<string>(
            """
            SELECT table_name
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND column_name = 'date_update'
            ORDER BY table_name;
            """)).ToArray();
        var metadataTriggerTables = (await connection.QueryAsync<string>(
            """
            SELECT DISTINCT table_class.relname
            FROM pg_trigger trigger_definition
            INNER JOIN pg_class table_class
                ON table_class.oid = trigger_definition.tgrelid
            INNER JOIN pg_namespace table_namespace
                ON table_namespace.oid = table_class.relnamespace
            INNER JOIN pg_proc trigger_function
                ON trigger_function.oid = trigger_definition.tgfoid
            INNER JOIN pg_namespace function_namespace
                ON function_namespace.oid = trigger_function.pronamespace
            WHERE NOT trigger_definition.tgisinternal
              AND table_namespace.nspname = 'public'
              AND function_namespace.nspname = 'public'
              AND trigger_function.proname = 'set_update_metadata'
            ORDER BY table_class.relname;
            """)).ToArray();
        var updateMetadataFunctionCount = await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM pg_proc function_definition
            INNER JOIN pg_namespace function_namespace
                ON function_namespace.oid = function_definition.pronamespace
            WHERE function_namespace.nspname = 'public'
              AND function_definition.proname = 'set_update_metadata';
            """);
        var emailSingletonConstraint = await connection.ExecuteScalarAsync<string>(
            """
            SELECT pg_get_constraintdef(constraint_definition.oid)
            FROM pg_constraint constraint_definition
            WHERE constraint_definition.conrelid = 'public.email_config'::regclass
              AND constraint_definition.conname = 'ck_email_config_singleton';
            """);
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

        Assert.DoesNotContain("date_update", answerColumns);
        Assert.DoesNotContain("user_update", answerColumns);
        Assert.DoesNotContain("date_update", autoCreationColumns);
        Assert.DoesNotContain("user_update", autoCreationColumns);
        Assert.Equal(0, userUpdateColumnCount);
        Assert.Empty(dateUpdateTables);
        Assert.Empty(metadataTriggerTables);
        Assert.Equal(0, updateMetadataFunctionCount);
        Assert.Contains("id_config = 1", emailSingletonConstraint);
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
        var surveyPeriod = await connection.QuerySingleAsync<SurveyDateRow>(
            """
            SELECT date_begin AS DateBegin, date_end AS DateEnd
            FROM public.survey
            WHERE id_survey = @SurveyId;
            """,
            new { SurveyId = result.SurveyId });
        var auditCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM public.survey_l WHERE id_survey = @SurveyId AND operation = 'INSERT';",
            new { SurveyId = result.SurveyId });

        Assert.True(result.Success);
        Assert.Equal(2, questionCount);
        Assert.Equal(organizationIds.Count, assignmentCount);
        Assert.Equal(DateTime.Today.AddDays(-1), surveyPeriod.DateBegin);
        Assert.Equal(DateTime.Today.AddDays(14), surveyPeriod.DateEnd);
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
        var smtpPasswordProtector = new SmtpPasswordProtector(new EphemeralDataProtectionProvider());
        var emailService = new EmailTemplateService(
            _connectionFactory,
            new SmtpEmailSender(),
            smtpPasswordProtector);
        var themeService = new ThemeSettingsService(
            _connectionFactory,
            NullLogger<ThemeSettingsService>.Instance);

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
        var emailConfigCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM public.email_config;");
        var storedAuditPasswords = (await connection.QueryAsync<string?>(
            "SELECT smtp_password FROM public.email_config_l;")).ToArray();
        await connection.ExecuteAsync(
            "UPDATE public.email_config SET smtp_password = 'CfDJ8LegacyPassword';");
        var senderWithLegacyPassword = await emailService.GetSenderAsync();
        var users = (await userService.GetActiveUsersPageAsync(1, "name", "asc")).Users;
        var user = Assert.Single(users, item => item.NameUser == "repository-user");
        var deletion = await userService.DeleteUserAsync(user.IdUser);
        var deleteAfterUserDeletion = await organizationService.DeleteOrganizationAsync(organizationId);
        var createEmptyOrganization = await organizationService.CreateOrganizationAsync(new OrganizationSaveRequest
        {
            Name = "Организация без истории",
            DateBegin = DateTime.Today.ToString("yyyy-MM-dd")
        });
        var deleteEmptyOrganization = await organizationService.DeleteOrganizationAsync(createEmptyOrganization.EntityId!.Value);

        Assert.True(createOrganization.Success);
        Assert.True(createUser.Success);
        Assert.Equal("Репозиторий организация", organization!.OrganizationName);
        Assert.Equal(organizationId, user.OrganizationId);
        Assert.Equal("recipient@example.test", emailMessage.To);
        Assert.Equal("Обновлённое письмо", emailMessage.Subject);
        Assert.Equal("Новое содержание", emailMessage.Content);
        Assert.Equal("smtp.example.test", emailSender.SmtpHost);
        Assert.Empty(emailSender.SmtpPassword);
        Assert.Equal(1, storedEmailConfig.IdConfig);
        Assert.Equal(1, emailConfigCount);
        Assert.StartsWith(SmtpPasswordProtector.ProtectedValuePrefix, storedEmailConfig.SmtpPassword);
        Assert.Equal("smtp-password", smtpPasswordProtector.Unprotect(storedEmailConfig.SmtpPassword));
        Assert.All(
            storedAuditPasswords,
            password => Assert.True(string.IsNullOrEmpty(password) || password == "[REDACTED]"));
        Assert.Empty(senderWithLegacyPassword.SmtpPassword);
        Assert.Equal("#B2A8FF", theme.BackgroundColor);
        Assert.True(deletion.Success);
        Assert.True(deleteAfterUserDeletion.Success);
        Assert.True(deleteEmptyOrganization.Success);
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
        var answerUserId = await connection.ExecuteScalarAsync<int>(
            """
            SELECT answer.id_user
            FROM public.answer answer
            WHERE answer.id_organization_survey = @AssignmentId
            """,
            new { AssignmentId = assignmentId });

        var surveyDeletion = await new SurveyService(_connectionFactory, _surveyRepository, _clock)
            .DeleteSurveyAsync(surveyId);
        var userDeletion = await new UserManagementService(_connectionFactory, _clock)
            .DeleteUserAsync(userId);
        var organizationDateEndBeforeDeletion = await connection.ExecuteScalarAsync<DateTime?>(
            "SELECT date_end FROM public.organization WHERE id_organization = @OrganizationId;",
            new { OrganizationId = organizationId });
        var organizationDeletion = await new OrganizationManagementService(_connectionFactory, _clock)
            .DeleteOrganizationAsync(organizationId);
        var userDeletionHttpResult = await new UserController(new UserManagementService(_connectionFactory, _clock))
            .DeleteUser(userId, CancellationToken.None);

        Assert.False(surveyDeletion.Success);
        Assert.Equal("survey_in_use", surveyDeletion.Code);
        Assert.Contains("Интеграционная анкета", surveyDeletion.Message);
        Assert.Contains("Орг 1", surveyDeletion.Message);
        Assert.Contains("по ней есть ответы", surveyDeletion.Message);

        Assert.False(userDeletion.Success);
        Assert.Equal(userId, answerUserId);
        Assert.Equal("user_in_use", userDeletion.Code);
        Assert.Contains("Тестовый клиент", userDeletion.Message);
        Assert.Contains("Связанные анкеты", userDeletion.Message);
        Assert.IsType<ConflictObjectResult>(userDeletionHttpResult);

        Assert.False(organizationDeletion.Success);
        Assert.Equal("organization_in_use", organizationDeletion.Code);
        Assert.Contains("Анкеты:", organizationDeletion.Message);
        Assert.Contains("Пользователи:", organizationDeletion.Message);
        Assert.Equal(organizationDateEndBeforeDeletion, await connection.ExecuteScalarAsync<DateTime?>(
            "SELECT date_end FROM public.organization WHERE id_organization = @OrganizationId;",
            new { OrganizationId = organizationId }));

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
        Assert.False((await organizationService.DeleteOrganizationAsync(organizationId)).Success);

        Assert.True((await answerService.DeleteAnswerAsync(answer.IdAnswer)).Success);
        Assert.True((await surveyService.DeleteSurveyAsync(surveyId)).Success);
        Assert.True((await userService.DeleteUserAsync(userId)).Success);
        Assert.True((await organizationService.DeleteOrganizationAsync(organizationId)).Success);

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
        Assert.False(await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM public.organization WHERE id_organization = @OrganizationId);",
            new { OrganizationId = organizationId }));
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
    public async Task SurveyExtension_UpdatesOnlyExistingAssignments()
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
        var basePeriod = await connection.QuerySingleAsync<SurveyDateRow>(
            """
            SELECT date_begin AS DateBegin, date_end AS DateEnd
            FROM public.survey
            WHERE id_survey = @SurveyId;
            """,
            new { SurveyId = survey.SurveyId });
        var activePage = await service.GetSurveysPageAsync(1, null, null, null);
        var activePageSortedByEndDate = await service.GetSurveysPageAsync(
            1,
            SurveyListSortFields.DateEnd,
            "asc",
            null);

        Assert.True(result.Success, result.Message);
        var assignment = Assert.Single(assignments);
        Assert.Equal(organizationIds[0], assignment.OrganizationId);
        Assert.Equal(DateTime.Today.AddDays(-1), assignment.DateBegin);
        Assert.Equal(extendedUntil, assignment.DateEnd);
        Assert.Equal(DateTime.Today.AddDays(-1), basePeriod.DateBegin);
        Assert.Equal(DateTime.Today.AddDays(14), basePeriod.DateEnd);
        Assert.Contains(
            activePage.SurveyRows,
            row => row.NameSurvey == "Интеграционная анкета"
                && !row.IsExtension
                && row.ExtensionOrganizationId == null);
        Assert.Contains(
            activePage.SurveyRows,
            row => row.NameSurvey == "Интеграционная анкета: продление для Орг 1"
                && row.OriginalNameSurvey == "Интеграционная анкета"
                && row.IsExtension
                && row.ExtensionOrganizationId == organizationIds[0]);
        Assert.Equal(
            [
                "Интеграционная анкета",
                "Интеграционная анкета: продление для Орг 1"
            ],
            activePageSortedByEndDate.SurveyRows.Select(row => row.NameSurvey));
    }

    [RequiresPostgresFact]
    public async Task SurveyExtension_RejectsUnassignedOrganization()
    {
        var organizationIds = await CreateOrganizationsAsync(2);
        var survey = await CreateSurveyAsync([organizationIds[0]]);
        var service = new SurveyService(_connectionFactory, _surveyRepository, _clock);

        var result = await service.SaveExtensionsAsync(new SurveyExtensionRequest
        {
            SurveyId = survey.SurveyId!.Value,
            Extensions =
            [
                new SurveyExtensionItemRequest
                {
                    OrganizationId = organizationIds[1],
                    ExtendedUntil = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd")
                }
            ]
        });

        await using var connection = _fixture.CreateConnection();
        var assignments = (await connection.QueryAsync<AssignmentDateRow>(
            """
            SELECT id_organization AS OrganizationId, date_begin AS DateBegin, date_end AS DateEnd
            FROM public.organization_survey
            WHERE id_survey = @SurveyId;
            """,
            new { SurveyId = survey.SurveyId })).ToArray();

        Assert.False(result.Success);
        Assert.Equal("Продлить анкету можно только для уже назначенных организаций.", result.Message);
        var assignment = Assert.Single(assignments);
        Assert.Equal(organizationIds[0], assignment.OrganizationId);
        Assert.Equal(DateTime.Today.AddDays(14), assignment.DateEnd);
    }

    [RequiresPostgresFact]
    public async Task SurveyLists_DefaultToStartDateDescending()
    {
        var organizationId = Assert.Single(await CreateOrganizationsAsync(1));
        await using var connection = _fixture.CreateConnection();

        async Task<int> CreateSurveyWithPeriodAsync(string name, DateTime dateBegin, DateTime dateEnd)
        {
            var surveyId = await connection.ExecuteScalarAsync<int>(
                """
                INSERT INTO public.survey (name_survey, description, date_begin, date_end)
                VALUES (@Name, 'Проверка сортировки', @DateBegin, @DateEnd)
                RETURNING id_survey;
                """,
                new { Name = name, DateBegin = dateBegin.Date, DateEnd = dateEnd.Date });
            await connection.ExecuteAsync(
                """
                INSERT INTO public.organization_survey
                    (id_organization, id_survey, date_begin, date_end)
                VALUES
                    (@OrganizationId, @SurveyId, @DateBegin, @DateEnd);
                """,
                new
                {
                    OrganizationId = organizationId,
                    SurveyId = surveyId,
                    DateBegin = dateBegin.Date,
                    DateEnd = dateEnd.Date
                });
            return surveyId;
        }

        var activeOlderId = await CreateSurveyWithPeriodAsync(
            "Активная ранняя",
            DateTime.Today.AddDays(-5),
            DateTime.Today.AddDays(10));
        var activeNewerId = await CreateSurveyWithPeriodAsync(
            "Активная поздняя",
            DateTime.Today.AddDays(-1),
            DateTime.Today.AddDays(10));
        var archivedOlderId = await CreateSurveyWithPeriodAsync(
            "Архивная ранняя",
            DateTime.Today.AddDays(-30),
            DateTime.Today.AddDays(-20));
        var archivedNewerId = await CreateSurveyWithPeriodAsync(
            "Архивная поздняя",
            DateTime.Today.AddDays(-15),
            DateTime.Today.AddDays(-5));

        var service = new SurveyService(_connectionFactory, _surveyRepository, _clock);
        var activePage = await service.GetSurveysPageAsync(1, null, null, null);
        var archivePage = await service.GetAdminArchivedSurveysPageAsync(
            1,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        Assert.Equal(
            new[] { activeNewerId, activeOlderId },
            activePage.SurveyRows.Select(row => row.IdSurvey));
        Assert.Equal(
            new[] { archivedNewerId, archivedOlderId },
            archivePage.SurveyRows.Select(row => row.IdSurvey));
    }

    [RequiresPostgresFact]
    public async Task SurveyExtension_RejectsDateNotLaterThanCurrentAssignmentEndDate()
    {
        var organizationIds = await CreateOrganizationsAsync(1);
        var survey = await CreateSurveyAsync(organizationIds);
        var service = new SurveyService(_connectionFactory, _surveyRepository, _clock);

        var result = await service.SaveExtensionsAsync(new SurveyExtensionRequest
        {
            SurveyId = survey.SurveyId!.Value,
            Extensions =
            [
                new SurveyExtensionItemRequest
                {
                    OrganizationId = organizationIds[0],
                    ExtendedUntil = DateTime.Today.AddDays(10).ToString("yyyy-MM-dd")
                }
            ]
        });

        await using var connection = _fixture.CreateConnection();
        var dateEnd = await connection.ExecuteScalarAsync<DateTime>(
            """
            SELECT date_end
            FROM public.organization_survey
            WHERE id_survey = @SurveyId
              AND id_organization = @OrganizationId;
            """,
            new { SurveyId = survey.SurveyId, OrganizationId = organizationIds[0] });

        Assert.False(result.Success);
        Assert.Equal("Новая дата конца должна быть позже текущей даты конца назначения.", result.Message);
        Assert.Equal(DateTime.Today.AddDays(14), dateEnd);
    }

    [RequiresPostgresFact]
    public async Task SurveyExtension_ListsOnlyUnansweredOrganizationsAndRejectsAnsweredAssignment()
    {
        var organizationIds = await CreateOrganizationsAsync(2);
        var userId = await CreateUserAsync(organizationIds[0], "extension-answered-client");
        var survey = await CreateSurveyAsync(organizationIds);
        var service = new SurveyService(_connectionFactory, _surveyRepository, _clock);
        var answerResult = await CreateAnswerService(userId).InsertAnswerAsync(
            BuildAnswerRecord(survey.SurveyId!.Value, organizationIds[0], 4));

        var availableOrganizations = await service.GetAssignedOrganizationsForExtensionAsync(
            survey.SurveyId.Value);
        var extensionResult = await service.SaveExtensionsAsync(new SurveyExtensionRequest
        {
            SurveyId = survey.SurveyId.Value,
            Extensions =
            [
                new SurveyExtensionItemRequest
                {
                    OrganizationId = organizationIds[0],
                    ExtendedUntil = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd")
                }
            ]
        });

        await using var connection = _fixture.CreateConnection();
        var answeredAssignmentEnd = await connection.ExecuteScalarAsync<DateTime>(
            """
            SELECT date_end
            FROM public.organization_survey
            WHERE id_survey = @SurveyId
              AND id_organization = @OrganizationId;
            """,
            new { SurveyId = survey.SurveyId, OrganizationId = organizationIds[0] });

        Assert.True(answerResult.Success, answerResult.Error);
        var availableOrganization = Assert.Single(availableOrganizations);
        Assert.Equal(organizationIds[1], availableOrganization.Id);
        Assert.False(extensionResult.Success);
        Assert.Equal(
            "Нельзя продлить доступ: организация уже отправила ответ по анкете.",
            extensionResult.Message);
        Assert.Equal(DateTime.Today.AddDays(14), answeredAssignmentEnd);
    }

    [RequiresPostgresFact]
    public async Task ExtensionEndDateUpdate_ChangesOnlySelectedAssignmentEndDate()
    {
        var organizationIds = await CreateOrganizationsAsync(2);
        var survey = await CreateSurveyAsync(organizationIds);
        var service = new SurveyService(_connectionFactory, _surveyRepository, _clock);

        var extensionResult = await service.SaveExtensionsAsync(new SurveyExtensionRequest
        {
            SurveyId = survey.SurveyId!.Value,
            Extensions =
            [
                new SurveyExtensionItemRequest
                {
                    OrganizationId = organizationIds[0],
                    ExtendedUntil = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd")
                }
            ]
        });
        var updateResult = await service.UpdateExtensionPeriodAsync(
            survey.SurveyId.Value,
            organizationIds[0],
            new SurveyAssignmentPeriodRequest
            {
                DateEnd = DateTime.Today.AddDays(45)
            });

        await using var connection = _fixture.CreateConnection();
        var basePeriod = await connection.QuerySingleAsync<SurveyDateRow>(
            """
            SELECT date_begin AS DateBegin, date_end AS DateEnd
            FROM public.survey
            WHERE id_survey = @SurveyId;
            """,
            new { SurveyId = survey.SurveyId });
        var assignments = (await connection.QueryAsync<AssignmentDateRow>(
            """
            SELECT id_organization AS OrganizationId, date_begin AS DateBegin, date_end AS DateEnd
            FROM public.organization_survey
            WHERE id_survey = @SurveyId
            ORDER BY id_organization;
            """,
            new { SurveyId = survey.SurveyId })).ToArray();

        Assert.True(extensionResult.Success, extensionResult.Message);
        Assert.True(updateResult.Success, updateResult.Message);
        Assert.Equal(DateTime.Today.AddDays(-1), basePeriod.DateBegin);
        Assert.Equal(DateTime.Today.AddDays(14), basePeriod.DateEnd);
        Assert.Equal(DateTime.Today.AddDays(-1), assignments[0].DateBegin);
        Assert.Equal(DateTime.Today.AddDays(45), assignments[0].DateEnd);
        Assert.Equal(DateTime.Today.AddDays(-1), assignments[1].DateBegin);
        Assert.Equal(DateTime.Today.AddDays(14), assignments[1].DateEnd);
    }

    [RequiresPostgresFact]
    public async Task ExtensionEndDateUpdate_RejectsDateBeforeBaseSurveyEndDate()
    {
        var organizationIds = await CreateOrganizationsAsync(1);
        var survey = await CreateSurveyAsync(organizationIds);
        var service = new SurveyService(_connectionFactory, _surveyRepository, _clock);
        var extensionEnd = DateTime.Today.AddDays(30);

        var extensionResult = await service.SaveExtensionsAsync(new SurveyExtensionRequest
        {
            SurveyId = survey.SurveyId!.Value,
            Extensions =
            [
                new SurveyExtensionItemRequest
                {
                    OrganizationId = organizationIds[0],
                    ExtendedUntil = extensionEnd.ToString("yyyy-MM-dd")
                }
            ]
        });
        var updateResult = await service.UpdateExtensionPeriodAsync(
            survey.SurveyId.Value,
            organizationIds[0],
            new SurveyAssignmentPeriodRequest
            {
                DateEnd = DateTime.Today.AddDays(10)
            });

        await using var connection = _fixture.CreateConnection();
        var dateEnd = await connection.ExecuteScalarAsync<DateTime>(
            """
            SELECT date_end
            FROM public.organization_survey
            WHERE id_survey = @SurveyId
              AND id_organization = @OrganizationId;
            """,
            new { SurveyId = survey.SurveyId, OrganizationId = organizationIds[0] });

        Assert.True(extensionResult.Success, extensionResult.Message);
        Assert.False(updateResult.Success);
        Assert.Equal("Дата конца продления не может быть раньше даты конца анкеты.", updateResult.Message);
        Assert.Equal(extensionEnd, dateEnd);
    }

    [RequiresPostgresFact]
    public async Task ExtensionEndDateUpdate_BaseSurveyEndDateCancelsExtension()
    {
        var organizationIds = await CreateOrganizationsAsync(1);
        var survey = await CreateSurveyAsync(organizationIds);
        var service = new SurveyService(_connectionFactory, _surveyRepository, _clock);
        var baseEnd = DateTime.Today.AddDays(14);

        var extensionResult = await service.SaveExtensionsAsync(new SurveyExtensionRequest
        {
            SurveyId = survey.SurveyId!.Value,
            Extensions =
            [
                new SurveyExtensionItemRequest
                {
                    OrganizationId = organizationIds[0],
                    ExtendedUntil = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd")
                }
            ]
        });
        var updateResult = await service.UpdateExtensionPeriodAsync(
            survey.SurveyId.Value,
            organizationIds[0],
            new SurveyAssignmentPeriodRequest { DateEnd = baseEnd });

        await using var connection = _fixture.CreateConnection();
        var assignment = await connection.QuerySingleAsync<AssignmentDateRow>(
            """
            SELECT id_organization AS OrganizationId, date_begin AS DateBegin, date_end AS DateEnd
            FROM public.organization_survey
            WHERE id_survey = @SurveyId
              AND id_organization = @OrganizationId;
            """,
            new { SurveyId = survey.SurveyId, OrganizationId = organizationIds[0] });
        var activePage = await service.GetSurveysPageAsync(1, null, null, null);

        Assert.True(extensionResult.Success, extensionResult.Message);
        Assert.True(updateResult.Success, updateResult.Message);
        Assert.Equal("Продление успешно отменено.", updateResult.Message);
        Assert.Equal(DateTime.Today.AddDays(-1), assignment.DateBegin);
        Assert.Equal(baseEnd, assignment.DateEnd);
        Assert.DoesNotContain(
            activePage.SurveyRows,
            row => row.IdSurvey == survey.SurveyId && row.IsExtension);
    }

    [RequiresPostgresFact]
    public async Task ExtensionDeletion_RestoresBasePeriodAndPreservesAssignmentAndDraft()
    {
        var organizationIds = await CreateOrganizationsAsync(2);
        var userId = await CreateUserAsync(organizationIds[0], "extension-reset-draft-client");
        var survey = await CreateSurveyAsync(organizationIds);
        var service = new SurveyService(_connectionFactory, _surveyRepository, _clock);

        var extensionResult = await service.SaveExtensionsAsync(new SurveyExtensionRequest
        {
            SurveyId = survey.SurveyId!.Value,
            Extensions =
            [
                new SurveyExtensionItemRequest
                {
                    OrganizationId = organizationIds[0],
                    ExtendedUntil = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd")
                }
            ]
        });
        var draftResult = await CreateAnswerService(userId).SaveDraftAnswerAsync(
            BuildAnswerRecord(survey.SurveyId.Value, organizationIds[0], 4));
        var deletionResult = await service.DeleteExtensionAsync(
            survey.SurveyId.Value,
            organizationIds[0]);

        await using var connection = _fixture.CreateConnection();
        var surveyCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM public.survey WHERE id_survey = @SurveyId;",
            new { SurveyId = survey.SurveyId });
        var assignments = (await connection.QueryAsync<AssignmentDateRow>(
            """
            SELECT
                id_organization_survey AS AssignmentId,
                id_organization AS OrganizationId,
                date_begin AS DateBegin,
                date_end AS DateEnd
            FROM public.organization_survey
            WHERE id_survey = @SurveyId
            ORDER BY id_organization;
            """,
            new { SurveyId = survey.SurveyId })).ToArray();
        var draftCount = await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM public.answer_draft draft
            INNER JOIN public.organization_survey assignment
                ON assignment.id_organization_survey = draft.id_organization_survey
            WHERE assignment.id_survey = @SurveyId
              AND assignment.id_organization = @OrganizationId;
            """,
            new { SurveyId = survey.SurveyId, OrganizationId = organizationIds[0] });
        var activePage = await service.GetSurveysPageAsync(1, null, null, null);

        Assert.True(extensionResult.Success, extensionResult.Message);
        Assert.True(draftResult.Success, draftResult.Error);
        Assert.True(deletionResult.Success, deletionResult.Message);
        Assert.Equal("Продление успешно удалено.", deletionResult.Message);
        Assert.Equal(1, surveyCount);
        Assert.Equal(2, assignments.Length);
        Assert.All(assignments, assignment =>
        {
            Assert.True(assignment.AssignmentId > 0);
            Assert.Equal(DateTime.Today.AddDays(-1), assignment.DateBegin);
            Assert.Equal(DateTime.Today.AddDays(14), assignment.DateEnd);
        });
        Assert.Equal(1, draftCount);
        Assert.Contains(
            activePage.SurveyRows,
            row => row.IdSurvey == survey.SurveyId
                && !row.IsExtension
                && row.OrganizationIds.SequenceEqual(organizationIds));
        Assert.DoesNotContain(
            activePage.SurveyRows,
            row => row.IdSurvey == survey.SurveyId && row.IsExtension);
    }

    [RequiresPostgresFact]
    public async Task ExtensionDeletion_RestoresBasePeriodAndPreservesAnswer()
    {
        var organizationIds = await CreateOrganizationsAsync(2);
        var userId = await CreateUserAsync(organizationIds[0], "extension-deletion-client");
        var survey = await CreateSurveyAsync(organizationIds);
        var service = new SurveyService(_connectionFactory, _surveyRepository, _clock);

        var extensionResult = await service.SaveExtensionsAsync(new SurveyExtensionRequest
        {
            SurveyId = survey.SurveyId!.Value,
            Extensions =
            [
                new SurveyExtensionItemRequest
                {
                    OrganizationId = organizationIds[0],
                    ExtendedUntil = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd")
                }
            ]
        });
        var answerResult = await CreateAnswerService(userId).InsertAnswerAsync(
            BuildAnswerRecord(survey.SurveyId.Value, organizationIds[0], 4));
        var deletionResult = await service.DeleteExtensionAsync(
            survey.SurveyId.Value,
            organizationIds[0]);

        await using var connection = _fixture.CreateConnection();
        var assignment = await connection.QuerySingleAsync<AssignmentDateRow>(
            """
            SELECT
                id_organization_survey AS AssignmentId,
                id_organization AS OrganizationId,
                date_begin AS DateBegin,
                date_end AS DateEnd
            FROM public.organization_survey
            WHERE id_survey = @SurveyId
              AND id_organization = @OrganizationId;
            """,
            new { SurveyId = survey.SurveyId, OrganizationId = organizationIds[0] });
        var answerCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM public.answer WHERE id_organization_survey = @AssignmentId;",
            new { assignment.AssignmentId });

        Assert.True(extensionResult.Success, extensionResult.Message);
        Assert.True(answerResult.Success, answerResult.Error);
        Assert.True(deletionResult.Success, deletionResult.Message);
        Assert.Equal(DateTime.Today.AddDays(-1), assignment.DateBegin);
        Assert.Equal(DateTime.Today.AddDays(14), assignment.DateEnd);
        Assert.Equal(1, answerCount);
    }

    [RequiresPostgresFact]
    public async Task ExtensionDeletion_RejectsAnswerCompletedAfterBaseSurveyEndDate()
    {
        var organizationId = Assert.Single(await CreateOrganizationsAsync(1));
        var userId = await CreateUserAsync(organizationId, "extension-history-delete-client");
        var survey = await CreateSurveyAsync([organizationId]);
        var service = new SurveyService(_connectionFactory, _surveyRepository, _clock);
        var extensionEnd = DateTime.Today.AddDays(30);
        var baseEnd = DateTime.Today.AddDays(-1);

        var extensionResult = await service.SaveExtensionsAsync(new SurveyExtensionRequest
        {
            SurveyId = survey.SurveyId!.Value,
            Extensions =
            [
                new SurveyExtensionItemRequest
                {
                    OrganizationId = organizationId,
                    ExtendedUntil = extensionEnd.ToString("yyyy-MM-dd")
                }
            ]
        });

        await using (var connection = _fixture.CreateConnection())
        {
            await connection.ExecuteAsync(
                "UPDATE public.survey SET date_end = @BaseEnd WHERE id_survey = @SurveyId;",
                new { BaseEnd = baseEnd, SurveyId = survey.SurveyId });
        }

        var answerResult = await CreateAnswerService(userId).InsertAnswerAsync(
            BuildAnswerRecord(survey.SurveyId.Value, organizationId, 4));
        var deletionResult = await service.DeleteExtensionAsync(survey.SurveyId.Value, organizationId);

        await using var verificationConnection = _fixture.CreateConnection();
        var assignmentEnd = await verificationConnection.ExecuteScalarAsync<DateTime>(
            """
            SELECT date_end
            FROM public.organization_survey
            WHERE id_survey = @SurveyId
              AND id_organization = @OrganizationId;
            """,
            new { SurveyId = survey.SurveyId, OrganizationId = organizationId });

        Assert.True(extensionResult.Success, extensionResult.Message);
        Assert.True(answerResult.Success, answerResult.Error);
        Assert.False(deletionResult.Success);
        Assert.Equal("extension_answer_in_extended_period", deletionResult.Code);
        Assert.Equal(
            "Нельзя удалить продление: по анкете был отправлен ответ в продлённый период.",
            deletionResult.Message);
        Assert.Equal(extensionEnd, assignmentEnd);
    }

    [RequiresPostgresFact]
    public async Task ExtensionEndDateUpdate_BaseDateRejectsAnswerCompletedInExtendedPeriod()
    {
        var organizationId = Assert.Single(await CreateOrganizationsAsync(1));
        var userId = await CreateUserAsync(organizationId, "extension-history-edit-client");
        var survey = await CreateSurveyAsync([organizationId]);
        var service = new SurveyService(_connectionFactory, _surveyRepository, _clock);
        var extensionEnd = DateTime.Today.AddDays(30);
        var baseEnd = DateTime.Today.AddDays(-1);

        var extensionResult = await service.SaveExtensionsAsync(new SurveyExtensionRequest
        {
            SurveyId = survey.SurveyId!.Value,
            Extensions =
            [
                new SurveyExtensionItemRequest
                {
                    OrganizationId = organizationId,
                    ExtendedUntil = extensionEnd.ToString("yyyy-MM-dd")
                }
            ]
        });

        await using (var connection = _fixture.CreateConnection())
        {
            await connection.ExecuteAsync(
                "UPDATE public.survey SET date_end = @BaseEnd WHERE id_survey = @SurveyId;",
                new { BaseEnd = baseEnd, SurveyId = survey.SurveyId });
        }

        var answerResult = await CreateAnswerService(userId).InsertAnswerAsync(
            BuildAnswerRecord(survey.SurveyId.Value, organizationId, 4));
        var updateResult = await service.UpdateExtensionPeriodAsync(
            survey.SurveyId.Value,
            organizationId,
            new SurveyAssignmentPeriodRequest { DateEnd = baseEnd });

        await using var verificationConnection = _fixture.CreateConnection();
        var assignmentEnd = await verificationConnection.ExecuteScalarAsync<DateTime>(
            """
            SELECT date_end
            FROM public.organization_survey
            WHERE id_survey = @SurveyId
              AND id_organization = @OrganizationId;
            """,
            new { SurveyId = survey.SurveyId, OrganizationId = organizationId });

        Assert.True(extensionResult.Success, extensionResult.Message);
        Assert.True(answerResult.Success, answerResult.Error);
        Assert.False(updateResult.Success);
        Assert.Equal("extension_answer_in_extended_period", updateResult.Code);
        Assert.Equal(
            "Нельзя удалить продление: по анкете был отправлен ответ в продлённый период.",
            updateResult.Message);
        Assert.Equal(extensionEnd, assignmentEnd);
    }

    [RequiresPostgresFact]
    public async Task AdminLists_MoveBaseSurveyAndExtensionIndependently()
    {
        var organizationId = Assert.Single(await CreateOrganizationsAsync(1));
        var survey = await CreateSurveyAsync([organizationId]);
        var extensionEnd = DateTime.Today.AddDays(30);
        var service = new SurveyService(_connectionFactory, _surveyRepository, _clock);

        var extensionResult = await service.SaveExtensionsAsync(new SurveyExtensionRequest
        {
            SurveyId = survey.SurveyId!.Value,
            Extensions =
            [
                new SurveyExtensionItemRequest
                {
                    OrganizationId = organizationId,
                    ExtendedUntil = extensionEnd.ToString("yyyy-MM-dd")
                }
            ]
        });

        await using (var connection = _fixture.CreateConnection())
        {
            await connection.ExecuteAsync(
                """
                UPDATE public.survey
                SET date_end = CURRENT_DATE - 1
                WHERE id_survey = @SurveyId;
                """,
                new { SurveyId = survey.SurveyId });
        }

        var activeWithExtension = await service.GetSurveysPageAsync(1, null, null, null);
        var archiveWithBase = await service.GetAdminArchivedSurveysPageAsync(
            1, null, null, null, null, null, null, null, null);

        Assert.True(extensionResult.Success, extensionResult.Message);
        Assert.Contains(activeWithExtension.SurveyRows, row => row.NameSurvey == "Интеграционная анкета");
        Assert.Contains(
            activeWithExtension.SurveyRows,
            row => row.NameSurvey == "Интеграционная анкета: продление для Орг 1"
                && row.DateEnd == extensionEnd);
        Assert.Contains(
            archiveWithBase.SurveyRows,
            row => row.NameSurvey == "Интеграционная анкета"
                && row.DateEnd == DateTime.Today.AddDays(-1));
        Assert.DoesNotContain(
            archiveWithBase.SurveyRows,
            row => row.NameSurvey.Contains(": продление для", StringComparison.Ordinal));

        var futureClock = new FixedClock(extensionEnd.AddDays(1));
        var futureService = new SurveyService(
            _connectionFactory,
            new SurveyRepository(futureClock),
            futureClock);
        var activeAfterExtension = await futureService.GetSurveysPageAsync(1, null, null, null);
        var archiveAfterExtension = await futureService.GetAdminArchivedSurveysPageAsync(
            1, null, null, null, null, null, null, null, null);

        Assert.DoesNotContain(activeAfterExtension.SurveyRows, row => row.IdSurvey == survey.SurveyId);
        Assert.Contains(archiveAfterExtension.SurveyRows, row => row.NameSurvey == "Интеграционная анкета");
        Assert.Contains(
            archiveAfterExtension.SurveyRows,
            row => row.NameSurvey == "Интеграционная анкета: продление для Орг 1");
    }

    [RequiresPostgresFact]
    public async Task ArchivedSurveyExtension_MovesOriginalAndExtensionRowsToActiveList()
    {
        var organizationId = Assert.Single(await CreateOrganizationsAsync(1));
        var survey = await CreateSurveyAsync([organizationId]);
        var service = new SurveyService(_connectionFactory, _surveyRepository, _clock);
        var baseEnd = DateTime.Today.AddDays(-1);
        var extensionEnd = DateTime.Today.AddDays(20);

        await using (var connection = _fixture.CreateConnection())
        {
            await connection.ExecuteAsync(
                """
                UPDATE public.organization_survey
                SET date_end = @BaseEnd
                WHERE id_survey = @SurveyId;

                UPDATE public.survey
                SET date_end = @BaseEnd
                WHERE id_survey = @SurveyId;
                """,
                new { BaseEnd = baseEnd, SurveyId = survey.SurveyId });
        }

        var extensionResult = await service.SaveExtensionsAsync(new SurveyExtensionRequest
        {
            SurveyId = survey.SurveyId!.Value,
            Extensions =
            [
                new SurveyExtensionItemRequest
                {
                    OrganizationId = organizationId,
                    ExtendedUntil = extensionEnd.ToString("yyyy-MM-dd")
                }
            ]
        });
        var activePage = await service.GetSurveysPageAsync(1, null, null, null);
        var archivePage = await service.GetAdminArchivedSurveysPageAsync(
            1, null, null, null, null, null, null, null, null);

        Assert.True(extensionResult.Success, extensionResult.Message);
        var activeRows = activePage.SurveyRows
            .Where(row => row.IdSurvey == survey.SurveyId)
            .ToArray();
        Assert.Equal(2, activeRows.Length);
        Assert.False(activeRows[0].IsExtension);
        Assert.Equal(baseEnd, activeRows[0].DateEnd);
        Assert.True(activeRows[1].IsExtension);
        Assert.Equal(extensionEnd, activeRows[1].DateEnd);
        Assert.Contains(
            activePage.SurveyRows,
            row => row.IdSurvey == survey.SurveyId && !row.IsExtension);
        Assert.Contains(
            archivePage.SurveyRows,
            row => row.IdSurvey == survey.SurveyId && !row.IsExtension && row.DateEnd == baseEnd);
        Assert.DoesNotContain(
            archivePage.SurveyRows,
            row => row.IdSurvey == survey.SurveyId && row.IsExtension);
    }

    [RequiresPostgresFact]
    public async Task ArchivedSurveyExtension_AllowsExtendingUntilToday()
    {
        var organizationId = Assert.Single(await CreateOrganizationsAsync(1));
        var survey = await CreateSurveyAsync([organizationId]);
        var service = new SurveyService(_connectionFactory, _surveyRepository, _clock);
        var archivedEnd = DateTime.Today.AddDays(-1);

        await using (var connection = _fixture.CreateConnection())
        {
            await connection.ExecuteAsync(
                """
                UPDATE public.organization_survey
                SET date_end = @ArchivedEnd
                WHERE id_survey = @SurveyId;

                UPDATE public.survey
                SET date_end = @ArchivedEnd
                WHERE id_survey = @SurveyId;
                """,
                new { ArchivedEnd = archivedEnd, SurveyId = survey.SurveyId });
        }

        var extensionResult = await service.SaveExtensionsAsync(new SurveyExtensionRequest
        {
            SurveyId = survey.SurveyId!.Value,
            Extensions =
            [
                new SurveyExtensionItemRequest
                {
                    OrganizationId = organizationId,
                    ExtendedUntil = DateTime.Today.ToString("yyyy-MM-dd")
                }
            ]
        });

        await using var verificationConnection = _fixture.CreateConnection();
        var assignmentEnd = await verificationConnection.ExecuteScalarAsync<DateTime>(
            """
            SELECT date_end
            FROM public.organization_survey
            WHERE id_survey = @SurveyId
              AND id_organization = @OrganizationId;
            """,
            new { SurveyId = survey.SurveyId, OrganizationId = organizationId });

        Assert.True(extensionResult.Success, extensionResult.Message);
        Assert.Equal(DateTime.Today, assignmentEnd);
    }

    [RequiresPostgresFact]
    public async Task AssignmentRepository_ArchivesExpiredAndFutureSurveysWithoutAnswers()
    {
        var organizationIds = await CreateOrganizationsAsync(1);
        var expiredSurvey = await CreateSurveyAsync(organizationIds);
        var futureSurvey = await CreateSurveyAsync(organizationIds);
        await using var userConnection = _fixture.CreateConnection();
        var userId = await userConnection.ExecuteScalarAsync<int>(
            """
            INSERT INTO public.app_user (id_organization, login, full_name, role, password, date_begin)
            VALUES (@OrganizationId, 'archive-boundary-client', 'Клиент проверки архива', 'user', 'hash', CURRENT_DATE)
            RETURNING id_user;
            """,
            new { OrganizationId = organizationIds[0] });
        var activePage = await new SurveyService(_connectionFactory, _surveyRepository, _clock)
            .GetSurveysPageAsync(1, null, null, null);

        Assert.Contains(activePage.SurveyRows, row => row.IdSurvey == expiredSurvey.SurveyId);
        Assert.Contains(activePage.SurveyRows, row => row.IdSurvey == futureSurvey.SurveyId);

        await using (var connection = _fixture.CreateConnection())
        {
            await connection.ExecuteAsync(
                """
                UPDATE public.organization_survey
                SET date_end = CURRENT_DATE - 1
                WHERE id_survey = @SurveyId;

                UPDATE public.survey
                SET date_end = CURRENT_DATE - 1
                WHERE id_survey = @SurveyId;
                """,
                new { SurveyId = expiredSurvey.SurveyId });
            await connection.ExecuteAsync(
                """
                UPDATE public.organization_survey
                SET
                    date_begin = CURRENT_DATE + 1,
                    date_end = CURRENT_DATE + 10
                WHERE id_survey = @SurveyId;

                UPDATE public.survey
                SET
                    date_begin = CURRENT_DATE + 1,
                    date_end = CURRENT_DATE + 10
                WHERE id_survey = @SurveyId;
                """,
                new { SurveyId = futureSurvey.SurveyId });
        }

        var surveyService = new SurveyService(_connectionFactory, _surveyRepository, _clock);
        var archivePage = await surveyService
            .GetAdminArchivedSurveysPageAsync(1, null, null, null, null, null, null, null, null);
        var activeAfterArchive = await surveyService.GetSurveysPageAsync(1, null, null, null);
        var archivedSurveys = await surveyService.GetAdminArchivedSurveysAsync();
        var activeSurveys = await surveyService.GetSurveysAsync();
        var clientActivePage = await surveyService.GetActiveSurveysPageAsync(userId, 1, null);
        var clientArchivePage = await surveyService
            .GetUserArchivePageAsync(userId, 1, null, null, null, null, signedOnly: false);

        Assert.DoesNotContain(activeAfterArchive.SurveyRows, row => row.IdSurvey == expiredSurvey.SurveyId);
        Assert.DoesNotContain(activeAfterArchive.SurveyRows, row => row.IdSurvey == futureSurvey.SurveyId);
        Assert.Contains(archivePage.SurveyRows, row => row.IdSurvey == expiredSurvey.SurveyId);
        Assert.Contains(archivePage.SurveyRows, row => row.IdSurvey == futureSurvey.SurveyId);
        Assert.DoesNotContain(activeSurveys, survey => survey.IdSurvey == expiredSurvey.SurveyId);
        Assert.DoesNotContain(activeSurveys, survey => survey.IdSurvey == futureSurvey.SurveyId);
        Assert.Contains(archivedSurveys, survey => survey.IdSurvey == expiredSurvey.SurveyId);
        Assert.Contains(archivedSurveys, survey => survey.IdSurvey == futureSurvey.SurveyId);
        Assert.NotNull(clientActivePage);
        Assert.DoesNotContain(clientActivePage!.AccessibleSurveys, survey => survey.IdSurvey == expiredSurvey.SurveyId);
        Assert.DoesNotContain(clientActivePage.AccessibleSurveys, survey => survey.IdSurvey == futureSurvey.SurveyId);
        Assert.NotNull(clientArchivePage);
        Assert.Contains(clientArchivePage!.ArchivedSurveys, survey => survey.IdSurvey == expiredSurvey.SurveyId && survey.IdAnswer == 0);
        Assert.Contains(clientArchivePage.ArchivedSurveys, survey => survey.IdSurvey == futureSurvey.SurveyId && survey.IdAnswer == 0);
    }

    [RequiresPostgresFact]
    public async Task AdminActiveSurveyFilter_UsesActiveAssignmentForSelectedOrganization()
    {
        var organizationIds = await CreateOrganizationsAsync(2);
        var sharedSurvey = await CreateSurveyAsync(organizationIds);
        var secondOrganizationSurvey = await CreateSurveyAsync([organizationIds[1]]);

        await using (var connection = _fixture.CreateConnection())
        {
            await connection.ExecuteAsync(
                """
                UPDATE public.organization_survey
                SET date_end = CURRENT_DATE - 1
                WHERE id_survey = @SurveyId
                  AND id_organization = @OrganizationId;
                """,
                new
                {
                    SurveyId = sharedSurvey.SurveyId,
                    OrganizationId = organizationIds[1]
                });
        }

        var service = new SurveyService(_connectionFactory, _surveyRepository, _clock);
        var firstOrganizationPage = await service.GetSurveysPageAsync(
            1,
            sortBy: null,
            sortDirection: null,
            organizationIds: organizationIds[0].ToString());
        var secondOrganizationPage = await service.GetSurveysPageAsync(
            1,
            sortBy: null,
            sortDirection: null,
            organizationIds: organizationIds[1].ToString());
        var bothOrganizationsPage = await service.GetSurveysPageAsync(
            1,
            sortBy: null,
            sortDirection: null,
            organizationIds: $"{organizationIds[0]},{organizationIds[1]}");

        Assert.Contains(firstOrganizationPage.SurveyRows, row => row.IdSurvey == sharedSurvey.SurveyId);
        Assert.DoesNotContain(secondOrganizationPage.SurveyRows, row => row.IdSurvey == sharedSurvey.SurveyId);
        Assert.Contains(secondOrganizationPage.SurveyRows, row => row.IdSurvey == secondOrganizationSurvey.SurveyId);
        Assert.Contains(bothOrganizationsPage.SurveyRows, row => row.IdSurvey == sharedSurvey.SurveyId);
        Assert.Contains(bothOrganizationsPage.SurveyRows, row => row.IdSurvey == secondOrganizationSurvey.SurveyId);
        Assert.Equal(new[] { organizationIds[1] }, secondOrganizationPage.FilterState.SelectedOrganizationIds);
        Assert.All(
            organizationIds,
            organizationId => Assert.Contains(
                secondOrganizationPage.FilterState.OrganizationOptions,
                option => option.Id == organizationId));
    }

    [RequiresPostgresFact]
    public async Task AdminArchiveFilters_WorkSeparatelyAndInCombination()
    {
        var organizationIds = await CreateOrganizationsAsync(2);
        var marchSurvey = await CreateSurveyAsync([organizationIds[0]]);
        var aprilSurvey = await CreateSurveyAsync([organizationIds[1]]);

        await using (var connection = _fixture.CreateConnection())
        {
            await connection.ExecuteAsync(
                """
                UPDATE public.organization_survey
                SET
                    date_begin = CASE id_survey
                        WHEN @MarchSurveyId THEN DATE '2024-03-10'
                        ELSE DATE '2024-04-10'
                    END,
                    date_end = CASE id_survey
                        WHEN @MarchSurveyId THEN DATE '2024-03-20'
                        ELSE DATE '2024-04-20'
                    END
                WHERE id_survey = ANY(@SurveyIds);
                """,
                new
                {
                    MarchSurveyId = marchSurvey.SurveyId,
                    SurveyIds = new[] { marchSurvey.SurveyId!.Value, aprilSurvey.SurveyId!.Value }
                });
            await connection.ExecuteAsync(
                """
                UPDATE public.survey
                SET
                    name_survey = CASE id_survey
                        WHEN @MarchSurveyId THEN 'Мартовская анкета'
                        ELSE 'Апрельская анкета'
                    END,
                    date_begin = CASE id_survey
                        WHEN @MarchSurveyId THEN DATE '2024-03-10'
                        ELSE DATE '2024-04-10'
                    END,
                    date_end = CASE id_survey
                        WHEN @MarchSurveyId THEN DATE '2024-03-20'
                        ELSE DATE '2024-04-20'
                    END
                WHERE id_survey = ANY(@SurveyIds);
                """,
                new
                {
                    MarchSurveyId = marchSurvey.SurveyId,
                    SurveyIds = new[] { marchSurvey.SurveyId!.Value, aprilSurvey.SurveyId!.Value }
                });
        }

        var service = new SurveyService(_connectionFactory, _surveyRepository, _clock);
        var organizationPage = await service.GetAdminArchivedSurveysPageAsync(
            1, null, null, organizationIds[0].ToString(), null, null, null, null, null);
        var surveyPage = await service.GetAdminArchivedSurveysPageAsync(
            1, null, null, null, aprilSurvey.SurveyId.ToString(), null, null, null, null);
        var yearPage = await service.GetAdminArchivedSurveysPageAsync(
            1, null, null, null, null, "2024", null, null, null);
        var monthPage = await service.GetAdminArchivedSurveysPageAsync(
            1, null, null, null, null, null, "2024-03", null, null);
        var rangePage = await service.GetAdminArchivedSurveysPageAsync(
            1, null, null, null, null, null, null, "2024-04-01", "2024-04-30");
        var combinedPage = await service.GetAdminArchivedSurveysPageAsync(
            1,
            null,
            null,
            organizationIds[0].ToString(),
            marchSurvey.SurveyId.ToString(),
            null,
            "2024-03",
            null,
            null);
        var mismatchedPage = await service.GetAdminArchivedSurveysPageAsync(
            1,
            null,
            null,
            organizationIds[0].ToString(),
            aprilSurvey.SurveyId.ToString(),
            null,
            null,
            null,
            null);

        Assert.Equal(marchSurvey.SurveyId, Assert.Single(organizationPage.SurveyRows).IdSurvey);
        Assert.Equal(aprilSurvey.SurveyId, Assert.Single(surveyPage.SurveyRows).IdSurvey);
        Assert.Equal(2, yearPage.TotalCount);
        Assert.Equal(marchSurvey.SurveyId, Assert.Single(monthPage.SurveyRows).IdSurvey);
        Assert.Equal(aprilSurvey.SurveyId, Assert.Single(rangePage.SurveyRows).IdSurvey);
        Assert.Equal(marchSurvey.SurveyId, Assert.Single(combinedPage.SurveyRows).IdSurvey);
        Assert.Empty(mismatchedPage.SurveyRows);
        Assert.Equal(2024, yearPage.FilterState.Year);
        Assert.Equal("2024-03", monthPage.FilterState.Month);
        Assert.Equal("2024-04-01", rangePage.FilterState.DateFrom);
        Assert.Equal("2024-04-30", rangePage.FilterState.DateTo);
    }

    [RequiresPostgresFact]
    public async Task SurveyNameFilters_GroupMonthlySurveyCopiesEverywhere()
    {
        var organizationId = Assert.Single(await CreateOrganizationsAsync(1));
        var firstSurvey = await CreateSurveyAsync([organizationId]);
        var secondSurvey = await CreateSurveyAsync([organizationId]);
        var surveyIds = new[] { firstSurvey.SurveyId!.Value, secondSurvey.SurveyId!.Value };
        var answerService = CreateAnswerService();

        Assert.True((await answerService.InsertAnswerAsync(
            BuildAnswerRecord(surveyIds[0], organizationId, 4))).Success);
        Assert.True((await answerService.InsertAnswerAsync(
            BuildAnswerRecord(surveyIds[1], organizationId, 5))).Success);

        await using (var connection = _fixture.CreateConnection())
        {
            await connection.ExecuteAsync(
                """
                UPDATE public.survey
                SET date_begin = DATE '2024-03-01', date_end = DATE '2024-03-31'
                WHERE id_survey = ANY(@SurveyIds);

                UPDATE public.organization_survey
                SET date_begin = DATE '2024-03-01', date_end = DATE '2024-03-31'
                WHERE id_survey = ANY(@SurveyIds);
                """,
                new { SurveyIds = surveyIds });
        }

        var userId = await CreateUserAsync(organizationId, "grouped-survey-filter-client");
        var surveyService = new SurveyService(_connectionFactory, _surveyRepository, _clock);
        var adminArchive = await surveyService.GetAdminArchivedSurveysPageAsync(
            1, null, null, null, surveyIds[1].ToString(), null, null, null, null);
        var userArchive = await surveyService.GetUserArchivePageAsync(
            userId,
            1,
            searchTerm: null,
            date: null,
            dateFrom: null,
            dateTo: null,
            signedOnly: false,
            surveyIds: surveyIds[1].ToString());
        var answerJournal = await answerService.GetAnswersPageAsync(
            1,
            null,
            null,
            null,
            surveyIds[1].ToString(),
            null,
            null,
            null,
            null);

        var adminOption = Assert.Single(adminArchive.FilterState.SurveyOptions);
        var userOption = Assert.Single(userArchive!.FilterState.SurveyOptions);
        var answerOption = Assert.Single(answerJournal.FilterState.SurveyOptions);

        Assert.Equal(surveyIds, adminOption.Ids);
        Assert.Equal(surveyIds, userOption.Ids);
        Assert.Equal(surveyIds, answerOption.Ids);
        Assert.Equal(surveyIds, adminArchive.FilterState.SelectedSurveyIds);
        Assert.Equal(surveyIds, userArchive.FilterState.SelectedSurveyIds);
        Assert.Equal(surveyIds, answerJournal.FilterState.SelectedSurveyIds);
        Assert.Equal(surveyIds, adminArchive.SurveyRows.Select(row => row.IdSurvey).Order().ToArray());
        Assert.Equal(surveyIds, userArchive.ArchivedSurveys.Select(survey => survey.IdSurvey).Order().ToArray());
        Assert.Equal(surveyIds, answerJournal.Answers.Select(answer => answer.IdSurvey).Order().ToArray());
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

        var beforeSubmission = await surveyUserService.GetActiveSurveysPageAsync(userId, 99, "Интеграционная");

        Assert.NotNull(beforeSubmission);
        Assert.Equal(1, beforeSubmission!.CurrentPage);
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
        var extensionEnd = DateTime.Today.AddDays(30);

        var extensionResult = await service.SaveExtensionsAsync(new SurveyExtensionRequest
        {
            SurveyId = survey.SurveyId!.Value,
            Extensions =
            [
                new SurveyExtensionItemRequest
                {
                    OrganizationId = organizationIds[0],
                    ExtendedUntil = extensionEnd.ToString("yyyy-MM-dd")
                }
            ]
        });

        var editPage = await service.GetSurveyEditPageAsync(survey.SurveyId!.Value);
        var result = await service.UpdateActiveSurveysWorkPeriodAsync(new SurveyWorkPeriodRequest
        {
            DateBegin = DateTime.Today,
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
        var basePeriod = await connection.QuerySingleAsync<SurveyDateRow>(
            """
            SELECT date_begin AS DateBegin, date_end AS DateEnd
            FROM public.survey
            WHERE id_survey = @SurveyId;
            """,
            new { SurveyId = survey.SurveyId });

        Assert.NotNull(editPage);
        Assert.Equal(organizationIds, editPage!.SelectedOrganizationIds);
        Assert.True(extensionResult.Success, extensionResult.Message);
        Assert.True(result.Success);
        Assert.Equal(DateTime.Today.Date, basePeriod.DateBegin);
        Assert.Equal(DateTime.Today.AddDays(10).Date, basePeriod.DateEnd);
        Assert.All(assignments, row => Assert.Equal(DateTime.Today.Date, row.DateBegin));
        Assert.Equal(extensionEnd, assignments[0].DateEnd);
        Assert.Equal(DateTime.Today.AddDays(10).Date, assignments[1].DateEnd);
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
    public async Task UpdateSurvey_WithAnswerProtectsCriteriaAndAnsweredOrganizationAssignment()
    {
        var organizationIds = await CreateOrganizationsAsync(2);
        var survey = await CreateSurveyAsync(organizationIds);
        var surveyId = survey.SurveyId!.Value;
        var service = new SurveyService(_connectionFactory, _surveyRepository, _clock);
        var answerResult = await CreateAnswerService().InsertAnswerAsync(
            BuildAnswerRecord(surveyId, organizationIds[0], 5));

        var editPage = await service.GetSurveyEditPageAsync(surveyId);
        var changedCriteriaResult = await service.UpdateSurveyAsync(surveyId, new SurveyUpdateRequest
        {
            Title = "Анкета с ответом",
            Description = "Описание",
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddDays(20),
            Organizations = organizationIds.ToList(),
            Criteria = ["Изменённый вопрос", "Второй вопрос"]
        });
        var removedAnsweredOrganizationResult = await service.UpdateSurveyAsync(surveyId, new SurveyUpdateRequest
        {
            Title = "Анкета с ответом",
            Description = "Описание",
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddDays(20),
            Organizations = [organizationIds[1]],
            Criteria = ["Первый вопрос", "Второй вопрос"]
        });
        var removeUnansweredOrganizationResult = await service.UpdateSurveyAsync(surveyId, new SurveyUpdateRequest
        {
            Title = "Анкета с защищённым ответом",
            Description = "Обновлённое описание",
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddDays(20),
            Organizations = [organizationIds[0]],
            Criteria = ["Первый вопрос", "Второй вопрос"]
        });
        var updatedEditPage = await service.GetSurveyEditPageAsync(surveyId);

        Assert.True(answerResult.Success, answerResult.Error);
        Assert.NotNull(editPage);
        Assert.True(editPage!.HasAnswers);
        Assert.Contains(organizationIds[0], editPage.SelectedOrganizationIds);
        Assert.DoesNotContain(editPage.AllOrganization, item => item.Id == organizationIds[0]);
        Assert.Contains(editPage.AllOrganization, item => item.Id == organizationIds[1]);
        Assert.False(changedCriteriaResult.Success);
        Assert.Equal("Нельзя изменить критерии: по анкете уже есть ответы.", changedCriteriaResult.Message);
        Assert.False(removedAnsweredOrganizationResult.Success);
        Assert.Equal(
            "Нельзя отменить назначение организации: по анкете уже есть ответ.",
            removedAnsweredOrganizationResult.Message);
        Assert.True(removeUnansweredOrganizationResult.Success, removeUnansweredOrganizationResult.Message);
        Assert.NotNull(updatedEditPage);
        Assert.Equal(new[] { organizationIds[0] }, updatedEditPage!.SelectedOrganizationIds);
        Assert.Equal(new[] { "Первый вопрос", "Второй вопрос" }, updatedEditPage.Criteria);
    }

    [RequiresPostgresFact]
    public async Task UpdateSurvey_UpdatesEveryStartDateAndPreservesExtensionEndDate()
    {
        var organizationIds = await CreateOrganizationsAsync(2);
        var survey = await CreateSurveyAsync(organizationIds);
        var service = new SurveyService(_connectionFactory, _surveyRepository, _clock);
        var extensionEnd = DateTime.Today.AddDays(40);
        var newStart = DateTime.Today;
        var newEnd = DateTime.Today.AddDays(20);

        var extensionResult = await service.SaveExtensionsAsync(new SurveyExtensionRequest
        {
            SurveyId = survey.SurveyId!.Value,
            Extensions =
            [
                new SurveyExtensionItemRequest
                {
                    OrganizationId = organizationIds[0],
                    ExtendedUntil = extensionEnd.ToString("yyyy-MM-dd")
                }
            ]
        });
        var updateResult = await service.UpdateSurveyAsync(survey.SurveyId.Value, new SurveyUpdateRequest
        {
            Title = "Анкета после изменения периода",
            Description = "Описание",
            StartDate = newStart,
            EndDate = newEnd,
            Organizations = organizationIds.ToList(),
            Criteria = ["Критерий после изменения"]
        });

        await using var connection = _fixture.CreateConnection();
        var basePeriod = await connection.QuerySingleAsync<SurveyDateRow>(
            """
            SELECT date_begin AS DateBegin, date_end AS DateEnd
            FROM public.survey
            WHERE id_survey = @SurveyId;
            """,
            new { SurveyId = survey.SurveyId });
        var assignments = (await connection.QueryAsync<AssignmentDateRow>(
            """
            SELECT id_organization AS OrganizationId, date_begin AS DateBegin, date_end AS DateEnd
            FROM public.organization_survey
            WHERE id_survey = @SurveyId
            ORDER BY id_organization;
            """,
            new { SurveyId = survey.SurveyId })).ToArray();

        Assert.True(extensionResult.Success, extensionResult.Message);
        Assert.True(updateResult.Success, updateResult.Message);
        Assert.Equal(newStart.Date, basePeriod.DateBegin);
        Assert.Equal(newEnd.Date, basePeriod.DateEnd);
        Assert.All(assignments, assignment => Assert.Equal(newStart.Date, assignment.DateBegin));
        Assert.Equal(extensionEnd, assignments[0].DateEnd);
        Assert.Equal(newEnd.Date, assignments[1].DateEnd);
    }

    [RequiresPostgresFact]
    public async Task UpdateSurvey_BaseEndAfterExtensionResetsAssignmentToNewBaseEnd()
    {
        var organizationIds = await CreateOrganizationsAsync(1);
        var survey = await CreateSurveyAsync(organizationIds);
        var service = new SurveyService(_connectionFactory, _surveyRepository, _clock);
        var extensionEnd = DateTime.Today.AddDays(30);
        var newBaseEnd = DateTime.Today.AddDays(40);

        var extensionResult = await service.SaveExtensionsAsync(new SurveyExtensionRequest
        {
            SurveyId = survey.SurveyId!.Value,
            Extensions =
            [
                new SurveyExtensionItemRequest
                {
                    OrganizationId = organizationIds[0],
                    ExtendedUntil = extensionEnd.ToString("yyyy-MM-dd")
                }
            ]
        });
        var updateResult = await service.UpdateSurveyAsync(survey.SurveyId.Value, new SurveyUpdateRequest
        {
            Title = "Анкета с новым базовым сроком",
            Description = "Описание",
            StartDate = DateTime.Today,
            EndDate = newBaseEnd,
            Organizations = organizationIds.ToList(),
            Criteria = ["Критерий"]
        });

        await using var connection = _fixture.CreateConnection();
        var assignmentDateEnd = await connection.ExecuteScalarAsync<DateTime>(
            """
            SELECT date_end
            FROM public.organization_survey
            WHERE id_survey = @SurveyId
              AND id_organization = @OrganizationId;
            """,
            new { SurveyId = survey.SurveyId, OrganizationId = organizationIds[0] });
        var activePage = await service.GetSurveysPageAsync(1, null, null, null);

        Assert.True(extensionResult.Success, extensionResult.Message);
        Assert.True(updateResult.Success, updateResult.Message);
        Assert.Equal(newBaseEnd, assignmentDateEnd);
        Assert.DoesNotContain(
            activePage.SurveyRows,
            row => row.IdSurvey == survey.SurveyId && row.IsExtension);
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
                """
                UPDATE public.organization_survey
                SET date_end = CURRENT_DATE - 1
                WHERE id_survey = @SurveyId;

                UPDATE public.survey
                SET date_end = CURRENT_DATE - 1
                WHERE id_survey = @SurveyId;
                """,
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
    public async Task AnswerSnapshots_UseServerQuestionTextInsteadOfClientPayload()
    {
        var organizationId = Assert.Single(await CreateOrganizationsAsync(1));
        var survey = await CreateSurveyAsync([organizationId]);
        var surveyId = survey.SurveyId!.Value;
        var workflow = CreateAnswerService();
        var answerRecord = BuildAnswerRecord(surveyId, organizationId, 4);
        answerRecord.Answers[0].QuestionText = "Подменённый вопрос из question_text";
        answerRecord.Answers[0].Text = "Подменённый вопрос из text";
        answerRecord.Answers[1].QuestionText = "Ещё один подменённый вопрос";

        var draftResult = await workflow.SaveDraftAnswerAsync(answerRecord);

        await using var connection = _fixture.CreateConnection();
        var draftQuestionTexts = (await connection.QueryAsync<string>(
            """
            SELECT item.question_text
            FROM public.answer_draft_item item
            INNER JOIN public.answer_draft draft ON draft.id_answer_draft = item.id_answer_draft
            INNER JOIN public.organization_survey assignment
                ON assignment.id_organization_survey = draft.id_organization_survey
            WHERE assignment.id_survey = @SurveyId
              AND assignment.id_organization = @OrganizationId
            ORDER BY item.question_order;
            """,
            new { SurveyId = surveyId, OrganizationId = organizationId })).ToArray();

        var submissionResult = await workflow.InsertAnswerAsync(answerRecord);
        var submittedQuestionTexts = (await connection.QueryAsync<string>(
            """
            SELECT item.question_text
            FROM public.answer_item item
            INNER JOIN public.answer answer ON answer.id_answer = item.id_answer
            INNER JOIN public.organization_survey assignment
                ON assignment.id_organization_survey = answer.id_organization_survey
            WHERE assignment.id_survey = @SurveyId
              AND assignment.id_organization = @OrganizationId
            ORDER BY item.question_order;
            """,
            new { SurveyId = surveyId, OrganizationId = organizationId })).ToArray();

        Assert.True(draftResult.Success);
        Assert.True(submissionResult.Success);
        Assert.NotNull(submissionResult.Model);
        Assert.Equal(
            new[] { "Первый вопрос", "Второй вопрос" },
            submissionResult.Model.Answers.Select(answer => answer.DisplayQuestion));
        Assert.Equal(new[] { "Первый вопрос", "Второй вопрос" }, draftQuestionTexts);
        Assert.Equal(new[] { "Первый вопрос", "Второй вопрос" }, submittedQuestionTexts);
    }

    [RequiresPostgresFact]
    public async Task RepeatedSubmission_DoesNotReplaceUnsignedAnswer()
    {
        var organizationId = Assert.Single(await CreateOrganizationsAsync(1));
        var survey = await CreateSurveyAsync([organizationId]);
        var surveyId = survey.SurveyId!.Value;
        var workflow = CreateAnswerService();
        Assert.True((await workflow.InsertAnswerAsync(BuildAnswerRecord(surveyId, organizationId, 4))).Success);

        var exception = await Assert.ThrowsAsync<AnswerAlreadySubmittedException>(() =>
            workflow.InsertAnswerAsync(BuildAnswerRecord(surveyId, organizationId, 5)));

        await using var connection = _fixture.CreateConnection();
        var storedItems = (await connection.QueryAsync<StoredAnswerItem>(
            """
            SELECT item.question_order AS QuestionOrder,
                   item.rating AS Rating,
                   item.comment AS Comment
            FROM public.answer_item item
            INNER JOIN public.answer answer ON answer.id_answer = item.id_answer
            INNER JOIN public.organization_survey assignment
                ON assignment.id_organization_survey = answer.id_organization_survey
            WHERE assignment.id_survey = @SurveyId
              AND assignment.id_organization = @OrganizationId
            ORDER BY item.question_order;
            """,
            new { SurveyId = surveyId, OrganizationId = organizationId })).ToArray();
        var answerCount = await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM public.answer answer
            INNER JOIN public.organization_survey assignment
                ON assignment.id_organization_survey = answer.id_organization_survey
            WHERE assignment.id_survey = @SurveyId
              AND assignment.id_organization = @OrganizationId;
            """,
            new { SurveyId = surveyId, OrganizationId = organizationId });

        Assert.Equal(AnswerAlreadySubmittedException.UserMessage, exception.Message);
        Assert.Equal(1, answerCount);
        Assert.Equal(4, storedItems[0].Rating);
        Assert.Equal("Нужен комментарий", storedItems[0].Comment);
        Assert.Equal(5, storedItems[1].Rating);
    }

    [RequiresPostgresFact]
    public async Task SignedDraft_UnchangedAutosavePreservesSignature()
    {
        var organizationId = Assert.Single(await CreateOrganizationsAsync(1));
        var survey = await CreateSurveyAsync([organizationId]);
        var surveyId = survey.SurveyId!.Value;
        var workflow = CreateAnswerService();
        var original = BuildAnswerRecord(surveyId, organizationId, 4);
        Assert.True((await workflow.SaveDraftAnswerAsync(original)).Success);

        var signature = Convert.ToBase64String(Encoding.UTF8.GetBytes("signed-draft"));
        Assert.True(await workflow.SaveDraftSignatureAsync(surveyId, organizationId, new AnswerSignatureSaveRequest
        {
            Signature = signature
        }));

        Assert.True((await workflow.SaveDraftAnswerAsync(original)).Success);

        await using var connection = _fixture.CreateConnection();
        var storedDraft = await connection.QuerySingleAsync<SignedDraftState>(
            """
            SELECT draft.csp AS Signature,
                   item.rating AS Rating,
                   item.comment AS Comment
            FROM public.answer_draft draft
            INNER JOIN public.organization_survey assignment
                ON assignment.id_organization_survey = draft.id_organization_survey
            INNER JOIN public.answer_draft_item item
                ON item.id_answer_draft = draft.id_answer_draft
               AND item.question_order = 1
            WHERE assignment.id_survey = @SurveyId
              AND assignment.id_organization = @OrganizationId;
            """,
            new { SurveyId = surveyId, OrganizationId = organizationId });

        Assert.Equal(signature, storedDraft.Signature);
        Assert.Equal(4, storedDraft.Rating);
        Assert.Equal("Нужен комментарий", storedDraft.Comment);
    }

    [RequiresPostgresFact]
    public async Task SignedDraft_ChangedAutosaveClearsSignatureAndSavesAnswers()
    {
        var organizationId = Assert.Single(await CreateOrganizationsAsync(1));
        var survey = await CreateSurveyAsync([organizationId]);
        var surveyId = survey.SurveyId!.Value;
        var workflow = CreateAnswerService();
        Assert.True((await workflow.SaveDraftAnswerAsync(BuildAnswerRecord(surveyId, organizationId, 4))).Success);

        Assert.True(await workflow.SaveDraftSignatureAsync(surveyId, organizationId, new AnswerSignatureSaveRequest
        {
            Signature = Convert.ToBase64String(Encoding.UTF8.GetBytes("signed-draft"))
        }));
        Assert.True((await workflow.SaveDraftAnswerAsync(BuildAnswerRecord(surveyId, organizationId, 5))).Success);

        await using var connection = _fixture.CreateConnection();
        var storedDraft = await connection.QuerySingleAsync<SignedDraftState>(
            """
            SELECT draft.csp AS Signature,
                   item.rating AS Rating,
                   item.comment AS Comment
            FROM public.answer_draft draft
            INNER JOIN public.organization_survey assignment
                ON assignment.id_organization_survey = draft.id_organization_survey
            INNER JOIN public.answer_draft_item item
                ON item.id_answer_draft = draft.id_answer_draft
               AND item.question_order = 1
            WHERE assignment.id_survey = @SurveyId
              AND assignment.id_organization = @OrganizationId;
            """,
            new { SurveyId = surveyId, OrganizationId = organizationId });

        Assert.Null(storedDraft.Signature);
        Assert.Equal(5, storedDraft.Rating);
        Assert.Null(storedDraft.Comment);
    }

    [RequiresPostgresFact]
    public async Task SignedDraft_CanBeSignedAgainBeforeSubmission()
    {
        var organizationId = Assert.Single(await CreateOrganizationsAsync(1));
        var survey = await CreateSurveyAsync([organizationId]);
        var surveyId = survey.SurveyId!.Value;
        var workflow = CreateAnswerService();
        Assert.True((await workflow.SaveDraftAnswerAsync(BuildAnswerRecord(surveyId, organizationId, 4))).Success);

        var firstSignature = Convert.ToBase64String(Encoding.UTF8.GetBytes("first-signature"));
        var secondSignature = Convert.ToBase64String(Encoding.UTF8.GetBytes("second-signature"));
        Assert.True(await workflow.SaveDraftSignatureAsync(surveyId, organizationId, new AnswerSignatureSaveRequest
        {
            Signature = firstSignature
        }));
        Assert.True(await workflow.SaveDraftSignatureAsync(surveyId, organizationId, new AnswerSignatureSaveRequest
        {
            Signature = secondSignature
        }));

        await using var connection = _fixture.CreateConnection();
        var storedSignature = await connection.ExecuteScalarAsync<string>(
            """
            SELECT draft.csp
            FROM public.answer_draft draft
            INNER JOIN public.organization_survey assignment
                ON assignment.id_organization_survey = draft.id_organization_survey
            WHERE assignment.id_survey = @SurveyId
              AND assignment.id_organization = @OrganizationId;
            """,
            new { SurveyId = surveyId, OrganizationId = organizationId });

        Assert.Equal(secondSignature, storedSignature);
    }

    [RequiresPostgresFact]
    public async Task AnswerAndDraftSignatures_CannotBeSavedAfterAssignmentExpires()
    {
        var organizationIds = await CreateOrganizationsAsync(2);
        var survey = await CreateSurveyAsync(organizationIds);
        var surveyId = survey.SurveyId!.Value;
        var workflow = CreateAnswerService();
        Assert.True((await workflow.InsertAnswerAsync(BuildAnswerRecord(surveyId, organizationIds[0], 5))).Success);
        Assert.True((await workflow.SaveDraftAnswerAsync(BuildAnswerRecord(surveyId, organizationIds[1], 4))).Success);

        await using (var connection = _fixture.CreateConnection())
        {
            await connection.ExecuteAsync(
                """
                UPDATE public.organization_survey
                SET date_end = CURRENT_DATE - 1
                WHERE id_survey = @SurveyId;
                """,
                new { SurveyId = surveyId });
        }

        var signatureRequest = new AnswerSignatureSaveRequest
        {
            Signature = Convert.ToBase64String(Encoding.UTF8.GetBytes("late-signature"))
        };
        await Assert.ThrowsAsync<AnswerSigningClosedException>(() =>
            workflow.SaveSignatureAsync(surveyId, organizationIds[0], signatureRequest));
        await Assert.ThrowsAsync<AnswerSigningClosedException>(() =>
            workflow.SaveDraftSignatureAsync(surveyId, organizationIds[1], signatureRequest));

        await using var verificationConnection = _fixture.CreateConnection();
        var savedSignatureCount = await verificationConnection.ExecuteScalarAsync<int>(
            """
            SELECT
                (SELECT COUNT(*) FROM public.answer WHERE COALESCE(BTRIM(csp), '') <> '')
              + (SELECT COUNT(*) FROM public.answer_draft WHERE COALESCE(BTRIM(csp), '') <> '');
            """);

        Assert.Equal(0, savedSignatureCount);
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
    public async Task ImportedSignature_DoesNotAssumeSubmitterWhenCmsCannotBeRead()
    {
        var organizationId = Assert.Single(await CreateOrganizationsAsync(1));
        var userId = await CreateUserAsync(organizationId, "legacy-signature-client");
        var survey = await CreateSurveyAsync([organizationId]);
        var workflow = CreateAnswerService(userId);
        var submission = await workflow.InsertAnswerAsync(
            BuildAnswerRecord(survey.SurveyId!.Value, organizationId, 5));
        Assert.True(submission.Success, submission.Error);

        await using var connection = _fixture.CreateConnection();
        var answerId = await connection.ExecuteScalarAsync<int>(
            """
            SELECT answer.id_answer
            FROM public.answer answer
            INNER JOIN public.organization_survey assignment
                ON assignment.id_organization_survey = answer.id_organization_survey
            WHERE assignment.id_survey = @SurveyId
              AND assignment.id_organization = @OrganizationId;
            """,
            new { SurveyId = survey.SurveyId.Value, OrganizationId = organizationId });
        await connection.ExecuteAsync(
            """
            UPDATE public.answer
            SET csp = @Signature
            WHERE id_answer = @AnswerId;
            """,
            new
            {
                AnswerId = answerId,
                Signature = Convert.ToBase64String("legacy signature"u8.ToArray())
            });

        var response = await workflow.GetAnswersResponseAsync(
            survey.SurveyId.Value,
            organizationId,
            "archive",
            includeAllOrganizationAnswers: false);

        Assert.True(response.Success, response.Error);
        var answer = Assert.Single(response.Answers);
        Assert.True(answer.IsSigned);
        Assert.Equal("Не удалось определить", answer.SignatureInfo?.SignedBy);
        Assert.Equal("Проверка недоступна", answer.SignatureInfo?.Status);
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
    public async Task UserArchive_DateFiltersAlwaysUseAnswerCompletionDate()
    {
        var organizationIds = await CreateOrganizationsAsync(1);
        var survey = await CreateSurveyAsync(organizationIds);
        var workflow = CreateAnswerService();
        Assert.True((await workflow.InsertAnswerAsync(BuildAnswerRecord(survey.SurveyId!.Value, organizationIds[0], 5))).Success);

        var userId = await CreateUserAsync(organizationIds[0], "archive-date-filter-client");
        var completionDate = new DateTime(2024, 3, 15);
        await using (var connection = _fixture.CreateConnection())
        {
            await connection.ExecuteAsync(
                """
                UPDATE public.answer AS answer
                SET
                    completion_date = @CompletionDate,
                    csp = 'integration-signature'
                FROM public.organization_survey AS assignment
                WHERE answer.id_organization_survey = assignment.id_organization_survey
                  AND assignment.id_survey = @SurveyId
                  AND assignment.id_organization = @OrganizationId;
                """,
                new
                {
                    CompletionDate = completionDate,
                    SurveyId = survey.SurveyId,
                    OrganizationId = organizationIds[0]
                });
        }

        var service = new SurveyService(_connectionFactory, _surveyRepository, _clock);
        var exactDateArchive = await service.GetUserArchivePageAsync(
            userId,
            1,
            searchTerm: null,
            date: "2024-03-15",
            dateFrom: null,
            dateTo: null,
            signedOnly: false);
        var monthArchive = await service.GetUserArchivePageAsync(
            userId,
            1,
            searchTerm: null,
            date: null,
            dateFrom: null,
            dateTo: null,
            signedOnly: false,
            month: "2024-03");
        var yearArchive = await service.GetUserArchivePageAsync(
            userId,
            1,
            searchTerm: null,
            date: null,
            dateFrom: null,
            dateTo: null,
            signedOnly: false,
            year: "2024");
        var rangeArchive = await service.GetUserArchivePageAsync(
            userId,
            1,
            searchTerm: null,
            date: null,
            dateFrom: "2024-03-10",
            dateTo: "2024-03-20",
            signedOnly: false);
        var assignmentPeriodArchive = await service.GetUserArchivePageAsync(
            userId,
            1,
            searchTerm: null,
            date: null,
            dateFrom: DateTime.Today.AddDays(-2).ToString("yyyy-MM-dd"),
            dateTo: DateTime.Today.AddDays(15).ToString("yyyy-MM-dd"),
            signedOnly: false);
        var combinedArchive = await service.GetUserArchivePageAsync(
            userId,
            99,
            searchTerm: "Интеграционная",
            date: null,
            dateFrom: null,
            dateTo: null,
            signedOnly: true,
            surveyIds: survey.SurveyId.ToString(),
            month: "2024-03");

        Assert.All(
            new[] { exactDateArchive, monthArchive, yearArchive, rangeArchive },
            archive => Assert.Contains(archive!.ArchivedSurveys, item => item.IdSurvey == survey.SurveyId));
        Assert.DoesNotContain(
            assignmentPeriodArchive!.ArchivedSurveys,
            item => item.IdSurvey == survey.SurveyId);
        Assert.NotNull(combinedArchive);
        Assert.Equal(1, combinedArchive!.CurrentPage);
        Assert.Equal(1, combinedArchive.TotalPages);
        Assert.Equal(1, combinedArchive.TotalCount);
        Assert.Contains(combinedArchive.ArchivedSurveys, item => item.IdSurvey == survey.SurveyId);
        Assert.Equal(new[] { survey.SurveyId!.Value }, combinedArchive.FilterState.SelectedSurveyIds);
        Assert.Equal("2024-03", combinedArchive.FilterState.Month);
    }

    [RequiresPostgresFact]
    public async Task AutoCreation_SavePersistsScheduleAndSelectedTemplate()
    {
        var organizationIds = await CreateOrganizationsAsync(1);
        var templateId = await CreateAutoCreationTemplateAsync(organizationIds);
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
            TemplateIds = [templateId]
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
        var selectedTemplateId = await connection.ExecuteScalarAsync<int>(
            "SELECT id_survey_template FROM public.survey_template_auto_creation_config WHERE id_config = 1;");

        Assert.True(result.Success);
        Assert.Equal("quarter", stored.ReportingPeriod);
        Assert.Equal(16, stored.ReportingOffset);
        Assert.Equal(20, stored.WorkingPeriod);
        Assert.False(stored.IsEnabled);
        Assert.Equal(templateId, selectedTemplateId);
    }

    [RequiresPostgresFact]
    public async Task AutoCreation_ApplyWhileRunning_PersistsSettingsAndKeepsProcessEnabled()
    {
        var organizationIds = await CreateOrganizationsAsync(1);
        var templateId = await CreateAutoCreationTemplateAsync(organizationIds);
        var autoCreation = new SurveyService(
            _connectionFactory,
            _surveyRepository,
            new FixedClock(new DateTime(2026, 4, 20)),
            logger: NullLogger<SurveyService>.Instance,
            productionCalendar: CreateWeekdayProductionCalendar());

        var startResult = await autoCreation.StartAsync(new SurveyAutoCreationSettingsRequest
        {
            ReportingPeriod = "month",
            ReportingOffsetBusinessDays = 1,
            ActivePeriodBusinessDays = 8,
            TemplateIds = [templateId]
        });
        var applyResult = await autoCreation.SaveAsync(new SurveyAutoCreationSettingsRequest
        {
            ReportingPeriod = "quarter",
            ReportingOffsetBusinessDays = 3,
            ActivePeriodBusinessDays = 12,
            TemplateIds = [templateId]
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

        Assert.True(startResult.Success, startResult.Message);
        Assert.StartsWith(
            "Новые настройки автосоздания применены, автосоздание анкет запущено.",
            startResult.Message);
        Assert.True(applyResult.Success, applyResult.Message);
        Assert.StartsWith("Новые настройки автосоздания применены.", applyResult.Message);
        Assert.True(applyResult.IsEnabled);
        Assert.Equal("quarter", stored.ReportingPeriod);
        Assert.Equal(3, stored.ReportingOffset);
        Assert.Equal(12, stored.WorkingPeriod);
        Assert.True(stored.IsEnabled);
    }

    [RequiresPostgresFact]
    public async Task AutoCreation_TemplateOptionsContainOnlyActiveTemplates()
    {
        var organizationId = Assert.Single(await CreateOrganizationsAsync(1));
        await using var connection = _fixture.CreateConnection();
        var templateIds = (await connection.QueryAsync<int>(
            """
            INSERT INTO public.survey_template (
                name_survey_template,
                description,
                date_begin,
                date_end
            )
            VALUES
                ('Активный шаблон', 'Активная версия', '2000-01-01', NULL),
                ('Архивный шаблон', 'Архивная версия', '2000-01-01', '2000-01-02')
            RETURNING id_survey_template;
            """)).ToArray();
        await connection.ExecuteAsync(
            """
            INSERT INTO public.organization_survey_template (id_organization, id_survey_template)
            SELECT @OrganizationId, selected.template_id
            FROM unnest(@TemplateIds) AS selected(template_id);
            """,
            new { OrganizationId = organizationId, TemplateIds = templateIds });

        var service = new SurveyService(_connectionFactory, _surveyRepository, _clock);
        var options = await service.GetTemplateOptionsAsync();

        Assert.Contains(options, option => option.Id == templateIds[0] && option.Name == "Активный шаблон");
        Assert.DoesNotContain(options, option => option.Id == templateIds[1]);
    }

    [RequiresPostgresFact]
    public async Task AutoCreation_RunPending_UsesCurrentSelectedTemplateContent()
    {
        var organizationIds = await CreateOrganizationsAsync(2);
        var templateId = await CreateAutoCreationTemplateAsync([organizationIds[0]]);
        var calendar = CreateWeekdayProductionCalendar();
        var setupService = new SurveyService(
            _connectionFactory,
            _surveyRepository,
            new FixedClock(new DateTime(2026, 4, 1)),
            logger: NullLogger<SurveyService>.Instance,
            productionCalendar: calendar);

        var startResult = await setupService.StartAsync(new SurveyAutoCreationSettingsRequest
        {
            ReportingPeriod = "quarter",
            ReportingOffsetBusinessDays = 5,
            ActivePeriodBusinessDays = 5,
            TemplateIds = [templateId]
        });

        await using (var connection = _fixture.CreateConnection())
        {
            await connection.ExecuteAsync(
                """
                UPDATE public.survey_template
                SET description = 'Последняя версия'
                WHERE id_survey_template = @TemplateId;

                DELETE FROM public.survey_template_question
                WHERE id_survey_template = @TemplateId;

                INSERT INTO public.survey_template_question (
                    id_survey_template,
                    question_order,
                    question_text
                )
                VALUES (@TemplateId, 1, 'Вопрос последней версии');

                DELETE FROM public.organization_survey_template
                WHERE id_survey_template = @TemplateId;

                INSERT INTO public.organization_survey_template
                    (id_organization, id_survey_template)
                VALUES (@OrganizationId, @TemplateId);
                """,
                new
                {
                    TemplateId = templateId,
                    OrganizationId = organizationIds[1]
                });
        }

        var runService = new SurveyService(
            _connectionFactory,
            _surveyRepository,
            new FixedClock(new DateTime(2026, 4, 27)),
            logger: NullLogger<SurveyService>.Instance,
            productionCalendar: calendar);
        var runResult = await runService.RunPendingAsync();

        await using var verificationConnection = _fixture.CreateConnection();
        var createdSurveyId = await verificationConnection.QuerySingleAsync<int>(
            """
            SELECT id_survey
            FROM public.survey
            WHERE lower(btrim(name_survey)) = lower(btrim('Интеграционная анкета'))
              AND date_begin = '2026-04-24'
              AND date_end = '2026-04-30';
            """);
        var copiedDescription = await verificationConnection.ExecuteScalarAsync<string>(
            "SELECT description FROM public.survey WHERE id_survey = @SurveyId;",
            new { SurveyId = createdSurveyId });
        var copiedQuestions = (await verificationConnection.QueryAsync<string>(
            """
            SELECT question_text
            FROM public.survey_question
            WHERE id_survey = @SurveyId
            ORDER BY question_order;
            """,
            new { SurveyId = createdSurveyId })).ToArray();
        var copiedOrganizationIds = (await verificationConnection.QueryAsync<int>(
            """
            SELECT id_organization
            FROM public.organization_survey
            WHERE id_survey = @SurveyId
            ORDER BY id_organization;
            """,
            new { SurveyId = createdSurveyId })).ToArray();

        Assert.True(startResult.Success, startResult.Message);
        Assert.True(runResult.Processed);
        Assert.Equal(1, runResult.CreatedSurveyCount);
        Assert.Equal("Последняя версия", copiedDescription);
        Assert.Equal(["Вопрос последней версии"], copiedQuestions);
        Assert.Equal([organizationIds[1]], copiedOrganizationIds);
    }

    [RequiresPostgresFact]
    public async Task AutoCreation_RejectsTemplateWithoutOrganizationAssignments()
    {
        var templateId = await CreateAutoCreationTemplateAsync(
            [],
            "Шаблон без назначений",
            "Не должен использоваться для автосоздания");
        var service = new SurveyService(_connectionFactory, _surveyRepository, _clock);

        var options = await service.GetTemplateOptionsAsync();
        var saveResult = await service.SaveAsync(new SurveyAutoCreationSettingsRequest
        {
            ReportingPeriod = "month",
            ReportingOffsetBusinessDays = 1,
            ActivePeriodBusinessDays = 8,
            TemplateIds = [templateId]
        });

        Assert.DoesNotContain(options, option => option.Id == templateId);
        Assert.False(saveResult.Success);
        Assert.Equal(
            "Один или несколько выбранных шаблонов не найдены, не активны или не назначены организациям.",
            saveResult.Message);
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
        var templateId = await CreateAutoCreationTemplateAsync(organizationIds);
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
            TemplateIds = [templateId]
        });
        var repeatedRun = await autoCreation.RunPendingAsync();

        await using var connection = _fixture.CreateConnection();
        var copy = await connection.QuerySingleAsync<SurveyCopyRow>(
            """
            SELECT id_survey AS IdSurvey
            FROM public.survey
            WHERE name_survey = 'Интеграционная анкета';
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
        var copiedBasePeriod = await connection.QuerySingleAsync<SurveyDateRow>(
            """
            SELECT date_begin AS DateBegin, date_end AS DateEnd
            FROM public.survey
            WHERE id_survey = @SurveyId;
            """,
            new { SurveyId = copy.IdSurvey });

        Assert.True(startResult.Success);
        Assert.StartsWith(
            "Новые настройки автосоздания применены, автосоздание анкет запущено.",
            startResult.Message,
            StringComparison.Ordinal);
        Assert.Equal(2, copiedQuestionCount);
        Assert.Equal(new DateTime(2026, 4, 24), copiedBasePeriod.DateBegin);
        Assert.Equal(new DateTime(2026, 4, 30), copiedBasePeriod.DateEnd);
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
    public async Task AutoCreation_RunPending_DoesNotRecreateExpiredScheduledCopy()
    {
        var organizationIds = await CreateOrganizationsAsync(1);
        var templateId = await CreateAutoCreationTemplateAsync(organizationIds);
        var today = new DateTime(2026, 8, 31);

        var autoCreation = new SurveyService(
            _connectionFactory,
            _surveyRepository,
            new FixedClock(today),
            logger: NullLogger<SurveyService>.Instance,
            productionCalendar: CreateWeekdayProductionCalendar());

        var startResult = await autoCreation.StartAsync(new SurveyAutoCreationSettingsRequest
        {
            ReportingPeriod = "month",
            ReportingOffsetBusinessDays = 5,
            ActivePeriodBusinessDays = 5,
            TemplateIds = [templateId]
        });

        await using (var normalizationConnection = _fixture.CreateConnection())
        {
            await normalizationConnection.ExecuteAsync(
                """
                UPDATE public.survey
                SET name_survey = '  интеграционная АНКЕТА  '
                WHERE date_begin = '2026-08-18'
                  AND date_end = '2026-08-24';
                """);
        }

        var repeatedRun = await autoCreation.RunPendingAsync();

        await using var verificationConnection = _fixture.CreateConnection();
        var scheduledCopies = await verificationConnection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM public.survey
            WHERE lower(btrim(name_survey)) = lower(btrim('Интеграционная анкета'))
              AND date_begin = '2026-08-18'
              AND date_end = '2026-08-24';
            """);

        Assert.True(startResult.Success, startResult.Message);
        Assert.True(repeatedRun.Processed);
        Assert.Equal(0, repeatedRun.CreatedSurveyCount);
        Assert.Equal(1, scheduledCopies);
    }

    [RequiresPostgresFact]
    public async Task AutoCreation_RunPending_CreatesNextPeriodDespitePreviousExtension()
    {
        var organizationIds = await CreateOrganizationsAsync(1);
        var survey = await CreateSurveyAsync(organizationIds);
        var surveyId = survey.SurveyId!.Value;
        var templateId = await CreateAutoCreationTemplateAsync(organizationIds);
        var today = new DateTime(2026, 8, 24);

        await using (var connection = _fixture.CreateConnection())
        {
            await connection.ExecuteAsync(
                """
                UPDATE public.survey
                SET date_begin = '2026-07-20', date_end = '2026-07-29'
                WHERE id_survey = @SurveyId;

                UPDATE public.organization_survey
                SET date_begin = '2026-07-20', date_end = '2026-08-25'
                WHERE id_survey = @SurveyId;
                """,
                new { SurveyId = surveyId });
        }

        var autoCreation = new SurveyService(
            _connectionFactory,
            _surveyRepository,
            new FixedClock(today),
            logger: NullLogger<SurveyService>.Instance,
            productionCalendar: CreateWeekdayProductionCalendar());

        var startResult = await autoCreation.StartAsync(new SurveyAutoCreationSettingsRequest
        {
            ReportingPeriod = "month",
            ReportingOffsetBusinessDays = 1,
            ActivePeriodBusinessDays = 5,
            TemplateIds = [templateId]
        });

        await using var verificationConnection = _fixture.CreateConnection();
        var augustCopy = await verificationConnection.QuerySingleAsync<SurveyDateRow>(
            """
            SELECT date_begin AS DateBegin, date_end AS DateEnd
            FROM public.survey
            WHERE lower(btrim(name_survey)) = lower(btrim('Интеграционная анкета'))
              AND id_survey <> @SurveyId;
            """,
            new { SurveyId = surveyId });

        Assert.True(startResult.Success, startResult.Message);
        Assert.Equal(new DateTime(2026, 8, 24), augustCopy.DateBegin);
        Assert.Equal(new DateTime(2026, 8, 28), augustCopy.DateEnd);
    }

    [RequiresPostgresFact]
    public async Task AutoCreation_AddingSelectedTemplateAfterRunCreatesItsSurveyImmediately()
    {
        var organizationIds = await CreateOrganizationsAsync(2);
        var firstTemplateId = await CreateAutoCreationTemplateAsync([organizationIds[0]]);
        var clock = new FixedClock(new DateTime(2026, 8, 24));
        var service = new SurveyService(
            _connectionFactory,
            new SurveyRepository(clock),
            clock,
            logger: NullLogger<SurveyService>.Instance,
            productionCalendar: CreateWeekdayProductionCalendar());

        var startResult = await service.StartAsync(new SurveyAutoCreationSettingsRequest
        {
            ReportingPeriod = "month",
            ReportingOffsetBusinessDays = 1,
            ActivePeriodBusinessDays = 5,
            TemplateIds = [firstTemplateId]
        });
        var createResult = await service.CreateSurveyTemplateAsync(new SurveyAddRequest
        {
            Title = "Новый шаблон после запуска",
            Description = "Создаётся без ожидания следующего запуска службы",
            StartDate = "2026-08-23",
            EndDate = string.Empty,
            Organizations = [organizationIds[1]],
            Criteria = ["Новый критерий"],
            IsAutoCreationEnabled = true
        });

        await using var connection = _fixture.CreateConnection();
        var generatedSurvey = await connection.QuerySingleAsync<(int IdSurvey, DateTime DateBegin, DateTime DateEnd)>(
            """
            SELECT
                id_survey AS IdSurvey,
                date_begin AS DateBegin,
                date_end AS DateEnd
            FROM public.survey
            WHERE name_survey = 'Новый шаблон после запуска';
            """);
        var generatedOrganizationId = await connection.ExecuteScalarAsync<int>(
            """
            SELECT id_organization
            FROM public.organization_survey
            WHERE id_survey = @SurveyId;
            """,
            new { SurveyId = generatedSurvey.IdSurvey });
        var generatedCriterion = await connection.ExecuteScalarAsync<string>(
            """
            SELECT question_text
            FROM public.survey_question
            WHERE id_survey = @SurveyId;
            """,
            new { SurveyId = generatedSurvey.IdSurvey });

        Assert.True(startResult.Success, startResult.Message);
        Assert.True(createResult.Success, createResult.Message);
        Assert.Contains("Создано анкет: 1", createResult.Message);
        Assert.Equal(new DateTime(2026, 8, 24), generatedSurvey.DateBegin);
        Assert.Equal(new DateTime(2026, 8, 28), generatedSurvey.DateEnd);
        Assert.Equal(organizationIds[1], generatedOrganizationId);
        Assert.Equal("Новый критерий", generatedCriterion);
    }

    [RequiresPostgresFact]
    public async Task AutoCreation_DoesNotCreateDuplicateWhenSurveyNameAlreadyExistsInReportingMonth()
    {
        var organizationId = Assert.Single(await CreateOrganizationsAsync(1));
        var templateId = await CreateAutoCreationTemplateAsync([organizationId]);
        var clock = new FixedClock(new DateTime(2026, 8, 31));
        var service = new SurveyService(
            _connectionFactory,
            new SurveyRepository(clock),
            clock,
            logger: NullLogger<SurveyService>.Instance,
            productionCalendar: CreateWeekdayProductionCalendar());

        var saveResult = await service.SaveAsync(new SurveyAutoCreationSettingsRequest
        {
            ReportingPeriod = "month",
            ReportingOffsetBusinessDays = 5,
            ActivePeriodBusinessDays = 5,
            TemplateIds = [templateId]
        });

        await using (var connection = _fixture.CreateConnection())
        {
            await connection.ExecuteAsync(
                """
                INSERT INTO public.survey (name_survey, description, date_begin, date_end)
                VALUES ('  интеграционная АНКЕТА  ', 'Создана вручную', '2026-08-01', '2026-08-10');
                """);
        }

        var startResult = await service.StartAsync(new SurveyAutoCreationSettingsRequest
        {
            ReportingPeriod = "month",
            ReportingOffsetBusinessDays = 5,
            ActivePeriodBusinessDays = 5,
            TemplateIds = [templateId]
        });

        await using var verificationConnection = _fixture.CreateConnection();
        var surveyCount = await verificationConnection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM public.survey
            WHERE lower(btrim(name_survey)) = lower(btrim('Интеграционная анкета'))
              AND date_begin >= '2026-08-01'
              AND date_begin < '2026-09-01';
            """);

        Assert.True(saveResult.Success, saveResult.Message);
        Assert.True(startResult.Success, startResult.Message);
        Assert.Equal(1, surveyCount);
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
            """
            INSERT INTO public.survey (name_survey, description, date_begin, date_end)
            VALUES ('Анкета тестовых часов', '', '2026-01-01', @DateEnd)
            RETURNING id_survey;
            """,
            new { DateEnd = dateEnd });
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
    public async Task Auth_RejectsUserBeforeUserOrOrganizationStartDate()
    {
        await using var connection = _fixture.CreateConnection();
        var today = _clock.Today.Date;
        var activeOrganizationId = await connection.ExecuteScalarAsync<int>(
            """
            INSERT INTO public.organization (organization_name, organization_short_name, date_begin)
            VALUES ('Действующая организация будущего пользователя', 'Действующая', @DateBegin)
            RETURNING id_organization;
            """,
            new { DateBegin = today.AddDays(-10) });
        var futureOrganizationId = await connection.ExecuteScalarAsync<int>(
            """
            INSERT INTO public.organization (organization_name, organization_short_name, date_begin)
            VALUES ('Будущая организация входа', 'Будущая', @DateBegin)
            RETURNING id_organization;
            """,
            new { DateBegin = today.AddDays(1) });

        const string password = "not-started-password";
        const string futureUserLogin = "future-auth-user";
        const string futureOrganizationUserLogin = "future-organization-auth-user";
        var passwordHasher = new PasswordHasher<string>();
        await connection.ExecuteAsync(
            """
            INSERT INTO public.app_user (id_organization, login, full_name, role, password, date_begin)
            VALUES
                (@ActiveOrganizationId, @FutureUserLogin, 'Будущий пользователь', 'user', @FutureUserPassword, @FutureDateBegin),
                (@FutureOrganizationId, @FutureOrganizationUserLogin, 'Пользователь будущей организации', 'user', @FutureOrganizationUserPassword, @ActiveDateBegin);
            """,
            new
            {
                ActiveOrganizationId = activeOrganizationId,
                FutureOrganizationId = futureOrganizationId,
                FutureUserLogin = futureUserLogin,
                FutureOrganizationUserLogin = futureOrganizationUserLogin,
                FutureUserPassword = passwordHasher.HashPassword(futureUserLogin, password),
                FutureOrganizationUserPassword = passwordHasher.HashPassword(futureOrganizationUserLogin, password),
                FutureDateBegin = today.AddDays(1),
                ActiveDateBegin = today.AddDays(-10)
            });

        var accessStatusService = new UserAccessStatusService(_connectionFactory, _clock);
        var authService = new AuthService(_connectionFactory, accessStatusService);

        var futureUserResult = await authService.AuthenticateAsync(futureUserLogin, password);
        var futureOrganizationUserResult = await authService.AuthenticateAsync(futureOrganizationUserLogin, password);
        var userService = new UserManagementService(_connectionFactory, _clock);
        var organizationService = new OrganizationManagementService(_connectionFactory, _clock);
        var activeUsers = (await userService.GetActiveUsersPageAsync(1, "name", "asc")).Users;
        var archivedUsers = (await userService.GetArchivedUsersPageAsync(1, "name", "asc")).Users;
        var activeOrganizations = (await organizationService.GetActiveOrganizationsPageAsync(1, "name", "asc")).Organizations;
        var archivedOrganizations = (await organizationService.GetArchivedOrganizationsPageAsync(1, "name", "asc")).Organizations;

        Assert.False(futureUserResult.Success);
        Assert.Equal(StatusCodes.Status403Forbidden, futureUserResult.StatusCode);
        Assert.Equal(AuthService.BlockedUserMessage, futureUserResult.ErrorMessage);
        Assert.False(futureOrganizationUserResult.Success);
        Assert.Equal(StatusCodes.Status403Forbidden, futureOrganizationUserResult.StatusCode);
        Assert.Equal(AuthService.BlockedUserMessage, futureOrganizationUserResult.ErrorMessage);
        Assert.DoesNotContain(activeUsers, user => user.NameUser is futureUserLogin or futureOrganizationUserLogin);
        Assert.Contains(archivedUsers, user => user.NameUser == futureUserLogin);
        Assert.Contains(archivedUsers, user => user.NameUser == futureOrganizationUserLogin);
        Assert.Contains(activeOrganizations, organization => organization.OrganizationId == activeOrganizationId);
        Assert.DoesNotContain(activeOrganizations, organization => organization.OrganizationId == futureOrganizationId);
        Assert.Contains(archivedOrganizations, organization => organization.OrganizationId == futureOrganizationId);
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
        var reportAnswers = await reportRepository.GetSurveyAnswersAsync(
            survey.SurveyId.Value,
            organizationIds[0],
            DateTime.Today.AddMonths(-1),
            DateTime.Today.AddMonths(1));
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
    public async Task SurveyMonthlyReport_FiltersByCompletionDateAndUsesSelectedPeriodInTitle()
    {
        var organizationIds = await CreateOrganizationsAsync(2);
        var survey = await CreateSurveyAsync(organizationIds);
        var surveyId = survey.SurveyId!.Value;
        var workflow = CreateAnswerService();
        Assert.True((await workflow.InsertAnswerAsync(BuildAnswerRecord(surveyId, organizationIds[0], 4))).Success);
        Assert.True((await workflow.InsertAnswerAsync(BuildAnswerRecord(surveyId, organizationIds[1], 5))).Success);

        await using (var connection = _fixture.CreateConnection())
        {
            await connection.ExecuteAsync(
                """
                UPDATE public.answer answer
                SET completion_date = CASE
                    WHEN assignment.id_organization = @JulyOrganizationId THEN @JulyCompletionDate
                    ELSE @AugustCompletionDate
                END
                FROM public.organization_survey assignment
                WHERE assignment.id_organization_survey = answer.id_organization_survey
                  AND assignment.id_survey = @SurveyId;
                """,
                new
                {
                    SurveyId = surveyId,
                    JulyOrganizationId = organizationIds[0],
                    JulyCompletionDate = new DateTime(2026, 7, 15, 12, 0, 0),
                    AugustCompletionDate = new DateTime(2026, 8, 5, 12, 0, 0)
                });
        }

        var reportClock = new FixedClock(new DateTime(2026, 8, 11));
        var reportRepository = new SurveyRepository(_connectionFactory, reportClock);
        var reportService = new SurveyService(_connectionFactory, reportRepository, reportClock);
        var report = await reportService.CreateSurveyMonthlyReportAsync(surveyId, 0, 7, 2026);

        using var reportStream = new MemoryStream(report.Content);
        using var document = WordprocessingDocument.Open(reportStream, false);
        var reportText = document.MainDocumentPart?.Document?.Body?.InnerText ?? string.Empty;

        Assert.Contains("июль 2026", report.FileName);
        Assert.Contains("за июль 2026", reportText);
        Assert.Contains("Орг 1", reportText);
        Assert.DoesNotContain("Орг 2", reportText);

        var error = await Assert.ThrowsAsync<ReportDataNotFoundException>(() =>
            reportService.CreateSurveyMonthlyReportAsync(surveyId, 0, 9, 2026));
        Assert.Equal("За выбранный месяц и год нет ответов для формирования отчёта.", error.Message);
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
            "SELECT COUNT(*) FROM public.answer WHERE id_answer = @AnswerId;",
            new { AnswerId = activeAnswer.IdAnswer }));
    }

    private AnswerService CreateAnswerService(int? userId = null)
    {
        var participantUserId = userId ?? GetOrCreateAnswerUserId();
        return new AnswerService(
            _connectionFactory,
            _surveyRepository,
            _answerRepository,
            new FixedCurrentUserService(participantUserId),
            new FixedClock(DateTime.Today));
    }

    private int GetOrCreateAnswerUserId()
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
        return new ProductionCalendarService(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("https://calendar.test/")
            },
            Options.Create(new ProductionCalendarOptions()),
            NullLogger<ProductionCalendarService>.Instance);
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

    private async Task<int> CreateAutoCreationTemplateAsync(
        IReadOnlyList<int> organizationIds,
        string name = "Интеграционная анкета",
        string description = "Проверка сценария автосоздания")
    {
        await using var connection = _fixture.CreateConnection();
        var templateId = await connection.ExecuteScalarAsync<int>(
            """
            INSERT INTO public.survey_template (
                name_survey_template,
                description,
                date_begin,
                date_end
            )
            VALUES (@Name, @Description, '2000-01-01', NULL)
            RETURNING id_survey_template;
            """,
            new { Name = name, Description = description });

        await connection.ExecuteAsync(
            """
            INSERT INTO public.survey_template_question (
                id_survey_template,
                question_order,
                question_text
            )
            VALUES
                (@TemplateId, 1, 'Первый вопрос'),
                (@TemplateId, 2, 'Второй вопрос');

            INSERT INTO public.organization_survey_template (
                id_organization,
                id_survey_template
            )
            SELECT organization_id, @TemplateId
            FROM unnest(@OrganizationIds) AS selected(organization_id);
            """,
            new { TemplateId = templateId, OrganizationIds = organizationIds.ToArray() });

        return templateId;
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

    private sealed class StoredAnswerItem
    {
        public int QuestionOrder { get; init; }
        public int? Rating { get; init; }
        public string? Comment { get; init; }
    }

    private sealed class SignedDraftState
    {
        public string? Signature { get; init; }
        public int? Rating { get; init; }
        public string? Comment { get; init; }
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
        public int AssignmentId { get; init; }
        public int OrganizationId { get; init; }
        public DateTime DateBegin { get; init; }
        public DateTime? DateEnd { get; init; }
    }

    private sealed class SurveyDateRow
    {
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
