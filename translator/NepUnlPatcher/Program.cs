using System;
using NepUnlPatcher;

namespace NepUnlPatcher
{
    /// <summary>
    /// Entry point of the console application: parses the command line and runs the <see cref="Patcher"/>.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Main application handler.
        /// Parses command line input values. Will require input, output, translations - checksums are optional.
        /// </summary>
        /// <param name="args">Command line arguments, see <see cref="CommandLineOptions.Usage"/>.</param>
        /// <returns>Exit code: 0 if all is correct, 1 in case of errors.</returns>
        public static int Main(string[] args)
        {
            CommandLineOptions? options;
            try
            {
                options = args.Length == 0 ? null : CommandLineOptions.Parse(args);

                // Application requires input, output, and translations directories. Checksums are optional.
                if (options == null)
                {
                    Console.WriteLine(CommandLineOptions.USAGE);
                    return args.Length == 0 ? 1 : 0;
                }
            }
            catch (ArgumentException e)
            {
                Console.Error.WriteLine($"Error: {e.Message}");
                Console.Error.WriteLine();
                Console.Error.WriteLine(CommandLineOptions.USAGE);
                return 1;
            }

            // Display the parsed command line options.
            Console.WriteLine($"Input:        {options.InputDir}");
            Console.WriteLine($"Output:       {options.OutputDir}");
            Console.WriteLine($"Translations: {options.TranslationsDir}");
            Console.WriteLine($"Checksums:    {options.ChecksumsFile ?? "(none, input files are not checked)"}");

            Console.WriteLine();

            PatchSummary summary;
            try
            {
                Dictionary<string, string>? checksums = (options.ChecksumsFile == null) ? null : Checksums.Load(options.ChecksumsFile);
                Patcher patcher = new Patcher(options.InputDir, options.OutputDir, new TranslationFiles(options.TranslationsDir), checksums, Console.Out);
                summary = patcher.Run();
            }
            catch (Exception e)   // translation or checksums file that cannot be read: nothing can be patched
            {
                Console.WriteLine($"[EXCEPTION] {e.Message}");
                return 1;
            }

            Console.WriteLine();
            if (summary.MissingChecksums.Count > 0)
            {
                Console.WriteLine($"Missing checksum for {string.Join(", ", summary.MissingChecksums)}");
            }

            if (summary.HashMismatches.Count > 0)
            {
                Console.WriteLine($"SHA1 mismatches for {string.Join(", ", summary.HashMismatches)} (files might not be PS5 version 1.00)");
            }

            if (summary.Failed.Count > 0)
            {
                Console.WriteLine($"Exceptions for {string.Join(", ", summary.Failed)} (these files were not written)");
            }

            Console.WriteLine(summary.Failed.Count == 0 ? "Done." : "Done with errors.");
            return summary.Failed.Count == 0 ? 0 : 1;
        }
    }
}
