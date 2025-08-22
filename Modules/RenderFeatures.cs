using HarmonyLib;
using NeonLite;
using NeonLite.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UltraPotato.Modules
{
    internal class SimplerWater : IModule
    {
        const bool priority = true;
        static bool active = true;

        static void Setup()
        {
            active = QualityControl._simplerWater.SetupForModule(Activate, (_, after) => after);
        }

        static readonly Type passT = typeof(PlanarReflectionRenderFeature).GetNestedType("PlanarReflectionRenderPass", AccessTools.all);
        static void Activate(bool activate)
        {
            Patching.TogglePatch(activate, passT, "DrawRenderers", SkipRenderers, Patching.PatchTarget.Prefix);
            active = activate;
        }
        static bool SkipRenderers() => false;
    }

    internal class DisableAmplify : IModule
    {
        const bool priority = true;
        static bool active = true;

        static void Setup()
        {
            active = QualityControl._amplifyOcclusion.SetupForModule(Activate, (_, after) => !after);
        }

        static readonly Type passT = typeof(AmplifyOcclusionRendererFeature.AmplifyOcclusionPass);
        static void Activate(bool activate)
        {
            Patching.TogglePatch(activate, passT, "Execute", SkipRenderers, Patching.PatchTarget.Prefix);
            active = activate;
        }
        static bool SkipRenderers() => false;
    }

}
