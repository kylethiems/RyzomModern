using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Ryzom.Core.Formats;

namespace Ryzom.Engine.Cooker;

/// <summary>
/// High-throughput multithreaded batch asset cooker.
/// Traverses legacy NeL directory trees and transforms them into modern glTF/PBR assets.
/// </summary>
public static class BatchAssetCooker
{
    public record BatchCookResult(
        int TotalAssetsProcessed,
        int SuccessfulUpgrades,
        int FailedUpgrades,
        double TotalDurationSeconds,
        double AverageAssetTimeMs,
        float AveragePolyReductionPct,
        List<AssetUpgradeSummary> Manifest);

    public record AssetUpgradeSummary(
        string AssetName,
        string InputPath,
        string OutputGlbPath,
        int OriginalVerts,
        int ModernVerts,
        float PolyReductionPct,
        double ElapsedMs);

    public static async Task<BatchCookResult> CookDirectoryAsync(
        string inputDirectory,
        string outputDirectory,
        int maxConcurrency = 0)
    {
        if (!Directory.Exists(inputDirectory))
            throw new DirectoryNotFoundException($"Input asset directory not found: {inputDirectory}");

        Directory.CreateDirectory(outputDirectory);

        var shapeFiles = Directory.GetFiles(inputDirectory, "*.*", SearchOption.AllDirectories);
        var eligibleFiles = new List<string>();
        foreach (var f in shapeFiles)
        {
            string ext = Path.GetExtension(f).ToLowerInvariant();
            if (ext == ".shape" || ext == ".mesh" || ext == ".obj")
            {
                eligibleFiles.Add(f);
            }
        }

        var sw = Stopwatch.StartNew();
        var manifestList = new ConcurrentBag<AssetUpgradeSummary>();
        int successCount = 0;
        int failureCount = 0;

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxConcurrency > 0 ? maxConcurrency : Environment.ProcessorCount
        };

        await Task.Run(() =>
        {
            Parallel.ForEach(eligibleFiles, parallelOptions, filePath =>
            {
                try
                {
                    string modelName = Path.GetFileNameWithoutExtension(filePath);
                    string outSubDir = Path.Combine(outputDirectory, Path.GetRelativePath(inputDirectory, Path.GetDirectoryName(filePath) ?? ""));
                    Directory.CreateDirectory(outSubDir);

                    string outGlbPath = Path.Combine(outSubDir, $"{modelName}.glb");

                    using var stream = File.OpenText(filePath);
                    var mesh = NeLShapeParser.Parse(stream, modelName);

                    var telemetry = AssetUpgradeEngine.UpgradeNeLMesh(mesh);

                    File.WriteAllBytes(outGlbPath, telemetry.GlbBinary);

                    manifestList.Add(new AssetUpgradeSummary(
                        modelName,
                        filePath,
                        outGlbPath,
                        telemetry.OriginalVertices,
                        telemetry.DecimatedVertices,
                        telemetry.PolygonReductionPercent,
                        telemetry.ExecutionTimeMs));

                    System.Threading.Interlocked.Increment(ref successCount);
                }
                catch
                {
                    System.Threading.Interlocked.Increment(ref failureCount);
                }
            });
        });

        sw.Stop();

        var manifest = manifestList.ToList();
        float avgReduction = 0.0f;
        if (manifest.Count > 0)
        {
            float totalRed = 0.0f;
            foreach (var m in manifest) totalRed += m.PolyReductionPct;
            avgReduction = totalRed / manifest.Count;
        }

        var result = new BatchCookResult(
            eligibleFiles.Count,
            successCount,
            failureCount,
            sw.Elapsed.TotalSeconds,
            successCount > 0 ? sw.Elapsed.TotalMilliseconds / successCount : 0.0,
            avgReduction,
            manifest);

        string manifestJson = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "modernization_manifest.json"), manifestJson);

        return result;
    }
}
