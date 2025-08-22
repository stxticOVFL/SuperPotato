using NeonLite;
using NeonLite.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace UltraPotato.Modules
{
    internal class RealtimeReflects : IModule
    {
        const bool priority = true;
        static bool active = true;

        static readonly List<ReflectionProbe> probes = [];

        static void Setup()
        {
            active = QualityControl.realtimeReflectionProbes.SetupForModule(Activate, (_, after) => after);
            QualityControl._reflectionProbeMultiplier.OnEntryValueChanged.Subscribe((_, _) => ResetProbes());
            QualityControl._reflectionUpdate.OnEntryValueChanged.Subscribe((_, _) => ResetProbes());
            QualityControl._reflectionProbeMax.OnEntryValueChanged.Subscribe((_, _) => ResetProbes());
        }

        static void ResetProbes()
        {
            foreach (var probe in probes)
                probe.Reset();

            lastLevel = null;
            OnLevelLoad(Singleton<Game>.Instance.GetCurrentLevel());            
        }

        static void Activate(bool activate)
        {
            if (activate)
            {
                lastLevel = null;
                OnLevelLoad(Singleton<Game>.Instance.GetCurrentLevel());
            }
            else
            {
                foreach (var probe in probes)
                    probe.Reset();

                probes.Clear();
            }
            active = activate;
        }

        static LevelData lastLevel;
        static void OnLevelLoad(LevelData level)
        {
            if (lastLevel == level)
                return;
            lastLevel = level;

            if (!level && level.type == LevelData.LevelType.Hub)
                return;

            foreach (var probe in UnityEngine.Object.FindObjectsOfType<ReflectionProbe>())
            {
                probe.resolution = Math.Max((int)(probe.resolution * QualityControl._reflectionProbeMultiplier.Value), QualityControl._reflectionProbeMax.Value);
                probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.EveryFrame;
                probe.timeSlicingMode = QualityControl._reflectionUpdate.Value;
                probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
                probe.renderDynamicObjects = true;
            }
        }

    }
}
