using MelonLoader;

namespace UltraPotato
{
    public class SuperPotato : MelonMod
    {
        internal static SuperPotato instance;

#if DEBUG
        internal static bool DEBUG { get { return Settings.debug.Value; } }
#else
        internal const bool DEBUG = false;
#endif
        public static MelonLogger.Instance Log => instance.LoggerInstance;

        public override void OnInitializeMelon()
        {
            instance = this;
            Settings.Register();

#if DEBUG
            NeonLite.Modules.Anticheat.Register(MelonAssembly);
#endif
            NeonLite.NeonLite.LoadModules(MelonAssembly);

        }

        public static class Settings
        {
            public const string h = "SuperPotato";
            public static MelonPreferences_Entry<bool> debug;

            public static MelonPreferences_Entry<bool> enabled;

            public static void Register()
            {
                NeonLite.Settings.AddHolder(h);
#if DEBUG
                debug = NeonLite.Settings.Add(h, "", "debug", "Debug Mode", null, false, true);
#endif

                enabled = NeonLite.Settings.Add(h, "", "enabled", "Enabled", null, true);
            }
        }
    }
}
