using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace NepUnlPatcher;

/// <summary>
/// The layout of the translations directory (-t). All files are UTF-8 JSON with real newlines
/// inside the texts; all are objects "key": "English text", except source/mtext.json (a list).
/// </summary>
public sealed class TranslationFiles
{
    /// <summary>The translations directory.</summary>
    public string Root { get; }

    /// <summary>
    /// New instance of <see cref="TranslationFiles"/> with the specified translations directory.
    /// </summary>
    /// <param name="root">The translations directory.</param>
    public TranslationFiles(string root)
    {
        this.Root = root;
    }

    /// <summary>Speaker names JP → EN (glossary.json, section "names").</summary>
    /// <returns>Japanese name → English name.</returns>
    public Dictionary<string, string> LoadNames()
    {
        JObject glossary = JObject.Parse(File.ReadAllText(Path.Combine(this.Root, "glossary.json")));
        return glossary["names"]?.ToObject<Dictionary<string, string>>()
            ?? throw new InvalidDataException("glossary.json has no \"names\" section");
    }

    /// <summary>Scene ids that have a translation file, sorted.</summary>
    /// <returns>Scene ids like "00000050".</returns>
    public List<string> GetSceneIds() 
    {
        return Directory.GetFiles(Path.Combine(this.Root, "scenario"), "*.json")
            .Select(p => Path.GetFileNameWithoutExtension(p))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Translations of one scene (scenario/[scene id].json).</summary>
    /// <param name="sceneId">Scene id like "00000050".</param>
    /// <returns>Translation key → English.</returns>
    public Dictionary<string, string> LoadScene(string sceneId)
    {
        return this.LoadMap(Path.Combine(this.Root, "scenario", $"{sceneId}.json"));
    }

    /// <summary>Translations of the skits (skit.json).</summary>
    /// <returns>"talk_XXXXXXXX/[row]" → English.</returns>
    public Dictionary<string, string> LoadSkit()
    {
        return this.LoadMap(Path.Combine(this.Root, "skit.json"));
    }

    /// <summary>String databases that have at least one translation file, sorted.</summary>
    /// <returns>Database names like "strdatabase".</returns>
    public List<string> GetStringDatabases()
    {
        return Directory.GetFiles(Path.Combine(this.Root, "str"), "*.json")
            .Select(p => Path.GetFileName(p).Split('.')[0])
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Translations of one string database:
    /// db files and db part files are by id, db text by Japanese text.
    /// A key that occurs in two files of the same kind is an error.
    /// </summary>
    /// <param name="db">Database name like "strdatabase".</param>
    /// <returns>Translations by id and by Japanese text.</returns>
    public StringDatabaseTranslations LoadStringDatabase(string db)
    {
        Dictionary<string, string> byId = new Dictionary<string, string>();
        Dictionary<string, string> byText = new Dictionary<string, string>();
        IEnumerable<string> files = Directory.GetFiles(Path.Combine(this.Root, "str"), "*.json")
            .Where(p => Path.GetFileName(p).StartsWith($"{db}.", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);
        foreach (string file in files)
        {
            bool isByText = Path.GetFileName(file).Split('.')[1].StartsWith("text", StringComparison.Ordinal);
            Dictionary<string, string> target = isByText ? byText : byId;
            foreach ((string key, string value) in this.LoadMap(file))
            {
                if (!target.TryAdd(key, value))
                {
                    throw new InvalidDataException($"{file}: already translated elsewhere: {key}");
                }
            }
        }

        return new StringDatabaseTranslations(byId, byText);
    }

    /// <summary>
    /// Where the hard-coded UI labels are (source/mtext.json): a list of
    /// {"file": bundle file name, "pathId": path id of the TextMeshPro MonoBehaviour, "text": Japanese label}.
    /// </summary>
    /// <returns>Bundle file name → path id → Japanese label, sorted by bundle name.</returns>
    public Dictionary<string, Dictionary<long, string>> LoadLabelSources()
    {
        string path = Path.Combine(this.Root, "source", "mtext.json");
        SortedDictionary<string, Dictionary<long, string>> result = new SortedDictionary<string, Dictionary<long, string>>(StringComparer.Ordinal);
        foreach (JToken entry in JArray.Parse(File.ReadAllText(path)))
        {
            string file = (string?)entry["file"] ?? throw new InvalidDataException($"{path}: entry without \"file\": {entry}");
            long pathId = (long?)entry["pathId"] ?? throw new InvalidDataException($"{path}: entry without \"pathId\": {entry}");
            string text = (string?)entry["text"] ?? throw new InvalidDataException($"{path}: entry without \"text\": {entry}");

            if (!result.TryGetValue(file, out Dictionary<long, string>? labels))
            {
                labels = new Dictionary<long, string>();
                result[file] = labels;
            }

            labels[pathId] = text;
        }

        return result.ToDictionary(e => e.Key, e => e.Value);
    }

    /// <summary>UI label translations Japanese → English (mtext.json).</summary>
    /// <returns>Japanese label → English.</returns>
    public Dictionary<string, string> LoadLabels()
    {
        return this.LoadMap(Path.Combine(this.Root, "mtext.json"));
    }

    /// <summary>Reads a JSON object of strings ("key": "text").</summary>
    /// <param name="path">Path of the JSON file.</param>
    /// <returns>Key → text.</returns>
    private Dictionary<string, string> LoadMap(string path)
    {
        return JObject.Parse(File.ReadAllText(path)).ToObject<Dictionary<string, string>>()
            ?? throw new InvalidDataException($"{path}: not a JSON object");
    }
}

/// <param name="ById">[id] → English.</param>
/// <param name="ByText">Japanese text → English, for rows without an id entry.</param>
public sealed record StringDatabaseTranslations(Dictionary<string, string> ById, Dictionary<string, string> ByText);
