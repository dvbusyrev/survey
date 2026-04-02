using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using main_project.Models;
using System.Data;
using System.IO.Compression;
using Npgsql;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using QuestPDF.Helpers;
using Microsoft.Extensions.Logging;
using QuestPDFDocument = QuestPDF.Fluent.Document;
using NewtonsoftJson = Newtonsoft.Json.JsonConvert;
using SystemJson = System.Text.Json.JsonSerializer;
using QuestDocument = QuestPDF.Fluent.Document;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Body = DocumentFormat.OpenXml.Wordprocessing.Body;
using Table = DocumentFormat.OpenXml.Wordprocessing.Table;
using TableRow = DocumentFormat.OpenXml.Wordprocessing.TableRow;
using TableCell = DocumentFormat.OpenXml.Wordprocessing.TableCell;
using Paragraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using Run = DocumentFormat.OpenXml.Wordprocessing.Run;
using Text = DocumentFormat.OpenXml.Wordprocessing.Text;
using TableCellProperties = DocumentFormat.OpenXml.Wordprocessing.TableCellProperties;
using GridSpan = DocumentFormat.OpenXml.Wordprocessing.GridSpan;
using WordDocument = DocumentFormat.OpenXml.Wordprocessing.Document;



[Authorize]
public class AnswerController : Controller
{

    private int? GetCurrentUserId()
    {
        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    }

    private bool IsAdmin()
    {
        return User.IsInRole("Админ");
    }

    private int? GetCurrentUserOmsuId()
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return null;

        using (var connection = _db.CreateConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT id_omsu FROM public.users WHERE id_user = @userId";
            command.Parameters.Add(new NpgsqlParameter("@userId", currentUserId.Value));
            var result = command.ExecuteScalar();
            return result == null || result == DBNull.Value ? null : Convert.ToInt32(result);
        }
    }

    private IActionResult? EnsureOmsuAccess(int requestedOmsuId)
    {
        if (IsAdmin())
            return null;

        var currentOmsuId = GetCurrentUserOmsuId();
        if (!currentOmsuId.HasValue)
            return Forbid();

        if (currentOmsuId.Value != requestedOmsuId)
            return Forbid();

        return null;
    }

    private readonly DatabaseController _db;
    private readonly string _connectionString;
    private readonly ILogger<AnswerController> _logger;

    private class PdfAnswerItem
    {
        public string? Question { get; set; }
        public string? Rating { get; set; }
        public string? Comment { get; set; }
    }

private class AnswerWrapper
{
    public List<AnswerItem> Answers { get; set; }
}

public class AnswerItem
{
    [JsonProperty("question_text")]
    public string QuestionText { get; set; }
    
    [JsonProperty("text")]
    public string Text { get; set; } 
    
    [JsonProperty("rating")]
    public string Rating { get; set; }
    
    [JsonProperty("comment")]
    public string Comment { get; set; }

    public string Question => QuestionText ?? Text;
}


    public AnswerController(
         IConfiguration configuration, 
        DatabaseController db,
        ILogger<AnswerController> logger)
    {
        _db = db;
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? 
            throw new ArgumentNullException("DefaultConnection");
        _logger = logger;
    }



[HttpGet("create_pdf_report/{idSurvey}/{idOmsu}")]
public async Task<IActionResult> CreatePdfReport(int idSurvey, int idOmsu)
{
    try
    {
        // Получаем информацию об анкете
        Survey survey = null;
        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using (var command = new NpgsqlCommand(@"
                SELECT id_survey, name_survey, description 
                FROM surveys 
                WHERE id_survey = @idSurvey
                
                UNION ALL
                
                SELECT id_survey, name_survey, description 
                FROM history_surveys 
                WHERE id_survey = @idSurvey
                AND NOT EXISTS (SELECT 1 FROM surveys WHERE id_survey = @idSurvey)
                
                LIMIT 1", connection))
            {
                command.Parameters.AddWithValue("@idSurvey", idSurvey);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        survey = new Survey
                        {
                            id_survey = reader.GetInt32(0),
                            name_survey = reader.IsDBNull(1) ? null : reader.GetString(1),
                            description = reader.IsDBNull(2) ? null : reader.GetString(2)
                        };
                    }
                }
            }
        }

        if (survey == null)
        {
            _logger.LogWarning($"Анкета с ID {idSurvey} не найдена");
            return NotFound("Анкета не найдена");
        }

        // Получаем ответы из базы данных
        List<HistoryAnswer> answers = new List<HistoryAnswer>();
        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using (var command = new NpgsqlCommand(@"
                SELECT id_answer, id_omsu, id_survey, csp, completion_date, answers 
                FROM public.history_answer 
                WHERE id_survey = @idSurvey AND id_omsu = @idOmsu", connection))
            {
                command.Parameters.AddWithValue("@idSurvey", idSurvey);
                command.Parameters.AddWithValue("@idOmsu", idOmsu);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        answers.Add(new HistoryAnswer
                        {
                            id_answer = reader.GetInt32(0),
                            id_omsu = reader.GetInt32(1),
                            id_survey = reader.GetInt32(2),
                            csp = reader.IsDBNull(3) ? null : reader.GetString(3),
                            completion_date = reader.GetDateTime(4),
                            answers = reader.GetString(5)
                        });
                    }
                }
            }
        }

        if (!answers.Any())
        {
            _logger.LogWarning($"Ответы для анкеты {idSurvey} и ОМСУ {idOmsu} не найдены");
            return NotFound("Ответы не найдены");
        }

        // Генерируем PDF
        QuestPDF.Settings.License = LicenseType.Community;
        byte[] pdfBytes;

        try
        {
            // Явно указываем пространство имен QuestPDF.Fluent
            var document = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(style => style
                        .FontSize(12)
                        .FontFamily("Arial")
                        .Fallback(x => x.FontFamily("Times New Roman")));

                    page.Header()
                        .AlignCenter()
                        .PaddingBottom(15)
                        .Text($"Анкета: {survey.name_survey}")
                        .Bold()
                        .FontSize(18);

                    page.Content()
                        .Column(column =>
                        {
                            if (!string.IsNullOrEmpty(survey.description))
                            {
                                column.Item()
                                    .Border(1)
                                    .BorderColor(Colors.Grey.Medium)
                                    .Padding(10)
                                    .Text(survey.description);
                            }

                            column.Item()
                                .PaddingTop(15)
                                .Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(3);
                                        columns.RelativeColumn(1);
                                        columns.RelativeColumn(3);
                                    });

                                    table.Header(header =>
                                    {
                                        header.Cell().Text("Вопрос").Bold();
                                        header.Cell().AlignCenter().Text("Оценка").Bold();
                                        header.Cell().Text("Комментарий").Bold();
                                    });

                                    foreach (var answer in answers)
                                    {
                                        var answerData = ParseAnswerData(answer.answers);
                                        
                                        foreach (var item in answerData)
                                        {
                                            table.Cell()
                                                .BorderBottom(1)
                                                .Padding(5)
                                                .Text(item.Question ?? "Вопрос не указан");

                                            table.Cell()
                                                .BorderBottom(1)
                                                .AlignCenter()
                                                .Padding(5)
                                                .Text(item.Rating ?? "0");

                                            table.Cell()
                                                .BorderBottom(1)
                                                .Padding(5)
                                                .Text(item.Comment ?? "Нет комментария");
                                        }
                                    }
                                });
                        });
                });
            });

            using var stream = new MemoryStream();
            await Task.Run(() => document.GeneratePdf(stream));
            pdfBytes = stream.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка генерации PDF");
            return StatusCode(500, "Ошибка при создании PDF");
        }

        // Формируем имя файла
        var fileName = $"{CleanFileName(survey.name_survey ?? "Анкета")}_ответы_{DateTime.Now:yyyyMMdd}.pdf";
        
        return File(pdfBytes, "application/pdf", fileName);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Ошибка генерации PDF");
        return StatusCode(500, "Ошибка при создании PDF");
    }
}

private string CleanFileName(string fileName)
{
    if (string.IsNullOrEmpty(fileName))
        return "Анкета";

    var invalidChars = Path.GetInvalidFileNameChars();
    return string.Concat(fileName.Split(invalidChars));
}


