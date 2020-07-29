namespace DockerSub.RestModel
{
    public class EventNotification<T>
    {
        public string Topic { get; set; }
        public string Subject { get; set; }
        public string Id { get; set; }
        public string EventType { get; set; }
        public string EventTime { get; set; }
        public T Data { get; set; }
        public string DataVersion { get; set; }
        public string MetadataVersion { get; set; }
    }
}