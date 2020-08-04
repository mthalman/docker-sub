using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using DockerSub.RestModel;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json;

namespace DockerSub
{
    internal class AcrClient : DockerRegistryClient<AcrInfo>
    {
        private readonly ILogger logger;
        private readonly HttpClient httpClient;

        public AcrClient(ILogger logger, HttpClient httpClient, DockerRegistryInfo registryInfo, IEnumerable<string> repos)
            : base(logger, httpClient, (AcrInfo)registryInfo, repos)
        {
            this.logger = logger;
            this.httpClient = httpClient;
        }

        protected override async Task<string> GetBearerTokenAsync(IEnumerable<string> repos)
        {
            AuthenticationContext authContext = new AuthenticationContext($"https://login.microsoftonline.com/{this.Registry.Tenant}");
            AuthenticationResult result = await authContext.AcquireTokenAsync(
                "https://management.azure.com", new ClientCredential(this.Registry.ClientId, this.Registry.ClientSecret));
            string aadAccessToken = result.AccessToken;

            FormUrlEncodedContent oauthExchangeBody = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "access_token" },
                { "service", this.Registry.HostName },
                { "tenant", this.Registry.Tenant },
                { "access_token", aadAccessToken }
            });

            HttpResponseMessage tokenExchangeResponse = await this.HttpClient.PostAsync(
                $"https://{this.Registry.HostName}/oauth2/exchange", oauthExchangeBody);
            tokenExchangeResponse.EnsureSuccessStatusCode();
            OAuthExchangeResult acrRefreshTokenResult = JsonConvert.DeserializeObject<OAuthExchangeResult>(
                await tokenExchangeResponse.Content.ReadAsStringAsync());

            var fields = new Dictionary<string, string>
            {
                { "grant_type", "refresh_token" },
                { "service", this.Registry.HostName },
                { "refresh_token", acrRefreshTokenResult.RefreshToken }
            };

            foreach (string repo in repos)
            {
                fields.Add("scope", $"repository:{repo}:pull");
            }

            FormUrlEncodedContent oauthTokenBody = new FormUrlEncodedContent(fields);

            HttpResponseMessage tokenResponse = await HttpClient.PostAsync(
                $"https://{this.Registry.HostName}/oauth2/token", oauthTokenBody);
            tokenResponse.EnsureSuccessStatusCode();
            OAuthTokenResult acrAccessTokenResult = JsonConvert.DeserializeObject<OAuthTokenResult>(
                await tokenResponse.Content.ReadAsStringAsync());
            return acrAccessTokenResult.AccessToken;
        }
    }
}