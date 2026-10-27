using HarmonyLib;
using Peak.Network;
using Photon.Realtime;
using UnityEngine;

namespace PeakMorePlayers
{
    // The game reads its player cap from this getter when it builds the room and the lobby UI.
    [HarmonyPatch(typeof(NetworkingUtilities), nameof(NetworkingUtilities.MAX_PLAYERS), MethodType.Getter)]
    internal static class MaxPlayersGetterPatch
    {
        private static bool Prefix(ref int __result)
        {
            __result = Plugin.Instance.MaxPlayers.Value;
            return false;
        }
    }

    // Belt and braces: whatever the vanilla method builds, make sure the room cap is ours.
    [HarmonyPatch(typeof(NetworkingUtilities), nameof(NetworkingUtilities.HostRoomOptions))]
    internal static class HostRoomOptionsPatch
    {
        private static void Postfix(ref RoomOptions __result)
        {
            if (__result == null) return;
            __result.MaxPlayers = (byte)Mathf.Clamp(Plugin.Instance.MaxPlayers.Value, 1, 30);
        }
    }
}
