using System.Runtime.CompilerServices;
using Ryzom.Core.Math3D;

namespace Ryzom.Core.Graphics;

public class PbrTextureSet
{
    public int Width { get; set; }
    public int Height { get; set; }
    public byte[] AlbedoRgba { get; set; } = Array.Empty<byte>();
    public byte[] NormalRgba { get; set; } = Array.Empty<byte>();
    public byte[] OrmRgba { get; set; } = Array.Empty<byte>(); // R: Occlusion, G: Roughness, B: Metallic
}

public static class PbrTextureSynthesizer
{
    /// <summary>
    /// Mathematically converts a legacy 2004 diffuse texture into a modern PBR material suite:
    /// 1. Tangent-Space Normal Map (via Scharr spatial gradient operator)
    /// 2. ORM Map (Red = Ambient Occlusion, Green = Roughness, Blue = Metallic)
    /// </summary>
    public static PbrTextureSet SynthesizePbrSet(
        byte[] diffuseRgba,
        int width,
        int height,
        float normalStrength = 2.5f,
        float defaultRoughness = 0.65f)
    {
        if (diffuseRgba.Length != width * height * 4)
            throw new ArgumentException("Buffer size must equal width * height * 4.", nameof(diffuseRgba));

        byte[] normalMap = new byte[diffuseRgba.Length];
        byte[] ormMap = new byte[diffuseRgba.Length];

        // 1. Compute grayscale luminance channel for gradient analysis
        float[] luminance = new float[width * height];
        for (int i = 0; i < width * height; i++)
        {
            int baseIdx = i * 4;
            float r = diffuseRgba[baseIdx + 0] / 255.0f;
            float g = diffuseRgba[baseIdx + 1] / 255.0f;
            float b = diffuseRgba[baseIdx + 2] / 255.0f;
            // ITU-R BT.709 luminance
            luminance[i] = 0.2126f * r + 0.7152f * g + 0.0722f * b;
        }

        // 2. Synthesize Tangent-Space Normal Map via Scharr gradient filter
        for (int y = 0; y < height; y++)
        {
            int ym1 = (y > 0 ? y - 1 : 0) * width;
            int y0  = y * width;
            int yp1 = (y < height - 1 ? y + 1 : height - 1) * width;

            for (int x = 0; x < width; x++)
            {
                int xm1 = x > 0 ? x - 1 : 0;
                int xp1 = x < width - 1 ? x + 1 : width - 1;

                // Scharr operator horizontal (dX)
                float dX = (3.0f * luminance[ym1 + xp1] + 10.0f * luminance[y0 + xp1] + 3.0f * luminance[yp1 + xp1])
                         - (3.0f * luminance[ym1 + xm1] + 10.0f * luminance[y0 + xm1] + 3.0f * luminance[yp1 + xm1]);

                // Scharr operator vertical (dY)
                float dY = (3.0f * luminance[yp1 + xm1] + 10.0f * luminance[yp1 + x] + 3.0f * luminance[yp1 + xp1])
                         - (3.0f * luminance[ym1 + xm1] + 10.0f * luminance[ym1 + x] + 3.0f * luminance[ym1 + xp1]);

                // Normal vector: [-dX * strength, -dY * strength, 1.0]
                var norm = new Vector3f(-dX * normalStrength, -dY * normalStrength, 1.0f).Normalize();

                int pixelIdx = (y0 + x) * 4;
                // Encode normal [-1, 1] -> [0, 255]
                normalMap[pixelIdx + 0] = (byte)Math.Clamp((norm.X * 0.5f + 0.5f) * 255.0f, 0, 255);
                normalMap[pixelIdx + 1] = (byte)Math.Clamp((norm.Y * 0.5f + 0.5f) * 255.0f, 0, 255);
                normalMap[pixelIdx + 2] = (byte)Math.Clamp((norm.Z * 0.5f + 0.5f) * 255.0f, 0, 255);
                normalMap[pixelIdx + 3] = 255; // Alpha

                // 3. Synthesize ORM Map (R: Ambient Occlusion, G: Roughness, B: Metallic)
                float lum = luminance[y0 + x];

                // Cavity Ambient Occlusion: darker crevices get deeper AO shadows
                float ao = Math.Clamp(MathF.Pow(lum, 0.45f), 0.2f, 1.0f);

                // Metallic estimation: High luminance with low color variance is typical of metal highlights
                float r = diffuseRgba[pixelIdx + 0] / 255.0f;
                float g = diffuseRgba[pixelIdx + 1] / 255.0f;
                float b = diffuseRgba[pixelIdx + 2] / 255.0f;
                float maxC = MathF.Max(r, MathF.Max(g, b));
                float minC = MathF.Min(r, MathF.Min(g, b));
                float saturation = maxC > 1e-4f ? (maxC - minC) / maxC : 0f;

                float metallic = (lum > 0.65f && saturation < 0.25f) ? 0.85f : 0.05f;

                // Roughness: Invert specular response; metallic areas are smoother
                float roughness = metallic > 0.5f ? 0.25f : defaultRoughness;

                ormMap[pixelIdx + 0] = (byte)(ao * 255.0f);
                ormMap[pixelIdx + 1] = (byte)(roughness * 255.0f);
                ormMap[pixelIdx + 2] = (byte)(metallic * 255.0f);
                ormMap[pixelIdx + 3] = 255;
            }
        }

        return new PbrTextureSet
        {
            Width = width,
            Height = height,
            AlbedoRgba = diffuseRgba,
            NormalRgba = normalMap,
            OrmRgba = ormMap
        };
    }
}
