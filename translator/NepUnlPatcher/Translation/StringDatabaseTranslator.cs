using System;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace NepUnlPatcher;

/// <summary>
/// Writes English into a string database (database_str*, a StrDatabase MonoBehaviour)
/// Every row of "datas_" has an "id_" and a "jp_text_"; the
/// English goes into jp_text_ ("pretend everything is Japanese").
/// A row is translated by its id, otherwise by its Japanese text.
/// Rule: placeholders ({0}, #NAME, [key=…], [word=…], [color…], …) must match the original.
/// </summary>
public static partial class StringDatabaseTranslator
{
    /// <summary>
    /// Translates <paramref name="db"/> (the MonoBehaviour as JSON) in place.
    /// Throws <see cref="InvalidDataException"/> if a translation breaks a rule.
    /// </summary>
    /// <param name="db">The string database MonoBehaviour as JSON; changed in place.</param>
    /// <param name="translations">Translations by id and by Japanese text.</param>
    /// <param name="dbName">Name of the database, e.g. "strdatabase" (for messages).</param>
    /// <returns>Counts and leftovers, for the log.</returns>
    public static StringDatabaseResult Translate(JObject db, StringDatabaseTranslations translations, string dbName)
    {
        JArray rows = db["datas_"] as JArray ?? throw new InvalidDataException($"{dbName}: no \"datas_\" list");
        HashSet<string> usedIds = new HashSet<string>();
        HashSet<string> usedTexts = new HashSet<string>();
        int translated = 0;
        int japaneseLeft = 0;

        foreach (JToken row in rows)
        {
            string id = ((long)row["id_"]!).ToString();
            string original = (string)row["jp_text_"]!;
            if (translations.ById.TryGetValue(id, out string? byId))
            {
                CheckPlaceholders(byId, original, $"{dbName}/{id}");
                row["jp_text_"] = byId;
                usedIds.Add(id);
                translated++;
            }
            else if (translations.ByText.TryGetValue(original, out string? byText))
            {
                CheckPlaceholders(byText, original, $"{dbName}/{id}");
                row["jp_text_"] = byText;
                usedTexts.Add(original);
                translated++;
            }
            else if (Japanese().IsMatch(original))
            {
                japaneseLeft++;
            }
        }

        List<string> unknownIds = translations.ById.Keys.Where(k => !usedIds.Contains(k)).Order(StringComparer.Ordinal).ToList();
        int unusedTexts = translations.ByText.Keys.Count(k => !usedTexts.Contains(k));
        return new StringDatabaseResult(translated, japaneseLeft, unknownIds, unusedTexts);
    }

    /// <summary>Throws if the translation does not have exactly the placeholders of the original.</summary>
    /// <param name="text">The English text.</param>
    /// <param name="original">The Japanese text it replaces.</param>
    /// <param name="where">Database/id for error messages.</param>
    private static void CheckPlaceholders(string text, string original, string where)
    {
        List<string> expected = Placeholders(original);
        if (!Placeholders(text).SequenceEqual(expected))
        {
            throw new InvalidDataException($"{where}: placeholders differ: [{string.Join(", ", expected)}] -> {text}");
        }
    }

    /// <summary>All placeholders in a text, sorted.</summary>
    /// <param name="text">The text to search.</param>
    /// <returns>The placeholders as written, sorted ordinally.</returns>
    private static List<string> Placeholders(string text) =>
        Placeholder().Matches(text).Select(m => m.Value).Order(StringComparer.Ordinal).ToList();

    /// <summary>{0}, #ABILITY_POWER, [key=…], [color=…], [/color], ….</summary>
    /// <returns>The compiled regular expression.</returns>
    [GeneratedRegex(@"\{\d+\}|#[A-Z_]+|<[a-z_]+(?:=[^>]*)?>|</[a-z_]+>")]
    private static partial Regex Placeholder();

    /// <summary>Hiragana/katakana, CJK ideographs, full-width forms.</summary>
    /// <returns>The compiled regular expression.</returns>
    [GeneratedRegex("[぀-ヿ㐀-鿿＀-￯]")]
    private static partial Regex Japanese();
}

/// <param name="Translated">Number of rows that got a translation.</param>
/// <param name="JapaneseLeft">Rows without translation that still contain Japanese.</param>
/// <param name="UnknownIds">Ids in the translation files that do not exist in the database.</param>
/// <param name="UnusedTexts">Entries of the by-text files that matched no row.</param>
public record StringDatabaseResult(int Translated, int JapaneseLeft, List<string> UnknownIds, int UnusedTexts);
