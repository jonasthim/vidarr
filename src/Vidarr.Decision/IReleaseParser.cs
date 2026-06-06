using Vidarr.Contracts.Models;

namespace Vidarr.Decision;

public interface IReleaseParser
{
    ParsedReleaseInfo Parse(string releaseTitle);
}
