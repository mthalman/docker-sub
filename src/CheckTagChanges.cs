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
using System.Net.Http.Json;

namespace DockerSub
{
    public class CheckTagChanges
    {
        private readonly HttpClient httpClient;
        private readonly IConfigurationRoot config;
        private readonly ILogger log;

        public CheckTagChanges(IHttpClientFactory httpClientFactory, IConfigurationRoot config, ILogger<CheckTagChanges> log)
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
            CloudTableClient client = storageAccount.CreateCloudTableClient();

            CloudTable digestsTable = client.GetTableReference("digests");
            await digestsTable.CreateIfNotExistsAsync();

            CloudTable subscriptionsTable = client.GetTableReference("subscriptions");
            await subscriptionsTable.CreateIfNotExistsAsync();

            var subscriptions = subscriptionsTable.ExecuteQuery(new TableQuery<Subscription>());

            foreach (var subscription in subscriptions)
            {
                await CheckTagChangeAsync(subscription.Repo, subscription.Tag, digestsTable);
            }
        }

        private async Task CheckTagChangeAsync(string repo, string tag, CloudTable digestsTable)
        {
            log.LogInformation($"Checking tag change for '{repo}:{tag}'");

            string digest = await GetDigestAsync(log, config["DockerHubUsername"], config["DockerHubPassword"], repo, tag);

            log.LogInformation($"Querying stored digest for '{repo}:{tag}'");
            var retrieveOperation = TableOperation.Retrieve<DigestEntry>(StringHelper.EncodePartitionKey(repo), tag);
            var result = (DigestEntry)(await digestsTable.ExecuteAsync(retrieveOperation)).Result;

            TagChangedData tagChangedData = null;

            if (result is null)
            {
                log.LogInformation($"Tag '{repo}:{tag}' is not stored yet. Inserting into table.");
                var insertOperation = TableOperation.Insert(new DigestEntry(repo, tag)
                {
                    Digest = digest
                });
                await digestsTable.ExecuteAsync(insertOperation);

                tagChangedData = new TagChangedData
                {
                    ChangeType = TagChangeType.New,
                    Digest = digest,
                    Repo = repo,
                    Tag = tag
                };
            }
            else if (result.Digest != digest)
            {
                log.LogInformation($"Tag '{repo}:{tag}' has digest diff. Updating table with new value.{Environment.NewLine}Stored: {result.Digest}{Environment.NewLine}Latest: {digest}");
                result.Digest = digest;
                var insertOperation = TableOperation.Merge(result);
                await digestsTable.ExecuteAsync(insertOperation);

                tagChangedData = new TagChangedData
                {
                    ChangeType = TagChangeType.Updated,
                    Digest = digest,
                    Repo = repo,
                    Tag = tag
                };
            }
            else
            {
                log.LogInformation($"No change to digest for '{repo}:{tag}'.");
            }

            if (tagChangedData != null)
            {
                EventNotification<TagChangedData> eventNotification = new EventNotification<TagChangedData>
                {
                    Data = tagChangedData,
                    Subject = $"{tagChangedData.Repo}:{tagChangedData.Tag}",
                    Id = Guid.NewGuid().ToString(),
                    EventTime = DateTime.UtcNow.ToString(),
                    EventType = tagChangedData.ChangeType.ToString(),
                    DataVersion = "1.0"
                };

                await SendEventNotificationAsync(log, config["TagChangedEndpoint"], config["TagChangedAccessKey"], eventNotification);
            }
        }

        private async Task<string> GetDigestAsync(ILogger log, string username, string password, string repo, string tag)
        {
            log.LogInformation($"Querying digest for '{repo}:{tag}'");
            var token = await GetRegistryAccessTokenAsync(username, password, repo);
            
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"https://registry-1.docker.io/v2/{repo}/manifests/{tag}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.docker.distribution.manifest.v2+json"));
            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var digest = response.Headers.GetValues("Docker-Content-Digest").First();
            log.LogInformation($"Digest result for '{repo}:{tag}': {digest}");
            return digest;
        }

        private async Task<string> GetRegistryAccessTokenAsync(string username, string password, string repo)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get,
                $"https://auth.docker.io/token?service=registry.docker.io&scope=repository:{repo}:pull");
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}")));
            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            string result = await response.Content.ReadAsStringAsync();
            var tokenResult = JsonConvert.DeserializeObject<RegistryTokenResult>(result);
            return tokenResult.Token;
        }

        private async Task SendEventNotificationAsync<T>(ILogger log, string topicEndpoint, string topicAccessKey, EventNotification<T> eventNotification)
        {
            log.LogInformation($"Sending event notification to '{topicEndpoint}'");

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, topicEndpoint);
            request.Content = JsonContent.Create(new object[] { eventNotification });
            request.Headers.Add("aeg-sas-key", new string[] { topicAccessKey });

            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }
    }
}
