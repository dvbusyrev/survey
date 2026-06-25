using MainProject.Web.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MainProject.Web.Infrastructure;

public sealed record ApiErrorResponse(
    bool Success,
    string Error,
    string Message,
    string TraceId)
{
    public static ApiErrorResponse Create(HttpContext? context, string error)
    {
        var traceId = string.IsNullOrWhiteSpace(context?.TraceIdentifier)
            ? Guid.NewGuid().ToString("N")
            : context!.TraceIdentifier;

        return new ApiErrorResponse(false, error, error, traceId);
    }
}

public static class ControllerErrorExtensions
{
    public static ObjectResult SafeError(
        this ControllerBase controller,
        Exception exception,
        string publicMessage,
        string operation,
        int statusCode = StatusCodes.Status500InternalServerError)
    {
        var httpContext = controller.HttpContext;
        var payload = ApiErrorResponse.Create(httpContext, publicMessage);
        var loggerFactory = httpContext?.RequestServices?.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
        var logger = loggerFactory.CreateLogger(controller.GetType());

        logger.LogError(exception, "{Operation}. TraceId: {TraceId}", operation, payload.TraceId);

        return controller.StatusCode(statusCode, payload);
    }

    public static ViewResult SafeErrorView(
        this Controller controller,
        Exception exception,
        string publicMessage,
        string operation)
    {
        var httpContext = controller.HttpContext;
        var payload = ApiErrorResponse.Create(httpContext, publicMessage);
        var loggerFactory = httpContext?.RequestServices?.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
        var logger = loggerFactory.CreateLogger(controller.GetType());

        logger.LogError(exception, "{Operation}. TraceId: {TraceId}", operation, payload.TraceId);
        if (httpContext != null)
        {
            controller.Response.StatusCode = StatusCodes.Status500InternalServerError;
        }

        return controller.View("Error", new ErrorViewModel
        {
            Message = publicMessage,
            RequestId = payload.TraceId
        });
    }
}
