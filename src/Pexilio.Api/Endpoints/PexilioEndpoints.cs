using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Pexilio.Application;
using Pexilio.Domain;
using Pexilio.Domain.Company;

namespace Pexilio.Api.Endpoints;

public static class PexilioEndpoints
{
    public static WebApplication MapPexilioEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/setup/status", async (ISetupService setup, CancellationToken cancellationToken) =>
            Results.Ok(new { complete = await setup.IsCompleteAsync(cancellationToken) }));

        app.MapPost("/api/v1/setup", async (SetupRequest request, ISetupService setup, CancellationToken cancellationToken) =>
        {
            var result = await setup.SetupAsync(request, cancellationToken);
            return ToResult(result, created => Results.Created($"/api/v1/company/{created.CompanyId}", created));
        });

        app.MapPost("/api/v1/auth/login", async (LoginRequest request, ISessionService sessions, CancellationToken cancellationToken) =>
            ToResult(await sessions.LoginAsync(request, cancellationToken), Results.Ok));

        app.MapPost("/api/v1/auth/refresh", async (RefreshRequest request, ISessionService sessions, CancellationToken cancellationToken) =>
            ToResult(await sessions.RefreshAsync(request, cancellationToken), Results.Ok));

        app.MapPost("/api/v1/auth/logout", async (RefreshRequest request, ISessionService sessions, CancellationToken cancellationToken) =>
        {
            await sessions.LogoutAsync(request, cancellationToken);
            return Results.NoContent();
        });

        app.MapGet("/api/v1/company", async (ICompanyService companies, CancellationToken cancellationToken) =>
        {
            var company = await companies.GetAsync(cancellationToken);
            return company is null
                ? Problem(404, ErrorCodes.CompanyMissing)
                : Results.Ok(company);
        }).RequireAuthorization();

        app.MapPatch("/api/v1/company", async (CompanyInput request, ICompanyService companies, CancellationToken cancellationToken) =>
            ToResult(await companies.UpdateAsync(request, cancellationToken), Results.Ok))
            .RequireAuthorization("Administrator");

        app.MapPost("/api/v1/users", async (LoginRequest request, ICashierService cashiers, CancellationToken cancellationToken) =>
            ToResult(await cashiers.CreateAsync(request, cancellationToken), userId => Results.Created($"/api/v1/users/{userId}", new { userId })))
            .RequireAuthorization("Administrator");

        app.MapGet("/api/v1/categories", async (ICatalogService catalog, CancellationToken cancellationToken) =>
            ToResult(await catalog.ListCategoriesAsync(cancellationToken), Results.Ok))
            .RequireAuthorization("products.read");

        app.MapPost("/api/v1/categories", async (CategoryRequest request, ICatalogService catalog, CancellationToken cancellationToken) =>
            ToResult(await catalog.CreateCategoryAsync(request, cancellationToken), created => Results.Created($"/api/v1/categories/{created.Id}", created)))
            .RequireAuthorization("products.write");

        app.MapPatch("/api/v1/categories/{id:guid}", async (Guid id, CategoryRequest request, ICatalogService catalog, CancellationToken cancellationToken) =>
            ToResult(await catalog.UpdateCategoryAsync(id, request, cancellationToken), Results.Ok))
            .RequireAuthorization("products.write");

        app.MapGet("/api/v1/products", async (ICatalogService catalog, CancellationToken cancellationToken) =>
            ToResult(await catalog.ListProductsAsync(cancellationToken), Results.Ok))
            .RequireAuthorization("products.read");

        app.MapPost("/api/v1/products", async (ProductRequest request, ICatalogService catalog, CancellationToken cancellationToken) =>
            ToResult(await catalog.CreateProductAsync(request, cancellationToken), created => Results.Created($"/api/v1/products/{created.Id}", created)))
            .RequireAuthorization("products.write");

        app.MapPatch("/api/v1/products/{id:guid}", async (Guid id, ProductRequest request, ICatalogService catalog, CancellationToken cancellationToken) =>
            ToResult(await catalog.UpdateProductAsync(id, request, cancellationToken), Results.Ok))
            .RequireAuthorization("products.write");

        app.MapGet("/api/v1/stock/locations", async (IStockService stock, CancellationToken cancellationToken) =>
            ToResult(await stock.ListLocationsAsync(cancellationToken), Results.Ok))
            .RequireAuthorization("stock.read");

        app.MapGet("/api/v1/stock/balances", async (IStockService stock, CancellationToken cancellationToken) =>
            ToResult(await stock.ListBalancesAsync(cancellationToken), Results.Ok))
            .RequireAuthorization("stock.read");

        app.MapGet("/api/v1/stock/movements", async (Guid? productId, IStockService stock, CancellationToken cancellationToken) =>
            ToResult(await stock.ListMovementsAsync(productId, cancellationToken), Results.Ok))
            .RequireAuthorization("stock.read");

        app.MapPost("/api/v1/stock/opening", async (StockQuantityRequest request, HttpRequest http, ClaimsPrincipal user, IStockService stock, CancellationToken cancellationToken) =>
            await PostStockAsync(http, user, (key, userId) => stock.OpenAsync(request, key, userId, cancellationToken)))
            .RequireAuthorization("stock.adjust");

        app.MapPost("/api/v1/stock/receipts", async (StockQuantityRequest request, HttpRequest http, ClaimsPrincipal user, IStockService stock, CancellationToken cancellationToken) =>
            await PostStockAsync(http, user, (key, userId) => stock.ReceiveAsync(request, key, userId, cancellationToken)))
            .RequireAuthorization("stock.adjust");

        app.MapPost("/api/v1/stock/adjustments", async (StockQuantityRequest request, HttpRequest http, ClaimsPrincipal user, IStockService stock, CancellationToken cancellationToken) =>
            await PostStockAsync(http, user, (key, userId) => stock.AdjustAsync(request, key, userId, cancellationToken)))
            .RequireAuthorization("stock.adjust");

        app.MapPatch("/api/v1/stock/balances/{productId:guid}/minimum", async (Guid productId, MinimumStockRequest request, IStockService stock, CancellationToken cancellationToken) =>
            ToResult(await stock.SetMinimumAsync(productId, request, cancellationToken), Results.Ok))
            .RequireAuthorization("stock.adjust");

        return app;
    }

    private static async Task<IResult> PostStockAsync(
        HttpRequest http,
        ClaimsPrincipal user,
        Func<Guid, Guid, Task<Result<StockMovementResponse>>> post)
    {
        if (!TryUserId(user, out var userId))
        {
            return Problem(401, ErrorCodes.AuthenticationRequired);
        }

        if (!TryIdempotencyKey(http, out var key, out var error))
        {
            return error;
        }

        var result = await post(key, userId);
        return ToResult(result, created => Results.Created($"/api/v1/stock/movements/{created.Id}", created));
    }

    private static bool TryUserId(ClaimsPrincipal user, out Guid userId)
    {
        var value = user.FindFirst("sub")?.Value;
        return Guid.TryParse(value, out userId);
    }

    private static bool TryIdempotencyKey(HttpRequest http, out Guid key, out IResult error)
    {
        key = Guid.Empty;
        if (!http.Headers.TryGetValue("Idempotency-Key", out var values) || string.IsNullOrWhiteSpace(values.ToString()))
        {
            error = Problem(400, ErrorCodes.ValidationFailed, [new FieldError("idempotencyKey", "idempotencyKey.required")]);
            return false;
        }

        if (!Guid.TryParse(values.ToString(), out key))
        {
            error = Problem(400, ErrorCodes.ValidationFailed, [new FieldError("idempotencyKey", "idempotencyKey.invalid")]);
            return false;
        }

        error = Results.Empty;
        return true;
    }

    private static IResult ToResult<T>(Result<T> result, Func<T, IResult> success)
    {
        if (result.Error is null && result.Value is not null)
        {
            return success(result.Value);
        }

        var error = result.Error ?? OperationFailure.Of(500, "request.failed");
        return Problem(error.StatusCode, error.Code, error.Errors, error.Details);
    }

    private static IResult Problem(
        int statusCode,
        string code,
        IReadOnlyList<FieldError>? errors = null,
        IReadOnlyDictionary<string, string>? details = null)
    {
        var extensions = new Dictionary<string, object?> { ["code"] = code };
        if (errors is { Count: > 0 })
        {
            extensions["errors"] = errors
                .GroupBy(error => error.Field)
                .ToDictionary(group => group.Key, group => group.Select(error => error.Code).ToArray());
        }

        if (details is not null)
        {
            foreach (var (name, value) in details)
            {
                extensions[name] = value;
            }
        }

        return Results.Problem(title: code, statusCode: statusCode, extensions: extensions);
    }
}

public sealed class ProblemAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Succeeded)
        {
            await next(context);
            return;
        }

        var forbidden = authorizeResult.Forbidden;
        var status = forbidden ? StatusCodes.Status403Forbidden : StatusCodes.Status401Unauthorized;
        var code = forbidden ? ErrorCodes.Forbidden : ErrorCodes.AuthenticationRequired;
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new { title = code, status, code });
    }
}
