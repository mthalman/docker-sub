namespace DockerSub.RestModel
{
    public class SubscriptionRequest
    {
        public string Repo { get; set; }
        public string Tag { get; set; }
        public string WebhookUrl { get; set; }
    }
}