private async Task<byte[]?> GeneratePdfBytes(Survey survey, List<HistoryAnswer> answers)
    {
        try
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var document = QuestDocument.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(style => style
                        .FontSize(12)
                        .FontFamily("Arial")
                        .Fallback(x => x.FontFamily("Times New Roman"))
                    );

                    page.Header()
                        .AlignCenter()
                        .Text($"Анкета: {survey.name_survey}")
                        .Bold().FontSize(18);

                    page.Content()
                        .PaddingVertical(1, Unit.Centimetre)
                        .Column(column =>
                        {
                            if (!string.IsNullOrEmpty(survey.description))
                            {
                                column.Item().Text(survey.description);
                                column.Item().PaddingBottom(10);
                            }

                            column.Item().Text("Ответы:").Bold().FontSize(14);
                            column.Item().PaddingBottom(10);

                            foreach (var answer in answers)
                            {
                                var answerData = ParseAnswerData(answer.answers);
                                if (answerData.Count == 0)
                                {
                                    _logger.LogWarning($"Empty answer data for answer ID: {answer.id_answer}");
                                    continue;
                                }

                                column.Item().Table(table => 
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(3);
                                        columns.RelativeColumn(1);
                                        columns.RelativeColumn(2);
                                    });

                                    table.Header(header =>
                                    {
                                        header.Cell().Text("Вопрос").Bold();
                                        header.Cell().Text("Оценка").Bold();
                                        header.Cell().Text("Комментарий").Bold();
                                    });

                                    foreach (var item in answerData)
                                    {
                                        table.Cell().PaddingVertical(5).Text(item.QuestionText ?? "Нет вопроса");
                                        table.Cell().AlignCenter().Text(item.Rating ?? "0");
                                        table.Cell().PaddingVertical(5).Text(item.Comment ?? "Нет комментария");
                                    }
                                });
                                
                                column.Item().PaddingBottom(20);
                            }
                        });
                });
            });

            using var stream = new MemoryStream();
            await Task.Run(() => document.GeneratePdf(stream));
            return stream.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDF generation error");
            return null;
        }
    }

private void AddSurveyContent(ColumnDescriptor column, Survey survey, List<HistoryAnswer> answers)
{
    if (!string.IsNullOrEmpty(survey.description))
    {
        column.Item().Text(survey.description);
        column.Item().PaddingBottom(10);
    }

    column.Item().Text("Ответы:").Bold().FontSize(14);
    column.Item().PaddingBottom(10);

    foreach (var answer in answers)
    {
        var answerData = ParseAnswerData(answer.answers);
        foreach (var item in answerData)
        {
            column.Item().Text(item.QuestionText ?? "Вопрос не указан").Bold();
            column.Item().Text($"Оценка: {item.Rating ?? "0"}/5");

            if (!string.IsNullOrEmpty(item.Comment))
            {
                column.Item().Text(t => 
                {
                    t.Span("Комментарий: ").Bold();
                    t.Span(item.Comment);
                });
            }
            
            column.Item().PaddingBottom(15);
        }
    }
}


private void RegisterDefaultFonts()
{
    try
    {
        _logger.LogInformation("Font registration is not required for system fonts in QuestPDF");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error in font registration");
    }
}

private byte[]? GeneratePdfReport(Survey? survey, List<HistoryAnswer>? answers, out string fileName)
{
    fileName = $"Анкета_{DateTime.Now:yyyy-MM-dd}.pdf";
    
    try
    {
        if (survey == null || answers == null)
            return null;

        QuestPDF.Settings.License = LicenseType.Community;
        
        var document = QuestPDFDocument.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(12).FontFamily("Arial"));

                page.Header()
                    .AlignCenter()
                    .Text($"Анкета: {survey.name_survey}")
                    .FontSize(16);
                
                page.Content()
                    .PaddingVertical(1, Unit.Centimetre)
                    .Column(column =>
                    {
                        if (!string.IsNullOrEmpty(survey.description))
                        {
                            column.Item().Text(survey.description);
                            column.Item().PaddingBottom(10);
                        }

                        column.Item().Text("Ответы:").Bold().FontSize(14);
                        
                        foreach (var answer in answers)
                        {
                            var answerData = JsonConvert.DeserializeObject<List<AnswerItem>>(answer.answers) 
                                ?? new List<AnswerItem>();
                            
                            foreach (var item in answerData)
                            {
                                column.Item().Text(item.QuestionText ?? "Вопрос не указан").Bold();
                                column.Item().Text($"Оценка: {item.Rating?.ToString() ?? "0"}/5");
                                
                                if (!string.IsNullOrEmpty(item.Comment))
                                {
                                    column.Item().Text($"Комментарий: {item.Comment}");
                                }
                                
                                column.Item().PaddingBottom(10);
                            }
                        }
                    });
            });
        });

        using var stream = new MemoryStream();
        document.GeneratePdf(stream);
        return stream.ToArray();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Ошибка генерации PDF");
        return null;
    }
}


private List<AnswerItem> ParseAnswerData(string jsonAnswers)
{
    if (string.IsNullOrWhiteSpace(jsonAnswers))
        return new List<AnswerItem>();

    try
    {
        var result = JsonConvert.DeserializeObject<List<AnswerItem>>(jsonAnswers) 
                  ?? new List<AnswerItem>();

        if (!result.Any())
        {
            var wrapper = JsonConvert.DeserializeObject<AnswerWrapper>(jsonAnswers);
            if (wrapper?.Answers != null)
            {
                result = wrapper.Answers;
            }
        }

        return result;
    }
    catch
    {
        return new List<AnswerItem>();
    }
}

    private List<dynamic> DeserializeAnswersWithNewtonsoft(string jsonString)
    {
        try
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<List<dynamic>>(jsonString) 
                ?? new List<dynamic>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deserializing with Newtonsoft.Json");
            return new List<dynamic>();
        }
    }
    
private string GetPropertyValue(dynamic obj, string propertyName)
{
    try
    {
        if (obj == null) return null;
        
        if (obj is IDictionary<string, object> dict)
        {
            return dict.ContainsKey(propertyName) ? dict[propertyName]?.ToString() : null;
        }
        
        return obj.GetType().GetProperty(propertyName)?.GetValue(obj, null)?.ToString();
    }
    catch
    {
        return null;
    }
}



public IActionResult get_list_answers()
{
    List<HistoryAnswer> answers = new List<HistoryAnswer>();

    try
    {
        using (var connection = _db.CreateConnection())
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT 
    ha.id_omsu, 
    (SELECT name_omsu FROM public.omsu WHERE omsu.id_omsu = ha.id_omsu) AS name_omsu, 
    ha.csp, 
    ha.id_answer, 
    ha.id_survey, 
    COALESCE(
        (SELECT name_survey FROM public.surveys WHERE surveys.id_survey = ha.id_survey), 
        (SELECT name_survey FROM public.history_surveys WHERE history_surveys.id_survey = ha.id_survey)
    ) AS name_survey, 
    ha.completion_date, 
    ha.answers 
FROM public.history_answer ha;";

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var answer = new HistoryAnswer
                        {
                            id_omsu = reader.GetInt32(0),
                            name_omsu = reader.GetString(1),
                            csp = reader.IsDBNull(2) ? "Не подписано" : reader.GetString(2),
                            id_answer = reader.GetInt32(3),
                            id_survey = reader.GetInt32(4),
                            name_survey = reader.IsDBNull(5) ? "Нет данных" : reader.GetString(5),
                            completion_date = reader.GetDateTime(6),
                            answers = reader.GetString(7)
                        };
                        answers.Add(answer);
                    }
                }
            }
        }

        return View(answers);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Ошибка при получении ответов из базы данных: {ex.Message}");
        return StatusCode(500, "Произошла ошибка на сервере");
    }
}


