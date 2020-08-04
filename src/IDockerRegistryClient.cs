using System.Threading.Tasks;
using DockerSub.DataModel;

namespace DockerSub
{
    public interface IDockerRegistryClient
    {
        Task<string> GetDigestAsync(Subscription subscription);
    }
}