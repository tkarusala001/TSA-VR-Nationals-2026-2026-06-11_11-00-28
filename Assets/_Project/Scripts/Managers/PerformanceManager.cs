// -----------------------------------------------------------------------------
//  PerformanceManager.cs
//  DECRYPTED — A Walk Through the History of Secret Writing
//
//  Single place that configures and protects the Quest 1 frame budget (72 FPS).
//  Responsibilities:
//    * Lock the display to 72 Hz at boot.
//    * Enable Fixed Foveated Rendering (FFR) and adapt its level if frame time
//      creeps up (cheap GPU savings at the periphery).
//    * Drive memory hygiene at *safe* moments (after a room transition, never
//      mid-interaction) via UnloadUnusedAssets + a deliberate GC pass.
//    * Apply a Quest-appropriate QualitySettings profile.
//
//  Design choice — REFLECTION into Unity.XR.Oculus:
//  We call the Oculus XR plugin through reflection rather than a hard assembly
//  reference. This keeps the script compiling on any machine/CI even before the
//  Oculus XR Plugin is installed, and degrades gracefully in the Editor. The
//  plugin's public surface used here:
//     Unity.XR.Oculus.Performance.TrySetDisplayRefreshRate(float)
//     Unity.XR.Oculus.Utils.foveatedRenderingLevel   (int, 0..3)
//     Unity.XR.Oculus.Utils.useDynamicFoveatedRendering (bool)
//  If you prefer a hard reference, define OCULUS_XR_PRESENT and replace the
//  reflection calls — the method bodies are isolated for exactly that.
// -----------------------------------------------------------------------------

using System;
using System.Reflection;
using Decrypted.Core;
using Decrypted.Util;
using UnityEngine;

namespace Decrypted.Managers
{
    [DefaultExecutionOrder(-95)]
    public class PerformanceManager : Singleton<PerformanceManager>
    {
        [Header("Display")]
        [Tooltip("Target refresh rate for Quest 1. 72 is the supported native rate.")]
        [SerializeField] private float _targetRefreshRate = 72f;

        [Header("Foveated Rendering")]
        [Tooltip("Baseline FFR level (0=off,1=low,2=med,3=high). 2 is a safe default.")]
        [SerializeField, Range(0, 3)] private int _baseFoveationLevel = 2;
        [Tooltip("Let FFR rise to this level under load.")]
        [SerializeField, Range(0, 3)] private int _maxFoveationLevel = 3;
        [Tooltip("Allow the driver to ramp foveation dynamically.")]
        [SerializeField] private bool _dynamicFoveation = true;

        [Header("Adaptive Quality")]
        [Tooltip("Frame time (ms) above which we raise foveation. 72fps ≈ 13.9ms.")]
        [SerializeField] private float _frameTimeBudgetMs = 14.5f;
        [Tooltip("Seconds of sustained overage before reacting (avoids thrashing).")]
        [SerializeField] private float _reactWindow = 2f;

        [Header("Memory")]
        [Tooltip("Run UnloadUnusedAssets + GC after each room transition.")]
        [SerializeField] private bool _cleanupOnRoomChange = true;

        [Header("Quality Profile")]
        [SerializeField] private bool _applyQuestQualityProfile = true;

        private int _currentFoveation;
        private float _overageTimer;
        private float _smoothedFrameMs;

        // -------------------------------------------------------------- boot

        protected override void OnSingletonAwake()
        {
            // Disable Unity's vSync; on Android XR the compositor governs pacing.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Mathf.RoundToInt(_targetRefreshRate);

            if (_applyQuestQualityProfile) ApplyQuestQualityProfile();
        }

        private void Start()
        {
            SetRefreshRate(_targetRefreshRate);
            _currentFoveation = _baseFoveationLevel;
            SetFoveation(_currentFoveation, _dynamicFoveation);
        }

        private void OnEnable()  => EventBus.Subscribe<RoomEnteredEvent>(OnRoomEntered);
        private void OnDisable() => EventBus.Unsubscribe<RoomEnteredEvent>(OnRoomEntered);

        // -------------------------------------------------------- quality

        private void ApplyQuestQualityProfile()
        {
            // Conservative, Quest 1-appropriate settings. These mirror what we set
            // in the URP asset; doing it here too guarantees runtime correctness.
            QualitySettings.shadowResolution = ShadowResolution.Low;
            QualitySettings.shadowDistance = 15f;            // we rely mostly on baked GI
            QualitySettings.shadowCascades = 0;
            QualitySettings.pixelLightCount = 1;
            QualitySettings.softParticles = false;
            QualitySettings.realtimeReflectionProbes = false; // baked probes only
            QualitySettings.skinWeights = SkinWeights.TwoBones;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
            QualitySettings.lodBias = 1.0f;
            QualitySettings.maximumLODLevel = 0;
            QualitySettings.asyncUploadTimeSlice = 2;
            QualitySettings.asyncUploadBufferSize = 16;
        }

