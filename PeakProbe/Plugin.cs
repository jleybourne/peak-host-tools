using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PeakProbe
{
    // Read-only diagnostics. Logs every RPC the game sends, and writes a dump of game internals on F6.
    [BepInPlugin("au.leybourne.peakprobe", "PEAK Probe @jleybourne", "0.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static string OutDir;
        private static string _rpcLog;

        private void Awake()
        {
            Log = Logger;
            OutDir = Path.Combine(Paths.BepInExRootPath, "PeakProbe");
            Directory.CreateDirectory(OutDir);
            _rpcLog = Path.Combine(OutDir, "rpc.log");
            File.WriteAllText(_rpcLog, $"# PEAK Probe RPC log, started {DateTime.Now}\n");

            var h = new Harmony("au.leybourne.peakprobe");
            h.PatchAll();
            TryPatchIncoming(h);
            Log.LogInfo("PEAK Probe loaded. F6 writes a dump, rpc.log records RPCs.");
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb[Key.F6].wasPressedThisFrame) Dump.Write();
        }

        private static readonly string[] Noise = { "SyncAfflictionsRPC", "SyncStatusesRPC", "SetKinematicRPC", "RPCA_SyncTime", "RPC_SyncTime", "RPCA_SyncFog", "SyncThornsRPC", "RPCA_SyncTornado", "RPCA_SyncAnim" };

        internal static void Rpc(string line)
        {
            foreach (var n in Noise) if (line.Contains(n)) return;
            try { File.AppendAllText(_rpcLog, $"[{Time.realtimeSinceStartup:0.00}] {line}\n"); } catch { }
        }

        private static void TryPatchIncoming(Harmony h)
        {
            var m = AccessTools.Method(typeof(PhotonNetwork), "ExecuteRpc");
            if (m == null) { Log.LogWarning("PhotonNetwork.ExecuteRpc not found, incoming RPCs not logged"); return; }
            h.Patch(m, prefix: new HarmonyMethod(typeof(Plugin), nameof(IncomingPrefix)));
        }

        private static void IncomingPrefix(object[] __args)
        {
            try
            {
                var sb = new StringBuilder("IN ");
                foreach (var a in __args)
                {
                    if (a is ExitGames.Client.Photon.Hashtable ht)
                        foreach (System.Collections.DictionaryEntry e in ht) sb.Append($"[{e.Key}]={Dump.Fmt(e.Value)} ");
                    else if (a is Photon.Realtime.Player p) sb.Append($"from {p.NickName}");
                }
                Rpc(sb.ToString());
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(PhotonView), nameof(PhotonView.RPC), typeof(string), typeof(RpcTarget), typeof(object[]))]
    internal static class RpcTargetPatch
    {
        private static void Prefix(PhotonView __instance, string methodName, RpcTarget target, object[] parameters) =>
            Plugin.Rpc($"OUT {methodName} -> {target} on {__instance.gameObject.name} (view {__instance.ViewID}) args: {Dump.Args(parameters)}");
    }

    [HarmonyPatch(typeof(PhotonView), nameof(PhotonView.RPC), typeof(string), typeof(Photon.Realtime.Player), typeof(object[]))]
    internal static class RpcPlayerPatch
    {
        private static void Prefix(PhotonView __instance, string methodName, Photon.Realtime.Player targetPlayer, object[] parameters) =>
            Plugin.Rpc($"OUT {methodName} -> {targetPlayer?.NickName} on {__instance.gameObject.name} (view {__instance.ViewID}) args: {Dump.Args(parameters)}");
    }

    internal static class Dump
    {
        internal static string Fmt(object o) => o == null ? "null" : o is Array a ? $"{o.GetType().Name}[{a.Length}]" : o.ToString();
        internal static string Args(object[] p) => p == null ? "" : string.Join(", ", p.Select(x => $"{x?.GetType().Name}:{Fmt(x)}"));

        internal static void Write()
        {
            var path = Path.Combine(Plugin.OutDir, $"dump-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            var sb = new StringBuilder();
            Section(sb, "ROOM", () =>
            {
                sb.AppendLine($"InRoom={PhotonNetwork.InRoom} Master={PhotonNetwork.IsMasterClient} Players={PhotonNetwork.PlayerList.Length}");
                if (PhotonNetwork.InRoom) sb.AppendLine($"MaxPlayers={PhotonNetwork.CurrentRoom.MaxPlayers} Open={PhotonNetwork.CurrentRoom.IsOpen} Visible={PhotonNetwork.CurrentRoom.IsVisible}");
            });
            Section(sb, "PUN RPC METHODS (Assembly-CSharp)", () =>
            {
                foreach (var t in typeof(Character).Assembly.GetTypes().OrderBy(t => t.FullName))
                    foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        if (m.GetCustomAttributes(typeof(PunRPC), false).Length > 0)
                            sb.AppendLine($"{t.FullName}.{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");
            });
            Section(sb, "ITEMS (id, name, prefab loads from Resources 0_Items/<name>)", () =>
            {
                var db = Traverse.Create(typeof(ItemDatabase)).Property("Instance").GetValue();
                var lookup = Traverse.Create(db).Field("itemLookup").GetValue<Dictionary<ushort, Item>>();
                foreach (var kv in lookup.OrderBy(k => k.Value.name))
                    sb.AppendLine($"{kv.Key}\t{kv.Value.name}\t0_Items ok={Resources.Load<GameObject>("0_Items/" + kv.Value.name) != null}");
            });
            Section(sb, "LOCAL CHARACTER", () =>
            {
                var me = Character.localCharacter;
                if (me == null) { sb.AppendLine("none"); return; }
                sb.AppendLine($"object={me.name} view={me.photonView.ViewID} pos={me.Center}");
                Fields(sb, "data", me.data);
                Fields(sb, "movement", me.refs.movement);
                sb.AppendLine("statuses:");
                foreach (CharacterAfflictions.STATUSTYPE s in Enum.GetValues(typeof(CharacterAfflictions.STATUSTYPE)))
                    sb.AppendLine($"  {s} = {me.refs.afflictions.GetCurrentStatus(s):0.000}");
                sb.AppendLine("components on character:");
                foreach (var c in me.GetComponentsInChildren<Component>(true).Select(c => c?.GetType().Name).Distinct().OrderBy(n => n)) sb.AppendLine("  " + c);
            });
            Section(sb, "ALL CHARACTERS", () =>
            {
                foreach (var c in Character.AllCharacters)
                    sb.AppendLine($"{c.name} bot={c.isBot} mine={c.photonView.IsMine} owner={c.photonView.Owner?.NickName} dead={c.data.dead}");
            });
            Section(sb, "CAMPFIRES", () =>
            {
                foreach (var c in UnityEngine.Object.FindObjectsByType<Campfire>(FindObjectsSortMode.None))
                    sb.AppendLine($"{c.name} segment={c.advanceToSegment} pos={c.transform.position} nameOverride={c.nameOverride}");
            });
            Section(sb, "SCENE ROOTS", () =>
            {
                foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                    sb.AppendLine($"{go.name} ({go.transform.childCount} children)");
            });
            File.WriteAllText(path, sb.ToString());
            Plugin.Log.LogInfo("Probe dump written: " + path);
        }

        private static void Fields(StringBuilder sb, string label, object o)
        {
            sb.AppendLine($"{label} ({o.GetType().Name}) fields:");
            foreach (var f in o.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (f.FieldType.IsPrimitive || f.FieldType == typeof(Vector3) || f.FieldType.IsEnum || f.FieldType == typeof(string))
                    try { sb.AppendLine($"  {(f.IsPublic ? "public " : "private")} {f.FieldType.Name} {f.Name} = {f.GetValue(o)}"); } catch { }
            }
        }

        private static void Section(StringBuilder sb, string title, Action a)
        {
            sb.AppendLine($"\n===== {title} =====");
            try { a(); } catch (Exception e) { sb.AppendLine($"FAILED: {e.GetType().Name}: {e.Message}"); }
        }
    }
}
