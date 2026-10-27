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
        private Rect _window = new Rect(40, 40, 500, 620);
        private Vector2 _scroll;
        private string _itemFilter = "";
        private int _maxPlayersDraft;
        private float _spawnCount = 1;
        private bool _toHand;
        private Character _itemTarget;
        private int _statusIdx;
        private float _statusAmount = 0.25f;
        private static readonly CharacterAfflictions.STATUSTYPE[] Statuses =
            (CharacterAfflictions.STATUSTYPE[])Enum.GetValues(typeof(CharacterAfflictions.STATUSTYPE));

        private CursorLockMode _prevLock;
        private bool _prevVisible;

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb[Plugin.Instance.MenuKey.Value].wasPressedThisFrame) SetOpen(!_open);
            if (_open)
            {
                // The game re-locks the cursor every frame while you play; keep it free for the menu.
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void SetOpen(bool open)
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
            _window = GUILayout.Window(0x9EA6, _window, Draw, GUIContent.none, Ui.WindowStyle, GUILayout.Width(500));
            _window.x = Mathf.Clamp(_window.x, 0, Screen.width - 120);
            _window.y = Mathf.Clamp(_window.y, 0, Screen.height - 60);
        }

        private static void DrawEsp()
        {
            var cam = Camera.main;
            if (cam == null || Features.Me == null) return;
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 13 };
            foreach (var c in Features.Players)
            {
                if (c == Features.Me) continue;
                Vector3 p = cam.WorldToScreenPoint(c.Center + Vector3.up * 0.8f);
                if (p.z <= 0f) continue;
                float dist = Vector3.Distance(cam.transform.position, c.Center);
                style.normal.textColor = c.data.dead ? Ui.Bad : Color.white;
                GUI.Label(new Rect(p.x - 80, Screen.height - p.y - 12, 160, 24), $"{Features.NameOf(c)}  {dist:0}m", style);
            }
        }

        private void Draw(int id)
        {
            DrawHeader();
            GUILayout.Space(6);

            GUILayout.BeginHorizontal();
            for (int i = 0; i < Tabs.Length; i++)
                if (Ui.Tab(Tabs[i], _tab == i)) { _tab = i; _scroll = Vector2.zero; }
            GUILayout.EndHorizontal();
            GUILayout.Space(4);

            _scroll = GUILayout.BeginScrollView(_scroll, false, false, GUIStyle.none, GUIStyle.none, GUIStyle.none, GUILayout.Height(520));
            switch (_tab)
            {
                case 0: DrawLobby(); break;
                case 1: DrawMe(); break;
                case 2: DrawPlayers(); break;
                case 3: DrawTeleport(); break;
                case 4: DrawItems(); break;
            }
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0, 0, 10000, 40));
        }

        private void DrawHeader()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("PEAK Host Menu", Ui.Title);
            GUILayout.Label($"v{Plugin.Version}", Ui.Muted_);
            GUILayout.FlexibleSpace();
            if (!PhotonNetwork.InRoom) Ui.Pill("OFFLINE", Ui.Muted);
            else if (PhotonNetwork.IsMasterClient) Ui.Pill("HOST", Ui.Good);
            else Ui.Pill("GUEST", Ui.Accent);
            GUILayout.Space(8);
            if (Ui.Button("Close", width: 56)) SetOpen(false);
            GUILayout.EndHorizontal();
        }

        private static bool NoCharacter()
        {
            if (Features.Me != null) return false;
            Ui.BeginCard();
            Ui.Hint("No character yet. Start or join a run first.");
            Ui.EndCard();
            return true;
        }

        private void DrawLobby()
        {
            Ui.Heading("Room");
            Ui.BeginCard();
            _maxPlayersDraft = Mathf.RoundToInt(Ui.Slider("Player cap", _maxPlayersDraft, 1, 30, "0"));
            Ui.Hint("Raise it past 4 with the PEAK More Players mod.");
            if (Ui.Button("Apply to current room", Ui.Kind.Accent, Features.IsHost)) Features.ApplyMaxPlayersToRoom(_maxPlayersDraft);
            GUILayout.BeginHorizontal();
            if (Ui.Button("Lock room", enabled: Features.IsHost)) Features.SetRoomOpen(false);
            if (Ui.Button("Unlock room", enabled: Features.IsHost)) Features.SetRoomOpen(true);
            GUILayout.EndHorizontal();
            if (PhotonNetwork.InRoom)
            {
                var room = PhotonNetwork.CurrentRoom;
                Ui.Hint($"Cap {room.MaxPlayers}   Players {room.PlayerCount}   {(room.IsOpen ? "Open to joins" : "Locked")}");
            }
            else Ui.Hint("Not in a room.");
            Ui.EndCard();

            Ui.Heading("Display");
            Ui.BeginCard();
            Plugin.Instance.Esp.Value = Ui.Switch("Player ESP (names and distance)", Plugin.Instance.Esp.Value);
            Ui.EndCard();
        }

        private void DrawMe()
        {
            var cfg = Plugin.Instance;
            var f = Features.Instance;

            Ui.Heading("Survival");
            Ui.BeginCard();
            cfg.God.Value = Ui.Switch("God mode", cfg.God.Value);
            cfg.Stamina.Value = Ui.Switch("Infinite stamina", cfg.Stamina.Value);
            cfg.NoAfflictions.Value = Ui.Switch("No afflictions", cfg.NoAfflictions.Value);
            cfg.NoFallDamage.Value = Ui.Switch("No fall damage", cfg.NoFallDamage.Value);
            cfg.NoWeight.Value = Ui.Switch("No carry weight", cfg.NoWeight.Value);
            cfg.NoRagdoll.Value = Ui.Switch("No ragdoll (stay upright)", cfg.NoRagdoll.Value);
            cfg.UnlimitedUses.Value = Ui.Switch("Unlimited item uses", cfg.UnlimitedUses.Value);
            Ui.EndCard();

            Ui.Heading("Movement");
            Ui.BeginCard();
            cfg.Fly.Value = Ui.Switch($"Fly   [{cfg.FlyKey.Value}]", cfg.Fly.Value);
            Ui.Hint("WASD to move, Space up, Left Ctrl down.");
            cfg.FlySpeed.Value = Ui.Slider("Fly speed", cfg.FlySpeed.Value, 2f, 60f, "0");
            f.SpeedMultiplier = Ui.Slider("Move speed", f.SpeedMultiplier, 0.5f, 5f, "0.0x");
            f.JumpMultiplier = Ui.Slider("Jump power", f.JumpMultiplier, 0.5f, 4f, "0.0x");
            if (Ui.Button("Reset speed and jump")) { f.SpeedMultiplier = 1f; f.JumpMultiplier = 1f; }
            Ui.EndCard();

            Ui.Heading("Actions");
            Ui.BeginCard();
            var me = Features.Me;
            GUILayout.BeginHorizontal();
            if (Ui.Button("Heal me", Ui.Kind.Accent, me != null)) Features.Heal(me);
            if (Ui.Button("Revive me", enabled: me != null)) Features.Revive(me);
            if (Ui.Button("Die", Ui.Kind.Danger, me != null)) Features.KillSelf();
            GUILayout.EndHorizontal();
            if (me == null) Ui.Hint("Actions need a character. Start or join a run first.");
            Ui.EndCard();
        }

        private void DrawPlayers()
        {
            if (NoCharacter()) return;

            Ui.Heading("Everyone");
            Ui.BeginCard();
            GUILayout.BeginHorizontal();
            if (Ui.Button("Bring all to me")) Features.ForAll(Features.BringToMe);
            if (Ui.Button("Revive all")) Features.ForAll(Features.Revive, true);
            if (Ui.Button("Heal all")) Features.ForAll(Features.Heal, true);
            GUILayout.EndHorizontal();
            Ui.EndCard();

            Ui.Heading("Status tool");
            Ui.BeginCard();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Status", Ui.Title, GUILayout.Width(60));
            if (Ui.Button("<", width: 30)) _statusIdx = (_statusIdx + Statuses.Length - 1) % Statuses.Length;
            GUILayout.Label(Statuses[_statusIdx].ToString(), Ui.Title, GUILayout.Width(90));
            if (Ui.Button(">", width: 30)) _statusIdx = (_statusIdx + 1) % Statuses.Length;
            GUILayout.EndHorizontal();
            _statusAmount = Ui.Slider("Amount", _statusAmount, 0.05f, 1f, "0.00");
            if (Ui.Button("Apply to everyone", enabled: true)) Features.ForAll(p => Features.GiveStatus(p, Statuses[_statusIdx], _statusAmount), true);
            Ui.EndCard();

            Ui.Heading("Players");
            foreach (var c in Features.Players.ToList())
            {
                bool mine = c == Features.Me;
                bool dead = c.data.dead;
                Ui.BeginCard();
                GUILayout.BeginHorizontal();
                GUILayout.Label(Features.NameOf(c), Ui.Title);
                GUILayout.FlexibleSpace();
                if (dead) Ui.Pill("DEAD", Ui.Bad);
                Ui.Pill(mine ? "YOU" : $"{Vector3.Distance(Features.Me.Center, c.Center):0}m", Ui.Muted);
                GUILayout.EndHorizontal();
                Ui.Bar(Features.StaminaFrac(c), Ui.Good, "Stamina");
                Ui.Bar(Features.AfflictedFrac(c), Ui.Bad, "Afflicted");
                GUILayout.Space(4);
                GUILayout.BeginHorizontal();
                if (Ui.Button("Go to", enabled: !mine)) Features.GoTo(c);
                if (Ui.Button("Bring", enabled: !mine)) Features.BringToMe(c);
                if (Ui.Button("Revive")) Features.Revive(c);
                if (Ui.Button("Heal")) Features.Heal(c);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (Ui.Button("Make host", enabled: Features.IsHost && !mine)) Features.MakeHost(c);
                if (Ui.Button("Kick", Ui.Kind.Danger, Features.IsHost && !mine)) Features.Kick(c);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (Ui.Button($"Give {Statuses[_statusIdx]}")) Features.GiveStatus(c, Statuses[_statusIdx], _statusAmount);
                if (Ui.Button("Scoutmaster", enabled: Features.IsHost)) Features.SpawnScoutmaster(c);
                GUILayout.EndHorizontal();
                Ui.EndCard();
            }
        }

        private void DrawTeleport()
        {
            if (NoCharacter()) return;

            Ui.Heading("Waypoints");
            Ui.BeginCard();
            for (int i = 0; i < Features.Instance.Waypoints.Length; i++)
            {
                bool saved = Features.Instance.Waypoints[i].HasValue;
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Slot {i + 1}", Ui.Title, GUILayout.Width(70));
                GUILayout.Label(saved ? "saved" : "empty", Ui.Muted_, GUILayout.Width(50));
                GUILayout.FlexibleSpace();
                if (Ui.Button("Save here", width: 90)) Features.SaveWaypoint(i);
                if (Ui.Button("Go", Ui.Kind.Accent, saved, 60)) Features.GoToWaypoint(i);
                GUILayout.EndHorizontal();
            }
            Ui.EndCard();

            Ui.Heading("Jump to biome");
            Ui.BeginCard();
            GUILayout.BeginHorizontal();
            foreach (Segment s in Enum.GetValues(typeof(Segment)))
                if (Ui.Button(s.ToString())) Features.JumpToSegment(s);
            GUILayout.EndHorizontal();
            Plugin.Instance.TeleportToPing.Value = Ui.Switch("Teleport to where I ping", Plugin.Instance.TeleportToPing.Value);
            if (Ui.Button("Open all luggage", enabled: Features.IsHost)) Features.OpenAllLuggage();
            Ui.EndCard();

            Ui.Heading("Campfires");
            Ui.BeginCard();
            var fires = Features.Campfires();
            if (fires.Count == 0) Ui.Hint("No campfires found in this scene.");
            foreach (var cf in fires)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{cf.advanceToSegment}", Ui.Title, GUILayout.Width(110));
                GUILayout.FlexibleSpace();
                if (Ui.Button("Go there", width: 90)) Features.Warp(Features.Me, cf.transform.position + Vector3.up * 2f);
                if (Ui.Button("Bring everyone", width: 120)) Features.ForAll(p => Features.Warp(p, cf.transform.position + Vector3.up * 2f), true);
                GUILayout.EndHorizontal();
            }
            Ui.EndCard();
        }

        private void DrawItems()
        {
            if (NoCharacter()) return;
            var players = Features.Players.ToList();
            if (_itemTarget == null || !players.Contains(_itemTarget)) _itemTarget = Features.Me;

            Ui.Heading("Target");
            Ui.BeginCard();
            GUILayout.BeginHorizontal();
            foreach (var p in players)
                if (Ui.Tab(Features.NameOf(p), _itemTarget == p)) _itemTarget = p;
            GUILayout.EndHorizontal();
            _spawnCount = Mathf.Round(Ui.Slider("Amount", _spawnCount, 1, 10, "0"));
            _toHand = Ui.Switch("Put in hand (uses the game's own give call, amount 1)", _toHand);
            Ui.EndCard();

            Ui.Heading("Items");
            Ui.BeginCard();
            _itemFilter = GUILayout.TextField(_itemFilter, Ui.Field, GUILayout.Height(26));
            if (_itemFilter.Length == 0) Ui.Hint("Type to filter, for example rope or backpack.");
            Ui.EndCard();

            var items = Features.AllItems();
            if (items.Count == 0) { Ui.Hint("Item list not available. Check the BepInEx log."); return; }
            Ui.BeginCard();
            int shown = 0;
            foreach (var item in items.Where(i => i.name.IndexOf(_itemFilter, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                if (Ui.Button(item.name))
                {
                    if (_toHand) Features.GiveToHand(item, _itemTarget);
                    else Features.SpawnItem(item, _itemTarget, Mathf.RoundToInt(_spawnCount));
                }
                shown++;
            }
            if (shown == 0) Ui.Hint("No items match.");
            Ui.EndCard();
        }
    }
}
