// -----------------------------------------------------------------------------
//  SplashScreenController.cs
//  DECRYPTED — A Walk Through the History of Secret Writing
//
//  Owns the opening beat: elegant branding, a glowing PLAY button, and a set of
//  non-verbal tutorial cards (GRAB / TWIST / POINT / PULL) that auto-cycle so the
//  player learns the museum's vocabulary before entering. Pressing PLAY (poke or
//  ray) raises ExperienceStartedEvent, which the GameManager turns into the
//  transition to the Atrium.
//
//  This script intentionally knows nothing about state transitions beyond firing
//  the one event — it is a self-contained "menu" screen.
// -----------------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using Decrypted.Core;
using UnityEngine;

namespace Decrypted.Interaction
{
    [DisallowMultipleComponent]
    public class SplashScreenController : MonoBehaviour
    {
        [Header("Play")]
        [Tooltip("The glowing PLAY button (poke or ray).")]
        [SerializeField] private PokeButton _playButton;
        [Tooltip("Renderer that pulses to draw attention to PLAY.")]
        [SerializeField] private Renderer _playGlow;
        [SerializeField] private Color _playGlowColor = new Color(0.95f, 0.78f, 0.35f, 1f);
        [SerializeField] private float _playPulseSpeed = 2.0f;
        [SerializeField] private float _playPulseAmount = 0.4f;

        [Header("Tutorial cards")]
        [Tooltip("GRAB / TWIST / POINT / PULL cards, cycled in order.")]
        [SerializeField] private List<TutorialCard> _cards = new List<TutorialCard>();
        [Tooltip("Seconds each card is highlighted before the next.")]
        [SerializeField] private float _cardDwell = 2.4f;

        [Header("Audio")]
        [SerializeField] private string _startSfxKey = "sfx_success_chime";

        private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock _mpb;
        private bool _started;
        private int _cardIndex;
        private float _cardTimer;

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            if (_playButton != null)
            {
                _playButton.OnPressed += OnPlayPressed;
                _playButton.Pressed.AddListener(OnPlayPressedUnity);
            }
            _cardIndex = 0;
            _cardTimer = 0f;
            if (_cards.Count > 0) _cards[0].Focus();
        }

        private void OnDisable()
        {
            if (_playButton != null)
            {
                _playButton.OnPressed -= OnPlayPressed;
                _playButton.Pressed.RemoveListener(OnPlayPressedUnity);
            }
        }

        private void Update()
        {
            // PLAY glow pulse.
            if (_playGlow != null && !_started)
            {
                float k = 1f + Mathf.Sin(Time.time * _playPulseSpeed) * _playPulseAmount;
                _playGlow.GetPropertyBlock(_mpb);
                _mpb.SetColor(EmissionColorID, _playGlowColor * k);
                _playGlow.SetPropertyBlock(_mpb);
            }

            // Cycle tutorial cards.
            if (_cards.Count > 1 && !_started)
            {
                _cardTimer += Time.deltaTime;
                if (_cardTimer >= _cardDwell)
                {
                    _cardTimer = 0f;
                    _cardIndex = (_cardIndex + 1) % _cards.Count;
                    _cards[_cardIndex].Focus();
                }
            }
        }

        private void OnPlayPressedUnity() { } // UnityEvent hook kept for Inspector use

        private void OnPlayPressed(string _)
        {
            if (_started) return;
            _started = true;

            if (!string.IsNullOrEmpty(_startSfxKey) && Managers.AudioManager.Instance != null)
                Managers.AudioManager.Instance.PlayUI(_startSfxKey, 1f, 1f);

            StartCoroutine(BeginAfterChime());
        }

        private IEnumerator BeginAfterChime()
        {
            // Tiny beat so the chime reads before the fade-out begins.
            yield return new WaitForSeconds(0.25f);
            EventBus.Publish(new ExperienceStartedEvent());
        }
    }
}
