// -----------------------------------------------------------------------------
//  PlaqueController.cs
//  DECRYPTED — A Walk Through the History of Secret Writing
//
//  A diegetic museum plaque. All teaching in DECRYPTED is non-verbal, so plaques
//  carry the bulk of the educational text. To keep the scene calm (and to respect
//  Quest fill-rate), plaques are dim/blank until the player approaches, then fade
//  their text + a subtle backlight in. They gently billboard toward the player on
//  the yaw axis only (never roll/pitch — that reads as nauseating in VR).
//
//  The component is fully data-driven from the Inspector: assign a TMP target,
//  a CanvasGroup (or a renderer for a physical etched plaque), the trigger radius
//  and the copy. No per-plaque code.
// -----------------------------------------------------------------------------

using TMPro;
using UnityEngine;

namespace Decrypted.Visuals
{
    [DisallowMultipleComponent]
    public class PlaqueController : MonoBehaviour
    {
        [Header("Content")]
        [Tooltip("Optional heading (bold, accent colour).")]
        [SerializeField] private TMP_Text _titleField;
        [Tooltip("Body copy.")]
        [SerializeField] private TMP_Text _bodyField;

        [TextArea] [SerializeField] private string _title = "";
        [TextArea(2, 8)] [SerializeField] private string _body = "";

        [Header("Reveal")]
        [Tooltip("CanvasGroup faded in/out with proximity. If null we fade TMP alpha.")]
        [SerializeField] private CanvasGroup _group;
        [Tooltip("Distance (m) at which the plaque begins to reveal.")]
        [SerializeField] private float _revealRadius = 3.0f;
        [Tooltip("Distance (m) at which it is fully revealed.")]
        [SerializeField] private float _fullRadius = 1.6f;
        [Tooltip("Fade smoothing.")]
        [SerializeField] private float _fadeLerp = 6f;
        [Tooltip("Reveal even without proximity (e.g. the big conclusion plaque).")]
        [SerializeField] private bool _alwaysVisible = false;

        [Header("Billboard")]
        [Tooltip("Softly rotate to face the player on the yaw axis.")]
        [SerializeField] private bool _billboardYaw = true;
        [SerializeField] private float _billboardLerp = 3f;
        [Tooltip("If true keep the authored facing and never rotate.")]
        [SerializeField] private bool _fixedFacing = false;

        private Transform _player;
        private float _alpha;

        private void Awake()
        {
            if (_titleField != null) _titleField.text = _title;
            if (_bodyField != null) _bodyField.text = _body;
            _alpha = _alwaysVisible ? 1f : 0f;
            ApplyAlpha(_alpha);
        }

        private void OnEnable()
        {
            if (Camera.main != null) _player = Camera.main.transform;
        }

        private void Update()
        {
            if (_player == null)
            {
                if (Camera.main == null) return;
                _player = Camera.main.transform;
            }

            // Proximity → target alpha.
            float target = 1f;
            if (!_alwaysVisible)
            {
                float d = Vector3.Distance(_player.position, transform.position);
                target = Mathf.InverseLerp(_revealRadius, _fullRadius, d); // 0 far → 1 near
            }
            _alpha = Mathf.MoveTowards(_alpha, target, _fadeLerp * Time.deltaTime);
            ApplyAlpha(_alpha);

            // Billboard (yaw only) — only worth doing while at least partly visible.
            if (_billboardYaw && !_fixedFacing && _alpha > 0.01f)
            {
                Vector3 toPlayer = _player.position - transform.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.0001f)
                {
                    // Plaques read correctly when their +Z faces the viewer.
                    Quaternion want = Quaternion.LookRotation(-toPlayer.normalized, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, want, _billboardLerp * Time.deltaTime);
                }
            }
        }

        /// <summary>Replace the plaque copy at runtime (used by some exhibits).</summary>
        public void SetText(string title, string body)
        {
            _title = title; _body = body;
            if (_titleField != null) _titleField.text = title;
            if (_bodyField != null) _bodyField.text = body;
        }

        private void ApplyAlpha(float a)
        {
            if (_group != null) { _group.alpha = a; return; }
            if (_titleField != null) SetTmpAlpha(_titleField, a);
            if (_bodyField != null) SetTmpAlpha(_bodyField, a);
        }

        private static void SetTmpAlpha(TMP_Text t, float a)
        {
            var c = t.color; c.a = a; t.color = c;
        }
    }
}
