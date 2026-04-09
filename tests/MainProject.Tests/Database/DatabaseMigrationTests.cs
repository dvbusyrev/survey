using System.IO;

namespace MainProject.Tests.Database;

public sealed class DatabaseMigrationTests
{
    [Fact]
    public void ApplyAll_IncludesUpdateMetadataMigration()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db", "migrations", "000_apply_all.sql"));

        Assert.Contains(@"\ir 003_add_update_metadata.sql", script);
        Assert.Contains("date_update", script);
        Assert.Contains("user_update", script);
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