private byte[] GeneratePdfContent(Survey survey, List<HistoryAnswer> answers)
{
    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    QuestPDF.Settings.License = LicenseType.Community;

    var document = QuestPDF.Fluent.Document.Create(container =>
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(2, Unit.Centimetre);
            page.DefaultTextStyle(style => style
                .FontSize(12)
                .FontFamily("Arial")
                .Fallback(x => x.FontFamily("Times New Roman")));

            page.Header()
                .AlignCenter()
                .PaddingBottom(15)
                .Text($"Анкета: {survey.name_survey}")
                .Bold()
                .FontSize(18);

            page.Content()
                .Column(column =>
                {
                    if (!string.IsNullOrEmpty(survey.description))
                    {
                        column.Item()
                            .Border(1)
                            .BorderColor(Colors.Grey.Medium)
                            .Padding(10)
                            .Text(survey.description);
                    }

                    column.Item()
                        .PaddingTop(15)
                        .Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(3);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Text("Вопрос").Bold();
                                header.Cell().AlignCenter().Text("Оценка").Bold();
                                header.Cell().Text("Комментарий").Bold();
                            });

                            foreach (var answer in answers)
                            {
                                var answerData = ParseAnswerData(answer.answers);
                                
                                foreach (var item in answerData)
                                {
                                    
table.Cell()
    .BorderBottom(1)
    .Padding(5)
    .Text(item.Question ?? "Вопрос не указан");

                                    table.Cell()
                                        .BorderBottom(1)
                                        .AlignCenter()
                                        .Padding(5)
                                        .Text(item.Rating ?? "0");

                                    table.Cell()
                                        .BorderBottom(1)
                                        .Padding(5)
                                        .Text(item.Comment ?? "Нет комментария");
                                }
                            }
                        });
                });
        });
    });

    using var stream = new MemoryStream();
    document.GeneratePdf(stream);
    return stream.ToArray();
}

[HttpGet("download_signed_archive/{idSurvey}/{idOmsu}")]
public async Task<IActionResult> DownloadSignedArchive(int idSurvey, int idOmsu)
{
    try
    {
        // 1. Получение данных анкеты
        var survey = GetSurveyInfo(idSurvey);
        if (survey == null)
        {
            _logger.LogWarning($"Анкета с ID {idSurvey} не найдена");
            return NotFound($"Анкета с ID {idSurvey} не найдена");
        }

        // 2. Получение ответов
        var answers = GetAnswersFromDatabase(idSurvey, idOmsu);
        if (answers == null || !answers.Any())
        {
            _logger.LogWarning($"Ответы для анкеты {idSurvey} и ОМСУ {idOmsu} не найдены");
            return NotFound("Ответы не найдены");
        }

        // 3. Генерация PDF
        var pdfBytes = GeneratePdfContent(survey, answers);
        if (pdfBytes == null || pdfBytes.Length == 0)
        {
            _logger.LogError("Не удалось сгенерировать PDF");
            return StatusCode(500, "Ошибка генерации PDF");
        }

        // 4. Подготовка данных для архива
        var cleanName = CleanFileName(survey.name_survey ?? "Анкета");
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var pdfFileName = $"{cleanName}_ответы_{timestamp}.pdf";
        var zipFileName = $"{cleanName}_с_подписью_{timestamp}.zip";
        var signature = answers.FirstOrDefault()?.csp;

        // 5. Создание ZIP-архива в памяти
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            // Добавляем PDF в архив
            var pdfEntry = archive.CreateEntry(pdfFileName, CompressionLevel.Optimal);
            using (var entryStream = pdfEntry.Open())
            {
                await entryStream.WriteAsync(pdfBytes);
            }

            // Добавляем подпись, если она есть
            if (!string.IsNullOrEmpty(signature))
            {
                var sigEntry = archive.CreateEntry($"Подпись_{pdfFileName}.sig");
                using (var entryStream = sigEntry.Open())
                using (var writer = new StreamWriter(entryStream, Encoding.UTF8))
                {
                    await writer.WriteAsync(signature);
                }
            }
        }

        // 6. Возвращаем архив для скачивания
        memoryStream.Position = 0;
        return File(memoryStream.ToArray(), "application/zip", zipFileName);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, $"Ошибка при создании архива для анкеты {idSurvey}");
        return StatusCode(500, new 
        {
            Success = false,
            Error = "Ошибка при создании архива",
            Details = ex.Message
        });
    }
}

private string GetSafeString(dynamic obj, string propertyName)
{
    try
    {
        if (obj == null) return null;
        
        var value = obj[propertyName] as JValue ?? obj.GetType().GetProperty(propertyName)?.GetValue(obj, null);
        return value?.ToString();
    }
    catch
    {
        return null;
    }
}

[HttpGet("get_signing_data/{id}/{idOmsu}")]
[Authorize]
public IActionResult GetSigningData(int id, int idOmsu)
{
    var omsuAccessResult = EnsureOmsuAccess(idOmsu);
    if (omsuAccessResult != null)
        return omsuAccessResult;
    try
    {
        var data = $"Данные для подписи анкеты {id} организации {idOmsu}";
        return Content(data);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Ошибка при получении данных для подписи: {ex.Message}");
        return StatusCode(500, "Ошибка при получении данных для подписи");
    }
}

[HttpPost("csp/{id}/{idOmsu}")]
[Authorize]
public IActionResult CSP_answer([FromRoute] int id, [FromRoute] int idOmsu, [FromBody] CSPRequest request)
{
    var omsuAccessResult = EnsureOmsuAccess(idOmsu);
    if (omsuAccessResult != null)
        return omsuAccessResult;
    try
    {

        if (string.IsNullOrEmpty(request.Signature))
        {
            return BadRequest("Signature не может быть пустым.");
        }

        using (var connection = _db.CreateConnection())
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    UPDATE public.history_answer
                    SET csp = @signature
                    WHERE id_omsu = @idOmsu AND id_survey = @id";

                command.Parameters.Add(new NpgsqlParameter("@idOmsu", idOmsu));
                command.Parameters.Add(new NpgsqlParameter("@id", id));
                command.Parameters.Add(new NpgsqlParameter("@signature", request.Signature));

                int rowsAffected = command.ExecuteNonQuery();
                
                if (rowsAffected == 0)
                {
                    return NotFound("Запись для обновления не найдена.");
                }
            }
        }

        return Ok("Запись успешно обновлена.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Ошибка при обновлении ответа: {ex.Message}");
        return StatusCode(500, $"Ошибка при обновлении ответа: {ex.Message}");
    }
}

[HttpGet("get_list_csp/{id:int}")]
    public IActionResult get_list_csp(int id)
    {
        try
        {
            if (id <= 0)
            {
                return BadRequest("Неверный ID анкеты");
            }

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                string surveyName = null;
                using (var command = new NpgsqlCommand(
                    "SELECT name_survey FROM surveys WHERE id_survey = @id", 
                    connection))
                {
                    command.Parameters.AddWithValue("@id", id);
                    var result = command.ExecuteScalar();
                    if (result != null)
                    {
                        surveyName = result.ToString();
                    }
                }

                var signatures = new List<HistoryAnswer>();
                using (var command = new NpgsqlCommand(
                    @"SELECT 
                        o.name_omsu, 
                        CASE WHEN ha.completion_date IS NOT NULL THEN 'Пройдена' ELSE 'Не пройдена' END, 
                        CASE WHEN ha.csp IS NOT NULL THEN 'Подписана' ELSE 'Не подписана' END
                      FROM omsu o
                      LEFT JOIN history_answer ha ON o.id_omsu = ha.id_omsu AND ha.id_survey = @id
                      WHERE o.list_surveys LIKE '%' || @id || '%'
                      ORDER BY o.name_omsu", 
                    connection))
                {
                    command.Parameters.AddWithValue("@id", id);
                    
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            signatures.Add(new HistoryAnswer
                            {
                                name_omsu = reader.GetString(0),
                                answers = reader.GetString(1),
                                csp = reader.GetString(2)
                            });
                        }
                    }
                }

                ViewBag.SurveyName = surveyName ?? "Неизвестная анкета";
                ViewBag.SurveyId = id;

                return View(signatures);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}\n{ex.StackTrace}");
            return StatusCode(500, $"Внутренняя ошибка сервера: {ex.Message}");
        }
    }




[HttpGet]
public IActionResult get_data_statistic()
{
    try
    {
        // 1. Линейная диаграмма (количество ответов по месяцам)
        var lineChartData = GetLineChartData();

        // 2. Круговая диаграмма (количество ответов по типам анкет)
        var pieChartData = GetPieChartData();

        // 3. Гистограмма (количество ответов по годам)
        var barChartData = GetBarChartData();

        // 4. Диаграмма радиальных столбцов (средние оценки по характеристикам)
        var radarChartData = GetRadarChartData();

        // 5. Диаграмма средних баллов по ОМСУ по годам (3 последних года)
        var avgScoreByOmsuRadar = GetAvgScoreByOmsuRadar();

        var chartData = new
        {
            lineChart = lineChartData,
            pieChart = pieChartData,
            barChart = barChartData,
            radarChart = radarChartData,
            avgScoreByOmsuRadar = avgScoreByOmsuRadar
        };
        return Json(chartData);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Ошибка в get_data_statistic: {ex}");
        return StatusCode(500, "Внутренняя ошибка сервера");
    }
}



