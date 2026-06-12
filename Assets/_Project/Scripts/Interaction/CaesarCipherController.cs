// -----------------------------------------------------------------------------
//  CaesarCipherController.cs
//  DECRYPTED — A Walk Through the History of Secret Writing  (Exhibit 1)
//
//  Drives the Ancient Cryptography centrepiece: a Roman-styled cipher disk with
//  an outer (ciphertext) ring and an inner (plaintext) ring. The player grabs and
//  twists the inner ring (see XRGrabTwistDisk). For every detent we:
//
//   * Recompute the decoded message by shifting the fixed ciphertext back by the
//     current offset (a live Caesar decode).
//   * Update the encoded + decoded TextMeshPro panels.
//   * Light up the letter glyphs that participate, and animate mapping lines from
//     each ciphertext letter to its plaintext partner so the transformation is
//     literally drawn in front of the player.
//   * Publish CaesarShiftChangedEvent for the audio/idle-hint systems.
//
//  Puzzle data (fixed):
//     ciphertext : "FURVV WKH UXELFRQ"
//     target shift: +3  →  plaintext "CROSS THE RUBICON"
//
//  Solved when the offset equals the target shift; we then chime, freeze the
//  highlight and tell the GameManager.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using Decrypted.Core;
using TMPro;
using UnityEngine;

namespace Decrypted.Interaction
{
    [DisallowMultipleComponent]
    public class CaesarCipherController : MonoBehaviour
    {
        [Header("Puzzle")]
        [Tooltip("Fixed ciphertext shown on the disk (letters A-Z and spaces).")]
        [SerializeField] private string _ciphertext = "FURVV WKH UXELFRQ";
        [Tooltip("Shift (number of detents) that decodes the message. Caesar used 3.")]
        [SerializeField] private int _targetShift = 3;

        [Header("Disk")]
        [SerializeField] private XRGrabTwistDisk _disk;

        [Header("Readouts")]
        [Tooltip("Shows the encoded message (static).")]
        [SerializeField] private TMP_Text _encodedField;
        [Tooltip("Shows the live-decoded message.")]
        [SerializeField] private TMP_Text _decodedField;
        [Tooltip("Shows the current shift value, e.g. 'SHIFT +3'.")]
        [SerializeField] private TMP_Text _shiftField;

        [Header("Ring glyph anchors (A..Z, 26 each)")]
        [Tooltip("World anchors of the outer (ciphertext) ring letters A..Z.")]
        [SerializeField] private Transform[] _outerAnchors = new Transform[26];
        [Tooltip("World anchors of the inner (plaintext) ring letters A..Z.")]
        [SerializeField] private Transform[] _innerAnchors = new Transform[26];

        [Header("Glyph highlight (optional, A..Z)")]
        [SerializeField] private Renderer[] _outerGlyphs = new Renderer[26];
        [SerializeField] private Renderer[] _innerGlyphs = new Renderer[26];
        [SerializeField] private Color _activeGlyphColor = new Color(0.95f, 0.78f, 0.35f, 1f);

        [Header("Mapping lines")]
        [Tooltip("Pre-created LineRenderers reused to connect letter pairs. " +
                 "Provide at least as many as the longest run of unique letters (~12).")]
        [SerializeField] private List<LineRenderer> _mappingLines = new List<LineRenderer>();
        [SerializeField] private Color _lineColor = new Color(0.95f, 0.78f, 0.35f, 0.9f);

        [Header("Solve")]
        [SerializeField] private string _solveSfxKey = "sfx_success_chime";

        private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock _mpb;
        private int _offset;
        private bool _solved;

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _ciphertext = _ciphertext.ToUpperInvariant();
            if (_encodedField != null) _encodedField.text = _ciphertext;
            foreach (var lr in _mappingLines) if (lr != null) lr.enabled = false;
        }

        private void OnEnable()
        {
            if (_disk != null) _disk.OnStepChanged += OnDiskStep;
            Recompute(0);
        }

        private void OnDisable()
        {
            if (_disk != null) _disk.OnStepChanged -= OnDiskStep;
        }

