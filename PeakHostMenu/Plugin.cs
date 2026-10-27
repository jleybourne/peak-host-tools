using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine.InputSystem;

namespace PeakHostMenu
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "au.leybourne.peakhostmenu";
        public const string Name = "PEAK Host Menu @jleybourne";
        public const string Author = "@jleybourne";
        public const string Version = "0.4.0";

        internal static Plugin Instance;
        internal static ManualLogSource Log;

        // A switch in the menu plus an optional hotkey for it.
        internal class Toggle
        {
            internal string Label;
            internal ConfigEntry<bool> Value;
            internal ConfigEntry<Key> Hotkey;
        }

        internal readonly List<Toggle> Toggles = new List<Toggle>();

        internal ConfigEntry<Key> MenuKey;
        internal ConfigEntry<Key> FlyKey;
        internal ConfigEntry<bool> God;
        internal ConfigEntry<bool> Stamina;
        internal ConfigEntry<bool> NoAfflictions;
        internal ConfigEntry<bool> NoFallDamage;
        internal ConfigEntry<bool> Fly;
        internal ConfigEntry<float> FlySpeed;
        internal ConfigEntry<bool> Esp;
        internal ConfigEntry<bool> UnlimitedUses;
        internal ConfigEntry<bool> NoWeight;
        internal ConfigEntry<bool> NoRagdoll;
        internal ConfigEntry<bool> TeleportToPing;
        internal ConfigEntry<bool> ShowCoords;
        internal ConfigEntry<bool> LuggageEsp;

        // One immunity switch per status, so you can be immune to cold but still get hungry.
        internal readonly Dictionary<CharacterAfflictions.STATUSTYPE, ConfigEntry<bool>> Immune =
            new Dictionary<CharacterAfflictions.STATUSTYPE, ConfigEntry<bool>>();

        internal static readonly CharacterAfflictions.STATUSTYPE[] ImmuneList =
        {
            CharacterAfflictions.STATUSTYPE.Injury, CharacterAfflictions.STATUSTYPE.Hunger, CharacterAfflictions.STATUSTYPE.Cold,
            CharacterAfflictions.STATUSTYPE.Hot, CharacterAfflictions.STATUSTYPE.Poison, CharacterAfflictions.STATUSTYPE.Curse,
            CharacterAfflictions.STATUSTYPE.Drowsy, CharacterAfflictions.STATUSTYPE.Spores, CharacterAfflictions.STATUSTYPE.Petrify,
        };

        private Harmony _harmony;

        private ConfigEntry<bool> Switch(string key, string label, string desc)
        {
            var value = Config.Bind("Toggles", key, false, desc);
            var hot = Config.Bind("Hotkeys", key, Key.None, $"Key that toggles: {label}. None disables it.");
            Toggles.Add(new Toggle { Label = label, Value = value, Hotkey = hot });
            return value;
        }

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            MenuKey = Config.Bind("Keys", "MenuKey", Key.F3, "Key that opens and closes the host menu.");
            FlyKey = Config.Bind("Keys", "FlyKey", Key.F4, "Key that toggles fly mode.");

            God = Switch("GodMode", "God mode", "No damage.");
            Stamina = Switch("InfiniteStamina", "Infinite stamina", "Stamina never runs out.");
            NoAfflictions = Switch("NoAfflictions", "No afflictions", "Freeze statuses and keep clearing them.");
            NoFallDamage = Switch("NoFallDamage", "No fall damage", "Falling never hurts.");
            NoWeight = Switch("NoWeight", "No carry weight", "Carrying weight never slows you.");
            NoRagdoll = Switch("NoRagdoll", "No ragdoll", "Stay upright instead of going limp.");
            UnlimitedUses = Switch("UnlimitedItemUses", "Unlimited item uses", "Items never run out of uses.");
            TeleportToPing = Switch("TeleportToPing", "Teleport to ping", "Pinging a spot teleports you there.");
            Esp = Switch("PlayerEsp", "Player ESP", "Draw names and distance over every player.");
            LuggageEsp = Switch("LuggageEsp", "Luggage ESP", "Draw luggage through walls.");
            ShowCoords = Switch("ShowCoords", "Coordinates overlay", "Show your position on screen.");
            Fly = Switch("Fly", "Fly", "WASD plus Space and Left Ctrl to fly.");
            FlySpeed = Config.Bind("Toggles", "FlySpeed", 12f, new ConfigDescription("Fly speed.", new AcceptableValueRange<float>(2f, 60f)));
            // The fly hotkey predates the generic ones, so it keeps its own entry and defaults to F4.
            Toggles.Find(t => t.Value == Fly).Hotkey.Value = FlyKey.Value;

            foreach (var s in ImmuneList)
                Immune[s] = Config.Bind("Immunity", s.ToString(), false, $"Stay immune to {s}.");

            _harmony = new Harmony(Guid);
            _harmony.PatchAll();

            gameObject.AddComponent<Features>();
            gameObject.AddComponent<HostMenu>();

            Log.LogInfo($"{Name} {Version} loaded");
        }

        private void OnDestroy() => _harmony?.UnpatchSelf();
    }
}
