using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using DockerSub.DataModel;
using Microsoft.Extensions.Logging;

namespace DockerSub
{
    internal abstract class DockerRegistryClient<TRegistry> : IDockerRegistryClient
        where TRegistry : DockerRegistryInfo
    {
        private AsyncLockedValue<string> bearerToken = new AsyncLockedValue<string>();

        protected ILogger Logger { get; }
        protected HttpClient HttpClient { get; }
        protected TRegistry Registry { get; }
        protected IEnumerable<string> Repos { get; }

        public DockerRegistryClient(ILogger logger, HttpClient httpClient, TRegistry registry, IEnumerable<string> repos)
        {
            this.Logger = logger;
            this.HttpClient = httpClient;
            Registry = registry;
            Repos = repos;
        }

        public async Task<string> GetDigestAsync(Subscription subscription)
        {
            this.Logger.LogInformation($"Querying digest for '{subscription.Repo}:{subscription.Tag}'");

            string bearerToken = await this.bearerToken.GetValueAsync(() => this.GetBearerTokenAsync(this.Repos));

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get,
                $"https://{this.Registry.HostName}/v2/{subscription.Repo}/manifests/{subscription.Tag}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.docker.distribution.manifest.v2+json"));
            var response = await this.HttpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var digest = response.Headers.GetValues("Docker-Content-Digest").First();
            this.Logger.LogInformation($"Digest result for '{subscription.Repo}:{subscription.Tag}': {digest}");
            return digest;
        }

        protected abstract Task<string> GetBearerTokenAsync(IEnumerable<string> repos);
    }
}