using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pexilio.Application;
using Pexilio.Domain;
using Pexilio.Domain.Accounts;
using Pexilio.Domain.Company;
using Pexilio.Infrastructure.Auth;
using Pexilio.Infrastructure.Identity;
using Pexilio.Infrastructure.Persistence;

namespace Pexilio.Infrastructure.Accounts;

public sealed class SetupService(
    PexilioDbContext database,
    UserManager<AppUser> users,
    RoleManager<AppRole> roles) : ISetupService
{
    private const long SetupLockKey = 481271001;

    public Task<bool> IsCompleteAsync(CancellationToken cancellationToken) =>
        database.Companies.AnyAsync(cancellationToken);

    public async Task<Result<SetupCreated>> SetupAsync(SetupRequest request, CancellationToken cancellationToken)
    {
        var errors = new List<FieldError>();
        errors.AddRange(EmailAddressValidator.Validate(request.Email));
        errors.AddRange(PasswordPolicy.Validate(request.Password));
        var companyValid = CompanyProfileValidator.TryNormalize(request.ToCompanyInput(), out var profile, out var companyErrors);
        errors.AddRange(companyErrors);
        if (!companyValid || profile is null || errors.Count > 0)
        {
            return Result<SetupCreated>.Fail(OperationFailure.Validation(errors));
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({SetupLockKey})",
            cancellationToken);

        if (await database.Companies.AnyAsync(cancellationToken))
        {
            return Result<SetupCreated>.Fail(OperationFailure.Of(409, ErrorCodes.SetupAlreadyComplete));
        }

        var company = new Company
        {
            Id = Guid.CreateVersion7(),
            SingletonKey = 1,
            LegalName = profile.LegalName,
            TradeName = profile.TradeName,
            CurrencyCode = profile.CurrencyCode,
            CurrencyScale = profile.CurrencyScale,
            TimeZoneId = profile.TimeZoneId,
            TaxMode = profile.TaxMode,
            Rc = profile.Rc,
            Nif = profile.Nif,
            Nis = profile.Nis,
            Ai = profile.Ai,
        };
        var taxRate = new TaxRate
        {
            Id = Guid.CreateVersion7(),
            Company = company,
            Name = "default",
            Rate = profile.DefaultTaxRate,
            IsDefault = true,
        };
        database.Companies.Add(company);
        database.TaxRates.Add(taxRate);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await EnsureRoleAsync(RoleNames.Administrator);
            await EnsureRoleAsync(RoleNames.Cashier);

            var email = request.Email!.Trim();
            var administrator = new AppUser
            {
                Id = Guid.CreateVersion7(),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
            };
            var created = await users.CreateAsync(administrator, request.Password!);
            if (!created.Succeeded)
            {
                return Result<SetupCreated>.Fail(MapIdentity(created));
            }

            var assigned = await users.AddToRoleAsync(administrator, RoleNames.Administrator);
            if (!assigned.Succeeded)
            {
                return Result<SetupCreated>.Fail(MapIdentity(assigned));
            }

            await transaction.CommitAsync(cancellationToken);
            return Result<SetupCreated>.Success(new SetupCreated(company.Id, administrator.Id));
        }
        catch (DbUpdateException exception) when (IsSetupRace(exception))
        {
            return Result<SetupCreated>.Fail(OperationFailure.Of(409, ErrorCodes.SetupAlreadyComplete));
        }
    }

    private async Task EnsureRoleAsync(string roleName)
    {
        if (await roles.RoleExistsAsync(roleName))
        {
            return;
        }

        var created = await roles.CreateAsync(new AppRole
        {
            Id = Guid.CreateVersion7(),
            Name = roleName,
        });
        if (!created.Succeeded)
        {
            throw new InvalidOperationException("A required role could not be created.");
        }
    }

    private static OperationFailure MapIdentity(IdentityResult result)
    {
        if (result.Errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName"))
        {
            return OperationFailure.Of(409, ErrorCodes.EmailTaken);
        }

        return OperationFailure.Validation([new FieldError("password", "password.rejected")]);
    }

    private static bool IsSetupRace(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres && postgres.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure)
            {
                return true;
            }
        }

        return false;
    }
}

