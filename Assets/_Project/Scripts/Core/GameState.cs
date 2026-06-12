// -----------------------------------------------------------------------------
//  GameState.cs
//  DECRYPTED — A Walk Through the History of Secret Writing
//
//  Defines the linear museum's high-level states and a serializable descriptor
//  that designers fill in per room from the Inspector. The GameManager owns the
//  active state; everything else reacts to state-change events on the EventBus.
// -----------------------------------------------------------------------------

using System;
using UnityEngine;

namespace Decrypted.Core
{
    /// <summary>
    /// The museum is a strictly linear progression. Each value maps to one
    /// physical zone. Transitions only ever move forward (or back to Splash on a
    /// full reset), which keeps the state machine trivially verifiable.
    /// </summary>
    public enum MuseumState
    {
        Boot,            // one frame: managers initialise
        Splash,          // glowing PLAY button + non-verbal tutorial cards
        Atrium,          // orientation hub, sets tone, routes to Ancient room
        AncientRoom,     // Exhibit 1 — Caesar cipher disk
        WWIIRoom,        // Exhibit 2 — Enigma-inspired machine
        VaultRoom,       // Exhibit 3 — modern digital security vault
        RevealChamber,   // final synthesis + conclusion
        Complete         // walkthrough finished (used by demo/auto-play)
    }

    /// <summary>
    /// Inspector-authored description of a single room. Lives on the
    /// SceneController so designers can wire rooms without touching code.
    /// </summary>
    [Serializable]
    public class RoomDescriptor
    {
        [Tooltip("State this room corresponds to.")]
        public MuseumState state;

        [Tooltip("Human-readable name shown on the wayfinding plaque.")]
        public string displayName = "Room";

        [Tooltip("Root GameObject for every static + dynamic object in this room. " +
                 "Toggled on/off for room-based culling.")]
        public GameObject roomRoot;

        [Tooltip("Where the player rig is placed when this room becomes active.")]
        public Transform playerAnchor;

        [Tooltip("Reflection probe that should be enabled while in this room (optional).")]
        public ReflectionProbe reflectionProbe;

        [Tooltip("AudioManager key for this room's ambient loop (see AudioLibrary).")]
        public string ambientKey = "amb_atrium";

        [Tooltip("Mixer snapshot name to transition to on entry (optional).")]
        public string mixerSnapshot = "";

        [Tooltip("Seconds to blend the mixer snapshot on entry.")]
        public float snapshotBlend = 1.5f;

        [HideInInspector] public bool hasBeenVisited;
    }
}
