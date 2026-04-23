namespace MainProject.Application.UseCases.Admin;

public sealed class EmailTemplateValidationException : Exception
{
    public EmailTemplateValidationException(IReadOnlyList<string> errors)
        : base(errors.FirstOrDefault() ?? "Параметры письма заполнены некорректно.")
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}
