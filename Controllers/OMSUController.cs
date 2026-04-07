using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using main_project.Infrastructure.Database;
using main_project.Infrastructure.Security;
using main_project.Models;
using Newtonsoft.Json;
using Npgsql;
using NpgsqlTypes;

[Authorize(Roles = AppRoles.Admin)]
public class OMSUController : Controller
{
    private readonly IDbConnectionFactory _connectionFactory;

    public OMSUController(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

public IActionResult get_omsu(string variantType)
{
    if (variantType == "data")
    {
        var organizations = new List<object>();

        using (var connection = _connectionFactory.CreateConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT id_omsu, name_omsu FROM public.omsu WHERE block = false";

            try
            {
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        organizations.Add(new 
                        {
                            id = reader.GetInt32(0),
                            name = reader.GetString(1)
                        });
                    }
                }
                return Json(organizations);
            }
            catch (Exception ex)
            {
                return Json(new { error = $"Ошибка при получении списка организаций: {ex.Message}" });
            }
        }
    }
    else
    {
        List<OMSU> omsus = new List<OMSU>();
        List<Survey> surveys = new List<Survey>();

        using (var connection = _connectionFactory.CreateConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT " +
                                  "o.id_omsu, " +
                                  "o.name_omsu, " +
                                  "o.date_begin, " +
                                  "o.date_end, " +
                                  "COALESCE((SELECT array_agg(s.name_survey ORDER BY s.name_survey) " +
                                  "FROM public.omsu_surveys os " +
                                  "INNER JOIN public.surveys s ON s.id_survey = os.id_survey " +
                                  "WHERE os.id_omsu = o.id_omsu), ARRAY[]::text[]) AS survey_names, " +
                                  "o.block, " +
                                  "o.email " +
                                  "FROM " +
                                  "public.omsu o WHERE block = false;";

            try
            {
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var surveyNames = reader.IsDBNull(4)
                            ? Array.Empty<string>()
                            : (reader.GetValue(4) as string[] ?? Array.Empty<string>());
                        var omsu = new OMSU
                        {
                            id_omsu = reader.GetInt32(0),
                            name_omsu = reader.GetString(1),
                            date_begin = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2),
                            date_end = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3),
                            survey_names = surveyNames.Length == 0
                                ? "Не указано"
                                : string.Join(", ", surveyNames),
                            block = reader.GetBoolean(5),
                            email = reader.IsDBNull(6) ? null : reader.GetString(6),
                        };
                        omsus.Add(omsu);
                    }
                }
            }
            catch (Exception ex)
            {
                return View("Error", new ErrorViewModel { Message = $"Ошибка при получении списка организаций: {ex.Message}" });
            }

            using (var command2 = connection.CreateCommand())
            {
                command2.CommandText = "SELECT id_survey, name_survey FROM public.surveys;";

                try
                {
                    using (var reader = command2.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var survey = new Survey
                            {
                                id_survey = reader.GetInt32(0),
                                name_survey = reader.GetString(1),
                                Questions = new List<main_project.Services.Surveys.SurveyQuestionItem>(),
                                date_close = DateTime.Now,
                                date_create = DateTime.Now,
                                date_open = DateTime.Now
                            };
                            surveys.Add(survey);
                        }
                    }
                }
                catch (Exception ex)
                {
                    return View("Error", new ErrorViewModel { Message = $"Ошибка при получении списка анкет: {ex.Message}" });
                }
            }
        }

        ViewBag.Omsus = omsus;
        ViewBag.Surveys = surveys;

        return View();
    }
}

[HttpPost]
    public IActionResult delete_omsu(int id)
    {

        using (var connection = _connectionFactory.CreateConnection())
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "UPDATE public.omsu SET block = true WHERE id_omsu = @id";
                command.Parameters.Add(new NpgsqlParameter("@id", NpgsqlTypes.NpgsqlDbType.Integer) { Value = id });

                int rowsAffected = command.ExecuteNonQuery();
                if (rowsAffected == 0)
                {
                    return BadRequest("Произошла ошибка при удалении организации.");
                }
            }
        }

        return Ok("Организация успешно удалена.");
    }

    public IActionResult UpdateListSurveys(int omsuId, int surveyId)
    {
        using (var connection = _connectionFactory.CreateConnection())
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    INSERT INTO public.omsu_surveys (id_omsu, id_survey)
                    VALUES (@omsuId, @surveyId)
                    ON CONFLICT (id_omsu, id_survey) DO NOTHING";
                command.Parameters.Add(new NpgsqlParameter("@surveyId", NpgsqlTypes.NpgsqlDbType.Integer) { Value = surveyId });
                command.Parameters.Add(new NpgsqlParameter("@omsuId", NpgsqlTypes.NpgsqlDbType.Integer) { Value = omsuId });

                try
                {
                    command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    return BadRequest($"Ошибка при обновлении списка анкет: {ex.Message}");
                }
            }
        }

        return Ok("Список анкет успешно обновлен.");
    }

    public IActionResult add_omsu()
    {
        return View();
    }


