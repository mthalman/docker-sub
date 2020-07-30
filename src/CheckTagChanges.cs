using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Extensions.Configuration;
using DockerSub.DataModel;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using DockerSub.RestModel;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System.Collections.Generic;
using Microsoft.Azure.EventGrid;
using Microsoft.Azure.EventGrid.Models;

namespace DockerSub
{
    public class CheckTagChanges
    {
        private readonly HttpClient httpClient;
        private readonly IConfigurationRoot config;
        private readonly ILogger log;

        public CheckTagChanges(System.Net.Http.IHttpClientFactory httpClientFactory, IConfigurationRoot config, ILogger<CheckTagChanges> log)
        {
            this.httpClient = httpClientFactory.CreateClient();
            this.config = config;
            this.log = log;
        }

        [FunctionName(nameof(CheckTagChanges))]
        public async Task Run(
            [TimerTrigger("0 */1 * * * *")] TimerInfo myTimer,
            ExecutionContext context)
        {
            if (myTimer.IsPastDue)
            {
                log.LogInformation("Timer is running late!");
            }

            log.LogInformation("Checking tag changes");

            string storageConnectionString = config["AzureWebJobsStorage"];
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageConnectionString);
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();

            CloudTable digestsTable = tableClient.GetTableReference("digests");
            await digestsTable.CreateIfNotExistsAsync();

            CloudTable subscriptionsTable = tableClient.GetTableReference("subscriptions");
            await subscriptionsTable.CreateIfNotExistsAsync();

            var subscriptions = subscriptionsTable.ExecuteQuery(new TableQuery<Subscription>());

            foreach (var subscription in subscriptions)
            {
                await CheckTagChangeAsync(subscription, digestsTable);
            }
        }

        private async Task CheckTagChangeAsync(Subscription subscription, CloudTable digestsTable)
        {
            log.LogInformation($"Checking tag change for '{subscription.Repo}:{subscription.Tag}'");

            string digest = await GetDigestAsync(log, subscription);

            log.LogInformation($"Querying stored digest for '{subscription.Repo}:{subscription.Tag}'");

            var keys = DigestEntry.GetKeys(subscription.Registry, subscription.Repo, subscription.Tag);
            var retrieveOperation = TableOperation.Retrieve<DigestEntry>(keys.PartitionKey, keys.RowKey);
            var result = (DigestEntry)(await digestsTable.ExecuteAsync(retrieveOperation)).Result;

            TagChangedData tagChangedData = null;

            if (result is null)
            {
                log.LogInformation($"Tag '{subscription.Repo}:{subscription.Tag}' is not stored yet. Inserting into table.");
                var insertOperation = TableOperation.Insert(new DigestEntry(subscription.Registry, subscription.Repo, subscription.Tag)
                {
                    Digest = digest
                });
                await digestsTable.ExecuteAsync(insertOperation);

                tagChangedData = CreateTagChangedData(subscription, digest, TagChangeType.New);
            }
            else if (result.Digest != digest)
            {
                log.LogInformation($"Tag '{subscription.Repo}:{subscription.Tag}' has digest diff. Updating table with new value.{Environment.NewLine}Stored: {result.Digest}{Environment.NewLine}Latest: {digest}");
                result.Digest = digest;
                var insertOperation = TableOperation.Merge(result);
                await digestsTable.ExecuteAsync(insertOperation);

                tagChangedData = CreateTagChangedData(subscription, digest, TagChangeType.Updated);
            }
            else
            {
                log.LogInformation($"No change to digest for '{subscription.Repo}:{subscription.Tag}'.");
            }

            if (tagChangedData != null)
            {
                EventGridEvent eventGridEvent = new EventGridEvent
                {
                    Data = tagChangedData,
                    Subject = $"{tagChangedData.Repo}:{tagChangedData.Tag}",
                    Id = Guid.NewGuid().ToString(),
                    EventTime = DateTime.UtcNow,
                    EventType = tagChangedData.ChangeType.ToString(),
                    DataVersion = "1.0",
                };

                await SendEventNotificationAsync(log, config["TagChangedEndpoint"], config["TagChangedAccessKey"], eventGridEvent);
            }
        }

        private static TagChangedData CreateTagChangedData(Subscription subscription, string digest, TagChangeType tagChangeType) =>
            new TagChangedData
            {
                ChangeType = tagChangeType,
                Digest = digest,
                Registry = subscription.Registry,
                Repo = subscription.Repo,
                Tag = subscription.Tag,
                SubscriptionId = subscription.Id
            };

