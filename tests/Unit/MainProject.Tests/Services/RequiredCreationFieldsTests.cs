using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Application.UseCases.Admin;
using MainProject.Application.UseCases.Surveys;
using MainProject.Infrastructure.Persistence;
using Npgsql;

namespace MainProject.Tests.Services;

public sealed class RequiredCreationFieldsTests
{
    private readonly IDbConnectionFactory _connectionFactory = new UnexpectedConnectionFactory();
    private readonly IClock _clock = new FixedClock();

    [Fact]
    public async Task CreateUser_RejectsMissingRole()
    {
        var service = new UserManagementService(_connectionFactory, _clock);

        var result = await service.CreateUserAsync(CreateUserRequest(role: string.Empty));

        Assert.False(result.Success);
        Assert.Equal("Роль обязательна.", result.Message);
    }

    [Fact]
    public async Task CreateUser_RejectsMissingStartDate()
    {
        var service = new UserManagementService(_connectionFactory, _clock);

        var result = await service.CreateUserAsync(CreateUserRequest(dateBegin: null));

        Assert.False(result.Success);
        Assert.Equal("Дата начала обязательна.", result.Message);
    }

    [Fact]
    public async Task CreateUser_RejectsMissingOrganization()
    {
        var service = new UserManagementService(_connectionFactory, _clock);

        var result = await service.CreateUserAsync(CreateUserRequest(organizationId: string.Empty));

        Assert.False(result.Success);
        Assert.Equal("Не указана корректная организация.", result.Message);
    }

    [Fact]
    public async Task UpdateUser_RejectsMissingOrganization()
    {
        var service = new UserManagementService(_connectionFactory, _clock);

        var result = await service.UpdateUserAsync(1, new UserUpdateRequest
        {
            Username = "test-user",
            OrganizationId = string.Empty,
            Role = "user"
        });

        Assert.False(result.Success);
        Assert.Equal("Не указана корректная организация.", result.Message);
    }

    [Fact]
    public async Task UpdateUser_RejectsMissingStartDate()
    {
        var service = new UserManagementService(_connectionFactory, _clock);

        var result = await service.UpdateUserAsync(1, new UserUpdateRequest
        {
            Username = "test-user",
            FullName = "Тестовый пользователь",
            OrganizationId = "1",
            Role = "user",
            DateBegin = null
        });

        Assert.False(result.Success);
        Assert.Equal("Дата начала обязательна.", result.Message);
    }

    [Fact]
    public async Task CreateUser_RejectsPastEndDate()
    {
        var service = new UserManagementService(_connectionFactory, _clock);

        var result = await service.CreateUserAsync(CreateUserRequest(
            dateBegin: "2026-08-02",
            dateEnd: "2026-08-03"));

        Assert.False(result.Success);
        Assert.Equal("Дата конца не может быть раньше сегодняшней даты.", result.Message);
    }

    [Fact]
    public async Task UpdateUser_RejectsPastEndDate()
    {
        var service = new UserManagementService(_connectionFactory, _clock);

        var result = await service.UpdateUserAsync(1, new UserUpdateRequest
        {
            Username = "test-user",
            FullName = "Тестовый пользователь",
            OrganizationId = "1",
            Role = "user",
            DateBegin = "2026-08-02",
            DateEnd = "2026-08-03"
        });

        Assert.False(result.Success);
        Assert.Equal("Дата конца не может быть раньше сегодняшней даты.", result.Message);
    }

    [Fact]
    public async Task CreateSurvey_RejectsMissingOrganizations()
    {
        var service = CreateSurveyService();

        var result = await service.CreateSurveyAsync(new SurveyAddRequest
        {
            Title = "Анкета",
            StartDate = "2026-08-04",
            EndDate = "2026-08-05",
            Organizations = [],
            Criteria = ["Критерий"]
        });

        Assert.False(result.Success);
        Assert.Equal("Выберите хотя бы одну организацию", result.Message);
    }

    [Fact]
    public async Task CreateSurvey_RejectsPastEndDate()
    {
        var service = CreateSurveyService();

        var result = await service.CreateSurveyAsync(new SurveyAddRequest
        {
            Title = "Анкета",
            StartDate = "2026-08-02",
            EndDate = "2026-08-03",
            Organizations = [1],
            Criteria = ["Критерий"]
        });

        Assert.False(result.Success);
        Assert.Equal("Дата конца не может быть раньше сегодняшней даты.", result.Message);
    }

