using System;
using System.Linq;
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PeakHostMenu
{
    internal class HostMenu : MonoBehaviour
    {
        private static readonly string[] Tabs = { "Lobby", "Me", "Players", "Teleport", "Items" };

        private bool _open;
        private int _tab;
        private Rect _window = new Rect(40, 40, 480, 580);
        private Vector2 _scroll;
        private string _itemFilter = "";
        private int _maxPlayersDraft;
        private int _spawnCount = 1;
        private Character _itemTarget;

        private CursorLockMode _prevLock;
        private bool _prevVisible;

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb[Plugin.Instance.MenuKey.Value].wasPressedThisFrame) Toggle(!_open);
            if (_open)
            {
                // The game re-locks the cursor every frame while you play; keep it free for the menu.
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void Toggle(bool open)
        {
            if (open == _open) return;
            _open = open;
            if (open)
            {
                _prevLock = Cursor.lockState;
                _prevVisible = Cursor.visible;
                _maxPlayersDraft = PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.MaxPlayers : 4;
            }
            else
            {
                Cursor.lockState = _prevLock;
                Cursor.visible = _prevVisible;
            }
        }

        private void OnGUI()
        {
            if (Plugin.Instance.Esp.Value) DrawEsp();
            if (!_open) return;
            GUI.depth = 0;
            _window = GUILayout.Window(0x9EA6, _window, Draw, $"{Plugin.Name} {Plugin.Version}  [{Plugin.Instance.MenuKey.Value}]");
        }

        private static void DrawEsp()
        {
            var cam = Camera.main;
            if (cam == null || Features.Me == null) return;
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            foreach (var c in Features.Players)
            {
                if (c == Features.Me) continue;
                Vector3 p = cam.WorldToScreenPoint(c.Center + Vector3.up * 0.8f);
                if (p.z <= 0f) continue;
                float dist = Vector3.Distance(cam.transform.position, c.Center);
                style.normal.textColor = c.data.dead ? Color.red : Color.white;
                GUI.Label(new Rect(p.x - 80, Screen.height - p.y - 12, 160, 24), $"{Features.NameOf(c)}  {dist:0}m", style);
            }
        }

        private void Draw(int id)
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < Tabs.Length; i++)
                if (GUILayout.Toggle(_tab == i, Tabs[i], GUI.skin.button)) _tab = i;
            GUILayout.EndHorizontal();

            string status = PhotonNetwork.InRoom
                ? (PhotonNetwork.IsMasterClient ? "You are the host" : "Not host: lobby tools are disabled")
                : "Not in a room";
            GUILayout.Label(status);

            _scroll = GUILayout.BeginScrollView(_scroll);
            switch (_tab)
            {
                case 0: DrawLobby(); break;
                case 1: DrawMe(); break;
                case 2: DrawPlayers(); break;
                case 3: DrawTeleport(); break;
                case 4: DrawItems(); break;
            }
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        private static void Toggle(Action<bool> set, bool value, string label) => set(GUILayout.Toggle(value, label));

        private void DrawLobby()
        {
            GUILayout.Label($"Room player cap: {_maxPlayersDraft}  (raise it past 4 with the PEAK More Players mod)");
            _maxPlayersDraft = Mathf.RoundToInt(GUILayout.HorizontalSlider(_maxPlayersDraft, 1, 30));
            GUI.enabled = Features.IsHost;
            if (GUILayout.Button("Apply to current room")) Features.ApplyMaxPlayersToRoom(_maxPlayersDraft);

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Lock room (no joins)")) Features.SetRoomOpen(false);
            if (GUILayout.Button("Unlock room")) Features.SetRoomOpen(true);
            GUILayout.EndHorizontal();
            GUI.enabled = true;

            if (PhotonNetwork.InRoom)
            {
                var room = PhotonNetwork.CurrentRoom;
                GUILayout.Label($"Room cap {room.MaxPlayers}, players {room.PlayerCount}, open {room.IsOpen}");
            }

            GUILayout.Space(8);
            Toggle(v => Plugin.Instance.Esp.Value = v, Plugin.Instance.Esp.Value, "Player ESP (names and distance on screen)");
        }

        private void DrawMe()
        {
            var cfg = Plugin.Instance;
            var f = Features.Instance;
            Toggle(v => cfg.God.Value = v, cfg.God.Value, "God mode (no damage)");
            Toggle(v => cfg.Stamina.Value = v, cfg.Stamina.Value, "Infinite stamina");
            Toggle(v => cfg.NoAfflictions.Value = v, cfg.NoAfflictions.Value, "No afflictions (clears poison, cold, hunger...)");
            Toggle(v => cfg.NoFallDamage.Value = v, cfg.NoFallDamage.Value, "No fall damage");
            Toggle(v => cfg.Fly.Value = v, cfg.Fly.Value, $"Fly  [{cfg.FlyKey.Value}]  WASD, Space up, Left Ctrl down");

            GUILayout.Label($"Fly speed {cfg.FlySpeed.Value:0}");
            cfg.FlySpeed.Value = GUILayout.HorizontalSlider(cfg.FlySpeed.Value, 2f, 60f);
            GUILayout.Label($"Move speed x{f.SpeedMultiplier:0.0}");
            f.SpeedMultiplier = GUILayout.HorizontalSlider(f.SpeedMultiplier, 0.5f, 5f);
            GUILayout.Label($"Jump power x{f.JumpMultiplier:0.0}");
            f.JumpMultiplier = GUILayout.HorizontalSlider(f.JumpMultiplier, 0.5f, 4f);
            if (GUILayout.Button("Reset speed and jump")) { f.SpeedMultiplier = 1f; f.JumpMultiplier = 1f; }

            GUILayout.Space(8);
            var me = Features.Me;
            if (me == null) { GUILayout.Label("No character yet. Join a run first."); return; }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Heal me")) Features.Heal(me);
            if (GUILayout.Button("Revive me")) Features.Revive(me);
            if (GUILayout.Button("Die")) Features.KillSelf();
            GUILayout.EndHorizontal();
        }

        private void DrawPlayers()
        {
            if (Features.Me == null) { GUILayout.Label("No characters yet. Join a run first."); return; }

            GUILayout.Label("Everyone");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Bring all to me")) Features.ForAll(Features.BringToMe);
            if (GUILayout.Button("Revive all")) Features.ForAll(Features.Revive, true);
            if (GUILayout.Button("Heal all")) Features.ForAll(Features.Heal, true);
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            foreach (var c in Features.Players.ToList())
            {
                bool mine = c == Features.Me;
                GUILayout.BeginVertical(GUI.skin.box);
                string dist = mine ? "you" : $"{Vector3.Distance(Features.Me.Center, c.Center):0}m";
                GUILayout.Label($"{Features.NameOf(c)}  ({dist}){(c.data.dead ? "  DEAD" : "")}");
                GUILayout.Label(Features.Condition(c));
                GUILayout.BeginHorizontal();
                GUI.enabled = !mine;
                if (GUILayout.Button("Go to")) Features.GoTo(c);
                if (GUILayout.Button("Bring")) Features.BringToMe(c);
                GUI.enabled = true;
                if (GUILayout.Button("Revive")) Features.Revive(c);
                if (GUILayout.Button("Heal")) Features.Heal(c);
                GUI.enabled = Features.IsHost && !mine;
                if (GUILayout.Button("Host")) Features.MakeHost(c);
                if (GUILayout.Button("Kick")) Features.Kick(c);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
        }

        private void DrawTeleport()
        {
            if (Features.Me == null) { GUILayout.Label("No character yet. Join a run first."); return; }

            GUILayout.Label("Waypoints (your position)");
            for (int i = 0; i < Features.Instance.Waypoints.Length; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Slot {i + 1}: {(Features.Instance.Waypoints[i].HasValue ? "saved" : "empty")}", GUILayout.Width(110));
                if (GUILayout.Button("Save here")) Features.SaveWaypoint(i);
                GUI.enabled = Features.Instance.Waypoints[i].HasValue;
                if (GUILayout.Button("Go")) Features.GoToWaypoint(i);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(8);
            GUILayout.Label("Campfires");
            foreach (var cf in Features.Campfires())
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{cf.advanceToSegment}", GUILayout.Width(110));
                if (GUILayout.Button("Go there")) Features.Warp(Features.Me, cf.transform.position + Vector3.up * 2f);
                if (GUILayout.Button("Bring everyone")) Features.ForAll(p => Features.Warp(p, cf.transform.position + Vector3.up * 2f), true);
                GUILayout.EndHorizontal();
            }
        }

        private void DrawItems()
        {
            var players = Features.Players.ToList();
            if (players.Count == 0) { GUILayout.Label("No characters yet. Join a run first."); return; }
            if (_itemTarget == null || !players.Contains(_itemTarget)) _itemTarget = Features.Me;

            GUILayout.BeginHorizontal();
            GUILayout.Label("Spawn at:", GUILayout.Width(70));
            foreach (var p in players)
                if (GUILayout.Toggle(_itemTarget == p, Features.NameOf(p), GUI.skin.button)) _itemTarget = p;
            GUILayout.EndHorizontal();

            GUILayout.Label($"Amount: {_spawnCount}");
            _spawnCount = Mathf.RoundToInt(GUILayout.HorizontalSlider(_spawnCount, 1, 10));

            GUILayout.BeginHorizontal();
            GUILayout.Label("Filter:", GUILayout.Width(70));
            _itemFilter = GUILayout.TextField(_itemFilter);
            GUILayout.EndHorizontal();

            var items = Features.AllItems();
            if (items.Count == 0) { GUILayout.Label("Item list not available (see BepInEx log)."); return; }
            foreach (var item in items.Where(i => i.name.IndexOf(_itemFilter, StringComparison.OrdinalIgnoreCase) >= 0))
                if (GUILayout.Button(item.name)) Features.SpawnItem(item, _itemTarget, _spawnCount);
        }
    }
}
