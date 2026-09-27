//-----------------------------------------------------------------------
// Copyright © 2019 Tobii Pro AB. All rights reserved.
//-----------------------------------------------------------------------

using System;
using System.Globalization;
using System.Xml;
using UnityEngine;

namespace Tobii.Research.Unity
{
    public class ScreenBasedSaveData : MonoBehaviour
    {
        /// <summary>
        /// Instance of <see cref="ScreenBasedSaveData"/> for easy access.
        /// Assigned in Awake() so use earliest in Start().
        /// </summary>
        public static ScreenBasedSaveData Instance { get; private set; }

        [SerializeField]
        [Tooltip("If true, data is saved.")]
        private bool _saveData;

        [SerializeField]
        [Tooltip("If true, Unity3D-converted data is saved.")]
        private bool _saveUnityData = true;

        [SerializeField]
        [Tooltip("If true, raw gaze data is saved.")]
        private bool _saveRawData = true;

        [SerializeField]
        [Tooltip("This key will start or stop saving data.")]
        private KeyCode _toggleSaveData = KeyCode.None;

        [SerializeField, Tooltip("Write the built-in local XML file.")]
        private bool _saveLocally = true;
        [SerializeField, Tooltip("Layers containing gaze targets.")]
        private LayerMask _gazeTargetLayers = ~0;
        [SerializeField, Tooltip("Optional components implementing IGazeDataSink, such as a future DHive adapter.")]
        private MonoBehaviour[] _additionalSinkBehaviours;
        [SerializeField, Tooltip("Show the most recent mouse gaze target in the Game view.")]
        private bool _showMouseHitOverlay = true;

        public bool MouseSimulationEnabled { get { return EyeTrackingSettings.MouseSimulationEnabled; } }
        public bool SimulatedCalibrationSucceeds { get { return EyeTrackingSettings.SimulatedCalibrationSucceeds; } }
        /// <summary>
        /// If true, data is saved.
        /// </summary>
        public bool SaveData
        {
            get
            {
                return _saveData;
            }

            set
            {
                _saveData = value;
            }
        }

        private EyeTracker _eyeTracker;
        private XmlWriterSettings _fileSettings;
        private XmlWriter _file;
        private IGazeHitClassifier _hitClassifier;
        private IGazeDataSink[] _additionalSinks;
        private GazeSampleRecord _latestMouseRecord;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            _eyeTracker = EyeTracker.Instance;
            _hitClassifier = new ScreenBasedGazeHitClassifier(Camera.main, _gazeTargetLayers.value);
            BuildAdditionalSinks();
        }

        private void Update()
        {
            if (Input.GetKeyDown(_toggleSaveData))
            {
                SaveData = !SaveData;
            }

            if (!_saveData)
            {
                if (_file != null)
                {
                    // Closes _file and sets it to null.
                    CloseDataFile();
                }

                return;
            }

            if (_saveLocally && _file == null)
            {
                // Opens data file. It becomes non-null.
                OpenDataFile();
            }

            if (!_saveUnityData && !_saveRawData)
            {
                // No one wants to save anyway.
                return;
            }

            // Do not mix calibration or failure-prompt samples into task gaze data.
            // Drain real samples so they cannot be written after the task resumes.
            if (Calibration.Instance != null && Calibration.Instance.TaskPausedForCalibration)
            {
                if (!MouseSimulationEnabled && _eyeTracker != null)
                    while (_eyeTracker.NextData != default(IGazeData)) { }
                return;
            }

            if (MouseSimulationEnabled)
            {
                WriteGazeData(new MouseGazeData(Input.mousePosition));
                return;
            }

            if (_eyeTracker == null) return;
            var data = _eyeTracker.NextData;
            while (data != default(IGazeData))
            {
                WriteGazeData(data);
                data = _eyeTracker.NextData;
            }
        }

        private void OnDestroy()
        {
            CloseDataFile();
            CloseAdditionalSinks();
        }

        private void OnGUI()
        {
            if (!MouseSimulationEnabled || !_showMouseHitOverlay || _latestMouseRecord == null) return;
            if (Calibration.Instance != null && Calibration.Instance.TaskPausedForCalibration) return;
            string target = _latestMouseRecord.Hit.IsHit ? _latestMouseRecord.Hit.TargetId : "No hit";
            GUI.Box(new Rect(10f, 10f, 360f, 48f), "MOUSE SIMULATION  |  " + target);
        }

