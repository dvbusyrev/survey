using Microsoft.AspNetCore.Authorization;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Mvc;
using MainProject.Infrastructure.Security;
using System.IO;
using System.Text;

[Authorize]
public class HelpController : Controller
{
    private readonly string _uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "help_files");

    [ActionName("help_file")]
    public IActionResult HelpFile(string type)
    {
        var docxFilePath = ResolveHelpDocumentPath(type);

        if (string.IsNullOrWhiteSpace(docxFilePath) || !System.IO.File.Exists(docxFilePath))
        {
            return NotFound("Файл DOCX не найден.");
        }

        try
        {
            // Считываем содержимое DOCX файла
            string htmlContent = ConvertDocxToHtml(docxFilePath);

            // Передаем HTML-содержимое в представление через ViewBag
            ViewBag.HtmlContent = htmlContent;

            // Возвращаем представление
            return View();
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Произошла ошибка: {ex.Message}");
        }
    }

    private string? ResolveHelpDocumentPath(string type)
    {
        var helpDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "help_files");
        var aliases = type?.Trim().ToLowerInvariant() switch
        {
            "admin-guide" or "admin_guide" => new[]
            {
                "admin_survey_guide.docx",
                "Руководство администратора.docx"
            },
            "user-guide" or "user_guide" => new[]
            {
                "user_survey_guide.docx",
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
            var fullPath = Path.Combine(helpDirectory, fileName);
            if (System.IO.File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }

    private string ConvertDocxToHtml(string docxFilePath)
    {
        StringBuilder htmlBuilder = new StringBuilder();

        // Открываем DOCX файл с помощью OpenXML
        using (WordprocessingDocument document = WordprocessingDocument.Open(docxFilePath, false))
        {
            // Получаем основной текст документа
            Body body = document.MainDocumentPart.Document.Body;

            // Обрабатываем каждый элемент в документе
            foreach (var element in body.Elements())
            {
                if (element is Paragraph paragraph)
                {
                    // Добавляем текст параграфа в HTML
                    htmlBuilder.Append($"<p>{GetTextFromParagraph(paragraph)}</p>");
                }
                else if (element is Table table)
                {
                    // Обрабатываем таблицы (если нужно)
                    htmlBuilder.Append("<table>");
                    foreach (var row in table.Elements<TableRow>())
                    {
                        htmlBuilder.Append("<tr>");
                        foreach (var cell in row.Elements<TableCell>())
                        {
                            htmlBuilder.Append($"<td>{GetTextFromTableCell(cell)}</td>");
                        }
                        htmlBuilder.Append("</tr>");
                    }
                    htmlBuilder.Append("</table>");
                }
            }

            // Обрабатываем изображения
            foreach (var imagePart in document.MainDocumentPart.ImageParts)
            {
                using (var imageStream = imagePart.GetStream())
                {
                    // Читаем изображение в массив байтов
                    byte[] imageBytes = new byte[imageStream.Length];
                    imageStream.Read(imageBytes, 0, imageBytes.Length);

                    // Преобразуем массив байтов в строку Base64
                    string base64Image = Convert.ToBase64String(imageBytes);

                    // Добавляем изображение в HTML
                    htmlBuilder.Append($"<img src='data:image/png;base64,{base64Image}' />");
                }
            }
        }

        return htmlBuilder.ToString();
    }

    [ActionName("help_page")]
    public IActionResult HelpPage()
{


    return View();
}

[Authorize(Roles = AppRoles.Admin)]
[HttpPost]
    [ActionName("upload_instruction")]
    public async Task<IActionResult> UploadInstruction(IFormFile file, string role)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("Файл не выбран.");
        }

        if (string.IsNullOrEmpty(role) || (role != "admin" && role != "administrator" && role != "user"))
        {
            return BadRequest("Неверная роль.");
        }

        // Определяем имя файла в зависимости от роли
        string fileName = (role == "admin" || role == "administrator")
            ? "admin_survey_guide" + Path.GetExtension(file.FileName)
            : "user_survey_guide" + Path.GetExtension(file.FileName);

        // Убеждаемся, что папка существует
        if (!Directory.Exists(_uploadFolder))
        {
            Directory.CreateDirectory(_uploadFolder);
        }

        string filePath = Path.Combine(_uploadFolder, fileName);

        // Сохраняем файл
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return Ok(new { message = "Файл успешно загружен.", fileName });
    }


    private string GetTextFromParagraph(Paragraph paragraph)
    {
        StringBuilder textBuilder = new StringBuilder();

        foreach (var run in paragraph.Elements<Run>())
        {
            textBuilder.Append(run.InnerText);
        }

        return textBuilder.ToString();
    }

    private string GetTextFromTableCell(TableCell cell)
    {
        StringBuilder textBuilder = new StringBuilder();

        foreach (var paragraph in cell.Elements<Paragraph>())
        {
            textBuilder.Append(GetTextFromParagraph(paragraph));
        }

        return textBuilder.ToString();
    }
}
