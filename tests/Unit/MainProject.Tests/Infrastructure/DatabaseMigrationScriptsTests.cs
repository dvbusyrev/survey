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
    }

    [Fact]
    public void UnifiedSchemaMigration_ContainsPortableCurrentBaseline()
    {
        var migration = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "001_unified_schema.sql"));
        var schema = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "001_current_schema.sql"));

        Assert.Contains(@"\ir 001_current_schema.sql", migration);
        Assert.Contains("('017', 'rename_app_user_credentials')", migration);
        Assert.Contains("INSERT INTO public.week_day", migration);
        Assert.Contains("CREATE TABLE public.app_user", schema);
        Assert.Contains("login text NOT NULL", schema);
        Assert.Contains("role text NOT NULL", schema);
        Assert.Contains("password text NOT NULL", schema);
        Assert.Contains("CREATE TABLE public.email_config", schema);
        Assert.Contains("CREATE TABLE public.survey_auto_creation_config_l", schema);
        Assert.DoesNotContain("recovery/", migration);
        Assert.DoesNotContain("\\restrict", schema);
        Assert.DoesNotContain("transaction_timeout", schema);
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
