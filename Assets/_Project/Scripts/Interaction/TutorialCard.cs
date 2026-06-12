// -----------------------------------------------------------------------------
//  TutorialCard.cs
//  DECRYPTED — A Walk Through the History of Secret Writing
//
//  A single non-verbal tutorial card shown on the splash screen. Each card
//  teaches one core verb — GRAB, TWIST, POINT, PULL — using an icon, a one-word
//  TMP label and a small looping demonstrative animation (a ghost hand / arrow),
//  with NO spoken or sentence-level text. Cards gently float and pulse to draw
//  the eye, and play a soft tick when they cycle in.
//
//  The SplashScreenController owns a list of these and cycles them; this class
//  just handles its own presentation and the looping hint motion.
// -----------------------------------------------------------------------------

using TMPro;
using UnityEngine;

namespace Decrypted.Interaction
{
    public enum TutorialVerb { Grab, Twist, Point, Pull, Press }

    [DisallowMultipleComponent]
    public class TutorialCard : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private TutorialVerb _verb = TutorialVerb.Grab;
        [Tooltip("One-word label (GRAB / TWIST / POINT / PULL / PRESS).")]
        [SerializeField] private TMP_Text _label;

        [Header("Demonstration")]
        [Tooltip("Animator that loops a ghost-hand / arrow showing the gesture.")]
        [SerializeField] private Animator _demoAnimator;
        [Tooltip("Icon renderer (emissive glyph).")]
        [SerializeField] private Renderer _iconRenderer;

        [Header("Idle motion")]
        [Tooltip("Vertical bob amplitude (m).")]
        [SerializeField] private float _bob = 0.015f;
        [SerializeField] private float _bobSpeed = 1.6f;
        [Tooltip("Emissive pulse amount.")]
        [SerializeField] private float _pulse = 0.25f;
        [SerializeField] private float _pulseSpeed = 2.2f;

        private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock _mpb;
        private Vector3 _home;
        private Color _baseEmission = Color.white;
        private float _phase;

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _home = transform.localPosition;
            _phase = Random.value * 10f; // desync multiple cards
            if (_label != null) _label.text = _verb.ToString().ToUpperInvariant();
            if (_iconRenderer != null && _iconRenderer.sharedMaterial != null &&
                _iconRenderer.sharedMaterial.HasProperty(EmissionColorID))
                _baseEmission = _iconRenderer.sharedMaterial.GetColor(EmissionColorID);
        }

        private void Update()
        {
            float t = Time.time + _phase;
            // Bob.
            transform.localPosition = _home + Vector3.up * (Mathf.Sin(t * _bobSpeed) * _bob);
            // Emissive pulse.
            if (_iconRenderer != null)
            {
                float k = 1f + Mathf.Sin(t * _pulseSpeed) * _pulse;
                _iconRenderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(EmissionColorID, _baseEmission * k);
                _iconRenderer.SetPropertyBlock(_mpb);
            }
        }

        /// <summary>Called when this card becomes the active/highlighted one.</summary>
        public void Focus()
        {
            if (_demoAnimator != null) _demoAnimator.SetTrigger("Play");
            if (Managers.AudioManager.Instance != null)
                Managers.AudioManager.Instance.PlayUI("sfx_brass_click", 0.4f, 1.3f);
        }

        public TutorialVerb Verb => _verb;
    }
}
