// -----------------------------------------------------------------------------
//  AudioSynth.cs
//  DECRYPTED — A Walk Through the History of Secret Writing
//
//  Generates ORIGINAL audio at runtime as AudioClips — no imported files, no
//  royalty-free packs. This satisfies the "design all audio from scratch"
//  requirement two ways:
//    1) The offline Python tooling (Tooling/Audio) bakes high-quality WAVs that
//       designers import for the shipped build.
//    2) THIS class can synthesise the same families of sound at runtime, so the
//       project is fully audible the moment you press Play, and so procedural
//       one-shots (e.g. brass clicks with subtle pitch variation) never repeat
//       identically.
//
//  Everything is built from first principles: oscillators, envelopes (ADSR),
//  filtered noise, additive partials, simple waveguide-ish resonance, and a
//  lightweight Schroeder reverb. Clips are mono PCM at the project sample rate.
//
//  CPU note: synthesis happens on the main thread when a clip is *created*, not
//  per-frame. Bake one-shots at Awake and reuse; only re-synthesise when you
//  explicitly want variation.
// -----------------------------------------------------------------------------

using System;
using UnityEngine;

namespace Decrypted.Util
{
    public static class AudioSynth
    {
        public const int SampleRate = 48000;
        private static readonly System.Random _rng = new System.Random(20240517);

        // -------------------------------------------------------- primitives

        /// <summary>ADSR envelope value at time t (seconds) for a note of given length.</summary>
        public static float Adsr(float t, float length, float a, float d, float s, float r)
        {
            if (t < 0f) return 0f;
            float sustainEnd = Mathf.Max(a + d, length - r);
            if (t < a) return Mathf.Lerp(0f, 1f, t / Mathf.Max(a, 1e-4f));
            if (t < a + d) return Mathf.Lerp(1f, s, (t - a) / Mathf.Max(d, 1e-4f));
            if (t < sustainEnd) return s;
            if (t < sustainEnd + r) return Mathf.Lerp(s, 0f, (t - sustainEnd) / Mathf.Max(r, 1e-4f));
            return 0f;
        }

        private static float Sine(float phase) => Mathf.Sin(phase * 2f * Mathf.PI);
        private static float Saw(float phase) => 2f * (phase - Mathf.Floor(phase + 0.5f));
        private static float Square(float phase) => Mathf.Sign(Sine(phase));
        private static float Tri(float phase) => 2f * Mathf.Abs(Saw(phase)) - 1f;
        private static float Noise() => (float)(_rng.NextDouble() * 2.0 - 1.0);

        private static AudioClip MakeClip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float[] Alloc(float seconds) => new float[Mathf.Max(1, (int)(seconds * SampleRate))];

        private static void Normalize(float[] buf, float peak = 0.9f)
        {
            float max = 1e-6f;
            for (int i = 0; i < buf.Length; i++) max = Mathf.Max(max, Mathf.Abs(buf[i]));
            float g = peak / max;
            for (int i = 0; i < buf.Length; i++) buf[i] *= g;
        }

        // One-pole low-pass (gentle, cheap). cutoff in Hz.
        private static void LowPass(float[] buf, float cutoff)
        {
            float dt = 1f / SampleRate;
            float rc = 1f / (2f * Mathf.PI * cutoff);
            float alpha = dt / (rc + dt);
            float prev = 0f;
            for (int i = 0; i < buf.Length; i++) { prev += alpha * (buf[i] - prev); buf[i] = prev; }
        }

        // One-pole high-pass.
        private static void HighPass(float[] buf, float cutoff)
        {
            float dt = 1f / SampleRate;
            float rc = 1f / (2f * Mathf.PI * cutoff);
            float alpha = rc / (rc + dt);
            float prevIn = 0f, prevOut = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                float outv = alpha * (prevOut + buf[i] - prevIn);
                prevIn = buf[i]; prevOut = outv; buf[i] = outv;
            }
        }

        // Tiny Schroeder reverb (4 combs + 2 allpass) — adds the "room" to stone/vault.
        private static void Reverb(float[] buf, float mix, float decay)
        {
            int[] combDelays = { 1117, 1188, 1277, 1356 };
            int[] apDelays = { 225, 556 };
            float[] wet = (float[])buf.Clone();

            foreach (int d in combDelays)
            {
                var line = new float[d];
                int idx = 0;
                for (int i = 0; i < wet.Length; i++)
                {
                    float y = line[idx];
                    line[idx] = buf[i] + y * decay;
                    wet[i] += y;
                    idx = (idx + 1) % d;
                }
            }
            foreach (int d in apDelays)
            {
                var line = new float[d];
                int idx = 0;
                const float g = 0.5f;
                for (int i = 0; i < wet.Length; i++)
                {
                    float bufv = line[idx];
                    float y = -g * wet[i] + bufv;
                    line[idx] = wet[i] + g * y;
                    wet[i] = y;
                    idx = (idx + 1) % d;
                }
            }
            for (int i = 0; i < buf.Length; i++) buf[i] = Mathf.Lerp(buf[i], wet[i] * 0.25f, mix);
        }

