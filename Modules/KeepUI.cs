using NeonLite;
using NeonLite.Modules;
using System.Collections.Generic;
using UnityEngine;

namespace UltraPotato.Modules
{
    internal class KeepUI : IModule
    {
        const bool priority = true;
        static bool active = false;

        static readonly Dictionary<Texture2D, Texture2D> replacements = [];

        static void Setup()
        {
            //active = SuperPotato.Settings.enabled.SetupForModule(Activate, (_, after) => after);
        }

        static void Activate(bool activate)
        {
            Patching.TogglePatch(activate, typeof(PlayerUI), "Setup", UIStart, Patching.PatchTarget.Postfix);

            active = activate;
        }

        static void UIStart(PlayerUI __instance)
        {
            var matbuf = __instance.warningBeamLock.GetComponent<MeshRenderer>().material;
            matbuf.mainTexture = GetReplacement((Texture2D)matbuf.mainTexture);
            matbuf = __instance.warningLowHealth.GetComponent<MeshRenderer>().material;
            matbuf.mainTexture = GetReplacement((Texture2D)matbuf.mainTexture);
        }

        static Texture2D GetReplacement(Texture2D og)
        {
            if (replacements.ContainsKey(og))
                return replacements[og];

            og.requestedMipmapLevel = 0;

            var tmp = RenderTexture.GetTemporary(og.width, og.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(og, tmp);
            Texture2D newt = new(og.width, og.height, TextureFormat.ARGB32, false);

            var a = RenderTexture.active;
            RenderTexture.active = tmp;
            newt.ReadPixels(new(0, 0, og.width, og.height), 0, 0);
            newt.Apply();
            RenderTexture.active = a;

            RenderTexture.ReleaseTemporary(tmp);

            replacements.Add(og, newt);

            Texture.streamingTextureForceLoadAll = false;

            return newt;
        }
    }
}
