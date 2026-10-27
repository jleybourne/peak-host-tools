using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using Photon.Pun;
using PeakShared;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PeakHostMenu
{
    internal class HostMenu : MonoBehaviour
    {
        private static readonly string[] Tabs = { "Lobby", "Me", "Players", "Fun", "Teleport", "World", "Items", "Mods" };
        private static readonly CharacterAfflictions.STATUSTYPE[] Statuses =
            (CharacterAfflictions.STATUSTYPE[])Enum.GetValues(typeof(CharacterAfflictions.STATUSTYPE));

        // True while a hotkey is being captured, so the key press doesn't also toggle something.
        internal static bool Rebinding { get; private set; }

        private bool _open;
        private int _tab;
        private Rect _window = new Rect(40, 40, 600, 640);
        private Vector2 _scroll;
        private float _scrollHeight = 540f;

        private string _itemFilter = "";
        private int _maxPlayersDraft;
        private float _spawnCount = 1;
        private bool _toHand, _atLookPoint;
        private Character _itemTarget, _funTarget;
        private bool _funAll;
        private int _statusIdx;
        private float _statusAmount = 0.25f;
        private float _fling = 600f;
        private string _tpX = "0", _tpY = "0", _tpZ = "0";
        private float _timeDraft = 12f, _fogDraft = 0f, _gravity = -9.81f, _speed = 1f, _timer = 60f, _luggageRadius = 300f;
        private Plugin.Toggle _rebindTarget;
        private float _morale = 0.5f, _balloons = 3f, _lava;
        private int _emoteIdx;
        private Character _carried;
        private string _profileName = "";

        private CursorLockMode _prevLock;
        private bool _prevVisible;

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (Rebinding)
            {
                foreach (var k in kb.allKeys)
                {
                    if (!k.wasPressedThisFrame) continue;
                    _rebindTarget.Hotkey.Value = k.keyCode == Key.Escape ? Key.None : k.keyCode;
                    Rebinding = false;
                    _rebindTarget = null;
                    break;
                }
            }
            else if (kb[Plugin.Instance.MenuKey.Value].wasPressedThisFrame) SetOpen(!_open);

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
            InputBlock.Open = open;
            if (open)
            {
                _prevLock = Cursor.lockState;
                _prevVisible = Cursor.visible;
                _maxPlayersDraft = PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.MaxPlayers : 4;
                _gravity = Physics.gravity.y;
                _speed = Time.timeScale;
                if (HostActions.TimeOfDay(out var t, out _)) _timeDraft = t;
                _fogDraft = HostActions.CurrentFogHeight();
            }
            else
            {
                Cursor.lockState = _prevLock;
                Cursor.visible = _prevVisible;
                Rebinding = false;
            }
        }

        // ---- Overlays ------------------------------------------------------------------------

        private void OnGUI()
        {
            if (Plugin.Instance.Esp.Value) DrawPlayerEsp();
            if (Plugin.Instance.LuggageEsp.Value) DrawLuggageEsp();
            if (Plugin.Instance.Hud.Value) Extras.DrawHud();
            if (Plugin.Instance.Radar.Value) Extras.DrawRadar();
            if (Plugin.Instance.ShowCoords.Value && Features.Me != null)
            {
                var p = Features.Me.Center;
                GUI.Label(new Rect(10, 8, 400, 22), $"X {p.x:0.0}   Y {p.y:0.0}   Z {p.z:0.0}");
            }
            if (!_open) return;
            GUI.depth = 0;
            Ui.UserScale = Plugin.Instance.UiScale.Value;
            float s = Ui.ScaleFor(600f, 700f);
            _scrollHeight = Mathf.Clamp(Screen.height / s - 150f, 200f, 540f);
            var prevMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            _window = GUILayout.Window(0x9EA6, _window, Draw, GUIContent.none, Ui.WindowStyle, GUILayout.Width(600));
            _window.x = Mathf.Clamp(_window.x, 0, Screen.width / s - 120);
            _window.y = Mathf.Clamp(_window.y, 0, Screen.height / s - 60);
            GUI.matrix = prevMatrix;
        }

        private static GUIStyle OverlayStyle(Color c) =>
            new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 13, normal = { textColor = c } };

        private static void DrawPlayerEsp()
        {
            var cam = Camera.main;
            if (cam == null || Features.Me == null) return;
            foreach (var c in Features.Players)
            {
                if (c == Features.Me) continue;
                Vector3 p = cam.WorldToScreenPoint(c.Center + Vector3.up * 0.8f);
                if (p.z <= 0f) continue;
                float dist = Vector3.Distance(cam.transform.position, c.Center);
                GUI.Label(new Rect(p.x - 80, Screen.height - p.y - 12, 160, 24), $"{Features.NameOf(c)}  {dist:0}m", OverlayStyle(c.data.dead ? Ui.Bad : Color.white));
            }
        }

        private static void DrawLuggageEsp()
        {
            var cam = Camera.main;
            if (cam == null) return;
            foreach (var l in HostActions.AllLuggage())
            {
                Vector3 p = cam.WorldToScreenPoint(l.transform.position);
                if (p.z <= 0f || p.z > 400f) continue;
                string label = string.IsNullOrEmpty(l.displayName) ? "Luggage" : l.displayName;
                GUI.Label(new Rect(p.x - 80, Screen.height - p.y - 12, 160, 24), $"{label}  {p.z:0}m", OverlayStyle(Ui.Good));
            }
        }

        // ---- Window --------------------------------------------------------------------------

        private void Draw(int id)
        {
            DrawHeader();
            GUILayout.Space(6);

            GUILayout.BeginHorizontal();
            for (int i = 0; i < Tabs.Length; i++)
                if (Ui.Tab(Tabs[i], _tab == i)) { _tab = i; _scroll = Vector2.zero; }
            GUILayout.EndHorizontal();
            GUILayout.Space(4);

            _scroll = GUILayout.BeginScrollView(_scroll, false, false, GUIStyle.none, GUIStyle.none, GUIStyle.none, GUILayout.Height(_scrollHeight));
            switch (_tab)
            {
                case 0: DrawLobby(); break;
                case 1: DrawMe(); break;
                case 2: DrawPlayers(); break;
                case 3: DrawFun(); break;
                case 4: DrawTeleport(); break;
                case 5: DrawWorld(); break;
                case 6: DrawItems(); break;
                case 7: DrawMods(); break;
            }
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0, 0, 10000, 40));
        }

        private void DrawHeader()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("PEAK Host Menu", Ui.Title);
            GUILayout.Space(6);
            GUILayout.Label($"{Plugin.Author}  v{Plugin.Version}", Ui.Muted_);
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

        private static void Row(params Action[] buttons)
        {
            GUILayout.BeginHorizontal();
            foreach (var b in buttons) b();
            GUILayout.EndHorizontal();
        }

        private static Action Btn(string text, Action click, Ui.Kind kind = Ui.Kind.Normal, bool enabled = true) =>
            () => { if (Ui.Button(text, kind, enabled)) click(); };

        // ---- Lobby ---------------------------------------------------------------------------

        private void DrawLobby()
        {
            Ui.Heading("Room");
            Ui.BeginCard();
            _maxPlayersDraft = Mathf.RoundToInt(Ui.Slider("Player cap", _maxPlayersDraft, 1, 30, "0"));
            Ui.Hint("Raise it past 4 with the PEAK More Players mod.");
            if (Ui.Button("Apply to current room", Ui.Kind.Accent, Features.IsHost)) Features.ApplyMaxPlayersToRoom(_maxPlayersDraft);
            Row(Btn("Lock room", () => Features.SetRoomOpen(false), enabled: Features.IsHost),
                Btn("Unlock room", () => Features.SetRoomOpen(true), enabled: Features.IsHost));
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
            Plugin.Instance.ShowCoords.Value = Ui.Switch("Coordinates overlay", Plugin.Instance.ShowCoords.Value);
            Plugin.Instance.Hud.Value = Ui.Switch("Altitude and timer HUD", Plugin.Instance.Hud.Value);
            Plugin.Instance.Radar.Value = Ui.Switch("Teammate radar", Plugin.Instance.Radar.Value);
            Plugin.Instance.UiScale.Value = Mathf.Round(Ui.Slider("UI size", Plugin.Instance.UiScale.Value, 0.5f, 2f, "0.0x") * 10f) / 10f;
            Ui.Hint("Scales with your screen automatically. This is an extra multiplier.");
            Ui.EndCard();
        }

        // ---- Me ------------------------------------------------------------------------------

        private void DrawMe()
        {
            var cfg = Plugin.Instance;
            var f = Features.Instance;

            Ui.Heading("Survival");
            Ui.BeginCard();
            cfg.God.Value = Ui.Switch("God mode", cfg.God.Value);
            cfg.Stamina.Value = Ui.Switch("Infinite stamina", cfg.Stamina.Value);
            cfg.NoAfflictions.Value = Ui.Switch("No afflictions (all)", cfg.NoAfflictions.Value);
            cfg.NoFallDamage.Value = Ui.Switch("No fall damage", cfg.NoFallDamage.Value);
            cfg.NoWeight.Value = Ui.Switch("No carry weight", cfg.NoWeight.Value);
            cfg.NoRagdoll.Value = Ui.Switch("No ragdoll (stay upright)", cfg.NoRagdoll.Value);
            cfg.UnlimitedUses.Value = Ui.Switch("Unlimited item uses", cfg.UnlimitedUses.Value);
            Ui.EndCard();

            Ui.Heading("Immune to");
            Ui.BeginCard();
            foreach (var s in Plugin.ImmuneList) cfg.Immune[s].Value = Ui.Switch(s.ToString(), cfg.Immune[s].Value);
            Ui.EndCard();

            Ui.Heading("Movement");
            Ui.BeginCard();
            cfg.Fly.Value = Ui.Switch("Fly", cfg.Fly.Value);
            Ui.Hint("WASD to move, Space up, Left Ctrl down.");
            cfg.FlySpeed.Value = Ui.Slider("Fly speed", cfg.FlySpeed.Value, 2f, 60f, "0");
            f.SpeedMultiplier = Ui.Slider("Move speed", f.SpeedMultiplier, 0.5f, 5f, "0.0x");
            f.JumpMultiplier = Ui.Slider("Jump power", f.JumpMultiplier, 0.5f, 4f, "0.0x");
            f.ClimbMultiplier = Ui.Slider("Climb speed", f.ClimbMultiplier, 0.5f, 5f, "0.0x");
            f.VineMultiplier = Ui.Slider("Vine speed", f.VineMultiplier, 0.5f, 5f, "0.0x");
            f.RopeMultiplier = Ui.Slider("Rope speed", f.RopeMultiplier, 0.5f, 5f, "0.0x");
            if (Ui.Button("Reset all speeds"))
                f.SpeedMultiplier = f.JumpMultiplier = f.ClimbMultiplier = f.VineMultiplier = f.RopeMultiplier = 1f;
            Ui.EndCard();

            Ui.Heading("Actions");
            Ui.BeginCard();
            var me = Features.Me;
            Row(Btn("Heal me", () => Features.Heal(me), Ui.Kind.Accent, me != null),
                Btn("Revive me", () => Features.Revive(me), enabled: me != null),
                Btn("Die", Features.KillSelf, Ui.Kind.Danger, me != null));
            if (me == null) Ui.Hint("Actions need a character. Start or join a run first.");
            Ui.EndCard();

            Ui.Heading("Profiles");
            Ui.BeginCard();
            Ui.Hint("Save the switches you have on as a preset, then load it with one click.");
            GUILayout.BeginHorizontal();
            _profileName = GUILayout.TextField(_profileName, Ui.Field, GUILayout.Height(26));
            if (Ui.Button("Save", Ui.Kind.Accent, true, 70)) { Extras.SaveProfile(_profileName); _profileName = ""; }
            GUILayout.EndHorizontal();
            foreach (var kv in Extras.LoadProfiles().ToList())
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(kv.Key, Ui.Title);
                GUILayout.Label($"{kv.Value.Count} on", Ui.Muted_);
                GUILayout.FlexibleSpace();
                if (Ui.Button("Load", width: 70)) Extras.LoadProfile(kv.Key);
                if (Ui.Button("Delete", Ui.Kind.Danger, true, 70)) Extras.DeleteProfile(kv.Key);
                GUILayout.EndHorizontal();
            }
            Ui.EndCard();

            Ui.Heading("Hotkeys");
            Ui.BeginCard();
            Ui.Hint(Rebinding ? "Press a key for the highlighted switch. Esc clears it." : "Click a key to rebind it. None means no hotkey.");
            foreach (var t in cfg.Toggles)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(t.Label, Ui.Title, GUILayout.Width(220));
                GUILayout.FlexibleSpace();
                bool capturing = Rebinding && _rebindTarget == t;
                if (Ui.Button(capturing ? "press a key..." : t.Hotkey.Value.ToString(), capturing ? Ui.Kind.Accent : Ui.Kind.Normal, true, 140))
                {
                    Rebinding = true;
                    _rebindTarget = t;
                }
                GUILayout.EndHorizontal();
            }
            Ui.EndCard();
        }

        // ---- Players -------------------------------------------------------------------------

        private void DrawPlayers()
        {
            if (NoCharacter()) return;

            Ui.Heading("Everyone");
            Ui.BeginCard();
            Row(Btn("Bring all to me", () => Features.ForAll(Features.BringToMe)),
                Btn("Revive all", () => Features.ForAll(Features.Revive, true)),
                Btn("Heal all", () => Features.ForAll(Features.Heal, true)));
            Ui.EndCard();

            Ui.Heading("Co-op helpers");
            Ui.BeginCard();
            Plugin.Instance.AutoRevive.Value = Ui.Switch("Auto-revive dead players (host)", Plugin.Instance.AutoRevive.Value);
            Plugin.Instance.AutoReviveDelay.Value = Mathf.Round(Ui.Slider("Revive after", Plugin.Instance.AutoReviveDelay.Value, 3f, 120f, "0s"));
            Plugin.Instance.AutoHeal.Value = Ui.Switch("Auto-heal and feed everyone (host)", Plugin.Instance.AutoHeal.Value);
            Plugin.Instance.AutoHealInterval.Value = Mathf.Round(Ui.Slider("Heal every", Plugin.Instance.AutoHealInterval.Value, 2f, 120f, "0s"));
            _morale = Ui.Slider("Morale boost", _morale, 0.1f, 1f, "0.00");
            Row(Btn("Boost everyone's stamina", () => Extras.MoraleBoost(_morale), Ui.Kind.Accent, Features.IsHost));
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
            if (Ui.Button("Apply to everyone")) Features.ForAll(p => Features.GiveStatus(p, Statuses[_statusIdx], _statusAmount), true);
            Ui.EndCard();

            Ui.Heading("Players");
            foreach (var c in Features.Players.ToList())
            {
                bool mine = c == Features.Me;
                Ui.BeginCard();
                GUILayout.BeginHorizontal();
                GUILayout.Label(Features.NameOf(c), Ui.Title);
                GUILayout.FlexibleSpace();
                if (c.data.dead) Ui.Pill("DEAD", Ui.Bad);
                Ui.Pill(mine ? "YOU" : $"{Vector3.Distance(Features.Me.Center, c.Center):0}m", Ui.Muted);
                GUILayout.EndHorizontal();
                Ui.Bar(Features.StaminaFrac(c), Ui.Good, "Stamina");
                Ui.Bar(Features.AfflictedFrac(c), Ui.Bad, "Afflicted");
                GUILayout.Space(4);
                Row(Btn("Go to", () => Features.GoTo(c), enabled: !mine), Btn("Bring", () => Features.BringToMe(c), enabled: !mine),
                    Btn("Revive", () => Features.Revive(c)), Btn("Heal", () => Features.Heal(c)));
                Row(Btn("Make host", () => Features.MakeHost(c), enabled: Features.IsHost && !mine),
                    Btn("Kick", () => Features.Kick(c), Ui.Kind.Danger, Features.IsHost && !mine));
                Row(Btn($"Give {Statuses[_statusIdx]}", () => Features.GiveStatus(c, Statuses[_statusIdx], _statusAmount)),
                    Btn("Scoutmaster", () => Features.SpawnScoutmaster(c), enabled: Features.IsHost));
                Ui.EndCard();
            }
        }

        // ---- Fun -----------------------------------------------------------------------------

        private void TargetPicker(ref Character target, ref bool all)
        {
            var players = Features.Players.ToList();
            if (target == null || !players.Contains(target)) target = Features.Me;
            Ui.BeginCard();
            GUILayout.BeginHorizontal();
            foreach (var p in players)
                if (Ui.Tab(Features.NameOf(p), !all && target == p)) { target = p; all = false; }
            GUILayout.EndHorizontal();
            all = Ui.Switch("Apply to everyone", all);
            Ui.EndCard();
        }

        private void Apply(Action<Character> action)
        {
            if (_funAll) Features.ForAll(action, true);
            else if (_funTarget != null) action(_funTarget);
        }

        private Action PerTarget(string text, Action<Character> perTarget, Ui.Kind kind = Ui.Kind.Normal) =>
            () => { if (Ui.Button(text, kind)) Apply(perTarget); };

        private void DrawFun()
        {
            if (NoCharacter()) return;
            Ui.Heading("Target");
            TargetPicker(ref _funTarget, ref _funAll);

            Ui.Heading("Body");
            Ui.BeginCard();
            Row(PerTarget("Freeze", c => HostActions.Freeze(c, true)), PerTarget("Unfreeze", c => HostActions.Freeze(c, false)));
            Row(PerTarget("Make fall", HostActions.Fall), PerTarget("Make jump", HostActions.Jump));
            Row(PerTarget("Pass out", HostActions.PassOut), PerTarget("Wake up", HostActions.Wake),
                PerTarget("Stop climbing", HostActions.StopClimbing));
            _fling = Ui.Slider("Fling strength", _fling, 100f, 3000f, "0");
            Row(PerTarget("Fling upward", c => HostActions.Fling(c, _fling)));
            Ui.Hint("Fling strength is a guess. Raise it if nothing happens.");
            Ui.EndCard();

            Ui.Heading("Transform");
            Ui.BeginCard();
            Row(PerTarget("Make skeleton", c => Extras.Skeleton(c, true)), PerTarget("Undo skeleton", c => Extras.Skeleton(c, false)),
                PerTarget("Zombify", Extras.Zombify, Ui.Kind.Danger));
            Ui.EndCard();

            Ui.Heading("Launch and float");
            Ui.BeginCard();
            Row(PerTarget("Rocket", Extras.Rocket), PerTarget("Rocket explode", Extras.RocketExplode, Ui.Kind.Danger), PerTarget("Jetpack fuel", Extras.Jetpack));
            _balloons = Mathf.Round(Ui.Slider("Balloons", _balloons, 1f, 10f, "0"));
            Row(PerTarget("Tie balloons", c => Extras.Balloons(c, (int)_balloons)), PerTarget("Open parachute", c => Extras.Parachute(c, true)),
                PerTarget("Close parachute", c => Extras.Parachute(c, false)));
            Row(PerTarget("Fire from scout cannon", Extras.CannonLaunch));
            if (!Extras.HasCannon()) Ui.Hint("There's no scout cannon in this level.");
            Ui.EndCard();

            Ui.Heading("Emote");
            Ui.BeginCard();
            var emotes = Extras.Emotes();
            if (emotes.Count == 0) Ui.Hint("No emotes loaded yet.");
            else
            {
                _emoteIdx = Mathf.Clamp(_emoteIdx, 0, emotes.Count - 1);
                GUILayout.BeginHorizontal();
                if (Ui.Button("<", width: 30)) _emoteIdx = (_emoteIdx + emotes.Count - 1) % emotes.Count;
                GUILayout.Label(emotes[_emoteIdx].emoteName, Ui.Title);
                GUILayout.FlexibleSpace();
                if (Ui.Button(">", width: 30)) _emoteIdx = (_emoteIdx + 1) % emotes.Count;
                GUILayout.EndHorizontal();
                Row(PerTarget("Play emote", c => Extras.Emote(c, emotes[_emoteIdx]), Ui.Kind.Accent));
            }
            Ui.EndCard();

            Ui.Heading("Carry");
            Ui.BeginCard();
            var all = Features.Players.ToList();
            if (all.Count < 2) Ui.Hint("Needs two players.");
            else
            {
                if (_carried == null || !all.Contains(_carried) || _carried == _funTarget) _carried = all.FirstOrDefault(p => p != _funTarget);
                Ui.Hint("The target above carries the player picked here.");
                GUILayout.BeginHorizontal();
                foreach (var p in all) if (Ui.Tab(Features.NameOf(p), _carried == p)) _carried = p;
                GUILayout.EndHorizontal();
                Row(Btn("Start carrying", () => Extras.Carry(_funTarget, _carried), Ui.Kind.Accent), Btn("Drop", () => Extras.Drop(_funTarget, _carried)));
            }
            Ui.EndCard();

            Ui.Heading("Trouble");
            Ui.BeginCard();
            Row(PerTarget("Eruption (volcano areas only)", HostActions.Eruption, Ui.Kind.Danger),
                PerTarget("Coconut on head", c => HostActions.DropProp(c, "Item_Coconut", 8f)));
            Row(PerTarget("Banana peel", c => HostActions.DropProp(c, "Berrynana Peel Yellow", 0.5f)),
                PerTarget("Beehive", c => HostActions.DropProp(c, "Beehive", 2f)),
                PerTarget("Dynamite", c => HostActions.DropProp(c, "Dynamite", 3f)));
            Row(PerTarget("Scoutmaster", Features.SpawnScoutmaster));
            Ui.EndCard();
        }

        // ---- Teleport ------------------------------------------------------------------------

        private void DrawTeleport()
        {
            if (NoCharacter()) return;

            Ui.Heading("Coordinates");
            Ui.BeginCard();
            GUILayout.BeginHorizontal();
            _tpX = GUILayout.TextField(_tpX, Ui.Field, GUILayout.Height(26));
            _tpY = GUILayout.TextField(_tpY, Ui.Field, GUILayout.Height(26));
            _tpZ = GUILayout.TextField(_tpZ, Ui.Field, GUILayout.Height(26));
            GUILayout.EndHorizontal();
            Row(Btn("Use my position", () =>
                {
                    var p = Features.Me.Center;
                    _tpX = p.x.ToString("0.0", CultureInfo.InvariantCulture);
                    _tpY = p.y.ToString("0.0", CultureInfo.InvariantCulture);
                    _tpZ = p.z.ToString("0.0", CultureInfo.InvariantCulture);
                }),
                Btn("Teleport me", () => { if (TryXyz(out var p)) Features.Warp(Features.Me, p); }, Ui.Kind.Accent));
            Row(Btn("Bring everyone here", () => { if (TryXyz(out var p)) Features.ForAll(c => Features.Warp(c, p), true); }));
            Ui.EndCard();

            Ui.Heading("Where I'm looking");
            Ui.BeginCard();
            Row(Btn("Teleport me", () => HostActions.ToCrosshair(Features.Me), Ui.Kind.Accent),
                Btn("Everyone else", () => Features.ForAll(HostActions.ToCrosshair)));
            Row(Btn("To a random player", HostActions.ToRandomPlayer));
            Row(Btn("Undo last teleport", Features.UndoWarp, enabled: Features.PreviousPosition.HasValue));
            Ui.EndCard();

            Ui.Heading("Swap players");
            Ui.BeginCard();
            var players = Features.Players.ToList();
            if (players.Count < 2) Ui.Hint("Needs two players.");
            else
            {
                if (_funTarget == null || !players.Contains(_funTarget)) _funTarget = players[0];
                if (_itemTarget == null || !players.Contains(_itemTarget) || _itemTarget == _funTarget) _itemTarget = players.First(p => p != _funTarget);
                Ui.Hint("First row is one player, second row is the other.");
                GUILayout.BeginHorizontal();
                foreach (var p in players) if (Ui.Tab(Features.NameOf(p), _funTarget == p)) { _funTarget = p; _funAll = false; }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                foreach (var p in players) if (Ui.Tab(Features.NameOf(p), _itemTarget == p)) _itemTarget = p;
                GUILayout.EndHorizontal();
                if (Ui.Button("Swap them")) HostActions.Swap(_funTarget, _itemTarget);
            }
            Ui.EndCard();

            Ui.Heading("Waypoints");
            Ui.BeginCard();
            for (int i = 0; i < Features.Instance.Waypoints.Length; i++)
            {
                int slot = i;
                bool saved = Features.Instance.Waypoints[i].HasValue;
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Slot {i + 1}", Ui.Title, GUILayout.Width(70));
                GUILayout.Label(saved ? "saved" : "empty", Ui.Muted_, GUILayout.Width(50));
                GUILayout.FlexibleSpace();
                if (Ui.Button("Save here", width: 90)) Features.SaveWaypoint(slot);
                if (Ui.Button("Go", Ui.Kind.Accent, saved, 60)) Features.GoToWaypoint(slot);
                GUILayout.EndHorizontal();
            }
            Ui.EndCard();

            Ui.Heading("Biomes");
            Ui.BeginCard();
            GUILayout.BeginHorizontal();
            foreach (Segment s in Enum.GetValues(typeof(Segment)))
                if (Ui.Button(s.ToString())) Features.JumpToSegment(s);
            GUILayout.EndHorizontal();
            Plugin.Instance.TeleportToPing.Value = Ui.Switch("Teleport to where I ping", Plugin.Instance.TeleportToPing.Value);
            Ui.EndCard();

            Ui.Heading("Campfires");
            Ui.BeginCard();
            var fires = Features.Campfires();
            if (fires.Count == 0) Ui.Hint("No campfires found in this scene.");
            foreach (var cf in fires)
            {
                var spot = cf.transform.position + Vector3.up * 2f;
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{cf.advanceToSegment}", Ui.Title, GUILayout.Width(110));
                GUILayout.FlexibleSpace();
                if (Ui.Button("Go there", width: 90)) Features.Warp(Features.Me, spot);
                if (Ui.Button("Bring everyone", width: 120)) Features.ForAll(p => Features.Warp(p, spot), true);
                GUILayout.EndHorizontal();
            }
            Ui.EndCard();
        }

        private bool TryXyz(out Vector3 p)
        {
            p = default;
            var ci = CultureInfo.InvariantCulture;
            if (!float.TryParse(_tpX, NumberStyles.Float, ci, out var x) || !float.TryParse(_tpY, NumberStyles.Float, ci, out var y)
                || !float.TryParse(_tpZ, NumberStyles.Float, ci, out var z)) return false;
            p = new Vector3(x, y, z);
            return true;
        }

        // ---- World ---------------------------------------------------------------------------

        private void DrawWorld()
        {
            Ui.Heading("Sky");
            Ui.BeginCard();
            if (HostActions.TimeOfDay(out _, out var max))
            {
                _timeDraft = Ui.Slider("Time of day", _timeDraft, 0f, max, "0.0");
                if (Ui.Button("Set time for everyone", Ui.Kind.Accent, Features.IsHost)) HostActions.SetTimeOfDay(_timeDraft);
            }
            else Ui.Hint("No day and night cycle in this scene.");
            _fogDraft = Ui.Slider("Fog height", _fogDraft, 0f, Mathf.Max(500f, _fogDraft), "0");
            Row(Btn("Set fog", () => HostActions.SetFogHeight(_fogDraft), enabled: Features.IsHost),
                Btn("Resume fog", HostActions.ResumeFog, enabled: Features.IsHost));
            Row(Btn("Wind on", () => HostActions.SetWind(true), enabled: Features.IsHost),
                Btn("Wind off", () => HostActions.SetWind(false), enabled: Features.IsHost));
            Ui.EndCard();

            Ui.Heading("Physics and speed (this computer only)");
            Ui.BeginCard();
            _gravity = Ui.Slider("Gravity", _gravity, -30f, 0f, "0.0");
            Row(Btn("Apply gravity", () => HostActions.SetGravity(_gravity)), Btn("Reset", () => { _gravity = -9.81f; HostActions.SetGravity(_gravity); }));
            _speed = Ui.Slider("Game speed", _speed, 0f, 4f, "0.0x");
            Row(Btn("Apply speed", () => HostActions.SetGameSpeed(_speed)), Btn("Pause", () => { _speed = 0f; HostActions.SetGameSpeed(0f); }),
                Btn("Resume", () => { _speed = 1f; HostActions.SetGameSpeed(1f); }));
            Ui.EndCard();

            Ui.Heading("Campfires");
            Ui.BeginCard();
            Row(Btn("Light all", HostActions.LightAllCampfires, enabled: Features.IsHost),
                Btn("Put out all", HostActions.ExtinguishAllCampfires, enabled: Features.IsHost));
            Ui.EndCard();

            Ui.Heading("Luggage");
            Ui.BeginCard();
            Plugin.Instance.LuggageEsp.Value = Ui.Switch("Luggage ESP", Plugin.Instance.LuggageEsp.Value);
            _luggageRadius = Ui.Slider("Nearby radius", _luggageRadius, 20f, 600f, "0m");
            Row(Btn("Open nearby", () => HostActions.OpenNearbyLuggage(_luggageRadius), enabled: Features.IsHost),
                Btn("Open all", Features.OpenAllLuggage, enabled: Features.IsHost),
                Btn("Warp to nearest", HostActions.WarpToNearestLuggage, enabled: Features.Me != null));
            Ui.EndCard();

            Ui.Heading("Items lying around");
            Ui.BeginCard();
            Row(Btn("Freeze all", () => HostActions.FreezeItems(true)), Btn("Unfreeze all", () => HostActions.FreezeItems(false)),
                Btn("Spin all", HostActions.SpinItems));
            Row(Btn("Cook all", HostActions.CookItems), Btn("Deny pickups", HostActions.DenyPickups),
                Btn("Clear all", HostActions.ClearItems, Ui.Kind.Danger, Features.IsHost));
            Ui.EndCard();

            Ui.Heading("Enemies");
            Ui.BeginCard();
            Row(Btn("Clear all enemies", Extras.ClearEnemies, Ui.Kind.Danger, Features.IsHost));
            Ui.EndCard();

            Ui.Heading("Lava");
            Ui.BeginCard();
            if (!Extras.HasLava()) Ui.Hint("There's no rising lava in this level.");
            else
            {
                Row(Btn("Start the rise", Extras.StartLava, enabled: Features.IsHost));
                _lava = Ui.Slider("Lava height", _lava == 0f ? Extras.LavaHeight() : _lava, Extras.LavaHeight() - 200f, Extras.LavaHeight() + 600f, "0");
                Row(Btn("Set lava height", () => Extras.SetLavaHeight(_lava), enabled: Features.IsHost));
            }
            Ui.EndCard();

            Ui.Heading("Campfire anywhere");
            Ui.BeginCard();
            Row(Btn("Spawn stove and firewood where I look", Extras.CampfireKit, Ui.Kind.Accent, Features.IsHost));
            Ui.EndCard();

            Ui.Heading("Game state");
            Ui.BeginCard();
            Row(Btn("Force win", HostActions.ForceWin, Ui.Kind.Danger, Features.IsHost),
                Btn("Call helicopter", HostActions.CallHelicopter, enabled: Features.IsHost));
            _timer = Mathf.Round(Ui.Slider("Peak timer", _timer, 0f, 600f, "0s"));
            if (Ui.Button("Set peak timer", enabled: Features.IsHost)) HostActions.SetPeakTimer((int)_timer);
            Ui.EndCard();
        }

        // ---- Items ---------------------------------------------------------------------------

        private void DrawItems()
        {
            if (NoCharacter()) return;
            var players = Features.Players.ToList();
            if (_itemTarget == null || !players.Contains(_itemTarget)) _itemTarget = Features.Me;

            Ui.Heading("My inventory");
            Ui.BeginCard();
            for (int i = 0; i < 3; i++)
            {
                int slot = i;
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Slot {i + 1}", Ui.Title, GUILayout.Width(60));
                GUILayout.Label(HostActions.SlotName(i), Ui.Muted_);
                GUILayout.FlexibleSpace();
                if (Ui.Button("Recharge", width: 90)) HostActions.RechargeSlot(slot);
                if (Ui.Button("Remove", Ui.Kind.Danger, true, 80)) HostActions.RemoveSlot(slot);
                GUILayout.EndHorizontal();
            }
            Ui.EndCard();

            Ui.Heading("Target");
            Ui.BeginCard();
            GUILayout.BeginHorizontal();
            foreach (var p in players)
                if (Ui.Tab(Features.NameOf(p), _itemTarget == p)) _itemTarget = p;
            GUILayout.EndHorizontal();
            _spawnCount = Mathf.Round(Ui.Slider("Amount", _spawnCount, 1, 10, "0"));
            _atLookPoint = Ui.Switch("Spawn where I'm looking", _atLookPoint);
            _toHand = Ui.Switch("Put in hand (the game's own give call, amount 1)", _toHand);
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
                    else if (_atLookPoint) HostActions.SpawnAtLookPoint(item, Mathf.RoundToInt(_spawnCount));
                    else Features.SpawnItem(item, _itemTarget, Mathf.RoundToInt(_spawnCount));
                }
                shown++;
            }
            if (shown == 0) Ui.Hint("No items match.");
            Ui.EndCard();
        }

        // ---- Mods ----------------------------------------------------------------------------

        private static bool IsKeyType(Type t) => t == typeof(Key) || t == typeof(KeyCode) || t == typeof(KeyboardShortcut);

        private void DrawMods()
        {
            Ui.Heading("Loaded mods");
            var infos = Chainloader.PluginInfos.Values.OrderBy(i => i.Metadata.Name).ToList();
            Ui.Hint($"{infos.Count} BepInEx mods loaded. Hotkeys are read from each mod's config file.");
            foreach (var info in infos)
            {
                Ui.BeginCard();
                GUILayout.BeginHorizontal();
                GUILayout.Label(info.Metadata.Name, Ui.Title);
                GUILayout.FlexibleSpace();
                Ui.Pill($"v{info.Metadata.Version}", Ui.Muted);
                GUILayout.EndHorizontal();
                Ui.Hint(info.Metadata.GUID);

                var keys = new List<string>();
                var config = info.Instance != null ? info.Instance.Config : null;
                if (config != null)
                    foreach (var def in config.Keys)
                    {
                        var entry = config[def];
                        if (!IsKeyType(entry.SettingType)) continue;
                        string value = entry.BoxedValue?.ToString() ?? "";
                        if (value == "None" || value.Length == 0) continue;
                        keys.Add($"{def.Section} / {def.Key}: {value}");
                    }
                if (keys.Count == 0) Ui.Hint("No hotkeys.");
                else foreach (var k in keys) Ui.Label(k);
                Ui.EndCard();
            }
        }
    }
}
