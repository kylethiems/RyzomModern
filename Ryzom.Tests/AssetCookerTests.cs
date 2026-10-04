using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Ryzom.Core.Formats;
using Ryzom.Core.Math3D;
using Ryzom.Engine.Cooker;
using Xunit;

namespace Ryzom.Tests;

public class AssetCookerTests
{
    [Fact]
    public void NeLShapeParser_ParsesAsciiMeshStreamCorrectly()
    {
        string sampleNeL = @"
# Ryzom NeL sample shape export
v 0.0 0.0 0.0
v 1.0 0.0 0.0
v 0.0 1.0 0.0
vn 0.0 0.0 1.0
vt 0.0 0.0
vt 1.0 0.0
vt 0.0 1.0
f 1/1/1 2/2/1 3/3/1
";
        using var reader = new StringReader(sampleNeL);
        var mesh = NeLShapeParser.Parse(reader, "TestCuirass");

        Assert.Equal("TestCuirass", mesh.Name);
        Assert.Equal(3, mesh.Vertices.Count);
        Assert.Equal(3, mesh.Indices.Count);
        Assert.Equal(0.0f, mesh.Vertices[0].Position.X);
        Assert.Equal(1.0f, mesh.Vertices[1].Position.X);
        Assert.Equal(1.0f, mesh.Vertices[2].Position.Y);
    }

    [Fact]
    public void UpgradeNeLMesh_TransformsLegacyMeshToValidGlbWithAcetoneWash()
    {
        var mesh = new NeLMesh { Name = "FyrosSentinelPauldrons" };

        // Add a pyramid with intentional layer line noise
        mesh.Vertices.Add(new Vertex3D(new Vector3f(0f, 0f, 0.2f), Vector3f.UnitZ, 0f, 0f));
        mesh.Vertices.Add(new Vertex3D(new Vector3f(1f, 0f, 0f), Vector3f.UnitZ, 1f, 0f));
        mesh.Vertices.Add(new Vertex3D(new Vector3f(0.5f, 1f, 0f), Vector3f.UnitZ, 0.5f, 1f));
        mesh.Indices.AddRange(new ushort[] { 0, 1, 2 });

        byte[] fakeDiffuse = new byte[16 * 16 * 4];
        Array.Fill(fakeDiffuse, (byte)180);

        var telemetry = AssetUpgradeEngine.UpgradeNeLMesh(mesh, fakeDiffuse, 16, 16);

        Assert.Equal("FyrosSentinelPauldrons", telemetry.ModelName);
        Assert.True(telemetry.GlbBinary.Length > 20);
        Assert.NotNull(telemetry.PbrTextures);

        // Verify glTF 2.0 Magic Header (0x46546C67 = "glTF")
        uint magic = BitConverter.ToUInt32(telemetry.GlbBinary, 0);
        Assert.Equal(0x46546C67u, magic);

        uint version = BitConverter.ToUInt32(telemetry.GlbBinary, 4);
        Assert.Equal(2u, version);

        // Verify volume conservation ratio is close to 1.0 (non-shrinking Taubin)
        Assert.True(telemetry.VolumePreservationRatio > 0.65f && telemetry.VolumePreservationRatio < 1.35f,
            $"Volume ratio outside non-shrinking bound: {telemetry.VolumePreservationRatio}");
    }

    [Fact]
    public async Task BatchCookDirectoryAsync_ProcessesMeshDirectoryAndEmitsManifest()
    {
        string tempIn = Path.Combine(Path.GetTempPath(), "ryzom_test_in_" + Guid.NewGuid().ToString("N"));
        string tempOut = Path.Combine(Path.GetTempPath(), "ryzom_test_out_" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(tempIn);

        string sampleShape = @"
v 0 0 0
v 2 0 0
v 1 2 0
f 1/1/1 2/1/1 3/1/1
";
        await File.WriteAllTextAsync(Path.Combine(tempIn, "arm_pauldron.shape"), sampleShape);
        await File.WriteAllTextAsync(Path.Combine(tempIn, "helmet_crest.mesh"), sampleShape);

        try
        {
            var result = await BatchAssetCooker.CookDirectoryAsync(tempIn, tempOut);

            Assert.Equal(2, result.TotalAssetsProcessed);
            Assert.Equal(2, result.SuccessfulUpgrades);
            Assert.Equal(0, result.FailedUpgrades);
            Assert.True(File.Exists(Path.Combine(tempOut, "arm_pauldron.glb")));
            Assert.True(File.Exists(Path.Combine(tempOut, "helmet_crest.glb")));
            Assert.True(File.Exists(Path.Combine(tempOut, "modernization_manifest.json")));
        }
        finally
        {
            if (Directory.Exists(tempIn)) Directory.Delete(tempIn, true);
            if (Directory.Exists(tempOut)) Directory.Delete(tempOut, true);
        }
    }
}
