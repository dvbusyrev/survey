namespace MainProject.Application.UseCases.Answers;

public sealed class AnswerAlreadySignedException : InvalidOperationException
{
    public AnswerAlreadySignedException()
        : base("Анкета уже подписана и не может быть подписана повторно.")
    {
    }
}
