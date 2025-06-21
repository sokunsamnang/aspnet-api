using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace aspnet_core_api.Services
{
    public interface IServiceDiscovery
    {
        Task<string?> GetServiceUrlAsync(string serviceName);
        Task<IEnumerable<string>> GetAllServiceUrlsAsync(string serviceName);
        Task RegisterServiceAsync(string serviceName, string serviceUrl);
        Task DeregisterServiceAsync(string serviceName, string serviceUrl);
    }

    public class ServiceDiscovery : IServiceDiscovery
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ServiceDiscovery> _logger;
        private readonly Dictionary<string, List<string>> _services = new();

        public ServiceDiscovery(IConfiguration configuration, ILogger<ServiceDiscovery> logger)
        {
            _configuration = configuration;
            _logger = logger;
            InitializeServices();
        }

        private void InitializeServices()
        {
            // Initialize from configuration
            var reverseProxyConfig = _configuration.GetSection("ReverseProxy:Clusters");
            foreach (var cluster in reverseProxyConfig.GetChildren())
            {
                var serviceName = cluster.Key.Replace("-cluster", "");
                var destinations = cluster.GetSection("Destinations").GetChildren();

                foreach (var destination in destinations)
                {
                    var address = destination.GetValue<string>("Address");
                    if (!string.IsNullOrEmpty(address))
                    {
                        RegisterServiceAsync(serviceName, address).Wait();
                    }
                }
            }
        }

        public Task<string?> GetServiceUrlAsync(string serviceName)
        {
            if (_services.TryGetValue(serviceName, out var urls) && urls.Count > 0)
            {
                // Simple round-robin selection
                var index = Random.Shared.Next(urls.Count);
                return Task.FromResult<string?>(urls[index]);
            }
            return Task.FromResult<string?>(null);
        }

        public Task<IEnumerable<string>> GetAllServiceUrlsAsync(string serviceName)
        {
            if (_services.TryGetValue(serviceName, out var urls))
            {
                return Task.FromResult<IEnumerable<string>>(urls);
            }
            return Task.FromResult<IEnumerable<string>>(Array.Empty<string>());
        }

        public Task RegisterServiceAsync(string serviceName, string serviceUrl)
        {
            if (!_services.ContainsKey(serviceName))
            {
                _services[serviceName] = new List<string>();
            }

            if (!_services[serviceName].Contains(serviceUrl))
            {
                _services[serviceName].Add(serviceUrl);
                _logger.LogInformation("Registered service {ServiceName} at {ServiceUrl}", serviceName, serviceUrl);
            }

            return Task.CompletedTask;
        }

        public Task DeregisterServiceAsync(string serviceName, string serviceUrl)
        {
            if (_services.TryGetValue(serviceName, out var urls))
            {
                urls.Remove(serviceUrl);
                _logger.LogInformation("Deregistered service {ServiceName} from {ServiceUrl}", serviceName, serviceUrl);
            }

            return Task.CompletedTask;
        }
    }
}
