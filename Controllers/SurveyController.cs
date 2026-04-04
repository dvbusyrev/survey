using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using main_project.Models;
using main_project.Data;
using DocumentFormat.OpenXml.Spreadsheet;
using System.Security.Claims;
using System.Data;
using Npgsql;
using Dapper;
using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Vml.Spreadsheet;
using DocumentFormat.OpenXml.Drawing.Spreadsheet;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Office2010.Excel;
using OfficeOpenXml.Style;
using OfficeOpenXml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using ClosedXML;

using Word = DocumentFormat.OpenXml.Wordprocessing;
using Excel = DocumentFormat.OpenXml.Spreadsheet;
using Justification = DocumentFormat.OpenXml.Wordprocessing.Justification;
using Table = DocumentFormat.OpenXml.Wordprocessing.Table;
using TableCell = DocumentFormat.OpenXml.Wordprocessing.TableCell;
using TableRow = DocumentFormat.OpenXml.Wordprocessing.TableRow;
using TableProperties = DocumentFormat.OpenXml.Wordprocessing.TableProperties;
using TableBorders = DocumentFormat.OpenXml.Wordprocessing.TableBorders;
using TopBorder = DocumentFormat.OpenXml.Wordprocessing.TopBorder;
using LeftBorder = DocumentFormat.OpenXml.Wordprocessing.LeftBorder;
using RightBorder = DocumentFormat.OpenXml.Wordprocessing.RightBorder;
using BottomBorder = DocumentFormat.OpenXml.Wordprocessing.BottomBorder;
using InsideHorizontalBorder = DocumentFormat.OpenXml.Wordprocessing.InsideHorizontalBorder;
using InsideVerticalBorder = DocumentFormat.OpenXml.Wordprocessing.InsideVerticalBorder;
using GridSpan = DocumentFormat.OpenXml.Wordprocessing.GridSpan;
using TableCellProperties = DocumentFormat.OpenXml.Wordprocessing.TableCellProperties;
using ParagraphProperties = DocumentFormat.OpenXml.Wordprocessing.ParagraphProperties;
using Run = DocumentFormat.OpenXml.Wordprocessing.Run;
using Text = DocumentFormat.OpenXml.Wordprocessing.Text;
using RunProperties = DocumentFormat.OpenXml.Wordprocessing.RunProperties;
using Bold = DocumentFormat.OpenXml.Wordprocessing.Bold;
using Break = DocumentFormat.OpenXml.Wordprocessing.Break;
[Authorize]
public class SurveyController : Controller
{

    private int? GetCurrentUserId()
    {
        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    }

    private bool IsAdmin()
    {
        return User.IsInRole("Админ");
    }

    private IActionResult? EnsureUserRouteAccess(int requestedUserId)
    {
        var currentUserId = GetCurrentUserId();

        if (!currentUserId.HasValue)
            return Challenge();

        if (!IsAdmin() && currentUserId.Value != requestedUserId)
            return Forbid();

        return null;
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
    private readonly IConfiguration _configuration;
    private readonly ILogger<SurveyController> _logger;
    private readonly string _connectionString;
    private readonly IWebHostEnvironment _environment;
    private readonly LogController _logController;
    private readonly string _downloadsPath;

    public SurveyController(IConfiguration configuration, ILogger<SurveyController> logger, IWebHostEnvironment environment, LogController logController)
    {
        _db = new DatabaseController(configuration);
        _logger = logger;
        _configuration = configuration;
        _connectionString = _configuration.GetConnectionString("DefaultConnection");
        _environment = environment;
        _logController = logController;
                _downloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        
    }

    [HttpGet("view_answer/{idSurvey}/{idOmsu}/{type}")]
[Authorize(Roles = "Админ")]
public IActionResult ViewAnswer(int idSurvey, int idOmsu, string type)
{
    try
    {
        List<HistoryAnswer> answers = new List<HistoryAnswer>();

        using (var connection = _db.CreateConnection())
        {
            answers = connection.Query<HistoryAnswer>(@"
                SELECT 
                    ha.id_answer,
                    ha.id_survey,
                    ha.id_omsu,
                    o.name_omsu,
                    ha.completion_date,
                    ha.create_date_survey,
                    ha.answers,
                    ha.csp,
                    s.name_survey
                FROM 
                    public.history_answer ha
                JOIN 
                    public.omsu o ON o.id_omsu = ha.id_omsu
                JOIN
                    public.surveys s ON s.id_survey = ha.id_survey
                WHERE 
                    ha.id_survey = @SurveyId
                ORDER BY 
                    ha.completion_date DESC", 
                new { SurveyId = idSurvey }).ToList();
        }

        return View(answers);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, $"Ошибка при получении ответов для анкеты {idSurvey}");
        return StatusCode(500, "Произошла ошибка при загрузке данных");
    }
}

 [Authorize(Roles = "Админ")]
[HttpGet("view_otchets")]
[Authorize(Roles = "Админ")]
public IActionResult view_otchets()
{
    return View();
}


// ФУНКЦИИ СОЗДАНИЯ ОТЧЁТА В DOCX (ИСПОЛЬЗУЕТСЯ БИБЛИОТЕКА OPENXML)
        string nameOmsu = "";
        string nameSurvey = "";
        string answers = "";

        List<int> ratings = new List<int>();
        List<string> comments = new List<string>();
        List<string> questionIds = new List<string>();

                private readonly string _templatePath = @"wwwroot\docx\shablon_docx.docx";       




