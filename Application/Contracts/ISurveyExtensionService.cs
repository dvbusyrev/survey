using MainProject.Application.DTO;

namespace MainProject.Application.Contracts;

public interface ISurveyExtensionService
{
    OperationResult SaveExtensions(SurveyExtensionRequest request);
}