        // --------------------------------------------------------- one-shots

        /// <summary>Brass "click" — short metallic transient (Caesar disk detents).</summary>
        public static AudioClip BrassClick(float pitch = 1f)
        {
            var buf = Alloc(0.12f);
            float baseF = 2200f * pitch;
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 60f);
                // Inharmonic partials give the metallic timbre.
                float s = Sine(baseF * t) * 0.6f
                        + Sine(baseF * 2.76f * t) * 0.3f
                        + Sine(baseF * 5.4f * t) * 0.15f;
                buf[i] = s * env + Noise() * env * 0.15f;
            }
            HighPass(buf, 800f);
            Normalize(buf, 0.7f);
            return MakeClip("sfx_brass_click", buf);
        }

        /// <summary>Mechanical gear step — band-passed noise burst with a thunk.</summary>
        public static AudioClip GearStep(float pitch = 1f)
        {
            var buf = Alloc(0.18f);
            float thunk = 140f * pitch;
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 28f);
                float body = Sine(thunk * t) * 0.5f + Tri(thunk * 1.5f * t) * 0.2f;
                buf[i] = (body + Noise() * 0.5f) * env;
            }
            LowPass(buf, 1800f);
            HighPass(buf, 90f);
            Normalize(buf, 0.7f);
            return MakeClip("sfx_gear_step", buf);
        }

        /// <summary>Enigma key strike — sharp keyboard clack.</summary>
        public static AudioClip KeyClack()
        {
            var buf = Alloc(0.09f);
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 90f);
                buf[i] = (Noise() * 0.8f + Square(900f * t) * 0.2f) * env;
            }
            HighPass(buf, 1500f);
            Normalize(buf, 0.6f);
            return MakeClip("sfx_key_clack", buf);
        }

        /// <summary>Lamp illumination — soft filtered "ping" rising.</summary>
        public static AudioClip LampOn()
        {
            var buf = Alloc(0.4f);
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / SampleRate;
                float f = Mathf.Lerp(500f, 1400f, Mathf.Clamp01(t * 4f));
                float env = Adsr(t, 0.4f, 0.01f, 0.08f, 0.4f, 0.2f);
                buf[i] = Sine(f * t) * env * 0.8f + Sine(f * 2f * t) * env * 0.2f;
            }
            Normalize(buf, 0.6f);
            return MakeClip("sfx_lamp_on", buf);
        }

        /// <summary>Lever pull — descending mechanical groan with a final clunk.</summary>
        public static AudioClip LeverPull()
        {
            var buf = Alloc(0.7f);
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / SampleRate;
                float f = Mathf.Lerp(220f, 90f, Mathf.Clamp01(t / 0.5f));
                float groan = (Saw(f * t) * 0.5f + Noise() * 0.3f) * Mathf.Exp(-t * 2.5f);
                float clunk = (t > 0.5f) ? Sine(70f * t) * Mathf.Exp(-(t - 0.5f) * 30f) : 0f;
                buf[i] = groan + clunk;
            }
            LowPass(buf, 1500f);
            Normalize(buf, 0.8f);
            return MakeClip("sfx_lever_pull", buf);
        }

        /// <summary>Success chime — bright ascending major triad.</summary>
        public static AudioClip SuccessChime()
        {
            var buf = Alloc(1.2f);
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f }; // C5 E5 G5 C6
            for (int n = 0; n < notes.Length; n++)
            {
                float start = n * 0.09f;
                for (int i = 0; i < buf.Length; i++)
                {
                    float t = (float)i / SampleRate - start;
                    if (t < 0f) continue;
                    float env = Adsr(t, 1.0f, 0.005f, 0.15f, 0.3f, 0.6f);
                    buf[i] += (Sine(notes[n] * t) * 0.7f + Sine(notes[n] * 2f * t) * 0.2f) * env * 0.5f;
                }
            }
            Reverb(buf, 0.25f, 0.6f);
            Normalize(buf, 0.7f);
            return MakeClip("sfx_success_chime", buf);
        }

        /// <summary>Vault rumble — deep filtered noise swell (door + locking).</summary>
        public static AudioClip VaultRumble(float seconds = 2.5f)
        {
            var buf = Alloc(seconds);
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / SampleRate;
                float env = Adsr(t, seconds, 0.4f, 0.3f, 0.8f, 0.6f);
                float sub = Sine(38f * t) * 0.6f + Sine(55f * t) * 0.3f;
                buf[i] = (sub + Noise() * 0.4f) * env;
            }
            LowPass(buf, 220f);
            Normalize(buf, 0.85f);
            return MakeClip("sfx_vault_rumble", buf);
        }

        /// <summary>Final reveal chord — lush sustained chord (the emotional payoff).</summary>
        public static AudioClip FinalChord(float seconds = 4.5f)
        {
            var buf = Alloc(seconds);
            // Cmaj add9 across octaves for a warm, resolved cadence.
            float[] freqs = { 130.81f, 196f, 261.63f, 329.63f, 392f, 587.33f };
            foreach (var f in freqs)
            {
                float detune = 1f + ((float)_rng.NextDouble() - 0.5f) * 0.004f; // chorusy width
                for (int i = 0; i < buf.Length; i++)
                {
                    float t = (float)i / SampleRate;
                    float env = Adsr(t, seconds, 1.2f, 0.5f, 0.7f, 1.6f);
                    buf[i] += (Sine(f * detune * t) * 0.6f + Tri(f * detune * t) * 0.2f) * env * 0.18f;
                }
            }
            Reverb(buf, 0.4f, 0.78f);
            Normalize(buf, 0.8f);
            return MakeClip("sfx_final_chord", buf);
        }

        // ----------------------------------------------------- ambient loops

        /// <summary>Ancient room drone — slow detuned low partials, breathing.</summary>
        public static AudioClip AmbientAncientDrone(float seconds = 12f)
        {
            var buf = Alloc(seconds);
            float[] partials = { 55f, 82.4f, 110f, 164.8f }; // A1-based stack
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / SampleRate;
                float breathe = 0.7f + 0.3f * Sine(0.06f * t); // ~16s swell
                float s = 0f;
                foreach (var f in partials)
                {
                    float lfo = 1f + 0.002f * Sine(0.13f * t + f);
                    s += Sine(f * lfo * t);
                }
                buf[i] = s / partials.Length * breathe;
            }
            LowPass(buf, 700f);
            Reverb(buf, 0.35f, 0.8f);
            LoopFade(buf, 0.5f);
            Normalize(buf, 0.5f);
            return MakeClip("amb_ancient", buf);
        }

        /// <summary>WWII room ambience — faint electrical hum + ticking room tone.</summary>
        public static AudioClip AmbientWWII(float seconds = 12f)
        {
            var buf = Alloc(seconds);
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / SampleRate;
                float hum = Sine(50f * t) * 0.4f + Sine(150f * t) * 0.15f; // mains hum + harmonic
                float roomTone = Noise() * 0.05f;
                buf[i] = hum * 0.5f + roomTone;
            }
            HighPass(buf, 40f);
            LowPass(buf, 4000f);
            LoopFade(buf, 0.4f);
            Normalize(buf, 0.4f);
            return MakeClip("amb_wwii", buf);
        }

        /// <summary>Vault ambience — clean digital pad with subtle data shimmer.</summary>
        public static AudioClip AmbientVault(float seconds = 12f)
        {
            var buf = Alloc(seconds);
            float[] pad = { 220f, 277.18f, 329.63f }; // A minor-ish cool pad
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / SampleRate;
                float s = 0f;
                foreach (var f in pad) s += Sine(f * t) * 0.3f + Tri(f * 2f * t) * 0.05f;
                // shimmering high "data" sparkle, sparse
                float sparkle = (Noise() > 0.997f) ? Sine(3000f * t) * 0.3f : 0f;
                buf[i] = s / pad.Length + sparkle;
            }
            HighPass(buf, 120f);
            Reverb(buf, 0.3f, 0.7f);
            LoopFade(buf, 0.5f);
            Normalize(buf, 0.45f);
            return MakeClip("amb_vault", buf);
        }

        /// <summary>Atrium ambience — airy, neutral, welcoming hall tone.</summary>
        public static AudioClip AmbientAtrium(float seconds = 12f)
        {
            var buf = Alloc(seconds);
            float[] pad = { 196f, 293.66f, 392f };
            for (int i = 0; i < buf.Length; i++)
            {
                float t = (float)i / SampleRate;
                float breathe = 0.75f + 0.25f * Sine(0.05f * t);
                float s = 0f;
                foreach (var f in pad) s += Sine(f * t);
                buf[i] = s / pad.Length * breathe;
            }
            LowPass(buf, 2200f);
            Reverb(buf, 0.45f, 0.85f); // big, reverberant hall
            LoopFade(buf, 0.6f);
            Normalize(buf, 0.4f);
            return MakeClip("amb_atrium", buf);
        }

        // Crossfade the head and tail of a buffer so it loops seamlessly.
        private static void LoopFade(float[] buf, float seconds)
        {
            int n = Mathf.Min((int)(seconds * SampleRate), buf.Length / 2);
            for (int i = 0; i < n; i++)
            {
                float k = (float)i / n;
                int tail = buf.Length - n + i;
                float mixed = buf[tail] * (1f - k) + buf[i] * k;
                buf[i] = mixed;
                buf[tail] = mixed;
            }
        }
    }
}
