namespace DockerSub
{
    internal static class StringHelper
    {
        public static string EncodePartitionKey(string partitionKey)
        {
            return partitionKey.Replace("/", "_");
        }

        public static string DecodePartitionKey(string partitionKey)
        {
            return partitionKey.Replace("_", "/");
        }
    }
}