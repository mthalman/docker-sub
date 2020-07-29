using Microsoft.Azure.Cosmos.Table;
using static DockerSub.StringHelper;

namespace DockerSub.DataModel
{
    public class DigestEntry : TableEntity
    {
        public DigestEntry()
        {
        }

        public DigestEntry(string repo, string tag) : base(EncodePartitionKey(repo), tag)
        {
        }

        public string Repo => DecodePartitionKey(PartitionKey);
        public string Tag => RowKey;

        public string Digest { get; set; }
    }
}