namespace DockerSub
{
    public class DockerHubRegistryInfo : DockerRegistryInfo
    {
        public override string RegistryType => DockerSub.RegistryType.DockerHub;

        public override string HostName => "registry-1.docker.io";
    }
}