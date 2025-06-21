using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace aspnet_core_api.Middleware
{
    public class ApiKeyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ApiKeyMiddleware> _logger;

        public ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<ApiKeyMiddleware> logger)
        {
            _next = next;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Skip API key validation for health checks, auth endpoints, and swagger
            var path = context.Request.Path.Value?.ToLower();
            if (path != null && (
                path.Contains("/health") ||
                path.Contains("/api/auth") ||
                path.Contains("/swagger") ||
                path.Contains("/api/gateway") ||
                path == "/" ||
                path == ""))
            {
                await _next(context);
                return;
            }

            // Check for API key in header or query parameter
            var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault()
                         ?? context.Request.Query["apikey"].FirstOrDefault();

            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("Request to {Path} missing API key", context.Request.Path);
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("API Key is required");
                return;
            }

            // Validate API key (in production, this should be from a database or config)
            var validApiKeys = _configuration.GetSection("ApiKeys").Get<string[]>() ?? new[] { "gateway-api-key-123" };

            if (!validApiKeys.Contains(apiKey))
            {
                _logger.LogWarning("Invalid API key used: {ApiKey}", apiKey);
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Invalid API Key");
                return;
            }

            await _next(context);
        }
    }
}
