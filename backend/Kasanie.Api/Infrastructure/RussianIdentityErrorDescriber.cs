using Microsoft.AspNetCore.Identity;

namespace Kasanie.Api.Infrastructure;

/// <summary>
/// Русские тексты ошибок ASP.NET Identity. Эти строки уходят на фронт в
/// <c>errors.account</c> / <c>errors.newPassword</c> и показываются пользователю как есть.
/// </summary>
public sealed class RussianIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() => new() { Code = nameof(DefaultError), Description = "Произошла неизвестная ошибка. Попробуйте ещё раз." };
    public override IdentityError ConcurrencyFailure() => new() { Code = nameof(ConcurrencyFailure), Description = "Данные уже изменил другой запрос. Обновите страницу и повторите." };

    public override IdentityError PasswordMismatch() => new() { Code = nameof(PasswordMismatch), Description = "Неверный текущий пароль." };
    public override IdentityError InvalidToken() => new() { Code = nameof(InvalidToken), Description = "Ссылка недействительна или устарела." };
    public override IdentityError RecoveryCodeRedemptionFailed() => new() { Code = nameof(RecoveryCodeRedemptionFailed), Description = "Не удалось применить код восстановления." };

    public override IdentityError LoginAlreadyAssociated() => new() { Code = nameof(LoginAlreadyAssociated), Description = "Этот способ входа уже привязан к другому аккаунту." };

    public override IdentityError InvalidUserName(string? userName) => new() { Code = nameof(InvalidUserName), Description = "Имя пользователя содержит недопустимые символы." };
    public override IdentityError InvalidEmail(string? email) => new() { Code = nameof(InvalidEmail), Description = "Укажите корректный email." };
    public override IdentityError DuplicateUserName(string userName) => new() { Code = nameof(DuplicateUserName), Description = "Аккаунт с таким email уже существует." };
    public override IdentityError DuplicateEmail(string email) => new() { Code = nameof(DuplicateEmail), Description = "Аккаунт с таким email уже существует." };
    public override IdentityError InvalidRoleName(string? role) => new() { Code = nameof(InvalidRoleName), Description = "Недопустимое название роли." };
    public override IdentityError DuplicateRoleName(string role) => new() { Code = nameof(DuplicateRoleName), Description = "Такая роль уже существует." };

    public override IdentityError UserAlreadyHasPassword() => new() { Code = nameof(UserAlreadyHasPassword), Description = "У пользователя уже задан пароль." };
    public override IdentityError UserLockoutNotEnabled() => new() { Code = nameof(UserLockoutNotEnabled), Description = "Блокировка для этого аккаунта отключена." };
    public override IdentityError UserAlreadyInRole(string role) => new() { Code = nameof(UserAlreadyInRole), Description = "Пользователь уже состоит в этой роли." };
    public override IdentityError UserNotInRole(string role) => new() { Code = nameof(UserNotInRole), Description = "Пользователь не состоит в этой роли." };

    public override IdentityError PasswordTooShort(int length) => new() { Code = nameof(PasswordTooShort), Description = $"Пароль должен содержать не менее {length} символов." };
    public override IdentityError PasswordRequiresNonAlphanumeric() => new() { Code = nameof(PasswordRequiresNonAlphanumeric), Description = "Пароль должен содержать хотя бы один специальный знак (например ! ? * -)." };
    public override IdentityError PasswordRequiresDigit() => new() { Code = nameof(PasswordRequiresDigit), Description = "Пароль должен содержать хотя бы одну цифру." };
    public override IdentityError PasswordRequiresLower() => new() { Code = nameof(PasswordRequiresLower), Description = "Пароль должен содержать хотя бы одну строчную букву." };
    public override IdentityError PasswordRequiresUpper() => new() { Code = nameof(PasswordRequiresUpper), Description = "Пароль должен содержать хотя бы одну заглавную букву." };
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => new() { Code = nameof(PasswordRequiresUniqueChars), Description = $"Пароль должен содержать не менее {uniqueChars} различных символов." };
}
