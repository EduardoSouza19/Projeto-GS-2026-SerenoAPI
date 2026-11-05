using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SerenoAPI.Telemetry;

/// <summary>
/// Cria spans customizados e registra métricas para todas as ações dos controllers.
/// A instrumentação automática do ASP.NET Core continua responsável pelo trace HTTP.
/// </summary>
public sealed class TelemetryActionFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var controller = context.Controller.GetType().Name;
        var action = context.ActionDescriptor.RouteValues["action"] ?? "Unknown";
        var method = context.HttpContext.Request.Method;

        using var activity = AppTelemetry.ActivitySource.StartActivity($"API {method} {controller}.{action}");
        activity?.SetTag("http.request.method", method);
        activity?.SetTag("api.controller", controller);
        activity?.SetTag("api.action", action);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var executed = await next();
            var statusCode = executed.HttpContext.Response.StatusCode;

            activity?.SetTag("http.response.status_code", statusCode);
            activity?.SetStatus(statusCode >= 500 ? ActivityStatusCode.Error : ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetTag("error.type", ex.GetType().FullName);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            stopwatch.Stop();

            var tags = new TagList
            {
                { "controller", controller },
                { "action", action },
                { "method", method }
            };

            AppMetrics.RequestsCounter.Add(1, tags);
            AppMetrics.RequestDuration.Record(stopwatch.Elapsed.TotalMilliseconds, tags);
        }
    }
}
