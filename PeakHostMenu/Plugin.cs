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
        public const string Name = "PEAK Host Menu";
        public const string Version = "0.3.0";

        internal static Plugin Instance;
        internal static ManualLogSource Log;

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

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            MenuKey = Config.Bind("Keys", "MenuKey", Key.F3, "Key that opens and closes the host menu.");
            FlyKey = Config.Bind("Keys", "FlyKey", Key.F4, "Key that toggles fly mode.");
            God = Config.Bind("Toggles", "GodMode", false, "No damage.");
            Stamina = Config.Bind("Toggles", "InfiniteStamina", false, "Stamina never runs out.");
            NoAfflictions = Config.Bind("Toggles", "NoAfflictions", false, "Keep clearing poison, cold, hunger and the rest.");
            NoFallDamage = Config.Bind("Toggles", "NoFallDamage", false, "Falling never hurts.");
            Fly = Config.Bind("Toggles", "Fly", false, "WASD plus Space and Left Ctrl to fly.");
            FlySpeed = Config.Bind("Toggles", "FlySpeed", 12f, new ConfigDescription("Fly speed.", new AcceptableValueRange<float>(2f, 60f)));
            Esp = Config.Bind("Toggles", "PlayerEsp", false, "Draw names and distance over every player.");
            UnlimitedUses = Config.Bind("Toggles", "UnlimitedItemUses", false, "Items never run out of uses.");
            NoWeight = Config.Bind("Toggles", "NoWeight", false, "Carrying weight never slows you.");
            NoRagdoll = Config.Bind("Toggles", "NoRagdoll", false, "Stay upright instead of going limp.");
            TeleportToPing = Config.Bind("Toggles", "TeleportToPing", false, "Pinging a spot teleports you there.");

            _harmony = new Harmony(Guid);
            _harmony.PatchAll();

            gameObject.AddComponent<Features>();
            gameObject.AddComponent<HostMenu>();

            Log.LogInfo($"{Name} {Version} loaded");
        }

        private void OnDestroy() => _harmony?.UnpatchSelf();
    }
}
