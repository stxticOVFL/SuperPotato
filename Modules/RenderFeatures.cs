using HarmonyLib;
using NeonLite;
using NeonLite.Modules;
using System;

namespace UltraPotato.Modules
{
    internal class SimplerWater : IModule
    {
        const bool priority = true;
        static bool active = true;

        internal static void _Setup()
        {
            active = QualityControl._simplerWater.SetupForModule(Activate, (_, _) => CheckActive());
            SuperPotato.Settings.enabled.SetupForModule(Activate, (_, _) => CheckActive());
        }

        static bool lastActive;
        internal static bool CheckActive() => QualityControl.active && QualityControl._simplerWater.Value;


        static readonly Type passT = typeof(PlanarReflectionRenderFeature).GetNestedType("PlanarReflectionRenderPass", AccessTools.all);
        static void Activate(bool activate)
        {
            if (lastActive == activate)
                return;
            lastActive = activate;

            Patching.TogglePatch(activate, passT, "DrawRenderers", SkipRenderers, Patching.PatchTarget.Prefix);
            active = activate;
        }
        
        static bool SkipRenderers() => false;
    }

    internal class DisableAmplify : IModule
    {
        const bool priority = true;
        static bool active = true;

        internal static void _Setup()
        {
            active = QualityControl._amplifyOcclusion.SetupForModule(Activate, (_, _) => CheckActive());
            SuperPotato.Settings.enabled.SetupForModule(Activate, (_, _) => CheckActive());
        }

        static bool lastActive;
        internal static bool CheckActive() => QualityControl.active && !QualityControl._amplifyOcclusion.Value;

        static readonly Type passT = typeof(AmplifyOcclusionRendererFeature.AmplifyOcclusionPass);
        static void Activate(bool activate)
        {
            if (lastActive == activate)
                return;
            lastActive = activate;

            Patching.TogglePatch(activate, passT, "Execute", SkipRenderers, Patching.PatchTarget.Prefix);
            active = activate;
        }
        static bool SkipRenderers() => false;
    }

}
