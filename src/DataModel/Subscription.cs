using Microsoft.Azure.Cosmos.Table;
using static DockerSub.StringHelper;

namespace DockerSub.DataModel
{
    public class Subscription : TableEntity
    {
        public Subscription()
        {
        }

        public Subscription(string repo, string tag) : base(EncodePartitionKey(repo), tag)
        {
        }

        public string Repo => DecodePartitionKey(PartitionKey);
        public string Tag => RowKey;

        public string WebhookUrl { get; set; }
    }
}