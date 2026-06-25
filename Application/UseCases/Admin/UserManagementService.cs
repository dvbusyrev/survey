using System.Text.RegularExpressions;
using System.Text;
using MainProject.Application.Contracts;
using MainProject.Application.DTO;
using MainProject.Application.DTO.User;
using MainProject.Application.Support;
using MainProject.Infrastructure.Security;
using MainProject.Domain.Entities;
using MainProject.Web.ViewModels;
using Microsoft.AspNetCore.Identity;

namespace MainProject.Application.UseCases.Admin;

public sealed class UserManagementService : IUserManagementService
{
    private readonly IUserRepository _userRepository;
    private static readonly PasswordHasher<string> PasswordHasher = new();

    public UserManagementService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public Task<UserListPageViewModel> GetActiveUsersPageAsync(
        int currentPage,
        string? sortBy,
        string? sortDirection,
        bool openAddUserModal = false,
        CancellationToken cancellationToken = default)
    {
        return GetUsersPageAsync(currentPage, sortBy, sortDirection, includeArchived: false, openAddUserModal, cancellationToken);
    }

    public Task<IReadOnlyList<User>> GetArchivedUsersAsync(CancellationToken cancellationToken = default)
    {
        return GetUsersAsync(includeArchived: true, cancellationToken);
    }

    public Task<UserListPageViewModel> GetArchivedUsersPageAsync(
        int currentPage,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken = default)
    {
        return GetUsersPageAsync(currentPage, sortBy, sortDirection, includeArchived: true, cancellationToken: cancellationToken);
    }

    private async Task<UserListPageViewModel> GetUsersPageAsync(
        int currentPage,
        string? sortBy,
        string? sortDirection,
        bool includeArchived,
        bool openAddUserModal = false,
        CancellationToken cancellationToken = default)
    {
        var hasExplicitSort = AppSortState.HasExplicitSort(sortBy);
        var normalizedSortBy = NormalizeUserSortField(hasExplicitSort ? sortBy : null);
        var normalizedSortDirection = hasExplicitSort
            ? AppSortState.NormalizeExplicitDirection(sortDirection)
            : NormalizeUserSortDirection(null, normalizedSortBy);

        var totalCount = await _userRepository.CountAsync(includeArchived, cancellationToken);
        var pageWindow = AppListPaging.CreateWindow(totalCount, currentPage);
        var users = await _userRepository.GetPageAsync(
            includeArchived,
            normalizedSortBy,
            normalizedSortDirection,
            pageWindow.PageSize,
            pageWindow.Offset,
            cancellationToken);
        var organizations = await _userRepository.GetActiveOrganizationOptionsAsync(cancellationToken);

        return new UserListPageViewModel
        {
            Users = users,
            Organizations = organizations,
            OpenAddUserModal = openAddUserModal,
            CurrentPage = pageWindow.CurrentPage,
            TotalPages = pageWindow.TotalPages,
            TotalCount = pageWindow.TotalCount,
            PageSize = pageWindow.PageSize,
            HasExplicitSort = hasExplicitSort,
            SortBy = hasExplicitSort ? normalizedSortBy : string.Empty,
            SortDirection = hasExplicitSort ? normalizedSortDirection : string.Empty,
            ViewModeIsArchive = includeArchived
        };
    }

    public Task<User?> GetUserByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return _userRepository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<OperationResult> CreateUserAsync(UserSaveRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryValidateUserCreateRequest(
            request,
            out var organizationId,
            out var normalizedRole,
            out var dateBegin,
            out var dateEnd,
            out var validationError))
        {
            return new OperationResult
            {
                Success = false,
                Message = validationError,
                Error = validationError
            };
        }

        var affectedRows = await _userRepository.CreateAsync(new UserWriteModel(
            organizationId,
            request.Username.Trim(),
            request.FullName.Trim(),
            normalizedRole,
            PasswordHasher.HashPassword(request.Username.Trim(), request.Password),
            string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            dateBegin,
            dateEnd), cancellationToken);