private object GetAvgScoreByOmsuRadar()
{
    // Получаем последние 3 года
    List<int> years = new List<int>();
    using (var conn = _db.CreateConnection())
    {
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT DISTINCT EXTRACT(YEAR FROM s.date_create)::int AS year
                FROM public.surveys s
                ORDER BY year DESC
                LIMIT 3
            ";
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                    years.Add(reader.GetInt32(0));
            }
        }
    }
    years = years.OrderBy(y => y).ToList();
    var yearsStr = string.Join(",", years);

    // Получаем список ОМСУ
    var omsuList = new List<(int id, string name)>();
    using (var conn = _db.CreateConnection())
    {
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT DISTINCT o.id_omsu, o.name_omsu
                FROM public.history_answer ha
                JOIN public.omsu o ON ha.id_omsu = o.id_omsu
            ";
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                    omsuList.Add((reader.GetInt32(0), reader.GetString(1)));
            }
        }
    }

    // Получаем средние баллы по ОМСУ и годам
    var avgDict = new Dictionary<(int omsuId, int year), double>();
    using (var conn = _db.CreateConnection())
    {
        using (var cmd = conn.CreateCommand())
        {
cmd.CommandText = $@"
    SELECT
        o.id_omsu,
        EXTRACT(YEAR FROM s.date_create)::int AS year,
        AVG((elem->>'rating')::int) AS avg_rating
    FROM public.history_answer ha
    JOIN public.surveys s ON ha.id_survey = s.id_survey
    JOIN public.omsu o ON ha.id_omsu = o.id_omsu
    JOIN LATERAL jsonb_array_elements(ha.answers::jsonb) AS elem ON TRUE
    WHERE EXTRACT(YEAR FROM s.date_create)::int IN ({yearsStr})
    GROUP BY o.id_omsu, year
";
            
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    int omsuId = reader.GetInt32(0);
                    int year = reader.GetInt32(1);
                    double avg = reader.IsDBNull(2) ? 0 : reader.GetDouble(2);
                    avgDict[(omsuId, year)] = avg;
                }
            }
        }
    }

    // Собираем структуру для radar chart
    var datasets = new List<object>();
    var colors = new[] { "rgba(54,162,235,0.2)", "rgba(255,99,132,0.2)", "rgba(255,206,86,0.2)" };
    var borderColors = new[] { "rgb(54,162,235)", "rgb(255,99,132)", "rgb(255,206,86)" };
    for (int i = 0; i < years.Count; i++)
    {
        var year = years[i];
        var data = omsuList.Select(omsu => avgDict.TryGetValue((omsu.id, year), out var avg) ? avg : 0.0).ToList();
        datasets.Add(new
        {
            label = year.ToString(),
            data = data,
            backgroundColor = colors[i % colors.Length],
            borderColor = borderColors[i % borderColors.Length]
        });
    }

return new
{
    labels = omsuList.Select(o => o.name).ToList(), // названия ОМСУ по кругу
    datasets = datasets // массив, где каждый dataset — это год, data — средние баллы по ОМСУ
};

}




private object GetLineChartData()
{
    var labels = new List<string>();
    var data = new List<int>();

    using (var connection = new NpgsqlConnection(_connectionString))
    {
                connection.Open();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = @"
                SET lc_time = 'ru_RU.UTF-8';

SELECT
    TO_CHAR(completion_date, 'Month YYYY') as month_year,
    COUNT(*) as count
FROM public.history_answer
GROUP BY month_year
ORDER BY MIN(completion_date);";

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    labels.Add(reader.GetString(0));
                    data.Add(reader.GetInt32(1));
                }
            }
        }
    }

    return new
    {
        labels = labels,
        label = "Количество ответов",
        data = data
    };
}

     private object GetPieChartData()
    {
          var labels = new List<string>();
          var data = new List<int>();

         using (var connection = new NpgsqlConnection(_connectionString))
        {
                    connection.Open();
           using (var command = connection.CreateCommand())
           {
              command.CommandText = @"
                     SELECT
                        CASE
                             WHEN s.name_survey IS NOT NULL THEN s.name_survey
                             ELSE hs.name_survey
                        END as survey_type,
                         COUNT(*) as count
                       FROM public.history_answer ha
                        LEFT JOIN public.surveys s ON ha.id_survey = s.id_survey
                        LEFT JOIN public.history_surveys hs ON ha.id_survey = hs.id_survey
                     GROUP BY survey_type";
            
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    labels.Add(reader.IsDBNull(0) ? "Неизвестно" : reader.GetString(0));
                    data.Add(reader.GetInt32(1));
                }
            }
            }
        }
        return new
        {
             labels = labels,
             data = data
        };
    }

   private object GetBarChartData()
   {
        var labels = new List<string>();
        var data = new List<int>();
      using (var connection = new NpgsqlConnection(_connectionString))
        {
                    connection.Open();
           using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                     SELECT
                        EXTRACT(YEAR FROM completion_date) as year,
                        COUNT(*) AS count
                    FROM public.history_answer
                   GROUP BY year
                   ORDER BY year";
            
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int year = (int)reader.GetDouble(0);
                         labels.Add(year.ToString());
                         data.Add(reader.GetInt32(1));
                    }
                }
            }
        }
        return new
        {
            labels = labels,
            label = "Количество ответов",
            data = data
        };
    }

private object GetRadarChartData()
{
    var chartData = new Dictionary<string, Dictionary<string, int>>();
    var labels = new HashSet<string>();

    using (var connection = new NpgsqlConnection(_connectionString))
    {
        connection.Open();
        using (var command = connection.CreateCommand())
        {
             command.CommandText = @"
                SELECT
                    CASE
                        WHEN s.name_survey IS NOT NULL THEN s.name_survey
                        ELSE hs.name_survey
                    END as survey_type,
                    ha.id_answer,
                    AVG(CAST((value->>'rating') AS INTEGER)) AS avg_rating,
                    CASE
                        WHEN s.description IS NOT NULL THEN s.description
                        WHEN hs.description IS NOT NULL THEN hs.description
                        ELSE 'Вопрос ' || ha.id_answer::TEXT
                    END as question_description
                FROM public.history_answer ha
                LEFT JOIN public.surveys s ON ha.id_survey = s.id_survey
                LEFT JOIN public.history_surveys hs ON ha.id_survey = hs.id_survey
                CROSS JOIN jsonb_array_elements(ha.answers::jsonb) as value
                WHERE jsonb_typeof(ha.answers::jsonb) = 'array'
                GROUP BY survey_type, ha.id_answer, s.description, hs.description
                ORDER BY survey_type, ha.id_answer
            ";

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                   string surveyType = reader.IsDBNull(0) ? "Неизвестно" : reader.GetString(0);
                   
                   var questionIdValue = reader.GetValue(1);
                   var avgRatingValue = reader.GetValue(2);
                   var questionDescriptionValue = reader.GetValue(3);

                 

                   int questionId = questionIdValue is int ? (int)questionIdValue : 0;

                   double avgRating = avgRatingValue is double ? (double)avgRatingValue : 0;

                     string questionDescription = questionDescriptionValue is string ? (string)questionDescriptionValue : $"Вопрос {questionId}";


                   if (!chartData.ContainsKey(surveyType))
                    {
                        chartData[surveyType] = new Dictionary<string, int>();
                     }

                    chartData[surveyType][questionDescription] = Convert.ToInt32(avgRating);
                    labels.Add(questionDescription);
                }
            }
        }
    }

    var datasets = new List<object>();
    foreach (var surveyTypeData in chartData)
    {
        var dataset = new
        {
            label = surveyTypeData.Key,
            data = labels
                .Select(label => surveyTypeData.Value.ContainsKey(label) ? surveyTypeData.Value[label] : 0)
                .ToList()
        };
        datasets.Add(dataset);
    }

    return new
    {
        labels = labels.ToList(),
        datasets = datasets
    };
}

private List<dynamic> DeserializeAnswers(string jsonString)
{
    try
    {
        if (string.IsNullOrWhiteSpace(jsonString))
        {
            _logger.LogWarning("Пустая строка JSON для десериализации");
            return new List<dynamic>();
        }

        var result = JsonConvert.DeserializeObject<List<dynamic>>(jsonString);
        return result ?? new List<dynamic>();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, $"Ошибка десериализации JSON: {jsonString}");
        return new List<dynamic>();
    }
}


