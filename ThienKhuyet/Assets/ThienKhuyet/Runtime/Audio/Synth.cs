using System;
using System.Collections.Generic;
using ThienKhuyet.Core;

namespace ThienKhuyet.Audio
{
    /// <summary>
    /// Procedural audio synthesis (pure managed, deterministic): placeholder SFX, ambience and generative music built from
    /// oscillators, filtered noise and Karplus-Strong plucked strings (guzheng-like). Every clip can be replaced by recorded audio
    /// through AudioManager's override table without touching gameplay code.
    /// </summary>
    public static class Synth
    {
        public const int Rate = 32000;

        // ------------------------------------------------------------------ primitives
        public static float[] Buffer(float seconds)
        {
            return new float[Math.Max(1, (int)(seconds * Rate))];
        }

        public static float Env(float t, float attack, float decay)
        {
            if (t < attack) return t / Math.Max(1e-5f, attack);
            return (float)Math.Exp(-(t - attack) / Math.Max(1e-5f, decay));
        }

        public static float Smooth(float t, float length, float fade)
        {
            float a = Math.Min(1f, t / Math.Max(1e-5f, fade));
            float b = Math.Min(1f, (length - t) / Math.Max(1e-5f, fade));
            return Math.Max(0f, Math.Min(a, b));
        }

        sealed class Lp
        {
            float y;
            public float Next(float x, float a) { y += a * (x - y); return y; }
        }

        static float Alpha(float cutoff)
        {
            return 1f - (float)Math.Exp(-2.0 * Math.PI * cutoff / Rate);
        }

        public static void Normalize(float[] d, float peak = 0.9f)
        {
            float m = 0f;
            for (int i = 0; i < d.Length; i++) m = Math.Max(m, Math.Abs(d[i]));
            if (m < 1e-6f) return;
            float k = peak / m;
            for (int i = 0; i < d.Length; i++) d[i] *= k;
        }

        public static void FadeEdges(float[] d, float seconds = 0.006f)
        {
            int n = Math.Min(d.Length / 2, (int)(seconds * Rate));
            for (int i = 0; i < n; i++)
            {
                float k = (float)i / n;
                d[i] *= k;
                d[d.Length - 1 - i] *= k;
            }
        }

        /// <summary>Makes a clip loop seamlessly by cross-fading its tail into its head.</summary>
        public static float[] MakeLoop(float[] d, float fadeSeconds)
        {
            int f = Math.Min(d.Length / 2, (int)(fadeSeconds * Rate));
            int len = d.Length - f;
            var r = new float[len];
            Array.Copy(d, r, len);
            for (int i = 0; i < f; i++)
            {
                float k = (float)i / f;
                r[i] = r[i] * k + d[len + i] * (1f - k);
            }
            return r;
        }

        // ------------------------------------------------------------------ SFX recipes
        public static float[] Whoosh(float seconds, float lo, float hi, int seed, float gain = 0.8f)
        {
            float[] d = Buffer(seconds);
            var rng = new Rng(seed);
            var lp1 = new Lp(); var lp2 = new Lp();
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / d.Length;
                float cutoff = lo + (hi - lo) * (float)Math.Sin(t * Math.PI);
                float n = rng.Signed();
                float a = Alpha(cutoff);
                float y = lp2.Next(lp1.Next(n, a), a);
                d[i] = y * (float)Math.Sin(t * Math.PI) * 3.2f * gain;
            }
            FadeEdges(d);
            return d;
        }

