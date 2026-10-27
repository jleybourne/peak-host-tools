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

namespace PeakHostMenu
{
    // The game's own reduce-uses RPC and the can-use checks are what gate item consumption.
    [HarmonyPatch(typeof(Action_ReduceUses), "ReduceUsesRPC")]
    internal static class ReduceUsesPatch
    {
        private static bool Prefix() => !Plugin.Instance.UnlimitedUses.Value;
    }

    [HarmonyPatch(typeof(Item), "CanUsePrimary")]
    internal static class CanUsePrimaryPatch
    {
        private static bool Prefix(ref bool __result)
        {
            if (!Plugin.Instance.UnlimitedUses.Value) return true;
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(Item), "CanUseSecondary")]
    internal static class CanUseSecondaryPatch
    {
        private static bool Prefix(ref bool __result)
        {
            if (!Plugin.Instance.UnlimitedUses.Value) return true;
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(CharacterAfflictions), "UpdateWeight")]
    internal static class NoWeightPatch
    {
        private static void Postfix(CharacterAfflictions __instance)
        {
            if (!Plugin.Instance.NoWeight.Value || __instance.character != Character.localCharacter) return;
            Features.Safe("no weight", () => __instance.SetStatus(CharacterAfflictions.STATUSTYPE.Weight, 0f));
        }
    }

    [HarmonyPatch(typeof(CharacterData), "GetTargetRagdollControll")]
    internal static class NoRagdollPatch
    {
        private static bool Prefix(CharacterData __instance, ref float __result)
        {
            if (!Plugin.Instance.NoRagdoll.Value || !__instance.photonView.IsMine || __instance.dead) return true;
            __result = 1f;
            return false;
        }
    }

    [HarmonyPatch(typeof(PointPinger), "ReceivePoint_Rpc")]
    internal static class TeleportToPingPatch
    {
        private static void Postfix(PointPinger __instance, Vector3 point)
        {
            if (!Plugin.Instance.TeleportToPing.Value) return;
            var owner = __instance.GetComponentInParent<Character>();
            if (owner == null || owner != Character.localCharacter) return;
            Features.Warp(owner, point + Vector3.up);
        }
    }
}
