// -----------------------------------------------------------------------------
//  GameEvents.cs
//  DECRYPTED — A Walk Through the History of Secret Writing
//
//  Every concrete event that flows over the EventBus. Keeping them in one file
//  gives a single readable "vocabulary" of everything that can happen in the
//  museum, which is great for onboarding and for the demo director.
//
//  Convention: events are immutable readonly structs (no GC churn) and override
//  ToString() for clean logging.
// -----------------------------------------------------------------------------

using Decrypted.Core;

namespace Decrypted.Core
{
    // ---- Flow / state ------------------------------------------------------

    public readonly struct StateChangedEvent : IGameEvent
    {
        public readonly MuseumState Previous;
        public readonly MuseumState Current;
        public StateChangedEvent(MuseumState previous, MuseumState current)
        { Previous = previous; Current = current; }
        public override string ToString() => $"{Previous} -> {Current}";
    }

    /// <summary>Raised by the SplashScreenController PLAY button.</summary>
    public readonly struct ExperienceStartedEvent : IGameEvent
    {
        public override string ToString() => "PLAY pressed";
    }

    /// <summary>Raised when a room's activation/teleport finishes.</summary>
    public readonly struct RoomEnteredEvent : IGameEvent
    {
        public readonly MuseumState Room;
        public RoomEnteredEvent(MuseumState room) { Room = room; }
        public override string ToString() => $"entered {Room}";
    }

    /// <summary>Raised by InteractionManager when an exhibit's goal is met.</summary>
    public readonly struct ExhibitSolvedEvent : IGameEvent
    {
        public readonly MuseumState Room;
        public ExhibitSolvedEvent(MuseumState room) { Room = room; }
        public override string ToString() => $"solved {Room}";
    }

    // ---- Exhibit-specific telemetry (drives audio/visual feedback) ---------

    /// <summary>Caesar disk rotated to a new shift (0-25).</summary>
    public readonly struct CaesarShiftChangedEvent : IGameEvent
    {
        public readonly int Shift;
        public readonly string Decoded;
        public CaesarShiftChangedEvent(int shift, string decoded) { Shift = shift; Decoded = decoded; }
        public override string ToString() => $"shift={Shift} '{Decoded}'";
    }

    /// <summary>An Enigma rotor settled on a new index (0-25).</summary>
    public readonly struct EnigmaRotorChangedEvent : IGameEvent
    {
        public readonly int RotorIndex;   // 0,1,2
        public readonly int Value;        // 0-25
        public EnigmaRotorChangedEvent(int rotorIndex, int value) { RotorIndex = rotorIndex; Value = value; }
        public override string ToString() => $"rotor[{RotorIndex}]={Value}";
    }

    /// <summary>A key was struck on the Enigma keyboard; lamp letter lights up.</summary>
    public readonly struct EnigmaKeyPressedEvent : IGameEvent
    {
        public readonly char Input;
        public readonly char Output;
        public EnigmaKeyPressedEvent(char input, char output) { Input = input; Output = output; }
        public override string ToString() => $"{Input}->{Output}";
    }

    /// <summary>Vault keypad accepted/rejected a passphrase attempt.</summary>
    public readonly struct VaultAttemptEvent : IGameEvent
    {
        public readonly bool Correct;
        public readonly string Entered;
        public VaultAttemptEvent(bool correct, string entered) { Correct = correct; Entered = entered; }
        public override string ToString() => $"{(Correct ? "OK" : "DENIED")} '{Entered}'";
    }

    // ---- Generic feedback hooks -------------------------------------------

    /// <summary>Fire-and-forget request to play a one-shot SFX by library key.</summary>
    public readonly struct PlaySfxEvent : IGameEvent
    {
        public readonly string Key;
        public readonly UnityEngine.Vector3 Position;  // for spatial one-shots
        public readonly bool Spatial;
        public PlaySfxEvent(string key, UnityEngine.Vector3 position, bool spatial = true)
        { Key = key; Position = position; Spatial = spatial; }
        public override string ToString() => $"sfx '{Key}'";
    }

    /// <summary>Request the UIManager show a transient feedback toast / hint.</summary>
    public readonly struct ShowHintEvent : IGameEvent
    {
        public readonly string Message;
        public readonly float Duration;
        public ShowHintEvent(string message, float duration = 3f) { Message = message; Duration = duration; }
        public override string ToString() => $"hint '{Message}'";
    }
}
