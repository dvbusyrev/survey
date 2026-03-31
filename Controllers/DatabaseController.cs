using Microsoft.AspNetCore.Mvc;
using main_project.Models;
using System.Data;

public class DatabaseController : Controller
{
    private readonly DatabaseConnection _databaseConnection;

    public DatabaseController(IConfiguration configuration)
    {
        _databaseConnection = new DatabaseConnection(configuration);
    }

    public IDbCommand CreateCommand()
    {
        var connection = _databaseConnection.CreateConnection();
        return connection.CreateCommand();
    }

    public IDbConnection CreateConnection()
    {
        return _databaseConnection.CreateConnection();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            var connection = _databaseConnection.CreateConnection();
            _databaseConnection.CloseConnection(connection);
        }
        base.Dispose(disposing);
    }
    

    
}