using System.Text.Json;
using MainProject.Application.DTO.Theme;
using MainProject.Application.UseCases.Admin;
using MainProject.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace MainProject.Tests.Controllers;

public sealed class ThemeControllerTests
{
    [Fact]
    public async Task SaveThemeSettings_ReturnsValidationErrorsForInvalidValues()
    {
        var controller = CreateController();
        var settings = new ThemeSettings
        {
            FontColor = "invalid",
            BackgroundColor = "#B2A8FF",
            BackgroundImageOpacity = 101,
            HeaderDarkenPercent = 42,
            FooterDarkenPercent = 42,
            ButtonDarkenPercent = 42,
            SurfaceTintOpacityPercent = 59
        };

        var result = await controller.SaveThemeSettings(settings, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var errors = ReadErrors(badRequest.Value);
        Assert.Contains("Проверьте значение поля «Цвет шрифта».", errors);
        Assert.Contains("Непрозрачность фонового изображения должна быть от 0 до 100.", errors);
    }

    [Fact]
    public async Task SaveThemeSettings_ReturnsValidationErrorForMissingPayload()
    {
        var controller = CreateController();

        var result = await controller.SaveThemeSettings(null, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Параметры темы не переданы.", ReadErrors(badRequest.Value));
    }

    private static string[] ReadErrors(object? response)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(response));
        return document.RootElement
            .GetProperty("errors")
            .EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty)
            .ToArray();
    }

    private static ThemeController CreateController()
        => new(new ThemeSettingsService(
            new UnexpectedConnectionFactory(),
            NullLogger<ThemeSettingsService>.Instance));

    private sealed class UnexpectedConnectionFactory : IDbConnectionFactory
    {
        public Task<NpgsqlConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Некорректная тема не должна обращаться к БД.");
    }
}
