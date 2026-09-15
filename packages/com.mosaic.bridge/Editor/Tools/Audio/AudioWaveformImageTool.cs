using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Audio
{
    public static class AudioWaveformImageTool
    {
        [MosaicTool("audio/waveform-image",
                    "Renders an AudioClip's waveform (min/max per pixel column) to a PNG asset, for course " +
                    "slides/labs showing compressed vs PCM or loop points. Fails on clips whose effective " +
                    "LoadType is Streaming — set LoadType to DecompressOnLoad via audio/set-import-settings first.",
                    isReadOnly: false)]
        public static ToolResult<AudioWaveformImageResult> Execute(AudioWaveformImageParams p)
        {
            if (p.Width <= 0 || p.Height <= 0)
                return ToolResult<AudioWaveformImageResult>.Fail("Width and Height must be > 0", ErrorCodes.INVALID_PARAM);

            var importer = AssetImporter.GetAtPath(p.AssetPath) as AudioImporter;
            if (importer == null)
                return ToolResult<AudioWaveformImageResult>.Fail(
                    $"No audio clip found at '{p.AssetPath}'. Ensure the path is a valid audio asset.",
                    ErrorCodes.NOT_FOUND);

            var effectiveSettings = importer.ContainsSampleSettingsOverride("Standalone")
                ? importer.GetOverrideSampleSettings("Standalone")
                : importer.defaultSampleSettings;
            if (effectiveSettings.loadType == AudioClipLoadType.Streaming)
                return ToolResult<AudioWaveformImageResult>.Fail(
                    $"'{p.AssetPath}' is imported with LoadType=Streaming, which cannot be read via " +
                    "AudioClip.GetData. Use audio/set-import-settings to set LoadType to DecompressOnLoad " +
                    "or CompressedInMemory first.",
                    ErrorCodes.NOT_PERMITTED);

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(p.AssetPath);
            if (clip == null)
                return ToolResult<AudioWaveformImageResult>.Fail(
                    $"AudioClip could not be loaded at '{p.AssetPath}'", ErrorCodes.NOT_FOUND);

            var samples = new float[clip.samples * clip.channels];
            if (!clip.GetData(samples, 0))
                return ToolResult<AudioWaveformImageResult>.Fail(
                    $"AudioClip.GetData failed for '{p.AssetPath}'", ErrorCodes.INTERNAL_ERROR);

            Color background = ParseColor(p.BackgroundColor, new Color(0f, 0f, 0f, 0f));
            Color waveform = ParseColor(p.WaveformColor, new Color(0.29f, 0.68f, 0.31f, 1f));

            var texture = new Texture2D(p.Width, p.Height, TextureFormat.RGBA32, false);
            var pixels = new Color[p.Width * p.Height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = background;

            int totalMonoSamples = clip.samples;
            int samplesPerColumn = Mathf.Max(1, totalMonoSamples / p.Width);

            for (int x = 0; x < p.Width; x++)
            {
                int startSample = x * samplesPerColumn;
                int endSample = Mathf.Min(startSample + samplesPerColumn, totalMonoSamples);
                float min = 0f, max = 0f;

                for (int s = startSample; s < endSample; s++)
                {
                    // Average across channels (interleaved) for a single representative amplitude.
                    float value = 0f;
                    for (int c = 0; c < clip.channels; c++)
                        value += samples[s * clip.channels + c];
                    value /= clip.channels;

                    if (value < min) min = value;
                    if (value > max) max = value;
                }

                int yMin = Mathf.Clamp(Mathf.RoundToInt((1f - (max + 1f) / 2f) * (p.Height - 1)), 0, p.Height - 1);
                int yMax = Mathf.Clamp(Mathf.RoundToInt((1f - (min + 1f) / 2f) * (p.Height - 1)), 0, p.Height - 1);
                for (int y = Mathf.Min(yMin, yMax); y <= Mathf.Max(yMin, yMax); y++)
                    pixels[y * p.Width + x] = waveform;
            }

            texture.SetPixels(pixels);
            texture.Apply();

            string directory = Path.GetDirectoryName(p.OutputPath);
            if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                return ToolResult<AudioWaveformImageResult>.Fail(
                    $"Directory '{directory}' does not exist", ErrorCodes.NOT_FOUND);
            }

            string absolutePath = Path.Combine(Path.GetDirectoryName(Application.dataPath), p.OutputPath);
            File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(p.OutputPath, ImportAssetOptions.ForceSynchronousImport);

            // Data-visualization image, not a game texture — never let NPOT scaling resize it.
            if (AssetImporter.GetAtPath(p.OutputPath) is TextureImporter outputImporter &&
                outputImporter.npotScale != TextureImporterNPOTScale.None)
            {
                outputImporter.npotScale = TextureImporterNPOTScale.None;
                outputImporter.SaveAndReimport();
            }

            return ToolResult<AudioWaveformImageResult>.Ok(new AudioWaveformImageResult
            {
                AssetPath  = p.AssetPath,
                OutputPath = p.OutputPath,
                Width      = p.Width,
                Height     = p.Height,
            });
        }

        private static Color ParseColor(float[] rgba, Color fallback)
        {
            if (rgba == null || rgba.Length < 3) return fallback;
            return new Color(rgba[0], rgba[1], rgba[2], rgba.Length >= 4 ? rgba[3] : 1f);
        }
    }
}
