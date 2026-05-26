using MainProject.Domain.Entities;

namespace MainProject.Web.ViewModels;

public sealed class UserSurveyPageContentViewModel
{
    public IReadOnlyList<Survey> Surveys { get; init; } = Array.Empty<Survey>();
    public string ActiveTab { get; init; } = "active";
    public int CurrentPage { get; init; } = 1;
    public int TotalPages { get; init; } = 1;
    public int ActiveCount { get; init; }
    public int ArchivedCount { get; init; }
    public string SearchTerm { get; init; } = string.Empty;
    public bool SignedOnly { get; init; }

    public bool IsArchive => string.Equals(ActiveTab, "archived", StringComparison.OrdinalIgnoreCase);

    // public string Title => IsArchive ? "Архив анкет" : "Доступные анкеты";

    public string Description => IsArchive
        ? "Ниже вы можете открыть ранее завершённые анкеты и посмотреть отправленные ответы."
        : "Ниже вы можете открыть доступные анкеты и сразу перейти к заполнению.";

    public AppPaginationViewModel Pagination => Surveys.Count == 0
        ? new AppPaginationViewModel
        {
            AriaLabel = IsArchive
                ? "Навигация по страницам архива анкет"
                : "Навигация по страницам доступных анкет"
        }
        : AppPaginationViewModel.Create(
            CurrentPage,
            TotalPages,
            IsArchive
                ? "Навигация по страницам архива анкет"
                : "Навигация по страницам доступных анкет");
}
