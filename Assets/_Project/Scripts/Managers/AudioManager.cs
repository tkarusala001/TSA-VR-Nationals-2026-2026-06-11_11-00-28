// -----------------------------------------------------------------------------
//  AudioManager.cs
//  DECRYPTED — A Walk Through the History of Secret Writing
//
//  Centralised audio service:
//    * A named library of AudioClips (imported baked WAVs OR runtime-synthesised
//      via AudioSynth, configurable).
//    * A pool of spatialised AudioSources for one-shots (no per-call alloc).
//    * Per-room ambient loop with smooth crossfade.
//    * AudioMixer snapshot transitions on room entry.
//    * Listens to PlaySfxEvent / StateChangedEvent so exhibits stay decoupled.
//
//  Quest 1 budget: spatialisation uses Unity's built-in panner (cheap). We cap
//  simultaneous voices and reuse sources. Reverb is baked into clips offline,
//  not run as a realtime DSP effect, to save CPU.
// -----------------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using Decrypted.Core;
using Decrypted.Util;
using UnityEngine;
using UnityEngine.Audio;

namespace Decrypted.Managers
{
    [DefaultExecutionOrder(-90)]
    public class AudioManager : Singleton<AudioManager>
    {
        [System.Serializable]
        public struct NamedClip { public string key; public AudioClip clip; }

        [Header("Mixer")]
        [SerializeField] private AudioMixer _mixer;
        [SerializeField] private AudioMixerGroup _sfxGroup;
        [SerializeField] private AudioMixerGroup _ambientGroup;
        [SerializeField] private AudioMixerGroup _uiGroup;

        [Header("Clip Library")]
        [Tooltip("Baked WAVs assigned in the Inspector. Looked up by key.")]
        [SerializeField] private List<NamedClip> _library = new List<NamedClip>();
        [Tooltip("If a key is missing, synthesise it at runtime via AudioSynth.")]
        [SerializeField] private bool _synthesiseMissing = true;

        [Header("Pool")]
        [SerializeField] private int _poolSize = 12;
        [SerializeField, Range(0f, 1f)] private float _defaultSpatialBlend = 1f; // fully 3D
        [SerializeField] private float _maxDistance = 18f;

        [Header("Ambient")]
        [SerializeField] private float _ambientCrossfade = 2f;
        [SerializeField, Range(0f, 1f)] private float _ambientVolume = 0.6f;

        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private readonly List<AudioSource> _pool = new List<AudioSource>();
        private AudioSource _ambientA, _ambientB;
        private bool _ambientAActive = true;
        private string _currentAmbientKey = "";

        // ---------------------------------------------------------------- boot

        protected override void OnSingletonAwake()
        {
            BuildLibrary();
            BuildPool();
            BuildAmbientSources();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<PlaySfxEvent>(OnPlaySfx);
            EventBus.Subscribe<StateChangedEvent>(OnStateChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<PlaySfxEvent>(OnPlaySfx);
            EventBus.Unsubscribe<StateChangedEvent>(OnStateChanged);
        }

        private void BuildLibrary()
        {
            _clips.Clear();
            foreach (var nc in _library)
                if (!string.IsNullOrEmpty(nc.key) && nc.clip != null) _clips[nc.key] = nc.clip;
        }

        private void BuildPool()
        {
            var poolRoot = new GameObject("SFX_Pool").transform;
            poolRoot.SetParent(transform);
            for (int i = 0; i < _poolSize; i++)
            {
                var go = new GameObject($"SFXSource_{i}");
                go.transform.SetParent(poolRoot);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = _defaultSpatialBlend;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.maxDistance = _maxDistance;
                src.dopplerLevel = 0f; // doppler on UI/mechanical sounds feels wrong
                src.outputAudioMixerGroup = _sfxGroup;
                _pool.Add(src);
            }
        }

        private void BuildAmbientSources()
        {
            _ambientA = NewAmbientSource("Ambient_A");
            _ambientB = NewAmbientSource("Ambient_B");
        }

        private AudioSource NewAmbientSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            src.spatialBlend = 0f; // ambience is 2D (envelops the player)
            src.volume = 0f;
            src.outputAudioMixerGroup = _ambientGroup;
            return src;
        }

        // ------------------------------------------------------------- lookup

        public AudioClip GetClip(string key)
        {
            if (_clips.TryGetValue(key, out var c)) return c;
            if (_synthesiseMissing)
            {
                var synth = Synthesise(key);
                if (synth != null) { _clips[key] = synth; return synth; }
            }
            Debug.LogWarning($"[AudioManager] clip '{key}' not found.");
            return null;
        }

