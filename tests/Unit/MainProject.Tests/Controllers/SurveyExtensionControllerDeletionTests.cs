using System.Text.Json;
using MainProject.Application.DTO;
using MainProject.Application.UseCases.Surveys;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace MainProject.Tests.Controllers;

public sealed class SurveyExtensionControllerDeletionTests
{
    [Fact]
    public async Task DeleteExtension_ReturnsConflict_WhenAssignmentHasAnswer()
    {
        const string message = "Нельзя удалить продление анкеты: по нему есть ответы.";
        var controller = CreateController(new OperationResult
        {
            Message = message,
            Code = "extension_in_use"
        });

        var actionResult = await controller.DeleteExtension(42, 7, CancellationToken.None);

        var conflictResult = Assert.IsType<ConflictObjectResult>(actionResult);
        var payload = JsonSerializer.SerializeToElement(conflictResult.Value);
        Assert.False(payload.GetProperty("success").GetBoolean());
        Assert.Equal(message, payload.GetProperty("message").GetString());
    }

    [Fact]
    public async Task DeleteExtension_ReturnsSuccess_WhenAssignmentWasDeleted()
    {
        const string message = "Продление успешно удалено.";
        var controller = CreateController(new OperationResult
        {
            Success = true,
            Message = message
        });

        var actionResult = await controller.DeleteExtension(42, 7, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var payload = JsonSerializer.SerializeToElement(okResult.Value);
        Assert.True(payload.GetProperty("success").GetBoolean());
        Assert.Equal(message, payload.GetProperty("message").GetString());
    }

    private static SurveyExtensionController CreateController(OperationResult result) =>
        new(
            new StubSurveyService(result),
            NullLogger<SurveyExtensionController>.Instance);

    private sealed class StubSurveyService(OperationResult result) : SurveyService
    {
        public override Task<OperationResult> DeleteExtensionAsync(
            int surveyId,
            int organizationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }
}
