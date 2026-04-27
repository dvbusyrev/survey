using System.IO;

namespace MainProject.Tests.Database;

public sealed class DatabaseMigrationTests
{
    [Fact]
    public void ApplyAll_IncludesCurrentMigrations()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "000_apply_all.sql"));

        Assert.Contains(@"\ir 003_add_update_metadata.sql", script);
        Assert.Contains(@"\ir 004_add_organization_short_name.sql", script);
        Assert.Contains(@"\ir 005_add_organization_survey_schedule.sql", script);
        Assert.Contains(@"\ir 006_move_organization_survey_schedule_sync_to_triggers.sql", script);
        Assert.Contains(@"\ir 007_rename_schedule_dates_and_remove_legacy_columns.sql", script);
        Assert.Contains(@"\ir 008_convert_dates_and_rename_organization_id.sql", script);
        Assert.Contains(@"\ir 009_derive_survey_schedule_from_assignments.sql", script);
        Assert.Contains(@"\ir 012_add_survey_auto_creation.sql", script);
        Assert.Contains("date_update", script);
        Assert.Contains("user_update", script);
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
    public void ReconstructSchema_UsesSharedUpdateMetadataSupport()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "recovery", "reconstruct_schema.sql"));

        Assert.Contains(@"\ir update_metadata_support.sql", script);
        Assert.Contains("date_update", script);
        Assert.Contains("user_update", script);
    }

    [Fact]
    public void UpdateMetadataSupport_AddsColumnsAndTriggers_ForAllPublicTables()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "recovery", "update_metadata_support.sql"));

        Assert.Contains("ADD COLUMN IF NOT EXISTS date_update", script);
        Assert.Contains("ADD COLUMN IF NOT EXISTS user_update", script);
        Assert.Contains("CREATE OR REPLACE FUNCTION public.set_update_metadata()", script);
        Assert.Contains("CREATE TRIGGER %I", script);
        Assert.Contains("BEFORE INSERT OR UPDATE ON public.%I", script.Replace(Environment.NewLine, " "));
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
