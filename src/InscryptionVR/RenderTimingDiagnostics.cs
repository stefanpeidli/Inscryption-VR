using System;
using System.Globalization;
using System.Runtime.InteropServices;
using BepInEx.Logging;
using UnityEngine;
using Valve.VR;

namespace InscryptionVR
{
    /// <summary>Read-only, low-frequency samples from the existing compositor session.</summary>
    public sealed class RenderTimingDiagnostics : MonoBehaviour
    {
        private ManualLogSource _logger;
        private StereoRenderer _renderer;
        private float _nextSample;
        private bool _failed;
        private bool _havePrevious;
        private Compositor_CumulativeStats _previous;
        private uint _frameSize;
        private uint _statsSize;

        public void Initialize(ManualLogSource logger, StereoRenderer renderer)
        {
            _logger = logger;
            _renderer = renderer;
            // Use Valve's generated Windows ABI layout. Never reuse Linux's packed structures.
            _frameSize = (uint)Marshal.SizeOf(typeof(Compositor_FrameTiming));
            _statsSize = (uint)Marshal.SizeOf(typeof(Compositor_CumulativeStats));
            // Header v2.12.14, default Windows pack=8, both x86 and x64.
            if (_frameSize != 192 || _statsSize != 112)
            {
                _failed = true;
                _logger.LogWarning("VR timing diagnostics disabled: unexpected ABI sizes " + _frameSize + "/" + _statsSize + ".");
                return;
            }
            _logger.LogInfo("VR timing diagnostics enabled: read-only samples every 5 seconds; sampled flags do not cover intervening frames.");
        }

        private void Update()
        {
            if (_failed || _logger == null || _renderer == null || Time.realtimeSinceStartup < _nextSample) return;
            Vector3 position;
            Quaternion rotation;
            float scale;
            if (!_renderer.TryGetHeadPose(out position, out rotation, out scale)) return;
            _nextSample = Time.realtimeSinceStartup + 5f;
            try
            {
                // The native renderer owns initialization, waits, tracking space and shutdown.
                CVRCompositor compositor = OpenVR.Compositor;
                if (compositor == null) return;
                var timing = new Compositor_FrameTiming();
                timing.m_nSize = _frameSize;
                bool available = compositor.GetFrameTiming(ref timing, 0);
                var stats = new Compositor_CumulativeStats();
                compositor.GetCumulativeStats(ref stats, _statsSize);
                uint processId = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
                bool ownStats = stats.m_nPid == processId;
                bool supported = compositor.IsMotionSmoothingSupported();
                bool smoothingEnabled = supported && compositor.IsMotionSmoothingEnabled();
                string sample = available ? string.Format(CultureInfo.InvariantCulture,
                    "lastFrame={0}, flags=0x{1:X}, presents={2}, dropped={3}, mispresented={4}, clientIntervalMs={5:F2}, renderGpuMs={6:F2}, submitMs={7:F2}, transferMs={8:F2}",
                    timing.m_nFrameIndex, timing.m_nReprojectionFlags, timing.m_nNumFramePresents,
                    timing.m_nNumDroppedFrames, timing.m_nNumMisPresented, timing.m_flClientFrameIntervalMs,
                    timing.m_flTotalRenderGpuMs, timing.m_flSubmitFrameMs, timing.m_flTransferLatencyMs)
                    : "lastFrame=unavailable";
                string cumulative = "cumulativePid=" + stats.m_nPid + ", ownProcess=" + ownStats;
                if (ownStats)
                {
                    cumulative += ", presents=" + stats.m_nNumFramePresents + ", submits=" + stats.m_nNumFrameSubmits +
                        ", dropped=" + stats.m_nNumDroppedFrames + ", reprojected=" + stats.m_nNumReprojectedFrames +
                        ", depthFrames=" + stats.m_nNumFramesWithDepth;
                    if (_havePrevious && _previous.m_nPid == stats.m_nPid &&
                        stats.m_nNumFramePresents >= _previous.m_nNumFramePresents &&
                        stats.m_nNumDroppedFrames >= _previous.m_nNumDroppedFrames &&
                        stats.m_nNumReprojectedFrames >= _previous.m_nNumReprojectedFrames)
                        cumulative += ", intervalPresents=" + (stats.m_nNumFramePresents - _previous.m_nNumFramePresents) +
                            ", intervalDropped=" + (stats.m_nNumDroppedFrames - _previous.m_nNumDroppedFrames) +
                            ", intervalReprojected=" + (stats.m_nNumReprojectedFrames - _previous.m_nNumReprojectedFrames);
                }
                _previous = stats;
                _havePrevious = ownStats;
                _logger.LogInfo("VR timing sample: " + sample + "; " + cumulative +
                    "; motionSmoothingSupported=" + supported + ", motionSmoothingEnabled=" + smoothingEnabled + ".");
            }
            catch (Exception exception)
            {
                _failed = true;
                _logger.LogWarning("VR timing diagnostics disabled after read failure: " + exception.Message);
            }
        }
    }
}
