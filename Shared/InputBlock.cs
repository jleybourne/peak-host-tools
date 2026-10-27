using HarmonyLib;
using UnityEngine;

namespace PeakShared
{
    // While one of our windows is open the game must not re-lock the cursor or move the character.
    internal static class InputBlock
    {
        // Set by the plugin that owns the open window.
        internal static bool Open;
    }

    // The game's cursor handler runs every frame and re-locks the mouse during a run.
    [HarmonyPatch(typeof(CursorHandler), "Update")]
    internal static class CursorHandlerPatch
    {
        private static bool Prefix()
        {
            if (!InputBlock.Open) return true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return false;
        }
    }

    // Player input is sampled once per frame. Zero it while the window is open so clicks don't swing the camera.
    [HarmonyPatch(typeof(CharacterInput), "Sample")]
    internal static class CharacterInputPatch
    {
        private static bool Prefix(CharacterInput __instance)
        {
            if (!InputBlock.Open) return true;
            Traverse.Create(__instance).Method("ResetInput").GetValue();
            return false;
        }
    }

    // The game's own flag for "a window is blocking player input".
    [HarmonyPatch(typeof(GUIManager), "windowBlockingInput", MethodType.Getter)]
    internal static class WindowBlockingInputPatch
    {
        private static bool Prefix(ref bool __result)
        {
            if (!InputBlock.Open) return true;
            __result = true;
            return false;
        }
    }
}