        private void OpenDataFile()
        {
            if (_file != null)
            {
                Debug.Log("Already saving data.");
                return;
            }

            _fileSettings = new XmlWriterSettings();
            _fileSettings.Indent = true;

            var fileName = GameManager.identifierName + "_" + GameManager.escena + "_" + GameManager.TotalTrials + ".xml";

            if (GameManager.escena == "InterTrialRest")
            {
                fileName = GameManager.identifierName + "_" + GameManager.escena + "_" + (GameManager.TotalTrials) + ".xml";
            }


            _file = XmlWriter.Create(StudyDataPaths.GetEyeTrackingFile(fileName), _fileSettings);
            _file.WriteStartDocument();
            _file.WriteStartElement("Data");
        }

        private void CloseDataFile()
        {
            if (_file == null)
            {
                Debug.Log("No ongoing recording.");
                return;
            }

            _file.WriteEndElement();
            _file.WriteEndDocument();
            _file.Flush();
            _file.Close();
            _file = null;
            _fileSettings = null;
        }

        private void WriteGazeData(IGazeData gazeData)
        {
            GazeRaySource raySource;
            GazeHit hit = _hitClassifier.Classify(gazeData, out raySource);
            var record = new GazeSampleRecord(gazeData, hit, raySource, GameManager.participantID,
                GameManager.escena, GameManager.block, GameManager.trial);
            if (gazeData is MouseGazeData) _latestMouseRecord = record;
            if (_file != null) WriteLocalRecord(record);
            WriteAdditionalSinks(record);
        }

        private void WriteLocalRecord(GazeSampleRecord record)
        {
            IGazeData gazeData = record.GazeData;
            _file.WriteStartElement("GazeData");
            _file.WriteAttributeString("SchemaVersion", GazeSampleRecord.CurrentSchemaVersion);
            _file.WriteAttributeString("ParticipantId", record.ParticipantId);
            _file.WriteAttributeString("Scene", record.Scene);
            _file.WriteAttributeString("Block", record.Block.ToString(CultureInfo.InvariantCulture));
            _file.WriteAttributeString("Trial", record.Trial.ToString(CultureInfo.InvariantCulture));
            _file.WriteAttributeString("TimeStamp", gazeData.TimeStamp.ToString(CultureInfo.InvariantCulture));
            _file.WriteAttributeString("SystemTimeUtc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            GameManager.EyeTrackerTime = gazeData.TimeStamp.ToString(CultureInfo.InvariantCulture);
            _file.WriteAttributeString("GazeRaySource", record.RaySource.ToString());
            _file.WriteAttributeString("Simulated", gazeData is MouseGazeData ? "True" : "False");
            _file.WriteAttributeString("Hit", record.Hit.IsHit ? "True" : "False");
            _file.WriteAttributeString("HitTargetId", record.Hit.TargetId);
            _file.WriteAttributeString("HitObject", record.Hit.ObjectName);
            _file.WriteAttributeString("HitPoint", record.Hit.Point.ToString("F6", CultureInfo.InvariantCulture));
            _file.WriteAttributeString("HitBoundsMin", record.Hit.BoundsMin.ToString("F6", CultureInfo.InvariantCulture));
            _file.WriteAttributeString("HitBoundsMax", record.Hit.BoundsMax.ToString("F6", CultureInfo.InvariantCulture));
            if (_saveUnityData) { _file.WriteEye(gazeData.Left, "Left"); _file.WriteEye(gazeData.Right, "Right"); }
            if (_saveRawData && gazeData.OriginalGaze != null) _file.WriteRawGaze(gazeData.OriginalGaze);
            _file.WriteEndElement();
        }

        private void BuildAdditionalSinks()
        {
            var sinks = new System.Collections.Generic.List<IGazeDataSink>();
            if (_additionalSinkBehaviours != null) foreach (MonoBehaviour behaviour in _additionalSinkBehaviours)
            {
                IGazeDataSink sink = behaviour as IGazeDataSink;
                if (sink != null) sinks.Add(sink);
                else if (behaviour != null) Debug.LogError(behaviour.name + " does not implement IGazeDataSink.", behaviour);
            }
            _additionalSinks = sinks.ToArray();
        }

        private void WriteAdditionalSinks(GazeSampleRecord record)
        {
            foreach (IGazeDataSink sink in _additionalSinks) try { sink.Write(record); }
            catch (Exception exception) { Debug.LogError("Gaze sink failed: " + exception); }
        }

        private void CloseAdditionalSinks()
        {
            if (_additionalSinks == null) return;
            foreach (IGazeDataSink sink in _additionalSinks) try { sink.Close(); }
            catch (Exception exception) { Debug.LogError("Gaze sink close failed: " + exception); }
        }
    }
}
