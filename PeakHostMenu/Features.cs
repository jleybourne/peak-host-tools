using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PeakHostMenu
{
    // Toggles and one-shot actions behind the menu. Everything touching game internals is wrapped
    // so a game update that renames something logs a warning instead of breaking the menu.
    internal class Features : MonoBehaviour
    {
        internal static Features Instance;

        internal float SpeedMultiplier = 1f;
        internal float JumpMultiplier = 1f;
        internal readonly Vector3?[] Waypoints = new Vector3?[3];

        private float _baseMove = -1f;
        private float _baseJump = -1f;
        private float _nextClear;
        private bool _godWasOn;
        private float _baseClimb = -1f, _baseVine, _baseRope;
        private float _nextImmune;
        internal float ClimbMultiplier = 1f, VineMultiplier = 1f, RopeMultiplier = 1f;

        private void Awake() => Instance = this;

        internal static Character Me => Character.localCharacter;

        internal static IEnumerable<Character> Players =>
            Character.AllCharacters.Where(c => c != null && !c.isBot);

        internal static string NameOf(Character c)
        {
            var owner = c.photonView != null ? c.photonView.Owner : null;
            return owner != null && !string.IsNullOrEmpty(owner.NickName) ? owner.NickName : c.name;
        }

        private void Update()
        {
            var kb = Keyboard.current;
            var cfg = Plugin.Instance;
            if (kb != null && !HostMenu.Rebinding)
                foreach (var t in cfg.Toggles)
                    if (t.Hotkey.Value != Key.None && kb[t.Hotkey.Value].wasPressedThisFrame) t.Value.Value = !t.Value.Value;

            var me = Me;
            if (me == null) return;

            Safe("god mode", () =>
            {
                var t = Traverse.Create(me.data).Field("isInvincible");
                if (cfg.God.Value) t.SetValue(true);
                else if (_godWasOn) t.SetValue(false);
                _godWasOn = cfg.God.Value;
            });

            Safe("stamina", () => me.infiniteStam = cfg.Stamina.Value);
            Safe("status lock", () => Traverse.Create(me).Property("statusesLocked").SetValue(cfg.NoAfflictions.Value));

            if (cfg.NoAfflictions.Value && Time.time >= _nextClear)
            {
                _nextClear = Time.time + 0.25f;
                Safe("clear afflictions", () => me.refs.afflictions.ClearAllStatus(true, true));
            }

            Safe("movement", () =>
            {
                var mv = me.refs.movement;
                if (_baseMove < 0f) { _baseMove = mv.movementModifier; _baseJump = mv.jumpImpulse; }
                mv.movementModifier = _baseMove * SpeedMultiplier;
                mv.jumpImpulse = _baseJump * JumpMultiplier;
            });

            Safe("climb speed", () =>
            {
                if (_baseClimb < 0f)
                {
                    _baseClimb = me.refs.climbing.climbSpeedMod;
                    _baseVine = me.refs.vineClimbing.climbSpeedMod;
                    _baseRope = me.refs.ropeHandling.climbSpeedMod;
                }
                me.refs.climbing.climbSpeedMod = _baseClimb * ClimbMultiplier;
                me.refs.vineClimbing.climbSpeedMod = _baseVine * VineMultiplier;
                me.refs.ropeHandling.climbSpeedMod = _baseRope * RopeMultiplier;
            });

            if (Time.time >= _nextImmune)
            {
                _nextImmune = Time.time + 0.25f;
                Safe("immunity", () =>
                {
                    foreach (var kv in cfg.Immune)
                        if (kv.Value.Value) me.refs.afflictions.SetStatus(kv.Key, 0f);
                });
            }

            if (cfg.Fly.Value) Safe("fly", () => DoFly(me, kb));
        }

        // Fly by driving every body part's velocity, so the whole ragdoll moves together.
        private static void DoFly(Character me, Keyboard kb)
        {
            if (kb == null) return;
            var cam = Camera.main;
            Vector3 fwd = cam != null ? cam.transform.forward : me.data.lookDirection;
            Vector3 right = cam != null ? cam.transform.right : me.data.lookDirection_Right;

            Vector3 dir = Vector3.zero;
            if (kb.wKey.isPressed) dir += fwd;
            if (kb.sKey.isPressed) dir -= fwd;
            if (kb.dKey.isPressed) dir += right;
            if (kb.aKey.isPressed) dir -= right;
            if (kb.spaceKey.isPressed) dir += Vector3.up;
            if (kb.leftCtrlKey.isPressed) dir += Vector3.down;

            me.data.sinceGrounded = 0f;
            Traverse.Create(me.data).Field("isGrounded").SetValue(true);
            Vector3 vel = dir.sqrMagnitude > 0.01f ? dir.normalized * Plugin.Instance.FlySpeed.Value : Vector3.zero;
            foreach (var part in me.refs.ragdoll.partList)
            {
                var rig = Traverse.Create(part).Field("rig").GetValue<Rigidbody>();
                if (rig != null) rig.linearVelocity = vel;
            }
        }

        // ---- Lobby ---------------------------------------------------------------------------

        internal static bool IsHost => PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient;

        internal static void ApplyMaxPlayersToRoom(int max)
        {
            if (IsHost) PhotonNetwork.CurrentRoom.MaxPlayers = (byte)Mathf.Clamp(max, 1, 30);
        }

        internal static void SetRoomOpen(bool open)
        {
            if (IsHost) PhotonNetwork.CurrentRoom.IsOpen = open;
        }

        internal static void Kick(Character c)
        {
            if (!IsHost || c == null || c.photonView.IsMine) return;
            // Ask the game's own kick first so the player gets a clean exit, then cut the connection if they're still here.
            var owner = c.photonView.Owner;
            Safe("kick", () => c.player.photonView.RPC("RPC_GetKicked", RpcTarget.All));
            Instance.StartCoroutine(CloseLater(owner));
        }

        private static System.Collections.IEnumerator CloseLater(Photon.Realtime.Player owner)
        {
            yield return new WaitForSeconds(1.5f);
            if (PhotonNetwork.InRoom && PhotonNetwork.PlayerList.Contains(owner)) PhotonNetwork.CloseConnection(owner);
        }

        // Drop a point to the ground so revived or warped players don't land inside rock.
        internal static Vector3 SafePosition(Vector3 from)
        {
            if (Physics.Raycast(from + Vector3.up * 3f, Vector3.down, out var hit, 50f)) return hit.point + Vector3.up * 1.5f;
            return from + Vector3.up;
        }

        // ---- Host extras ---------------------------------------------------------------------

        internal static void GiveStatus(Character c, CharacterAfflictions.STATUSTYPE type, float amount)
        {
            Safe("give status", () =>
            {
                var aff = c.refs.afflictions;
                if (c.photonView.IsMine) { aff.AddStatus(type, amount, false, true, true, false, false); return; }
                var arr = new float[CharacterAfflictions.NumStatusTypes];
                for (int i = 0; i < arr.Length; i++) arr[i] = aff.GetCurrentStatus((CharacterAfflictions.STATUSTYPE)i);
                arr[(int)type] = Mathf.Clamp01(arr[(int)type] + amount);
                aff.photonView.RPC("RPC_ApplyStatusesFromFloatArray", RpcTarget.All, (object)arr);
            });
        }

        internal static void SpawnScoutmaster(Character target)
        {
            if (!IsHost || target == null) return;
            Instance.StartCoroutine(SpawnScoutmasterRoutine(target));
        }

        // Give the spawned Scoutmaster a moment to start, then aim it with the game's own target call.
        private static System.Collections.IEnumerator SpawnScoutmasterRoutine(Character target)
        {
            GameObject go = null;
            Safe("scoutmaster spawn", () => go = PhotonNetwork.InstantiateRoomObject("Character_Scoutmaster", target.Center + Vector3.up * 2f, Quaternion.identity));
            if (go == null) yield break;
            yield return new WaitForSeconds(1f);
            if (go == null || target == null) yield break;
            Safe("scoutmaster target", () => go.GetComponent<PhotonView>().RPC("RPCA_SetCurrentTarget", RpcTarget.All, target.photonView.ViewID, 60f));
        }

        internal static void OpenAllLuggage()
        {
            Safe("open luggage", () =>
            {
                foreach (var l in Luggage.ALL_LUGGAGE.ToList())
                    if (l != null) l.photonView.RPC("OpenLuggageRPC", RpcTarget.All, true);
            });
        }

        internal static void JumpToSegment(Segment s) => Safe("jump to segment", () =>
        {
            if (UnityEngine.Object.FindFirstObjectByType<MapHandler>() == null) { Plugin.Log.LogWarning("Jump to biome only works during a run, not in the airport."); return; }
            MapHandler.JumpToSegment(s);
        });

        internal static void MakeHost(Character c)
        {
            if (!IsHost || c == null || c.photonView.IsMine) return;
            PhotonNetwork.SetMasterClient(c.photonView.Owner);
        }

        // ---- Character actions ---------------------------------------------------------------

        internal static void Warp(Character c, Vector3 pos)
        {
            Safe("warp", () =>
            {
                if (c.photonView.IsMine) Traverse.Create(c).Method("WarpPlayer", new object[] { pos, true }).GetValue();
                else c.photonView.RPC("WarpPlayerRPC", RpcTarget.All, pos, true);
            });
        }

        internal static void BringToMe(Character c)
        {
            if (Me == null || c == null || c == Me) return;
            Warp(c, Me.Center + Vector3.up * 1f);
        }

        internal static void GoTo(Character c)
        {
            if (Me == null || c == null || c == Me) return;
            Warp(Me, c.Center + Vector3.up * 1f);
        }

        internal static void Revive(Character c)
        {
            Safe("revive", () =>
            {
                if (c.photonView.IsMine) Traverse.Create(c).Method("ReviveCharacter", new object[] { true }).GetValue();
                else
                {
                    Vector3 spot = SafePosition((Me ?? c).Center);
                    c.photonView.RPC("RPCA_ReviveAtPosition", RpcTarget.All, spot, true, -1);
                }
            });
        }

        internal static void Heal(Character c)
        {
            Safe("heal", () =>
            {
                if (c.photonView.IsMine)
                {
                    c.refs.afflictions.ClearAllStatus(true, true);
                    Character.GainFullStamina();
                }
                else
                {
                    var aff = c.refs.afflictions;
                    var zeros = new float[CharacterAfflictions.NumStatusTypes];
                    aff.photonView.RPC("RPC_ApplyStatusesFromFloatArray", RpcTarget.All, (object)zeros);
                }
            });
        }

        internal static void KillSelf() => Safe("die", () => { if (Me != null) Traverse.Create(Me).Method("DieInstantly").GetValue(); });

        internal static void ForAll(Action<Character> action, bool includeMe = false)
        {
            foreach (var c in Players.ToList())
                if (includeMe || c != Me) action(c);
        }

        // One-line health readout for the player list. Statuses are synced, so this works for anyone.
        internal static string Condition(Character c)
        {
            try
            {
                float afflicted = c.refs.afflictions.statusSum;
                return $"stamina {c.data.currentStamina:0.00}, afflictions {afflicted:0.00}";
            }
            catch { return ""; }
        }


        // Stamina and affliction load as 0..1 fractions for the player-list bars.
        internal static float StaminaFrac(Character c)
        {
            try { return Mathf.Clamp01(c.data.currentStamina / Mathf.Max(0.01f, c.GetMaxStamina())); }
            catch { return 0f; }
        }

        internal static float AfflictedFrac(Character c)
        {
            try { return Mathf.Clamp01(c.refs.afflictions.statusSum); }
            catch { return 0f; }
        }

        // Uses the game's own "spawn item in hand" call, which the host runs for that character.
        internal static void GiveToHand(Item item, Character c)
        {
            if (item == null || c == null) return;
            Safe("give item", () => c.refs.items.photonView.RPC("RPC_SpawnItemInHandMaster", RpcTarget.MasterClient, item.name));
        }
        // ---- Teleport ------------------------------------------------------------------------

        internal static List<Campfire> Campfires()
        {
            var list = new List<Campfire>();
            Safe("campfires", () =>
                list = UnityEngine.Object.FindObjectsByType<Campfire>(FindObjectsSortMode.None)
                    .OrderBy(c => (int)c.advanceToSegment).ToList());
            return list;
        }

        internal static void SaveWaypoint(int slot)
        {
            if (Me != null) Instance.Waypoints[slot] = Me.Center;
        }

        internal static void GoToWaypoint(int slot)
        {
            var w = Instance.Waypoints[slot];
            if (w.HasValue && Me != null) Warp(Me, w.Value + Vector3.up);
        }

        // ---- Items ---------------------------------------------------------------------------

        private static List<Item> _items;

        internal static List<Item> AllItems()
        {
            if (_items != null && _items.Count > 0) return _items;
            Safe("item list", () =>
            {
                var db = Traverse.Create(typeof(ItemDatabase)).Property("Instance").GetValue();
                var lookup = db != null ? Traverse.Create(db).Field("itemLookup").GetValue<Dictionary<ushort, Item>>() : null;
                _items = lookup != null
                    ? lookup.Values.Where(i => i != null).ToList()
                    : Resources.FindObjectsOfTypeAll<Item>().Where(i => i != null && i.gameObject.scene.handle == 0).ToList();
                _items = _items.OrderBy(i => i.name, StringComparer.OrdinalIgnoreCase).ToList();
            });
            return _items ?? new List<Item>();
        }

        // Spawns networked items just in front of a player.
        internal static void SpawnItem(Item item, Character at, int count = 1)
        {
            if (item == null || at == null) return;
            Safe("spawn item", () =>
            {
                for (int i = 0; i < count; i++)
                {
                    var pos = at.Center + Vector3.up * (0.5f + 0.35f * i) + at.data.lookDirection_Flat * 1.2f;
                    PhotonNetwork.Instantiate("0_Items/" + item.name, pos, Quaternion.identity);
                }
            });
        }

        internal static void Safe(string what, Action a)
        {
            try { a(); }
            catch (Exception e)
            {
                // Reflection and RPC calls wrap the real error, so report the inner one with where it happened.
                var inner = e is System.Reflection.TargetInvocationException && e.InnerException != null ? e.InnerException : e;
                var stack = (inner.StackTrace ?? "").Split('\n').Take(3).Select(l => l.Trim());
                Plugin.Log.LogWarning($"{what} failed: {inner.GetType().Name}: {inner.Message} | {string.Join(" <- ", stack)}");
            }
        }
    }
}