        private void OnDiskStep(int step)
        {
            if (_solved) return;
            Recompute(step);
        }

        // ------------------------------------------------------------- core

        private void Recompute(int offset)
        {
            _offset = ((offset % 26) + 26) % 26;

            string decoded = Decode(_ciphertext, _offset);
            if (_decodedField != null) _decodedField.text = decoded;
            if (_shiftField != null) _shiftField.text = $"SHIFT +{_offset}";

            DrawMappings(decoded);

            EventBus.Publish(new CaesarShiftChangedEvent(_offset, decoded));

            if (_offset == _targetShift) Solve(decoded);
        }

        private static string Decode(string cipher, int offset)
        {
            var sb = new System.Text.StringBuilder(cipher.Length);
            foreach (char c in cipher)
            {
                if (c < 'A' || c > 'Z') { sb.Append(c); continue; }
                int idx = c - 'A';
                int plain = ((idx - offset) % 26 + 26) % 26;
                sb.Append((char)('A' + plain));
            }
            return sb.ToString();
        }

        private void DrawMappings(string decoded)
        {
            // Reset all glyph highlights.
            for (int i = 0; i < 26; i++)
            {
                SetGlyph(_outerGlyphs, i, 0f);
                SetGlyph(_innerGlyphs, i, 0f);
            }

            // For every unique ciphertext letter present, draw a line to its plain
            // partner and light both glyphs.
            int lineIdx = 0;
            var used = new HashSet<int>();
            foreach (char c in _ciphertext)
            {
                if (c < 'A' || c > 'Z') continue;
                int cipherIdx = c - 'A';
                if (!used.Add(cipherIdx)) continue;
                int plainIdx = ((cipherIdx - _offset) % 26 + 26) % 26;

                SetGlyph(_outerGlyphs, cipherIdx, 1f);
                SetGlyph(_innerGlyphs, plainIdx, 1f);

                if (lineIdx < _mappingLines.Count &&
                    _outerAnchors != null && cipherIdx < _outerAnchors.Length &&
                    _innerAnchors != null && plainIdx < _innerAnchors.Length &&
                    _outerAnchors[cipherIdx] != null && _innerAnchors[plainIdx] != null)
                {
                    var lr = _mappingLines[lineIdx++];
                    if (lr != null)
                    {
                        lr.enabled = true;
                        lr.useWorldSpace = true;
                        lr.positionCount = 2;
                        lr.SetPosition(0, _outerAnchors[cipherIdx].position);
                        lr.SetPosition(1, _innerAnchors[plainIdx].position);
                        lr.startColor = _lineColor;
                        lr.endColor = _lineColor;
                    }
                }
            }

            // Disable any unused lines.
            for (int i = lineIdx; i < _mappingLines.Count; i++)
                if (_mappingLines[i] != null) _mappingLines[i].enabled = false;
        }

        private void SetGlyph(Renderer[] glyphs, int idx, float k)
        {
            if (glyphs == null || idx < 0 || idx >= glyphs.Length || glyphs[idx] == null) return;
            glyphs[idx].GetPropertyBlock(_mpb);
            _mpb.SetColor(EmissionColorID, _activeGlyphColor * k);
            glyphs[idx].SetPropertyBlock(_mpb);
        }

        private void Solve(string decoded)
        {
            if (_solved) return;
            _solved = true;

            if (!string.IsNullOrEmpty(_solveSfxKey) && Managers.AudioManager.Instance != null)
                Managers.AudioManager.Instance.Play(_solveSfxKey, transform.position, true, 1f);

            if (_decodedField != null) _decodedField.color = _activeGlyphColor;

            GameManager.Instance?.MarkSolved(MuseumState.AncientRoom);
        }

        /// <summary>Demo hook: rotate the inner ring to the solving shift, which
        /// drives the normal win path through OnDiskStep. Safe to call once.</summary>
        public void AutoSolve()
        {
            if (_solved || _disk == null) return;
            _disk.AnimateToStep(_targetShift, 1.2f);
        }
    }
}