[HttpPost("api/insert_answer")]
public IActionResult insert_answer([FromBody] HistoryAnswer historyAnswerData)
{
    try
    {
        if (historyAnswerData == null)
        {
            throw new ArgumentNullException(nameof(historyAnswerData));
        }

        // Сохранение в БД
        using (var connection = _db.CreateConnection())
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    INSERT INTO public.history_answer (
    id_omsu,
    id_survey,
    completion_date,
    answers,
    create_date_survey
) 
VALUES (
    @idOmsu,
    @idSurvey,
    @completionDate,
    @answers,
    (SELECT date_create FROM public.surveys WHERE id_survey = @idSurvey)
)
RETURNING id_answer";

                command.Parameters.Add(new NpgsqlParameter("@idOmsu", historyAnswerData.id_omsu));
                command.Parameters.Add(new NpgsqlParameter("@idSurvey", historyAnswerData.id_survey));
                command.Parameters.Add(new NpgsqlParameter("@completionDate", DateTime.Now));
                command.Parameters.Add(new NpgsqlParameter("@answers", NpgsqlTypes.NpgsqlDbType.Jsonb) 
                { 
                    Value = string.IsNullOrWhiteSpace(historyAnswerData.answers) ? "[]" : historyAnswerData.answers 
                });

                var answerId = command.ExecuteScalar();
            }

            // Удаление записи в access_extensions, если она есть
            DeleteAccessExtension(historyAnswerData.id_omsu, historyAnswerData.id_survey);
        }

        // Получение информации об анкете
        var survey = GetSurveyInfo(historyAnswerData.id_survey) ?? new Survey 
        { 
            id_survey = historyAnswerData.id_survey,
            name_survey = "Неизвестная анкета",
            description = "Описание отсутствует"
        };

        // Парсинг ответов
        var answerItems = ParseAnswerData(historyAnswerData.answers ?? "[]")
            .Select(item => new 
            {
                QuestionText = item.QuestionText ?? item.Text ?? "Вопрос не указан",
                Rating = item.Rating ?? "0",
                Comment = item.Comment ?? "Нет комментария"
            })
            .ToList();

        // Передача данных в представление
        ViewBag.Survey = survey;
        ViewBag.Answers = answerItems;
        ViewBag.IdOmsu = historyAnswerData.id_omsu;

        return View("check_answers");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Ошибка при сохранении ответа");
        return View("Error", new { Message = $"Ошибка при сохранении ответа: {ex.Message}" });
    }
}

// Добавляем недостающий метод
private void DeleteAccessExtension(int omsuId, int surveyId)
{
    try
    {
        using (var connection = _db.CreateConnection())
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    DELETE FROM public.access_extensions
                    WHERE id_omsu = @omsuId AND id_survey = @surveyId";

                command.Parameters.Add(new NpgsqlParameter("@omsuId", omsuId));
                command.Parameters.Add(new NpgsqlParameter("@surveyId", surveyId));

                command.ExecuteNonQuery();
            }
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, $"Ошибка при удалении записи из access_extensions для OMSU: {omsuId}, Survey: {surveyId}");
    }
}



    


[HttpGet("answers/{idSurvey}/{idOmsu}/{type?}")]
public IActionResult answers(int idSurvey, int idOmsu = 0, string type = "regular")
{
    try
    {
        bool isArchive = type.ToLower() == "archive";
        var logPrefix = $"{(isArchive ? "[ARCHIVE]" : "[REGULAR]")} Анкета {idSurvey}, ОМСУ {idOmsu}";

        Survey surveyInfo = null;
        using (var connection = _db.CreateConnection())
        using (var command = connection.CreateCommand())
        {
            // Объединенный запрос с приоритетом к surveys
            command.CommandText = @"
                SELECT id_survey, name_survey, description 
                FROM surveys 
                WHERE id_survey = @idSurvey
                
                UNION ALL
                
                SELECT id_survey, name_survey, description 
                FROM history_surveys 
                WHERE id_survey = @idSurvey
                AND NOT EXISTS (
                    SELECT 1 FROM surveys 
                    WHERE id_survey = @idSurvey
                )
                
                LIMIT 1";
            
            command.Parameters.Add(new NpgsqlParameter("@idSurvey", idSurvey));

            using (var reader = command.ExecuteReader())
            {
                if (reader.Read())
                {
                    surveyInfo = new Survey
                    {
                        id_survey = reader.GetInt32(0),
                        name_survey = reader.IsDBNull(1) ? null : reader.GetString(1),
                        description = reader.IsDBNull(2) ? null : reader.GetString(2)
                    };
                }
            }
        }

        if (surveyInfo == null)
        {
            _logger.LogWarning($"{logPrefix} - анкета не найдена ни в surveys, ни в history_surveys");
            return NotFound(new { success = false, error = "Анкета не найдена" });
        }

        List<HistoryAnswer> answers = new List<HistoryAnswer>();
        using (var connection = _db.CreateConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = isArchive
                ? @"SELECT ha.id_answer, ha.id_omsu, ha.id_survey, ha.csp, ha.completion_date, ha.answers, o.name_omsu 
                   FROM history_answer ha
                   LEFT JOIN omsu o ON ha.id_omsu = o.id_omsu
                   WHERE ha.id_survey = @idSurvey
                   ORDER BY ha.completion_date DESC"
                : @"SELECT ha.id_answer, ha.id_omsu, ha.id_survey, ha.csp, ha.completion_date, ha.answers, o.name_omsu 
                   FROM history_answer ha
                   LEFT JOIN omsu o ON ha.id_omsu = o.id_omsu
                   WHERE ha.id_survey = @idSurvey AND ha.id_omsu = @idOmsu
                   ORDER BY ha.completion_date DESC";
            
            command.Parameters.Add(new NpgsqlParameter("@idSurvey", idSurvey));
            if (!isArchive)
            {
                command.Parameters.Add(new NpgsqlParameter("@idOmsu", idOmsu));
            }

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    answers.Add(new HistoryAnswer
                    {
                        id_answer = reader.GetInt32(0),
                        id_omsu = reader.GetInt32(1),
                        id_survey = reader.GetInt32(2),
                        csp = reader.IsDBNull(3) ? null : reader.GetString(3),
                        completion_date = reader.IsDBNull(4) ? null : (DateTime?)reader.GetDateTime(4),
                        answers = reader.IsDBNull(5) ? null : reader.GetString(5),
                        name_omsu = reader.IsDBNull(6) ? null : reader.GetString(6)
                    });
                }
            }
        }

        if (!answers.Any())
        {
            _logger.LogWarning($"{logPrefix} - ответы не найдены");
            return NotFound(new { success = false, error = "Ответы не найдены" });
        }

        var processedAnswers = new List<object>();
        foreach (var answer in answers)
        {
            try 
            {
                var answerItems = ParseAnswerData(answer.answers) ?? new List<AnswerItem>();
                string formattedDate = answer.completion_date.HasValue 
                    ? answer.completion_date.Value.ToString("dd.MM.yyyy HH:mm") 
                    : "Дата не указана";
                
                processedAnswers.Add(new 
                {
                    id = answer.id_answer,
                    omsu_id = answer.id_omsu,
                    omsu_name = answer.name_omsu ?? "Неизвестно",
                    date = formattedDate,
                    answers = answerItems.Select(item => new 
                    {
                        question_text = item.Question ?? "Вопрос не указан",
                        rating = item.Rating ?? "0",
                        comment = item.Comment ?? "Нет комментария"
                    }),
                    is_signed = !string.IsNullOrEmpty(answer.csp),
                    signature = answer.csp
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{logPrefix} - ошибка обработки ответа ID: {answer.id_answer}");
            }
        }

        _logger.LogInformation($"{logPrefix} - успешно возвращено {processedAnswers.Count} ответов");
        
        return Ok(new 
        {
            success = true,
            survey = new { 
                id = idSurvey,
                name = surveyInfo.name_survey,
                description = surveyInfo.description,
                is_archive = isArchive
            },
            answers = processedAnswers
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Ошибка при обработке запроса ответов");
        return StatusCode(500, new { 
            success = false, 
            error = "Внутренняя ошибка сервера",
            details = ex.Message 
        });
    }
}

private async Task<List<HistoryAnswer>> GetAnswersFromDatabaseAsync(int idSurvey, int idOmsu)
{
    const string query = @"
        SELECT id_answer, id_omsu, id_survey, csp, completion_date, answers 
        FROM history_answer 
        WHERE id_survey = @idSurvey AND id_omsu = @idOmsu";

    var answers = new List<HistoryAnswer>();

    using (var connection = new NpgsqlConnection(_connectionString))
    {
        await connection.OpenAsync();
        
        using (var command = new NpgsqlCommand(query, connection))
        {
            command.Parameters.AddWithValue("@idSurvey", idSurvey);
            command.Parameters.AddWithValue("@idOmsu", idOmsu);

            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    answers.Add(new HistoryAnswer
                    {
                        id_answer = reader.GetInt32(0),
                        id_omsu = reader.GetInt32(1),
                        id_survey = reader.GetInt32(2),
                        csp = reader.IsDBNull(3) ? null : reader.GetString(3),
                        completion_date = reader.GetDateTime(4),
                        answers = reader.GetString(5)
                    });
                }
            }
        }
    }

    return answers;
}

