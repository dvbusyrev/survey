using System.IO;

namespace MainProject.Tests.Infrastructure;

public sealed class DatabaseMigrationScriptsTests
{
    [Fact]
    public void ApplyAll_IncludesEmailTemplateMigration()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "000_apply_all.sql"));

        Assert.Contains(@"\ir 001_unified_schema.sql", script);
        Assert.Contains(@"\ir 002_repair_survey_foreign_keys.sql", script);
        Assert.Contains(@"\ir 003_add_update_metadata.sql", script);
        Assert.Contains(@"\ir 010_add_email_template_storage.sql", script);
        Assert.Contains(@"\ir 011_add_email_template_audit_log.sql", script);
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
    }

    [Fact]
    public void UnifiedSchemaMigration_ContainsPortableCurrentBaseline()
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
    public void ReadPlanScript_UsesExplainAnalyzeOnlyOnAnIsolatedDatabase()
    {
        var root = GetRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "scripts", "explain-read-paths.sh"));
        var plans = File.ReadAllText(Path.Combine(root, "db", "performance", "explain_read_paths.sql"));

        Assert.Contains("SURVEY_EXPLAIN_DATABASE", script);
        Assert.Contains("current_database", script);
        Assert.Contains("rehearsal|perf|benchmark|test", script);
        Assert.Contains(".NET connection-string syntax", script);
        Assert.Contains("BEGIN READ ONLY", plans);
        Assert.Contains("EXPLAIN (ANALYZE, BUFFERS, SETTINGS, SUMMARY, TIMING OFF)", plans);
        Assert.Contains("Журнал событий", plans);
        Assert.Contains("Архив анкет администратора", plans);
        Assert.Contains("Архив анкет клиента", plans);
        Assert.Contains("Отчеты: ответы анкеты", plans);
    }

    [Fact]
    public void RepairAndMetadataMigrations_DoNotReferenceRecoveryDirectory()
    {
        var repairScript = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "002_repair_survey_foreign_keys.sql"));
        var metadataScript = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "003_add_update_metadata.sql"));

        Assert.DoesNotContain("recovery/", repairScript);
        Assert.DoesNotContain("recovery/", metadataScript);
        Assert.Contains("CREATE OR REPLACE FUNCTION public.set_update_metadata()", metadataScript);
        Assert.Contains("ADD COLUMN IF NOT EXISTS date_update", metadataScript);
        Assert.Contains("ADD COLUMN IF NOT EXISTS user_update", metadataScript);
        Assert.Contains("VALUES ('002', 'repair_survey_foreign_keys')", repairScript);
        Assert.Contains("VALUES ('003', 'add_update_metadata')", metadataScript);
    }

    [Fact]
    public void EmailTemplateMigration_CreatesStorageAndTriggers()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "010_add_email_template_storage.sql"));

        Assert.Contains("CREATE OR REPLACE FUNCTION public.set_update_metadata()", script);
        Assert.Contains("CREATE TABLE IF NOT EXISTS public.email_template", script);
        Assert.Contains("CREATE TABLE IF NOT EXISTS public.email_template_l", script);
        Assert.Contains("recipient_emails", script);
        Assert.Contains("smtp_host", script);
        Assert.Contains("smtp_password", script);
        Assert.Contains("from_address", script);
        Assert.Contains("idx_email_template_l_changed_at", script);
        Assert.Contains("idx_email_template_l_record_pk", script);
        Assert.Contains("CREATE TRIGGER trg_email_template_set_update_metadata", script);
        Assert.Contains("CREATE TRIGGER trg_email_template_audit", script);
        Assert.Contains("write_crud_audit('id_email_template')", script);
        Assert.Contains("VALUES ('010', 'add_email_template_storage')", script);
    }

    [Fact]
    public void EmailTemplateAuditMigration_BackfillsAuditTableForExistingDatabases()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "011_add_email_template_audit_log.sql"));

        Assert.Contains("CREATE TABLE IF NOT EXISTS public.email_template_l", script);
        Assert.Contains("CREATE INDEX IF NOT EXISTS idx_email_template_l_changed_at", script);
        Assert.Contains("CREATE INDEX IF NOT EXISTS idx_email_template_l_record_pk", script);
        Assert.Contains("CREATE TRIGGER trg_email_template_audit", script);
        Assert.Contains("VALUES ('011', 'add_email_template_audit_log')", script);
    }

    [Fact]
    public void SurveyAutoCreationMigration_CreatesConfigStorage()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "012_add_survey_auto_creation.sql"));

        Assert.Contains("CREATE TABLE IF NOT EXISTS public.survey_auto_creation_config", script);
        Assert.Contains("CREATE TABLE IF NOT EXISTS public.survey_auto_creation_config_survey", script);
        Assert.Contains("creation_pattern", script);
        Assert.Contains("start_pattern", script);
        Assert.Contains("end_offset_business_days", script);
        Assert.Contains("last_processed_schedule_date", script);
        Assert.Contains("has_legacy_config", script);
        Assert.DoesNotContain("REFERENCES public.survey_auto_creation_config (id_config)", script);
        Assert.Contains("VALUES ('012', 'add_survey_auto_creation')", script);
    }

    [Fact]
    public void SurveyAutoCreationTransformMigration_CreatesNormalizedStorage()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "013_transform_survey_auto_creation_config.sql"));

        Assert.Contains("CREATE TABLE IF NOT EXISTS public.week_day", script);
        Assert.Contains("CREATE TABLE IF NOT EXISTS public.auto_creation_config", script);
        Assert.Contains("CREATE TABLE IF NOT EXISTS public.survey_auto_creation_config", script);
        Assert.Contains("CREATE TABLE IF NOT EXISTS public.l_survey_auto_creation_config", script);
        Assert.Contains("id_creation_day", script);
        Assert.Contains("id_begin_day", script);
        Assert.Contains("working_period", script);
        Assert.Contains("REFERENCES public.week_day", script);
        Assert.DoesNotContain("last_processed_schedule_date", script);
        Assert.DoesNotContain("date_update timestamp", script);
        Assert.DoesNotContain("user_update integer", script);
        Assert.Contains("VALUES ('013', 'transform_survey_auto_creation_config')", script);
    }

    [Fact]
    public void SurveyAutoCreationMetadataRemovalMigration_DropsAutoCreationMetadataColumns()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "014_remove_auto_creation_config_metadata.sql"));

        Assert.Contains("ALTER TABLE public.auto_creation_config", script);
        Assert.Contains("DROP COLUMN IF EXISTS last_processed_schedule_date", script);
        Assert.Contains("DROP COLUMN IF EXISTS date_update", script);
        Assert.Contains("DROP COLUMN IF EXISTS user_update", script);
        Assert.Contains("VALUES ('014', 'remove_auto_creation_config_metadata')", script);
    }

    [Fact]
    public void AnswerAssignmentMigration_StoresAnswerAssignmentKey()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "015_link_answers_to_organization_survey.sql"));

        Assert.Contains("id_organization_survey integer", script);
        Assert.Contains("answer_id_organization_survey_key UNIQUE (id_organization_survey)", script);
        Assert.Contains("REFERENCES public.organization_survey (id_organization_survey)", script);
        Assert.Contains("column_name = 'id_organization'", script);
        Assert.Contains("column_name = 'id_survey'", script);
        Assert.Contains("current_primary_key_columns = ARRAY['id_organization_survey']", script);
        Assert.Contains("DROP COLUMN IF EXISTS id_organization", script);
        Assert.Contains("DROP COLUMN IF EXISTS id_survey", script);
        Assert.Contains("VALUES ('015', 'link_answers_to_organization_survey')", script);
    }

    [Fact]
    public void ConfigRenameMigration_RenamesEmailAndAutoCreationAuditTables()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "016_rename_config_tables.sql"));

        Assert.Contains("ALTER TABLE public.email_template RENAME TO email_config", script);
        Assert.Contains("ALTER TABLE public.email_template_l RENAME TO email_config_l", script);
        Assert.Contains("ALTER TABLE public.l_survey_auto_creation_config RENAME TO survey_auto_creation_config_l", script);
        Assert.Contains("RENAME COLUMN id_email_template TO id_config", script);
        Assert.Contains("DROP COLUMN IF EXISTS template_key", script);
        Assert.Contains("idx_email_config_l_changed_at", script);
        Assert.Contains("idx_survey_auto_creation_config_l_changed_at", script);
        Assert.Contains("VALUES ('016', 'rename_config_tables')", script);
    }

    [Fact]
    public void AppUserCredentialsRenameMigration_RenamesColumnsAndConstraints()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "017_rename_app_user_credentials.sql"));

        Assert.Contains("ALTER TABLE public.app_user RENAME COLUMN name_user TO login", script);
        Assert.Contains("ALTER TABLE public.app_user RENAME COLUMN name_role TO role", script);
        Assert.Contains("ALTER TABLE public.app_user RENAME COLUMN hash_password TO password", script);
        Assert.Contains("app_user_login_key", script);
        Assert.Contains("chk_app_user_role", script);
        Assert.Contains("app_user_login_not_null", script);
        Assert.Contains("app_user_role_not_null", script);
        Assert.Contains("app_user_password_not_null", script);
        Assert.Contains("VALUES ('017', 'rename_app_user_credentials')", script);
    }

    [Fact]
    public void AuditLogCurrentTablesMigration_AddsMissingAuditTablesAndTriggers()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "018_add_audit_log_current_tables.sql"));

        Assert.Contains("CREATE TABLE IF NOT EXISTS public.survey_question_l", script);
        Assert.Contains("CREATE TABLE IF NOT EXISTS public.answer_item_l", script);
        Assert.Contains("CREATE TABLE IF NOT EXISTS public.auto_creation_config_l", script);
        Assert.Contains("CREATE TRIGGER trg_survey_question_crud_audit", script);
        Assert.Contains("CREATE TRIGGER trg_answer_item_crud_audit", script);
        Assert.Contains("CREATE TRIGGER trg_auto_creation_config_audit", script);
        Assert.Contains("write_crud_audit('id_question')", script);
        Assert.Contains("write_crud_audit('id_item')", script);
        Assert.Contains("write_crud_audit('id_config')", script);
        Assert.Contains("VALUES ('018', 'add_audit_log_current_tables')", script);
    }

    [Fact]
    public void AuditOldNewRowsMigration_StoresBothAuditVersions()
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
    public void AutoCreationScheduleLimitMigration_LimitsWorkingPeriod()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "020_limit_auto_creation_schedule_options.sql"));

        Assert.Contains("SET working_period = 14", script);
        Assert.Contains("current_day.week_number > 3", script);
        Assert.Contains("DELETE FROM public.week_day", script);
        Assert.Contains("CHECK (week_number BETWEEN 1 AND 3)", script);
        Assert.Contains("DROP CONSTRAINT IF EXISTS ck_auto_creation_config_working_period", script);
        Assert.Contains("CHECK (working_period BETWEEN 1 AND 14)", script);
        Assert.Contains("VALUES ('020', 'limit_auto_creation_schedule_options')", script);
    }

    [Fact]
    public void AutoCreationEmptyPeriodMigration_AllowsOpenEndedAssignments()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "021_allow_empty_auto_creation_period.sql"));

        Assert.Contains("ALTER COLUMN working_period DROP NOT NULL", script);
        Assert.Contains("ALTER COLUMN working_period DROP DEFAULT", script);
        Assert.Contains("working_period IS NULL OR working_period BETWEEN 1 AND 14", script);
        Assert.Contains("ALTER COLUMN date_end DROP NOT NULL", script);
        Assert.Contains("CREATE OR REPLACE VIEW public.survey_schedule", script);
        Assert.Contains("VALUES ('021', 'allow_empty_auto_creation_period')", script);
    }

    [Fact]
    public void AutoCreationReportingPeriodMigration_ReplacesWeekdaySchedule()
    {
        var script = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "db",
            "migrations",
            "029_redesign_auto_creation_reporting_period.sql"));

        Assert.Contains("reporting_period text NOT NULL DEFAULT 'month'", script);
        Assert.Contains("reporting_offset_business_days integer NOT NULL DEFAULT 1", script);
        Assert.Contains("working_period SET NOT NULL", script);
        Assert.Contains("DROP COLUMN IF EXISTS id_creation_day CASCADE", script);
        Assert.Contains("DROP COLUMN IF EXISTS id_begin_day CASCADE", script);
        Assert.Contains("VALUES ('029', 'redesign_auto_creation_reporting_period')", script);
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
    public void ObsoleteWeekDayMigration_RemovesOnlyTheUnusedDictionary()
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
    public void AuditIdGeneratorRepairMigration_RepairsOnlyLegacyAuditColumns()
    {
        var script = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "db",
            "migrations",
            "034_repair_audit_id_generators.sql"));

        Assert.Contains("'email_config_l'", script);
        Assert.Contains("IF id_is_identity OR id_default IS NOT NULL", script);
        Assert.Contains("CREATE SEQUENCE IF NOT EXISTS", script);
        Assert.Contains("ALTER COLUMN id_audit SET DEFAULT", script);
        Assert.Contains("pg_catalog.setval", script);
        Assert.Contains("VALUES ('034', 'repair_audit_id_generators')", script);
        Assert.DoesNotContain("CASCADE", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SchemaConsistencyMigration_NormalizesCurrentSchema()
    {
        var script = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "db",
            "migrations",
            "030_reconcile_schema_consistency.sql"));

        Assert.Contains("ADD COLUMN IF NOT EXISTS date_update", script);
        Assert.Contains("DROP TRIGGER IF EXISTS trg_auto_creation_config_set_update_metadata", script);
        Assert.Contains("DROP INDEX IF EXISTS public.idx_answer_id_organization_survey", script);
        Assert.Contains("RENAME CONSTRAINT %I TO %I", script);
        Assert.Contains("RENAME TO organization_id_organization_seq", script);
        Assert.Contains("VALUES ('030', 'reconcile_schema_consistency')", script);
    }

    [Fact]
    public void ThemeConfigMigration_CreatesStorageAndAuditTriggers()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "023_add_theme_config.sql"));

        Assert.Contains("CREATE TABLE IF NOT EXISTS public.theme_config", script);
        Assert.Contains("CREATE TABLE IF NOT EXISTS public.theme_config_l", script);
        Assert.Contains("font_color", script);
        Assert.Contains("background_color", script);
        Assert.Contains("background_image_data_url", script);
        Assert.Contains("background_image_opacity", script);
        Assert.Contains("idx_theme_config_l_changed_at", script);
        Assert.Contains("CREATE TRIGGER trg_theme_config_set_update_metadata", script);
        Assert.Contains("CREATE TRIGGER trg_theme_config_audit", script);
        Assert.Contains("VALUES ('023', 'add_theme_config')", script);
    }

    [Fact]
    public void ThemePaletteControlsMigration_AddsShadeParameters()
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
