//-----------------------------------------------------------------------
// Copyright © 2019 Tobii Pro AB. All rights reserved.
//-----------------------------------------------------------------------

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Tobii.Research.Unity
{
    public class Calibration : MonoBehaviour
    {
        /// <summary>
        /// Instance of <see cref="Calibration"/> for easy access.
        /// Assigned in Awake() so use earliest in Start().
        /// </summary>
        public static Calibration Instance { get; private set; }

        /// <summary>
        /// Flag indicating if the latest calibration was successful
        /// or not, true/false.
        /// </summary>
        public bool LatestCalibrationSuccessful { get; private set; }

        /// <summary>
        /// Is calibration in progress?
        /// </summary>
        public bool CalibrationInProgress { get { return _calibrationInProgress; } }

        /// <summary>True while calibration or its failure decision has paused the task.</summary>
        public bool TaskPausedForCalibration { get { return ExperimentPauseService.CalibrationIsActive; } }

        /// <summary>
        /// Calibration points.
        /// Example:
        /// (0.2f, 0.2f)
        /// (0.8f, 0.2f)
        /// (0.2f, 0.8f)
        /// (0.8f, 0.8f)
        /// (0.5f, 0.5f)
        /// </summary>
        [SerializeField]
        [Tooltip("Calibration points.")]
        private Vector2[] _points;

        [SerializeField]
        private Image _calibrationPoint;

        [SerializeField]
        private Canvas _canvas;

        [SerializeField]
        private Image _panel;

        private CalibrationPoint _pointScript;

        // Handle blocking calls to calibration in a separate thread.
        private CalibrationThread _calibrationThread;
        private bool _calibrationInProgress;

        private bool ShowCalibrationPanel
        {
            get
            {
                return _showCalibrationPanel;
            }

            set
            {
                _showCalibrationPanel = value;
                if (_pointScript != null) _pointScript.gameObject.SetActive(_showCalibrationPanel);
                if (_canvas != null) _canvas.gameObject.SetActive(_showCalibrationPanel);
                if (_panel != null) _panel.color = _showCalibrationPanel ? Color.black : new Color(0, 0, 0, 0);
            }
        }

        private bool _showCalibrationPanel;
        private bool _showFailurePrompt;
        private bool _ownsCalibrationPause;
        private bool _duplicateInstance;
        private Vector2 _currentCalibrationPoint = new Vector2(0.5f, 0.5f);
        private Texture2D _pointTexture;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                _duplicateInstance = true;
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (_points == null || _points.Length == 0)
            {
                _points = new[]
                {
                    new Vector2(0.1f, 0.1f), new Vector2(0.5f, 0.1f), new Vector2(0.9f, 0.1f),
                    new Vector2(0.1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.9f, 0.5f),
                    new Vector2(0.1f, 0.9f), new Vector2(0.5f, 0.9f), new Vector2(0.9f, 0.9f)
                };
            }
        }

        private void Start()
        {
            if (_duplicateInstance) return;
            if (_calibrationPoint != null) _pointScript = _calibrationPoint.GetComponent<CalibrationPoint>();
            ShowCalibrationPanel = false;
        }

        /// <summary>
        /// Start a calibration. Either provide a set of calibration points,
        /// or null for default. The result callback provides a true or false
        /// answer to the success of the calibration.
        /// </summary>
        /// <param name="points">An array of calibration points, or null for default.</param>
        /// <param name="resultCallback">A result callback or null for none.</param>
        /// <returns>True if calibration was not already started, false otherwise.</returns>
        public bool StartCalibration(Vector2[] points = null, System.Action<bool> resultCallback = null)
        {
            if (_calibrationInProgress)
            {
                Debug.Log("Already performing calibration");
                Debug.Log("resultCallback is " + resultCallback);
                //GameManager.errorInScene("Calibration Unsuccessful");
                return false;
            }

            PauseTask();
            if (EyeTrackingSettings.MouseSimulationEnabled)
            {
                _calibrationInProgress = true;
                StartCoroutine(PerformSimulatedCalibration(points, resultCallback));
                return true;
            }
            if (EyeTracker.Instance == null || EyeTracker.Instance.EyeTrackerInterface == null)
            {
                LatestCalibrationSuccessful = false;
                _showFailurePrompt = true;
                LogCalibrationEvent("Calibration failed - no eye tracker");
                if (resultCallback != null) resultCallback(false);
                Debug.LogError("Calibration cannot start: no connected eye tracker.");
                return false;
            }
            _calibrationInProgress = true;
            StartCoroutine(PerformCalibration(points, resultCallback));
            return true;
        }

        /// <summary>
        /// Runs the visible calibration sequence without contacting Tobii hardware.
        /// The result comes from the shared eye-tracking settings and is logged as simulated.
        /// </summary>
        private IEnumerator PerformSimulatedCalibration(Vector2[] points, System.Action<bool> resultCallback)
        {
            if (points != null) _points = points;
            ShowCalibrationPanel = true;
            foreach (Vector2 pointPosition in _points)
            {
                ShowPoint(pointPosition);
                yield return new WaitForSecondsRealtime(1f);
            }

            ShowCalibrationPanel = false;
            LatestCalibrationSuccessful = EyeTrackingSettings.SimulatedCalibrationSucceeds;
            _calibrationInProgress = false;
            LogCalibrationEvent(LatestCalibrationSuccessful
                ? "Simulated calibration successful" : "Simulated calibration failed");
            if (LatestCalibrationSuccessful) RestoreTask();
            else _showFailurePrompt = true;
            if (resultCallback != null) resultCallback(LatestCalibrationSuccessful);
        }

        /// <summary>
        /// Wait for the <see cref="CalibrationThread.MethodResult"/> to be ready.
        /// </summary>
        /// <param name="result">The method result</param>
        /// <returns>An enumerator</returns>
        private IEnumerator WaitForResult(CalibrationThread.MethodResult result)
        {
            // Wait for the thread to finish the blocking call.
            while (!result.Ready)
            {
                yield return new WaitForSecondsRealtime(0.02f);
            }

            Debug.Log(result);
        }

        /// <summary>
        /// Calibration coroutine. Drives the calibration thread states.
        /// </summary>
        /// <param name="points">Optional point list. Null means default set.</param>
        /// <param name="resultCallback">A result callback or null for none.</param>
        /// <returns>An enumerator</returns>
        private IEnumerator PerformCalibration(Vector2[] points, System.Action<bool> resultCallback)
        {
            if (points != null)
            {
                _points = points;
            }

            if (_calibrationThread != null)
            {
                _calibrationThread.StopThread();
                _calibrationThread = null;
            }

            // Create and start the calibration thread.
            _calibrationThread = new CalibrationThread(EyeTracker.Instance.EyeTrackerInterface, screenBased: true);

            // Only continue if the calibration thread is running.
            for (int i = 0; i < 10; i++)
            {
                if (_calibrationThread.Running)
                {
                    break;
                }

                yield return new WaitForSecondsRealtime(0.1f);
            }

            if (!_calibrationThread.Running)
            {
                Debug.LogError("Failed to start calibration thread");
                _calibrationThread.StopThread();
                _calibrationThread = null;
                _calibrationInProgress = false;
                ShowCalibrationPanel = false;
                LatestCalibrationSuccessful = false;
                if (resultCallback != null) resultCallback(false);
                _showFailurePrompt = true;
                yield break;
            }

            ShowCalibrationPanel = true;

            var enterResult = _calibrationThread.EnterCalibrationMode();
            bool calibrationStepFailed = false;

            // Wait for the call to finish
            yield return StartCoroutine(WaitForResult(enterResult));
            calibrationStepFailed = enterResult.Status == CalibrationStatus.Failure;

            // Iterate through the calibration points.
            foreach (var pointPosition in _points)
            {
                // Set the local position and start the point animation
                ShowPoint(pointPosition);

                // Wait for animation.
                yield return new WaitForSecondsRealtime(1f);

                // As of this writing, adding a point takes about 175 ms. A failing add can take up to 3000 ms.
                var collectResult = _calibrationThread.CollectData(new CalibrationThread.Point(pointPosition));

                // Wait for the call to finish
                yield return StartCoroutine(WaitForResult(collectResult));

                // React to the result of adding a point.
                if (collectResult.Status == CalibrationStatus.Failure)
                {
                    calibrationStepFailed = true;
                    Debug.Log("There was an error gathering data for this calibration point: " + pointPosition);
                }
            }

            // Compute and apply the result of the calibration. A succesful compute currently takes about 300 ms. A failure may bail out in a few ms.
            var computeResult = _calibrationThread.ComputeAndApply();

            // Wait for the call to finish
            yield return StartCoroutine(WaitForResult(computeResult));

            // Leave calibration mode.
            var leaveResult = _calibrationThread.LeaveCalibrationMode();

            // Wait for the call to finish
            yield return StartCoroutine(WaitForResult(leaveResult));
            calibrationStepFailed = calibrationStepFailed || leaveResult.Status == CalibrationStatus.Failure;

            // Stop the thread.
            _calibrationThread.StopThread();
            _calibrationThread = null;

            // Finish up or restart if failure.
            LatestCalibrationSuccessful = !calibrationStepFailed && computeResult.Status == CalibrationStatus.Success;

            ShowCalibrationPanel = false;

            if (resultCallback != null) resultCallback(LatestCalibrationSuccessful);
            _calibrationInProgress = false;
            if (LatestCalibrationSuccessful)
            {
                LogCalibrationEvent("Calibration successful");
                RestoreTask();
            }
            else
            {
                LogCalibrationEvent("Calibration failed");
                _showFailurePrompt = true;
            }
        }

        /// <summary>
        /// This function is called when the behaviour becomes disabled() or inactive.
        /// </summary>
        private void OnDisable()
        {
            if (_duplicateInstance) return;
            // Stop the calibration thread if it is not null.
            if (_calibrationThread != null)
            {
                var result = _calibrationThread.StopThread();
                _calibrationThread = null;
                Debug.Log("Calibration thread stopped: " + (result ? "YES" : "NO"));
            }
            _calibrationInProgress = false;
            _showFailurePrompt = false;
            ShowCalibrationPanel = false;
            RestoreTask();
        }

        private void Update()
        {
            bool control = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (control && shift && Input.GetKeyDown(KeyCode.C) && !_showFailurePrompt)
                RequestCalibrationFromShortcut();
        }

        private void RequestCalibrationFromShortcut()
        {
            if (_calibrationInProgress || _showFailurePrompt) return;
            Debug.Log("Ctrl+Shift+C received; requesting eye-tracker calibration.");
            bool started = StartCalibration();
            Debug.Log("Calibration " + (started ? "" : "not ") + "started");
        }

        private void PauseTask()
        {
            if (_ownsCalibrationPause) return;
            if (ExperimentPauseService.Instance == null)
            {
                Debug.LogError("Calibration cannot pause the task because ExperimentPauseService is missing.");
                return;
            }

            _ownsCalibrationPause = ExperimentPauseService.Instance.BeginCalibrationPause();
            if (!_ownsCalibrationPause) return;
            LogCalibrationEvent("Calibration started");
        }

        private void LogCalibrationEvent(string eventType)
        {
            if (EyeTrackingSettings.MouseSimulationEnabled &&
                !eventType.StartsWith("Simulated ", System.StringComparison.Ordinal))
                eventType = "Simulated " + eventType;
            try { GameManager.saveTimeStamp(eventType); }
            catch (System.Exception exception) { Debug.LogError("Could not save calibration event: " + exception.Message); }
        }

        private void RestoreTask()
        {
            if (!_ownsCalibrationPause) return;
            if (ExperimentPauseService.Instance != null) ExperimentPauseService.Instance.EndCalibrationPause();
            _ownsCalibrationPause = false;
        }

        private void OnGUI()
        {
            Event currentEvent = Event.current;
            if (currentEvent != null && currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.C &&
                currentEvent.control && currentEvent.shift && !_showFailurePrompt)
            {
                RequestCalibrationFromShortcut();
                currentEvent.Use();
            }

            GUI.depth = -1000;
            if (_showCalibrationPanel)
            {
                Color previousColor = GUI.color;
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
                float elapsed = Mathf.Clamp01((Time.unscaledTime - _pointAnimationStart) / 1f);
                float size = Mathf.Lerp(52f, 12f, elapsed);
                GUI.DrawTexture(new Rect(Screen.width * _currentCalibrationPoint.x - size * 0.5f,
                    Screen.height * _currentCalibrationPoint.y - size * 0.5f, size, size), GetPointTexture());
                GUI.color = previousColor;
            }

            if (!_showFailurePrompt) return;
            const float width = 560f;
            const float height = 190f;
            Rect box = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUI.ModalWindow(GetInstanceID(), box, DrawFailureWindow, "Eye tracker calibration failed");
        }

        private float _pointAnimationStart;

        private void ShowPoint(Vector2 pointPosition)
        {
            _currentCalibrationPoint = pointPosition;
            _pointAnimationStart = Time.unscaledTime;
            if (_calibrationPoint != null)
                _calibrationPoint.rectTransform.anchoredPosition =
                    new Vector2(Screen.width * pointPosition.x, Screen.height * (1f - pointPosition.y));
            if (_pointScript != null) _pointScript.StartAnim();
        }

        private Texture2D GetPointTexture()
        {
            if (_pointTexture != null) return _pointTexture;
            const int textureSize = 64;
            _pointTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            _pointTexture.name = "Runtime Calibration Point";
            _pointTexture.hideFlags = HideFlags.HideAndDontSave;
            var pixels = new Color[textureSize * textureSize];
            Vector2 center = new Vector2((textureSize - 1) * 0.5f, (textureSize - 1) * 0.5f);
            float radiusSquared = center.x * center.x;
            for (int y = 0; y < textureSize; y++)
                for (int x = 0; x < textureSize; x++)
                    pixels[y * textureSize + x] = ((new Vector2(x, y) - center).sqrMagnitude <= radiusSquared)
                        ? Color.red : Color.clear;
            _pointTexture.SetPixels(pixels);
            _pointTexture.Apply();
            return _pointTexture;
        }

        private void DrawFailureWindow(int windowId)
        {
            GUI.Label(new Rect(25f, 40f, 510f, 55f), "Calibration was not applied. Re-calibrate before continuing, or explicitly ignore this warning.");
            if (GUI.Button(new Rect(80f, 115f, 170f, 45f), "Re-calibrate"))
            {
                _showFailurePrompt = false;
                LogCalibrationEvent("Calibration retry selected");
                StartCalibration();
            }
            if (GUI.Button(new Rect(310f, 115f, 170f, 45f), "Ignore and continue"))
            {
                _showFailurePrompt = false;
                LogCalibrationEvent("Calibration failure ignored");
                RestoreTask();
            }
        }
    }
}
