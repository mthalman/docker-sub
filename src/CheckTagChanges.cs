using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Extensions.Configuration;
using DockerSub.DataModel;
using System.Net.Http;
using DockerSub.RestModel;
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
        private readonly IDockerRegistryClientFactory dockerRegistryClientFactory;

        public CheckTagChanges(
            System.Net.Http.IHttpClientFactory httpClientFactory,
            IConfigurationRoot config,
            ILogger<CheckTagChanges> log,
            IDockerRegistryClientFactory dockerRegistryClientFactory)
        {
            this.httpClient = httpClientFactory.CreateClient();
            this.config = config;
            this.log = log;
            this.dockerRegistryClientFactory = dockerRegistryClientFactory;
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
            var comparer = new DockerRegistryIdentifierComparer();
            var subscriptionsByRegistry = subscriptions.GroupBy(sub => sub, comparer);

            List<Task> checkTagTasks = new List<Task>();
            foreach (var registrySubscriptions in subscriptionsByRegistry)
            {
                var registryClient = this.dockerRegistryClientFactory.CreateClient(
                    DockerRegistryInfo.Create(registrySubscriptions.Key),
                    registrySubscriptions.Select(sub => sub.Repo).ToArray());

                foreach (var subscription in registrySubscriptions)
                {
                    checkTagTasks.Add(CheckTagChangeAsync(subscription, registryClient, digestsTable));
                }
            }

            await Task.WhenAll(checkTagTasks);
        }

        private async Task CheckTagChangeAsync(
            Subscription subscription, IDockerRegistryClient registryClient, CloudTable digestsTable)
        {
            log.LogInformation($"Checking tag change for '{subscription.Repo}:{subscription.Tag}'");

            string digest = await registryClient.GetDigestAsync(subscription);

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

        private async Task SendEventNotificationAsync(ILogger log, string topicEndpoint, string topicAccessKey, EventGridEvent eventGridEvent)
        {
            log.LogInformation($"Sending event notification to '{topicEndpoint}'");

            TopicCredentials creds = new TopicCredentials(topicAccessKey);

            using EventGridClient client = new EventGridClient(creds, httpClient, disposeHttpClient: false);
            await client.PublishEventsAsync(topicEndpoint, new List<EventGridEvent> { eventGridEvent });
        }
    }
}