        return new OperationResult
        {
            Success = affectedRows > 0,
            Message = affectedRows > 0
                ? $"Добавлен Клиент: {request.Username.Trim()}"
                : "Не удалось добавить запись в БД"
        };
    }

    public async Task<OperationResult> UpdateUserAsync(int id, UserUpdateRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryValidateUserUpdateRequest(request, out var organizationId, out var normalizedRole, out var dateBegin, out var dateEnd, out var passwordHash, out var validationError))
        {
            return new OperationResult
            {
                Success = false,
                Message = validationError,
                Error = validationError
            };
        }

        var affectedRows = await _userRepository.UpdateAsync(id, new UserWriteModel(
            organizationId,
            request.Username.Trim(),
            request.FullName.Trim(),
            normalizedRole,
            passwordHash,
            string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            dateBegin,
            dateEnd), cancellationToken);

        return new OperationResult
        {
            Success = affectedRows > 0,
            Message = affectedRows > 0
                ? "Данные пользователя успешно обновлены"
                : "Клиент не найден или данные не изменились"
        };
    }

    public async Task<OperationResult> DeleteUserAsync(int id, CancellationToken cancellationToken = default)
    {
        var result = await _userRepository.DeleteIfAllowedAsync(id, cancellationToken);
        if (!result.Found || result.User == null)
        {
            return new OperationResult
            {
                Success = false,
                Message = "Пользователь с указанным ID не найден."
            };
        }

        if (result.AnsweredSurveyNames.Count > 0 || result.SignedSurveyNames.Count > 0)
        {
            return new OperationResult
            {
                Success = false,
                Message = BuildUserDeleteBlockedMessage(
                    ResolveUserDisplayName(result.User),
                    result.AnsweredSurveyNames,
                    result.SignedSurveyNames)
            };
        }

        return new OperationResult
        {
            Success = result.Deleted,
            Message = result.Deleted
                ? "Пользователь успешно удален"
                : "Пользователь с указанным ID не найден."
        };
    }

    private Task<IReadOnlyList<User>> GetUsersAsync(bool includeArchived, CancellationToken cancellationToken)
    {
        return _userRepository.GetAllAsync(includeArchived, cancellationToken);
    }

    private static string NormalizeUserSortField(string? sortBy)
    {
        return sortBy?.Trim() switch
        {
            UserListSortFields.Organization => UserListSortFields.Organization,
            UserListSortFields.Role => UserListSortFields.Role,
            UserListSortFields.DateBegin => UserListSortFields.DateBegin,
            UserListSortFields.DateEnd => UserListSortFields.DateEnd,
            _ => UserListSortFields.Name
        };
    }

    private static string NormalizeUserSortDirection(string? sortDirection, string sortField)
    {
        if (string.Equals(sortDirection?.Trim(), "asc", StringComparison.OrdinalIgnoreCase))
        {
            return "asc";
        }

        if (string.Equals(sortDirection?.Trim(), "desc", StringComparison.OrdinalIgnoreCase))
        {
            return "desc";
        }

        return sortField switch
        {
            UserListSortFields.DateBegin => "desc",
            UserListSortFields.DateEnd => "desc",
            _ => "asc"
        };
    }

    private static bool TryValidateUserCreateRequest(
        UserSaveRequest request,
        out int organizationId,
        out string normalizedRole,
        out DateTime? dateBegin,
        out DateTime? dateEnd,
        out string validationError)
    {
        normalizedRole = AppRoles.Normalize(request.Role);
        validationError = string.Empty;
        dateBegin = null;
        dateEnd = null;

        if (!TryParseOrganizationId(request.OrganizationId, out organizationId))
        {
            validationError = "Не указана корректная организация.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            validationError = "Логин обязателен.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            validationError = "ФИО обязательно";
            return false;
        }

        if (!AppRoles.IsSupported(normalizedRole))
        {
            validationError = $"Недопустимая роль. Допустимые значения: {string.Join(", ", AppRoles.SupportedRoles)}";
            return false;
        }

        if (!TryParseOptionalDate(request.DateBegin, out dateBegin, out validationError))
        {
            dateEnd = null;
            return false;
        }

        if (!TryParseOptionalDate(request.DateEnd, out dateEnd, out validationError))
        {
            return false;
        }

        if (dateBegin.HasValue && dateEnd.HasValue && dateEnd.Value < dateBegin.Value)
        {
            validationError = "Дата конца не может быть раньше даты начала.";
            return false;
        }

        if (!IsPasswordValid(request.Password, out validationError))
        {
            return false;
        }

        return true;
    }

    private static bool TryValidateUserUpdateRequest(
        UserUpdateRequest request,
        out int organizationId,
        out string normalizedRole,
        out DateTime? dateBegin,
        out DateTime? dateEnd,
        out string? passwordHash,
        out string validationError)
    {
        normalizedRole = AppRoles.Normalize(request.Role);
        validationError = string.Empty;
        passwordHash = null;

        if (!TryParseOrganizationId(request.OrganizationId, out organizationId))
        {
            dateBegin = null;
            dateEnd = null;
            validationError = "Не указана корректная организация.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            dateBegin = null;
            dateEnd = null;
            validationError = "Логин обязателен.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            dateBegin = null;
            dateEnd = null;
            validationError = "ФИО обязательно";
            return false;
        }

        if (!AppRoles.IsSupported(normalizedRole))
        {
            dateBegin = null;
            dateEnd = null;
            validationError = $"Недопустимая роль. Допустимые значения: {string.Join(", ", AppRoles.SupportedRoles)}";
            return false;
        }

        if (!TryParseOptionalDate(request.DateBegin, out dateBegin, out validationError))
        {
            dateEnd = null;
            return false;
        }

        if (!TryParseOptionalDate(request.DateEnd, out dateEnd, out validationError))
        {
            return false;
        }

        if (dateBegin.HasValue && dateEnd.HasValue && dateEnd.Value < dateBegin.Value)
        {
            validationError = "Дата конца не может быть раньше даты начала.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(request.Password)
            && !string.Equals(request.Password, "keep_original", StringComparison.Ordinal))
        {
            if (!IsPasswordValid(request.Password, out validationError))
            {
                return false;
            }

            passwordHash = PasswordHasher.HashPassword(request.Username.Trim(), request.Password);
        }

        return true;
    }

    private static bool TryParseOrganizationId(string? rawValue, out int organizationId)
    {
        organizationId = 0;
        return int.TryParse(rawValue, out organizationId) && organizationId > 0;
    }

    private static bool TryParseOptionalDate(string? rawValue, out DateTime? parsedValue, out string validationError)
    {
        parsedValue = null;
        validationError = string.Empty;

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return true;
        }

        if (DateTime.TryParse(rawValue, out var date))
        {
            parsedValue = date;
            return true;
        }

        validationError = "Некорректный формат даты.";
        return false;
    }

    private static bool IsPasswordValid(string password, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(password))
        {
            errorMessage = "Пароль не должен быть пустым.";
            return false;
        }

        if (password.Length < 14)
        {
            errorMessage = "Пароль должен быть длиной не менее 14 символов.";
            return false;
        }

        if (!Regex.IsMatch(password, @"\p{Ll}"))
        {
            errorMessage = "Пароль должен содержать хотя бы одну строчную букву.";
            return false;
        }

        if (!Regex.IsMatch(password, @"\p{Lu}"))
        {
            errorMessage = "Пароль должен содержать хотя бы одну заглавную букву.";
            return false;
        }

        if (!Regex.IsMatch(password, "[0-9]"))
        {
            errorMessage = "Пароль должен содержать хотя бы одну цифру.";
            return false;
        }

        if (!Regex.IsMatch(password, @"[^\p{L}\p{Nd}]"))
        {
            errorMessage = "Пароль должен содержать хотя бы один спецсимвол.";
            return false;
        }

        return true;
    }

    private static string ResolveUserDisplayName(UserDeleteCandidate user)
    {
        if (!string.IsNullOrWhiteSpace(user.FullName))
        {
            return user.FullName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(user.UserName))
        {
            return user.UserName.Trim();
        }

        return $"ID {user.IdUser}";
    }

    private static string BuildUserDeleteBlockedMessage(
        string userDisplayName,
        IReadOnlyList<string> answeredSurveyNames,
        IReadOnlyList<string> signedSurveyNames)
    {
        var builder = new StringBuilder();
        builder.Append($"Пользователь \"{userDisplayName}\" не может быть удалён, так как уже работал с анкетами.");

        if (answeredSurveyNames.Count > 0)
        {
            builder.AppendLine();
            builder.Append("Отвечал на анкеты: ");
            builder.Append(string.Join(", ", answeredSurveyNames));
            builder.Append('.');
        }

        if (signedSurveyNames.Count > 0)
        {
            builder.AppendLine();
            builder.Append("Подписывал анкеты: ");
            builder.Append(string.Join(", ", signedSurveyNames));
            builder.Append('.');
        }

        return builder.ToString();
    }
}