public sealed class CompanyService(PexilioDbContext database) : ICompanyService
{
    public async Task<CompanyResponse?> GetAsync(CancellationToken cancellationToken)
    {
        var company = await database.Companies
            .Include(item => item.TaxRates)
            .SingleOrDefaultAsync(cancellationToken);
        return company is null ? null : ToResponse(company);
    }

    public async Task<Result<CompanyResponse>> UpdateAsync(CompanyInput request, CancellationToken cancellationToken)
    {
        if (!CompanyProfileValidator.TryNormalize(request, out var profile, out var errors) || profile is null)
        {
            return Result<CompanyResponse>.Fail(OperationFailure.Validation(errors));
        }

        var company = await database.Companies
            .Include(item => item.TaxRates)
            .SingleOrDefaultAsync(cancellationToken);
        if (company is null)
        {
            return Result<CompanyResponse>.Fail(OperationFailure.Of(404, ErrorCodes.CompanyMissing));
        }

        var taxRate = company.TaxRates.SingleOrDefault(rate => rate.IsDefault);
        if (taxRate is null)
        {
            return Result<CompanyResponse>.Fail(OperationFailure.Of(404, ErrorCodes.CompanyMissing));
        }

        company.LegalName = profile.LegalName;
        company.TradeName = profile.TradeName;
        company.CurrencyCode = profile.CurrencyCode;
        company.CurrencyScale = profile.CurrencyScale;
        company.TimeZoneId = profile.TimeZoneId;
        company.TaxMode = profile.TaxMode;
        company.Rc = profile.Rc;
        company.Nif = profile.Nif;
        company.Nis = profile.Nis;
        company.Ai = profile.Ai;
        taxRate.Rate = profile.DefaultTaxRate;
        await database.SaveChangesAsync(cancellationToken);
        return Result<CompanyResponse>.Success(ToResponse(company));
    }

    internal static CompanyResponse ToResponse(Company company)
    {
        var rate = company.TaxRates.Single(item => item.IsDefault).Rate;
        return new CompanyResponse(
            company.Id,
            company.LegalName,
            company.TradeName,
            company.CurrencyCode,
            company.CurrencyScale,
            company.TimeZoneId,
            company.TaxMode == TaxMode.Inclusive ? "inclusive" : "exclusive",
            rate.ToString("0.######", CultureInfo.InvariantCulture),
            company.Rc,
            company.Nif,
            company.Nis,
            company.Ai);
    }
}

public sealed class CashierService(PexilioDbContext database, UserManager<AppUser> users) : ICashierService
{
    public async Task<Result<Guid>> CreateAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var errors = new List<FieldError>();
        errors.AddRange(EmailAddressValidator.Validate(request.Email));
        errors.AddRange(PasswordPolicy.Validate(request.Password));
        if (errors.Count > 0)
        {
            return Result<Guid>.Fail(OperationFailure.Validation(errors));
        }

        if (!await database.Companies.AnyAsync(cancellationToken))
        {
            return Result<Guid>.Fail(OperationFailure.Of(404, ErrorCodes.CompanyMissing));
        }

        var email = request.Email!.Trim();
        var cashier = new AppUser
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };
        var created = await users.CreateAsync(cashier, request.Password!);
        if (!created.Succeeded)
        {
            if (created.Errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName"))
            {
                return Result<Guid>.Fail(OperationFailure.Of(409, ErrorCodes.EmailTaken));
            }

            return Result<Guid>.Fail(OperationFailure.Validation([new FieldError("password", "password.rejected")]));
        }

        var assigned = await users.AddToRoleAsync(cashier, RoleNames.Cashier);
        if (!assigned.Succeeded)
        {
            return Result<Guid>.Fail(OperationFailure.Of(500, "users.role_failed"));
        }

        return Result<Guid>.Success(cashier.Id);
    }
}
