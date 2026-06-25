using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Domain.Entities;
using MainProject.Infrastructure.Persistence;

namespace MainProject.Application.UseCases.Answers;

public sealed class AnswerDataService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ISurveyAssignmentRepository _assignmentRepository;
    private readonly IAnswerRepository _answerRepository;

    public AnswerDataService(
        IDbConnectionFactory connectionFactory,
        ISurveyAssignmentRepository assignmentRepository,
        IAnswerRepository answerRepository)
    {
        _connectionFactory = connectionFactory;
        _assignmentRepository = assignmentRepository;
        _answerRepository = answerRepository;
    }

    public async Task<int?> GetUserOrganizationIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await _assignmentRepository.GetUserOrganizationIdAsync(connection, userId, cancellationToken);
    }

    public async Task<bool> IsSurveyAssignedToOrganizationAsync(
        int surveyId,
        int organizationId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        return await _assignmentRepository.IsActiveAssignmentAsync(connection, surveyId, organizationId, cancellationToken);
    }

    public Task<bool> AnswerRecordExistsAsync(int surveyId, int organizationId, CancellationToken cancellationToken = default)
        => _answerRepository.AnswerRecordExistsAsync(surveyId, organizationId, cancellationToken);

    public Task<Survey?> GetSurveyInfoAsync(int surveyId, CancellationToken cancellationToken = default)
        => _answerRepository.GetSurveyInfoAsync(surveyId, cancellationToken);

    public Task<IReadOnlyList<SurveyQuestionItem>> GetSurveyQuestionsAsync(int surveyId, CancellationToken cancellationToken = default)
        => _answerRepository.GetSurveyQuestionsAsync(surveyId, cancellationToken);

    public Task<AnswerRecord?> GetAnswerRecordAsync(int surveyId, int organizationId, CancellationToken cancellationToken = default)
        => _answerRepository.GetAnswerRecordAsync(surveyId, organizationId, cancellationToken);

    public Task<AnswerRecord?> GetDraftRecordAsync(int surveyId, int organizationId, CancellationToken cancellationToken = default)
        => _answerRepository.GetDraftRecordAsync(surveyId, organizationId, cancellationToken);

    public Task<IReadOnlyList<AnswerRecord>> GetAnswerRecordsAsync(
        int surveyId,
        int? organizationId = null,
        CancellationToken cancellationToken = default)
        => _answerRepository.GetAnswerRecordsAsync(surveyId, organizationId, cancellationToken);

    public async Task<int> InsertAnswerRecordAsync(AnswerRecord answerRecord, CancellationToken cancellationToken = default)
    {
        var result = await _answerRepository.SubmitAnswerAsync(answerRecord, cancellationToken);
        if (!result.Found)
        {
            throw new InvalidOperationException("Назначение анкеты для организации не найдено.");
        }

        if (result.AlreadySigned)
        {
            throw new AnswerAlreadySignedException();
        }

        return result.AnswerId;
    }

    public async Task<bool> UpdateAnswerRecordAsync(AnswerRecord answerRecord, CancellationToken cancellationToken = default)
    {
        var result = await _answerRepository.UpdateAnswerAsync(answerRecord, cancellationToken);
        if (result.AlreadySigned)
        {
            throw new AnswerAlreadySignedException();
        }

        return result.Found;
    }

    public Task<bool> UpdateSignatureAsync(
        int surveyId,
        int organizationId,
        string signature,
        byte[]? signedContent,
        CancellationToken cancellationToken = default)
        => _answerRepository.TrySaveAnswerSignatureAsync(surveyId, organizationId, signature, signedContent, cancellationToken);

    public Task<bool> SaveDraftRecordAsync(AnswerRecord answerRecord, CancellationToken cancellationToken = default)
        => _answerRepository.SaveDraftAsync(answerRecord, cancellationToken);

    public Task<bool> UpdateDraftSignatureAsync(
        int surveyId,
        int organizationId,
        string signature,
        byte[]? signedContent,
        CancellationToken cancellationToken = default)
        => _answerRepository.TrySaveDraftSignatureAsync(surveyId, organizationId, signature, signedContent, cancellationToken);

    public Task DeleteDraftRecordAsync(int surveyId, int organizationId, CancellationToken cancellationToken = default)
        => _answerRepository.DeleteDraftAsync(surveyId, organizationId, cancellationToken);
}
