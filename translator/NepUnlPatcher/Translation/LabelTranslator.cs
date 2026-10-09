using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace NepUnlPatcher;

/// <summary>
/// Writes English into hard-coded UI labels: the "m_text" field of TextMeshPro MonoBehaviours
/// in interface_* bundles.
/// </summary>
internal static class LabelTranslator
{
    /// <summary>
    /// Replaces m_text of <paramref name="label"/> (the MonoBehaviour as JSON) in place.
    /// The current text must be <paramref name="expectedJapanese"/> (from source/mtext.json) –
    /// guards against a different game version where the path id points elsewhere.
    /// </summary>
    /// <param name="label">The TextMeshPro MonoBehaviour as JSON; changed in place.</param>
    /// <param name="expectedJapanese">The label text the original must have.</param>
    /// <param name="translations">Japanese label → English (mtext.json).</param>
    /// <param name="where">Bundle/path id for error messages.</param>
    public static void Translate(JObject label, string expectedJapanese, Dictionary<string, string> translations, string where)
    {
        string current = (string?)label["m_text"] ?? throw new InvalidDataException($"{where}: no m_text field");
        if (current != expectedJapanese)
        {
            throw new InvalidDataException($"{where}: m_text is \"{current}\", expected \"{expectedJapanese}\"");
        }

        if (!translations.TryGetValue(current, out string? english))
        {
            throw new InvalidDataException($"{where}: no translation for \"{current}\"");
        }

        label["m_text"] = english;
    }
}
