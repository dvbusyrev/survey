using MainProject.Application.DTO;
using MainProject.Application.Support;

namespace MainProject.Tests.Support;

public sealed class SurveyFilterOptionsTests
{
    [Fact]
    public void Build_GroupsSurveyIdsByTrimmedCaseInsensitiveName()
    {
        var options = SurveyFilterOptions.Build(
        [
            new SelectionOption { Id = 8, Name = " Ежемесячная анкета " },
            new SelectionOption { Id = 3, Name = "ежемесячная анкета" },
            new SelectionOption { Id = 5, Name = "Другая анкета" }
        ]);

        Assert.Equal(2, options.Count);
        var groupedOption = Assert.Single(
            options,
            option => string.Equals(option.Name, "ежемесячная анкета", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(3, groupedOption.Id);
        Assert.Equal(new[] { 3, 8 }, groupedOption.Ids);
    }

    [Fact]
    public void ExpandSelectedIds_SelectsEverySurveyInNameGroup()
    {
        var options = SurveyFilterOptions.Build(
        [
            new SelectionOption { Id = 3, Name = "Ежемесячная анкета" },
            new SelectionOption { Id = 8, Name = "Ежемесячная анкета" },
            new SelectionOption { Id = 5, Name = "Другая анкета" }
        ]);

        var selectedIds = SurveyFilterOptions.ExpandSelectedIds([8, 99], options);

        Assert.Equal(new[] { 3, 8, 99 }, selectedIds);
    }
}
