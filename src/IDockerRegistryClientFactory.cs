using System.Collections.Generic;

namespace DockerSub
{
    public interface IDockerRegistryClientFactory
    {
        IDockerRegistryClient CreateClient(DockerRegistryInfo registry, IEnumerable<string> repos);
    }
}