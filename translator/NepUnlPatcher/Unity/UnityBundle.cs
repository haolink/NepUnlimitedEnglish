using System;
using System.Text;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Newtonsoft.Json.Linq;

namespace NepUnlPatcher;

/// <summary>
/// One .bundle file of the game, opened for patching. Open, patch, save.
/// A thin layer over AssetsTools.NET (patched for this game's 16-byte bundle/object alignment).
/// It's used to find find TextAssets and MonoBehaviours. MonoBehaviours are bridged JSON via <see cref="AssetValueJson"/>. 
/// Only replaced objects/serialised files are rebuilt; everything else keeps its original bytes (minimal diff), same as before.
/// </summary>
public sealed class UnityBundle
{
    /// <summary>Unity class id of TextAsset.</summary>
    private const int TEXT_ASSET_CLASS_ID = 49;
    
    /// <summary>Unity class id of MonoBehaviour.</summary>
    private const int MONO_BEHAVIOUR_CLASS_ID = 114;
    
    /// <summary>Platforms this game needs 16-byte alignment on (PS5); matches the patched library's default elsewhere.</summary>
    private const uint ALIGNMENT = 16;

    /// <summary>The bundle, already fully decompressed (see constructor).</summary>
    private readonly AssetBundleFile _bundle;

    /// <summary>Serialised files stored inside the bundle (in case of Nep Unlimited, usually only one).</summary>
    private readonly List<SerializedFile> _serializedFiles = new List<SerializedFile>();

    /// <summary>Internal storage for text assets.</summary>
    private readonly Dictionary<string, TextAssetEntry> _textAssets = new Dictionary<string, TextAssetEntry>();

    /// <summary>
    /// MonoBehaviours with a type tree by path id. Null marks a path id that occurs in more than
    /// one serialised file (ambiguous; not the case in this game).
    /// </summary>
    private readonly Dictionary<long, MonoBehaviourEntry?> _monoBehaviours = new Dictionary<long, MonoBehaviourEntry?>();

    /// <summary>Full path of the original file (only read).</summary>
    public string FileName { get; }

    /// <summary>
    /// Size of the original file. Save will enable compression should the rebuilt bundle grow larger than the original size.
    /// This is an attempt to ensure the rebuilt bundle does not exceed the original size unnecessarily.
    /// </summary>
    private long _originalSize;

    /// <summary>
    /// Reads the bundle and indexes its TextAssets. Throws if two TextAssets share a name
    /// (none in this game), because the name is the key.
    /// </summary>
    /// <param name="fileName">Path of the original bundle (only read).</param>
    public UnityBundle(string fileName)
    {
        this.FileName = Path.GetFullPath(fileName);
        byte[] data = File.ReadAllBytes(this.FileName);
        this._originalSize = data.Length;

        this._bundle = new AssetBundleFile();
        this._bundle.Read(new AssetsFileReader(new MemoryStream(data)));
        if (this._bundle.DataIsCompressed)
        {
            this._bundle = BundleHelper.UnpackBundle(this._bundle);
        }

        // Search for Asset entries in the bundle.
        for (int i = 0; i < this._bundle.BlockAndDirInfo.DirectoryInfos.Count; i++)
        {
            if (!this._bundle.BlockAndDirInfo.DirectoryInfos[i].IsSerialized)
            {
                continue;
            }

            AssetsFile file = new AssetsFile();
            file.Read(new AssetsFileReader(new MemoryStream(BundleHelper.LoadAssetDataFromBundle(this._bundle, i))));
            SerializedFile serializedFile = new SerializedFile(i, file);
            this._serializedFiles.Add(serializedFile);

            // Inside each file we iterate over all sub-assets (AssetFileInfo entries) and categorise them by type.
            foreach (AssetFileInfo info in file.Metadata.AssetInfos)
            {
                TypeTreeType typeTreeType = file.Metadata.TypeTreeTypes[info.TypeIdOrIndex];
                if (typeTreeType.TypeBlob == null || typeTreeType.Nodes.Count == 0)
                {
                    continue;   // no type tree for this type (not used by this game); cannot decode
                }

                AssetTypeValueField root = ReadField(serializedFile, info, typeTreeType);
                int classId = info.GetTypeId(file);
                if (classId == TEXT_ASSET_CLASS_ID)
                {
                    string name = root["m_Name"].Value.AsString;
                    if (!this._textAssets.TryAdd(name, new TextAssetEntry(serializedFile, info, root)))
                    {
                        throw new InvalidDataException($"{Path.GetFileName(this.FileName)}: TextAsset name '{name}' is not unique");
                    }
                }
                else if (classId == MONO_BEHAVIOUR_CLASS_ID)
                {
                    bool unique = this._monoBehaviours.TryAdd(info.PathId, new MonoBehaviourEntry(serializedFile, info, root));
                    if (!unique)
                    {
                        this._monoBehaviours[info.PathId] = null;   // ambiguous, FindMonoBehaviour throws
                    }
                }
            }
        }
    }

