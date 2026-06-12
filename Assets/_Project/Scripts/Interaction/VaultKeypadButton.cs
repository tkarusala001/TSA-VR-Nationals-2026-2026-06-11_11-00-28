// -----------------------------------------------------------------------------
//  VaultKeypadButton.cs
//  DECRYPTED — A Walk Through the History of Secret Writing  (Exhibit 3)
//
//  A single key on the modern vault's holographic keypad. Where the Enigma keys
//  are tactile poke buttons, the vault is the "point" exhibit: the player aims a
//  laser pointer (Ray Interactor) at a key, sees it HOVER-highlight, and pulls
//  the trigger to select it. We therefore lean on the hover events for the
//  point-feedback and the select event for the press.
//
//  Visually it fits the vault's dark-metal / cyan-glow language: the key sits
//  dim, lifts to a bright cyan rim on hover, and flashes on press. All emission
//  is driven through a MaterialPropertyBlock so there are zero material
//  instances (important for Quest batching).
// -----------------------------------------------------------------------------

using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Decrypted.Interaction
{
    [AddComponentMenu("DECRYPTED/Vault Keypad Button")]
    public class VaultKeypadButton : XRBaseInteractable
    {
        [Header("Identity")]
        [Tooltip("Value emitted on press: a letter A-Z, or the words ENTER / CLEAR.")]
        [SerializeField] private string _value = "";

        [Header("Highlight (point feedback)")]
        [SerializeField] private Renderer _faceRenderer;
        [SerializeField] private Color _idleColor = new Color(0.05f, 0.18f, 0.22f, 1f);
        [SerializeField] private Color _hoverColor = new Color(0.15f, 0.85f, 0.95f, 1f);
        [SerializeField] private Color _pressColor = new Color(0.6f, 1f, 1f, 1f);
        [Tooltip("Emissive multiplier at full hover.")]
        [SerializeField] private float _hoverIntensity = 1.4f;
        [SerializeField] private float _lerpSpeed = 12f;

        [Header("Press")]
        [Tooltip("Cap transform that dips slightly on press (optional).")]
        [SerializeField] private Transform _cap;
        [SerializeField] private float _travel = 0.004f;
        [SerializeField] private float _debounce = 0.15f;

        [Header("Audio")]
        [SerializeField] private string _clickSfxKey = "sfx_key_clack";

        public string Value { get => _value; set => _value = value; }

        /// <summary>Fired with this key's Value on a debounced press.</summary>
        public event Action<string> OnPressed;

        private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock _mpb;
        private Vector3 _capHome;
        private float _lastPress = -999f;
        private float _flash;            // 0..1 transient press flash
        private bool _hovered;

        protected override void Awake()
        {
            base.Awake();
            _mpb = new MaterialPropertyBlock();
            if (_cap != null) _capHome = _cap.localPosition;
            ApplyColor(_idleColor, 0f);
        }

        protected override void OnHoverEntered(HoverEnterEventArgs args)
        {
            base.OnHoverEntered(args);
            _hovered = true;
        }

        protected override void OnHoverExited(HoverExitEventArgs args)
        {
            base.OnHoverExited(args);
            _hovered = false;
        }

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            TryPress();
        }

        /// <summary>Public so the demo director / tests can press keys directly.</summary>
        public void TryPress()
        {
            if (Time.time - _lastPress < _debounce) return;
            _lastPress = Time.time;
            _flash = 1f;

            if (_cap != null) _cap.localPosition = _capHome - Vector3.up * _travel;

            if (!string.IsNullOrEmpty(_clickSfxKey) && Managers.AudioManager.Instance != null)
                Managers.AudioManager.Instance.Play(_clickSfxKey, transform.position, true, 0.8f,
                    UnityEngine.Random.Range(0.98f, 1.05f));

            OnPressed?.Invoke(_value);
        }

        private void Update()
        {
            // Ease the press flash back down and spring the cap home.
            _flash = Mathf.MoveTowards(_flash, 0f, Time.deltaTime * 5f);
            if (_cap != null)
                _cap.localPosition = Vector3.Lerp(_cap.localPosition, _capHome, Time.deltaTime * 12f);

            // Blend idle -> hover -> press for the face glow.
            Color target = _hovered ? _hoverColor : _idleColor;
            float baseIntensity = _hovered ? _hoverIntensity : 0f;
            if (_flash > 0f)
            {
                target = Color.Lerp(target, _pressColor, _flash);
                baseIntensity = Mathf.Max(baseIntensity, _hoverIntensity) + _flash * 0.8f;
            }
            ApplyColor(target, baseIntensity);
        }

        private void ApplyColor(Color c, float intensity)
        {
            if (_faceRenderer == null) return;
            _faceRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(EmissionColorID, c * Mathf.Max(0f, intensity));
            _faceRenderer.SetPropertyBlock(_mpb);
        }
    }
}