public IActionResult archive_list_omsus(string variantType)
{
    if (variantType == "data")
    {
        var organizations = new List<object>();

        using (var connection = _connectionFactory.CreateConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT id_omsu, name_omsu FROM public.omsu WHERE block = false";

            try
            {
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        organizations.Add(new 
                        {
                            id = reader.GetInt32(0),
                            name = reader.GetString(1)
                        });
                    }
                }
                return Json(organizations);
            }
            catch (Exception ex)
            {
                return Json(new { error = $"Ошибка при получении списка организаций: {ex.Message}" });
            }
        }
    }
    else
    {
        List<OMSU> omsus = new List<OMSU>();
        List<Survey> surveys = new List<Survey>();

        using (var connection = _connectionFactory.CreateConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT " +
                                  "o.id_omsu, " +
                                  "o.name_omsu, " +
                                  "o.date_begin, " +
                                  "o.date_end, " +
                                  "COALESCE((SELECT array_agg(s.name_survey ORDER BY s.name_survey) " +
                                  "FROM public.omsu_surveys os " +
                                  "INNER JOIN public.surveys s ON s.id_survey = os.id_survey " +
                                  "WHERE os.id_omsu = o.id_omsu), ARRAY[]::text[]) AS survey_names, " +
                                  "o.block, " +
                                  "o.email " +
                                  "FROM " +
                                  "public.omsu o WHERE block = true;";

            try
            {
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var surveyNames = reader.IsDBNull(4)
                            ? Array.Empty<string>()
                            : (reader.GetValue(4) as string[] ?? Array.Empty<string>());
                        var omsu = new OMSU
                        {
                            id_omsu = reader.GetInt32(0),
                            name_omsu = reader.GetString(1),
                            date_begin = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2),
                            date_end = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3),
                            survey_names = surveyNames.Length == 0
                                ? "Не указано"
                                : string.Join(", ", surveyNames),
                            block = reader.GetBoolean(5),
                            email = reader.IsDBNull(6) ? null : reader.GetString(6),
                        };
                        omsus.Add(omsu);
                    }
                }
            }
            catch (Exception ex)
            {
                return View("Error", new ErrorViewModel { Message = $"Ошибка при получении списка организаций: {ex.Message}" });
            }

            using (var command2 = connection.CreateCommand())
            {
                command2.CommandText = "SELECT id_survey, name_survey FROM public.surveys;";

                try
                {
                    using (var reader = command2.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var survey = new Survey
                            {
                                id_survey = reader.GetInt32(0),
                                name_survey = reader.GetString(1),
                                Questions = new List<main_project.Services.Surveys.SurveyQuestionItem>(),
                                date_close = DateTime.Now,
                                date_create = DateTime.Now,
                                date_open = DateTime.Now
                            };
                            surveys.Add(survey);
                        }
                    }
                }
                catch (Exception ex)
                {
                    return View("Error", new ErrorViewModel { Message = $"Ошибка при получении списка анкет: {ex.Message}" });
                }
            }
        }

        ViewBag.Omsus = omsus;
        ViewBag.Surveys = surveys;

        return View();
    }
}