        public static float[] Thump(float seconds, float f0, float f1, float noise, int seed, float gain = 0.9f)
        {
            float[] d = Buffer(seconds);
            var rng = new Rng(seed);
            var lp = new Lp();
            double phase = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float f = f1 + (f0 - f1) * (float)Math.Exp(-t * 35f);
                phase += 2 * Math.PI * f / Rate;
                float body = (float)Math.Sin(phase) * Env(t, 0.002f, seconds * 0.28f);
                float click = lp.Next(rng.Signed(), 0.5f) * Env(t, 0.0005f, 0.018f) * noise;
                d[i] = (body + click) * gain;
            }
            FadeEdges(d, 0.002f);
            return d;
        }

        public static float[] Clang(float seconds, float baseHz, int seed, float gain = 0.7f)
        {
            float[] d = Buffer(seconds);
            var rng = new Rng(seed);
            float[] ratios = { 1f, 2.76f, 5.4f, 8.9f };
            float[] amps = { 1f, 0.6f, 0.35f, 0.2f };
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float s = 0f;
                for (int k = 0; k < ratios.Length; k++) s += (float)Math.Sin(2 * Math.PI * baseHz * ratios[k] * t) * amps[k] * (float)Math.Exp(-t * (6f + k * 7f));
                s += rng.Signed() * Env(t, 0.0005f, 0.012f) * 0.6f;
                d[i] = s * gain * 0.5f;
            }
            FadeEdges(d, 0.002f);
            return d;
        }

        public static float[] Sweep(float seconds, float f0, float f1, float noiseMix, int seed, float gain = 0.6f, bool shimmer = false)
        {
            float[] d = Buffer(seconds);
            var rng = new Rng(seed);
            var lp = new Lp();
            double phase = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / d.Length;
                float f = f0 + (f1 - f0) * t * t;
                phase += 2 * Math.PI * f / Rate;
                float tone = (float)Math.Sin(phase) + (shimmer ? 0.4f * (float)Math.Sin(phase * 2.01) : 0f);
                float n = lp.Next(rng.Signed(), Alpha(f * 2f)) * 2f;
                d[i] = (tone * (1f - noiseMix) + n * noiseMix) * (float)Math.Sin(t * Math.PI) * gain;
            }
            FadeEdges(d);
            return d;
        }

        public static float[] Chime(float[] notes, float spacing, float decay, float gain = 0.5f)
        {
            float total = spacing * notes.Length + decay * 2.2f;
            float[] d = Buffer(total);
            for (int n = 0; n < notes.Length; n++)
            {
                int start = (int)(n * spacing * Rate);
                for (int i = 0; start + i < d.Length; i++)
                {
                    float t = (float)i / Rate;
                    float e = Env(t, 0.004f, decay);
                    if (e < 0.001f) break;
                    d[start + i] += ((float)Math.Sin(2 * Math.PI * notes[n] * t) + 0.35f * (float)Math.Sin(2 * Math.PI * notes[n] * 2.0 * t) + 0.12f * (float)Math.Sin(2 * Math.PI * notes[n] * 3.0 * t)) * e * gain;
                }
            }
            FadeEdges(d, 0.004f);
            return d;
        }

        public static float[] NoiseBurst(float seconds, float cutoff, float decay, int seed, float gain = 0.8f)
        {
            float[] d = Buffer(seconds);
            var rng = new Rng(seed);
            var lp = new Lp();
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                d[i] = lp.Next(rng.Signed(), Alpha(cutoff)) * Env(t, 0.002f, decay) * 3f * gain;
            }
            FadeEdges(d, 0.002f);
            return d;
        }

        public static float[] Growl(float seconds, float hz, int seed, float gain = 0.7f)
        {
            float[] d = Buffer(seconds);
            var rng = new Rng(seed);
            var lp = new Lp();
            double ph = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / d.Length;
                float f = hz * (1f + 0.15f * (float)Math.Sin(t * 22f));
                ph += 2 * Math.PI * f / Rate;
                float saw = (float)(ph / (2 * Math.PI) % 1.0) * 2f - 1f;
                float n = lp.Next(rng.Signed(), Alpha(900f)) * 2f;
                float env = (float)Math.Sin(t * Math.PI);
                d[i] = (saw * 0.5f + n * 0.7f) * env * gain * (0.6f + 0.4f * (float)Math.Sin(t * 60f));
            }
            FadeEdges(d);
            return d;
        }

        public static float[] Howl(float seconds, int seed, float gain = 0.6f)
        {
            float[] d = Buffer(seconds);
            double ph = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / d.Length;
                float f = 320f + 330f * (float)Math.Sin(t * Math.PI * 0.9f) + 6f * (float)Math.Sin(t * 70f);
                ph += 2 * Math.PI * f / Rate;
                float env = (float)Math.Sin(t * Math.PI);
                d[i] = ((float)Math.Sin(ph) + 0.35f * (float)Math.Sin(ph * 2) + 0.15f * (float)Math.Sin(ph * 3)) * env * gain * 0.6f;
            }
            FadeEdges(d);
            return d;
        }

        public static float[] Heartbeat(float seconds, float gain = 0.9f)
        {
            float[] d = Buffer(seconds);
            float[] times = { 0f, 0.22f };
            for (int b = 0; b < times.Length; b++)
            {
                int start = (int)(times[b] * Rate);
                for (int i = 0; start + i < d.Length && i < Rate / 3; i++)
                {
                    float t = (float)i / Rate;
                    float f = 60f - 25f * t * 6f;
                    d[start + i] += (float)Math.Sin(2 * Math.PI * Math.Max(25f, f) * t) * Env(t, 0.004f, 0.05f) * gain * (b == 0 ? 1f : 0.7f);
                }
            }
            FadeEdges(d, 0.003f);
            return d;
        }

        public static float[] Rumble(float seconds, float cutoff, int seed, float gain = 0.8f)
        {
            float[] d = Buffer(seconds);
            var rng = new Rng(seed);
            var lp1 = new Lp(); var lp2 = new Lp();
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / d.Length;
                float a = Alpha(cutoff);
                d[i] = lp2.Next(lp1.Next(rng.Signed(), a), a) * 6f * (float)Math.Sin(t * Math.PI) * gain * (0.7f + 0.3f * (float)Math.Sin(t * 40f));
            }
            FadeEdges(d);
            return d;
        }

        public static float[] Glass(float seconds, int seed, float gain = 0.7f)
        {
            float[] d = Buffer(seconds);
            var rng = new Rng(seed);
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                d[i] = rng.Signed() * Env(t, 0.0005f, 0.05f) * 0.5f;
            }
            for (int k = 0; k < 28; k++)
            {
                float f = 1800f + rng.Value() * 5200f;
                int start = (int)(rng.Value() * 0.5f * seconds * Rate);
                float amp = 0.1f + rng.Value() * 0.25f;
                for (int i = 0; start + i < d.Length && i < Rate / 3; i++)
                {
                    float t = (float)i / Rate;
                    d[start + i] += (float)Math.Sin(2 * Math.PI * f * t) * Env(t, 0.0005f, 0.045f) * amp;
                }
            }
            FadeEdges(d, 0.002f);
            return d;
        }

        public static float[] Drone(float seconds, float[] freqs, float gain = 0.5f, float wobble = 0.3f)
        {
            float[] d = Buffer(seconds);
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float s = 0f;
                for (int k = 0; k < freqs.Length; k++)
                    s += (float)Math.Sin(2 * Math.PI * freqs[k] * t + wobble * Math.Sin(t * (0.7f + k * 0.31f))) * (1f / freqs.Length);
                d[i] = s * gain * Smooth(t, seconds, 0.4f);
            }
            return d;
        }

        // ------------------------------------------------------------------ ambience loops
        public static float[] Wind(float seconds, int seed, float gain = 0.6f)
        {
            float[] raw = Buffer(seconds + 1.5f);
            var rng = new Rng(seed);
            var lp1 = new Lp(); var lp2 = new Lp(); var lp3 = new Lp();
            for (int i = 0; i < raw.Length; i++)
            {
                float t = (float)i / Rate;
                float gust = 0.5f + 0.5f * (float)Math.Sin(t * 0.9f) * (float)Math.Sin(t * 0.37f + 1f);
                float cutoff = 220f + 520f * gust;
                float n = rng.Signed();
                raw[i] = lp3.Next(lp2.Next(lp1.Next(n, Alpha(cutoff)), Alpha(cutoff)), Alpha(cutoff * 1.2f)) * (2.2f + 3f * gust) * gain;
            }
            return MakeLoop(raw, 1.4f);
        }

        public static float[] Water(float seconds, int seed, float gain = 0.5f)
        {
            float[] raw = Buffer(seconds + 1.5f);
            var rng = new Rng(seed);
            var lp = new Lp(); var hp = new Lp();
            for (int i = 0; i < raw.Length; i++)
            {
                float t = (float)i / Rate;
                float n = rng.Signed();
                float band = lp.Next(n, 0.35f) - hp.Next(n, 0.03f);
                raw[i] = band * (1.4f + 0.5f * (float)Math.Sin(t * 5.1f) * (float)Math.Sin(t * 2.3f)) * gain;
            }
            return MakeLoop(raw, 1.4f);
        }

        public static float[] Birds(float seconds, int seed, float gain = 0.4f)
        {
            float[] d = Buffer(seconds);
            var rng = new Rng(seed);
            int chirps = (int)(seconds * 1.4f);
            for (int c = 0; c < chirps; c++)
            {
                int start = (int)(rng.Value() * (seconds - 0.6f) * Rate);
                float baseF = 2200f + rng.Value() * 2200f;
                int notes = rng.Int(2, 5);
                float pos = 0f;
                for (int n = 0; n < notes; n++)
                {
                    float len = 0.05f + rng.Value() * 0.07f;
                    float f0 = baseF * (0.9f + rng.Value() * 0.4f), f1 = f0 * (0.8f + rng.Value() * 0.7f);
                    int s0 = start + (int)(pos * Rate);
                    int ln = (int)(len * Rate);
                    double ph = 0;
                    for (int i = 0; i < ln && s0 + i < d.Length; i++)
                    {
                        float t = (float)i / ln;
                        ph += 2 * Math.PI * (f0 + (f1 - f0) * t) / Rate;
                        d[s0 + i] += (float)Math.Sin(ph) * (float)Math.Sin(t * Math.PI) * gain * 0.35f;
                    }
                    pos += len + 0.02f + rng.Value() * 0.04f;
                }
            }
            return d;
        }

        public static float[] Crickets(float seconds, int seed, float gain = 0.25f)
        {
            float[] d = Buffer(seconds);
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float pulse = (float)Math.Max(0, Math.Sin(t * 2 * Math.PI * 7.0)) * (float)Math.Max(0, Math.Sin(t * 2 * Math.PI * 0.45));
                d[i] = (float)Math.Sin(2 * Math.PI * 4300 * t) * pulse * gain + (float)Math.Sin(2 * Math.PI * 3900 * t) * pulse * pulse * gain * 0.6f;
            }
            return MakeLoop(d, 0.3f);
        }

        public static float[] Fire(float seconds, int seed, float gain = 0.5f)
        {
            float[] d = Buffer(seconds);
            var rng = new Rng(seed);
            var lp = new Lp();
            for (int i = 0; i < d.Length; i++) d[i] = lp.Next(rng.Signed(), 0.06f) * 0.8f * gain;
            for (int k = 0; k < (int)(seconds * 14); k++)
            {
                int s = (int)(rng.Value() * (d.Length - 800));
                float a = 0.3f + rng.Value() * 0.7f;
                for (int i = 0; i < 700; i++) d[s + i] += rng.Signed() * (float)Math.Exp(-i / 90f) * a * gain;
            }
            return MakeLoop(d, 0.3f);
        }

        // ------------------------------------------------------------------ plucked strings and music
        /// <summary>Karplus-Strong pluck added into 'dest' at 'startSample'.</summary>
        public static void Pluck(float[] dest, int startSample, float freq, float seconds, float gain, int seed, float damp = 0.4975f)
        {
            int n = Math.Max(2, (int)(Rate / freq));
            var line = new float[n];
            var rng = new Rng(seed);
            for (int i = 0; i < n; i++) line[i] = rng.Signed();
            int len = (int)(seconds * Rate);
            int idx = 0;
            for (int i = 0; i < len && startSample + i < dest.Length; i++)
            {
                int next = (idx + 1) % n;
                float y = line[idx];
                line[idx] = (line[idx] + line[next]) * damp;
                idx = next;
                float fade = Math.Min(1f, (len - i) / (0.1f * Rate));
                dest[startSample + i] += y * gain * fade;
            }
        }

        public static void Pad(float[] dest, int start, float seconds, float[] freqs, float gain)
        {
            int len = (int)(seconds * Rate);
            for (int i = 0; i < len && start + i < dest.Length; i++)
            {
                float t = (float)i / Rate;
                float e = Math.Min(1f, t / (seconds * 0.35f)) * Math.Min(1f, (seconds - t) / (seconds * 0.45f));
                float s = 0f;
                for (int k = 0; k < freqs.Length; k++) s += (float)Math.Sin(2 * Math.PI * freqs[k] * t + 0.4 * Math.Sin(t * 0.5 + k)) * (1f / freqs.Length);
                dest[start + i] += s * e * gain;
            }
        }

        public static void Drum(float[] dest, int start, float gain, float f0 = 90f, float f1 = 38f, float decay = 0.22f)
        {
            int len = (int)(0.5f * Rate);
            for (int i = 0; i < len && start + i < dest.Length; i++)
            {
                float t = (float)i / Rate;
                float f = f1 + (f0 - f1) * (float)Math.Exp(-t * 28f);
                dest[start + i] += (float)Math.Sin(2 * Math.PI * f * t) * (float)Math.Exp(-t / decay) * gain;
            }
        }

        public static void Hat(float[] dest, int start, float gain, int seed)
        {
            var rng = new Rng(seed);
            int len = (int)(0.08f * Rate);
            float prev = 0f;
            for (int i = 0; i < len && start + i < dest.Length; i++)
            {
                float n = rng.Signed();
                float hp = n - prev * 0.9f;
                prev = n;
                dest[start + i] += hp * (float)Math.Exp(-i / (0.018f * Rate)) * gain;
            }
        }

        static readonly float[] PentaMinor = { 0f, 3f, 5f, 7f, 10f };
        static readonly float[] PentaMajor = { 0f, 2f, 4f, 7f, 9f };

        public static float Note(float rootHz, int degree, bool minor)
        {
            float[] scale = minor ? PentaMinor : PentaMajor;
            int oct = degree >= 0 ? degree / 5 : -((-degree + 4) / 5);
            int idx = ((degree % 5) + 5) % 5;
            return rootHz * (float)Math.Pow(2.0, (scale[idx] + 12 * oct) / 12.0);
        }

        /// <summary>Generative music. mood: calm, village, mystery, combat, boss, title, sorrow.</summary>
        public static float[] Music(string mood, int seed)
        {
            var rng = new Rng(seed);
            float bpm; float root; bool minor; float seconds; float padGain; float pluckDensity; bool drums; float drumGain = 0f;
            switch (mood)
            {
                case "village": bpm = 84f; root = 196f; minor = false; seconds = 28f; padGain = 0.16f; pluckDensity = 0.7f; drums = false; break;
                case "mystery": bpm = 56f; root = 146.83f; minor = true; seconds = 30f; padGain = 0.2f; pluckDensity = 0.32f; drums = false; break;
                case "combat": bpm = 128f; root = 164.81f; minor = true; seconds = 24f; padGain = 0.12f; pluckDensity = 0.85f; drums = true; drumGain = 0.55f; break;
                case "boss": bpm = 108f; root = 110f; minor = true; seconds = 26f; padGain = 0.22f; pluckDensity = 0.6f; drums = true; drumGain = 0.8f; break;
                case "title": bpm = 60f; root = 174.61f; minor = true; seconds = 32f; padGain = 0.22f; pluckDensity = 0.5f; drums = false; break;
                case "sorrow": bpm = 50f; root = 130.81f; minor = true; seconds = 30f; padGain = 0.2f; pluckDensity = 0.3f; drums = false; break;
                default: bpm = 66f; root = 220f; minor = true; seconds = 30f; padGain = 0.17f; pluckDensity = 0.55f; drums = false; break;
            }
            float beat = 60f / bpm;
            int beats = (int)(seconds / beat);
            seconds = beats * beat;
            float[] d = Buffer(seconds + 3f);

            // chord pads follow a slow 4-chord loop built on the pentatonic degrees
            int[][] chords = minor
                ? new[] { new[] { 0, 2, 4 }, new[] { -2, 0, 2 }, new[] { -1, 1, 3 }, new[] { -3, -1, 1 } }
                : new[] { new[] { 0, 2, 4 }, new[] { 1, 3, 5 }, new[] { -2, 0, 2 }, new[] { -1, 1, 3 } };
            float chordLen = beat * 8f;
            int chordCount = (int)(seconds / chordLen);
            for (int c = 0; c < chordCount; c++)
            {
                int[] ch = chords[c % chords.Length];
                var freqs = new float[ch.Length + 1];
                for (int k = 0; k < ch.Length; k++) freqs[k] = Note(root * 0.5f, ch[k], minor);
                freqs[ch.Length] = root * 0.25f;
                Pad(d, (int)(c * chordLen * Rate), chordLen * 1.25f, freqs, padGain);
            }
            // plucked melody
            int deg = 5;
            for (int b = 0; b < beats * 2; b++)
            {
                if (rng.Value() > pluckDensity) continue;
                deg += rng.Int(-2, 2);
                deg = Math.Max(0, Math.Min(11, deg));
                float time = b * beat * 0.5f;
                float f = Note(root, deg, minor);
                Pluck(d, (int)(time * Rate), f, 2.2f, 0.22f, seed + b * 7, 0.4985f);
                if (rng.Value() < 0.18f) Pluck(d, (int)(time * Rate), f * 2f, 1.6f, 0.08f, seed + b * 13, 0.497f);
            }
            if (drums)
            {
                for (int b = 0; b < beats; b++)
                {
                    int s = (int)(b * beat * Rate);
                    if (b % 4 == 0 || (mood == "combat" && b % 4 == 2)) Drum(d, s, drumGain);
                    else if (b % 2 == 0 && mood == "boss") Drum(d, s, drumGain * 0.7f, 70f, 32f, 0.3f);
                    Hat(d, s + (int)(beat * 0.5f * Rate), drumGain * 0.18f, seed + b);
                    if (mood == "combat") Hat(d, s, drumGain * 0.1f, seed + b * 3);
                }
            }
            return MakeLoop(Trim(d, seconds + 1.5f), 1.5f);
        }

        static float[] Trim(float[] d, float seconds)
        {
            int n = Math.Min(d.Length, (int)(seconds * Rate));
            var r = new float[n];
            Array.Copy(d, r, n);
            return r;
        }
    }
}
