namespace DockerSub.RestModel
{
    public class Registry
    {
        public string Name { get; set; }
        
        public string Type { get; set; }

        public string AadTenant { get; set; }

        public string AadClientId { get; set; }

        public string AadClientSecret { get; set; }
    }
}