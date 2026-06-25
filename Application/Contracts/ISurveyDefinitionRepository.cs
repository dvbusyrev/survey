using MainProject.Application.DTO;
using MainProject.Domain.Entities;
using Npgsql;

namespace MainProject.Application.Contracts;

public interface ISurveyDefinitionRepository
{
    Task<int> CreateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string name,
        string? description,
        CancellationToken cancellationToken = default);

    Task<int> UpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        string name,
        string? description,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        CancellationToken cancellationToken = default);

    Task<Survey?> GetByIdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int surveyId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetCriteriaAsync(
        NpgsqlConnection connection,
        int surveyId,
        CancellationToken cancellationToken = default);

    Task AttachQuestionsAsync(
        NpgsqlConnection connection,
        IEnumerable<Survey> surveys,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SurveyQuestionItem>> GetQuestionsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        CancellationToken cancellationToken = default);

    Task ReplaceQuestionsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int surveyId,
        IReadOnlyCollection<SurveyQuestionItem> questions,
        CancellationToken cancellationToken = default);

    Task CopyQuestionsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int sourceSurveyId,
        int targetSurveyId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SurveySelectionItem>> GetSelectionOptionsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlySet<int>> GetExistingIdsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        IReadOnlyCollection<int> surveyIds,
        CancellationToken cancellationToken = default);
}
