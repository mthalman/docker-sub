using Newtonsoft.Json;

namespace DockerSub.RestModel
{
    public class OAuthExchangeResult
    {
        [JsonProperty("refresh_token")]
        public string RefreshToken { get; set; }
    }
}
