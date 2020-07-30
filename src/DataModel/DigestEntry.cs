using Microsoft.Azure.Cosmos.Table;
using static DockerSub.StringHelper;

namespace DockerSub.DataModel
{
    public class DigestEntry : TableEntity
    {
        public DigestEntry()
        {
        }

        public DigestEntry(string registry, string repo, string tag)
            : base(GetPartitionKey(registry, repo), tag)
        {
        }

        public string Repo => ParsePartitionKey(PartitionKey).Repo;
        public string Tag => ParsePartitionKey(PartitionKey).Tag;

        public string Digest { get; set; }

        public static string GetPartitionKey(string registry, string repo)
            => EncodeTableKey($"{registry}+{repo}");

        public static (string Repo, string Tag) ParsePartitionKey(string partitionKey)
        {
            var parts = DecodeTableKey(partitionKey).Split("+");
            return (parts[0], parts[1]);
        }

        public static (string PartitionKey, string RowKey) GetKeys(string registry, string repo, string tag)
            => (GetPartitionKey(registry, repo), tag);
    }
}