using NeonLite;
using NeonLite.Modules;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UltraPotato.Modules
{
    internal class CompleteRenderer : MonoBehaviour, IModule
    {
        static CompleteRenderer i;

        const bool priority = true;
        internal static bool active = true;

        internal static void _Setup()
        {
            active = QualityControl._renderScale.SetupForModule(Activate, (_, after) => after != 1 && QualityControl.active);
            SuperPotato.Settings.enabled.SetupForModule(Activate, (_, _) => CheckActive());

            if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGB111110Float))
                cameraSceneFormat = RenderTextureFormat.RGB111110Float;
            else
                cameraSceneFormat = RenderTextureFormat.ARGBFloat;
        }

        static bool lastActive;
        internal static bool CheckActive() => QualityControl.active && QualityControl._renderScale.Value != 1;


        internal static readonly FieldInfo camStack = Helpers.Field(typeof(UniversalAdditionalCameraData), "m_Cameras");
        static void Activate(bool activate)
        {
            if (lastActive == activate)
                return;
            lastActive = activate;

            if (i)
                i.enabled = activate;

            if (camera)
            {
                if (activate)
                    OnLevelLoad(Singleton<Game>.Instance?.GetCurrentLevel());
                else
                    camera.enabled = false;

                if (QualityControl.playerCam)
                {
                    var uacdP = QualityControl.playerCam.GetUniversalAdditionalCameraData();
                    var uacdS = camera.GetUniversalAdditionalCameraData();

                    if (!activate)
                    {
                        SetPlayerCamTex(null);
                        camStack.SetValue(uacdP, uacdS.cameraStack);
                    }
                }
            }

            var method = Helpers.Method(typeof(Camera), "WorldToScreenPoint", [typeof(Vector3), typeof(Camera.MonoOrStereoscopicEye)]);
            Patching.TogglePatch(activate, method, FixWTScreen, Patching.PatchTarget.Postfix);

            active = activate;
        }

        internal static void SetPlayerCamTex(RenderTexture tex)
        {
            SuperPotato.Log.DebugMsg($"SetPlayerCamTex {tex} {tex?.width} {tex?.height}");
            if (!QualityControl.playerCam || QualityControl.playerCam.activeTexture == tex)
                return;

            QualityControl.playerCam.forceIntoRenderTexture = tex;
            i.StartCoroutine(SetPlayerCamTexE(tex));
        }

        static IEnumerator SetPlayerCamTexE(RenderTexture tex)
        {
            var old = QualityControl.playerCam.targetTexture;
            while (QualityControl.playerCam.targetTexture != tex)
            {
                QualityControl.playerCam.targetTexture = tex;
                yield return null;
            }
            SuperPotato.Log.DebugMsg($"success");

            if (old)
                Destroy(old);
        }

        static void FetchCameraFormat(ref CameraData cameraData)
        {
            var add = cameraData.camera.GetUniversalAdditionalCameraData();
            if (!add || add.renderType != CameraRenderType.Base)
                return;

            cameraSceneFormat = cameraData.cameraTargetDescriptor.colorFormat;
            var method = Helpers.Method(typeof(UniversalRenderPipeline), "RenderSingleCamera", [typeof(ScriptableRenderContext), typeof(CameraData), typeof(bool)]);

            Patching.RemovePatch(method, FetchCameraFormat);
        }

        static void FixWTScreen(Camera __instance, ref Vector3 __result)
        {
            if (__instance == QualityControl.playerCam)
                __result.Scale(new(1 / QualityControl._renderScale.Value, 1 / QualityControl._renderScale.Value, 1));
        }

        void Awake()
        {
            i = this;
            enabled = active;
        }

        int lWidth;
        int lHeight;
        void Update()
        {
            if (Screen.width != lWidth || Screen.height != lHeight)
                SetupRenderTextures();

            lWidth = Screen.width;
            lHeight = Screen.height;
        }


        static void OnLevelLoad(LevelData level) => camera.enabled = level && level.type != LevelData.LevelType.Hub;

        internal static int rendererIndex;
        internal static void SetupCamera()
        {
            var uacdP = QualityControl.playerCam.GetUniversalAdditionalCameraData();
            var uacdS = camera.GetUniversalAdditionalCameraData();
            if (uacdP.cameraStack != null)
            {
                camStack.SetValue(uacdS, uacdP.cameraStack);
                uacdS.SetRenderer(rendererIndex);
            }

            SetupRenderTextures();
        }

        internal static void TrySetupRendererData(ScriptableRendererData data)
        {
            if (data is not ForwardRendererData frd)
                return;

            rendererData = ForwardRendererData.Instantiate(frd);

            var featuresF = Helpers.Field(typeof(ScriptableRendererData), "m_RendererFeatures");
            var features = (List<ScriptableRendererFeature>)featuresF.GetValue(rendererData);

            var completeFeature = ScriptableObject.CreateInstance<CompleteRendererFeature>();
            completeFeature.name = "CompleteRendererFeature";

            features.Clear();
            features.Add(completeFeature);
        }
        internal static ForwardRendererData rendererData;

        // lower quality render
        internal static RenderTextureFormat cameraSceneFormat = RenderTextureFormat.Depth;
        internal static RenderTexture cameraScene;
        // result render
        internal static RenderTexture screenScene;

        internal static Camera camera;

        internal static void SetupRenderTextures()
        {
            if (!camera)
                return;

            var w = camera.pixelWidth;
            var h = camera.pixelHeight;
            if (!screenScene || w != screenScene.width || h != screenScene.height)
            {
                SuperPotato.Log.Msg("making screenscene");

                if (screenScene)
                {
                    var old = screenScene;
                    screenScene = new(old)
                    {
                        width = w,
                        height = h
                    };
                    RenderTexture.Destroy(old);
                }
                else
                    screenScene = new(w, h, 0, RenderTextureFormat.ARGB32);

                screenScene.name = "ScreenScene";
                screenScene.Create();
            }

            if (cameraSceneFormat == RenderTextureFormat.Depth)
                return;

            w = (int)(camera.pixelWidth * QualityControl._renderScale.Value);
            h = (int)(camera.pixelHeight * QualityControl._renderScale.Value);
            if (!cameraScene || w != cameraScene.width || h != cameraScene.height)
            {
                SuperPotato.Log.Msg("making camscene");

                if (cameraScene)
                {
                    var old = cameraScene;
                    cameraScene = new(old)
                    {
                        width = w,
                        height = h,
                    };
                    //RenderTexture.Destroy(old);
                }
                else
                    cameraScene = new(w, h, 24, cameraSceneFormat);

                cameraScene.name = "CameraScene";
                cameraScene.Create();
            }

        }

        class CompleteRendererFeature : ScriptableRendererFeature
        {
            CompleteRendererPass pass;

            public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
            {
                if (pass == null)
                    Create();

                pass.renderer = renderer;
                renderer.EnqueuePass(pass);
            }

            public override void Create()
            {
                pass ??= new CompleteRendererPass();
                pass.renderPassEvent = RenderPassEvent.AfterRendering;
            }

            class CompleteRendererPass : ScriptableRenderPass
            {
                public ScriptableRenderer renderer;

                new readonly ProfilingSampler profilingSampler = new("Complete Renderer");

                public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
                {
                    ref var cameraData = ref renderingData.cameraData;

                    CommandBuffer cmd = CommandBufferPool.Get();
                    cmd.BeginSample(profilingSampler.name);
                    using (new ProfilingScope(cmd, profilingSampler))
                    {
                        if (!FSR.Module.active)
                            cmd.Blit(cameraScene, screenScene); // blit it ourselves

                        cmd.Blit(screenScene, renderer.cameraColorTarget);
                    }
                    cmd.EndSample(profilingSampler.name);

                    context.ExecuteCommandBuffer(cmd);
                    CommandBufferPool.Release(cmd);
                }
            }
        }

    }
}
