using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;

namespace MainProject.Tests.Controllers;

public sealed class HelpControllerTests
{
    [Fact]
    public void DownloadHelpFile_UsesConfiguredWebRootPath()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), $"survey-help-{Guid.NewGuid():N}");
        var webRoot = Path.Combine(contentRoot, "published-wwwroot");
        var helpDirectory = Path.Combine(webRoot, "help_files");
        var helpFile = Path.Combine(helpDirectory, "user_survey_guide.docx");

        try
        {
            Directory.CreateDirectory(helpDirectory);
            File.WriteAllBytes(helpFile, [1, 2, 3]);

            var controller = new HelpController(
                null!,
                null!,
                new StubWebHostEnvironment(contentRoot, webRoot));

            var result = controller.DownloadHelpFile("user-guide");

            var fileResult = Assert.IsType<PhysicalFileResult>(result);
            Assert.Equal(helpFile, fileResult.FileName);
            Assert.Equal(
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                fileResult.ContentType);
            Assert.Equal("АИС Анкетирование. Инструкция пользователя.docx", fileResult.FileDownloadName);
        }
        finally
        {
            if (Directory.Exists(contentRoot))
            {
                Directory.Delete(contentRoot, recursive: true);
            }
        }
    }

    private sealed class StubWebHostEnvironment(string contentRootPath, string webRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "MainProject.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = "Development";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = webRootPath;
    }
}
