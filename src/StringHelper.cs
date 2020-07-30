namespace DockerSub
{
    internal static class StringHelper
    {
        public static string EncodeTableKey(string tableKey)
        {
            return tableKey.Replace("/", "_");
        }

        public static string DecodeTableKey(string tableKey)
        {
            return tableKey.Replace("_", "/");
        }
    }
}