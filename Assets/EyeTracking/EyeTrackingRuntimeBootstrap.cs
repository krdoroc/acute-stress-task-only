using UnityEngine;

namespace Tobii.Research.Unity
{
    /// <summary>Creates one eye-tracking and calibration runtime before the first scene.</summary>
    public static class EyeTrackingRuntimeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateRuntime()
        {
            EnsureRuntime();
        }

        /// <summary>
        /// Idempotent fallback used by GameManager as well as the Unity runtime hook.
        /// This guarantees the shortcut listener exists even when editor play-mode
        /// settings or an unusual scene entry point skip the normal bootstrap timing.
        /// </summary>
        public static void EnsureRuntime()
        {
            if (Calibration.Instance != null && ExperimentPauseService.Instance != null && EyeTracker.Instance != null)
                return;

            GameObject runtime = GameObject.Find("[EyeTrackingRuntime]");
            if (runtime == null) runtime = new GameObject("[EyeTrackingRuntime]");
            Object.DontDestroyOnLoad(runtime);
            if (ExperimentPauseService.Instance == null) runtime.AddComponent<ExperimentPauseService>();
            if (EyeTracker.Instance == null) runtime.AddComponent<EyeTracker>();
            if (Calibration.Instance == null) runtime.AddComponent<Calibration>();
            if (runtime.GetComponent<GazeColliderDebugOverlay>() == null)
                runtime.AddComponent<GazeColliderDebugOverlay>();
            Debug.Log("Eye-tracking runtime ready. Recalibrate with Ctrl+Shift+C.");
        }
    }
}
