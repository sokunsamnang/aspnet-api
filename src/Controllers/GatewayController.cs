using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using aspnet_core_api.Services;

namespace aspnet_core_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GatewayController : ControllerBase
    {
        private readonly IServiceDiscovery _serviceDiscovery;
        private readonly ILogger<GatewayController> _logger;

        public GatewayController(IServiceDiscovery serviceDiscovery, ILogger<GatewayController> logger)
        {
            _serviceDiscovery = serviceDiscovery;
            _logger = logger;
        }

        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new
            {
                Status = "Healthy",
                Timestamp = DateTime.UtcNow,
                Version = "1.0.0",
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"
            });
        }

        [HttpGet("services")]
        [Authorize]
        public async Task<IActionResult> GetServices()
        {
            var services = new Dictionary<string, object>();

            var serviceNames = new[] { "service1", "service2", "auth" };

            foreach (var serviceName in serviceNames)
            {
                var urls = await _serviceDiscovery.GetAllServiceUrlsAsync(serviceName);
                services[serviceName] = new
                {
                    Name = serviceName,
                    Instances = urls.Count(),
                    Urls = urls
                };
            }

            return Ok(new
            {
                TotalServices = services.Count,
                Services = services,
                Timestamp = DateTime.UtcNow
            });
        }

        [HttpPost("services/{serviceName}/register")]
        [Authorize]
        public async Task<IActionResult> RegisterService(string serviceName, [FromBody] ServiceRegistrationRequest request)
        {
            if (string.IsNullOrEmpty(request.ServiceUrl))
            {
                return BadRequest("ServiceUrl is required");
            }

            await _serviceDiscovery.RegisterServiceAsync(serviceName, request.ServiceUrl);

            _logger.LogInformation("Service {ServiceName} registered at {ServiceUrl}", serviceName, request.ServiceUrl);

            return Ok(new
            {
                Message = $"Service {serviceName} registered successfully",
                ServiceName = serviceName,
                ServiceUrl = request.ServiceUrl,
                Timestamp = DateTime.UtcNow
            });
        }

        [HttpDelete("services/{serviceName}/deregister")]
        [Authorize]
        public async Task<IActionResult> DeregisterService(string serviceName, [FromBody] ServiceRegistrationRequest request)
        {
            if (string.IsNullOrEmpty(request.ServiceUrl))
            {
                return BadRequest("ServiceUrl is required");
            }

            await _serviceDiscovery.DeregisterServiceAsync(serviceName, request.ServiceUrl);

            _logger.LogInformation("Service {ServiceName} deregistered from {ServiceUrl}", serviceName, request.ServiceUrl);

            return Ok(new
            {
                Message = $"Service {serviceName} deregistered successfully",
                ServiceName = serviceName,
                ServiceUrl = request.ServiceUrl,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    public class ServiceRegistrationRequest
    {
        public string ServiceUrl { get; set; } = string.Empty;
    }
}
