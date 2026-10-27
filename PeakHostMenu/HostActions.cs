using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PeakHostMenu
{
    // Player and world actions. Every call here is a game RPC or a Unity call that the RPC dump
    // from PEAK Probe confirmed exists; failures are logged by Features.Safe and never crash.
    internal static class HostActions
    {
        private static void Rpc(PhotonView view, string name, RpcTarget target, params object[] args) =>
            Features.Safe(name, () => view.RPC(name, target, args));

        // ---- Look point ----------------------------------------------------------------------

        internal static bool LookPoint(out Vector3 point)
        {
            point = default;
            var cam = Camera.main;
            if (cam == null) return false;
            if (!Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, 600f)) return false;
            point = hit.point;
            return true;
        }

        // ---- Per-player effects --------------------------------------------------------------

        internal static void Freeze(Character c, bool frozen) => Rpc(c.photonView, "RPC_SetBlockedState", RpcTarget.All, frozen);
        internal static void Fall(Character c) => Rpc(c.photonView, "RPCA_Fall", RpcTarget.All, 7f, 0f);
        internal static void Jump(Character c) => Rpc(c.photonView, "JumpRpc", RpcTarget.All, false);
        internal static void PassOut(Character c) => Rpc(c.photonView, "RPCA_PassOut", RpcTarget.All);
        internal static void Wake(Character c) => Rpc(c.photonView, "RPCA_UnPassOut", RpcTarget.All);
        internal static void StopClimbing(Character c) => Rpc(c.photonView, "StopClimbingRpc", RpcTarget.All, 0f);

        internal static void Fling(Character c, float strength)
        {
            var force = Vector3.up * strength;
            Rpc(c.photonView, "RPCA_AddForceToBodyPart", RpcTarget.All, BodypartType.Hip, force, force);
        }

        internal static void Eruption(Character c)
        {
            var spawner = Object.FindFirstObjectByType<EruptionSpawner>();
            if (spawner == null) { Plugin.Log.LogWarning("No EruptionSpawner in this scene"); return; }
            Rpc(spawner.GetComponent<PhotonView>(), "RPCA_SpawnEruption", RpcTarget.All, c.Center);
        }

        // Prop drops: spawn a real item above or below a player and let the game's physics do the rest.
        internal static void DropProp(Character c, string itemName, float height)
        {
            Features.Safe("drop " + itemName, () =>
                PhotonNetwork.Instantiate("0_Items/" + itemName, c.Center + Vector3.up * height, Quaternion.identity));
        }

        internal static void ToCrosshair(Character c)
        {
            if (LookPoint(out var p)) Features.Warp(c, p + Vector3.up * 1.5f);
        }

        internal static void Swap(Character a, Character b)
        {
            if (a == null || b == null || a == b) return;
            Vector3 pa = a.Center, pb = b.Center;
            Features.Warp(a, pb + Vector3.up);
            Features.Warp(b, pa + Vector3.up);
        }

        internal static void ToRandomPlayer()
        {
            var others = Features.Players.Where(p => p != Features.Me).ToList();
            if (others.Count > 0) Features.GoTo(others[Random.Range(0, others.Count)]);
        }

        // ---- Environment ---------------------------------------------------------------------

        internal static void SetGravity(float g) => Physics.gravity = new Vector3(0f, g, 0f);
        internal static void SetGameSpeed(float s) => Time.timeScale = Mathf.Max(0f, s);

        internal static void SetFogHeight(float h)
        {
            var fog = Object.FindFirstObjectByType<Fog>();
            if (fog != null) Rpc(fog.GetComponent<PhotonView>(), "RPCA_SyncFog", RpcTarget.All, h);
        }

        internal static void ResumeFog()
        {
            var fog = Object.FindFirstObjectByType<Fog>();
            if (fog != null) Rpc(fog.GetComponent<PhotonView>(), "RPCA_Resume", RpcTarget.All);
        }

        internal static float CurrentFogHeight()
        {
            var fog = Object.FindFirstObjectByType<Fog>();
            return fog != null ? fog.fogHeight : 0f;
        }

        internal static void SetWind(bool on)
        {
            foreach (var z in Object.FindObjectsByType<WindChillZone>(FindObjectsSortMode.None))
                Rpc(z.GetComponent<PhotonView>(), "RPCA_ToggleWind", RpcTarget.All, on, Vector3.right, 120f);
        }

        internal static bool TimeOfDay(out float time, out float max)
        {
            time = 0f; max = 24f;
            var d = DayNightManager.instance;
            if (d == null) return false;
            time = d.timeOfDay;
            max = d.dayEnd > 1.5f ? 24f : 1f;
            return true;
        }

        internal static void SetTimeOfDay(float time)
        {
            var d = DayNightManager.instance;
            if (d != null) Rpc(d.GetComponent<PhotonView>(), "RPCA_SyncTime", RpcTarget.All, d.dayCount, time);
        }

        internal static void LightAllCampfires()
        {
            foreach (var cf in Object.FindObjectsByType<Campfire>(FindObjectsSortMode.None))
            {
                var v = cf.GetComponent<PhotonView>();
                if (v != null) Rpc(v, "Light_Rpc", RpcTarget.All, true, cf.burnsFor);
            }
        }

        internal static void ExtinguishAllCampfires()
        {
            foreach (var cf in Object.FindObjectsByType<Campfire>(FindObjectsSortMode.None))
            {
                var v = cf.GetComponent<PhotonView>();
                if (v != null) Rpc(v, "Extinguish_Rpc", RpcTarget.All);
            }
        }

        // ---- Luggage -------------------------------------------------------------------------

        internal static IEnumerable<Luggage> AllLuggage() => Luggage.ALL_LUGGAGE.Where(l => l != null);

        internal static void OpenLuggage(Luggage l) => Rpc(l.photonView, "OpenLuggageRPC", RpcTarget.All, true);

        internal static void OpenNearbyLuggage(float radius)
        {
            if (Features.Me == null) return;
            foreach (var l in AllLuggage().ToList())
                if (Vector3.Distance(l.transform.position, Features.Me.Center) <= radius) OpenLuggage(l);
        }

        internal static void WarpToNearestLuggage()
        {
            if (Features.Me == null) return;
            var best = AllLuggage().OrderBy(l => Vector3.Distance(l.transform.position, Features.Me.Center)).FirstOrDefault();
            if (best != null) Features.Warp(Features.Me, best.transform.position + Vector3.up * 1.5f);
        }

        // ---- World items ---------------------------------------------------------------------

        // Items nobody is holding.
        private static List<Item> LooseItems() =>
            Object.FindObjectsByType<Item>(FindObjectsSortMode.None).Where(i => i != null && i.trueHolderCharacter == null).ToList();

        internal static void FreezeItems(bool frozen)
        {
            foreach (var i in LooseItems())
                Rpc(i.photonView, "SetKinematicRPC", RpcTarget.All, frozen, i.transform.position, i.transform.rotation);
        }

        internal static void SpinItems()
        {
            foreach (var i in LooseItems())
                Rpc(i.photonView, "ApplyForceRPC", RpcTarget.All, Vector3.up * 3f, Random.insideUnitSphere * 8f);
        }

        internal static void CookItems() { foreach (var i in LooseItems()) Rpc(i.photonView, "SetCookedAmountRPC", RpcTarget.All, 100); }
        internal static void DenyPickups() { foreach (var i in LooseItems()) Rpc(i.photonView, "DenyPickupRPC", RpcTarget.All); }

        internal static void ClearItems()
        {
            if (!Features.IsHost) return;
            foreach (var i in LooseItems()) Features.Safe("clear item", () => PhotonNetwork.Destroy(i.gameObject));
        }

        // ---- Game state ----------------------------------------------------------------------

        internal static void ForceWin()
        {
            if (Features.Me != null) Rpc(Features.Me.photonView, "RPCEndGame_ForceWin", RpcTarget.All);
        }

        internal static void SetPeakTimer(int seconds)
        {
            var seq = Object.FindFirstObjectByType<PeakSequence>();
            if (seq != null) Rpc(seq.GetComponent<PhotonView>(), "RPCUpdateTimer", RpcTarget.All, seconds, false);
        }

        // The helicopter is triggered by a flare, so use one that exists or spawn one.
        internal static void CallHelicopter()
        {
            var flare = Object.FindFirstObjectByType<Flare>();
            if (flare != null) { Rpc(flare.GetComponent<PhotonView>(), "TriggerHelicopter", RpcTarget.All); return; }
            if (Features.Me == null) return;
            Features.Instance.StartCoroutine(SpawnFlareThenCall());
        }

        private static System.Collections.IEnumerator SpawnFlareThenCall()
        {
            Features.Safe("flare", () => PhotonNetwork.Instantiate("0_Items/Flare", Features.Me.Center + Vector3.up, Quaternion.identity));
            yield return new WaitForSeconds(1f);
            var flare = Object.FindFirstObjectByType<Flare>();
            if (flare != null) Rpc(flare.GetComponent<PhotonView>(), "TriggerHelicopter", RpcTarget.All);
        }

        // ---- Item spawning at the look point ------------------------------------------------

        internal static void SpawnAtLookPoint(Item item, int count)
        {
            if (item == null || !LookPoint(out var p)) return;
            Features.Safe("spawn at look point", () =>
            {
                for (int i = 0; i < count; i++)
                    PhotonNetwork.Instantiate("0_Items/" + item.name, p + Vector3.up * (0.6f + 0.35f * i), Quaternion.identity);
            });
        }

        // ---- Your own inventory --------------------------------------------------------------

        internal static string SlotName(int slot)
        {
            try
            {
                var s = Player.localPlayer.itemSlots[slot];
                return s.prefab != null ? s.prefab.name : "empty";
            }
            catch { return "empty"; }
        }

        internal static void RemoveSlot(int slot) =>
            Rpc(Player.localPlayer.photonView, "RPCRemoveItemFromSlot", RpcTarget.All, (byte)slot);

        // Refill whatever use counters the item in a slot keeps.
        internal static void RechargeSlot(int slot)
        {
            Features.Safe("recharge", () =>
            {
                var s = Player.localPlayer.itemSlots[slot];
                var data = s.data;
                if (data == null || s.prefab == null) return;
                if (data.TryGetDataEntry(DataEntryKey.UseRemainingPercentage, out FloatItemData pct)) pct.Value = 1f;
                if (data.TryGetDataEntry(DataEntryKey.ItemUses, out OptionableIntItemData uses)) { uses.HasData = true; uses.Value = s.prefab.totalUses; }
                if (data.TryGetDataEntry(DataEntryKey.Fuel, out FloatItemData fuel)) fuel.Value = 1f;
            });
        }
    }
}