    [Fact]
    public async Task CopySurvey_RejectsPastEndDate()
    {
        var service = CreateSurveyService();

        var result = await service.CopySurveyAsync(1, new SurveyCopyRequest
        {
            StartDate = "2026-08-02",
            EndDate = "2026-08-03"
        });

        Assert.False(result.Success);
        Assert.Equal("Дата конца не может быть раньше сегодняшней даты.", result.Message);
    }

    [Fact]
    public async Task UpdateSurvey_RejectsMissingOrganizations()
    {
        var service = CreateSurveyService();

        var result = await service.UpdateSurveyAsync(1, new SurveyUpdateRequest
        {
            Title = "Анкета",
            StartDate = new DateTime(2026, 8, 4),
            EndDate = new DateTime(2026, 8, 5),
            Organizations = [],
            Criteria = ["Критерий"]
        });

        Assert.False(result.Success);
        Assert.Equal("Выберите хотя бы одну организацию", result.Message);
    }

    [Fact]
    public async Task UpdateSurvey_RejectsPastEndDate()
    {
        var service = CreateSurveyService();

        var result = await service.UpdateSurveyAsync(1, new SurveyUpdateRequest
        {
            Title = "Анкета",
            StartDate = new DateTime(2026, 8, 2),
            EndDate = new DateTime(2026, 8, 3),
            Organizations = [1],
            Criteria = ["Критерий"]
        });

        Assert.False(result.Success);
        Assert.Equal("Дата конца не может быть раньше сегодняшней даты.", result.Message);
    }

    [Fact]
    public async Task ExtendSurvey_RejectsMissingOrganizations()
    {
        var service = CreateSurveyService();

        var result = await service.SaveExtensionsAsync(new SurveyExtensionRequest
        {
            SurveyId = 1,
            Extensions = []
        });

        Assert.False(result.Success);
        Assert.Equal("Необходимо предоставить данные для продления", result.Message);
    }

    [Fact]
    public async Task CreateOrganization_RejectsMissingStartDate()
    {
        var service = new OrganizationManagementService(_connectionFactory, _clock);

        var result = await service.CreateOrganizationAsync(new OrganizationSaveRequest
        {
            Name = "Организация",
            DateBegin = null
        });

        Assert.False(result.Success);
        Assert.Equal("Дата начала обязательна.", result.Message);
    }

    [Fact]
    public async Task UpdateOrganization_RejectsMissingStartDate()
    {
        var service = new OrganizationManagementService(_connectionFactory, _clock);

        var result = await service.UpdateOrganizationAsync(1, new OrganizationSaveRequest
        {
            Name = "Организация",
            DateBegin = null
        });

        Assert.False(result.Success);
        Assert.Equal("Дата начала обязательна.", result.Message);
    }

    [Fact]
    public async Task CreateOrganization_RejectsPastEndDate()
    {
        var service = new OrganizationManagementService(_connectionFactory, _clock);

        var result = await service.CreateOrganizationAsync(new OrganizationSaveRequest
        {
            Name = "Организация",
            DateBegin = "2026-08-02",
            DateEnd = "2026-08-03"
        });

        Assert.False(result.Success);
        Assert.Equal("Дата конца не может быть раньше сегодняшней даты.", result.Message);
    }

    [Fact]
    public async Task UpdateOrganization_RejectsPastEndDate()
    {
        var service = new OrganizationManagementService(_connectionFactory, _clock);

        var result = await service.UpdateOrganizationAsync(1, new OrganizationSaveRequest
        {
            Name = "Организация",
            DateBegin = "2026-08-02",
            DateEnd = "2026-08-03"
        });

        Assert.False(result.Success);
        Assert.Equal("Дата конца не может быть раньше сегодняшней даты.", result.Message);
    }

    private SurveyService CreateSurveyService()
        => new(
            _connectionFactory,
            new SurveyRepository(_connectionFactory, _clock),
            _clock);

    private static UserSaveRequest CreateUserRequest(
        string role = "user",
        string? dateBegin = "2026-08-04",
        string organizationId = "1",
        string? dateEnd = null)
    {
        return new UserSaveRequest
        {
            Username = "test-user",
            Password = "StrongPassword1",
            FullName = "Тестовый пользователь",
            OrganizationId = organizationId,
            Role = role,
            DateBegin = dateBegin,
            DateEnd = dateEnd
        };
    }

    private sealed class UnexpectedConnectionFactory : IDbConnectionFactory
    {
        public Task<NpgsqlConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Проверка обязательных полей не должна обращаться к БД.");
        }
    }

    private sealed class FixedClock : IClock
    {
        public DateTime Today => new(2026, 8, 4);
        public DateTime Now => new(2026, 8, 4, 12, 0, 0);
    }
}
