using System;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;

namespace NepUnlPatcher;

/// <summary>
/// Applies all translations: reads the original bundles from the input directory, writes the
/// patched bundles (same file names) to the output directory. Covers the four text sources:
/// scenes, skits, string databases, UI labels.
/// <para>
/// One log line per bundle: "[file]... " then, if checksums are given and the input's SHA1
/// differs (or is not listed), "WARN: hash mismatch, trying anyway... " / "WARN: missing checksum,
/// trying anyway... " (patching continues), then "Success (…)" or "[EXCEPTION] …".
/// A failing bundle is skipped; the others are still written.
/// </para>
/// </summary>
public class Patcher
{
    /// <summary>Common end of all bundle file names of this game.</summary>
    private const string BUNDLE_SUFFIX = "__qual_high_assets_all.bundle";

    /// <summary>
    /// Scene markers "[50] " on the first text line of every scene. Kept on purpose (owner's
    /// decision): they locate crashes in-game.
    /// </summary>
    public bool Markers { get; init; } = true;

    /// <summary>Bundles not listed in the checksums file.</summary>
    private List<string> _missingChecksums = new List<string>();

    /// <summary>Bundles whose SHA1 differs from the checksums file.</summary>
    private List<string> _hashMismatches = new List<string>();

    /// <summary>Bundles that could not be patched.</summary>
    private List<string> _failed = new List<string>();

    /// <summary>Directory with the original bundles (only read).</summary>
    private string _inputDir;
    /// <summary>Directory for the patched bundles (same file names as the originals).</summary>
    private string _outputDir;
    /// <summary>The translation files (-t).</summary>
    private TranslationFiles _translations;
    /// <summary>Expected SHA1 by bundle file name (case-insensitive, see <see cref="Checksums"/>); null = no check.</summary>
    private Dictionary<string, string>? _expectedHashes;
    /// <summary>Receives one line per bundle.</summary>
    private TextWriter _log;

    /// <param name="inputDir">Directory with the original bundles.</param>
    /// <param name="outputDir">Directory for the translated bundles.</param>
    /// <param name="translations">The translation files (-t).</param>
    /// <param name="expectedHashes">Expected SHA1 by bundle file name (case-insensitive, see <see cref="Checksums"/>); null = no check.</param>
    /// <param name="log">Receives one line per bundle.</param>
    public Patcher(string inputDir, string outputDir, TranslationFiles translations, Dictionary<string, string>? expectedHashes, TextWriter log)
    {
        this._inputDir = inputDir;
        this._outputDir = outputDir;
        this._translations = translations;
        this._expectedHashes = expectedHashes;
        this._log = log;
    }

    /// <summary>Patches everything: scenes, skits, string databases, UI labels.</summary>
    /// <returns>Which bundles had checksum problems or failed.</returns>
    public PatchSummary Run()
    {
        Directory.CreateDirectory(this._outputDir);
        this.PatchScenes();
        this.PatchSkits();
        this.PatchStringDatabases();
        this.PatchLabels();
        return new PatchSummary(this._missingChecksums.ToList(), this._hashMismatches.ToList(), this._failed.ToList());
    }

    /// <summary>Translates every scene that has a file in scenario/ (event_scenario_* bundles).</summary>
    private void PatchScenes()
    {
        ScenarioTranslator translator = new ScenarioTranslator(this._translations.LoadNames());
        foreach (string sceneId in this._translations.GetSceneIds())
        {
            this.Patch($"event_scenario_{sceneId}{BUNDLE_SUFFIX}", (bundle) =>
            {
                string assetName = $"scenario_{sceneId}";
                ScenarioResult result = translator.Translate(bundle.GetTextAssets()[assetName], this._translations.LoadScene(sceneId), sceneId, this.Markers);
                bundle.UpdateTextAsset(assetName, result.Script);
                return $"{result.Translated} lines translated"
                    + Listed("untranslated", result.Missing)
                    + Listed("unknown line numbers in json", result.Unknown)
                    + Listed("names without translation", result.Unnamed);
            });
        }
    }

    /// <summary>Translates the skit bundle (event_skit) from skit.json.</summary>
    private void PatchSkits()
    {
        SkitTranslator translator = new SkitTranslator(this._translations.LoadNames());
        this.Patch($"event_skit{BUNDLE_SUFFIX}", (bundle) =>
        {
            SkitResult result = translator.Translate(bundle.GetTextAssets(), this._translations.LoadSkit());
            foreach ((string assetName, string script) in result.Changed)
            {
                bundle.UpdateTextAsset(assetName, script);
            }

            return $"{result.Lines} lines in {result.Changed.Count} assets" + Listed("unknown keys", result.Unknown);
        });
    }

