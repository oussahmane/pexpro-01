using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pexilio.Application;
using Pexilio.Domain;
using Pexilio.Domain.Accounts;
using Pexilio.Domain.Company;
using Pexilio.Infrastructure.Auth;
using Pexilio.Infrastructure.Identity;
using Pexilio.Infrastructure.Persistence;

namespace Pexilio.Infrastructure.Accounts;

public sealed class SessionService(
    PexilioDbContext database,
    UserManager<AppUser> users,
    AccessTokenFactory tokens,
    AuthSettings settings,
    TimeProvider clock) : ISessionService
{
    public async Task<Result<TokenPair>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var errors = new List<FieldError>();
        errors.AddRange(EmailAddressValidator.Validate(request.Email));
        errors.AddRange(PasswordPolicy.Validate(request.Password));
        if (errors.Count > 0)
        {
            return Result<TokenPair>.Fail(OperationFailure.Of(401, ErrorCodes.InvalidCredentials));
        }

        var user = await users.FindByEmailAsync(request.Email!.Trim());
        if (user is null)
        {
            return Result<TokenPair>.Fail(OperationFailure.Of(401, ErrorCodes.InvalidCredentials));
        }

        if (await users.IsLockedOutAsync(user))
        {
            return Result<TokenPair>.Fail(OperationFailure.Of(401, ErrorCodes.LockedOut));
        }

        if (!await users.CheckPasswordAsync(user, request.Password!))
        {
            await users.AccessFailedAsync(user);
            var code = await users.IsLockedOutAsync(user) ? ErrorCodes.LockedOut : ErrorCodes.InvalidCredentials;
            return Result<TokenPair>.Fail(OperationFailure.Of(401, code));
        }

        await users.ResetAccessFailedCountAsync(user);
        return Result<TokenPair>.Success(await IssueAsync(user, cancellationToken));
    }

    public async Task<Result<TokenPair>> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Result<TokenPair>.Fail(OperationFailure.Of(401, ErrorCodes.RefreshInvalid));
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var hash = TokenHasher.Hash(request.RefreshToken);
        var current = await database.RefreshTokens.SingleOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);
        if (current is null)
        {
            return Result<TokenPair>.Fail(OperationFailure.Of(401, ErrorCodes.RefreshInvalid));
        }

        if (current.RevokedAtUtc is not null && current.ReplacedByTokenId is not null)
        {
            await RevokeActiveAsync(current.UserId, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<TokenPair>.Fail(OperationFailure.Of(401, ErrorCodes.RefreshReused));
        }

        if (current.RevokedAtUtc is not null || current.ExpiresAtUtc <= now)
        {
            if (current.RevokedAtUtc is null)
            {
                current.RevokedAtUtc = now;
                await database.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }

            return Result<TokenPair>.Fail(OperationFailure.Of(401, ErrorCodes.RefreshInvalid));
        }

        var user = await users.FindByIdAsync(current.UserId.ToString());
        if (user is null)
        {
            return Result<TokenPair>.Fail(OperationFailure.Of(401, ErrorCodes.RefreshInvalid));
        }

        current.RevokedAtUtc = now;
        var (replacement, plaintext) = TokenHasher.Create(user.Id, now, settings.RefreshTokenLifetime);
        current.ReplacedByTokenId = replacement.Id;
        database.RefreshTokens.Add(replacement);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var roles = await users.GetRolesAsync(user);
        var (accessToken, accessExpires) = tokens.Create(user, roles, now);
        return Result<TokenPair>.Success(new TokenPair(accessToken, accessExpires, plaintext, replacement.ExpiresAtUtc));
    }

    public async Task LogoutAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return;
        }

        var hash = TokenHasher.Hash(request.RefreshToken);
        var current = await database.RefreshTokens.SingleOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);
        if (current is null || current.RevokedAtUtc is not null)
        {
            return;
        }

        current.RevokedAtUtc = clock.GetUtcNow();
        current.ReplacedByTokenId = null;
        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task<TokenPair> IssueAsync(AppUser user, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var (refresh, plaintext) = TokenHasher.Create(user.Id, now, settings.RefreshTokenLifetime);
        database.RefreshTokens.Add(refresh);
        await database.SaveChangesAsync(cancellationToken);
        var roles = await users.GetRolesAsync(user);
        var (accessToken, accessExpires) = tokens.Create(user, roles, now);
        return new TokenPair(accessToken, accessExpires, plaintext, refresh.ExpiresAtUtc);
    }

    private async Task RevokeActiveAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var active = await database.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var token in active)
        {
            token.RevokedAtUtc = now;
        }

        await database.SaveChangesAsync(cancellationToken);
    }
}
