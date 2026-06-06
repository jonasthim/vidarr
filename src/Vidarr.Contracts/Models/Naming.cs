namespace Vidarr.Contracts.Models;

public sealed record NamingConfig(
    string ArtistFolderTemplate,
    string FileTemplate,
    bool ReplaceIllegalCharacters,
    char IllegalCharacterReplacement)
{
    public static NamingConfig Default { get; } = new(
        ArtistFolderTemplate: "{Artist Name}",
        FileTemplate: "{Artist Name} - {Title} ({Year}) [{Quality Full}]",
        ReplaceIllegalCharacters: true,
        IllegalCharacterReplacement: '_');
}

public sealed record NamingInput(
    string ArtistName,
    string Title,
    int? Year,
    Quality Quality,
    string Extension,
    IReadOnlyDictionary<string, string>? ExtraTokens = null);
