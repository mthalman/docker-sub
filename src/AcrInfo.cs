namespace DockerSub
{
    public class AcrInfo : DockerRegistryInfo
    {
        public AcrInfo(IDockerRegistryIdentifier dockerRegistryId)
        {
            Registry = dockerRegistryId.Registry;
            Tenant = dockerRegistryId.AadTenant;
            ClientId = dockerRegistryId.AadClientId;
            ClientSecret = dockerRegistryId.AadClientSecret;
        }

        public override string RegistryType => DockerSub.RegistryType.AzureContainerRegistry;

        public override string HostName => this.Registry;

        public string Registry { get; }
        public string Tenant { get; }
        public string ClientId { get; }
        public string ClientSecret { get; }
    }
}