        private async Task<string> GetDigestAsync(ILogger log, Subscription subscription)
        {
            log.LogInformation($"Querying digest for '{subscription.Repo}:{subscription.Tag}'");

            string registryHostName = GetRegistryHostName(subscription);
            string bearerToken = await GetRegistryBearerTokenAsync(subscription);
            
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get,
                $"https://{registryHostName}/v2/{subscription.Repo}/manifests/{subscription.Tag}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.docker.distribution.manifest.v2+json"));
            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var digest = response.Headers.GetValues("Docker-Content-Digest").First();
            log.LogInformation($"Digest result for '{subscription.Repo}:{subscription.Tag}': {digest}");
            return digest;
        }

        private string GetRegistryHostName(Subscription subscription)
        {
            if (subscription.RegistryType == RegistryType.DockerHub)
            {
                return "registry-1.docker.io";
            }
            
            return subscription.Registry;
        }

        private Task<string> GetRegistryBearerTokenAsync(Subscription subscription)
        {
            switch (subscription.RegistryType)
            {
                case RegistryType.DockerHub:
                    return GetDockerHubRegistryBearerTokenAsync(subscription);
                case RegistryType.AzureContainerRegistry:
                    return GetAcrAccessBearerAsync(subscription);
                default:
                    throw new NotSupportedException($"Unknown registry type: {subscription.RegistryType}");
            }
        }

        private async Task<string> GetAcrAccessBearerAsync(Subscription subscription)
        {
            AuthenticationContext authContext = new AuthenticationContext($"https://login.microsoftonline.com/{subscription.AadTenant}");
            AuthenticationResult result = await authContext.AcquireTokenAsync(
                "https://management.azure.com", new ClientCredential(subscription.AadClientId, subscription.AadClientSecret));
            string aadAccessToken = result.AccessToken;

            FormUrlEncodedContent oauthExchangeBody = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "access_token" },
                { "service", subscription.Registry },
                { "tenant", subscription.AadTenant },
                { "access_token", aadAccessToken }
            });

            HttpResponseMessage tokenExchangeResponse = await httpClient.PostAsync(
                $"https://{subscription.Registry}/oauth2/exchange", oauthExchangeBody);
            tokenExchangeResponse.EnsureSuccessStatusCode();
            OAuthExchangeResult acrRefreshTokenResult = JsonConvert.DeserializeObject<OAuthExchangeResult>(
                await tokenExchangeResponse.Content.ReadAsStringAsync());

            FormUrlEncodedContent oauthTokenBody = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "refresh_token" },
                { "service", subscription.Registry },
                { "refresh_token", acrRefreshTokenResult.RefreshToken },
                { "scope", $"repository:{subscription.Repo}:pull"}
            });

            HttpResponseMessage tokenResponse = await httpClient.PostAsync(
                $"https://{subscription.Registry}/oauth2/token", oauthTokenBody);
            tokenResponse.EnsureSuccessStatusCode();
            OAuthTokenResult acrAccessTokenResult = JsonConvert.DeserializeObject<OAuthTokenResult>(
                await tokenResponse.Content.ReadAsStringAsync());
            return acrAccessTokenResult.AccessToken;
        }

        private async Task<string> GetDockerHubRegistryBearerTokenAsync(Subscription subscription)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get,
                $"https://auth.docker.io/token?service=registry.docker.io&scope=repository:{subscription.Repo}:pull");
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($"{config["DockerHubUsername"]}:{config["DockerHubPassword"]}")));
            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            string result = await response.Content.ReadAsStringAsync();
            var tokenResult = JsonConvert.DeserializeObject<RegistryTokenResult>(result);
            return tokenResult.Token;
        }

        private async Task SendEventNotificationAsync(ILogger log, string topicEndpoint, string topicAccessKey, EventGridEvent eventGridEvent)
        {
            log.LogInformation($"Sending event notification to '{topicEndpoint}'");

            TopicCredentials creds = new TopicCredentials(topicAccessKey);

            EventGridClient client = new EventGridClient(creds, httpClient, disposeHttpClient: false);
            await client.PublishEventsAsync(topicEndpoint, new List<EventGridEvent> { eventGridEvent });
        }
    }
}
