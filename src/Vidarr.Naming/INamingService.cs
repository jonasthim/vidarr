using Vidarr.Contracts.Models;

namespace Vidarr.Naming;

public interface INamingService
{
    string BuildRelativePath(NamingInput input, NamingConfig config);
}
