using UnityEngine;

namespace Tobii.Research.Unity
{
    public sealed class EyeTrackingSettings : ScriptableObject
    {
        private static EyeTrackingSettings _instance;
        private static bool _loadAttempted;

        [SerializeField, Tooltip("Use the mouse for gaze and calibration in every scene. Disable for Tobii hardware.")]
        private bool _useMouseSimulation;

        [SerializeField, Tooltip("Result of simulated calibration. Disable to test the failure prompt.")]
        private bool _simulatedCalibrationSucceeds = true;

        public static bool MouseSimulationEnabled
        {
            get { return Instance != null && Instance._useMouseSimulation; }
        }

        public static bool SimulatedCalibrationSucceeds
        {
            get { return Instance != null && Instance._simulatedCalibrationSucceeds; }
        }

        private static EyeTrackingSettings Instance
        {
            get
            {
                if (!_loadAttempted)
                {
                    _instance = Resources.Load<EyeTrackingSettings>("EyeTrackingSettings");
                    _loadAttempted = true;
                    if (_instance == null)
                        Debug.LogError("Missing Assets/Resources/EyeTrackingSettings.asset; mouse simulation is disabled.");
                }

                return _instance;
            }
        }
    }
}
