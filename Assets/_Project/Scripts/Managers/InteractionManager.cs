// -----------------------------------------------------------------------------
//  InteractionManager.cs
//  DECRYPTED — A Walk Through the History of Secret Writing
//
//  Coordinates *what the player is allowed to touch* and *whether they seem
//  stuck*. It does NOT implement individual interactions — those live on the
//  exhibit components (XRGrabTwistDisk, EnigmaRotor, VaultKeypad, …). Instead it:
//    * Enables the active room's interactable group and disables all others, so
//      a stray ray can never grab a future exhibit through a wall.
//    * Runs a gentle idle timer per gated room: if the player hasn't made
//      progress for a while, it surfaces an escalating, non-verbal hint via the
//      UIManager (great for first-time users and unattended demo kiosks).
//
//  "Progress" is reported by exhibits through the EventBus (rotor moved, key
//  pressed, shift changed). Any such event resets the idle timer.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using Decrypted.Core;
using Decrypted.Util;
using UnityEngine;

namespace Decrypted.Managers
{
    [DefaultExecutionOrder(-80)]
    public class InteractionManager : Singleton<InteractionManager>
    {
        [System.Serializable]
        public class RoomInteractables
        {
            public MuseumState room;
            [Tooltip("Parent containing all XR interactables for this room.")]
            public GameObject interactableGroup;

            [Header("Idle Hints (shown in order, escalating)")]
            [Tooltip("First nudge appears after this many idle seconds.")]
            public float firstHintAfter = 18f;
            [Tooltip("Subsequent nudges cadence.")]
            public float hintInterval = 14f;
            [TextArea] public string[] hints;
        }

        [SerializeField] private List<RoomInteractables> _rooms = new List<RoomInteractables>();

        private readonly Dictionary<MuseumState, RoomInteractables> _byState =
            new Dictionary<MuseumState, RoomInteractables>();

        private MuseumState _activeRoom = MuseumState.Boot;
        private float _idleTimer;
        private int _hintIndex;
        private bool _activeRoomSolved;

        protected override void OnSingletonAwake()
        {
            foreach (var r in _rooms) if (r != null) _byState[r.room] = r;
            // Everything off until a room becomes active.
            foreach (var r in _rooms)
                if (r?.interactableGroup != null) r.interactableGroup.SetActive(false);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<RoomEnteredEvent>(OnRoomEntered);
            EventBus.Subscribe<ExhibitSolvedEvent>(OnExhibitSolved);
            // Any of these count as "the player is doing something".
            EventBus.Subscribe<CaesarShiftChangedEvent>(_ => ResetIdle());
            EventBus.Subscribe<EnigmaRotorChangedEvent>(_ => ResetIdle());
            EventBus.Subscribe<EnigmaKeyPressedEvent>(_ => ResetIdle());
            EventBus.Subscribe<VaultAttemptEvent>(_ => ResetIdle());
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<RoomEnteredEvent>(OnRoomEntered);
            EventBus.Unsubscribe<ExhibitSolvedEvent>(OnExhibitSolved);
        }

        private void OnRoomEntered(RoomEnteredEvent e)
        {
            _activeRoom = e.Room;
            _activeRoomSolved = GameManager.Exists && GameManager.Instance.IsSolved(e.Room);
            ResetIdle();

            foreach (var kvp in _byState)
            {
                bool on = kvp.Key == e.Room;
                if (kvp.Value.interactableGroup != null)
                    kvp.Value.interactableGroup.SetActive(on);
            }
        }

        private void OnExhibitSolved(ExhibitSolvedEvent e)
        {
            if (e.Room == _activeRoom) _activeRoomSolved = true;
        }

        private void ResetIdle()
        {
            _idleTimer = 0f;
            _hintIndex = 0;
        }

        private void Update()
        {
            if (_activeRoomSolved) return;
            if (!_byState.TryGetValue(_activeRoom, out var room)) return;
            if (room.hints == null || room.hints.Length == 0) return;

            _idleTimer += Time.deltaTime;

            float threshold = room.firstHintAfter + _hintIndex * room.hintInterval;
            if (_idleTimer >= threshold && _hintIndex < room.hints.Length)
            {
                EventBus.Publish(new ShowHintEvent(room.hints[_hintIndex], 5f));
                _hintIndex++;
            }
        }
    }
}
