using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace aspnet_core_api.Middleware
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var requestId = Guid.NewGuid().ToString();

            // Add request ID to response headers for tracing
            context.Response.Headers["X-Request-ID"] = requestId;

            _logger.LogInformation("Request {RequestId} started: {Method} {Path}",
                requestId, context.Request.Method, context.Request.Path);

            try
            {
                await _next(context);
            }
            finally
            {
                stopwatch.Stop();
                _logger.LogInformation("Request {RequestId} completed in {ElapsedMilliseconds}ms with status {StatusCode}",
                    requestId, stopwatch.ElapsedMilliseconds, context.Response.StatusCode);
            }
        }
    }
}
