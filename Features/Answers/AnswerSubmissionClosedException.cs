namespace MainProject.Application.UseCases.Answers;

public sealed class AnswerSubmissionClosedException : InvalidOperationException
{
    public const string UserMessage = "Срок прохождения анкеты истёк. Ответы не отправлены.";

    public AnswerSubmissionClosedException()
        : base(UserMessage)
    {
    }
}
