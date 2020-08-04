using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using DockerSub.RestModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DockerSub
{
    internal class DockerHubClient : DockerRegistryClient<DockerHubRegistryInfo>
    {
        private readonly IConfigurationRoot config;

        public DockerHubClient(
            ILogger logger,
            HttpClient httpClient,
            DockerRegistryInfo registry,
            IEnumerable<string> repos,
            IConfigurationRoot config)
            : base(logger, httpClient, (DockerHubRegistryInfo)registry, repos)
        {
            this.config = config;
        }

        protected override async Task<string> GetBearerTokenAsync(IEnumerable<string> repos)
        {
            string scopeArgs = String.Join('&', repos
                .Select(repo => $"scope=repository:{repo}:pull")
                .ToArray());
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get,
                $"https://auth.docker.io/token?service=registry.docker.io&{scopeArgs}");
            string encodedCreds = Convert.ToBase64String(
                Encoding.ASCII.GetBytes($"{this.config["DockerHubUsername"]}:{this.config["DockerHubPassword"]}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encodedCreds);
            var response = await this.HttpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            string result = await response.Content.ReadAsStringAsync();
            var tokenResult = JsonConvert.DeserializeObject<RegistryTokenResult>(result);
            return tokenResult.Token;
        }
    }
}