using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PeakHostMenu
{
    // Co-op helpers, HUD, profiles and the extra player and world effects. Calls are the game's own RPCs
    // (confirmed present by PEAK Probe) wrapped in Features.Safe so a missing one only logs a warning.
    internal class Extras : MonoBehaviour
    {
        internal static Extras Instance;

        private readonly Dictionary<Character, float> _deadSince = new Dictionary<Character, float>();
        private float _nextHeal;

        private void Awake() => Instance = this;

        private void Update()
        {
            var cfg = Plugin.Instance;
            if (!Features.IsHost) { _deadSince.Clear(); return; }

            if (cfg.AutoRevive.Value) AutoRevive(cfg.AutoReviveDelay.Value);
            else _deadSince.Clear();

            if (cfg.AutoHeal.Value && Time.time >= _nextHeal)
            {
                _nextHeal = Time.time + cfg.AutoHealInterval.Value;
                Features.ForAll(Features.Heal, true);
            }
        }

        // Revive anyone who has been dead longer than the delay, next to a living teammate.
        private void AutoRevive(float delay)
        {
            foreach (var c in Features.Players.ToList())
            {
                if (!c.data.dead) { _deadSince.Remove(c); continue; }
                if (!_deadSince.ContainsKey(c)) { _deadSince[c] = Time.time; continue; }
                if (Time.time - _deadSince[c] < delay) continue;
                var alive = Features.Players.FirstOrDefault(p => !p.data.dead && p != c);
                if (alive == null) continue;
                _deadSince.Remove(c);
                Features.Revive(c, alive.Center);
            }
        }

        // ---- Co-op ---------------------------------------------------------------------------

        internal static void MoraleBoost(float amount)
        {
            int count = Features.Players.Count();
            Features.ForAll(c => Features.Safe("morale boost", () => c.photonView.RPC("MoraleBoost", RpcTarget.All, amount, count)), true);
        }

        internal static void Carry(Character carrier, Character target)
        {
            if (carrier == null || target == null || carrier == target) return;
            Features.Safe("carry", () => carrier.photonView.RPC("RPCA_StartCarry", RpcTarget.All, target.photonView));
        }

        internal static void Drop(Character carrier, Character target)
        {
            if (carrier == null || target == null) return;
            Features.Safe("drop carried", () => carrier.photonView.RPC("RPCA_Drop", RpcTarget.All, target.photonView));
        }

        // ---- Player effects ------------------------------------------------------------------

        internal static void Skeleton(Character c, bool on) =>
            Features.Safe("skeleton", () => c.photonView.RPC("RPC_SyncSkeleton", RpcTarget.All, on));

        internal static void Zombify(Character c) =>
            Features.Safe("zombify", () => c.photonView.RPC("RPCA_Zombify", RpcTarget.All));

        internal static void Rocket(Character c) =>
            Features.Safe("rocket", () => c.photonView.RPC("RocketRPC", RpcTarget.All, 0));

        internal static void RocketExplode(Character c) =>
            Features.Safe("rocket explode", () => c.photonView.RPC("RocketExplodeRPC", RpcTarget.All));

        internal static void Jetpack(Character c) =>
            Features.Safe("jetpack", () => c.photonView.RPC("JetpackRPC", RpcTarget.All, 10f));

        internal static void Balloons(Character c, int count)
        {
            Features.Safe("balloons", () =>
            {
                var b = c.refs.balloons;
                int colors = Mathf.Max(1, b.balloonColors != null ? b.balloonColors.Length : 1);
                for (int i = 0; i < count; i++) Traverse.Create(b).Method("TieNewBalloon", new object[] { Random.Range(0, colors) }).GetValue();
            });
        }

        internal static void Parachute(Character c, bool open) =>
            Features.Safe("parachute", () => c.refs.balloons.photonView.RPC("RPC_SetParachute", RpcTarget.All, open));

        // Emotes are data assets, so list whatever the game has loaded.
        private static List<EmoteWheelData> _emotes;

        internal static List<EmoteWheelData> Emotes()
        {
            if (_emotes == null || _emotes.Count == 0)
                _emotes = Resources.FindObjectsOfTypeAll<EmoteWheelData>().Where(e => e != null).OrderBy(e => e.emoteName).ToList();
            return _emotes;
        }

        internal static void Emote(Character c, EmoteWheelData e)
        {
            if (e == null) return;
            Features.Safe("emote", () =>
            {
                if (c.photonView.IsMine) Traverse.Create(c.refs.animations).Method("PlayEmote", new object[] { e.anim }).GetValue();
                else c.photonView.RPC("RPCA_PlayRemove", RpcTarget.All, e.anim, true);
            });
        }

        // ---- World ---------------------------------------------------------------------------

        internal static void ClearEnemies()
        {
            if (!Features.IsHost) return;
            foreach (var m in Object.FindObjectsByType<Mob>(FindObjectsSortMode.None))
                Features.Safe("clear enemy", () => PhotonNetwork.Destroy(m.gameObject));
        }

        private static PhotonView LavaView()
        {
            var lava = Object.FindFirstObjectByType<MovingLava>();
            return lava == null ? null : lava.GetComponent<PhotonView>() ?? lava.GetComponentInParent<PhotonView>();
        }

        internal static bool HasLava() => Object.FindFirstObjectByType<MovingLava>() != null;

        internal static void StartLava()
        {
            var v = LavaView();
            if (v != null) Features.Safe("start lava", () => v.RPC("RPCA_StartLavaRise", RpcTarget.All));
        }

        internal static void SetLavaHeight(float h)
        {
            var v = LavaView();
            if (v != null) Features.Safe("set lava", () => v.RPC("RPCA_SyncLavaHeight", RpcTarget.All, h));
        }

        internal static float LavaHeight()
        {
            var lava = Object.FindFirstObjectByType<MovingLava>();
            return lava != null ? lava.transform.position.y : 0f;
        }

        internal static bool HasCannon() => Object.FindFirstObjectByType<ScoutCannon>() != null;

        internal static void CannonLaunch(Character target)
        {
            if (target == null) return;
            var cannon = Object.FindObjectsByType<ScoutCannon>(FindObjectsSortMode.None)
                .OrderBy(c => Vector3.Distance(c.transform.position, target.Center)).FirstOrDefault();
            if (cannon == null) { Plugin.Log.LogWarning("No scout cannon in this level."); return; }
            Features.Instance.StartCoroutine(CannonRoutine(cannon.GetComponent<PhotonView>(), target.photonView.ViewID));
        }

        private static IEnumerator CannonRoutine(PhotonView v, int id)
        {
            if (v == null) yield break;
            Features.Safe("cannon target", () => v.RPC("RPCA_SetTarget", RpcTarget.All, id));
            yield return new WaitForSeconds(0.3f);
            Features.Safe("cannon light", () => v.RPC("RPCA_Light", RpcTarget.All));
            yield return new WaitForSeconds(2f);
            Features.Safe("cannon launch", () => v.RPC("RPCA_LaunchTarget", RpcTarget.All, id));
        }

        // The game has no placeable campfire, so use its portable stove and firewood items.
        internal static void CampfireKit()
        {
            if (!HostActions.LookPoint(out var p)) return;
            Features.Safe("campfire kit", () =>
            {
                PhotonNetwork.Instantiate("0_Items/PortableStovetopItem", p + Vector3.up * 0.5f, Quaternion.identity);
                for (int i = 0; i < 3; i++) PhotonNetwork.Instantiate("0_Items/FireWood", p + new Vector3(0.4f * i, 1f, 0f), Quaternion.identity);
            });
        }

        // ---- HUD -----------------------------------------------------------------------------

        private static GUIStyle _hud;

        private static GUIStyle HudStyle()
        {
            if (_hud == null)
                _hud = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 15, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            return _hud;
        }

        private static void Outlined(Rect r, string text, Color color)
        {
            var s = HudStyle();
            s.normal.textColor = Color.black;
            foreach (var o in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
                GUI.Label(new Rect(r.x + o.x, r.y + o.y, r.width, r.height), text, s);
            s.normal.textColor = color;
            GUI.Label(r, text, s);
        }

        internal static void DrawHud()
        {
            var me = Features.Me;
            if (me == null) return;
            float y = me.Center.y;
            var fires = Features.Campfires();
            string line = $"Altitude {y:0} m";
            if (fires.Count > 1)
            {
                float lo = fires.Min(f => f.transform.position.y), hi = fires.Max(f => f.transform.position.y);
                if (hi > lo) line += $"   {Mathf.Clamp01((y - lo) / (hi - lo)) * 100f:0}% to the top";
                var next = fires.Where(f => f.transform.position.y > y + 2f).OrderBy(f => f.transform.position.y).FirstOrDefault();
                if (next != null) line += $"   Next campfire {Mathf.Max(0f, next.transform.position.y - y):0} m up";
            }
            var run = RunManager.Instance;
            if (run != null)
            {
                int t = Mathf.FloorToInt(run.TimeSinceRunStarted);
                line += $"   Time {t / 3600}:{t / 60 % 60:00}:{t % 60:00}";
            }
            Outlined(new Rect(Screen.width / 2f - 400, 6, 800, 26), line, Color.white);
        }

        // Edge-of-screen arrows toward teammates and the next campfire.
        internal static void DrawRadar()
        {
            var cam = Camera.main;
            if (cam == null || Features.Me == null) return;
            foreach (var c in Features.Players)
                if (c != Features.Me) Marker(cam, c.Center, Features.NameOf(c), c.data.dead ? new Color(0.94f, 0.36f, 0.37f) : new Color(0.24f, 0.86f, 0.59f));

            var fires = Features.Campfires();
            var next = fires.Where(f => f.transform.position.y > Features.Me.Center.y + 2f).OrderBy(f => f.transform.position.y).FirstOrDefault();
            if (next != null) Marker(cam, next.transform.position, "Campfire", new Color(0.96f, 0.65f, 0.14f));
        }

        private static void Marker(Camera cam, Vector3 world, string label, Color color)
        {
            Vector3 v = cam.WorldToViewportPoint(world);
            bool behind = v.z < 0f;
            if (behind) { v.x = 1f - v.x; v.y = 1f - v.y; }
            bool onScreen = !behind && v.x > 0.03f && v.x < 0.97f && v.y > 0.05f && v.y < 0.95f;
            float dist = Vector3.Distance(cam.transform.position, world);

            if (onScreen)
            {
                Outlined(new Rect(v.x * Screen.width - 70, (1f - v.y) * Screen.height - 40, 140, 22), $"{label}  {dist:0}m", color);
                return;
            }

            // Clamp to the screen edge and point an arrow along the direction from the centre.
            Vector2 centre = new Vector2(Screen.width, Screen.height) / 2f;
            Vector2 dir = new Vector2(v.x * Screen.width, (1f - v.y) * Screen.height) - centre;
            if (dir.sqrMagnitude < 1f) dir = Vector2.up;
            dir.Normalize();
            float k = Mathf.Min(Mathf.Abs((centre.x - 60f) / (dir.x == 0 ? 0.0001f : dir.x)), Mathf.Abs((centre.y - 50f) / (dir.y == 0 ? 0.0001f : dir.y)));
            Vector2 pos = centre + dir * k;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            var prev = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, pos);
            Outlined(new Rect(pos.x - 12, pos.y - 12, 24, 24), ">", color);
            GUI.matrix = prev;
            Outlined(new Rect(pos.x - 70, pos.y + 8, 140, 20), $"{label}  {dist:0}m", color);
        }

        // ---- Profiles ------------------------------------------------------------------------

        // Stored in one config string as name:Label1,Label2;name2:... so presets survive restarts.
        internal static Dictionary<string, List<string>> LoadProfiles()
        {
            var result = new Dictionary<string, List<string>>();
            foreach (var part in Plugin.Instance.Profiles.Value.Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                int i = part.IndexOf(':');
                if (i <= 0) continue;
                result[part.Substring(0, i)] = part.Substring(i + 1).Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries).ToList();
            }
            return result;
        }

        private static void SaveProfiles(Dictionary<string, List<string>> p) =>
            Plugin.Instance.Profiles.Value = string.Join(";", p.Select(kv => kv.Key + ":" + string.Join(",", kv.Value)));

        internal static void SaveProfile(string name)
        {
            name = (name ?? "").Replace(":", "").Replace(";", "").Replace(",", "").Trim();
            if (name.Length == 0) return;
            var all = LoadProfiles();
            all[name] = Plugin.Instance.Toggles.Where(t => t.Value.Value).Select(t => t.Label).ToList();
            SaveProfiles(all);
        }

        internal static void LoadProfile(string name)
        {
            if (!LoadProfiles().TryGetValue(name, out var on)) return;
            foreach (var t in Plugin.Instance.Toggles) t.Value.Value = on.Contains(t.Label);
        }

        internal static void DeleteProfile(string name)
        {
            var all = LoadProfiles();
            if (all.Remove(name)) SaveProfiles(all);
        }
    }
}
