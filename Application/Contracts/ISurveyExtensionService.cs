using MainProject.Application.DTO;

namespace MainProject.Application.Contracts;

public interface ISurveyExtensionService
{
    Task<OperationResult> SaveExtensionsAsync(SurveyExtensionRequest request, CancellationToken cancellationToken = default);
}
