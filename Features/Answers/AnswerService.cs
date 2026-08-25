using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Domain.Entities;
using MainProject.Infrastructure.Persistence;

namespace MainProject.Application.UseCases.Answers;

public partial class AnswerService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly SurveyRepository _surveyRepository;
    private readonly AnswerRepository _answerRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IClock _clock;

    protected AnswerService()
    {
        _connectionFactory = null!;
        _surveyRepository = null!;
        _answerRepository = null!;
        _currentUserService = null!;
        _clock = null!;
    }

    public AnswerService(
        IDbConnectionFactory connectionFactory,
        SurveyRepository surveyRepository,
        AnswerRepository answerRepository,
        ICurrentUserService currentUserService,
        IClock clock)
    {
        _connectionFactory = connectionFactory;
        _surveyRepository = surveyRepository;
        _answerRepository = answerRepository;
        _currentUserService = currentUserService;
        _clock = clock;
    }

    private async Task<int?> GetUserOrganizationIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await _surveyRepository.GetUserOrganizationIdAsync(connection, userId, cancellationToken);
    }

    private async Task<bool> IsSurveyAssignedToOrganizationAsync(
        int surveyId,
        int organizationId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await _surveyRepository.IsActiveAssignmentAsync(connection, surveyId, organizationId, cancellationToken);
    }

    private Task<bool> AnswerRecordExistsAsync(int surveyId, int organizationId, CancellationToken cancellationToken = default)
        => _answerRepository.AnswerRecordExistsAsync(surveyId, organizationId, cancellationToken);

    private Task<Survey?> GetSurveyInfoAsync(int surveyId, CancellationToken cancellationToken = default)
        => _answerRepository.GetSurveyInfoAsync(surveyId, cancellationToken);

    private Task<IReadOnlyList<SurveyQuestionItem>> GetSurveyQuestionsAsync(
        int surveyId,
        CancellationToken cancellationToken = default)
        => _answerRepository.GetSurveyQuestionsAsync(surveyId, cancellationToken);

    private Task<AnswerRecord?> GetAnswerRecordAsync(
        int surveyId,
        int organizationId,
        CancellationToken cancellationToken = default)
        => _answerRepository.GetAnswerRecordAsync(surveyId, organizationId, cancellationToken);

    private Task<AnswerRecord?> GetDraftRecordAsync(
        int surveyId,
        int organizationId,
        CancellationToken cancellationToken = default)
        => _answerRepository.GetDraftRecordAsync(surveyId, organizationId, cancellationToken);

    private Task<IReadOnlyList<AnswerRecord>> GetAnswerRecordsAsync(
        int surveyId,
        int? organizationId = null,
        CancellationToken cancellationToken = default)
        => _answerRepository.GetAnswerRecordsAsync(surveyId, organizationId, cancellationToken);

    private async Task<int> InsertAnswerRecordAsync(
        AnswerRecord answerRecord,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var result = await _answerRepository.SubmitAnswerAsync(answerRecord, userId, cancellationToken);
        if (!result.Found)
        {
            throw new InvalidOperationException("Назначение анкеты для организации не найдено.");
        }

        if (result.SubmissionClosed)
        {
            throw new AnswerSubmissionClosedException();
        }

        if (result.AlreadySubmitted)
        {
            throw new AnswerAlreadySubmittedException();
        }

        if (result.AlreadySigned)
        {
            throw new AnswerAlreadySignedException();
        }

        return result.AnswerId;
    }

    private Task<AnswerStorageResult> UpdateSignatureAsync(
        int surveyId,
        int organizationId,
        string signature,
        byte[]? signedContent,
        CancellationToken cancellationToken = default)
        => _answerRepository.TrySaveAnswerSignatureAsync(surveyId, organizationId, signature, signedContent, cancellationToken);

    private Task<AnswerStorageResult> SaveDraftRecordAsync(
        AnswerRecord answerRecord,
        CancellationToken cancellationToken = default)
        => _answerRepository.SaveDraftAsync(answerRecord, cancellationToken);

    private Task<AnswerStorageResult> UpdateDraftSignatureAsync(
        int surveyId,
        int organizationId,
        string signature,
        byte[]? signedContent,
        CancellationToken cancellationToken = default)
        => _answerRepository.TrySaveDraftSignatureAsync(surveyId, organizationId, signature, signedContent, cancellationToken);

    private int GetRequiredCurrentUserId()
        => UserId ?? throw new InvalidOperationException("Не удалось определить текущего пользователя.");

    private Task DeleteDraftRecordAsync(int surveyId, int organizationId, CancellationToken cancellationToken = default)
        => _answerRepository.DeleteDraftAsync(surveyId, organizationId, cancellationToken);
}
