using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DockerSub.RestModel
{
    public class TagChangedData
    {
        public string SubscriptionId { get; set; }
        public string Registry { get; set; }
        public string Repo { get; set; }
        public string Tag { get; set; }
        public string Digest { get; set; }
        public TagChangeType ChangeType { get; set; }
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum TagChangeType
    {
        New,
        Updated
    }
}