using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Audio
{
    public static class AudioSynthesizeClipTool
    {
        [MosaicTool("audio/synthesize-clip",
                    "Generates a placeholder AudioClip (tone/noise/sweep/click) and imports it as a real " +
                    "asset at AssetPath, for course authoring before final audio is available",
                    isReadOnly: false)]
        public static ToolResult<AudioSynthesizeClipResult> Execute(AudioSynthesizeClipParams p)
        {
            if (!TryParseWaveform(p.Waveform, out var waveform))
                return ToolResult<AudioSynthesizeClipResult>.Fail(
                    $"Unknown Waveform '{p.Waveform}'. Valid: tone, noise, sweep, click", ErrorCodes.INVALID_PARAM);

            if (p.DurationSeconds <= 0f)
                return ToolResult<AudioSynthesizeClipResult>.Fail("DurationSeconds must be > 0", ErrorCodes.INVALID_PARAM);
            if (p.SampleRate <= 0)
                return ToolResult<AudioSynthesizeClipResult>.Fail("SampleRate must be > 0", ErrorCodes.INVALID_PARAM);
            if (p.Channels != 1 && p.Channels != 2)
                return ToolResult<AudioSynthesizeClipResult>.Fail("Channels must be 1 or 2", ErrorCodes.INVALID_PARAM);
            if (!p.AssetPath.StartsWith("Assets/", StringComparison.Ordinal))
                return ToolResult<AudioSynthesizeClipResult>.Fail(
                    "AssetPath must start with 'Assets/'", ErrorCodes.INVALID_PARAM);

            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(p.DurationSeconds * p.SampleRate));
            float amplitude = Mathf.Clamp01(p.Amplitude);
            var mono = GenerateWaveform(waveform, p, sampleCount, amplitude);

            var interleaved = new float[sampleCount * p.Channels];
            for (int i = 0; i < sampleCount; i++)
                for (int c = 0; c < p.Channels; c++)
                    interleaved[i * p.Channels + c] = mono[i];

            string directory = Path.GetDirectoryName(p.AssetPath);
            if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
                return ToolResult<AudioSynthesizeClipResult>.Fail(
                    $"Directory '{directory}' does not exist", ErrorCodes.NOT_FOUND);

            string absolutePath = Path.Combine(Path.GetDirectoryName(Application.dataPath), p.AssetPath);
            AudioToolHelpers.WriteWavPcm16(absolutePath, interleaved, p.SampleRate, p.Channels);
            AssetDatabase.ImportAsset(p.AssetPath, ImportAssetOptions.ForceSynchronousImport);

            string source = $"Synthesized {waveform.ToString().ToLowerInvariant()} " +
                             $"({p.Frequency}Hz{(waveform == Waveform.Sweep ? $"→{p.EndFrequency}Hz" : "")}) " +
                             $"{p.DurationSeconds}s @ {p.SampleRate}Hz {(p.Channels == 1 ? "mono" : "stereo")}";

            return ToolResult<AudioSynthesizeClipResult>.Ok(new AudioSynthesizeClipResult
            {
                AssetPath       = p.AssetPath,
                Waveform        = waveform.ToString(),
                DurationSeconds = p.DurationSeconds,
                SampleRate      = p.SampleRate,
                Channels        = p.Channels,
                SampleCount     = sampleCount,
                Source          = source,
            });
        }

        private static float[] GenerateWaveform(Waveform waveform, AudioSynthesizeClipParams p, int sampleCount, float amplitude)
        {
            var samples = new float[sampleCount];
            float duration = p.DurationSeconds;

            switch (waveform)
            {
                case Waveform.Tone:
                    for (int i = 0; i < sampleCount; i++)
                    {
                        float t = (float)i / p.SampleRate;
                        samples[i] = amplitude * Mathf.Sin(2f * Mathf.PI * p.Frequency * t);
                    }
                    break;

                case Waveform.Noise:
                    var rng = new System.Random(p.Seed);
                    for (int i = 0; i < sampleCount; i++)
                        samples[i] = amplitude * (float)(rng.NextDouble() * 2.0 - 1.0);
                    break;

                case Waveform.Sweep:
                    for (int i = 0; i < sampleCount; i++)
                    {
                        float t = (float)i / p.SampleRate;
                        // Linear chirp: instantaneous frequency f(t) = Frequency + (EndFrequency-Frequency) * t/duration
                        float k = (p.EndFrequency - p.Frequency) / Mathf.Max(duration, 0.0001f);
                        float phase = 2f * Mathf.PI * (p.Frequency * t + 0.5f * k * t * t);
                        samples[i] = amplitude * Mathf.Sin(phase);
                    }
                    break;

                case Waveform.Click:
                    // Impulse followed by a short exponential decay, so it's audible as a click
                    // rather than a single inaudible sample.
                    float decayTau = Mathf.Min(0.02f, duration);
                    for (int i = 0; i < sampleCount; i++)
                    {
                        float t = (float)i / p.SampleRate;
                        samples[i] = amplitude * Mathf.Exp(-t / Mathf.Max(decayTau, 0.0001f)) *
                                     (i == 0 ? 1f : Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 2000f * t)));
                    }
                    break;
            }

            return samples;
        }

        private enum Waveform { Tone, Noise, Sweep, Click }

        private static bool TryParseWaveform(string value, out Waveform result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "tone":  result = Waveform.Tone;  return true;
                case "noise": result = Waveform.Noise; return true;
                case "sweep": result = Waveform.Sweep; return true;
                case "click": result = Waveform.Click; return true;
                default:      result = Waveform.Tone;  return false;
            }
        }
    }
}
