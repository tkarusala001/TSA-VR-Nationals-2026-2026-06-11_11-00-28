// -----------------------------------------------------------------------------
//  SceneBuilder.cs   (Editor)
//  DECRYPTED — A Walk Through the History of Secret Writing
//
//  A focused bootstrapper that stamps out the scene's SKELETON so a designer
//  starts from a wired, runnable structure rather than an empty scene. It builds:
//
//    • A Managers rig (GameManager, SceneController, Audio/Performance/Interaction/
//      UI managers, DemoDirector) — the event-driven backbone.
//    • A camera rig placeholder (XR Origin ▸ Camera Offset ▸ Main Camera) with the
//      ScreenFader attached, ready for the Oculus/XR rig to replace or parent.
//    • Six room roots (Splash → Reveal), each with a RoomActivator, a player
//      anchor and a floor, spaced apart so the hierarchy stays legible.
//    • Auto-wiring: SceneController's room list, fader and camera refs, plus
//      GameManager's SceneController reference, populated via SerializedObject.
//
//  It deliberately does NOT place exhibit props, art or lighting — those come
//  from the Blender pipeline and the art pass. This is the connective tissue,
//  reversible via Undo.
//
//  Menu:  DECRYPTED ▸ Build Scene Skeleton
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using Decrypted.Core;
using Decrypted.Managers;
using Decrypted.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Decrypted.EditorTools
{
    public static class SceneBuilder
    {
        // (state, display name, ambient key, world X offset for legibility)
        private static readonly (MuseumState state, string name, string ambient, float x)[] Rooms =
        {
            (MuseumState.Splash,        "Splash",          "amb_atrium",  0f),
            (MuseumState.Atrium,        "Atrium",          "amb_atrium",  30f),
            (MuseumState.AncientRoom,   "Ancient Cryptography", "amb_ancient", 60f),
            (MuseumState.WWIIRoom,      "The Enigma Era",  "amb_wwii",    90f),
            (MuseumState.VaultRoom,     "Modern Security", "amb_vault",  120f),
            (MuseumState.RevealChamber, "The Reveal",      "amb_atrium", 150f),
        };

        [MenuItem("DECRYPTED/Build Scene Skeleton", priority = 20)]
        public static void BuildSkeleton()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[SceneBuilder] Exit Play Mode before building the skeleton.");
                return;
            }

            var root = new GameObject("DECRYPTED");
            Undo.RegisterCreatedObjectUndo(root, "Build DECRYPTED Skeleton");

            // --- Camera rig -----------------------------------------------------
            var xrOrigin = NewChild(root.transform, "XR Origin");
            var camOffset = NewChild(xrOrigin.transform, "Camera Offset");
            camOffset.transform.localPosition = new Vector3(0f, 1.36144f, 0f); // seated/standing eye height
            var camGo = NewChild(camOffset.transform, "Main Camera");
            var cam = camGo.AddComponent<Camera>();
            camGo.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            var fader = camGo.AddComponent<ScreenFader>();

            // --- Managers rig ---------------------------------------------------
            var managers = NewChild(root.transform, "Managers");
            var gameManager = managers.AddComponent<GameManager>();
            var sceneController = managers.AddComponent<SceneController>();
            managers.AddComponent<AudioManager>();
            managers.AddComponent<PerformanceManager>();
            managers.AddComponent<InteractionManager>();
            managers.AddComponent<UIManager>();
            managers.AddComponent<DemoDirector>();

            // --- Rooms ----------------------------------------------------------
            var roomsParent = NewChild(root.transform, "Rooms");
            var descriptors = new List<(MuseumState state, string name, string ambient,
                                        GameObject roomRoot, Transform anchor)>();

            foreach (var r in Rooms)
            {
                var roomRoot = NewChild(roomsParent.transform, $"Room_{r.state}");
                roomRoot.transform.position = new Vector3(r.x, 0f, 0f);
                roomRoot.AddComponent<RoomActivator>();

                var anchor = NewChild(roomRoot.transform, "PlayerAnchor").transform;
                anchor.localPosition = Vector3.zero;

                // A simple floor so the room is visible/standable out of the box.
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                Undo.RegisterCreatedObjectUndo(floor, "Build DECRYPTED Skeleton");
                floor.name = "Floor";
                floor.transform.SetParent(roomRoot.transform, false);
                floor.transform.localScale = new Vector3(1.2f, 1f, 1.2f); // ~12m square

                // Only the first room starts active; the rest are toggled on entry.
                roomRoot.SetActive(r.state == MuseumState.Splash);

                descriptors.Add((r.state, r.name, r.ambient, roomRoot, anchor));
            }

            // --- Wire SceneController via SerializedObject -----------------------
            var so = new SerializedObject(sceneController);
            so.FindProperty("_xrOrigin").objectReferenceValue = xrOrigin.transform;
            so.FindProperty("_hmdCamera").objectReferenceValue = cam;
            so.FindProperty("_fader").objectReferenceValue = fader;

            var roomsProp = so.FindProperty("_rooms");
            roomsProp.ClearArray();
            for (int i = 0; i < descriptors.Count; i++)
            {
                roomsProp.InsertArrayElementAtIndex(i);
                var element = roomsProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("state").enumValueIndex = (int)descriptors[i].state;
                element.FindPropertyRelative("displayName").stringValue = descriptors[i].name;
                element.FindPropertyRelative("roomRoot").objectReferenceValue = descriptors[i].roomRoot;
                element.FindPropertyRelative("playerAnchor").objectReferenceValue = descriptors[i].anchor;
                element.FindPropertyRelative("ambientKey").stringValue = descriptors[i].ambient;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            // --- Wire GameManager.SceneController -------------------------------
            var gmSo = new SerializedObject(gameManager);
            var scProp = gmSo.FindProperty("_sceneController");
            if (scProp != null) scProp.objectReferenceValue = sceneController;
            gmSo.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log("[SceneBuilder] Skeleton built and wired. Add exhibit prefabs under " +
                      "each Room_* root, then bake lighting. See Documentation/02_Environment_Design.md.");
        }

        private static GameObject NewChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Build DECRYPTED Skeleton");
            go.transform.SetParent(parent, false);
            return go;
        }
    }
}
