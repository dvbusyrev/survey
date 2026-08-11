namespace MainProject.Application.UseCases.Answers;

public sealed class AnswerSigningClosedException : InvalidOperationException
{
    public const string UserMessage = "Срок действия анкеты истёк. Подписание недоступно.";

    public AnswerSigningClosedException()
        : base(UserMessage)
    {
    }
}