        // -------------------------------------------------- adaptive update

        private void Update()
        {
            // Exponential moving average of frame time keeps the signal stable.
            float frameMs = Time.unscaledDeltaTime * 1000f;
            _smoothedFrameMs = Mathf.Lerp(_smoothedFrameMs <= 0 ? frameMs : _smoothedFrameMs, frameMs, 0.05f);

            if (_smoothedFrameMs > _frameTimeBudgetMs)
            {
                _overageTimer += Time.unscaledDeltaTime;
                if (_overageTimer >= _reactWindow && _currentFoveation < _maxFoveationLevel)
                {
                    _currentFoveation++;
                    SetFoveation(_currentFoveation, _dynamicFoveation);
                    _overageTimer = 0f;
                    Debug.Log($"[PerformanceManager] frame {_smoothedFrameMs:F1}ms -> FFR {_currentFoveation}");
                }
            }
            else
            {
                // Recover slowly toward the baseline when we have headroom.
                _overageTimer = Mathf.Max(0f, _overageTimer - Time.unscaledDeltaTime * 0.5f);
                if (_overageTimer <= 0f && _currentFoveation > _baseFoveationLevel
                    && _smoothedFrameMs < _frameTimeBudgetMs * 0.8f)
                {
                    _currentFoveation--;
                    SetFoveation(_currentFoveation, _dynamicFoveation);
                }
            }
        }

        // ----------------------------------------------------------- memory

        private void OnRoomEntered(RoomEnteredEvent _)
        {
            if (_cleanupOnRoomChange) CleanMemory();
        }

        /// <summary>Free assets from the room we just left. Safe between rooms only.</summary>
        public void CleanMemory()
        {
            var op = Resources.UnloadUnusedAssets();
            // A single deliberate collection here is fine — we are mid-fade, not
            // mid-frame-critical-interaction, so a small hitch is invisible.
            op.completed += _ => GC.Collect();
        }

        // -------------------------------------------- Oculus plugin (reflection)

        public void SetRefreshRate(float hz)
        {
#if OCULUS_XR_PRESENT
            Unity.XR.Oculus.Performance.TrySetDisplayRefreshRate(hz);
#else
            InvokeStatic("Unity.XR.Oculus.Performance", "TrySetDisplayRefreshRate", new object[] { hz });
#endif
        }

        public void SetFoveation(int level, bool dynamic)
        {
#if OCULUS_XR_PRESENT
            Unity.XR.Oculus.Utils.useDynamicFoveatedRendering = dynamic;
            Unity.XR.Oculus.Utils.foveatedRenderingLevel = level;
#else
            SetStaticProperty("Unity.XR.Oculus.Utils", "useDynamicFoveatedRendering", dynamic);
            SetStaticProperty("Unity.XR.Oculus.Utils", "foveatedRenderingLevel", level);
#endif
        }

        // -- reflection helpers (no-ops + warning if plugin/type absent) --------

        private static void InvokeStatic(string typeName, string method, object[] args)
        {
            var t = FindType(typeName);
            var m = t?.GetMethod(method, BindingFlags.Public | BindingFlags.Static);
            if (m != null) { try { m.Invoke(null, args); } catch (Exception e) { Warn(method, e); } }
            else WarnMissing($"{typeName}.{method}");
        }

        private static void SetStaticProperty(string typeName, string prop, object value)
        {
            var t = FindType(typeName);
            var p = t?.GetProperty(prop, BindingFlags.Public | BindingFlags.Static);
            if (p != null && p.CanWrite) { try { p.SetValue(null, value); } catch (Exception e) { Warn(prop, e); } }
            else WarnMissing($"{typeName}.{prop}");
        }

        private static Type FindType(string fullName)
        {
            var t = Type.GetType(fullName);
            if (t != null) return t;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }

        private static bool _warnedMissing;
        private static void WarnMissing(string what)
        {
            if (_warnedMissing) return;
            _warnedMissing = true;
            Debug.LogWarning($"[PerformanceManager] Oculus API '{what}' not found " +
                             "(running in Editor or plugin not installed). Skipping device-only call.");
        }
        private static void Warn(string what, Exception e)
            => Debug.LogWarning($"[PerformanceManager] '{what}' threw: {e.Message}");
    }
}
