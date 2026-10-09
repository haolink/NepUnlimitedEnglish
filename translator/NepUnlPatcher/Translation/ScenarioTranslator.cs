using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace NepUnlPatcher;

/// <summary>
/// Writes English into an event_scenario script.
/// The script is TSV: one command per line, fields separated by TAB, lines
/// separated by CRLF. Only the displayed text slot and the speaker name slot
/// are replaced; everything else stays byte for byte.
/// </summary>
public partial class ScenarioTranslator
{
    /// <summary>Maximum number of lines per text (the text box has no 4th line).</summary>
    public const int MAX_LINES = 3;

    /// <summary>Line separator for the script (CRLF).</summary>
    private const string LINE_SEPARATOR = "\r\n";

    /// <summary>Field separator for the script (TAB).</summary>
    private const char FIELD_SEPARATOR = '\t';

    /// <param name="names">Speaker names JP → EN (glossary.json "names").</param>
    private readonly Dictionary<string, string> _names;

    /// <summary>
    /// Initialises a new instance of the <see cref="ScenarioTranslator"/> class.
    /// </summary>
    /// <param name="names">Speaker names JP → EN (glossary.json "names").</param>
    public ScenarioTranslator(Dictionary<string, string> names)
    {
        this._names = names;
    }

    /// <summary>
    /// Translates <paramref name="script"/>. With <paramref name="mark"/>, the first text line
    /// of the scene gets the prefix "[[scene no]] " to locate crashes in-game.
    /// Throws <see cref="InvalidDataException"/> if a translation breaks a rule.
    /// </summary>
    /// <param name="script">The scene script (m_Script of scenario_[scene id]).</param>
    /// <param name="translations">Translation key → English (scenario/[scene id].json).</param>
    /// <param name="sceneId">Scene id, e.g. "00000050" (for messages and the marker).</param>
    /// <param name="mark">True: put the scene marker before the first text line.</param>
    /// <returns>The translated script and what was (not) translated.</returns>
    public ScenarioResult Translate(string script, Dictionary<string, string> translations, string sceneId, bool mark)
    {
        string[] lines = script.Split(LINE_SEPARATOR);
        HashSet<string> seen = new HashSet<string>();
        List<string> missing = new List<string>();
        SortedSet<string> unnamed = new SortedSet<string>(StringComparer.Ordinal);

        foreach ((int index, string key, int textField, int? nameField) in TextFields(lines))
        {
            string[] fields = lines[index].Split(FIELD_SEPARATOR);
            if (seen.Contains(key))
            {
                throw new InvalidDataException($"{sceneId}: duplicate key {key}");
            }

            if (translations.TryGetValue(key, out string? text))
            {
                fields[textField] = Encode(text, fields[textField], $"{sceneId}/{key}");
                if (nameField is int n)
                {
                    if (this._names.TryGetValue(fields[n], out string? name))
                    {
                        fields[n] = name;
                    }
                    else if (fields[n].Length > 0)
                    {
                        unnamed.Add(fields[n]);
                    }
                }
            }
            else
            {
                missing.Add(key);
            }

            if (mark && seen.Count == 0)
            {
                // marker only on the scene's first text line: "[50] " (enough to locate crashes)
                fields[textField] = $"[{int.Parse(sceneId)}] " + fields[textField];
            }

            seen.Add(key);
            lines[index] = string.Join(FIELD_SEPARATOR, fields);
        }

        List<string> unknown = translations.Keys.Where(k => !seen.Contains(k)).OrderBy(SortKey).ToList();
        return new ScenarioResult(string.Join(LINE_SEPARATOR, lines), seen.Count - missing.Count, missing, unknown, unnamed.ToList());
    }

