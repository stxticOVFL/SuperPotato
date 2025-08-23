using HarmonyLib;
using MelonLoader;
using NeonLite;
using NeonLite.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using URPA = UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;

namespace UltraPotato.Modules
{
    internal class QualityControl : IModule
    {
        const bool priority = false;
        static bool active = true;

        internal enum Presets
        {
            Minimum,
            Low,
            Medium,
            High,
            SlowReflects,
            Maximum,
            Custom
        }

        internal static MelonPreferences_Entry<Presets> _activePreset;
        //internal static MelonPreferences_Entry<float> _renderScale;

        internal static MelonPreferences_Entry<AnisotropicFiltering> anisotropicFiltering;
        internal static MelonPreferences_Entry<float> lodBias;
        internal static MelonPreferences_Entry<int> masterTextureLimit;
        internal static MelonPreferences_Entry<int> maximumLODLevel;
        internal static MelonPreferences_Entry<int> maxQueuedFrames;
        internal static MelonPreferences_Entry<int> particleRaycastBudget;
        internal static MelonPreferences_Entry<int> pixelLightCount;
        internal static MelonPreferences_Entry<bool> realtimeReflectionProbes;
        internal static MelonPreferences_Entry<ReflectionProbeTimeSlicingMode> _reflectionUpdate;
        internal static MelonPreferences_Entry<float> _reflectionProbeMultiplier;
        internal static MelonPreferences_Entry<int> _reflectionProbeMax;
        internal static MelonPreferences_Entry<bool> _simplerWater;
        internal static MelonPreferences_Entry<ShadowQuality> shadows;
        internal static MelonPreferences_Entry<ShadowResolution> shadowResolution;   
        internal static MelonPreferences_Entry<int> shadowDistance;
        internal static MelonPreferences_Entry<int> shadowCascades;
        internal static MelonPreferences_Entry<SkinWeights> skinWeights;
        internal static MelonPreferences_Entry<bool> softParticles;
        //internal static MelonPreferences_Entry<bool> softVegetation;
        internal static MelonPreferences_Entry<bool> streamingMipmapsActive;
        internal static MelonPreferences_Entry<int> streamingMipmapsMaxLevelReduction;
        internal static MelonPreferences_Entry<float> streamingMipmapsMemoryBudget;
        internal static MelonPreferences_Entry<bool> _amplifyOcclusion;

        static readonly List<MelonPreferences_Entry> settings = [];

        const string PRESET_DESC = """
            The preset to use. Enable advanced settings to finetune controls.

            Minimum: As low as I could possibly make it without the game looking entirely unrecognizable.
            Low: Loses more detail than Medium for slightly better performance, with a slightly lower memory budget.
            Medium: The default. Lowers on VRAM without really being all too noticable in motion.
            High: Matches how Neon White *normally* looks. Here to use as a base for customization.
            SlowReflects: High, but with reflections that update every 9 frames.
            Maximum: Cranks up the display to as high as it would let me. No LODs, super high-quality realtime reflections.
            **ONLY USE THIS IF YOUR PC IS ABSURDLY BEEFY!!!!!**
            """;