 [Authorize]
public IActionResult create_otchet_month(int id, int idOmsu, string type)
    {
        Console.WriteLine(id);
        string surveyName = "";
        List<string> criteriaList = new List<string>();
        int criteriaCount = 0;
        List<string> omsus = new List<string>();
        List<List<int>> ratings = new List<List<int>>();
        List<List<string>> comments = new List<List<string>>();
        List<double> srednee = new List<double>();

        using (var connection = _db.CreateConnection())
        {
            // Получаем название анкеты
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT name_survey FROM ( SELECT name_survey FROM public.surveys WHERE id_survey = @surveyId UNION ALL SELECT name_survey FROM public.history_surveys WHERE id_survey = @surveyId) sub LIMIT 1 ";
                command.Parameters.Add(new NpgsqlParameter("@surveyId", NpgsqlTypes.NpgsqlDbType.Integer) { Value = id });

                var result = command.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                {
                    surveyName = result.ToString();
                }
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT questions
FROM (
    SELECT questions
    FROM public.surveys
    WHERE id_survey = @surveyId

    UNION ALL

    SELECT file_questions
    FROM public.history_surveys
    WHERE id_survey =  @surveyId
) sub
LIMIT 1";

                command.Parameters.Add(new NpgsqlParameter("@surveyId", NpgsqlTypes.NpgsqlDbType.Integer) { Value = id });

                var result = command.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                {
                    var questionsJson = result.ToString();
                    var questions = JObject.Parse(questionsJson)["questions"];
                    foreach (var question in questions)
                    {
                        criteriaList.Add(question["text"].ToString());
                    }
                    criteriaCount = criteriaList.Count;
                }
            }

            if (idOmsu == 0)
            {
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
                    WHERE ha.answers IS NOT NULL";

                    command.Parameters.Add(new NpgsqlParameter("@surveyId", NpgsqlTypes.NpgsqlDbType.Integer) { Value = id });

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var nameOmsu = reader.GetString(0);
                            var answersJson = reader.GetString(1);

                            omsus.Add(nameOmsu);

                            var answers = JArray.Parse(answersJson);
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
                    }
                }
            }
            else
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                    SELECT 
                        o.name_omsu, 
                        ha.answers
                    FROM 
                        public.omsu o 
                    JOIN 
                        public.history_answer ha 
                    ON 
                        o.id_omsu = ha.id_omsu 
                    AND 
                        ha.id_survey = @surveyId
                    WHERE 
                        o.id_omsu = @omsuId
                    AND 
                        ha.answers IS NOT NULL";
                    
                    command.Parameters.Add(new NpgsqlParameter("@surveyId", NpgsqlTypes.NpgsqlDbType.Integer) { Value = id });
                    command.Parameters.Add(new NpgsqlParameter("@omsuId", NpgsqlTypes.NpgsqlDbType.Integer) { Value = idOmsu });

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var nameOmsu = reader.GetString(0);
                            var answersJson = reader.GetString(1);

                            omsus.Add(nameOmsu);

                            var answers = JArray.Parse(answersJson);
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
                srednee.Add(count > 0 ? sum / count : 0);
            }
        }

        string currentMonth = DateTime.Now.ToString("MMMM yyyy").ToLower();
        string fileName = idOmsu == 0 
            ? $"Отчет по анкете {surveyName} ({currentMonth}).docx"
            : $"Отчет по анкете {surveyName} для {omsus.FirstOrDefault()} ({currentMonth}).docx";

        using (MemoryStream mem = new MemoryStream())
        {
            using (WordprocessingDocument document = WordprocessingDocument.Create(mem, WordprocessingDocumentType.Document, true))
            {
                MainDocumentPart mainPart = document.AddMainDocumentPart();
                mainPart.Document = new Document();
                Body body = mainPart.Document.AppendChild(new Body());
                
                // Добавляем заголовок (используем Word.FontSize и Word.Bold)
                Paragraph titleParagraph = new Paragraph(
                    new Run(
                        new Text($"Отчет по анкете \"{surveyName}\"")
                    )
                    {
                        RunProperties = new RunProperties(
                            new Word.Bold(),
                            new Word.FontSize() { Val = "28" }
                        )
                    }
                );
                titleParagraph.ParagraphProperties = new ParagraphProperties(
                    new Justification() { Val = JustificationValues.Center },
                    new SpacingBetweenLines() { After = "200" }
                );
                body.AppendChild(titleParagraph);

                // Добавляем подзаголовок с датой
                Paragraph dateParagraph = new Paragraph(
                    new Run(
                        new Text($"за {currentMonth}")
                    )
                    {
                        RunProperties = new RunProperties(
                            new Word.Italic(),
                            new Word.FontSize() { Val = "22" }
                        )
                    }
                );
                dateParagraph.ParagraphProperties = new ParagraphProperties(
                    new Justification() { Val = JustificationValues.Center },
                    new SpacingBetweenLines() { After = "300" }
                );
                body.AppendChild(dateParagraph);

                // Добавляем описание
                Paragraph descriptionParagraph = new Paragraph(
                    new Run(
                        new Text("Данный отчет содержит информацию об оценках удовлетворенности потребителей услуг, полученных в результате ежемесячного анкетирования.")
                    )
                    {
                        RunProperties = new RunProperties(
                            new Word.FontSize() { Val = "20" }
                        )
                    }
                );
                descriptionParagraph.ParagraphProperties = new ParagraphProperties(
                    new SpacingBetweenLines() { After = "200" }
                );
                body.AppendChild(descriptionParagraph);

                // Добавляем критерии оценки
                Paragraph criteriaTitle = new Paragraph(
                    new Run(
                        new Text("Критерии оценки:")
                    )
                    {
                        RunProperties = new RunProperties(
                            new Word.Bold(),
                            new Word.FontSize() { Val = "20" }
                        )
                    }
                );
                criteriaTitle.ParagraphProperties = new ParagraphProperties(
                    new SpacingBetweenLines() { After = "100" }
                );
                body.AppendChild(criteriaTitle);

                foreach (var criteria in criteriaList)
                {
                    Paragraph criteriaItem = new Paragraph(
                        new Run(
                            new Text($"• {criteria}")
                        )
                        {
                            RunProperties = new RunProperties(
                                new Word.FontSize() { Val = "18" }
                            )
                        }
                    );
                    criteriaItem.ParagraphProperties = new ParagraphProperties(
                        new SpacingBetweenLines() { After = "50" }
                    );
                    body.AppendChild(criteriaItem);
                }

                // Добавляем таблицу с результатами
                Table table = new Table();
                
                // Настройки таблицы
                TableProperties tableProps = new TableProperties(
                    new TableBorders(
                        new TopBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                        new BottomBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                        new LeftBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                        new RightBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                        new InsideHorizontalBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                        new InsideVerticalBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 }
                    ),
                    new TableWidth() { Width = "100%", Type = TableWidthUnitValues.Auto },
                    new TableLayout() { Type = TableLayoutValues.Fixed }
                );
                table.AppendChild(tableProps);

                // Заголовок таблицы
                TableRow headerRow = new TableRow();
                
                // Ячейка с названием организации
                headerRow.Append(CreateTableCell("Наименование организации", true, true));
                
                // Ячейки с критериями
                foreach (var criteria in criteriaList)
                {
                    headerRow.Append(CreateTableCell(criteria, true, true));
                }
                
                // Ячейка со средним баллом
                headerRow.Append(CreateTableCell("Средний балл", true, true));
                
                // Ячейки с комментариями
                foreach (var criteria in criteriaList)
                {
                    headerRow.Append(CreateTableCell($"Комментарий ({criteria})", true, true));
                }
                
                table.Append(headerRow);

                // Данные по организациям
                for (int i = 0; i < omsus.Count; i++)
                {
                    TableRow dataRow = new TableRow();
                    
                    // Название организации
                    dataRow.Append(CreateTableCell(omsus[i], false, false));
                    
                    // Оценки по критериям
                    for (int j = 0; j < criteriaList.Count; j++)
                    {
                        string ratingValue = (ratings.Count > i && ratings[i].Count > j) ? ratings[i][j].ToString() : "-";
                        dataRow.Append(CreateTableCell(ratingValue, false, true));
                    }
                    
                    // Средний балл
                    string avgValue = (srednee.Count > i) ? srednee[i].ToString("F1") : "-";
                    dataRow.Append(CreateTableCell(avgValue, false, true));
                    
                    // Комментарии
                    for (int j = 0; j < criteriaList.Count; j++)
                    {
                        string comment = (comments.Count > i && comments[i].Count > j) ? comments[i][j] : "-";
                        dataRow.Append(CreateTableCell(comment, false, false));
                    }
                    
                    table.Append(dataRow);
                }
                
                // Итоговая строка
                TableRow totalRow = new TableRow();
                totalRow.Append(CreateTableCell("Итого:", true, false));
                
                // Средние по критериям
                for (int i = 0; i < criteriaList.Count; i++)
                {
                    double sum = 0;
                    int count = 0;
                    for (int row = 0; row < ratings.Count; row++)
                    {
                        if (ratings[row].Count > i)
                        {
                            sum += ratings[row][i];
                            count++;
                        }
                    }
                    string avgValue = count > 0 ? (sum / count).ToString("F1") : "-";
                    totalRow.Append(CreateTableCell(avgValue, false, true));
                }
                
                // Общее среднее
                double totalAvg = srednee.Count > 0 ? srednee.Average() : 0;
                totalRow.Append(CreateTableCell(totalAvg.ToString("F1"), false, true));
                
                // Пустые ячейки для комментариев
                for (int i = 0; i < criteriaList.Count; i++)
                {
                    totalRow.Append(CreateTableCell("", false, false));
                }
                
                table.Append(totalRow);

                body.AppendChild(table);

                // Добавляем итоговую оценку
                Paragraph finalScore = new Paragraph(
                    new Run(
                        new Text($"Общая оценка удовлетворенности: {totalAvg:F1} из 5")
                    )
                    {
                        RunProperties = new RunProperties(
                            new Word.Bold(),
                            new Word.FontSize() { Val = "20" }
                        )
                    }
                );
                finalScore.ParagraphProperties = new ParagraphProperties(
                    new Justification() { Val = JustificationValues.Right },
                    new SpacingBetweenLines() { Before = "300", After = "200" }
                );
                body.AppendChild(finalScore);

                // Добавляем подпись
                Paragraph signature = new Paragraph(
                    new Run(
                        new Text("Отчет сформирован автоматически")
                    )
                    {
                        RunProperties = new RunProperties(
                            new Word.Italic(),
                            new Word.FontSize() { Val = "16" }
                        )
                    }
                );
                signature.ParagraphProperties = new ParagraphProperties(
                    new Justification() { Val = JustificationValues.Right }
                );
                body.AppendChild(signature);
            }
            
            return File(mem.ToArray(), "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
        }
    }

    private TableCell CreateTableCell(string text, bool isHeader, bool centerAlign)
    {
        var cell = new TableCell(
            new Paragraph(
                new Run(
                    new Text(text)
                )
                {
                    RunProperties = new RunProperties(
                        isHeader ? new Word.Bold() : null,
                        new Word.FontSize() { Val = isHeader ? "18" : "16" }
                    )
                }
            )
        );
        
cell.TableCellProperties = new TableCellProperties(
    new Justification() { Val = centerAlign ? JustificationValues.Center : JustificationValues.Left },
    new TableCellVerticalAlignment() { Val = TableVerticalAlignmentValues.Center },
    new TableCellWidth() { Type = TableWidthUnitValues.Auto }
);
        
        return cell;
    }

