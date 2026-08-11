namespace MainProject.Application.UseCases.Answers;

public sealed class AnswerAlreadySubmittedException : InvalidOperationException
{
    public const string UserMessage = "Ответы на анкету уже отправлены и не могут быть изменены.";

    public AnswerAlreadySubmittedException()
        : base(UserMessage)
    {
    }
}
