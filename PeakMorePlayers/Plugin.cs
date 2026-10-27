using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using PeakShared;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PeakMorePlayers
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "au.leybourne.peakmoreplayers";
        public const string Name = "PEAK More Players @jleybourne";
        public const string Version = "0.2.0";

        internal static Plugin Instance;
        internal static ManualLogSource Log;

        internal ConfigEntry<int> MaxPlayers;
        internal ConfigEntry<Key> MenuKey;

        private Harmony _harmony;
        private bool _open;
        private int _draft;
        private Rect _window = new Rect(60, 60, 340, 10);
        private CursorLockMode _prevLock;
        private bool _prevVisible;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            MaxPlayers = Config.Bind("Lobby", "MaxPlayers", 8,
                new ConfigDescription("Player cap for lobbies you host. Only the host needs this mod.",
                    new AcceptableValueRange<int>(1, 30)));
            MenuKey = Config.Bind("Keys", "MenuKey", Key.F2, "Key that opens the player cap window.");

            _harmony = new Harmony(Guid);
            _harmony.PatchAll();

            Log.LogInfo($"{Name} {Version} loaded, max players {MaxPlayers.Value}");
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb[MenuKey.Value].wasPressedThisFrame) SetOpen(!_open);
            if (_open)
            {
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
                _draft = MaxPlayers.Value;
            }
            else
            {
                Cursor.lockState = _prevLock;
                Cursor.visible = _prevVisible;
            }
        }

        private void OnGUI()
        {
            if (!_open) return;
            GUI.depth = 1;
            _window = GUILayout.Window(0x9EA7, _window, Draw, GUIContent.none, Ui.WindowStyle, GUILayout.Width(340));
        }

        private void Draw(int id)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Player cap", Ui.Title);
            GUILayout.Label(Name.Substring(Name.IndexOf('@')), Ui.Muted_);
            GUILayout.FlexibleSpace();
            if (Ui.Button("Close", width: 56)) SetOpen(false);
            GUILayout.EndHorizontal();

            Ui.BeginCard();
            _draft = Mathf.RoundToInt(Ui.Slider("Max players", _draft, 1, 30, "0"));
            Ui.Hint("Applies to lobbies you host. Large lobbies can lag, so lower it if things get rough.");
            bool changed = _draft != MaxPlayers.Value;
            if (Ui.Button(changed ? "Save" : "Saved", Ui.Kind.Accent, changed)) MaxPlayers.Value = _draft;
            Ui.EndCard();

            Ui.BeginCard();
            if (PhotonNetwork.InRoom)
            {
                var room = PhotonNetwork.CurrentRoom;
                Ui.Label($"Current room: {room.PlayerCount} of {room.MaxPlayers}");
                bool host = PhotonNetwork.IsMasterClient;
                if (Ui.Button("Apply to current room", enabled: host))
                {
                    MaxPlayers.Value = _draft;
                    room.MaxPlayers = (byte)_draft;
                }
                if (!host) Ui.Hint("Only the host can change the current room.");
            }
            else Ui.Hint("Not in a room. The cap applies when you host one.");
            Ui.EndCard();

            GUI.DragWindow(new Rect(0, 0, 10000, 40));
        }

        private void OnDestroy() => _harmony?.UnpatchSelf();
    }
}
