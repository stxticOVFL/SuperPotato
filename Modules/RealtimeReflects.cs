using NeonLite;
using NeonLite.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace UltraPotato.Modules
{
    internal class RealtimeReflects : IModule
    {
        const bool priority = true;
        static bool active = true;

        class ReflectionProbeData(ReflectionProbe probe)
        {
            readonly int resolution = probe.resolution;
            readonly ReflectionProbeRefreshMode refreshMode = probe.refreshMode;
            readonly ReflectionProbeTimeSlicingMode timeSlicingMode = probe.timeSlicingMode;
            readonly ReflectionProbeMode mode = probe.mode;

            public ReflectionProbe probe = probe;
            public void Reset()
            {
                if (!probe)
                    return;
                probe.resolution = resolution;
                probe.refreshMode = refreshMode;
                probe.timeSlicingMode = timeSlicingMode;
                probe.mode = mode;
            }
        }

        static readonly List<ReflectionProbeData> probes = [];

        static void Setup()
        {
            active = QualityControl.realtimeReflectionProbes.SetupForModule(Activate, (_, after) => after);
            QualityControl._reflectionProbeMultiplier.OnEntryValueChanged.Subscribe((_, _) => ResetProbes(true));
            QualityControl._reflectionUpdate.OnEntryValueChanged.Subscribe((_, _) => ResetProbes(true));
            QualityControl._reflectionProbeMax.OnEntryValueChanged.Subscribe((_, _) => ResetProbes(true));
        }

        static void ResetProbes(bool oll)
        {
            foreach (var probe in probes)
                probe.Reset();
            probes.Clear();

            if (oll)
                OnLevelLoad(Singleton<Game>.Instance.GetCurrentLevel());
        }

        static void Activate(bool activate)
        {
            ResetProbes(activate);
            active = activate;
        }

        static void OnLevelLoad(LevelData level)
        {
            if (!level && level.type == LevelData.LevelType.Hub)
                return;

            probes.RemoveAll(x => !(bool)x.probe);
            foreach (var probe in UnityEngine.Object.FindObjectsOfType<ReflectionProbe>())
            {
                if (probes.Any(x => x.probe == probe))                        
                    continue;
                probes.Add(new(probe));
                probe.resolution = Math.Max((int)(probe.resolution * QualityControl._reflectionProbeMultiplier.Value), QualityControl._reflectionProbeMax.Value);
                probe.refreshMode = QualityControl._actuallyRealtime.Value ? ReflectionProbeRefreshMode.EveryFrame : ReflectionProbeRefreshMode.ViaScripting;
                probe.timeSlicingMode = QualityControl._actuallyRealtime.Value ? QualityControl._reflectionUpdate.Value : ReflectionProbeTimeSlicingMode.NoTimeSlicing;
                probe.mode = ReflectionProbeMode.Realtime;
                if (!QualityControl._actuallyRealtime.Value)
                    probe.RenderProbe();
            }
        }

    }
}