public IActionResult create_otchetAll_month()
{
    // Получаем список ID анкет (активных и архивных)
    List<int> activeSurveyIds = new List<int>();
    List<int> archiveSurveyIds = new List<int>();
    
    using (var connection = _db.CreateConnection())
    {
        using (var command = connection.CreateCommand())
        {
            command.CommandText = @"
                SELECT id_survey FROM public.surveys";
            
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    activeSurveyIds.Add(reader.GetInt32(0));
                }
            }
        }
    }

    string currentMonth = DateTime.Now.ToString("MMMM yyyy").ToLower();
    string fileName = $"Сводный отчет по всем анкетам ({currentMonth}).docx";

    using (MemoryStream mem = new MemoryStream())
    {
        using (WordprocessingDocument document = WordprocessingDocument.Create(mem, WordprocessingDocumentType.Document, true))
        {
            MainDocumentPart mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document();
            Body body = mainPart.Document.AppendChild(new Body());

            // Заголовок отчета
            body.AppendChild(new Paragraph(
                new Run(
                    new Text("Сводный отчет по всем анкетам")
                )
                {
                    RunProperties = new RunProperties(
                        new DocumentFormat.OpenXml.Wordprocessing.Bold(),
                        new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = "28" }
                    )
                })
            {
                ParagraphProperties = new ParagraphProperties(
                    new Justification() { Val = JustificationValues.Center },
                    new SpacingBetweenLines() { After = "200" }
                )
            });

            // Подзаголовок с датой
            body.AppendChild(new Paragraph(
                new Run(
                    new Text($"за {currentMonth}")
                )
                {
                    RunProperties = new RunProperties(
                        new DocumentFormat.OpenXml.Wordprocessing.Italic(),
                        new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = "22" }
                    )
                })
            {
                ParagraphProperties = new ParagraphProperties(
                    new Justification() { Val = JustificationValues.Center },
                    new SpacingBetweenLines() { After = "300" }
                )
            });

            // Описание отчета
            body.AppendChild(new Paragraph(
                new Run(
                    new Text("Данный отчет содержит сводную информацию по всем анкетам за указанный месяц.")
                )
                {
                    RunProperties = new RunProperties(
                        new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = "20" }
                    )
                })
            {
                ParagraphProperties = new ParagraphProperties(
                    new SpacingBetweenLines() { After = "200" }
                )
            });

            foreach (var surveyId in activeSurveyIds)
            {
                string surveyName = "";
                bool isArchive = false;
                List<string> criteriaList = new List<string>();
                List<string> omsus = new List<string>();
                List<List<int>> ratings = new List<List<int>>();
                List<double> srednee = new List<double>();

                // Проверяем, активная это анкета или архивная
                using (var connection = _db.CreateConnection())
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT COUNT(*) FROM public.surveys WHERE id_survey = @surveyId";
                        command.Parameters.Add(new NpgsqlParameter("@surveyId", surveyId));
                        var count = (long)command.ExecuteScalar();
                        isArchive = count == 0;
                    }
                }

                // Получаем данные анкеты
                using (var connection = _db.CreateConnection())
                {
                    // Название анкеты
                    using (var command = connection.CreateCommand())
                    {
                        if (!isArchive)
                        {
                            command.CommandText = "SELECT name_survey FROM public.surveys WHERE id_survey = @surveyId";
                        }
                        else
                        {
                            command.CommandText = "SELECT name_survey FROM public.history_surveys WHERE id_survey = @surveyId";
                        }
                        command.Parameters.Add(new NpgsqlParameter("@surveyId", surveyId));
                        surveyName = command.ExecuteScalar()?.ToString() ?? "";
                    }

                    // Вопросы анкеты
                    using (var command = connection.CreateCommand())
                    {
                        if (!isArchive)
                        {
                            command.CommandText = "SELECT questions FROM public.surveys WHERE id_survey = @surveyId";
                        }
                        else
                        {
                            command.CommandText = "SELECT file_questions FROM public.history_surveys WHERE id_survey = @surveyId";
                        }
                        command.Parameters.Add(new NpgsqlParameter("@surveyId", surveyId));

                        var result = command.ExecuteScalar();
                        if (result != null)
                        {
                            var questions = JObject.Parse(result.ToString())["questions"];
                            criteriaList = questions.Select(q => q["text"].ToString()).ToList();
                        }
                    }

                    // Ответы организаций
                    using (var command = connection.CreateCommand())
                    {
                        if (!isArchive)
                        {
                            command.CommandText = @"
                                SELECT o.name_omsu, a.answers 
                                FROM public.omsu o 
                                JOIN public.history_answer a ON o.id_omsu = a.id_omsu 
                                WHERE a.id_survey = @surveyId AND a.answers IS NOT NULL";
                        }
                        else
                        {
                            command.CommandText = @"
                                SELECT o.name_omsu, ha.answers 
                                FROM public.omsu o 
                                JOIN public.history_answer ha ON o.id_omsu = ha.id_omsu 
                                WHERE ha.id_survey = @surveyId AND ha.answers IS NOT NULL";
                        }
                        command.Parameters.Add(new NpgsqlParameter("@surveyId", surveyId));

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                omsus.Add(reader.GetString(0));
                                var answers = JArray.Parse(reader.GetString(1));
                                ratings.Add(answers.Select(a => (int)a["rating"]).ToList());
                            }
                        }
                    }

                    // Средние оценки
                    for (int i = 0; i < criteriaList.Count; i++)
                    {
                        double sum = 0;
                        int count = 0;
                        for (int j = 0; j < ratings.Count; j++)
                        {
                            if (ratings[j].Count > i)
                            {
                                sum += ratings[j][i];
                                count++;
                            }
                        }
                        srednee.Add(count > 0 ? sum / count : 0);
                    }
                }

                // Заголовок анкеты с пометкой (архивная/активная)
                string surveyTitle = surveyName + (isArchive ? " (архивная)" : "");
                body.AppendChild(new Paragraph(
                    new Run(new Text(surveyTitle))
                    {
                        RunProperties = new RunProperties(
                            new DocumentFormat.OpenXml.Wordprocessing.Bold(),
                            new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = "22" }
                        )
                    })
                {
                    ParagraphProperties = new ParagraphProperties(
                        new SpacingBetweenLines() { Before = "400", After = "100" }
                    )
                });

                // Таблица с вопросами и оценками
                var questionsTable = new Table();
                questionsTable.AppendChild(new TableProperties(
                    new TableBorders(
                        new TopBorder { Val = BorderValues.Single, Size = 4 },
                        new BottomBorder { Val = BorderValues.Single, Size = 4 },
                        new LeftBorder { Val = BorderValues.Single, Size = 4 },
                        new RightBorder { Val = BorderValues.Single, Size = 4 },
                        new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                        new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 }
                    ),
                    new TableWidth() { Width = "100%", Type = TableWidthUnitValues.Auto }
                ));

                // Заголовок таблицы вопросов
                var qHeaderRow = new TableRow();
                qHeaderRow.Append(new TableCell(new Paragraph(new Run(new Text("№")) { RunProperties = new RunProperties(new DocumentFormat.OpenXml.Wordprocessing.Bold()) })));
                qHeaderRow.Append(new TableCell(new Paragraph(new Run(new Text("Вопрос")) { RunProperties = new RunProperties(new DocumentFormat.OpenXml.Wordprocessing.Bold()) })));
                qHeaderRow.Append(new TableCell(new Paragraph(new Run(new Text("Средняя оценка")) { RunProperties = new RunProperties(new DocumentFormat.OpenXml.Wordprocessing.Bold()) })));
                questionsTable.Append(qHeaderRow);

                // Данные вопросов
                for (int i = 0; i < criteriaList.Count; i++)
                {
                    var row = new TableRow();
                    row.Append(new TableCell(new Paragraph(new Run(new Text((i + 1).ToString())))));
                    row.Append(new TableCell(new Paragraph(new Run(new Text(criteriaList[i])))));
                    row.Append(new TableCell(new Paragraph(new Run(new Text(srednee[i].ToString("F1"))))));
                    questionsTable.Append(row);
                }
                body.AppendChild(questionsTable);

                // Если есть ответы, выводим таблицу с организациями
                if (omsus.Count > 0)
                {
                    var orgsTable = new Table();
                    orgsTable.AppendChild(new TableProperties(
                        new TableBorders(
                            new TopBorder { Val = BorderValues.Single, Size = 4 },
                            new BottomBorder { Val = BorderValues.Single, Size = 4 },
                            new LeftBorder { Val = BorderValues.Single, Size = 4 },
                            new RightBorder { Val = BorderValues.Single, Size = 4 },
                            new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                            new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 }
                        ),
                        new TableWidth() { Width = "100%", Type = TableWidthUnitValues.Auto }
                    ));

                    // Заголовок таблицы организаций
                    var oHeaderRow = new TableRow();
                    oHeaderRow.Append(new TableCell(new Paragraph(new Run(new Text("Организация")) { RunProperties = new RunProperties(new DocumentFormat.OpenXml.Wordprocessing.Bold()) })));
                    oHeaderRow.Append(new TableCell(new Paragraph(new Run(new Text("Средняя оценка")) { RunProperties = new RunProperties(new DocumentFormat.OpenXml.Wordprocessing.Bold()) })));
                    oHeaderRow.Append(new TableCell(new Paragraph(new Run(new Text("Кол-во ответов")) { RunProperties = new RunProperties(new DocumentFormat.OpenXml.Wordprocessing.Bold()) })));
                    orgsTable.Append(oHeaderRow);

                    // Данные организаций
                    for (int i = 0; i < omsus.Count; i++)
                    {
                        var row = new TableRow();
                        row.Append(new TableCell(new Paragraph(new Run(new Text(omsus[i])))));
                        row.Append(new TableCell(new Paragraph(new Run(new Text(ratings[i].Count > 0 ? ratings[i].Average().ToString("F1") : "0")))));
                        row.Append(new TableCell(new Paragraph(new Run(new Text(ratings[i].Count.ToString())))));
                        orgsTable.Append(row);
                    }

                    // Итоговая строка
                    var totalRow = new TableRow();
                    totalRow.Append(new TableCell(new Paragraph(new Run(new Text("Итого:")))));
                    totalRow.Append(new TableCell(new Paragraph(new Run(new Text(srednee.Count > 0 ? srednee.Average().ToString("F1") : "0")))));
                    totalRow.Append(new TableCell(new Paragraph(new Run(new Text(ratings.Sum(r => r.Count).ToString())))));
                    orgsTable.Append(totalRow);

                    body.AppendChild(orgsTable);
                }
                else
                {
                    body.AppendChild(new Paragraph(
                        new Run(new Text("Нет данных по ответам организаций"))
                        {
                            RunProperties = new RunProperties(
                                new DocumentFormat.OpenXml.Wordprocessing.Italic(),
                                new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = "16" }
                            )
                        })
                    {
                        ParagraphProperties = new ParagraphProperties(
                            new SpacingBetweenLines() { Before = "100", After = "100" }
                        )
                    });
                }

                // Разрыв страницы между анкетами
                if (surveyId != activeSurveyIds.Last())
                {
                    body.AppendChild(new Paragraph(new Run(new Break() { Type = BreakValues.Page })));
                }
            }

            // Подпись
            body.AppendChild(new Paragraph(
                new Run(new Text("Отчет сформирован автоматически"))
                {
                    RunProperties = new RunProperties(
                        new DocumentFormat.OpenXml.Wordprocessing.Italic(),
                        new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = "16" }
                    )
                })
            {
                ParagraphProperties = new ParagraphProperties(
                    new Justification() { Val = JustificationValues.Right },
                    new SpacingBetweenLines() { Before = "300" }
                )
            });
        }

        return File(mem.ToArray(), "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
    }
}

  // Метод для получения списка ID анкет из базы данных
    private List<int> GetSurveyIdsFromDatabase()
    {
        List<int> surveyIds = new List<int>();

        using (var connection = _db.CreateConnection())
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT id_survey FROM public.surveys";

                try
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            surveyIds.Add(reader.GetInt32(0));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при получении айдишников анкет: {ex.Message}");
                }
            }
        }

        return surveyIds;
    }
