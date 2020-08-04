using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace DockerSub
{
    public class DockerRegistryIdentifierComparer : IEqualityComparer<IDockerRegistryIdentifier>
    {
        public bool Equals([AllowNull] IDockerRegistryIdentifier x, [AllowNull] IDockerRegistryIdentifier y)
        {
            return x?.RegistryType == y?.RegistryType &&
                x?.Registry == y?.Registry &&
                x?.AadTenant == y?.AadTenant &&
                x?.AadClientId == y?.AadClientId &&
                x?.AadClientSecret == y?.AadClientSecret;
        }

        public int GetHashCode([DisallowNull] IDockerRegistryIdentifier obj)
        {
            return (obj.RegistryType + obj.Registry + obj.AadTenant + obj.AadClientId + obj.AadClientSecret).GetHashCode();
        }
    }
}