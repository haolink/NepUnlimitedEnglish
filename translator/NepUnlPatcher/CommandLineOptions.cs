namespace NepUnlPatcher;

/// <summary>
/// Parsed command line. All stored paths are absolute.
/// </summary>
public class CommandLineOptions
{
    /// <summary>Original game bundles.</summary>
    public string InputDir { get; }
    /// <summary>Output directory for the translated bundles.</summary>
    public string OutputDir { get; }
    /// <summary>Directory containing the translation files (JSON).</summary>
    public string TranslationsDir { get; }
    /// <summary>Optional JSON file with the expected SHA1 of the original bundles; null = no check.</summary>
    public string? ChecksumsFile { get; }

    /// <summary>
    /// Internal constructor, use <see cref="Parse"/> to create an instance.
    /// </summary>
    /// <param name="inputDir">Original bundles (Media/StreamingAssets/aa/PS5).</param>
    /// <param name="outputDir">Target directory for the translated bundles.</param>
    /// <param name="translationsDir">Translation files (JSON).</param>
    /// <param name="checksumsFile">Optional JSON with the expected SHA1 of the original bundles; null = no check.</param>
    private CommandLineOptions(string inputDir, string outputDir, string translationsDir, string? checksumsFile)
    {
        this.InputDir = inputDir;
        this.OutputDir = outputDir;
        this.TranslationsDir = translationsDir;
        this.ChecksumsFile = checksumsFile;
    }

    /// <summary>Help text shown for -h/--help, without arguments and after a command line error.</summary>
    public const string USAGE = """
        Usage: NepUnlPatcher -i <input dir> -o <output dir> -t <translations dir> [-c <checksums file>]

          -i, --input         directory with the original bundles (only read)
          -o, --output        directory for the patched bundles (must not be the input directory)
          -t, --translations  directory with the translation files
          -c, --checksums     JSON file with the expected SHA1 of the original bundles (optional;
                              without it the input files are not checked)
          -h, --help          show this help
        """;

    /// <summary>
    /// Helper for SetOnce.
    /// </summary>
    delegate void StringRefDelegate(ref string? item);

    /// <summary>
    /// Parses the arguments.
    /// Throws <see cref="ArgumentException"/> for unknown, duplicate or missing options.
    /// </summary>
    /// <param name="args">Command line arguments as passed to Main.</param>
    /// <returns>The parsed options with absolute paths, or null if help was requested.</returns>
    public static CommandLineOptions? Parse(string[] args)
    {
        string? input = null, output = null, translations = null, checksums = null;

        int i = 0;
        //Quick and dirty helper to make sure an option is only set once.
        StringRefDelegate SetOnce = (ref string? field) =>
        {
            if (field is not null)
            {
                throw new ArgumentException($"duplicate argument '{args[i]}'");
            }
            if (i + 1 >= args.Length)
            {
                throw new ArgumentException($"missing value for argument '{args[i]}'");
            }
            field = args[++i];
        };

        for (i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-h" or "--help" or "/?":
                    return null;
                case "-i" or "--input":
                    SetOnce(ref input);
                    break;
                case "-o" or "--output":
                    SetOnce(ref output);
                    break;
                case "-t" or "--translations":
                    SetOnce(ref translations);
                    break;
                case "-c" or "--checksums":
                    SetOnce(ref checksums);
                    break;
                default:
                    throw new ArgumentException($"unknown argument '{args[i]}'");
            }
        }

        // Ensure that all required options have been set.
        if (input == null)
        {
            throw new ArgumentException("-i/--input is missing");
        }
        if (output == null)
        {
            throw new ArgumentException("-o/--output is missing");
        }
        if (translations == null)
        {
            throw new ArgumentException("-t/--translations is missing");
        }

        string? checksumPath = null;
        if (checksums is not null)
        {
            checksumPath = Path.GetFullPath(checksums);
            if (!File.Exists(checksumPath))
            {
                throw new ArgumentException($"checksums file not found: {checksumPath}");
            }
        }

        // Quick and dirty helper.
        Func<string, string, string> ensureDirectoryExists = (path, name) =>
        {
            if (Directory.Exists(path))
            {
                return Path.GetFullPath(path);
            }
            else
            {
                throw new ArgumentException($"{name} directory not found: {path}");
            }
        };

        input = ensureDirectoryExists(input, "input");
        output = ensureDirectoryExists(output, "output");
        translations = ensureDirectoryExists(translations, "translations");

        string relative = Path.GetRelativePath(input, output);
        if ((relative == "." || (!relative.StartsWith("..") && !Path.IsPathRooted(relative))))
        { // Output directory is inside input directory
            throw new ArgumentException("output directory must not be inside input directory");
        }

        return new CommandLineOptions(
            Path.GetFullPath(input),
            Path.GetFullPath(output),
            Path.GetFullPath(translations),
            checksumPath);
    }
}
