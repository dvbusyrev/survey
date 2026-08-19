using System.IO.Compression;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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

    [Fact]
    public async Task UploadInstruction_RejectsRenamedTextAndPreservesCurrentFile()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), $"survey-help-{Guid.NewGuid():N}");
        var webRoot = Path.Combine(contentRoot, "wwwroot");
        var helpDirectory = Path.Combine(webRoot, "help_files");
        var storedFile = Path.Combine(helpDirectory, "user_survey_guide.docx");
        var originalContent = new byte[] { 9, 8, 7 };

        try
        {
            Directory.CreateDirectory(helpDirectory);
            await File.WriteAllBytesAsync(storedFile, originalContent);
            var controller = CreateController(contentRoot, webRoot);
            var file = CreateFormFile(Encoding.UTF8.GetBytes("Это не документ Word"), "fake.docx");

            var result = await controller.UploadInstruction(file, null, "user-guide");

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Файл не является корректным документом DOCX.", badRequest.Value);
            Assert.Equal(originalContent, await File.ReadAllBytesAsync(storedFile));
        }
        finally
        {
            DeleteDirectory(contentRoot);
        }
    }

    [Fact]
    public async Task UploadInstruction_RejectsZipWithoutWordDocumentParts()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), $"survey-help-{Guid.NewGuid():N}");
        var webRoot = Path.Combine(contentRoot, "wwwroot");

        try
        {
            var controller = CreateController(contentRoot, webRoot);
            var file = CreateFormFile(CreateArbitraryZip(), "archive.docx");

            var result = await controller.UploadInstruction(file, null, "user-guide");

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Файл не является корректным документом DOCX.", badRequest.Value);
            Assert.False(File.Exists(Path.Combine(webRoot, "help_files", "user_survey_guide.docx")));
        }
        finally
        {
            DeleteDirectory(contentRoot);
        }
    }

    [Fact]
    public async Task UploadInstruction_AcceptsWordprocessingDocument()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), $"survey-help-{Guid.NewGuid():N}");
        var webRoot = Path.Combine(contentRoot, "wwwroot");
        var storedFile = Path.Combine(webRoot, "help_files", "admin_survey_guide.docx");

        try
        {
            var controller = CreateController(contentRoot, webRoot);
            var file = CreateFormFile(CreateDocx("Инструкция"), "instruction.docx");

            var result = await controller.UploadInstruction(file, null, "admin-guide");

            Assert.IsType<OkObjectResult>(result);
            Assert.True(File.Exists(storedFile));
            using var document = WordprocessingDocument.Open(storedFile, false);
            Assert.Equal("Инструкция", document.MainDocumentPart?.Document?.Body?.InnerText);

            var download = Assert.IsType<PhysicalFileResult>(controller.DownloadHelpFile("admin-guide"));
            Assert.Equal("instruction.docx", download.FileDownloadName);
        }
        finally
        {
            DeleteDirectory(contentRoot);
        }
    }

    private static HelpController CreateController(string contentRoot, string webRoot)
    {
        return new HelpController(
            null!,
            null!,
            new StubWebHostEnvironment(contentRoot, webRoot));
    }

    private static IFormFile CreateFormFile(byte[] content, string fileName)
    {
        return new FormFile(new MemoryStream(content), 0, content.Length, "file", fileName);
    }

    private static byte[] CreateDocx(string text)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body(new Paragraph(new Run(new Text(text)))));
            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    private static byte[] CreateArbitraryZip()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("document.txt");
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write("Это ZIP, но не DOCX");
        }

        return stream.ToArray();
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
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
