using Newtonsoft.Json;

namespace DockerSub.RestModel
{
    public class OAuthTokenResult
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; }
    }
}
