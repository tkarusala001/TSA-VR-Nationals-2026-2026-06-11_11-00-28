// -----------------------------------------------------------------------------
//  SignalTraceRenderer.cs
//  DECRYPTED — A Walk Through the History of Secret Writing
//
//  Animates a glowing pulse travelling along a poly-line path. Used by the Enigma
//  exhibit to make the abstract "electrical signal flows through the rotors" idea
//  literally visible: when a key is pressed, a bright dot races from the keyboard,
//  through each rotor contact, to the lit lamp. Also reused for the modern-vault
//  "data stream" accents.
//
//  Implementation notes (Quest-friendly):
//   * Uses a single LineRenderer with a baked gradient that we *scroll* by moving
//     a narrow bright band along the line's vertex colours. No per-frame mesh
//     rebuilds beyond the colour gradient, which is cheap.
//   * Path points are plain Transforms so artists can route wiring in-scene.
//   * Multiple concurrent pulses are supported via a small struct pool.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

namespace Decrypted.Visuals
{
    [RequireComponent(typeof(LineRenderer))]
    public class SignalTraceRenderer : MonoBehaviour
    {
        [Header("Path")]
        [Tooltip("Ordered waypoints the pulse follows. The LineRenderer is rebuilt " +
                 "from these on Awake (and on demand via RebuildPath).")]
        [SerializeField] private List<Transform> _waypoints = new List<Transform>();
        [Tooltip("Sub-divisions between waypoints for a smoother pulse and curve.")]
        [SerializeField] private int _subdivisions = 6;

        [Header("Look")]
        [SerializeField] private Color _baseColor = new Color(0.05f, 0.18f, 0.22f, 1f);
        [SerializeField] private Color _pulseColor = new Color(0.2f, 1f, 1f, 1f);
        [Tooltip("Fraction of the line the bright band occupies (0..1).")]
        [Range(0.02f, 0.5f)] [SerializeField] private float _pulseWidth = 0.12f;

        [Header("Motion")]
        [Tooltip("Seconds for one pulse to travel the full path.")]
        [SerializeField] private float _travelSeconds = 0.6f;
        [Tooltip("Continuously emit pulses (used for the idle 'powered' state).")]
        [SerializeField] private bool _loop = false;
        [SerializeField] private float _loopInterval = 1.2f;

        private LineRenderer _line;
        private Vector3[] _points;
        private Gradient _gradient;
        private GradientColorKey[] _colorKeys;
        private GradientAlphaKey[] _alphaKeys;

        private struct Pulse { public float t; }
        private readonly List<Pulse> _pulses = new List<Pulse>(4);
        private float _loopTimer;

        private void Awake()
        {
            _line = GetComponent<LineRenderer>();
            RebuildPath();
            _gradient = new Gradient();
        }

        private void Update()
        {
            if (_points == null || _points.Length < 2) return;

            // Drive looping emission.
            if (_loop)
            {
                _loopTimer -= Time.deltaTime;
                if (_loopTimer <= 0f) { Emit(); _loopTimer = _loopInterval; }
            }

            // Advance pulses.
            bool any = _pulses.Count > 0;
            for (int i = _pulses.Count - 1; i >= 0; i--)
            {
                var p = _pulses[i];
                p.t += Time.deltaTime / Mathf.Max(0.01f, _travelSeconds);
                if (p.t >= 1f + _pulseWidth) { _pulses.RemoveAt(i); continue; }
                _pulses[i] = p;
            }

            // Compose the gradient from base + the brightest pulse band at each
            // sampled position. For Quest we keep it to a handful of keys.
            ComposeGradient(any);
        }

        /// <summary>Launch one pulse from the start of the path to the end.</summary>
        public void Emit()
        {
            if (_pulses.Count >= 4) _pulses.RemoveAt(0); // cap concurrent pulses
            _pulses.Add(new Pulse { t = 0f });
            enabled = true;
        }

        /// <summary>Turn the steady "powered" glow on/off (loop emission).</summary>
        public void SetPowered(bool powered)
        {
            _loop = powered;
            if (powered && _loopTimer <= 0f) _loopTimer = 0.05f;
        }

        /// <summary>Rebuild the LineRenderer vertices from the waypoint transforms.</summary>
        public void RebuildPath()
        {
            if (_line == null) _line = GetComponent<LineRenderer>();
            var valid = new List<Vector3>();
            foreach (var w in _waypoints) if (w != null) valid.Add(w.position);
            if (valid.Count < 2) { _points = null; _line.positionCount = 0; return; }

            // Catmull-Rom-ish smoothing via simple subdivision (linear is fine for wiring).
            var dense = new List<Vector3>();
            for (int i = 0; i < valid.Count - 1; i++)
            {
                Vector3 a = valid[i], b = valid[i + 1];
                int steps = Mathf.Max(1, _subdivisions);
                for (int s = 0; s < steps; s++)
                    dense.Add(Vector3.Lerp(a, b, s / (float)steps));
            }
            dense.Add(valid[valid.Count - 1]);

            _points = dense.ToArray();
            _line.useWorldSpace = true;
            _line.positionCount = _points.Length;
            _line.SetPositions(_points);

            _colorKeys = new GradientColorKey[3];
            _alphaKeys = new GradientAlphaKey[3];
        }

        private void ComposeGradient(bool anyPulse)
        {
            if (!anyPulse)
            {
                // Flat base colour when idle (cheap path).
                _line.startColor = _baseColor;
                _line.endColor = _baseColor;
                return;
            }

            // Find the leading pulse centre (the most advanced one looks best on top).
            float centre = 0f;
            foreach (var p in _pulses) centre = Mathf.Max(centre, Mathf.Clamp01(p.t));

            float lo = Mathf.Clamp01(centre - _pulseWidth * 0.5f);
            float hi = Mathf.Clamp01(centre + _pulseWidth * 0.5f);

            _colorKeys[0] = new GradientColorKey(_baseColor, Mathf.Max(0f, lo - 0.001f));
            _colorKeys[1] = new GradientColorKey(_pulseColor, centre);
            _colorKeys[2] = new GradientColorKey(_baseColor, Mathf.Min(1f, hi + 0.001f));

            _alphaKeys[0] = new GradientAlphaKey(_baseColor.a, 0f);
            _alphaKeys[1] = new GradientAlphaKey(1f, centre);
            _alphaKeys[2] = new GradientAlphaKey(_baseColor.a, 1f);

            _gradient.SetKeys(_colorKeys, _alphaKeys);
            _line.colorGradient = _gradient;
        }
    }
}
