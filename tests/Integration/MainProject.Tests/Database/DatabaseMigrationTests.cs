using System.IO;

namespace MainProject.Tests.Database;

public sealed class DatabaseMigrationTests
{
    [Fact]
    public void ApplyAll_IncludesCurrentMigrations()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "000_apply_all.sql"));

        Assert.Contains(@"\ir 001_unified_schema.sql", script);
        Assert.Contains(@"\ir 002_repair_survey_foreign_keys.sql", script);
        Assert.Contains(@"\ir 003_add_update_metadata.sql", script);
        Assert.Contains(@"\ir 004_add_organization_short_name.sql", script);
        Assert.Contains(@"\ir 005_add_organization_survey_schedule.sql", script);
        Assert.Contains(@"\ir 006_move_organization_survey_schedule_sync_to_triggers.sql", script);
        Assert.Contains(@"\ir 007_rename_schedule_dates_and_remove_legacy_columns.sql", script);
        Assert.Contains(@"\ir 008_convert_dates_and_rename_organization_id.sql", script);
        Assert.Contains(@"\ir 009_derive_survey_schedule_from_assignments.sql", script);
        Assert.Contains(@"\ir 012_add_survey_auto_creation.sql", script);
        Assert.Contains(@"\ir 013_transform_survey_auto_creation_config.sql", script);
        Assert.Contains(@"\ir 014_remove_auto_creation_config_metadata.sql", script);
        Assert.Contains(@"\ir 015_link_answers_to_organization_survey.sql", script);
        Assert.Contains(@"\ir 016_rename_config_tables.sql", script);
        Assert.Contains(@"\ir 017_rename_app_user_credentials.sql", script);
        Assert.Contains(@"\ir 018_add_audit_log_current_tables.sql", script);
        Assert.Contains(@"\ir 019_store_audit_old_new_rows.sql", script);
        Assert.Contains(@"\ir 020_limit_auto_creation_schedule_options.sql", script);
        Assert.Contains(@"\ir 021_allow_empty_auto_creation_period.sql", script);
        Assert.Contains(@"\ir 022_store_signed_answer_content.sql", script);
        Assert.Contains(@"\ir 023_add_theme_config.sql", script);
        Assert.Contains(@"\ir 024_add_theme_palette_controls.sql", script);
        Assert.Contains(@"\ir 025_add_answer_drafts.sql", script);
        Assert.Contains(@"\ir 026_rebuild_audit_tables_as_structured_snapshots.sql", script);
        Assert.Contains(@"\ir 027_store_theme_background_image_blob.sql", script);
        Assert.Contains(@"\ir 028_remove_legacy_theme_columns.sql", script);
        Assert.Contains(@"\ir 029_redesign_auto_creation_reporting_period.sql", script);
        Assert.Contains(@"\ir 030_reconcile_schema_consistency.sql", script);
        Assert.Contains(@"\ir 031_require_user_organization.sql", script);
        Assert.Contains(@"\ir 032_allow_arbitrary_auto_creation_periods.sql", script);
        Assert.Contains(@"\ir 033_remove_obsolete_week_day.sql", script);
        Assert.Contains(@"\ir 034_repair_audit_id_generators.sql", script);
        Assert.Contains("date_update", script);
        Assert.Contains("user_update", script);
    }

    [Fact]
    public void UnifiedSchemaMigration_UsesPortableCurrentSchemaBaseline()
    {
        var migration = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "001_unified_schema.sql"));
        var schema = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "bootstrap", "001_base_schema.sql"));

        Assert.Contains(@"\ir ../bootstrap/001_base_schema.sql", migration);
        Assert.Contains("('017', 'rename_app_user_credentials')", migration);
        Assert.Contains("('018', 'add_audit_log_current_tables')", migration);
        Assert.Contains("('019', 'store_audit_old_new_rows')", migration);
        Assert.Contains("('020', 'limit_auto_creation_schedule_options')", migration);
        Assert.Contains("('021', 'allow_empty_auto_creation_period')", migration);
        Assert.Contains("INSERT INTO public.week_day", migration);
        Assert.Contains("CREATE TABLE public.app_user", schema);
        Assert.Contains("id_organization integer NOT NULL", schema);
        Assert.Contains("login text NOT NULL", schema);
        Assert.Contains("role text NOT NULL", schema);
        Assert.Contains("password text NOT NULL", schema);
        Assert.Contains("CREATE TABLE public.email_config", schema);
        Assert.Contains("CREATE TABLE public.survey_auto_creation_config_l", schema);
        Assert.Contains("CREATE TABLE public.survey_question_l", schema);
        Assert.Contains("CREATE TABLE public.answer_item_l", schema);
        Assert.Contains("CREATE TABLE public.auto_creation_config_l", schema);
        Assert.Contains("old_row_data jsonb", schema);
        Assert.Contains("new_row_data jsonb", schema);
        Assert.Contains("date_update timestamp without time zone DEFAULT now() NOT NULL", schema);
        Assert.Contains("trg_answer_set_update_metadata", schema);
        Assert.DoesNotContain("recovery/", migration);
        Assert.DoesNotContain("\\restrict", schema);
        Assert.DoesNotContain("transaction_timeout", schema);
    }

    [Fact]
    public void UpdateMetadataMigration_IsSelfContained()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "003_add_update_metadata.sql"));

        Assert.Contains("ADD COLUMN IF NOT EXISTS date_update", script);
        Assert.Contains("ADD COLUMN IF NOT EXISTS user_update", script);
        Assert.Contains("CREATE OR REPLACE FUNCTION public.set_update_metadata()", script);
        Assert.Contains("CREATE TRIGGER %I", script);
        Assert.Contains("BEFORE INSERT OR UPDATE ON %I.%I", script.Replace(Environment.NewLine, " "));
        Assert.DoesNotContain("recovery/", script);
    }

    [Fact]
    public void OrganizationShortNameMigration_AddsOrganizationShortNameColumn()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "004_add_organization_short_name.sql"));

        Assert.Contains("ALTER TABLE public.organization", script);
        Assert.Contains("ADD COLUMN IF NOT EXISTS organization_short_name", script);
        Assert.Contains("VALUES ('004', 'add_organization_short_name')", script);
    }

    [Fact]
    public void OrganizationSurveyScheduleMigration_AddsAssignmentScheduleColumns()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "005_add_organization_survey_schedule.sql"));

        Assert.Contains("ALTER TABLE public.organization_survey", script);
        Assert.Contains("ADD COLUMN IF NOT EXISTS date_open", script);
        Assert.Contains("ADD COLUMN IF NOT EXISTS date_close", script);
        Assert.Contains("ADD COLUMN IF NOT EXISTS is_custom_end_date", script);
        Assert.Contains("extended_until::date > s.date_close", script);
        Assert.Contains("VALUES ('005', 'add_organization_survey_schedule')", script);
    }

    [Fact]
    public void OrganizationSurveyScheduleTriggerMigration_MovesSyncLogicIntoDatabase()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "006_move_organization_survey_schedule_sync_to_triggers.sql"));

        Assert.Contains("CREATE TABLE IF NOT EXISTS public.organization_survey_schedule_sync", script);
        Assert.Contains("CREATE OR REPLACE FUNCTION public.organization_survey_apply_schedule_defaults()", script);
        Assert.Contains("CREATE OR REPLACE FUNCTION public.organization_survey_track_schedule_sync()", script);
        Assert.Contains("CREATE OR REPLACE FUNCTION public.survey_propagate_schedule_to_assignments()", script);
        Assert.Contains("DROP COLUMN IF EXISTS is_custom_end_date", script);
        Assert.Contains("DROP COLUMN IF EXISTS extended_until", script);
        Assert.Contains("VALUES ('006', 'move_organization_survey_schedule_sync_to_triggers')", script);
    }

    [Fact]
    public void RenameScheduleDatesMigration_RenamesColumnsAndRemovesLegacyFields()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "007_rename_schedule_dates_and_remove_legacy_columns.sql"));

        Assert.Contains("DROP COLUMN IF EXISTS create_date_survey", script);
        Assert.Contains("DROP COLUMN IF EXISTS block", script);
        Assert.Contains("DROP COLUMN IF EXISTS date_create", script);
        Assert.Contains("RENAME COLUMN date_open TO date_begin", script);
        Assert.Contains("RENAME COLUMN date_close TO date_end", script);
        Assert.Contains("RENAME COLUMN sync_date_open TO sync_date_begin", script);
        Assert.Contains("RENAME COLUMN sync_date_close TO sync_date_end", script);
        Assert.Contains("BEFORE INSERT OR UPDATE OF id_survey, date_begin, date_end", script);
        Assert.Contains("AFTER UPDATE OF date_begin, date_end", script);
        Assert.Contains("VALUES ('007', 'rename_schedule_dates_and_remove_legacy_columns')", script);
    }

    [Fact]
    public void ConvertDatesAndRenameOrganizationIdMigration_UsesDateColumnsAndNewOrganizationKey()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "008_convert_dates_and_rename_organization_id.sql"));

        Assert.Contains("ALTER COLUMN date_begin TYPE date USING date_begin::date", script);
        Assert.Contains("ALTER COLUMN date_end TYPE date USING date_end::date", script);
        Assert.Contains("DROP COLUMN IF EXISTS created_at", script);
        Assert.Contains("RENAME COLUMN organization_id TO id_organization", script);
        Assert.Contains("write_crud_audit('id_organization')", script);
        Assert.Contains("write_crud_audit('id_organization', 'id_survey')", script);
        Assert.Contains("AFTER INSERT OR UPDATE OF id_organization, id_survey, date_begin, date_end", script);
        Assert.Contains("VALUES ('008', 'convert_dates_and_rename_organization_id')", script);
    }

    [Fact]
    public void DeriveSurveyScheduleFromAssignmentsMigration_DropsStoredSurveyDatesAndCreatesAggregateView()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "009_derive_survey_schedule_from_assignments.sql"));

        Assert.Contains("DROP TABLE IF EXISTS public.organization_survey_schedule_sync", script);
        Assert.Contains("DROP COLUMN IF EXISTS date_begin", script);
        Assert.Contains("DROP COLUMN IF EXISTS date_end", script);
        Assert.Contains("CREATE VIEW public.survey_schedule AS", script);
        Assert.Contains("MIN(os.date_begin) AS date_begin", script);
        Assert.Contains("MAX(os.date_end) AS date_end", script);
        Assert.Contains("VALUES ('009', 'derive_survey_schedule_from_assignments')", script);
    }

    [Fact]
    public void LinkAnswersToOrganizationSurveyMigration_MovesAnswerForeignKeyToAssignment()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "015_link_answers_to_organization_survey.sql"));

        Assert.Contains("ADD COLUMN IF NOT EXISTS id_organization_survey", script);
        Assert.Contains("organization_survey_pkey PRIMARY KEY (id_organization_survey)", script);
        Assert.Contains("column_name = 'id_organization'", script);
        Assert.Contains("column_name = 'id_survey'", script);
        Assert.Contains("current_primary_key_columns = ARRAY['id_organization_survey']", script);
        Assert.Contains("answer_id_organization_survey_fkey", script);
        Assert.Contains("DROP COLUMN IF EXISTS id_organization", script);
        Assert.Contains("DROP COLUMN IF EXISTS id_survey", script);
        Assert.Contains("write_crud_audit('id_organization_survey')", script);
        Assert.Contains("VALUES ('015', 'link_answers_to_organization_survey')", script);
    }

    [Fact]
    public void RenameConfigTablesMigration_UsesFinalConfigTableNames()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "016_rename_config_tables.sql"));

        Assert.Contains("RENAME TO email_config", script);
        Assert.Contains("RENAME TO email_config_l", script);
        Assert.Contains("RENAME TO survey_auto_creation_config_l", script);
        Assert.Contains("RENAME COLUMN id_email_template TO id_config", script);
        Assert.Contains("DROP COLUMN IF EXISTS template_key", script);
        Assert.Contains("write_crud_audit('id_config')", script);
        Assert.Contains("VALUES ('016', 'rename_config_tables')", script);
    }

    [Fact]
    public void RenameAppUserCredentialsMigration_UsesFinalCredentialColumnNames()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "017_rename_app_user_credentials.sql"));

        Assert.Contains("RENAME COLUMN name_user TO login", script);
        Assert.Contains("RENAME COLUMN name_role TO role", script);
        Assert.Contains("RENAME COLUMN hash_password TO password", script);
        Assert.Contains("RENAME CONSTRAINT app_user_name_user_key TO app_user_login_key", script);
        Assert.Contains("RENAME CONSTRAINT chk_app_user_name_role TO chk_app_user_role", script);
        Assert.Contains("app_user_password_not_null", script);
        Assert.Contains("VALUES ('017', 'rename_app_user_credentials')", script);
    }

    [Fact]
    public void AuditLogCurrentTablesMigration_AddsAuditCoverageForCurrentSchema()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "018_add_audit_log_current_tables.sql"));

        Assert.Contains("CREATE TABLE IF NOT EXISTS public.survey_question_l", script);
        Assert.Contains("CREATE TABLE IF NOT EXISTS public.answer_item_l", script);
        Assert.Contains("CREATE TABLE IF NOT EXISTS public.auto_creation_config_l", script);
        Assert.Contains("CREATE TRIGGER trg_survey_question_crud_audit", script);
        Assert.Contains("CREATE TRIGGER trg_answer_item_crud_audit", script);
        Assert.Contains("CREATE TRIGGER trg_auto_creation_config_audit", script);
        Assert.Contains("VALUES ('018', 'add_audit_log_current_tables')", script);
    }

    [Fact]
    public void AuditOldNewRowsMigration_StoresOldAndNewAuditRows()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "019_store_audit_old_new_rows.sql"));

        Assert.Contains("ADD COLUMN IF NOT EXISTS old_row_data jsonb", script);
        Assert.Contains("ADD COLUMN IF NOT EXISTS new_row_data jsonb", script);
        Assert.Contains("CREATE OR REPLACE FUNCTION public.write_crud_audit()", script);
        Assert.Contains("old_row_data, new_row_data", script);
        Assert.Contains("audit_old_row_data", script);
        Assert.Contains("audit_new_row_data", script);
        Assert.Contains("VALUES ('019', 'store_audit_old_new_rows')", script);
    }

    [Fact]
    public void StructuredAuditTablesMigration_RebuildsAuditStorage()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "026_rebuild_audit_tables_as_structured_snapshots.sql"));

        Assert.Contains("ADD COLUMN IF NOT EXISTS parent_audit_id bigint", script);
        Assert.Contains("SELECT public.rebuild_structured_audit_table('survey'", script);
        Assert.Contains("DROP COLUMN IF EXISTS record_pk", script);
        Assert.Contains("DROP COLUMN IF EXISTS row_data", script);
        Assert.Contains("EXECUTE FUNCTION public.write_crud_audit('id_config', 'id_survey')", script);
        Assert.Contains("VALUES ('026', 'rebuild_audit_tables_as_structured_snapshots')", script);
    }

    [Fact]
    public void ThemeCleanupMigration_RemovesOnlyLegacyThemeColumns()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "028_remove_legacy_theme_columns.sql"));

        Assert.Contains("ALTER TABLE public.theme_config", script);
        Assert.Contains("ALTER TABLE public.theme_config_l", script);
        Assert.Contains("DROP COLUMN IF EXISTS gradient_enabled", script);
        Assert.Contains("DROP COLUMN IF EXISTS background_image_data_url", script);
        Assert.Contains("VALUES ('028', 'remove_legacy_theme_columns')", script);
    }

    [Fact]
    public void ThemeConfigMigration_CreatesThemeStorageAndAudit()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "023_add_theme_config.sql"));

        Assert.Contains("CREATE TABLE IF NOT EXISTS public.theme_config", script);
        Assert.Contains("CREATE TABLE IF NOT EXISTS public.theme_config_l", script);
        Assert.Contains("background_image_data_url", script);
        Assert.Contains("background_image_opacity", script);
        Assert.Contains("CREATE TRIGGER trg_theme_config_set_update_metadata", script);
        Assert.Contains("CREATE TRIGGER trg_theme_config_audit", script);
        Assert.Contains("write_crud_audit('id_config')", script);
        Assert.Contains("VALUES ('023', 'add_theme_config')", script);
    }

    [Fact]
    public void ThemePaletteControlsMigration_AddsThemeShadeSettings()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "024_add_theme_palette_controls.sql"));

        Assert.Contains("soft_lighten_percent", script);
        Assert.Contains("header_darken_percent", script);
        Assert.Contains("footer_darken_percent", script);
        Assert.Contains("button_darken_percent", script);
        Assert.Contains("button_strong_darken_percent", script);
        Assert.Contains("surface_tint_opacity_percent", script);
        Assert.Contains("VALUES ('024', 'add_theme_palette_controls')", script);
    }

    [Fact]
    public void AnswerDraftMigration_CreatesDraftStorage()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "025_add_answer_drafts.sql"));

        Assert.Contains("CREATE TABLE IF NOT EXISTS public.answer_draft", script);
        Assert.Contains("CREATE TABLE IF NOT EXISTS public.answer_draft_item", script);
        Assert.Contains("id_organization_survey integer NOT NULL", script);
        Assert.Contains("signed_content bytea", script);
        Assert.Contains("UNIQUE (id_organization_survey)", script);
        Assert.Contains("UNIQUE (id_answer_draft, question_order)", script);
        Assert.Contains("VALUES ('025', 'add_answer_drafts')", script);
    }

    [Fact]
    public void ThemeBackgroundImageBlobMigration_AddsBinaryImageStorage()
    {
        var applyAllScript = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "000_apply_all.sql"));
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "027_store_theme_background_image_blob.sql"));

        Assert.Contains(@"\ir 027_store_theme_background_image_blob.sql", applyAllScript);
        Assert.Contains("background_image bytea", script);
        Assert.Contains("background_image_file_name text", script);
        Assert.Contains("background_image_content_type text", script);
        Assert.Contains("ALTER TABLE public.theme_config_l", script);
        Assert.Contains("VALUES ('027', 'store_theme_background_image_blob')", script);
    }

    [Fact]
    public void SchemaConsistencyMigration_ReconcilesBootstrapAndUpgradeSchemas()
    {
        var script = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "db",
            "migrations",
            "030_reconcile_schema_consistency.sql"));

        Assert.Contains("ADD COLUMN IF NOT EXISTS date_update", script);
        Assert.Contains("ADD COLUMN IF NOT EXISTS user_update", script);
        Assert.Contains("DROP COLUMN IF EXISTS date_update", script);
        Assert.Contains("DROP COLUMN IF EXISTS user_update", script);
        Assert.Contains("ALTER COLUMN font_color SET DEFAULT '#343D4B'", script);
        Assert.Contains("ALTER COLUMN background_color SET DEFAULT '#B2A8FF'", script);
        Assert.Contains("idx_survey_auto_creation_config_id_survey", script);
        Assert.Contains("VALUES ('030', 'reconcile_schema_consistency')", script);
    }

    [Fact]
    public void RequiredUserOrganizationMigration_RejectsMissingOrganizationsAndAddsConstraint()
    {
        var script = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "db",
            "migrations",
            "031_require_user_organization.sql"));

        Assert.Contains("WHERE id_organization IS NULL", script);
        Assert.Contains("ALTER COLUMN id_organization SET NOT NULL", script);
        Assert.Contains("ON DELETE RESTRICT", script);
        Assert.Contains("VALUES ('031', 'require_user_organization')", script);
    }

    [Fact]
    public void AutoCreationArbitraryPeriodsMigration_RemovesUpperLimits()
    {
        var script = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "db",
            "migrations",
            "032_allow_arbitrary_auto_creation_periods.sql"));

        Assert.Contains("CHECK (working_period >= 1)", script);
        Assert.Contains("CHECK (reporting_offset_business_days >= 1)", script);
        Assert.DoesNotContain("BETWEEN 1 AND 14", script);
        Assert.Contains("VALUES ('032', 'allow_arbitrary_auto_creation_periods')", script);
    }

    [Fact]
    public void ObsoleteWeekDayMigration_DropsTheDictionaryWithoutCascade()
    {
        var script = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "db",
            "migrations",
            "033_remove_obsolete_week_day.sql"));

        Assert.Contains("DROP TABLE IF EXISTS public.week_day", script);
        Assert.DoesNotContain("CASCADE", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("VALUES ('033', 'remove_obsolete_week_day')", script);
    }

    [Fact]
    public void AuditIdGeneratorRepairMigration_SupportsLegacyAndCurrentSchemas()
    {
        var script = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "db",
            "migrations",
            "034_repair_audit_id_generators.sql"));

        Assert.Contains("is_identity = 'YES'", script);
        Assert.Contains("column_default", script);
        Assert.Contains("ALTER SEQUENCE public.%I OWNED BY public.%I.id_audit", script);
        Assert.Contains("ALTER COLUMN id_audit SET DEFAULT", script);
        Assert.Contains("VALUES ('034', 'repair_audit_id_generators')", script);
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "main_project.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