private List<HistoryAnswer> GetAnswersFromDatabase(int idSurvey, int idOmsu)
    {
        try
        {
            var answers = new List<HistoryAnswer>();

            using (var connection = _db.CreateConnection())
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT id_answer, id_omsu, id_survey, csp, completion_date, answers 
                        FROM public.history_answer 
                        WHERE id_survey = @idSurvey AND id_omsu = @idOmsu";

                    command.Parameters.Add(new NpgsqlParameter("@idSurvey", idSurvey));
                    command.Parameters.Add(new NpgsqlParameter("@idOmsu", idOmsu));

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            answers.Add(new HistoryAnswer
                            {
                                id_answer = reader.GetInt32(0),
                                id_omsu = reader.GetInt32(1),
                                id_survey = reader.GetInt32(2),
                                csp = reader.IsDBNull(3) ? null : reader.GetString(3),
                                completion_date = reader.GetDateTime(4),
                                answers = reader.GetString(5)
                            });
                        }
                    }
                }
            }

            _logger.LogInformation($"Retrieved {answers.Count} answers from DB");
            return answers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving answers from DB");
            throw;
        }
    }


 private Survey survey_parser(int idSurvey)
    {
        Survey survey = null;
        using (var connection = _db.CreateConnection())
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT id_survey, name_survey, description
                    FROM public.surveys 
                    WHERE id_survey = @idSurvey";

                command.Parameters.Add(new NpgsqlParameter("@idSurvey", idSurvey));

                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        survey = new Survey
                        {
                            id_survey = reader.GetInt32(0),
                            name_survey = reader.IsDBNull(1) ? null : reader.GetString(1),
                            description = reader.IsDBNull(2) ? null : reader.GetString(2)
                        };
                    }
                }
            }

            if (survey == null || string.IsNullOrEmpty(survey.name_survey))
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT id_survey, name_survey, NULL AS description
                        FROM public.history_surveys
                        WHERE id_survey = @idSurvey";

                    command.Parameters.Add(new NpgsqlParameter("@idSurvey", idSurvey));

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            survey = new Survey
                            {
                                id_survey = reader.GetInt32(0),
                                name_survey = reader.IsDBNull(1) ? null : reader.GetString(1),
                                description = null
                            };
                        }
                    }
                }
            }
        }
        return survey;
    }


[HttpPost("update_answer/{idSurvey}/{idOmsu}")]
public IActionResult update_answer([FromRoute] int idSurvey, [FromRoute] int idOmsu)
{

            try
            {

                var historyAnswer = GetHistoryAnswer(idSurvey, idOmsu);

                if (historyAnswer == null)
                {
                    return NotFound("Ответы не найдены");
                }

                if (string.IsNullOrEmpty(historyAnswer.answers))
                {
                    return BadRequest("Ответы отсутствуют или некорректны");
                }

                var answers = JsonConvert.DeserializeObject<List<dynamic>>(historyAnswer.answers);

                var viewModel = new
                {
                    SurveyId = idSurvey,
                    OmsuId = idOmsu,
                    Answers = answers
                };

                return View("update_answer", viewModel);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
                return StatusCode(500, "Произошла ошибка на сервере");
            }
        }

        private HistoryAnswer GetHistoryAnswer(int idSurvey, int idOmsu)
        {
            using (var connection = _db.CreateConnection())
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT id_answer, id_omsu, id_survey, completion_date, answers
                        FROM public.history_answer
                        WHERE id_survey = @idSurvey AND id_omsu = @idOmsu";

                    command.Parameters.Add(new NpgsqlParameter("@idSurvey", NpgsqlTypes.NpgsqlDbType.Integer) { Value = idSurvey });
                    command.Parameters.Add(new NpgsqlParameter("@idOmsu", NpgsqlTypes.NpgsqlDbType.Integer) { Value = idOmsu });

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new HistoryAnswer
                            {
                                id_answer = reader.GetInt32(0),
                                id_omsu = reader.GetInt32(1),
                                id_survey = reader.GetInt32(2),
                                completion_date = reader.GetDateTime(3),
                                answers = reader.GetString(4)
                            };
                        }
                    }
                }
            }

            return null;
        }


[HttpPost("update_answer_bd")]
public IActionResult UpdateAnswerBd([FromBody] HistoryAnswer historyAnswerData)
{
    try
    {
        using (var connection = _db.CreateConnection())
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    UPDATE public.history_answer
                    SET completion_date = @completionDate,
                        answers = @answers
                    WHERE id_omsu = @idOmsu AND id_survey = @idSurvey";

                command.Parameters.Add(new NpgsqlParameter("@idOmsu", NpgsqlTypes.NpgsqlDbType.Integer) { Value = historyAnswerData.id_omsu });
                command.Parameters.Add(new NpgsqlParameter("@idSurvey", NpgsqlTypes.NpgsqlDbType.Integer) { Value = historyAnswerData.id_survey });
                command.Parameters.Add(new NpgsqlParameter("@completionDate", NpgsqlTypes.NpgsqlDbType.Timestamp) { Value = DateTime.Now });
                command.Parameters.Add(new NpgsqlParameter("@answers", NpgsqlTypes.NpgsqlDbType.Jsonb) { Value = historyAnswerData.answers });

                int rowsAffected = command.ExecuteNonQuery();

                if (rowsAffected == 0)
                {
                    return NotFound("Запись для обновления не найдена.");
                }
            }
        }

        var surveyInfo = GetSurveyInfo(historyAnswerData.id_survey);
        int idOmsu = historyAnswerData.id_omsu;

        var answers = JsonConvert.DeserializeObject<List<dynamic>>(historyAnswerData.answers);

        var viewModel = new
        {
            Survey = surveyInfo,
            Answers = answers,
            IdOmsu = idOmsu
        };

        return View("check_answers", viewModel);
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message);
        return View("Error", new ErrorViewModel { Message = $"Ошибка при обновлении ответа: {ex.Message}" });
    }
}
    

private Survey GetSurveyInfo(int idSurvey)
{
    Survey survey = null;
    
    using (var connection = _db.CreateConnection())
    {
        // Сначала ищем в основной таблице surveys
        using (var command = connection.CreateCommand())
        {
            command.CommandText = @"
                SELECT id_survey, name_survey, description
                FROM public.surveys 
                WHERE id_survey = @idSurvey";

            command.Parameters.Add(new NpgsqlParameter("@idSurvey", idSurvey));

            using (var reader = command.ExecuteReader())
            {
                if (reader.Read())
                {
                    survey = new Survey
                    {
                        id_survey = reader.GetInt32(0),
                        name_survey = reader.IsDBNull(1) ? null : reader.GetString(1),
                        description = reader.IsDBNull(2) ? null : reader.GetString(2)
                    };
                }
            }
        }

        // Если не нашли в surveys, ищем в history_surveys
        if (survey == null)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT id_survey, name_survey, description
                    FROM public.history_surveys
                    WHERE id_survey = @idSurvey";

                command.Parameters.Add(new NpgsqlParameter("@idSurvey", idSurvey));

                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        survey = new Survey
                        {
                            id_survey = reader.GetInt32(0),
                            name_survey = reader.IsDBNull(1) ? null : reader.GetString(1),
                            description = reader.IsDBNull(2) ? null : reader.GetString(2)
                        };
                    }
                }
            }
        }
    }
    
    return survey;
}

