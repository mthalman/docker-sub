using Microsoft.Azure.Cosmos.Table;
using static DockerSub.StringHelper;

namespace DockerSub.DataModel
{
    public class Subscription : TableEntity
    {
        public Subscription()
        {
        }

        public Subscription(string id, string registry, string repo, string tag, string webhookUrl)
            : base(id, GetRowKey(registry, repo, tag, webhookUrl))
        {
        }

        public string Id => PartitionKey;
        public string Registry => ParseRowKey(RowKey).Registry;
        public string Repo => ParseRowKey(RowKey).Repo;
        public string Tag => ParseRowKey(RowKey).Tag;
        public string WebhookUrl => ParseRowKey(RowKey).WebhookUrl;

        public string RegistryType { get; set; }

        public string AadTenant { get; set; }

        public string AadClientId { get; set; }

        public string AadClientSecret { get; set; }

        public static (string Registry, string Repo, string Tag, string WebhookUrl) ParseRowKey(string partitionKey)
        {
            var parts = DecodeTableKey(partitionKey).Split("+");
            return (parts[0], parts[1], parts[2], parts[3]);
        }

        public static string GetRowKey(string registry, string repo, string tag, string webhookUrl)
            => EncodeTableKey($"{registry}+{repo}+{tag}+{webhookUrl}");
    }
}