        static void Setup()
        {
            active = SuperPotato.Settings.enabled.SetupForModule(Activate, (_, after) => after);
            //SetQuality();

            _activePreset = Settings.Add(SuperPotato.Settings.h, "", "preset", "Preset", PRESET_DESC.Trim(), Presets.Medium);
            _activePreset.OnEntryValueChanged.Subscribe((_, after) => SetPreset(after));

            //_renderScale = Settings.Add(UltraPotato.Settings.h, "", "_renderScale", "Render Scale", null, 1f, new ValueRange<float>(0.5f, 2f));
            anisotropicFiltering = Settings.Add(SuperPotato.Settings.h, "", "anisotropicFiltering", "Anisotropic Filtering", null, default(AnisotropicFiltering), true);
            lodBias = Settings.Add(SuperPotato.Settings.h, "", "lodBias", "LOD Bias", "The value to multiply for checking LOD distance.", default(float), true);
            masterTextureLimit = Settings.Add(SuperPotato.Settings.h, "", "masterTextureLimit", "Skip Mipmap", "How many mips to skip for textures.\nHigher values are lower quality.", default(int), true);
            maximumLODLevel = Settings.Add(SuperPotato.Settings.h, "", "maximumLODLevel", "Max LOD Level", "The maximum Level of Detail objects can have.\nLower values are lower quality.", default(int), true);
            maxQueuedFrames = Settings.Add(SuperPotato.Settings.h, "", "maxQueuedFrames", "Max Queued Frames", "How many frames to queue to the GPU.", default(int), true);
            particleRaycastBudget = Settings.Add(SuperPotato.Settings.h, "", "particleRaycastBudget", "Particle Raycast Budget", "How many times particles can raycast per frame.", default(int), true);
            pixelLightCount = Settings.Add(SuperPotato.Settings.h, "", "pixelLightCount", "Pixel Light Count", "How many pixel lights are allowed.\nUsed in the title, Heaven's Edge, TTT, and maybe other spots.", default(int), true);
            realtimeReflectionProbes = Settings.Add(SuperPotato.Settings.h, "", "realtimeReflectionProbes", "Realtime Reflection Probes", "Enabling this setting can cause lag!!\nMakes all reflection probes reflect in *realtime.*", default(bool), true);
            _reflectionProbeMultiplier = Settings.Add(SuperPotato.Settings.h, "", "_reflectionProbeMultiplier", "Reflection Probe Multiplier", "When realtime reflection probes are on, how much to multiply each probe's resolution by.", default(float), true);
            _reflectionProbeMax = Settings.Add(SuperPotato.Settings.h, "", "_reflectionProbeMax", "Reflection Probe Max", "The maxiumum realtime probe resolution.", default(int), true);
            _reflectionUpdate = Settings.Add(SuperPotato.Settings.h, "", "_reflectionUpdate", "Reflection Time Slicing", "AllFacesAtOnce: update every 9 frames\nIndividualFaces: update each face of the reflection individually (14 frames)\nNoTimeSlicing: update *every* frame", default(ReflectionProbeTimeSlicingMode), true);
            _simplerWater = Settings.Add(SuperPotato.Settings.h, "", "_simplerWater", "Simpler Water", "Makes the water only reflect the skybox.", default(bool), true);
            shadows = Settings.Add(SuperPotato.Settings.h, "", "shadows", "Shadows", "Only affects (some) realtime shadows.", default(ShadowQuality), true);
            shadowResolution = Settings.Add(SuperPotato.Settings.h, "", "shadowResolution", "Shadow Resolution", "Higher means sharper.", default(ShadowResolution), true);
            shadowDistance = Settings.Add(SuperPotato.Settings.h, "", "shadowDistance", "Shadow Distance", "The drawing distance for shadows.", default(int), true);
            shadowCascades = Settings.Add(SuperPotato.Settings.h, "", "shadowCascades", "Shadow Cascades", "How many \"cascades\" or splits to use for shadow quality.", default(int), true);
            skinWeights = Settings.Add(SuperPotato.Settings.h, "", "skinWeights", "Skin Weights", "How many weights to use for model animations.", default(SkinWeights), true);
            softParticles = Settings.Add(SuperPotato.Settings.h, "", "softParticles", "Soft Particles", "Whether or not to render soft particles.", default(bool), true);
            //softVegetation = Settings.Add(SuperPotato.Settings.h, "", "softVegetation", "Soft Vegetation", "i don't know what this does for NW", default(bool), true);
            streamingMipmapsActive = Settings.Add(SuperPotato.Settings.h, "", "streamingMipmapsActive", "Streaming Mipmaps", "Use streaming mipmaps.\n*The* setting for lowering texture quality and VRAM utilization.", default(bool), true);
            streamingMipmapsMaxLevelReduction = Settings.Add(SuperPotato.Settings.h, "", "streamingMipmapsMaxLevelReduction", "Minimum Mipmap", "When streaming mips is active, keep this as the minimum.", default(int), true);
            streamingMipmapsMemoryBudget = Settings.Add(SuperPotato.Settings.h, "", "streamingMipmapsMemoryBudget", "Mipmap Memory Budget", "Lower means less VRAM used, and lower quality textures.", default(float), true);
            _amplifyOcclusion = Settings.Add(SuperPotato.Settings.h, "", "_amplifyOcclusion", "Amplify Occlusion", "Whether or not to enable the additional AO effect from Amplify Occlusion.", default(bool), true);

            // populate settings
            foreach (var entry in typeof(QualityControl).GetRuntimeFields()
                .Select(x => x.GetValue(null) as MelonPreferences_Entry).Where(x => x != null))
            {
                if (entry == _activePreset)
                    continue;

                entry.OnEntryValueChangedUntyped.Subscribe(static (_, _) => SetToCustom());
                settings.Add(entry);
            }

            SetPreset(_activePreset.Value, false);
        }