    /// <summary>
    /// Every line that carries displayed text: (line index, translation key, text field index,
    /// name field index or null).
    /// </summary>
    /// <param name="lines">The script split into lines.</param>
    /// <returns>One entry per text line, in script order.</returns>
    private static IEnumerable<(int Index, string Key, int TextField, int? NameField)> TextFields(string[] lines)
    {
        int novel = 0;
        int question = 0;
        int answer = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            string[] f = lines[i].Split(FIELD_SEPARATOR);
            string command = f[0].TrimStart('﻿');   // the first line starts with the BOM
            if (command is "eTALK_SET_ALL" or "CINEMA_SUBTITLES")
            {
                if (f.Length > 12 && f[12].Length > 0)
                {
                    yield return (i, f[1], 12, 5);
                }
            }
            else if (command == "eNOVEL_SET_ALL")
            {
                if (f.Length > 3 && f[3].Length > 0)
                {
                    yield return (i, $"N{++novel}", 3, null);
                }
            }
            else if (command == "eUI_SELECT_SET_QUESTION")
            {
                if (f.Length > 1 && f[1].Length > 0)
                {
                    yield return (i, $"Q{++question}", 1, null);
                }
            }
            else if (command == "eUI_SELECT_SET_ANSWER")
            {
                if (f.Length > 2 && f[2].Length > 0)
                {
                    yield return (i, $"A{++answer}", 2, null);
                }
            }
        }
    }

    /// <summary>Checks the rules and turns real newlines into the script's literal "\n".</summary>
    /// <param name="text">The English text (real newlines).</param>
    /// <param name="original">The Japanese text it replaces (for the [word] tag check).</param>
    /// <param name="where">Scene/key for error messages.</param>
    /// <returns>The text as it goes into the script.</returns>
    private static string Encode(string text, string original, string where)
    {
        if (text.Contains('\t') || text.Contains('\r'))
        {
            throw new InvalidDataException($"{where}: tab/CR in translation: {text}");
        }

        if (text.Count(c => c == '\n') >= MAX_LINES)
        {
            throw new InvalidDataException($"{where}: more than {MAX_LINES} lines: {text}");
        }

        if (!WordTagIds(text).SequenceEqual(WordTagIds(original))
            || CountOf(text, "<word=") != CountOf(text, "</word>"))
        {
            throw new InvalidDataException($"{where}: <word> tags differ from original: {text}");
        }

        return text.Replace("\n", "\\n");
    }

    /// <summary>The ids of all [word=ID] tags in a text, sorted.</summary>
    /// <param name="text">The text to search.</param>
    /// <returns>The ids (digits only), sorted ordinally.</returns>
    private static List<string> WordTagIds(string text) 
    {
        return WordTag().Matches(text).Select(m => m.Groups[1].Value).Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>How often <paramref name="part"/> occurs in <paramref name="text"/> (like Python's str.count).</summary>
    /// <param name="text">The text to search.</param>
    /// <param name="part">The substring to count.</param>
    /// <returns>Number of non-overlapping occurrences.</returns>
    private static int CountOf(string text, string part)
    {
        return (text.Length - text.Replace(part, "").Length) / part.Length;
    }

    /// <summary>Line numbers first (numeric), then N/Q/A keys – same order as sort_key in Python.</summary>
    /// <param name="key">A translation key: "[line no]", "N1", "Q1", "A1", …</param>
    /// <returns>A sortable tuple.</returns>
    private static (int, string, int) SortKey(string key)
    {
        return key.All(char.IsAsciiDigit) ? (0, "", int.Parse(key)) : (1, key[..1], int.Parse(key[1..]));
    }

    /// <summary>A [word=ID] tag; group 1 is the id.</summary>
    /// <returns>The compiled regular expression.</returns>
    [GeneratedRegex(@"<word=(\d+)>")]
    private static partial Regex WordTag();
}

/// <param name="Script">The translated script.</param>
/// <param name="Translated">Number of text lines that got a translation.</param>
/// <param name="Missing">Keys in the script without translation (stay Japanese).</param>
/// <param name="Unknown">Keys in the translation file that do not exist in the script.</param>
/// <param name="Unnamed">Speaker names without an entry in the glossary (stay Japanese).</param>
public record ScenarioResult(string Script, int Translated, List<string> Missing, List<string> Unknown, List<string> Unnamed);
