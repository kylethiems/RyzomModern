using System;
using System.IO;
using System.Threading.Tasks;
using Ryzom.Engine.Cooker;

namespace Ryzom.Cooker;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("===============================================================================");
        Console.WriteLine("⚡ RYZOM MODERN 3-TIER BATCH ASSET MODERNIZATION & COOKING ENGINE");
        Console.WriteLine("   Pipeline: NeL Ingestion ➔ Voxel Decimation ➔ Acetone Wash ➔ PBR/glTF Export");
        Console.WriteLine("===============================================================================");

        if (args.Length < 2)
        {
            Console.WriteLine("Usage: dotnet run --project tools/cooker -- <input_legacy_dir> <output_modern_dir>");
            Console.WriteLine("Example: dotnet run --project tools/cooker -- /data/ryzom/art/ /data/ryzom/modern_pbr/");
            return 1;
        }

        string inputDir = args[0];
        string outputDir = args[1];

        if (!Directory.Exists(inputDir))
        {
            Console.Error.WriteLine($"[!] Error: Input directory '{inputDir}' does not exist.");
            return 2;
        }

        Console.WriteLine($"[*] Ingesting legacy assets from: {inputDir}");
        Console.WriteLine($"[*] Exporting modern GLB/PBR assets to: {outputDir}");

        var result = await BatchAssetCooker.CookDirectoryAsync(inputDir, outputDir);

        Console.WriteLine("\n[✓] Batch Modernization Complete!");
        Console.WriteLine($"    - Total Assets Traversed:      {result.TotalAssetsProcessed}");
        Console.WriteLine($"    - Successfully Upgraded:       {result.SuccessfulUpgrades}");
        Console.WriteLine($"    - Failed Assets:               {result.FailedUpgrades}");
        Console.WriteLine($"    - Total Processing Duration:   {result.TotalDurationSeconds:F2} seconds");
        Console.WriteLine($"    - Average Latency per Asset:   {result.AverageAssetTimeMs:F2} ms");
        Console.WriteLine($"    - Average Polygon Decimation:  {result.AveragePolyReductionPct:F1}%");
        Console.WriteLine($"    - Output Manifest Saved:       {Path.Combine(outputDir, "modernization_manifest.json")}");

        return 0;
    }
}
