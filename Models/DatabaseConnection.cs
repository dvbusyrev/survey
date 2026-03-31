using System.Data;
using Npgsql;
using Microsoft.Extensions.Configuration;

namespace main_project.Models
{
    public class DatabaseConnection
    {
        private readonly string _connectionString;

        public DatabaseConnection(IConfiguration configuration)
        {
            // Получаем строку подключения из конфигурации
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(_connectionString))
            {
                Console.WriteLine(" >> Подключение к базе данных не установлено.");
                throw new ArgumentException(" >> Подключение к базе данных не установлено.");
            }
            else
            {
                Console.WriteLine(" >> Подключение к базе данных успешно установлено ТЕСТ6.");
            }
        }

        public IDbConnection CreateConnection()
        {
            try
            {
                var connection = new NpgsqlConnection(_connectionString);
                connection.Open();
                return connection;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при создании подключения: {ex.Message}");
                throw;
            }
        }

        public void CloseConnection(IDbConnection connection)
        {
            try
            {
                if (connection != null && connection.State == ConnectionState.Open)
                {
                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при закрытии подключения: {ex.Message}");
                throw;
            }
        }
    }
}