[Authorize(Roles = "Админ")]
public IActionResult open_statistic()
{
    return View();
}

private readonly string _templatePath = @"wwwroot\docx\shablon_docx.docx";

[HttpGet]
[Authorize]
public IActionResult create_otchet_for_me(int idSurvey, int idOmsu, string type)
{
    var omsuAccessResult = EnsureOmsuAccess(idOmsu);
    if (omsuAccessResult != null)
        return omsuAccessResult;
    string surveyName = "";
    List<string> criteriaList = new List<string>();
    int criteriaCount = 0;
    List<string> omsus = new List<string>();
    List<List<int>> ratings = new List<List<int>>();
    List<List<string>> comments = new List<List<string>>();
        List<double> srednee = new List<double>();


    using (var connection = _db.CreateConnection())
    {
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT name_survey FROM public.surveys WHERE id_survey = @surveyId";
            command.Parameters.Add(new NpgsqlParameter("@surveyId", NpgsqlTypes.NpgsqlDbType.Integer) { Value = idSurvey });

            var result = command.ExecuteScalar();
            if (result != null && result != DBNull.Value)
            {
                surveyName = result.ToString();
            }
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT questions FROM public.surveys WHERE id_survey = @surveyId";
            command.Parameters.Add(new NpgsqlParameter("@surveyId", NpgsqlTypes.NpgsqlDbType.Integer) { Value = idSurvey });

            var result = command.ExecuteScalar();
            if (result != null && result != DBNull.Value)
            {
                var questionsJson = result.ToString();
                var questions = Newtonsoft.Json.Linq.JObject.Parse(questionsJson)["questions"];
                foreach (var question in questions)
                {
                    criteriaList.Add(question["text"].ToString());
                }
                criteriaCount = criteriaList.Count;
            }
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = @"
                SELECT 
                    o.name_omsu, 
                    ha.answers
                FROM 
                    public.omsu o 
                LEFT JOIN 
                    public.history_answer ha 
                ON 
                    o.id_omsu = ha.id_omsu 
                AND 
                    ha.id_survey = @surveyId
                WHERE o.id_omsu = @omsuId";

            command.Parameters.Add(new NpgsqlParameter("@surveyId", NpgsqlTypes.NpgsqlDbType.Integer) { Value = idSurvey });
            command.Parameters.Add(new NpgsqlParameter("@omsuId", NpgsqlTypes.NpgsqlDbType.Integer) { Value = idOmsu });

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var nameOmsu = reader.GetString(0);
                    var answersJson = reader.IsDBNull(1) ? null : reader.GetString(1);

                    omsus.Add(nameOmsu);

                    if (answersJson != null)
                    {
                        var answers = Newtonsoft.Json.Linq.JArray.Parse(answersJson);
                        var ratingsList = new List<int>();
                        var commentsList = new List<string>();

                        foreach (var answer in answers)
                        {
                            ratingsList.Add((int)answer["rating"]);
                            commentsList.Add(answer["comment"].ToString());
                        }

                        ratings.Add(ratingsList);
                        comments.Add(commentsList);
                    }
                    else
                    {
                        ratings.Add(new List<int>(new int[criteriaCount]));
                        comments.Add(new List<string>(new string[criteriaCount]));
                    }
                }
            }
        }
        for (int col = 0; col < criteriaCount; col++)
        {
            double sum = 0;
            int count = 0;
            for (int row = 0; row < ratings.Count; row++)
            {
                if (ratings[row].Count > col)
                {
                    sum += ratings[row][col];
                    count++;
                }
            }

            srednee.Add(sum / count);
        }
    }


    DateTime currentDate = DateTime.Now;

    string date = currentDate.ToString("MMMM yyyy", new System.Globalization.CultureInfo("ru-RU"));
    date = char.ToUpper(date[0]) + date.Substring(1);

    string fileName = $"Отчет по анкете {surveyName}.docx";
    string tempPath = Path.Combine(Path.GetTempPath(), fileName);

    var data = new Dictionary<string, string>
    {
        { "##NAME_SURVEY##", surveyName },
        { "##COUNT_CRITERIES##", criteriaCount.ToString() },
        { "##MASS_CRITERIES_LIST##", string.Join(Environment.NewLine, criteriaList.Select((c, i) => $" - {c}; ({i + 1})")) },
        { "##MASS_NAMES_CRITERIES_FOR_COMMENTS##", string.Join("     ", criteriaList) },
        { "##MASS_CRITERIES_FOR_TABLE##", string.Join("     ", criteriaList) },
        { "##DATE##", date },
        { "##MASS_OMSUS##", string.Join("\n", omsus) },
        { "##MASS_RATINGS##", string.Join("\n", ratings.Select(r => string.Join("     ", r))) },
        { "##MASS_COMMENTS##", string.Join("\n", comments.Select(c => string.Join("     ", c))) },
       { "##SREDNEE##", string.Join("     ", srednee)}
    };
    GenerateDocxFromTemplate(_templatePath, tempPath, data);
  
 if (type == "archiv")
    {
            var archiveFileName = $"Отчет по анкете {surveyName}.zip";
            var archivePath = Path.Combine(Path.GetTempPath(), archiveFileName);

            using (var zipArchive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                zipArchive.CreateEntryFromFile(tempPath, fileName);
            }
               var fileBytes = System.IO.File.ReadAllBytes(archivePath);
                    System.IO.File.Delete(archivePath);
                      System.IO.File.Delete(tempPath);
           
                var encodedFileName = System.Net.WebUtility.UrlEncode(archiveFileName);
              
                var contentType = "application/zip";

                Response.Headers.Add("Content-Disposition", $"attachment; filename={encodedFileName}");
                return File(fileBytes, contentType);
    }
    else
    {

        var fileBytes = System.IO.File.ReadAllBytes(tempPath);
        System.IO.File.Delete(tempPath);

          return File(
            fileBytes,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            fileName
          );
    }
}


private void GenerateDocxFromTemplate(string templatePath, string outputPath, Dictionary<string, string> data)
{
    System.IO.File.Copy(templatePath, outputPath, true);
    using (WordprocessingDocument doc = WordprocessingDocument.Open(outputPath, true))
    {
        var body = doc.MainDocumentPart.Document.Body;
        ReplacePlaceholdersInBody(body, data);
        ReplacePlaceholdersInTable(body, data);
    }
}

private void AppendToDocx(string templatePath, string outputPath, Dictionary<string, string> data)
{
    using (WordprocessingDocument doc = WordprocessingDocument.Open(outputPath, true))
    {
        var mainPart = doc.MainDocumentPart;

        var newPart = mainPart.AddAlternativeFormatImportPart(AlternativeFormatImportPartType.WordprocessingML);

        using (FileStream stream = new FileStream(templatePath, FileMode.Open, FileAccess.Read))
        {
            newPart.FeedData(stream);
        }

        using (WordprocessingDocument templateDoc = WordprocessingDocument.Open(templatePath, false))
        {
            var newBody = templateDoc.MainDocumentPart.Document.Body;

            ReplacePlaceholdersInBody(newBody, data);
            ReplacePlaceholdersInTable(newBody, data);

            foreach (var element in newBody.ChildElements)
            {
                mainPart.Document.Body.Append((OpenXmlElement)element.CloneNode(true));
            }
        }
    }
}

int i = 0;
private void ReplacePlaceholdersInBody(Body body, Dictionary<string, string> data)
{
    foreach (var text in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>())
    {
        foreach (var key in data.Keys)
        {
            if (text.Text.Contains(key) && ( key == "##NAME_SURVEY##" || key == "##COUNT_CRITERIES##" || key == "##MASS_CRITERIES_LIST##"))
            {
                if (key == "##MASS_CRITERIES_LIST##")
                {
                    var values = data[key].Split(new[] { "\n" }, StringSplitOptions.None);
                    foreach (var value in values)
                    {
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            text.Parent.Append(new DocumentFormat.OpenXml.Wordprocessing.Run(new DocumentFormat.OpenXml.Wordprocessing.Text(value)));
                            text.Parent.Append(new DocumentFormat.OpenXml.Wordprocessing.Break());
                        }
                    }
                    text.Text = string.Empty;
                }
                else
                {
                    text.Text = text.Text.Replace(key, data[key]);
                }
            }
        }
    }
}