    /// <summary>Translates every string database that has files in str/ (database_str* bundles).</summary>
    private void PatchStringDatabases()
    {
        foreach (string db in this._translations.GetStringDatabases())
        {
            this.Patch($"database_{db}{BUNDLE_SUFFIX}", (bundle) =>
            {
                long id = bundle.GetMonoBehaviourIds().Single();
                JObject data = bundle.GetMonoBehaviour(id);
                StringDatabaseResult result = StringDatabaseTranslator.Translate(data, this._translations.LoadStringDatabase(db), db);
                bundle.UpdateMonoBehaviour(id, data);
                return $"{result.Translated} strings, {result.JapaneseLeft} Japanese left"
                    + Listed("unknown ids", result.UnknownIds)
                    + (result.UnusedTexts > 0 ? $", {result.UnusedTexts} unused text entries" : "");
            });
        }
    }

    /// <summary>Translates the hard-coded UI labels listed in source/mtext.json.</summary>
    private void PatchLabels()
    {
        Dictionary<string, string> labels = this._translations.LoadLabels();
        foreach ((string bundleName, Dictionary<long, string> sources) in this._translations.LoadLabelSources())
        {
            this.Patch(bundleName, bundle =>
            {
                foreach ((long id, string japanese) in sources)
                {
                    JObject label = bundle.GetMonoBehaviour(id);
                    LabelTranslator.Translate(label, japanese, labels, $"{bundleName}/{id}");
                    bundle.UpdateMonoBehaviour(id, label);
                }

                return $"{sources.Count} labels";
            });
        }
    }

    /// <summary>
    /// Checks the SHA1 of one original bundle, lets <paramref name="translate"/> change it,
    /// saves it to the output directory and logs the result. Never throws: a failure is logged
    /// and recorded, and the caller continues with the next bundle.
    /// </summary>
    /// <param name="bundleName">File name of the bundle, the same in input and output directory.</param>
    /// <param name="translate">Changes the opened bundle; returns a short summary for the log.</param>
    private void Patch(string bundleName, Func<UnityBundle, string> translate)
    {
        this._log.Write($"{bundleName}... ");
        try
        {
            string input = Path.Combine(this._inputDir, bundleName);
            this.CheckHash(bundleName, input);

            UnityBundle bundle = new UnityBundle(input);
            string summary = translate(bundle);
            SaveResult saved = bundle.Save(Path.Combine(this._outputDir, bundleName));
            string packing = saved.Packing == BundlePacking.Lz4Hc ? "LZ4HC" : "uncompressed";
            this._log.WriteLine($"Success ({summary}, {saved.Size}/{saved.Limit} bytes {packing})");
        }
        catch (Exception e)   // any problem with this bundle: report it, continue with the next one
        {
            this._failed.Add(bundleName);
            this._log.WriteLine($"[EXCEPTION] {e.Message}");
        }
    }

    /// <summary>Compares the input file with the checksums (if given); a problem is logged and recorded, patching continues.</summary>
    /// <param name="bundleName">File name of the bundle (key in the checksums file).</param>
    /// <param name="path">Full path of the input file.</param>
    private void CheckHash(string bundleName, string path)
    {
        if (this._expectedHashes == null)
        {
            return;
        }

        if (!this._expectedHashes.TryGetValue(bundleName, out string? expected))
        {
            this._missingChecksums.Add(bundleName);
            this._log.Write("WARN: missing checksum, trying anyway... ");
            return;
        }

        using FileStream stream = File.OpenRead(path);
        string actual = Convert.ToHexString(SHA1.HashData(stream));
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
        {
            this._hashMismatches.Add(bundleName);
            this._log.Write("WARN: hash mismatch, trying anyway... ");
        }
    }

    /// <summary>Formats a list for the log line, e.g. ", untranslated: 3, 7".</summary>
    /// <param name="label">What the items are.</param>
    /// <param name="items">The items; nothing is shown if empty.</param>
    /// <returns>", label: a, b" or an empty string.</returns>
    private static string Listed(string label, List<string> items)
    {
        return items.Count > 0 ? $", {label}: {string.Join(", ", items)}" : "";
    }
}

/// <param name="MissingChecksums">Bundles not listed in the checksums file (patched anyway).</param>
/// <param name="HashMismatches">Input bundles whose SHA1 differs from the checksums file (patched anyway).</param>
/// <param name="Failed">Bundles that could not be patched (exception).</param>
public record PatchSummary(List<string> MissingChecksums, List<string> HashMismatches, List<string> Failed);
