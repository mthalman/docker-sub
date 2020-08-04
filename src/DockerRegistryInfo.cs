using System;

namespace DockerSub
{
    public abstract class DockerRegistryInfo
    {
        public abstract string RegistryType { get; }
        public abstract string HostName { get; }

        public static DockerRegistryInfo Create(IDockerRegistryIdentifier dockerRegistryId)
        {
            switch (dockerRegistryId.RegistryType)
            {
                case DockerSub.RegistryType.DockerHub:
                    return new DockerHubRegistryInfo();
                case DockerSub.RegistryType.AzureContainerRegistry:
                    return new AcrInfo(dockerRegistryId);
                default:
                    throw new NotSupportedException($"Unknown registry type: {dockerRegistryId.RegistryType}");
            }
        }
    }
}