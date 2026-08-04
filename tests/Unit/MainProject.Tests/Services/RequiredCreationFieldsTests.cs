using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Application.UseCases.Admin;
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

    private static UserSaveRequest CreateUserRequest(
        string role = "user",
        string? dateBegin = "2026-08-04")
    {
        return new UserSaveRequest
        {
            Username = "test-user",
            Password = "StrongPassword1",
            FullName = "Тестовый пользователь",
            OrganizationId = "1",
            Role = role,
            DateBegin = dateBegin
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
