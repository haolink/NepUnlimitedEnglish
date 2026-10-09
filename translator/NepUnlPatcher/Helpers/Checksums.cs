using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace NepUnlPatcher;

/// <summary>
/// The checksums file (-c): a JSON object "bundle file name": "SHA1 as hex".
/// Comparisons are case insensitive.
/// </summary>
public static class Checksums
{
    /// <summary>
    /// Reads the file. Throws <see cref="InvalidDataException"/> if it is not a JSON object of
    /// strings or lists a file name twice (also when the names differ only in case).
    /// </summary>
    /// <param name="path">Path of the checksums file.</param>
    /// <returns>SHA1 hex by bundle file name; lookups ignore case.</returns>
    public static Dictionary<string, string> Load(string path)
    {
        JObject json;
        try
        {
            json = JObject.Parse(File.ReadAllText(path));
        }
        catch (Newtonsoft.Json.JsonException e)
        {
            throw new InvalidDataException($"checksums file {path} is not valid JSON: {e.Message}", e);
        }

        Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (JProperty entry in json.Properties())
        {
            if (entry.Value.Type != JTokenType.String)
            {
                throw new InvalidDataException($"checksums file {path}: value of '{entry.Name}' is not a string");
            }

            if (!result.TryAdd(entry.Name, (string)entry.Value!))
            {
                throw new InvalidDataException($"checksums file {path}: '{entry.Name}' is listed twice");
            }
        }

        return result;
    }
}
