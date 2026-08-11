using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MainProject.Application.Contracts;
using MainProject.Application.UseCases.Surveys;
using MainProject.Infrastructure.Security;
using MainProject.Web.ViewModels;
using System.Globalization;
using System.IO;
using System.Text.Json;

[Authorize]
public class HelpController : Controller
{
    private const string HelpDocumentContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const string AdminGuideType = "admin-guide";
    private const string UserGuideType = "user-guide";
    private const string AdminGuideStorageFileName = "admin_survey_guide.docx";
    private const string UserGuideStorageFileName = "user_survey_guide.docx";
    private const string AdminGuideDownloadFileName = "АИС Анкетирование. Инструкция администратора.docx";
    private const string UserGuideDownloadFileName = "АИС Анкетирование. Инструкция пользователя.docx";

    private readonly string _uploadFolder;
    private readonly SurveyService _surveyUserService;
    private readonly ICurrentUserService _currentUserService;

    public HelpController(
        SurveyService surveyUserService,
        ICurrentUserService currentUserService,
        IWebHostEnvironment environment)
    {
        _surveyUserService = surveyUserService;
        _currentUserService = currentUserService;
        var webRootPath = string.IsNullOrWhiteSpace(environment.WebRootPath)
            ? Path.Combine(environment.ContentRootPath, "wwwroot")
            : environment.WebRootPath;
        _uploadFolder = Path.Combine(webRootPath, "help_files");
    }

    [HttpGet("help/files/{type}")]
    public IActionResult HelpFile(string type)
    {
        var docxFilePath = ResolveHelpDocumentPath(type);

        if (string.IsNullOrWhiteSpace(docxFilePath) || !System.IO.File.Exists(docxFilePath))
        {
            return NotFound("Файл DOCX не найден.");
        }

        return PhysicalFile(docxFilePath, HelpDocumentContentType, GetDownloadFileName(type));
    }

    [HttpGet("help/download/{type?}")]
    public IActionResult DownloadHelpFile(string? type = null)
    {
        var documentType = string.IsNullOrWhiteSpace(type)
            ? GetDefaultHelpDocumentType()
            : type;
        var docxFilePath = ResolveHelpDocumentPath(documentType);

        if (string.IsNullOrWhiteSpace(docxFilePath) || !System.IO.File.Exists(docxFilePath))
        {
            return NotFound("Файл DOCX не найден.");
        }

        return PhysicalFile(docxFilePath, HelpDocumentContentType, GetDownloadFileName(documentType));
    }

    private string GetDefaultHelpDocumentType()
    {
        return User.IsInRole(AppRoles.Admin) ? AdminGuideType : UserGuideType;
    }

    private string? ResolveHelpDocumentPath(string type)
    {
        var normalizedType = NormalizeHelpDocumentType(type) ?? type?.Trim().ToLowerInvariant();
        var aliases = normalizedType switch
        {
            AdminGuideType => new[]
            {
                AdminGuideStorageFileName,
                "Руководство администратора.docx"
            },
            UserGuideType => new[]
            {
                UserGuideStorageFileName,
                "Руководство пользователя.docx"
            },
            "csp-guide" or "csp_guide" or "csp" => new[]
            {
                "csp_guide.docx",
                "Работа с CSP(КриптоПро plugin).docx"
            },
            _ => Array.Empty<string>()
        };

        foreach (var fileName in aliases)
        {
            var fullPath = Path.Combine(_uploadFolder, fileName);
            if (System.IO.File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }

    private static string? NormalizeHelpDocumentType(string? type)
    {
        return type?.Trim().ToLowerInvariant() switch
        {
            "admin-guide" or "admin_guide" or "admin" or "administrator" => AdminGuideType,
            "user-guide" or "user_guide" or "user" or "client" => UserGuideType,
            _ => null
        };
    }

    private static string GetStorageFileName(string type)
    {
        return NormalizeHelpDocumentType(type) == AdminGuideType
            ? AdminGuideStorageFileName
            : UserGuideStorageFileName;
    }

    private static string GetDownloadFileName(string type)
    {
        return NormalizeHelpDocumentType(type) == AdminGuideType
            ? AdminGuideDownloadFileName
            : UserGuideDownloadFileName;
    }

    private static bool IsValidDocx(Stream stream)
    {
        try
        {
            stream.Position = 0;
            using var document = WordprocessingDocument.Open(stream, false);
            return document.DocumentType == WordprocessingDocumentType.Document
                && document.MainDocumentPart?.Document?.Body is not null;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return false;
        }
        finally
        {
            stream.Position = 0;
        }
    }

    private string GetMetadataPath(string type)
    {
        var storageFileName = GetStorageFileName(type);
        return Path.Combine(_uploadFolder, $"{Path.GetFileNameWithoutExtension(storageFileName)}.meta.json");
    }

    private HelpInstructionFileMetadata? ReadHelpMetadata(string type)
    {
        var metadataPath = GetMetadataPath(type);
        if (!System.IO.File.Exists(metadataPath))
        {
            return null;
        }

        try
        {
            var json = System.IO.File.ReadAllText(metadataPath);
            return JsonSerializer.Deserialize<HelpInstructionFileMetadata>(json);
        }
        catch
        {
            return null;
        }
    }

    private static string FormatHelpFileDate(DateTimeOffset value)
    {
        return value.ToLocalTime().ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("ru-RU"));
    }

    private HelpInstructionInfoViewModel BuildInstructionInfo(string type, string title)
    {
        var normalizedType = NormalizeHelpDocumentType(type) ?? UserGuideType;
        var filePath = ResolveHelpDocumentPath(normalizedType);
        var hasFile = !string.IsNullOrWhiteSpace(filePath) && System.IO.File.Exists(filePath);
        var metadata = ReadHelpMetadata(normalizedType);
        var fallbackDate = hasFile
            ? new DateTimeOffset(System.IO.File.GetLastWriteTime(filePath!))
            : DateTimeOffset.Now;
        var uploadedAt = metadata?.UploadedAt ?? fallbackDate;
        var fileName = !string.IsNullOrWhiteSpace(metadata?.OriginalFileName)
            ? metadata.OriginalFileName
            : GetDownloadFileName(normalizedType);

        return new HelpInstructionInfoViewModel
        {
            Type = normalizedType,
            UploadRole = normalizedType == AdminGuideType ? "admin" : "user",
            Title = title,
            FileName = fileName,
            UploadedAtText = hasFile ? FormatHelpFileDate(uploadedAt) : string.Empty,
            DownloadUrl = $"/help/download/{normalizedType}",
            HasFile = hasFile
        };
    }

    private HelpPageViewModel BuildAdminHelpPageModel()
    {
        return new HelpPageViewModel
        {
            AdminInstruction = BuildInstructionInfo(AdminGuideType, "Инструкция для администратора"),
            ClientInstruction = BuildInstructionInfo(UserGuideType, "Инструкция для клиента")
        };
    }

    [HttpGet("help")]
    public async Task<IActionResult> HelpPage(CancellationToken cancellationToken = default)
    {
        if (!User.IsInRole(AppRoles.Admin))
        {
            var documentModel = new HelpDocumentViewModel();
            ViewData["ClientSurveyActiveCount"] = await GetCurrentClientActiveSurveyCountAsync(cancellationToken);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("~/Views/Help/_ClientHelpContent.cshtml", documentModel);
            }

            ViewBag.SurveyUserBootstrapJson = await BuildClientSurveyBootstrapJsonAsync(cancellationToken);
            return View("client_help_page", documentModel);
        }

        return View("help_page", BuildAdminHelpPageModel());
    }

