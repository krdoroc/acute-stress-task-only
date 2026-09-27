using UnityEngine;

namespace Tobii.Research.Unity
{
    /// <summary>
    /// Owns the single reversible pause used while calibrating. Unity Update methods
    /// continue at timeScale zero, so task input and scene flow must also consult
    /// InputBlocked and SceneTransitionsBlocked.
    /// </summary>
    public sealed class ExperimentPauseService : MonoBehaviour
    {
        public static ExperimentPauseService Instance { get; private set; }

        public static bool CalibrationIsActive
        {
            get { return Instance != null && Instance.CalibrationPauseActive; }
        }

        public static bool InputIsBlocked { get { return CalibrationIsActive; } }
        public static bool SceneTransitionsAreBlocked { get { return CalibrationIsActive; } }

        public bool CalibrationPauseActive { get; private set; }
        public bool InputBlocked { get { return CalibrationPauseActive; } }
        public bool SceneTransitionsBlocked { get { return CalibrationPauseActive; } }

        private float _timeScaleBeforeCalibration = 1f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        /// <summary>Pauses the task once. Returns false when calibration already owns the pause.</summary>
        public bool BeginCalibrationPause()
        {
            if (CalibrationPauseActive) return false;

            _timeScaleBeforeCalibration = Time.timeScale;
            CalibrationPauseActive = true;
            Time.timeScale = 0f;
            return true;
        }

        /// <summary>Releases calibration's pause and restores the exact preceding time scale.</summary>
        public void EndCalibrationPause()
        {
            if (!CalibrationPauseActive) return;

            CalibrationPauseActive = false;
            Time.timeScale = _timeScaleBeforeCalibration;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            EndCalibrationPause();
            Instance = null;
        }
    }
}