[HttpGet]
public IActionResult GetSurveyAnswers(int id)
{
    try
    {
        using (var connection = _db.CreateConnection())
        {
            var survey = new Survey();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT id_survey, name_survey, description, date_open, date_close 
                    FROM surveys 
                    WHERE id_survey = @id";
                command.Parameters.Add(new NpgsqlParameter("@id", id));
                
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        survey = new Survey
                        {
                            id_survey = reader.GetInt32(reader.GetOrdinal("id_survey")),
                            name_survey = reader.GetString(reader.GetOrdinal("name_survey")),
                            description = reader.IsDBNull(reader.GetOrdinal("description")) 
                                ? null : reader.GetString(reader.GetOrdinal("description")),
                            date_open = reader.GetDateTime(reader.GetOrdinal("date_open")),
                            date_close= reader.GetDateTime(reader.GetOrdinal("date_close"))
                        };
                    }
                    else
                    {
                        return Json(new { 
                            success = false, 
                            error = "Анкета не найдена" 
                        });
                    }
                }
            }

            var answers = new List<HistoryAnswer>();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT 
                        ha.id_answer,
                        ha.id_omsu,
                        ha.id_survey,
                        o.name_omsu,
                        ha.csp,
                        ha.completion_date,
                        ha.answers
                    FROM history_answer ha
                    JOIN omsu o ON ha.id_omsu = o.id_omsu
                    WHERE ha.id_survey = @id
                    ORDER BY ha.completion_date DESC";
                command.Parameters.Add(new NpgsqlParameter("@id", id));
                
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        answers.Add(new HistoryAnswer
                        {
                            id_answer = reader.GetInt32(reader.GetOrdinal("id_answer")),
                            id_omsu = reader.GetInt32(reader.GetOrdinal("id_omsu")),
                            id_survey = reader.GetInt32(reader.GetOrdinal("id_survey")),
                            name_omsu = reader.GetString(reader.GetOrdinal("name_omsu")),
                            csp = reader.IsDBNull(reader.GetOrdinal("csp")) 
                                ? null : reader.GetString(reader.GetOrdinal("csp")),
                            completion_date = reader.GetDateTime(reader.GetOrdinal("completion_date")),
                            answers = reader.GetString(reader.GetOrdinal("answers"))
                        });
                    }
                }
            }

            return Json(new { 
                success = true, 
                survey = survey,
                answers = answers 
            });
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Ошибка при получении ответов анкеты {SurveyId}", id);
        return Json(new { 
            success = false, 
            error = "Внутренняя ошибка сервера",
            detail = ex.Message
        });
    }
}