private void ReplacePlaceholdersInTable(Body body, Dictionary<string, string> data)
{
    var tables = body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Table>();
    foreach (var table in tables)
    {
        foreach (var text in table.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>())
        {
            foreach (var key in data.Keys)
            {
                if (text.Text.Contains(key))
                {
                      if (key == "##MASS_CRITERIES_FOR_TABLE##")
{
    var cell = text.Ancestors<TableCell>().FirstOrDefault();
    if (cell != null)
    {
        var tableRow = cell.Ancestors<TableRow>().FirstOrDefault();
        if (tableRow != null)
        {
            if (key == "##MASS_CRITERIES_FOR_TABLE##")
            {
                var values = data[key].Split(new[] { "     " }, StringSplitOptions.None);
                text.Text = values[0];
                
                for (int i = values.Length - 1; i >= 1; i--)
                {
                    var newCell = new TableCell(
                        new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                            new DocumentFormat.OpenXml.Wordprocessing.Run(
                                new DocumentFormat.OpenXml.Wordprocessing.Text(values[i])
                            )
                        )
                    );
                    cell.InsertAfterSelf(newCell);
                }
            }
            else
            {
                var ratingRows = data[key].Split(new[] { "\n" }, StringSplitOptions.None);
                var firstRowRatings = ratingRows[0].Split(new[] { "     " }, StringSplitOptions.None);
                
                text.Text = firstRowRatings[0];
                
                for (int i = firstRowRatings.Length - 1; i >= 1; i--)
                {
                    var newCell = new TableCell(
                        new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                            new DocumentFormat.OpenXml.Wordprocessing.Run(
                                new DocumentFormat.OpenXml.Wordprocessing.Text(firstRowRatings[i])
                            )
                        )
                    );
                    cell.InsertAfterSelf(newCell);
                }
            }
        }
    }
}

if (key == "##DATE##")
{
    var cell = text.Ancestors<TableCell>().FirstOrDefault();
    if (cell != null)
    {
        var row = cell.Ancestors<TableRow>().FirstOrDefault();
  if (row != null)
        {
            var criteria1 = data["##MASS_CRITERIES_FOR_TABLE##"].Split(new[] { "     " }, StringSplitOptions.None);
            var criteria2 = data["##MASS_NAMES_CRITERIES_FOR_COMMENTS##"].Split(new[] { "     " }, StringSplitOptions.None);
            
            int totalColumns = criteria1.Length + criteria2.Length;
        
            text.Text = data[key];
            cell.TableCellProperties = new TableCellProperties(
                new GridSpan() { Val = totalColumns*2 }
            );
            
            var cellsToRemove = row.Elements<TableCell>().Where(c => c != cell).ToList();
            foreach (var cellToRemove in cellsToRemove)
            {
                cellToRemove.Remove();
            }
        }
    }
}

if (key == "##SREDNEE##")
{
    var cell = text.Ancestors<TableCell>().FirstOrDefault();
    if (cell != null)
    {
        var tableRow = cell.Ancestors<TableRow>().FirstOrDefault();
        if (tableRow != null)
        {
                    var row = cell.Ancestors<TableRow>().FirstOrDefault();
        if (row != null)
        {
            var criteria = data["##MASS_CRITERIES_FOR_TABLE##"].Split(new[] { "     " }, StringSplitOptions.None);
            int totalColumns = criteria.Length;

            var values = data[key].Split(new[] { "     " }, StringSplitOptions.None);
            
            text.Text = values[0];
            
            for (int i = 1; i < criteria.Length+1; i++)
            {
                var newCell = new TableCell(
                    new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                        new DocumentFormat.OpenXml.Wordprocessing.Run(
                            new DocumentFormat.OpenXml.Wordprocessing.Text(i < values.Length ? values[i] : "")
                        )
                    )
                );
                var lastCell = tableRow.Elements<TableCell>().Last();
                lastCell.InsertAfterSelf(newCell);
            }

            for (int i = 0; i < criteria.Length - 1; i++)
            {
                var emptyCell = new TableCell(
                    new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                        new DocumentFormat.OpenXml.Wordprocessing.Run(
                            new DocumentFormat.OpenXml.Wordprocessing.Text("")
                        )
                    )
                );
                tableRow.Append(emptyCell);
            }

                        var cells = row.Elements<TableCell>().ToList();
                cells[2].Remove();
            
        }
    }
}
}

if (key == "##MASS_OMSUS##")
{
    var cell = text.Ancestors<TableCell>().FirstOrDefault();
    if (cell != null)
    {
        var row = cell.Ancestors<TableRow>().FirstOrDefault();
        if (row != null)
        {
            var omsus = data[key].Split(new[] { "\n" }, StringSplitOptions.None);
            var ratings = data["##MASS_RATINGS##"].Split(new[] { "\n" }, StringSplitOptions.None);
            var comments = data["##MASS_COMMENTS##"].Split(new[] { "\n" }, StringSplitOptions.None);
            
            text.Text = omsus[0];

            var cells = row.Elements<TableCell>().ToList();
                cells[0].Remove();
                cells[1].Remove();
            
            var firstRatings = ratings[0].Split(new[] { "     " }, StringSplitOptions.None);
            for (int j = 0; j < firstRatings.Length; j++)
            {
                var ratingCell = new TableCell(
                    new DocumentFormat.OpenXml.Wordprocessing.Paragraph(new DocumentFormat.OpenXml.Wordprocessing.Run(new DocumentFormat.OpenXml.Wordprocessing.Text(firstRatings[j])))
                );
                row.Append(ratingCell);
            }
            
            var firstComments = comments[0].Split(new[] { "     " }, StringSplitOptions.None);
            for (int j = 0; j < firstComments.Length; j++)
            {
                var commentCell = new TableCell(
                    new DocumentFormat.OpenXml.Wordprocessing.Paragraph(new DocumentFormat.OpenXml.Wordprocessing.Run(new DocumentFormat.OpenXml.Wordprocessing.Text(firstComments[j])))
                );
                row.Append(commentCell);
            }
            
            for (int i = 1; i < omsus.Length; i++)
            {
                var newRow = new TableRow();
                
                var omsuCell = new TableCell(
                    new DocumentFormat.OpenXml.Wordprocessing.Paragraph(new DocumentFormat.OpenXml.Wordprocessing.Run(new DocumentFormat.OpenXml.Wordprocessing.Text(omsus[i])))
                );
                newRow.Append(omsuCell);
                
                var currentRatings = ratings[i].Split(new[] { "     " }, StringSplitOptions.None);
                for (int j = 0; j < currentRatings.Length; j++)
                {
                    var ratingCell = new TableCell(
                        new DocumentFormat.OpenXml.Wordprocessing.Paragraph(new DocumentFormat.OpenXml.Wordprocessing.Run(new DocumentFormat.OpenXml.Wordprocessing.Text(currentRatings[j])))
                    );
                    newRow.Append(ratingCell);
                }
                
                var currentComments = comments[i].Split(new[] { "     " }, StringSplitOptions.None);
                for (int j = 0; j < currentComments.Length; j++)
                {
                    var commentCell = new TableCell(
                        new DocumentFormat.OpenXml.Wordprocessing.Paragraph(new DocumentFormat.OpenXml.Wordprocessing.Run(new DocumentFormat.OpenXml.Wordprocessing.Text(currentComments[j])))
                    );
                    newRow.Append(commentCell);
                }
                
                row.InsertAfterSelf(newRow);
                row = newRow;
            }
        }
    }
}
                    

if (key == "##MASS_NAMES_CRITERIES_FOR_COMMENTS##")
{
    var cell = text.Ancestors<TableCell>().FirstOrDefault();
    if (cell != null)
    {
        var tableRow = cell.Ancestors<TableRow>().FirstOrDefault();
        if (tableRow != null)
        {
            var criteria = data[key].Split(new[] { "     " }, StringSplitOptions.None);
            
            text.Text = "Комментарии: " + criteria[0];
            
            for (int i = criteria.Length - 1; i >= 1; i--)
            {
                var newCell = new TableCell(
                    new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                        new DocumentFormat.OpenXml.Wordprocessing.Run(
                            new DocumentFormat.OpenXml.Wordprocessing.Text(criteria[i])
                        )
                    )
                );
                cell.InsertAfterSelf(newCell);
            }
        }
    }
}
                }}}}
}}

public class CSPRequest
{
    public string Signature { get; set; }
}

     public class PdfAnswerItem
    {
        public string? Question { get; set; }
        public string? Rating { get; set; }
        public string? Comment { get; set; }
    }
