using MainProject.Application.DTO.Theme;
using MainProject.Application.UseCases.Admin;
using MainProject.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace MainProject.Tests.Services;

public sealed class ThemeSettingsServiceTests
{
    [Fact]
    public async Task Save_RejectsMissingSettings()
    {
        var service = CreateService();

        var exception = await Assert.ThrowsAsync<ThemeSettingsValidationException>(
            () => service.SaveAsync(null!));

        Assert.Contains("Параметры темы не переданы.", exception.Errors);
    }

    [Theory]
    [InlineData("font-color", "Проверьте значение поля «Цвет шрифта».")]
    [InlineData("background-color", "Проверьте значение поля «Цвет фона».")]
    [InlineData("image-opacity", "Непрозрачность фонового изображения должна быть от 0 до 100.")]
    [InlineData("header-percent", "Поле «Яркость шапки» должно быть от 0 до 100.")]
    [InlineData("footer-percent", "Поле «Яркость подвала» должно быть от 0 до 100.")]
    [InlineData("button-percent", "Поле «Яркость кнопок» должно быть от 0 до 100.")]
    [InlineData("surface-percent", "Поле «Яркость деталей» должно быть от 0 до 100.")]
    [InlineData("file-name", "Имя файла фонового изображения должно содержать не более 255 символов.")]
    public async Task Save_RejectsInvalidValueInsteadOfCorrectingIt(string field, string expectedError)
    {
        var settings = CreateValidSettings();
        switch (field)
        {
            case "font-color":
                settings.FontColor = "invalid";
                break;
            case "background-color":
                settings.BackgroundColor = string.Empty;
                break;
            case "image-opacity":
                settings.BackgroundImageOpacity = -1;
                break;
            case "header-percent":
                settings.HeaderDarkenPercent = 101;
                break;
            case "footer-percent":
                settings.FooterDarkenPercent = -1;
                break;
            case "button-percent":
                settings.ButtonDarkenPercent = 101;
                break;
            case "surface-percent":
                settings.SurfaceTintOpacityPercent = -1;
                break;
            case "file-name":
                settings.BackgroundImageFileName = $"{new string('a', 252)}.png";
                break;
        }

        var service = CreateService();

        var exception = await Assert.ThrowsAsync<ThemeSettingsValidationException>(
            () => service.SaveAsync(settings));

        Assert.Contains(expectedError, exception.Errors);
    }

    private static ThemeSettings CreateValidSettings()
        => new()
        {
            FontColor = "#343D4B",
            BackgroundColor = "#B2A8FF",
            BackgroundImageOpacity = 35,
            HeaderDarkenPercent = 42,
            FooterDarkenPercent = 42,
            ButtonDarkenPercent = 42,
            SurfaceTintOpacityPercent = 59
        };

    private static ThemeSettingsService CreateService()
        => new(
            new UnexpectedConnectionFactory(),
            NullLogger<ThemeSettingsService>.Instance);

    private sealed class UnexpectedConnectionFactory : IDbConnectionFactory
    {
        public Task<NpgsqlConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Некорректная тема не должна обращаться к БД.");
    }
}