private List<Survey> GetSurveysForOtchet()
{
    List<Survey> surveys = new List<Survey>();
    
    using (var connection = _db.CreateConnection())
    {
        using (var command = connection.CreateCommand())
        {
            command.CommandText = @"
                SELECT 
                    s.id_survey,
                    s.name_survey, 
                    s.questions, 
                    COALESCE(
                        (SELECT string_agg(o.name_omsu, ', ') 
                         FROM public.omsu o 
                         WHERE o.list_surveys LIKE '%' || s.id_survey || '%'), 
                        'Не указано'
                    ) AS name_omsu
                FROM public.surveys s
                
                UNION ALL
                
                SELECT 
                    hs.id_survey, 
                    hs.name_survey, 
                    hs.file_questions, 
                    COALESCE(
                        (SELECT string_agg(o.name_omsu, ', ') 
                         FROM public.omsu o 
                         WHERE o.list_surveys LIKE '%' || hs.id_survey || '%'), 
                        'Не указано'
                    ) AS name_omsu
                FROM public.history_surveys hs";
            
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    surveys.Add(new Survey
                    {
                        id_survey = reader.GetInt32(0),
                        name_survey = reader.GetString(1),
                        questions = reader.GetString(2),
                        name_omsu = reader.GetString(3),
                    });
                }
            }
        }
    }
    
    return surveys;
}

[Authorize]
[HttpGet("survey_list_user/{id}")]
public IActionResult survey_list_user(int id, int? page, string searchTerm)
{
    var accessResult = EnsureUserRouteAccess(id);
    if (accessResult != null)
        return accessResult;

    try
    {
        _logger.LogInformation($"Запрос активных анкет. UserId: {id}, Page: {page}, Search: '{searchTerm}'");
        
        int pageSize = 10;
        int currentPage = page ?? 1;
        List<Survey> accessibleSurveys = new List<Survey>();
        int userOmsuId;
        int totalSurveysCount = 0;

        using (var connection = _db.CreateConnection())
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT id_omsu FROM public.users WHERE id_user = @userId";
                command.Parameters.Add(new NpgsqlParameter("@userId", id));
                userOmsuId = Convert.ToInt32(command.ExecuteScalar());
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT COUNT(*)
                        FROM (
                            -- Анкеты из surveys, к которым есть доступ у организации
                            SELECT s.id_survey
                            FROM public.surveys s
                            JOIN public.omsu o ON o.list_surveys LIKE '%' || s.id_survey::text || '%'
                            WHERE o.id_omsu = @userOmsuId
                            AND NOT EXISTS (
                                SELECT 1
                                FROM public.history_answer ha
                                WHERE ha.id_omsu = @userOmsuId
                                AND ha.id_survey = s.id_survey
                            )
                            UNION ALL
                            -- Анкеты из history_surveys, к которым продлили доступ
                            SELECT hs.id_survey
                            FROM public.history_surveys hs
                            JOIN public.access_extensions ae ON hs.id_survey = ae.id_survey
                            WHERE ae.id_omsu = @userOmsuId
                            AND ae.new_end_date > NOW()
                        ) AS combined_surveys";
                command.Parameters.Add(new NpgsqlParameter("@userOmsuId", userOmsuId));
                totalSurveysCount = Convert.ToInt32(command.ExecuteScalar());
            }
            
            using (var command = connection.CreateCommand())
            {
                string whereClause = "";
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    whereClause = "AND s.name_survey ILIKE @searchTerm";
                    command.Parameters.Add(new NpgsqlParameter("@searchTerm", $"%{searchTerm}%"));
                }

                command.CommandText = $@"
                    SELECT s.id_survey, s.name_survey, s.description, 
                           s.date_open, s.date_close, s.questions
                    FROM public.surveys s
                    JOIN public.omsu o ON o.list_surveys LIKE '%' || s.id_survey::text || '%'
                    WHERE o.id_omsu = @userOmsuId
                    AND NOT EXISTS (
                        SELECT 1 FROM public.history_answer ha
                        WHERE ha.id_omsu = @userOmsuId AND ha.id_survey = s.id_survey
                    )
                    {whereClause}
                    ORDER BY s.id_survey
                    OFFSET @offset LIMIT @pageSize";
                
                command.Parameters.Add(new NpgsqlParameter("@userOmsuId", userOmsuId));
                command.Parameters.Add(new NpgsqlParameter("@offset", (currentPage - 1) * pageSize));
                command.Parameters.Add(new NpgsqlParameter("@pageSize", pageSize));

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        accessibleSurveys.Add(new Survey
                        {
                            id_survey = reader.GetInt32(0),
                            name_survey = reader.IsDBNull(1) ? null : reader.GetString(1),
                            description = reader.IsDBNull(2) ? null : reader.GetString(2),
                            date_open = reader.GetDateTime(3),
                            date_close = reader.GetDateTime(4),
                            questions = reader.IsDBNull(5) ? null : reader.GetString(5),
                            id_omsu = userOmsuId
                        });
                    }
                }
            }
        }

        _logger.LogInformation($"Найдено {accessibleSurveys.Count} активных анкет");

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return Json(new {
                accessibleSurveys,
                currentPage,
                totalPages = (int)Math.Ceiling((double)totalSurveysCount / pageSize),
                totalCount = totalSurveysCount,
                searchTerm
            });
        }

        ViewBag.AccessibleSurveys = accessibleSurveys;
        ViewBag.UserOmsuId = userOmsuId;
        ViewBag.CurrentPage = currentPage;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalSurveysCount / pageSize);
        ViewBag.SearchTerm = searchTerm;

        return View();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Ошибка в survey_list_user");
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return StatusCode(500, new { error = "Ошибка сервера" });
        }
        throw;
    }
}

    



[Authorize]
[HttpGet("get_list_archive/{id}")]
public IActionResult GetListArchive(
    int id, 
    string searchTerm = "", 
    string dateFrom = "", 
    string dateTo = "", 
    bool signedOnly = false,
    bool countOnly = false)
{
    var accessResult = EnsureUserRouteAccess(id);
    if (accessResult != null)
        return accessResult;

    try
    {
        _logger.LogInformation($"Запрос архивных анкет. UserId: {id}, CountOnly: {countOnly}");

        using (var connection = _db.CreateConnection())
        {
            int userOmsuId;
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT id_omsu FROM public.users WHERE id_user = @userId";
                command.Parameters.Add(new NpgsqlParameter("@userId", NpgsqlTypes.NpgsqlDbType.Integer) { Value = id });
                
                var result = command.ExecuteScalar();
                if (result == null || result == DBNull.Value)
                {
                    _logger.LogWarning($"Пользователь с ID {id} не найден");
                    return NotFound(new { error = "Пользователь не найден" });
                }
                
                userOmsuId = Convert.ToInt32(result);
                _logger.LogInformation($"Найден id_omsu: {userOmsuId} для пользователя {id}");
            }

            if (countOnly)
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT COUNT(*) 
                        FROM public.surveys s
                        INNER JOIN public.history_answer ha ON s.id_survey = ha.id_survey
                        WHERE ha.id_omsu = @userOmsuId";
                    
                    command.Parameters.Add(new NpgsqlParameter("@userOmsuId", NpgsqlTypes.NpgsqlDbType.Integer) { Value = userOmsuId });
                    
                    _logger.LogInformation($"Выполняется запрос: {command.CommandText}");
                    
                    int count = Convert.ToInt32(command.ExecuteScalar());
                    _logger.LogInformation($"Найдено архивных анкет: {count}");
                    
                    return Ok(new { totalCount = count });
                }
            }

            var whereConditions = new List<string>();
            var parameters = new List<NpgsqlParameter>
            {
                new NpgsqlParameter("@userOmsuId", NpgsqlTypes.NpgsqlDbType.Integer) { Value = userOmsuId }
            };

            whereConditions.Add("ha.id_omsu = @userOmsuId");
            whereConditions.Add("o.id_omsu = @userOmsuId");

            if (!string.IsNullOrEmpty(searchTerm))
            {
                whereConditions.Add("s.name_survey ILIKE @searchTerm");
                parameters.Add(new NpgsqlParameter("@searchTerm", NpgsqlTypes.NpgsqlDbType.Text) { Value = $"%{searchTerm}%" });
            }

            if (!string.IsNullOrEmpty(dateFrom) && DateTime.TryParse(dateFrom, out var fromDate))
            {
                whereConditions.Add("ha.completion_date >= @dateFrom");
                parameters.Add(new NpgsqlParameter("@dateFrom", NpgsqlTypes.NpgsqlDbType.Timestamp) { Value = fromDate });
            }

            if (!string.IsNullOrEmpty(dateTo) && DateTime.TryParse(dateTo, out var toDate))
            {
                whereConditions.Add("ha.completion_date <= @dateTo");
                parameters.Add(new NpgsqlParameter("@dateTo", NpgsqlTypes.NpgsqlDbType.Timestamp) { Value = toDate });
            }

            if (signedOnly)
            {
                whereConditions.Add("ha.csp IS NOT NULL");
            }

            string whereClause = whereConditions.Any() ? "WHERE " + string.Join(" AND ", whereConditions) : "";

