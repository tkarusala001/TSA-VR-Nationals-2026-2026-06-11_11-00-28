// -----------------------------------------------------------------------------
//  SaveSystem.cs
//  DECRYPTED — A Walk Through the History of Secret Writing
//
//  Minimal, dependency-free persistence over PlayerPrefs. The museum is a short
//  guided experience, so we only persist:
//    * The furthest MuseumState reached (for optional "resume").
//    * Which gated rooms have been solved.
//
//  Kept static + tiny on purpose. For a larger title this would be swapped for a
//  JSON-to-disk profile; the GameManager only depends on this surface so the
//  swap would be local.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

namespace Decrypted.Core
{
    public static class SaveSystem
    {
        private const string KEY_FURTHEST = "decrypted.furthest";
        private const string KEY_SOLVED   = "decrypted.solved"; // CSV of MuseumState names

        public static bool HasProgress => PlayerPrefs.HasKey(KEY_FURTHEST);

        public static void SaveFurthestState(MuseumState state)
        {
            // Only ever store the *furthest* — never regress on a backward set.
            var current = LoadFurthestState();
            if ((int)state > (int)current || !HasProgress)
            {
                PlayerPrefs.SetString(KEY_FURTHEST, state.ToString());
                PlayerPrefs.Save();
            }
        }

        public static MuseumState LoadFurthestState()
        {
            var s = PlayerPrefs.GetString(KEY_FURTHEST, MuseumState.Splash.ToString());
            return System.Enum.TryParse(s, out MuseumState result) ? result : MuseumState.Splash;
        }

        public static void SaveSolvedRoom(MuseumState room)
        {
            var set = LoadSolvedRooms();
            if (set.Add(room))
            {
                PlayerPrefs.SetString(KEY_SOLVED, string.Join(",", set));
                PlayerPrefs.Save();
            }
        }

        public static HashSet<MuseumState> LoadSolvedRooms()
        {
            var result = new HashSet<MuseumState>();
            var csv = PlayerPrefs.GetString(KEY_SOLVED, "");
            if (string.IsNullOrEmpty(csv)) return result;
            foreach (var token in csv.Split(','))
                if (System.Enum.TryParse(token, out MuseumState s)) result.Add(s);
            return result;
        }

        public static void ClearProgress()
        {
            PlayerPrefs.DeleteKey(KEY_FURTHEST);
            PlayerPrefs.DeleteKey(KEY_SOLVED);
            PlayerPrefs.Save();
        }
    }
}
