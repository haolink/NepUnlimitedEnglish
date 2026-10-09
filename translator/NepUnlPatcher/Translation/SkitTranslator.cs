using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NepUnlPatcher;

/// <summary>
/// Writes English into the skit scripts (event_skit: battle/field barks, WIPE_TALK). 
/// Each TextAsset talk_XXXXXXXX is CSV with ';' and a header line
/// naming the columns; column 7 = name_jpn, column 14 = msg_jpn are replaced.
/// Rules: no ';' (separator), no TAB/CR, at most <see cref="MAX_LINES"/> lines (the wipe box is small).
/// </summary>
public class SkitTranslator
{
    /// <summary>Maximum number of lines per skit text (the wipe box is small).</summary>
    public const int MAX_LINES = 2;

    /// <summary>Column name_jpn (speaker name, slot 0).</summary>
    private const int NAME_COLUMN = 7;
    /// <summary>Column msg_jpn (text, slot 0).</summary>
    private const int MESSAGE_COLUMN = 14;

    /// <summary>Speaker names JP → EN (glossary.json "names").</summary>
    private readonly Dictionary<string, string> _names;

    /// <summary>
    /// Initialises a new instance of the <see cref="SkitTranslator"/> class with the given speaker names mapping.
    /// </summary>
    /// <param name="names">Speaker names JP → EN (glossary.json "names").</param>
    public SkitTranslator(Dictionary<string, string> names)
    {
        this._names = names;
    }

    /// <summary>
    /// Translates all skit scripts. <paramref name="assets"/>: TextAsset name → script;
    /// <paramref name="translations"/>: "talk_XXXXXXXX/[row]" → English.
    /// Throws <see cref="InvalidDataException"/> if a translation breaks a rule.
    /// </summary>
    /// <param name="assets">All skit scripts: TextAsset name → script.</param>
    /// <param name="translations">"talk_XXXXXXXX/[row]" → English (skit.json).</param>
    /// <returns>The changed scripts and what was (not) translated.</returns>
    public SkitResult Translate(Dictionary<string, string> assets, Dictionary<string, string> translations)
    {
        Dictionary<string, string> changed = new Dictionary<string, string>();
        HashSet<string> used = new HashSet<string>();

        foreach ((string assetName, string script) in assets)
        {
            string[] lines = script.Split("\r\n");
            bool assetChanged = false;
            for (int row = 1; row < lines.Length; row++)   // row 0 is the header
            {
                string[] f = lines[row].Split(';');
                string key = $"{assetName}/{row}";
                if (f.Length <= MESSAGE_COLUMN || f[MESSAGE_COLUMN].Length == 0 || !translations.TryGetValue(key, out string? text))
                {
                    continue;
                }

                f[MESSAGE_COLUMN] = Encode(text, key);
                f[NAME_COLUMN] = this._names.TryGetValue(f[NAME_COLUMN], out string? name) ? name : f[NAME_COLUMN];
                lines[row] = string.Join(';', f);
                used.Add(key);
                assetChanged = true;
            }

            if (assetChanged)
            {
                changed[assetName] = string.Join("\r\n", lines);
            }
        }

        List<string> unknown = translations.Keys.Where(k => !used.Contains(k)).Order(StringComparer.Ordinal).ToList();
        return new SkitResult(changed, used.Count, unknown);
    }

    /// <summary>Checks the rules and turns real newlines into the script's literal "\n".</summary>
    /// <param name="text">The English text (real newlines).</param>
    /// <param name="where">Key for error messages.</param>
    /// <returns>The text as it goes into the script.</returns>
    private static string Encode(string text, string where)
    {
        if (text.Contains(';') || text.Contains('\t') || text.Contains('\r'))
        {
            throw new InvalidDataException($"{where}: \";\", tab or CR in translation: {text}");
        }

        if (text.Count(c => c == '\n') >= MAX_LINES)
        {
            throw new InvalidDataException($"{where}: more than {MAX_LINES} lines: {text}");
        }

        return text.Replace("\n", "\\n");
    }
}

/// <param name="Changed">Translated scripts by TextAsset name (only assets with changes).</param>
/// <param name="Lines">Number of translated lines.</param>
/// <param name="Unknown">Keys in the translation file that do not exist in the skits.</param>
public record SkitResult(Dictionary<string, string> Changed, int Lines, List<string> Unknown);
