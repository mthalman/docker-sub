using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Extensions.Configuration;
using DockerSub.DataModel;
using System.Net.Http;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.Management.EventGrid;
using Microsoft.Azure.Management.EventGrid.Models;
using DockerSub.RestModel;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using System.Collections.Generic;

namespace DockerSub
{
    public class AddSubscription
    {
        private readonly HttpClient httpClient;
        private readonly IConfigurationRoot config;
        private readonly ILogger log;

        public AddSubscription(IHttpClientFactory httpClientFactory, IConfigurationRoot config, ILogger<CheckTagChanges> log)
        {
            this.httpClient = httpClientFactory.CreateClient();
            this.config = config;
            this.log = log;
        }

        [FunctionName(nameof(AddSubscription))]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequestMessage req,
            ExecutionContext context)
        {
            var subscriptionRequest = await req.Content.ReadAsAsync<SubscriptionRequest>();

            log.LogInformation($"Adding subscription for '{subscriptionRequest.WebhookUrl}' to '{subscriptionRequest.Repo}:{subscriptionRequest.Tag}'");

            Subscription subscription = new Subscription(subscriptionRequest.Repo, subscriptionRequest.Tag)
            {
                WebhookUrl = subscriptionRequest.WebhookUrl,
                RegistryName = subscriptionRequest.Registry.Name,
                RegistryType = subscriptionRequest.Registry.Type,
                AadTenant = subscriptionRequest.Registry.AadTenant,
                AadClientId = subscriptionRequest.Registry.AadClientId,
                AadClientSecret = subscriptionRequest.Registry.AadClientSecret
            };

            await CreateEventGridSubscriptionAsync(subscription);
            await PersistSubscriptionAsync(subscription);

            return new OkObjectResult("OK");
        }

        private async Task PersistSubscriptionAsync(Subscription subscription)
        {
            log.LogInformation("Saving subscription data to table storage");

            string storageConnectionString = config["AzureWebJobsStorage"];
            
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageConnectionString);
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CloudTable subscriptionsTable = tableClient.GetTableReference("subscriptions");
            await subscriptionsTable.CreateIfNotExistsAsync();

            var insertOperation = TableOperation.Insert(subscription);
            await subscriptionsTable.ExecuteAsync(insertOperation);
        }

        private async Task CreateEventGridSubscriptionAsync(Subscription subscription)
        {
            log.LogInformation("Creating event grid subscription");
            AzureCredentials azureCreds = SdkContext.AzureCredentialsFactory.FromServicePrincipal(config["DockerSubAppClientId"], config["DockerSubAppSecret"], config["DockerSubAppTenant"], AzureEnvironment.AzureGlobalCloud);

            using EventGridManagementClient managementClient = new EventGridManagementClient(azureCreds);
            managementClient.SubscriptionId = config["AzureSubscription"];
            string subscriberId = System.Guid.NewGuid().ToString("N");
            
            var result = await managementClient.EventSubscriptions.CreateOrUpdateAsync(
                $"/subscriptions/{managementClient.SubscriptionId}/resourceGroups/{config["AzureResourceGroup"]}/providers/Microsoft.EventGrid/topics/docker-sub-tag-changed",
                subscriberId,
                new EventSubscription(
                    topic: "docker-sub-tag-changed",
                    destination: new WebHookEventSubscriptionDestination(subscription.WebhookUrl),
                    eventDeliverySchema: "EventGridSchema",
                    filter: new EventSubscriptionFilter(advancedFilters: new List<AdvancedFilter>
                    {
                        new StringInAdvancedFilter("Subject", new List<string> { $"{subscription.Repo}:{subscription.Tag}" })
                    }))
            );
        }
    }
}
