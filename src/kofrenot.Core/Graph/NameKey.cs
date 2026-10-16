using System.Globalization;
using System.Text;

namespace Kofrenot.Core.Graph;

/// <summary>
/// The form under which names are compared: case, accents and extra spaces do not count
/// ("  Émile   Zola " and "emile zola" are the same name). Used for comparison only: names are
/// stored as typed.
/// </summary>
public static class NameKey
{
    public static string Of(string name)
    {
        var key = new StringBuilder(name.Length);
        var spacePending = false;
        foreach (var character in name.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                spacePending = key.Length > 0;
                continue;
            }

            if (spacePending)
            {
                key.Append(' ');
                spacePending = false;
            }

            key.Append(char.ToLowerInvariant(character));
        }

        return key.ToString();
    }
}
