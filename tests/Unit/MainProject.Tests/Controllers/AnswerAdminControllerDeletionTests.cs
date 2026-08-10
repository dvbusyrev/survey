using System.Text.Json;
using MainProject.Application.DTO;
using MainProject.Application.UseCases.Answers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace MainProject.Tests.Controllers;

public sealed class AnswerAdminControllerDeletionTests
{
    [Fact]
    public async Task DeleteAnswer_ReturnsConflict_WhenSurveyIsInactive()
    {
        const string message = "Нельзя удалить ответ: анкета больше не активна.";
        var controller = new AnswerAdminController(
            new StubAnswerService(new OperationResult
            {
                Success = false,
                Message = message,
                Code = "survey_inactive"
            }),
            NullLogger<AnswerAdminController>.Instance);

        var actionResult = await controller.DeleteAnswer(42, CancellationToken.None);

        var conflictResult = Assert.IsType<ConflictObjectResult>(actionResult);
        var payload = JsonSerializer.SerializeToElement(conflictResult.Value);
        Assert.False(payload.GetProperty("success").GetBoolean());
        Assert.Equal(message, payload.GetProperty("message").GetString());
    }

    private sealed class StubAnswerService(OperationResult result) : AnswerService
    {
        public override Task<OperationResult> DeleteAnswerAsync(
            int answerId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }
}