        static void Activate(bool activate)
        {
            // fuck you chuli from the past
            //var sdc = AccessTools.Constructor(typeof(ScoreData));
            //Patching.TogglePatch(activate, sdc, RegenPFPNoMips, Patching.PatchTarget.Prefix);

            Patching.TogglePatch(activate, typeof(LeaderboardScore), "SetScore", RegenPFPNoMipsLBS, Patching.PatchTarget.Prefix);
            Patching.TogglePatch(activate, typeof(GameDataManager), "ApplyShadowPrefs", SetQualityValues, Patching.PatchTarget.Postfix);

            active = activate;

            SetQuality();
            if (!activate)
                GameDataManager.ApplyShadowPrefs();
            else
                SetQualityValues();
        }

        static void RegenPFPNoMips(ref Texture2D profilePicture)
        {
            if (profilePicture == null)
                return;
            Texture2D c = new(profilePicture.width, profilePicture.height, profilePicture.format, false);
            c.SetPixels32(profilePicture.GetPixels32());
            c.Apply();
            profilePicture = c;
        }

        static void RegenPFPNoMipsLBS(ref ScoreData newData) => RegenPFPNoMips(ref newData._profilePicture);

        internal static void SetQuality(bool? set = null)
        {
            if (!set.HasValue)
                set = active;

            QualitySettings.SetQualityLevel(set.Value ? 1 : 0, true);
        }

        static readonly FieldInfo shadowRes = Helpers.Field(typeof(URPA), "m_MainLightShadowmapResolution");

        static URPA currentAsset;

        internal static void SetQualityValues()
        {
            if (!active)
                return;

            foreach (var e in settings)
            {
                if (e.Identifier.StartsWith("_"))
                    continue;

                var field = Helpers.Field(typeof(QualitySettings), e.Identifier);
                if (field != null) { 
                    field.SetValue(null, e.BoxedValue);
                }
                else
                {
                    var setter = AccessTools.PropertySetter(typeof(QualitySettings), e.Identifier);
                    setter?.Invoke(null, [e.BoxedValue]);
                }
            }

            if (currentAsset)
                URPA.Destroy(currentAsset);
            currentAsset = URPA.Instantiate((URPA)QualitySettings.renderPipeline);
            currentAsset.name = "SuperPotato Pipeline";
            currentAsset.shadowCascadeCount = shadowCascades.Value;
            currentAsset.shadowDistance = shadowDistance.Value;
            currentAsset.maxAdditionalLightsCount = pixelLightCount.Value;
            QualitySettings.renderPipeline = currentAsset;
            shadowRes.SetValue(currentAsset, (int)Math.Pow(2, 9 + (int)shadowResolution.Value));
        }

