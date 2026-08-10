using System.Text.Json;
using MainProject.Application.DTO;
using MainProject.Application.UseCases.Surveys;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace MainProject.Tests.Controllers;

public sealed class SurveyAdminControllerDeletionTests
{
    [Fact]
    public async Task DeleteSurvey_ReturnsConflictWithBusinessMessage_WhenSurveyIsInUse()
    {
        const string message = "Нельзя удалить анкету \"Тестовая анкета\": по ней есть ответы.";
        var controller = new SurveyAdminController(
            new StubSurveyService(new OperationResult
            {
                Success = false,
                Message = message,
                Code = "survey_in_use"
            }),
            NullLogger<SurveyAdminController>.Instance);

        var actionResult = await controller.DeleteSurvey(
            42,
            new DeleteSurveyRequest { SurveyId = 42 },
            CancellationToken.None);

        var conflictResult = Assert.IsType<ConflictObjectResult>(actionResult);
        var payload = JsonSerializer.SerializeToElement(conflictResult.Value);
        Assert.False(payload.GetProperty("success").GetBoolean());
        Assert.Equal(message, payload.GetProperty("message").GetString());
    }

    private sealed class StubSurveyService(OperationResult result) : SurveyService
    {
        public override Task<OperationResult> DeleteSurveyAsync(
            int surveyId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(result);
    }
}
