using FluentValidation;
using Api.Exceptions;
using Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Application.Common.Interfaces;

namespace Api.Middleware;

/// <summary>
/// Cross-cutting global error filter mapping domain rules and validation failures into structured HTTP REST JSON formats.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation("Request {Method} {Path} was cancelled.",
                context.Request.Method, context.Request.Path);
            if (!context.Response.HasStarted)
                context.Response.StatusCode = 499;
        }
        catch (Exception ex)
        {
            if (IsExpectedClientError(ex))
                _logger.LogWarning("Request {Method} {Path} was rejected: {Message}",
                    context.Request.Method, context.Request.Path, ex.Message);
            else
                _logger.LogError(ex,
                    "An unhandled exception occurred during transaction routing. Message: {Message}",
                    ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static bool IsExpectedClientError(Exception exception) =>
        exception is ApiProblemException or ValidationException or LogisticsException or
            DomainException or UnauthorizedAccessException or KeyNotFoundException or
            ArgumentException or InvalidOperationException or DbUpdateException;

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/problem+json";

        var (statusCode, responseBody) = exception switch
        {
            ApiProblemException problem => (problem.Status, SerializeProblem(context, problem)),
            MscTrackingReferenceNotFoundException notFound => (
                StatusCodes.Status404NotFound,
                JsonSerializer.Serialize(new
                {
                    Type = "https://api.bigsamgl.com/problems/msc-reference-not-found",
                    Title = "MSC tracking reference was not found.",
                    Status = StatusCodes.Status404NotFound,
                    Detail = notFound.Message,
                    CorrelationId = context.TraceIdentifier
                })
            ),
            MscTrackingUnavailableException unavailable => (
                StatusCodes.Status503ServiceUnavailable,
                JsonSerializer.Serialize(new
                {
                    Type = "https://api.bigsamgl.com/problems/msc-tracking-unavailable",
                    Title = "MSC tracking is temporarily unavailable.",
                    Status = StatusCodes.Status503ServiceUnavailable,
                    Detail = unavailable.Message,
                    CorrelationId = context.TraceIdentifier
                })
            ),
            ValidationException validationEx => (
                StatusCodes.Status400BadRequest,
                JsonSerializer.Serialize(new
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    Title = "One or more command validations failed.",
                    Status = StatusCodes.Status400BadRequest,
                    Errors = validationEx.Errors
                        .GroupBy(e => e.PropertyName)
                        .ToDictionary(
                            g => g.Key,
                            g => g.Select(x => x.ErrorMessage).ToArray()
                        )
                })
            ),
            LogisticsException logisticsEx => (
                StatusCodes.Status422UnprocessableEntity,
                JsonSerializer.Serialize(new
                {
                    Type = "https://tools.ietf.org/html/rfc4918#section-11.2",
                    Title = "Business and logistics rule violation.",
                    Status = StatusCodes.Status422UnprocessableEntity,
                    Detail = logisticsEx.Message
                })
            ),
            DomainException domainEx => (
                StatusCodes.Status400BadRequest,
                JsonSerializer.Serialize(new
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    Title = "Invalid business operation requested.",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = domainEx.Message
                })
            ),
            UnauthorizedAccessException unauthorizedEx => (
                StatusCodes.Status401Unauthorized,
                JsonSerializer.Serialize(new
                {
                    Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.2",
                    Title = "Authentication failed.",
                    Status = StatusCodes.Status401Unauthorized,
                    Detail = unauthorizedEx.Message
                })
            ),
            KeyNotFoundException notFoundEx => (
                StatusCodes.Status404NotFound,
                JsonSerializer.Serialize(new
                {
                    Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.5",
                    Title = "Resource not found.",
                    Status = StatusCodes.Status404NotFound,
                    Detail = notFoundEx.Message
                })
            ),
            ArgumentException argumentEx => (
                StatusCodes.Status400BadRequest,
                JsonSerializer.Serialize(new
                {
                    Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.1",
                    Title = "Invalid request.",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = argumentEx.Message
                })
            ),
            InvalidOperationException conflictEx => (
                StatusCodes.Status409Conflict,
                JsonSerializer.Serialize(new
                {
                    Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.10",
                    Title = "The operation conflicts with the current state.",
                    Status = StatusCodes.Status409Conflict,
                    Detail = conflictEx.Message
                })
            ),
            DbUpdateException => (
                StatusCodes.Status409Conflict,
                JsonSerializer.Serialize(new
                {
                    Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.10",
                    Title = "The record conflicts with existing data.",
                    Status = StatusCodes.Status409Conflict,
                    Detail = "A unique or relational data rule prevented this change."
                })
            ),
            _ => (
                StatusCodes.Status500InternalServerError,
                JsonSerializer.Serialize(new
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                    Title = "An internal server error occurred while processing command.",
                    Status = StatusCodes.Status500InternalServerError,
                    Detail = "A global unhandled background error occurred. Please contact a BiGSAM Global Logistics administrator."
                })
            )
        };

        context.Response.StatusCode = statusCode;
        return context.Response.WriteAsync(responseBody);
    }

    private static string SerializeProblem(HttpContext context, ApiProblemException problem)
    {
        var body = new Dictionary<string, object?>
        {
            ["type"] = "https://api.bigsamgl.com/problems/" + problem.Code.ToLowerInvariant().Replace('_', '-'),
            ["title"] = problem.Title,
            ["status"] = problem.Status,
            ["code"] = problem.Code,
            ["detail"] = problem.Message,
            ["correlationId"] = context.TraceIdentifier
        };
        if (problem.Errors is not null) body["errors"] = problem.Errors;
        if (problem.Extensions is not null)
            foreach (var value in problem.Extensions) body[value.Key] = value.Value;
        return JsonSerializer.Serialize(body);
    }
}