int totalCount = 0;
using (var countCommand = connection.CreateCommand())
{
    countCommand.CommandText = $@"
        SELECT COUNT(*) 
        FROM (
            -- Анкеты из surveys
            SELECT s.id_survey
            FROM public.surveys s
            INNER JOIN public.history_answer ha ON s.id_survey = ha.id_survey
            INNER JOIN public.omsu o ON o.id_omsu = ha.id_omsu
            {whereClause}
            
            UNION
            
            -- Анкеты из history_surveys
            SELECT hs.id_survey
            FROM public.history_surveys hs
            INNER JOIN public.history_answer ha ON hs.id_survey = ha.id_survey
            INNER JOIN public.omsu o ON o.id_omsu = ha.id_omsu
            WHERE ha.id_omsu = @userOmsuId AND o.id_omsu = @userOmsuId
            {(whereConditions.Any() ? " AND " + string.Join(" AND ", whereConditions.Where(c => !c.Contains("s."))) : "")}
        ) AS combined_surveys";
    
    foreach (var param in parameters)
    {
        countCommand.Parameters.Add(param);
    }
    
    totalCount = Convert.ToInt32(countCommand.ExecuteScalar());
    _logger.LogInformation($"Всего архивных анкет по фильтру (включая history_surveys): {totalCount}");
}

            List<Survey> archivedSurveys = new List<Survey>();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = $@"
SELECT 
    survey_data.id_survey,
    survey_data.name_survey,
    survey_data.description,
    survey_data.date_begin,
    survey_data.date_end,
    survey_data.date_open,
    survey_data.date_close,
    survey_data.completion_date,
    survey_data.csp,
    survey_data.source_type
