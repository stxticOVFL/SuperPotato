using Beautify.Universal;
using MelonLoader;
using MelonLoader.Preferences;
using NeonLite;
using NeonLite.Modules;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UltraPotato.Modules.FSR
{
    internal enum Presets
    {
        Disabled = -1,
        UltraQuality = 0,
        Quality,
        Balanced,
        Performance
    }

    internal class Module : IModule
    {
        const bool priority = true;
        internal static bool active = true;

        internal static MelonPreferences_Entry<bool> preset;
        internal static MelonPreferences_Entry<float> sharpening;

        internal static void _Setup()
        {
            preset = NeonLite.Settings.Add(SuperPotato.Settings.h, "", "fsr1", "FSR 1", "Use AMD's FSR 1 to upscale at lower render scales.", default(bool));
            active = preset.SetupForModule(Activate, (_, _) => CheckActive());
            SuperPotato.Settings.enabled.SetupForModule(Activate, (_, _) => CheckActive());
            QualityControl._renderScale.SetupForModule(Activate, (_, _) => CheckActive());
            sharpening = NeonLite.Settings.Add(SuperPotato.Settings.h, "", "fsr1Sharp", "FSR 1 Sharpening", "Adjust the sharpening affected by the sharpening pass.", 0.9f, true, new ValueRange<float>(0, 1));

            //preset.OnEntryValueChanged.Subscribe((_, after) => SetRenderScale(after));

            Handling.LoadShaders();
        }

        static bool lastActive;
        internal static bool CheckActive() => CompleteRenderer.CheckActive() && preset.Value;

        static void SetRenderScale(Presets preset)
        {
            if (preset == Presets.Disabled)
                return;
            QualityControl._renderScale.Value = Handling.presetValues[(int)preset].m_RenderScale;
        }

        static void Activate(bool activate)
        {
            if (lastActive == activate)
                return;
            lastActive = activate;
            // this function used to be massive
            // there were so many patches here once
            // now it is left to just this
            if (!activate)
            {
                if (bSettings != null && bSettings.TryGetTarget(out var settings))
                {
                    settings.sharpenIntensity.value = preSharp;
                    settings.ditherIntensity.value = preDither;
                    settings.chromaticAberrationIntensity.value = preChAb;
                }
            }
            else
            {
                OnLevelLoad(Singleton<Game>.Instance.GetCurrentLevel());
            }

            active = activate;
        }

        static WeakReference<Beautify.Universal.Beautify> bSettings;
        static float preSharp;
        static float preDither;
        static float preChAb;
        internal static void OnLevelLoad(LevelData level)
        {
            if (!level || level.type == LevelData.LevelType.Hub)
                return;

            AddFeature(QualityControl.currentAsset);

            if (bSettings == null || !bSettings.TryGetTarget(out var target) && target != BeautifySettings.settings)
            {
                bSettings = new(BeautifySettings.settings);
                preSharp = BeautifySettings.settings.sharpenIntensity.value;
                preDither = BeautifySettings.settings.ditherIntensity.value;
                preChAb = BeautifySettings.settings.chromaticAberrationIntensity.value;

                BeautifySettings.settings.sharpenIntensity.value = 0;
                BeautifySettings.settings.ditherIntensity.value = 0;
                BeautifySettings.settings.chromaticAberrationIntensity.value = 0;
            }
        }

        internal static void AddFeature(UniversalRenderPipelineAsset asset)
        {
            SuperPotato.Log.DebugMsg("FSR AddFeature");
            if (!asset)
                return;

            asset.msaaSampleCount = 8;

            var renderer = asset.GetRenderer(1);
            var featuresF = Helpers.Field(typeof(ScriptableRenderer), "m_RendererFeatures");
            var features = (List<ScriptableRendererFeature>)featuresF.GetValue(renderer);

            if (!features.Contains(Handling.renderFeature))
                features.Add(Handling.renderFeature);
        }

        internal static IEnumerator AddFeatureWait(LevelData level)
        {
            yield return null;
            OnLevelLoad(level);
        }
    }

    readonly struct Settings(in float render_scale, in float mipmap_bias)
    {
        public readonly float m_RenderScale = render_scale;
        public readonly float m_MipmapBias = mipmap_bias;
    };


    static class Handling
    {
        static ComputeShader easuCS;
        static ComputeShader rcasCS;

        internal static void LoadShaders()
        {
            easuCS = SuperPotato.bundle.LoadAsset<ComputeShader>("Assets/Shaders/EdgeAdaptiveSpatialUpsampling.compute");
            rcasCS = SuperPotato.bundle.LoadAsset<ComputeShader>("Assets/Shaders/RobustContrastAdaptiveSharpen.compute");

            // i can't get half to work :(
            // my guess is just simply the combination of dx11 + unity 2020.3 does not support it
            // tried forcing it but i think only dx12 supported
            //easuCS.EnableKeyword("_AMD_FSR_HALF");
            //rcasCS.EnableKeyword("_AMD_FSR_HALF");

            renderFeature = ScriptableObject.CreateInstance<FSRRenderFeature>();
            renderFeature.name = "FSRRenderFeature";
        }

        internal static FSRRenderFeature renderFeature;

        internal class FSRRenderFeature : ScriptableRendererFeature
        {
            FSRRenderPass pass;

            public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
            {
                if (pass == null)
                    Create();

                if (Module.active)
                {
                    pass.renderer = renderer;
                    renderer.EnqueuePass(pass);
                }
            }

            public override void Create()
            {
                pass ??= new FSRRenderPass();
                pass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            }

            protected override void Dispose(bool disposing)
            {                    
                pass?.easuCB?.Release();
                pass?.rcasCB?.Release();
                pass = null;
            }

            internal class FSRRenderPass : ScriptableRenderPass
            {
                public ComputeBuffer easuCB;
                public ComputeBuffer rcasCB;

                RenderTextureDescriptor descriptor;

                new readonly ProfilingSampler profilingSampler = new("FSR Pass");

                public ScriptableRenderer renderer;

                public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
                {
                    descriptor = cameraTextureDescriptor;
                    descriptor.enableRandomWrite = true;
                    descriptor.msaaSamples = 1;
                    descriptor.depthBufferBits = 0;

                    easuCB ??= new(4, sizeof(uint) * 4);
                    rcasCB ??= new(4, sizeof(uint) * 4);
                }

                public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
                {
                    ref var cameraData = ref renderingData.cameraData;

                    CommandBuffer cmd = CommandBufferPool.Get();
                    cmd.BeginSample(profilingSampler.name);
                    using (new ProfilingScope(cmd, profilingSampler))
                    {
                        cmd.GetTemporaryRT(ShaderConstants._EASUInputTexture, descriptor);
                        cmd.Blit(renderer.cameraColorTarget, ShaderConstants._EASUInputTexture);

                        DoFSR(cmd, ref cameraData, CompleteRenderer.screenScene);
                    }
                    cmd.EndSample(profilingSampler.name);

                    context.ExecuteCommandBuffer(cmd);
                    CommandBufferPool.Release(cmd);
                }

                void EdgeAdaptiveSpatialUpsampling(CommandBuffer cmd, ref CameraData cameraData, bool needs_convert_to_srgb)
                {
                    //if (needs_convert_to_srgb)
                    //    easuCS.EnableKeyword("_AMD_FSR_NEEDS_CONVERT_TO_SRGB");
                    //else
                    //    easuCS.DisableKeyword("_AMD_FSR_NEEDS_CONVERT_TO_SRGB");

                    int viewCount = 1;
                    int kinitialize_idx = easuCS.FindKernel("KInitialize");
                    int kmain_idx = easuCS.FindKernel("KMain");
                    cmd.SetComputeTextureParam(easuCS, kmain_idx, ShaderConstants._EASUInputTexture, ShaderConstants._EASUInputTexture);
                    int srcWidth = descriptor.width;
                    int srcHeight = descriptor.height;
                    int dstWidth = CompleteRenderer.camera.pixelWidth;
                    int dstHeight = CompleteRenderer.camera.pixelHeight;
                    cmd.SetComputeVectorParam(easuCS, ShaderConstants._EASUViewportSize, new Vector4(srcWidth, srcHeight));
                    cmd.SetComputeVectorParam(easuCS, ShaderConstants._EASUInputImageSize, new Vector4(srcWidth, srcHeight));
                    cmd.GetTemporaryRT(ShaderConstants._EASUOutputTexture, GetUAVCompatibleDescriptor(ref descriptor, dstWidth, dstHeight));
                    cmd.SetComputeTextureParam(easuCS, kmain_idx, ShaderConstants._EASUOutputTexture, ShaderConstants._EASUOutputTexture);
                    cmd.SetComputeVectorParam(easuCS, ShaderConstants._EASUOutputSize, new Vector4(dstWidth, dstHeight, 1.0f / dstWidth, 1.0f / dstHeight));
                    cmd.SetComputeBufferParam(easuCS, kinitialize_idx, ShaderConstants._EASUParameters, easuCB);
                    cmd.SetComputeBufferParam(easuCS, kmain_idx, ShaderConstants._EASUParameters, easuCB);
                    cmd.DispatchCompute(easuCS, kinitialize_idx, 1, 1, 1);
                    static int DivRoundUp(int x, int y) => (x + y - 1) / y;
                    int dispatchX = DivRoundUp(dstWidth, 8);
                    int dispatchY = DivRoundUp(dstHeight, 8);

                    cmd.DispatchCompute(easuCS, kmain_idx, dispatchX, dispatchY, viewCount);
                }

                void RobustContrastAdaptiveSharpening(CommandBuffer cmd, ref CameraData cameraData, bool needs_convert_to_srgb)
                {
                    //if (needs_convert_to_srgb)
                    //    rcasCS.EnableKeyword("_AMD_FSR_NEEDS_CONVERT_TO_SRGB");
                    //else
                    //    rcasCS.DisableKeyword("_AMD_FSR_NEEDS_CONVERT_TO_SRGB");

                    int viewCount = 1;
                    int kinitialize_idx = rcasCS.FindKernel("KInitialize");
                    int kmain_idx = rcasCS.FindKernel("KMain");

                    cmd.SetComputeFloatParam(rcasCS, ShaderConstants._RCASScale, 2 - (Module.sharpening.Value * 2));
                    cmd.SetComputeTextureParam(rcasCS, kmain_idx, ShaderConstants._RCASInputTexture, ShaderConstants._EASUOutputTexture);
                    int dstWidth = CompleteRenderer.camera.pixelWidth;
                    int dstHeight = CompleteRenderer.camera.pixelHeight;
                    cmd.GetTemporaryRT(ShaderConstants._RCASOutputTexture, GetUAVCompatibleDescriptor(ref descriptor, dstWidth, dstHeight));
                    cmd.SetComputeTextureParam(rcasCS, kmain_idx, ShaderConstants._RCASOutputTexture, ShaderConstants._RCASOutputTexture);
                    cmd.SetComputeBufferParam(rcasCS, kinitialize_idx, ShaderConstants._RCASParameters, rcasCB);
                    cmd.SetComputeBufferParam(rcasCS, kmain_idx, ShaderConstants._RCASParameters, rcasCB);
                    cmd.DispatchCompute(rcasCS, kinitialize_idx, 1, 1, 1);

                    static int DivRoundUp(int x, int y) => (x + y - 1) / y;
                    int dispatchX = DivRoundUp(dstWidth, 8);
                    int dispatchY = DivRoundUp(dstHeight, 8);

                    cmd.DispatchCompute(rcasCS, kmain_idx, dispatchX, dispatchY, viewCount);
                }

                void DoFSR(CommandBuffer cmd, ref CameraData cameraData, RenderTargetIdentifier dst)
                {
                    //bool needs_convert_to_srgb = !(cameraData.isHdrEnabled || QualitySettings.activeColorSpace == ColorSpace.Gamma);
                    // hdr is ALWAYS enabled with neon white, optimize
                    const bool needs_convert_to_srgb = false;

                    EdgeAdaptiveSpatialUpsampling(cmd, ref cameraData, needs_convert_to_srgb);
                    RobustContrastAdaptiveSharpening(cmd, ref cameraData, needs_convert_to_srgb);

                    cmd.Blit(ShaderConstants._RCASOutputTexture, dst);
                }

            }

            static class ShaderConstants
            {
                // Edge Adaptive Spatial Upsampling
                public static readonly int _EASUInputTexture = Shader.PropertyToID("_EASUInputTexture");
                public static readonly int _EASUOutputTexture = Shader.PropertyToID("_EASUOutputTexture");
                public static readonly int _EASUViewportSize = Shader.PropertyToID("_EASUViewportSize");
                public static readonly int _EASUInputImageSize = Shader.PropertyToID("_EASUInputImageSize");
                public static readonly int _EASUOutputSize = Shader.PropertyToID("_EASUOutputSize");
                public static readonly int _EASUParameters = Shader.PropertyToID("_EASUParameters");

                // Robust Contrast Adaptive Sharpening
                public static readonly int _RCASInputTexture = Shader.PropertyToID("_RCASInputTexture");
                public static readonly int _RCASScale = Shader.PropertyToID("_RCASScale");
                public static readonly int _RCASParameters = Shader.PropertyToID("_RCASParameters");
                public static readonly int _RCASOutputTexture = Shader.PropertyToID("_RCASOutputTexture");
            }
        }

        internal static readonly Settings[] presetValues =
        [
            new(.77f, -.38f),
            new(.67f, -.58f),
            new(.59f, -.79f),
            new(.50f, -1.0f)
        ];

        static RenderTextureDescriptor GetUAVCompatibleDescriptor(ref RenderTextureDescriptor m_Descriptor, int width, int height)
        {
            var desc = m_Descriptor;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
            desc.width = width;
            desc.height = height;
            desc.enableRandomWrite = true;
            return desc;
        }
    }
}
