// -----------------------------------------------------------------------------
//  BuildConfigurator.cs   (Editor)
//  DECRYPTED — A Walk Through the History of Secret Writing
//
//  One-click project configuration for a Meta Quest standalone build. Unity's
//  player/quality/build settings are scattered across several windows; this tool
//  applies the project's known-good baseline in a single menu action and prints a
//  short checklist for the handful of steps that must live in the XR and URP
//  asset windows (which are package-owned and best assigned by hand).
//
//  Menu:  DECRYPTED ▸ Build ▸ Configure for Quest
//         DECRYPTED ▸ Build ▸ Print Setup Checklist
//
//  Everything here uses stable BuildTargetGroup overloads so it compiles across
//  2022.3.x without depending on a specific XR/URP editor assembly being present.
//  The deeper XR plug-in + stereo-rendering + URP-asset assignment is documented
//  in 01_Unity_Setup_Guide.md and surfaced by the checklist below.
// -----------------------------------------------------------------------------

using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Decrypted.EditorTools
{
    public static class BuildConfigurator
    {
        private const string Company = "Decrypted Labs";
        private const string Product = "DECRYPTED";
        private const string Bundle  = "com.decryptedlabs.decrypted";

        [MenuItem("DECRYPTED/Build/Configure for Quest", priority = 0)]
        public static void ConfigureForQuest()
        {
            // --- Identity -------------------------------------------------------
            PlayerSettings.companyName = Company;
            PlayerSettings.productName = Product;
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, Bundle);

            // --- Rendering / colour --------------------------------------------
            // Linear colour space is required for correct PBR + baked lighting.
            PlayerSettings.colorSpace = ColorSpace.Linear;

            // Quest 1 ships a GLES3 driver; list it first. Vulkan can be enabled on
            // newer headsets — see the checklist. Disable auto-API so our order wins.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,
                new[] { GraphicsDeviceType.OpenGLES3 });

            // --- Scripting / arch ----------------------------------------------
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android,
                ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Android,
                ApiCompatibilityLevel.NET_Standard_2_0);

            // --- Android SDK levels --------------------------------------------
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            // --- Mobile GPU niceties -------------------------------------------
            PlayerSettings.gpuSkinning = true;                 // skin on GPU
            PlayerSettings.MTRendering = true;                 // multithreaded render
            PlayerSettings.Android.startInFullscreen = true;
            PlayerSettings.use32BitDisplayBuffer = false;      // 16-bit depth is plenty
            PlayerSettings.Android.blitType = AndroidBlitType.Never; // direct-to-surface

            // --- Texture compression for Android --------------------------------
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;

            // --- Switch platform if needed -------------------------------------
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.Log("[BuildConfigurator] Switching active build target to Android…");
                EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.Android, BuildTarget.Android);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[BuildConfigurator] Quest player settings applied. " +
                      "Run 'Print Setup Checklist' for the remaining XR/URP steps.");
            PrintChecklist();
        }

        [MenuItem("DECRYPTED/Build/Print Setup Checklist", priority = 1)]
        public static void PrintChecklist()
        {
            Debug.Log(
@"DECRYPTED — remaining manual setup (package-owned windows)
──────────────────────────────────────────────────────────
1. XR Plug-in Management (Project Settings ▸ XR Plug-in Management):
     • Install 'Oculus XR Plugin' + 'XR Plugin Management'.
     • Android tab → enable 'Oculus'.
     • Oculus settings → Stereo Rendering Mode = 'Multiview' (single-pass; ~2x cheaper).
     • Add the OCULUS_XR_PRESENT scripting define (Player ▸ Scripting Define Symbols)
       so PerformanceManager's fixed-foveation / dynamic calls compile in.
2. Universal Render Pipeline:
     • Install 'Universal RP'. Assign the project URP Asset in
       Project Settings ▸ Graphics ▸ Scriptable Render Pipeline Settings AND in
       Project Settings ▸ Quality (Android tier).
     • URP Asset: MSAA = 4x, HDR = Off, Shadow Distance ≈ 18m, one cascade.
3. Quality:
     • Single Android quality level, VSync = Don't Sync (the headset compositor
       drives pacing), Pixel Light Count low, Soft Particles off.
4. Build Settings ▸ add the scene 'Decrypted_Main' to Scenes In Build.
5. Texture import: set source art to ASTC 6x6, generate mips, max size 1024.
See Documentation/01_Unity_Setup_Guide.md for the full walkthrough.");
        }
    }
}
