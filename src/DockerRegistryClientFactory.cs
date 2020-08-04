using System;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DockerSub
{
    internal class DockerRegistryClientFactory : IDockerRegistryClientFactory
    {
        private readonly ILogger<DockerRegistryClientFactory> logger;
        private readonly IConfigurationRoot config;
        private readonly HttpClient httpClient;

        public DockerRegistryClientFactory(ILogger<DockerRegistryClientFactory> logger, IConfigurationRoot config, HttpClient httpClient)
        {
            this.logger = logger;
            this.config = config;
            this.httpClient = httpClient;
        }

        public IDockerRegistryClient CreateClient(DockerRegistryInfo registry, IEnumerable<string> repos)
        {
            switch (registry.RegistryType)
            {
                case RegistryType.DockerHub:
                    return new DockerHubClient(this.logger, this.httpClient, registry, repos, this.config);
                case RegistryType.AzureContainerRegistry:
                    return new AcrClient(this.logger, this.httpClient, registry, repos);
                default:
                    throw new NotSupportedException($"Unknown registry type: {registry.RegistryType}");
            }
        }
    }
}