        static bool settingPreset = false;
        static void SetToCustom()
        {
            if (settingPreset)
                return;
            _activePreset.Value = Presets.Custom;
            SetQualityValues();
        }
        static void SetPreset(Presets preset, bool set = true)
        {
            settingPreset = true;

            switch (preset)
            {
                case Presets.SlowReflects:
                    _reflectionProbeMultiplier.Value = 4;
                    _reflectionUpdate.Value = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
                    _reflectionProbeMax.Value = 1024;
                    goto case Presets.High;
                case Presets.High:
                    //_renderScale.Value = 1f;
                    anisotropicFiltering.Value = AnisotropicFiltering.ForceEnable;
                    lodBias.Value = 2;
                    masterTextureLimit.Value = 0;
                    maximumLODLevel.Value = 0;
                    maxQueuedFrames.Value = 2;
                    particleRaycastBudget.Value = 4096;
                    pixelLightCount.Value = 3;
                    realtimeReflectionProbes.Value = preset != Presets.High;
                    shadows.Value = ShadowQuality.HardOnly;
                    shadowCascades.Value = 4;
                    shadowResolution.Value = ShadowResolution.VeryHigh;
                    shadowDistance.Value = 400;
                    skinWeights.Value = SkinWeights.FourBones;
                    softParticles.Value = true;
                    //softVegetation.Value = true;
                    streamingMipmapsActive.Value = false;
                    _simplerWater.Value = false;
                    _amplifyOcclusion.Value = true;

                    break;
                case Presets.Medium:
                    //_renderScale.Value = 1f;
                    anisotropicFiltering.Value = AnisotropicFiltering.ForceEnable;
                    lodBias.Value = 1.5f;
                    masterTextureLimit.Value = 1;
                    maximumLODLevel.Value = 0;
                    maxQueuedFrames.Value = 2;
                    particleRaycastBudget.Value = 4096;
                    pixelLightCount.Value = 2;
                    realtimeReflectionProbes.Value = false;
                    shadows.Value = ShadowQuality.HardOnly;
                    shadowCascades.Value = 4;
                    shadowResolution.Value = ShadowResolution.High;
                    shadowDistance.Value = 200;
                    skinWeights.Value = SkinWeights.TwoBones;
                    softParticles.Value = false;
                    //softVegetation.Value = true;
                    streamingMipmapsActive.Value = true;
                    streamingMipmapsMaxLevelReduction.Value = 1;
                    streamingMipmapsMemoryBudget.Value = 1024;
                    _simplerWater.Value = false;
                    _amplifyOcclusion.Value = true;

                    break;
                case Presets.Low:
                    //_renderScale.Value = 1f;
                    anisotropicFiltering.Value = AnisotropicFiltering.ForceEnable;
                    lodBias.Value = 1;
                    masterTextureLimit.Value = 2;
                    maximumLODLevel.Value = 0;
                    maxQueuedFrames.Value = 2;
                    particleRaycastBudget.Value = 2048;
                    pixelLightCount.Value = 0;
                    realtimeReflectionProbes.Value = false;
                    shadows.Value = ShadowQuality.HardOnly;
                    shadowCascades.Value = 4;
                    shadowResolution.Value = ShadowResolution.Medium;
                    shadowDistance.Value = 100;
                    skinWeights.Value = SkinWeights.TwoBones;
                    softParticles.Value = false;
                    //softVegetation.Value = false;
                    streamingMipmapsActive.Value = true;
                    streamingMipmapsMaxLevelReduction.Value = 3;
                    streamingMipmapsMemoryBudget.Value = 512;
                    _simplerWater.Value = true;
                    _amplifyOcclusion.Value = true;

                    break;
                case Presets.Minimum:
                    //_renderScale.Value = 0.9f;
                    anisotropicFiltering.Value = AnisotropicFiltering.Disable;
                    lodBias.Value = 0.1f;
                    masterTextureLimit.Value = 3;
                    maximumLODLevel.Value = 0;
                    maxQueuedFrames.Value = 2;
                    particleRaycastBudget.Value = 1024;
                    pixelLightCount.Value = 0;
                    realtimeReflectionProbes.Value = false;
                    shadows.Value = ShadowQuality.Disable;
                    shadowCascades.Value = 1;
                    shadowDistance.Value = 0;
                    skinWeights.Value = SkinWeights.OneBone;
                    softParticles.Value = false;
                    //softVegetation.Value = false;
                    streamingMipmapsActive.Value = true;
                    streamingMipmapsMaxLevelReduction.Value = 4;
                    streamingMipmapsMemoryBudget.Value = 256;
                    _simplerWater.Value = true;
                    _amplifyOcclusion.Value = false;

                    break;
                case Presets.Maximum:
                    //_renderScale.Value = 2f;
                    anisotropicFiltering.Value = AnisotropicFiltering.ForceEnable;
                    lodBias.Value = 100;
                    masterTextureLimit.Value = 0;
                    maximumLODLevel.Value = 0;
                    maxQueuedFrames.Value = 2;
                    particleRaycastBudget.Value = 8192;
                    pixelLightCount.Value = 100;
                    realtimeReflectionProbes.Value = true;
                    _reflectionProbeMultiplier.Value = 32;
                    _reflectionProbeMax.Value = 2048;
                    _reflectionUpdate.Value = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
                    shadows.Value = ShadowQuality.HardOnly;
                    shadowCascades.Value = 4;
                    shadowResolution.Value = ShadowResolution.VeryHigh;
                    shadowDistance.Value = 600;
                    skinWeights.Value = SkinWeights.Unlimited;
                    softParticles.Value = true;
                    //softVegetation.Value = true;
                    streamingMipmapsActive.Value = false;
                    _simplerWater.Value = false;
                    _amplifyOcclusion.Value = true;

                    break;
            }

            if (set)
                SetQualityValues();
            settingPreset = false;
        }
    }
}