        // Map library keys to runtime synthesis so the project is audible
        // immediately and never silently fails.
        private AudioClip Synthesise(string key)
        {
            switch (key)
            {
                case "sfx_brass_click":   return AudioSynth.BrassClick();
                case "sfx_gear_step":     return AudioSynth.GearStep();
                case "sfx_key_clack":     return AudioSynth.KeyClack();
                case "sfx_lamp_on":       return AudioSynth.LampOn();
                case "sfx_lever_pull":    return AudioSynth.LeverPull();
                case "sfx_success_chime": return AudioSynth.SuccessChime();
                case "sfx_vault_rumble":  return AudioSynth.VaultRumble();
                case "sfx_final_chord":   return AudioSynth.FinalChord();
                case "amb_ancient":       return AudioSynth.AmbientAncientDrone();
                case "amb_wwii":          return AudioSynth.AmbientWWII();
                case "amb_vault":         return AudioSynth.AmbientVault();
                case "amb_atrium":        return AudioSynth.AmbientAtrium();
                default:                  return null;
            }
        }

        // ---------------------------------------------------------- one-shots

        /// <summary>Play a 2D UI sound (no spatialisation).</summary>
        public void PlayUI(string key, float volume = 1f, float pitch = 1f)
            => PlayInternal(key, Vector3.zero, false, volume, pitch, _uiGroup);

        /// <summary>Play a one-shot, optionally spatialised at a world position.</summary>
        public void Play(string key, Vector3 position, bool spatial = true, float volume = 1f, float pitch = 1f)
            => PlayInternal(key, position, spatial, volume, pitch, _sfxGroup);

        private void PlayInternal(string key, Vector3 pos, bool spatial, float vol, float pitch, AudioMixerGroup group)
        {
            var clip = GetClip(key);
            if (clip == null) return;
            var src = GetFreeSource();
            if (src == null) return;

            src.outputAudioMixerGroup = group;
            src.transform.position = pos;
            src.spatialBlend = spatial ? _defaultSpatialBlend : 0f;
            src.clip = clip;
            src.volume = vol;
            src.pitch = pitch;
            src.loop = false;
            src.Play();
        }

        private AudioSource GetFreeSource()
        {
            // Reuse the first non-playing source; if all busy, steal the quietest.
            AudioSource quietest = null; float minVol = float.MaxValue;
            foreach (var s in _pool)
            {
                if (!s.isPlaying) return s;
                if (s.volume < minVol) { minVol = s.volume; quietest = s; }
            }
            return quietest;
        }

        // ------------------------------------------------------------ ambient

        public void SetAmbient(string key)
        {
            if (key == _currentAmbientKey) return;
            var clip = GetClip(key);
            if (clip == null) return;
            _currentAmbientKey = key;
            StopCoroutine(nameof(CrossfadeAmbient));
            StartCoroutine(CrossfadeAmbient(clip));
        }

        private IEnumerator CrossfadeAmbient(AudioClip next)
        {
            var from = _ambientAActive ? _ambientA : _ambientB;
            var to   = _ambientAActive ? _ambientB : _ambientA;
            _ambientAActive = !_ambientAActive;

            to.clip = next;
            to.volume = 0f;
            to.Play();

            float t = 0f;
            float startFrom = from.volume;
            while (t < _ambientCrossfade)
            {
                t += Time.deltaTime;
                float k = t / _ambientCrossfade;
                to.volume = Mathf.Lerp(0f, _ambientVolume, k);
                from.volume = Mathf.Lerp(startFrom, 0f, k);
                yield return null;
            }
            to.volume = _ambientVolume;
            from.volume = 0f;
            from.Stop();
        }

        // ------------------------------------------------------------ mixer

        public void TransitionSnapshot(string snapshotName, float seconds)
        {
            if (_mixer == null || string.IsNullOrEmpty(snapshotName)) return;
            var snap = _mixer.FindSnapshot(snapshotName);
            if (snap != null) snap.TransitionTo(seconds);
        }

        /// <summary>Set a mixer exposed parameter in decibels (e.g. master duck).</summary>
        public void SetVolumeDb(string exposedParam, float db)
        {
            if (_mixer != null) _mixer.SetFloat(exposedParam, db);
        }

        // ---------------------------------------------------------- listeners

        private void OnPlaySfx(PlaySfxEvent e) => Play(e.Key, e.Position, e.Spatial);

        private void OnStateChanged(StateChangedEvent e)
        {
            // Swap ambience + snapshot to match the room descriptor.
            var sc = FindObjectOfType<SceneController>();
            var desc = sc != null ? sc.GetDescriptor(e.Current) : null;
            if (desc != null)
            {
                if (!string.IsNullOrEmpty(desc.ambientKey)) SetAmbient(desc.ambientKey);
                if (!string.IsNullOrEmpty(desc.mixerSnapshot))
                    TransitionSnapshot(desc.mixerSnapshot, desc.snapshotBlend);
            }
        }
    }
}
