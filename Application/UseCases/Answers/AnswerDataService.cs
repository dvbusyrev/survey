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

    public int? GetUserOrganizationId(int userId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return _assignmentRepository.GetUserOrganizationId(connection, userId);
    }

    public bool IsSurveyAssignedToOrganization(int surveyId, int organizationId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return _assignmentRepository.IsActiveAssignment(connection, surveyId, organizationId);
    }

    public bool AnswerRecordExists(int surveyId, int organizationId)
        => _answerRepository.AnswerRecordExists(surveyId, organizationId);

    public Survey? GetSurveyInfo(int surveyId)
        => _answerRepository.GetSurveyInfo(surveyId);

    public IReadOnlyList<SurveyQuestionItem> GetSurveyQuestions(int surveyId)
        => _answerRepository.GetSurveyQuestions(surveyId);

    public AnswerRecord? GetAnswerRecord(int surveyId, int organizationId)
        => _answerRepository.GetAnswerRecord(surveyId, organizationId);

    public AnswerRecord? GetDraftRecord(int surveyId, int organizationId)
        => _answerRepository.GetDraftRecord(surveyId, organizationId);

    public IReadOnlyList<AnswerRecord> GetAnswerRecords(int surveyId, int? organizationId = null)
        => _answerRepository.GetAnswerRecords(surveyId, organizationId);

    public int InsertAnswerRecord(AnswerRecord answerRecord)
    {
        var result = _answerRepository.SubmitAnswer(answerRecord);
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

    public bool UpdateAnswerRecord(AnswerRecord answerRecord)
    {
        var result = _answerRepository.UpdateAnswer(answerRecord);
        if (result.AlreadySigned)
        {
            throw new AnswerAlreadySignedException();
        }

        return result.Found;
    }

    public bool UpdateSignature(int surveyId, int organizationId, string signature, byte[]? signedContent)
        => _answerRepository.TrySaveAnswerSignature(surveyId, organizationId, signature, signedContent);

    public bool SaveDraftRecord(AnswerRecord answerRecord)
        => _answerRepository.SaveDraft(answerRecord);

    public bool UpdateDraftSignature(int surveyId, int organizationId, string signature, byte[]? signedContent)
        => _answerRepository.TrySaveDraftSignature(surveyId, organizationId, signature, signedContent);

    public void DeleteDraftRecord(int surveyId, int organizationId)
        => _answerRepository.DeleteDraft(surveyId, organizationId);
}