    /// <summary>
    /// All TextAssets of the bundle: m_Name → m_Script decoded as UTF-8 (current state,
    /// including updates). A BOM stays in the string as U+FEFF, so it survives an update.
    /// Some TextAssets are binary (e.g. M_00_000.acb audio in two scenario bundles); their
    /// string is garbage, but harmless: only updated assets are re-encoded, all others keep
    /// their original bytes.
    /// </summary>
    /// <returns>TextAsset name → content as text.</returns>
    public Dictionary<string, string> GetTextAssets()
    {
        return this._textAssets.ToDictionary(e => e.Key, e => Encoding.UTF8.GetString(e.Value.Root["m_Script"].Value.AsByteArray));
    }

    /// <summary>
    /// Replaces m_Script of the TextAsset <paramref name="name"/> with <paramref name="text"/>
    /// encoded as UTF-8; m_Name stays. Throws if there is no such TextAsset.
    /// </summary>
    /// <param name="name">m_Name of the TextAsset.</param>
    /// <param name="text">The new content.</param>
    public void UpdateTextAsset(string name, string text)
    {
        if (!this._textAssets.TryGetValue(name, out TextAssetEntry? entry))
        {
            throw new KeyNotFoundException($"{Path.GetFileName(this.FileName)}: no TextAsset '{name}'");
        }

        entry.Root["m_Script"].Value.AsByteArray = Encoding.UTF8.GetBytes(text);
        MarkChanged(entry.SerializedFile, entry.Info, entry.Root);
    }

    /// <summary>Path ids of all MonoBehaviours that can be read (those with a type tree).</summary>
    /// <returns>The path ids.</returns>
    public List<long> GetMonoBehaviourIds()
    {
        return this._monoBehaviours.Keys.ToList();
    }

    /// <summary>
    /// A MonoBehaviour as JSON (current state, including updates), like AssetStudio shows it:
    /// fields as properties, lists as arrays.
    /// </summary>
    /// <param name="pathId">Path id from <see cref="GetMonoBehaviourIds"/>.</param>
    /// <returns>All fields of the MonoBehaviour.</returns>
    public JObject GetMonoBehaviour(long pathId)
    {
        return (JObject)AssetValueJson.Read(this.FindMonoBehaviour(pathId).Root);
    } 

    /// <summary>
    /// Replaces a MonoBehaviour with <paramref name="value"/> (complete, as returned by
    /// <see cref="GetMonoBehaviour"/>). Throws if a field is missing or unknown, has the wrong
    /// kind (e.g. text for a number) or does not fit its type (e.g. negative for unsigned).
    /// </summary>
    /// <param name="pathId">Path id from <see cref="GetMonoBehaviourIds"/>.</param>
    /// <param name="value">All fields, changed as needed.</param>
    public void UpdateMonoBehaviour(long pathId, JObject value)
    {
        MonoBehaviourEntry entry = this.FindMonoBehaviour(pathId);
        AssetValueJson.Write(entry.Root, value);
        MarkChanged(entry.SerializedFile, entry.Info, entry.Root);
    }

    /// <summary>Marks an object as changed so <see cref="Save"/> rebuilds its serialised file.</summary>
    private static void MarkChanged(SerializedFile serializedFile, AssetFileInfo info, AssetTypeValueField root)
    {
        serializedFile.Dirty = true;
        if (!serializedFile.Changed.Any(c => c.Info == info))
        {
            serializedFile.Changed.Add((info, root));
        }
    }

