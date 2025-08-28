using MelonLoader;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UltraPotato.Modules;
using UnityEngine;

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

        internal static AssetBundle bundle;

        public override void OnInitializeMelon()
        {
            instance = this;
            bundle = AssetBundle.LoadFromMemory(Resources.r.assetbundle);

            Settings.Register();

#if DEBUG
            NeonLite.Modules.Anticheat.Register(MelonAssembly);
#endif
            NeonLite.NeonLite.LoadModules(MelonAssembly);

        }

        public override void OnLateInitializeMelon()
        {
            var holder = GameObject.Instantiate(bundle.LoadAsset<GameObject>("Assets/Prefabs/holder.prefab"));
            holder.name = "SuperPotato";
            GameObject.DontDestroyOnLoad(holder);

            holder.AddComponent<CompleteRenderer>();
            holder.AddComponent<QualityControl>();
            CompleteRenderer.camera = holder.GetComponent<Camera>();
            CompleteRenderer.SetupRenderTextures();

            Singleton<Game>.Instance.OnInitializationComplete += CheckCamera;  
        }

        private void CheckCamera() => CompleteRenderer.camera.enabled = false;

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

    public static class Extensions
    {
#pragma warning disable CS0162
        [Conditional("DEBUG")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void DebugMsg(this MelonLogger.Instance log, string msg)
        {
            if (SuperPotato.DEBUG)
            {
                log.Msg(msg);
                //UnityEngine.Debug.Log($"[SuperPotato] {msg}");
            }
        }

        [Conditional("DEBUG")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void DebugMsg(this MelonLogger.Instance log, object obj) => DebugMsg(log, obj.ToString());
    }
}
