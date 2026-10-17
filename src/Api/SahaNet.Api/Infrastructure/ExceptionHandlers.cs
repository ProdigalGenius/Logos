using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace SahaNet.Api.Infrastructure;

/// <summary>FluentValidation hatalarını 400 ValidationProblemDetails'e çevirir.</summary>
internal sealed class ValidationExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ValidationException validation)
        {
            return false;
        }

        var errors = validation.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new Microsoft.AspNetCore.Mvc.ValidationProblemDetails(errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "validation_failed",
            },
            Exception = exception,
        });
    }
}

/// <summary>Eşzamanlı güncelleme çakışmasını (örn. paralel OTP denemesi) 409'a çevirir.</summary>
internal sealed class ConcurrencyExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DbUpdateConcurrencyException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "concurrency_conflict",
                Detail = "İşlem çakıştı, tekrar deneyin.",
            },
            Exception = exception,
        });
    }
}
