using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace PeakMorePlayers
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "au.leybourne.peakmoreplayers";
        public const string Name = "PEAK More Players";
        public const string Version = "0.1.0";

        internal static Plugin Instance;
        internal static ManualLogSource Log;

        internal ConfigEntry<int> MaxPlayers;

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            MaxPlayers = Config.Bind("Lobby", "MaxPlayers", 8,
                new ConfigDescription("Player cap for lobbies you host. Only the host needs this mod.",
                    new AcceptableValueRange<int>(1, 30)));

            _harmony = new Harmony(Guid);
            _harmony.PatchAll();

            Log.LogInfo($"{Name} {Version} loaded, max players {MaxPlayers.Value}");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