FROM (
    -- Данные из history_surveys
    SELECT
        hs.id_survey,
        hs.name_survey,
        hs.description,
        hs.date_begin,
        hs.date_end,
        NULL AS date_open,
        NULL AS date_close,
        ha.completion_date,
        ha.csp,
        'history_survey' AS source_type
    FROM public.history_surveys hs
    INNER JOIN public.history_answer ha ON hs.id_survey = ha.id_survey
    INNER JOIN public.omsu o ON o.id_omsu = ha.id_omsu
    WHERE ha.id_omsu = 51 AND o.id_omsu = 51
    
    UNION ALL
    
    -- Данные из surveys
    SELECT 
        s.id_survey, 
        s.name_survey, 
        s.description, 
        NULL AS date_begin,
        NULL AS date_end,
        s.date_open, 
        s.date_close, 
        ha.completion_date,
        ha.csp,
        'survey' AS source_type
    FROM public.surveys s
    INNER JOIN public.history_answer ha ON s.id_survey = ha.id_survey
    INNER JOIN public.omsu o ON o.id_omsu = ha.id_omsu
    {whereClause}
) AS survey_data
ORDER BY survey_data.completion_date DESC
OFFSET @offset LIMIT @pageSize";
                
                foreach (var param in parameters)
                {
                    command.Parameters.Add(param);
                }
                
                command.Parameters.Add(new NpgsqlParameter("@offset", NpgsqlTypes.NpgsqlDbType.Integer) { Value = 0 });
                command.Parameters.Add(new NpgsqlParameter("@pageSize", NpgsqlTypes.NpgsqlDbType.Integer) { Value = 10 });

                _logger.LogInformation($"Выполняется запрос: {command.CommandText}");

  using (var reader = command.ExecuteReader())
{
    while (reader.Read())
    {
        archivedSurveys.Add(new Survey
        {
            id_survey = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
            name_survey = reader.IsDBNull(1) ? null : reader.GetString(1),
            description = reader.IsDBNull(2) ? null : reader.GetString(2),
            date_open = reader.IsDBNull(3) ? (reader.IsDBNull(5) ? DateTime.MinValue : reader.GetDateTime(5)) : reader.GetDateTime(3),
            date_close = reader.IsDBNull(4) ? (reader.IsDBNull(6) ? DateTime.MinValue : reader.GetDateTime(6)) : reader.GetDateTime(4),
            completion_date = reader.IsDBNull(7) ? DateTime.MinValue : reader.GetDateTime(7),
            csp = reader.IsDBNull(8) ? null : reader.GetString(8),
            id_omsu = userOmsuId
        });
    }
}
            }

            _logger.LogInformation($"Найдено архивных анкет: {archivedSurveys.Count}");

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Ok(new { 
                    accessibleSurveys = archivedSurveys,
                    currentPage = 1,
                    totalPages = (int)Math.Ceiling((double)totalCount / 10),
                    totalCount,
                    searchTerm,
                    dateFrom,
                    dateTo,
                    signedOnly
                });
            }

            ViewBag.CurrentPage = 1;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / 10);
            ViewBag.SearchTerm = searchTerm;
            ViewBag.DateFrom = dateFrom;
            ViewBag.DateTo = dateTo;
            ViewBag.SignedOnly = signedOnly;

            return View("ArchivedSurveys", archivedSurveys);
        }
    }
    catch (NpgsqlException npgEx)
    {
        _logger.LogError(npgEx, "Ошибка базы данных при получении архивных анкет");
        return StatusCode(500, new { 
            error = "Ошибка базы данных",
            details = npgEx.Message,
            innerException = npgEx.InnerException?.Message
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Неожиданная ошибка при получении архивных анкет");
        return StatusCode(500, new { 
            error = "Внутренняя ошибка сервера",
            details = ex.Message
        });
    }
}

[Authorize(Roles = "Админ")]
public IActionResult archiv_surveys()
{
    List<HistorySurvey> archivedSurveys = new List<HistorySurvey>();

    using (var connection = _db.CreateConnection())
    {
        using (var command = connection.CreateCommand())
        {
            command.CommandText = @"SELECT id_survey, date_begin, date_end, file_questions, name_survey, description FROM public.history_surveys";

            try
            {
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var archivedSurvey = new HistorySurvey
                        {
                            id_survey = reader.GetInt32(0),
                            date_begin = reader.GetDateTime(1),
                            date_end = reader.GetDateTime(2),
                            file_questions = reader.IsDBNull(3) ? null : reader.GetString(3),
                            name_survey = reader.IsDBNull(4) ? "Нет данных" : reader.GetString(4),
                            description = reader.IsDBNull(5) ? "Нет данных" : reader.GetString(5),
                        };

                        archivedSurveys.Add(archivedSurvey);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return Ok();
            }
        }
    }

    Console.WriteLine(archivedSurveys.Count());

    return View(archivedSurveys);
}

[Authorize]
public IActionResult zapolnenie_anketi(int id, int omsuId)
{
    var omsuAccessResult = EnsureOmsuAccess(omsuId);
    if (omsuAccessResult != null)
        return omsuAccessResult;

    List<Dictionary<string, object>> questions = new List<Dictionary<string, object>>();
    string? questionsJson = null;
    bool isHistorySurvey = false;

    using (var connection = _db.CreateConnection())
    {
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT questions FROM public.surveys WHERE id_survey = @id";
            command.Parameters.Add(new NpgsqlParameter("@id", NpgsqlTypes.NpgsqlDbType.Integer) { Value = id });

            using (var reader = command.ExecuteReader())
            {
                if (reader.Read())
                {
                    questionsJson = reader.GetString(0);
                    isHistorySurvey = false;
                }
            }
        }

        if (string.IsNullOrEmpty(questionsJson))
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT file_questions FROM public.history_surveys WHERE id_survey = @id";
                command.Parameters.Add(new NpgsqlParameter("@id", NpgsqlTypes.NpgsqlDbType.Integer) { Value = id });

                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        questionsJson = reader.GetString(0);
                        isHistorySurvey = true;
                    }
                }
            }
        }

        if (!string.IsNullOrEmpty(questionsJson))
        {
            if(isHistorySurvey)
            {
                var jsonDoc = JsonDocument.Parse(questionsJson);
                var questionArray = jsonDoc.RootElement.GetProperty("questions");

                foreach (var question in questionArray.EnumerateArray())
                {
                    var questionData = new Dictionary<string, object>
                    {
                        { "Id", question.GetProperty("question_id").GetInt32() },
                        { "Text", question.GetProperty("text").GetString() }
                    };
                    questions.Add(questionData);
                }
            }
            else
            {
                var jsonDoc = JsonDocument.Parse(questionsJson);
                var questionArray = jsonDoc.RootElement.GetProperty("questions");
                foreach (var question in questionArray.EnumerateArray())
                {
                    var questionData = new Dictionary<string, object>
                    {
                        { "Id", question.GetProperty("question_id").GetInt32() },
                        { "Text", question.GetProperty("text").GetString() }
                    };
                    questions.Add(questionData);
                }
            }
        }
    }

    return Json(new { questions });
}
[Authorize(Roles = "Админ")]
public IActionResult create_otchet_kvartal(int kvartal, int year)
{

        if (year == 0)
        {
            year = DateTime.Now.Year;
        }
    try
    {
        Dictionary<int, string> quarterNames = new Dictionary<int, string>
        {
            {1, "I"},
            {2, "II"},
            {3, "III"},
            {4, "IV"}
        };

        string quarterName = quarterNames.ContainsKey(kvartal) 
            ? quarterNames[kvartal] 
            : $"{kvartal} квартал";

        // Используем переданный год, а не текущий!
        Console.WriteLine($"Формирование отчета за {quarterName} {year} г.");

        List<HistoryAnswer> answers = GetAnswersFromDatabase();
        List<Survey> surveys = GetSurveysForOtchet();

        if (answers == null || surveys == null)
        {
            return View("Error", new ErrorViewModel { Message = "Ошибка при получении данных из базы данных." });
        }

        using (var workbook = new XLWorkbook())
        {
            foreach (var survey in surveys)
            {
                var surveyAnswers = answers.Where(a => a.id_survey == survey.id_survey).ToList();
                if (surveyAnswers.Count == 0) continue;

                string sheetName = survey.name_survey ?? "Опрос";
                sheetName = new string(sheetName
                    .Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || c == '-' || c == '_')
                    .Take(31)
                    .ToArray());

                var worksheet = workbook.Worksheets.Add(sheetName);
                var questionsJson = survey.questions;
                var questions = !string.IsNullOrEmpty(questionsJson) ? 
                    JsonConvert.DeserializeObject<SurveyQuestions>(questionsJson)?.questions : 
                    null;

                if (questions == null || questions.Length == 0) continue;

                BuildWorksheetHeaders(worksheet, questions);

                int currentRow = 3;
                var months = GetMonthsForQuarter(kvartal);
                List<double> orgAverages = new List<double>();
                Dictionary<int, List<double>> questionRatings = new Dictionary<int, List<double>>();

                for (int i = 0; i < questions.Length; i++)
                {
                    questionRatings[i] = new List<double>();
                }

                foreach (var month in months)
                {
                    string monthHeader = $"{month.Name} {year} г.";
                    worksheet.Cell(currentRow, 1).Value = monthHeader;
                    worksheet.Range(currentRow, 1, currentRow, 2 + questions.Length * 2 + 1).Merge();
                    worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    currentRow++;

                    var monthAnswers = surveyAnswers
                        .Where(a => a.create_date_survey?.Month == month.Number && 
                                   a.create_date_survey?.Year == year)
                        .GroupBy(a => a.name_omsu)
                        .OrderBy(g => g.Key);

                    foreach (var orgGroup in monthAnswers)
                    {
                        worksheet.Cell(currentRow, 1).Value = orgGroup.Key;
                        worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                        var answersJson = orgGroup.First().answers;
                        var answersData = !string.IsNullOrEmpty(answersJson) ? 
                            JsonConvert.DeserializeObject<List<AnswerData>>(answersJson) : 
                            new List<AnswerData>();

                        List<double> orgRatings = new List<double>();

                        for (int i = 0; i < questions.Length; i++)
                        {
                            string questionId = questions[i].question_id.ToString();
                            var answer = answersData?.FirstOrDefault(a => a.question_id == questionId);
                            
                            if (answer?.rating.HasValue == true)
                            {
                                double rating = answer.rating.Value;
                                worksheet.Cell(currentRow, 2 + i).Value = rating;
                                orgRatings.Add(rating);
                                questionRatings[i].Add(rating);
                            }
                            else
                            {
                                worksheet.Cell(currentRow, 2 + i).Value = string.Empty;
                            }
                        }

                        if (orgRatings.Count > 0)
                        {
                            double orgAvg = orgRatings.Average();
                            worksheet.Cell(currentRow, 2 + questions.Length).Value = orgAvg;
                            orgAverages.Add(orgAvg);
                        }
                        else
                        {
                            worksheet.Cell(currentRow, 2 + questions.Length).Value = string.Empty;
                        }

                        for (int i = 0; i < questions.Length; i++)
                        {
                            string questionId = questions[i].question_id.ToString();
                            var answer = answersData?.FirstOrDefault(a => a.question_id == questionId);
                            worksheet.Cell(currentRow, 2 + questions.Length + 1 + i).Value = answer?.comment ?? string.Empty;
                        }

                        worksheet.Row(currentRow).AdjustToContents();
                        currentRow++;
                    }

                    if (!monthAnswers.Any())
                    {
                        worksheet.Cell(currentRow, 1).Value = "Нет данных";
                        worksheet.Range(currentRow, 1, currentRow, 2 + questions.Length * 2 + 1).Merge();
                        currentRow++;
                    }
                }

                if (currentRow > 3)
                {
                    worksheet.Cell(currentRow, 1).Value = "Итого:";
                    worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                    for (int i = 0; i < questions.Length; i++)
                    {
                        if (questionRatings[i].Count > 0)
                        {
                            worksheet.Cell(currentRow, 2 + i).Value = questionRatings[i].Average();
                        }
                        else
                        {
                            worksheet.Cell(currentRow, 2 + i).Value = string.Empty;
                        }
                    }

                    if (questionRatings.Any(q => q.Value.Count > 0))
                    {
                        worksheet.Cell(currentRow, 2 + questions.Length).Value = 
                            questionRatings.Where(q => q.Value.Count > 0).Average(q => q.Value.Average());
                    }
                    currentRow++;

                    worksheet.Cell(currentRow, 1).Value = "Всего среднее";
                    worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                    if (orgAverages.Count > 0)
                    {
                        worksheet.Cell(currentRow, 2 + questions.Length).Value = orgAverages.Average();
                    }
                }

                FormatWorksheet(worksheet, questions.Length);
            }

            string safeQuarterName = string.Join("_", quarterName.Split(Path.GetInvalidFileNameChars()));
            // В имени файла оставим и выбранный год, и дату генерации
            string fileName = $"Otchet_za_{safeQuarterName}_kvartal_{year}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            
            Directory.CreateDirectory(_downloadsPath);
            string filePath = Path.Combine(_downloadsPath, fileName);
            
            workbook.SaveAs(filePath);
            
            var fileBytes = System.IO.File.ReadAllBytes(filePath);
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, $"Ошибка при формировании отчета за {kvartal} квартал {year}");
        return StatusCode(500, "Произошла ошибка при формировании отчета");
    }
}


