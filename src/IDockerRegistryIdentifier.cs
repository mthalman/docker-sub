namespace DockerSub
{
    public interface IDockerRegistryIdentifier
    {
        string RegistryType { get; }
        string Registry { get; }
        string AadTenant { get; }
        string AadClientId { get; }
        string AadClientSecret { get; }
    }
}