    private async Task<int> GetCurrentClientActiveSurveyCountAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue || userId.Value <= 0)
        {
            return 0;
        }

        return (await _surveyUserService.GetActiveSurveysPageAsync(userId.Value, 1, null, cancellationToken))?.TotalCount ?? 0;
    }

    private async Task<string> BuildClientSurveyBootstrapJsonAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId ?? 0;
        var userOrganizationId = userId > 0
            ? await _surveyUserService.GetUserOrganizationIdAsync(userId, cancellationToken) ?? 0
            : 0;
        var userName = _currentUserService.UserName;
        var organizationName = _currentUserService.OrganizationName;
        var displayName = !string.IsNullOrWhiteSpace(organizationName) && !string.IsNullOrWhiteSpace(userName)
            ? $"{organizationName}: {userName}"
            : (!string.IsNullOrWhiteSpace(userName) ? userName : AppRoles.GetDisplayName(_currentUserService.Role));

        return JsonSerializer.Serialize(new
        {
            initialTab = "help",
            userId,
            userRole = _currentUserService.Role,
            userOrganizationId,
            displayName,
            userName,
            organizationName
        });
    }

    [Authorize(Roles = AppRoles.Admin)]
    [HttpPost("help/upload")]
    public async Task<IActionResult> UploadInstruction(IFormFile file, string? role, string? type)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("Файл не выбран.");
        }

        var documentType = NormalizeHelpDocumentType(type) ?? NormalizeHelpDocumentType(role);
        if (string.IsNullOrWhiteSpace(documentType))
        {
            return BadRequest("Неверный тип инструкции.");
        }

        if (!string.Equals(Path.GetExtension(file.FileName), ".docx", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Можно загрузить только файл DOCX.");
        }

        await using var uploadedContent = new MemoryStream();
        await file.CopyToAsync(uploadedContent);
        if (!IsValidDocx(uploadedContent))
        {
            return BadRequest("Файл не является корректным документом DOCX.");
        }

        if (!Directory.Exists(_uploadFolder))
        {
            Directory.CreateDirectory(_uploadFolder);
        }

        var originalFileName = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            originalFileName = GetDownloadFileName(documentType);
        }

        var fileName = GetStorageFileName(documentType);
        string filePath = Path.Combine(_uploadFolder, fileName);

        await using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await uploadedContent.CopyToAsync(stream);
        }

        var metadata = new HelpInstructionFileMetadata(originalFileName, DateTimeOffset.Now);
        await System.IO.File.WriteAllTextAsync(
            GetMetadataPath(documentType),
            JsonSerializer.Serialize(metadata));

        var model = BuildInstructionInfo(
            documentType,
            documentType == AdminGuideType ? "Инструкция для администратора" : "Инструкция для клиента");

        return Ok(new
        {
            message = "Файл успешно загружен.",
            fileName = model.FileName,
            uploadedAt = model.UploadedAtText,
            displayText = model.DisplayText
        });
    }

    private sealed record HelpInstructionFileMetadata(string OriginalFileName, DateTimeOffset UploadedAt);
}
