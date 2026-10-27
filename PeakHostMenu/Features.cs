using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace PeakHostMenu
{
    // Toggles and one-shot actions behind the menu. Everything touching game internals is wrapped
    // so a game update that renames something logs a warning instead of breaking the menu.
    internal class Features : MonoBehaviour
    {
        internal static Features Instance;

        internal bool GodMode;
        internal bool InfiniteStamina;
        internal bool NoAfflictions;
        internal float SpeedMultiplier = 1f;
        internal float JumpMultiplier = 1f;

        private float _baseMove = -1f;
        private float _baseJump = -1f;
        private float _nextClear;

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
            var me = Me;
            if (me == null) return;

            Safe("god mode", () =>
            {
                var t = Traverse.Create(me.data).Field("isInvincible");
                if (GodMode) t.SetValue(true);
                else if (t.GetValue<bool>() && _godWasOn) t.SetValue(false);
                _godWasOn = GodMode;
            });

            if (InfiniteStamina) Safe("stamina", Character.GainFullStamina);

            if (NoAfflictions && Time.time >= _nextClear)
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
        }

        private bool _godWasOn;

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
            PhotonNetwork.CloseConnection(c.photonView.Owner);
        }

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
                    int segment = (int)c.data.lastRevivedSegment;
                    c.photonView.RPC("RPCA_ReviveAtPosition", RpcTarget.All, (Me ?? c).Center + Vector3.up, true, segment);
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

        // Spawns a networked item just in front of a player.
        internal static void SpawnItem(Item item, Character at)
        {
            if (item == null || at == null) return;
            Safe("spawn item", () =>
            {
                var pos = at.Center + Vector3.up * 0.5f + at.data.lookDirection_Flat * 1.2f;
                PhotonNetwork.Instantiate("0_Items/" + item.name, pos, Quaternion.identity);
            });
        }

        internal static void Safe(string what, Action a)
        {
            try { a(); }
            catch (Exception e) { Plugin.Log.LogWarning($"{what} failed: {e.GetType().Name}: {e.Message}"); }
        }
    }
}
