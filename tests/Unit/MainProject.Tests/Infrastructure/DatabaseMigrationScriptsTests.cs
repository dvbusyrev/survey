using System.IO;

namespace MainProject.Tests.Infrastructure;

public sealed class DatabaseMigrationScriptsTests
{
    [Fact]
    public void ApplyAll_IncludesEmailTemplateMigration()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "000_apply_all.sql"));

        Assert.Contains(@"\ir 010_add_email_template_storage.sql", script);
        Assert.Contains(@"\ir 011_add_email_template_audit_log.sql", script);
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