[HttpPost]
[ValidateAntiForgeryToken]
public IActionResult add_omsu_bd([FromBody] Dictionary<string, string> formData)
{
    try
    {
        Console.WriteLine("====== ПОЛУЧЕННЫЕ ДАННЫЕ ======");
        Console.WriteLine($"Название: {formData["Name"]}");
        Console.WriteLine($"Email: {formData["Email"]}");
        Console.WriteLine($"Дата начала: {formData["DateBegin"]}");
        Console.WriteLine($"Дата окончания: {formData["DateEnd"]}");
        Console.WriteLine("==============================");

        using (var connection = _connectionFactory.CreateConnection())
        {
            
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT INTO public.omsu 
                    (name_omsu, email, date_begin, date_end, block) 
                    VALUES (@name_omsu, @email, @date_begin, @date_end, false)";

cmd.Parameters.Add(new NpgsqlParameter("@name_omsu", NpgsqlDbType.Text) { 
    Value = formData["Name"] 
});
cmd.Parameters.Add(new NpgsqlParameter("@email", NpgsqlDbType.Text) { 
    Value = string.IsNullOrEmpty(formData["Email"]) ? DBNull.Value : (object)formData["Email"]
});

cmd.Parameters.Add(new NpgsqlParameter("@date_begin", NpgsqlDbType.Date) { 
    Value = string.IsNullOrEmpty(formData["DateBegin"]) ? DBNull.Value : 
            (object)DateTime.Parse(formData["DateBegin"])
});

cmd.Parameters.Add(new NpgsqlParameter("@date_end", NpgsqlDbType.Date) { 
    Value = string.IsNullOrEmpty(formData["DateEnd"]) ? DBNull.Value : 
            (object)DateTime.Parse(formData["DateEnd"])
});
                var newId = cmd.ExecuteScalar();

                return Json(new 
                {
                    success = true,
                    message = "Пользователь успешно добавлен",
                    userId = newId,
                    shouldReload = true
                });
            }
        }
    }
    catch (NpgsqlException dbEx)
    {
        Console.WriteLine($"❌ Ошибка БД: {dbEx.Message}");
        return StatusCode(500, new 
        {
            success = false,
            error = "Ошибка базы данных",
            details = dbEx.Message
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Ошибка: {ex.Message}");
        return StatusCode(500, new 
        {
            success = false,
            error = ex.Message
        });
    }
}

public class OrganizationModel
{
    public string Name { get; set; }
    public string Email { get; set; }
    public DateTime DateBegin { get; set; }
    public DateTime DateEnd { get; set; }
}

public IActionResult update_omsu(int id)
{
    OMSU omsu = null;

    using (var connection = _connectionFactory.CreateConnection())
    {
        using (var command = connection.CreateCommand())
        {
            command.CommandText = @"
                SELECT name_omsu, email, date_begin, date_end, id_omsu
                FROM public.omsu 
                WHERE id_omsu = @id";

            command.Parameters.Add(new NpgsqlParameter("@id", NpgsqlTypes.NpgsqlDbType.Integer) { Value = id });

            try
            {
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        omsu = new OMSU
                        {
                            name_omsu = reader.GetString(0),
                            email = reader.IsDBNull(1) ? null : reader.GetString(1),
                            date_begin = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2),
                            date_end = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3),
                            id_omsu = reader.GetInt32(4)
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                return View("Error", new ErrorViewModel { Message = $"Ошибка при получении данных организации: {ex.Message}" });
            }
        }
    }

    if (omsu == null)
    {
        return NotFound("Организация не найдена.");
    }

    return View(omsu);
}

[HttpPost("update_omsu_bd/{id}")]
public IActionResult update_omsu_bd(int id, [FromBody] string[] dataOmsu)
{
    if (dataOmsu == null || dataOmsu.Length != 4)
    {
        return BadRequest("Некорректные данные организации.");
    }

    string name = dataOmsu[0];
    string email = dataOmsu[1];
    string dateBeginStr = dataOmsu[2];
    string dateEndStr = dataOmsu[3];

    if (string.IsNullOrWhiteSpace(name))
    {
        return BadRequest("Название организации обязательно для заполнения.");
    }

    DateTime? dateBegin = null;
    DateTime? dateEnd = null;

    if (!string.IsNullOrWhiteSpace(dateBeginStr))
    {
        if (!DateTime.TryParse(dateBeginStr, out DateTime parsedDateBegin))
        {
            return BadRequest("Некорректный формат даты начала.");
        }
        dateBegin = parsedDateBegin;
    }

    if (!string.IsNullOrWhiteSpace(dateEndStr))
    {
        if (!DateTime.TryParse(dateEndStr, out DateTime parsedDateEnd))
        {
            return BadRequest("Некорректный формат даты окончания.");
        }
        dateEnd = parsedDateEnd;
    }

    if (dateBegin.HasValue && dateEnd.HasValue && dateEnd < dateBegin)
    {
        return BadRequest("Дата окончания не может быть раньше даты начала.");
    }

    try
    {
        using (var connection = _connectionFactory.CreateConnection())
        {
            var command = new NpgsqlCommand(@"
                UPDATE public.omsu 
                SET name_omsu = @name, 
                    email = @email, 
                    date_begin = @dateBegin, 
                    date_end = @dateEnd 
                WHERE id_omsu = @id", 
                (NpgsqlConnection)connection);

            command.Parameters.AddWithValue("@name", name);
            command.Parameters.AddWithValue("@email", string.IsNullOrEmpty(email) ? DBNull.Value : (object)email);
            command.Parameters.AddWithValue("@dateBegin", dateBegin ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@dateEnd", dateEnd ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@id", id);

            int rowsAffected = command.ExecuteNonQuery();
            
            if (rowsAffected == 0)
            {
                return NotFound("Организация не найдена.");
            }
        }

        return Ok("Организация успешно обновлена");
    }
    catch (Exception ex)
    {
        return StatusCode(500, $"Ошибка при обновлении организации: {ex.Message}");
    }
}
}
