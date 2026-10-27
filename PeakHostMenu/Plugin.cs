using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine.InputSystem;

namespace PeakHostMenu
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "au.leybourne.peakhostmenu";
        public const string Name = "PEAK Host Menu";
        public const string Version = "0.1.0";

        internal static Plugin Instance;
        internal static ManualLogSource Log;

        internal ConfigEntry<Key> MenuKey;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            MenuKey = Config.Bind("Menu", "MenuKey", Key.F3, "Key that opens and closes the host menu.");

            gameObject.AddComponent<Features>();
            gameObject.AddComponent<HostMenu>();

            Log.LogInfo($"{Name} {Version} loaded");
        }
    }
}