    /// <summary>
    /// Writes the bundle.
    /// </summary>
    /// <param name="targetFileName">Path of the bundle to write; must not be the original.</param>
    /// <returns>Size, limit and the packing that was used.</returns>
    public SaveResult Save(string targetFileName)
    {
        string target = Path.GetFullPath(targetFileName);
        if (string.Equals(target, this.FileName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"refusing to overwrite the original {this.FileName}");
        }

        // rebuild only the serialised files that have changes; the others keep their bytes
        foreach (SerializedFile serializedFile in this._serializedFiles.Where(c => c.Dirty))
        {
            foreach ((AssetFileInfo info, AssetTypeValueField root) in serializedFile.Changed)
            {
                using MemoryStream ms = new MemoryStream();
                AssetsFileWriter w = new AssetsFileWriter(ms) { BigEndian = serializedFile.File.Header.Endianness };
                root.Write(w);
                info.Replacer = new ContentReplacerFromBuffer(ms.ToArray());
            }

            using MemoryStream serializedFileMs = new MemoryStream();
            serializedFile.File.Write(new AssetsFileWriter(serializedFileMs), dataAlignment: ALIGNMENT, padDataOffsetToPage: false);
            this._bundle.BlockAndDirInfo.DirectoryInfos[serializedFile.DirIndex].Replacer = new ContentReplacerFromBuffer(serializedFileMs.ToArray());
        }

        BundlePacking packing = BundlePacking.Uncompressed;
        byte[] data = this.WriteBundle();
        if (data.Length > this._originalSize)
        {
            packing = BundlePacking.Lz4Hc;
            data = PackBundle(data);
            if (data.Length > this._originalSize)
            {
                throw new BundleTooLargeException(this.FileName, data.Length, this._originalSize);
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllBytes(target, data);
        return new SaveResult(data.Length, this._originalSize, packing);
    }

    /// <summary>Writes the (still uncompressed) bundle with every node 16-byte aligned.</summary>
    private byte[] WriteBundle()
    {
        using MemoryStream ms = new MemoryStream();
        this._bundle.Write(new AssetsFileWriter(ms), nodeAlignment: ALIGNMENT);
        return ms.ToArray();
    }

    /// <summary>LZ4HC-compresses an uncompressed bundle (re-reads it first: <see cref="AssetBundleFile.Pack"/>
    /// packs from the file's own directory info/data reader, not from an arbitrary buffer).</summary>
    private static byte[] PackBundle(byte[] uncompressed)
    {
        AssetBundleFile bundle = new AssetBundleFile();
        bundle.Read(new AssetsFileReader(new MemoryStream(uncompressed)));
        using MemoryStream ms = new MemoryStream();
        bundle.Pack(new AssetsFileWriter(ms), AssetBundleCompressionType.LZ4, blockDirAtEnd: false);
        return ms.ToArray();
    }

    /// <summary>Builds the value tree of one object: type tree template + raw bytes at its offset.</summary>
    private static AssetTypeValueField ReadField(SerializedFile serializedFile, AssetFileInfo info, TypeTreeType typeTreeType)
    {
        AssetTypeTemplateField template = new AssetTypeTemplateField();
        template.FromTypeTree(typeTreeType);

        AssetsFileReader reader = serializedFile.File.Reader;
        reader.BigEndian = serializedFile.File.Header.Endianness;
        return template.MakeValue(reader, info.GetAbsoluteByteOffset(serializedFile.File));
    }

    /// <summary>Looks up a MonoBehaviour; throws if it does not exist or its path id is not unique.</summary>
    /// <param name="pathId">Path id of the MonoBehaviour.</param>
    private MonoBehaviourEntry FindMonoBehaviour(long pathId)
    {
        if (!this._monoBehaviours.TryGetValue(pathId, out MonoBehaviourEntry? entry))
        {
            throw new KeyNotFoundException($"{Path.GetFileName(this.FileName)}: no MonoBehaviour with path id {pathId}");
        }

        return entry ?? throw new InvalidDataException($"{Path.GetFileName(this.FileName)}: path id {pathId} is not unique");
    }

    /// <summary>A serialised file inside the bundle, by its directory index.</summary>
    private class SerializedFile
    {
        /// <summary>True once a TextAsset/MonoBehaviour of this file was updated.</summary>
        public bool Dirty { get; set; }

        /// <summary>Objects of this file whose value tree was changed (reserialised and
        /// set as their <see cref="AssetFileInfo.Replacer"/> on save).</summary>
        public List<(AssetFileInfo Info, AssetTypeValueField Root)> Changed { get; } = [];

        /// <summary>Index into <see cref="AssetBundleBlockAndDirInfo.DirectoryInfos"/>.</summary>
        public int DirIndex { get; }
        /// <summary>The parsed serialised file.</summary>
        public AssetsFile File { get; }

        /// <summary>New instance of a serialised file.</summary>
        /// <param name="dirIndex">Index into <see cref="AssetBundleBlockAndDirInfo.DirectoryInfos"/>.</param>
        /// <param name="file">The parsed serialised file.</param>
        public SerializedFile(int dirIndex, AssetsFile file)
        {
            this.DirIndex = dirIndex;
            this.File = file;
        }
    }

    /// <summary>Where a MonoBehaviour is and how to read/write it.</summary>
    /// <param name="SerializedFile">The serialised file containing it (a <see cref="SerializedFile"/>).</param>
    /// <param name="Info">Its object table entry.</param>
    /// <param name="Root">Its value tree (current state, including updates).</param>
    private record MonoBehaviourEntry(SerializedFile SerializedFile, AssetFileInfo Info, AssetTypeValueField Root);

    /// <summary>A TextAsset: where it is and its value tree.</summary>
    /// <param name="SerializedFile">The serialised file containing it.</param>
    /// <param name="Info">Its object table entry.</param>
    /// <param name="Root">Its value tree (current state, including updates).</param>
    private record TextAssetEntry(SerializedFile SerializedFile, AssetFileInfo Info, AssetTypeValueField Root);
}

/// <summary>Compression Information</summary>
public enum BundlePacking
{
    /// <summary>Like the originals: one uncompressed data block.</summary>
    Uncompressed,
    /// <summary>LZ4HC-compressed.</summary>
    Lz4Hc,
}

/// <param name="Size">Size of the written file.</param>
/// <param name="Limit">Maximum allowed size (= original size).</param>
/// <param name="Packing">Which format was needed to stay within the limit.</param>
public sealed record SaveResult(long Size, long Limit, BundlePacking Packing);

/// <summary>Helper exception type.</summary>
/// <param name="fileName">The bundle that was too large.</param>
/// <param name="size">Its size even with LZ4HC compression.</param>
/// <param name="limit">The allowed size (the original's).</param>
public sealed class BundleTooLargeException(string fileName, long size, long limit)
    : Exception($"{Path.GetFileName(fileName)}: {size} bytes even compressed, limit {limit}");
