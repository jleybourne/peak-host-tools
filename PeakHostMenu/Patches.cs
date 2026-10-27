using HarmonyLib;
using UnityEngine;

namespace PeakHostMenu
{
    internal static class FallPatchHelper
    {
        // Only skip fall damage for the character this player controls.
        internal static bool ShouldSkip(Component c)
        {
            if (!Plugin.Instance.NoFallDamage.Value && !Plugin.Instance.Fly.Value) return false;
            var ch = c.GetComponentInParent<Character>();
            return ch != null && ch == Character.localCharacter;
        }
    }

    [HarmonyPatch(typeof(CharacterMovement), "CheckFallDamage")]
    internal static class MovementFallPatch
    {
        private static bool Prefix(CharacterMovement __instance) => !FallPatchHelper.ShouldSkip(__instance);
    }

    [HarmonyPatch(typeof(CharacterClimbing), "CheckFallDamage")]
    internal static class ClimbingFallPatch
    {
        private static bool Prefix(CharacterClimbing __instance) => !FallPatchHelper.ShouldSkip(__instance);
    }
}