private void BuildWorksheetHeaders(IXLWorksheet worksheet, Question[] questions)
{
    var orgHeader = worksheet.Cell(1, 1);
    orgHeader.Value = "Наименование организации";
    orgHeader.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    worksheet.Range(1, 1, 2, 1).Merge();

    var criteriaHeader = worksheet.Cell(1, 2);
    criteriaHeader.Value = "Название критериев";
    criteriaHeader.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    worksheet.Range(1, 2, 1, 1 + questions.Length).Merge();

    for (int i = 0; i < questions.Length; i++)
    {
        var cell = worksheet.Cell(2, 2 + i);
        cell.Value = questions[i].text;
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    var avgHeader = worksheet.Cell(1, 2 + questions.Length);
    avgHeader.Value = "Средний балл";
    avgHeader.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    worksheet.Range(1, 2 + questions.Length, 2, 2 + questions.Length).Merge();

    var commentsHeader = worksheet.Cell(1, 2 + questions.Length + 1);
    commentsHeader.Value = "Комментарии";
    commentsHeader.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    worksheet.Range(1, 2 + questions.Length + 1, 1, 1 + questions.Length * 2 + 1).Merge();

    for (int i = 0; i < questions.Length; i++)
    {
        var cell = worksheet.Cell(2, 2 + questions.Length + 1 + i);
        cell.Value = $"Комментарий к {questions[i].text}";
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }
}

private List<(string Name, int Number)> GetMonthsForQuarter(int quarter)
{
    switch (quarter)
    {
        case 1: return new List<(string, int)> { ("Январь", 1), ("Февраль", 2), ("Март", 3) };
        case 2: return new List<(string, int)> { ("Апрель", 4), ("Май", 5), ("Июнь", 6) };
        case 3: return new List<(string, int)> { ("Июль", 7), ("Август", 8), ("Сентябрь", 9) };
        case 4: return new List<(string, int)> { ("Октябрь", 10), ("Ноябрь", 11), ("Декабрь", 12) };
        default: return new List<(string, int)>();
    }
}


private void FormatWorksheet(IXLWorksheet worksheet, int questionsCount)
{
    var usedRange = worksheet.RangeUsed();
    usedRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    usedRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    
    foreach (var cell in usedRange.CellsUsed())
    {
        if (cell.DataType == XLDataType.Number || 
            (cell.DataType == XLDataType.Text && 
             !string.IsNullOrEmpty(cell.GetString()) && 
             double.TryParse(cell.GetString(), out _)))
        {
            cell.Style.NumberFormat.Format = "0.00";
        }
    }

    worksheet.Column(1).Width = 30;
    
    for (int col = 2; col <= 1 + questionsCount; col++)
    {
        worksheet.Column(col).Width = 20;
    }
    
    worksheet.Column(2 + questionsCount).Width = 15;
    

    for (int col = 2 + questionsCount + 1; col <= 1 + questionsCount * 2 + 1; col++)
    {
        worksheet.Column(col).Width = 25;
    }
    
    usedRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
    usedRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
    
    worksheet.Rows(1, 2).AdjustToContents();
    
    for (int row = 3; row <= worksheet.LastRowUsed().RowNumber(); row++)
    {
        var cell = worksheet.Cell(row, 1);
        if (cell.Value.ToString() != "Нет данных")
        {
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        }
    }
}

    private void WriteToCell(IXLWorksheet worksheet, int row, int column, string value)
    {
        worksheet.Cell(row, column).Value = value;
    }

   protected List<HistoryAnswer> GetAnswersFromDatabase()
{
    List<HistoryAnswer> answers = new List<HistoryAnswer>();
    using (var connection = _db.CreateConnection())
    {
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT id_omsu, (SELECT name_omsu FROM public.omsu WHERE omsu.id_omsu = history_answer.id_omsu) AS name_omsu, " +
               "csp, id_answer, id_survey, (SELECT name_survey FROM public.surveys WHERE surveys.id_survey = history_answer.id_survey) AS name_survey, " +
                "completion_date, create_date_survey, answers FROM public.history_answer";
            try
            {
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
                            create_date_survey = reader.GetDateTime(7),
                            answers = reader.GetString(8),
                        };
                        answers.Add(answer);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении ответов из базы данных");
                return null;
            }
        }
    }
    return answers;
}




public class SurveyQuestions
{
    public Question[]? questions { get; set; }
    public int survey_id { get; set; }
}

public class Question
{
    public string? text { get; set; }
    public int question_id { get; set; }
}

public class AnswerData
{
    public int? rating { get; set; }
    public string? comment { get; set; }
    public string? question_id { get; set; }
}

[Authorize(Roles = "Админ")]
[HttpPost("/copy_archive_survey")]
public async Task<IActionResult> copy_archive_survey()
{
    Console.WriteLine("Начало обработки запроса на копирование анкеты.");

    using var reader = new StreamReader(Request.Body);
    var body = await reader.ReadToEndAsync();
    Console.WriteLine("Тело запроса: " + body);

    // Десериализуем в словарь для извлечения дат как строк
    var jsonDoc = JsonDocument.Parse(body);
    var root = jsonDoc.RootElement;

    // Получаем поля из JSON
    string? nameSurvey = root.GetProperty("name_survey").GetString();
    string? description = root.TryGetProperty("description", out var descProp) ? descProp.GetString() : null;
    string? questions = root.TryGetProperty("questions", out var quesProp) ? quesProp.GetString() : null;

    string? dateOpenStr = root.TryGetProperty("date_open", out var dateOpenProp) ? dateOpenProp.GetString() : null;
    string? dateCloseStr = root.TryGetProperty("date_close", out var dateCloseProp) ? dateCloseProp.GetString() : null;

    string? nameOmsu = root.TryGetProperty("name_omsu", out var nameOmsuProp) ? nameOmsuProp.GetString() : null;
    int idOmsu = root.TryGetProperty("id_omsu", out var idOmsuProp) && idOmsuProp.TryGetInt32(out var idOmsuVal) ? idOmsuVal : 0;
    string? csp = root.TryGetProperty("csp", out var cspProp) ? cspProp.GetString() : null;
    int idAnswer = root.TryGetProperty("id_answer", out var idAnswerProp) && idAnswerProp.TryGetInt32(out var idAnswerVal) ? idAnswerVal : 0;

    // Функция парсинга дат
    DateTime ParseDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return default;

        var formats = new[] { "dd.MM.yyyy H:mm:ss", "dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy" };
        if (DateTime.TryParseExact(s, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dt))
            return dt;

        throw new FormatException($"Неверный формат даты: {s}");
    }

    DateTime dateOpen, dateClose;
    try
    {
        dateOpen = ParseDate(dateOpenStr);
        dateClose = ParseDate(dateCloseStr);
    }
    catch (FormatException ex)
    {
        Console.WriteLine(ex.Message);
        return BadRequest(ex.Message);
    }

    if (string.IsNullOrEmpty(nameSurvey))
    {
        return BadRequest("Название анкеты обязательно.");
    }

    // Создаем объект Survey
    var survey = new Survey
    {
        name_survey = nameSurvey,
        description = description,
        questions = questions,
        date_create = DateTime.Now,
        date_open = dateOpen,
        date_close = dateClose,
        name_omsu = nameOmsu,
        id_omsu = idOmsu,
        csp = csp,
        id_answer = idAnswer,
        completion_date = default,
        date_begin = default,
        date_end = default,
        Answers = new List<HistoryAnswer>()
    };

    var connectionString = _configuration.GetConnectionString("DefaultConnection");

    try
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var command = new NpgsqlCommand(@"
            INSERT INTO surveys 
            (name_survey, description, date_create, date_open, date_close, questions)
            VALUES 
            (@name_survey, @description, @date_create, @date_open, @date_close, @questions::jsonb)
            RETURNING id_survey;
        ", connection);

        command.Parameters.AddWithValue("@name_survey", survey.name_survey);
        command.Parameters.AddWithValue("@description", survey.description ?? string.Empty);
        command.Parameters.AddWithValue("@date_create", survey.date_create);
        command.Parameters.AddWithValue("@date_open", survey.date_open == default ? DBNull.Value : survey.date_open.Date);
        command.Parameters.AddWithValue("@date_close", survey.date_close == default ? DBNull.Value : survey.date_close.Date);
        command.Parameters.AddWithValue("@questions", survey.questions ?? "{}");

        var id = await command.ExecuteScalarAsync();
        Console.WriteLine($"Анкета успешно добавлена с ID = {id}");

        return Ok(new { message = "Анкета успешно добавлена", id });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Ошибка при добавлении анкеты: {ex}");
        return StatusCode(500, $"Ошибка при добавлении анкеты: {ex.Message}");
    }
}





}
