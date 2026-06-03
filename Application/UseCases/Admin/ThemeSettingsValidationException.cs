namespace MainProject.Application.UseCases.Admin;

public sealed class ThemeSettingsValidationException : Exception
{
    public ThemeSettingsValidationException(IReadOnlyList<string> errors)
        : base(errors.FirstOrDefault() ?? "Параметры темы заполнены некорректно.")
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}
