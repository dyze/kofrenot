using Kofrenot.Core.Graph;
using Kofrenot.Core.Storage;

namespace Kofrenot.Core.Imports;

/// <summary>
/// What an import from a vault reads of a vault's content: whether a version's file is here, a note's
/// text (null when not here), and whether what is not here can be downloaded (a server vault's).
/// </summary>
public sealed record VaultContent(Func<ContentEdited, bool> Has, Func<Guid, string?> NoteText, bool CanDownload = false)
{
    public static VaultContent Of(Vault vault) => new(vault.HasContent, vault.Notes.Read, vault.FetchesContent);
}
