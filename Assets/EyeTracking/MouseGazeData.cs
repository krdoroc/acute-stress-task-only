using System;
using UnityEngine;

namespace Tobii.Research.Unity
{
    /// <summary>
    /// One editor test sample. Mouse pixels are converted to the same top-left,
    /// normalized display coordinates used by Tobii screen-based samples.
    /// This is an input substitute, not a model of eye-tracker noise or timing.
    /// </summary>
    public sealed class MouseGazeData : IGazeData
    {
        public IGazeDataEye Left { get; private set; }
        public IGazeDataEye Right { get; private set; }
        public GazeDataEventArgs OriginalGaze { get { return null; } }
        public long TimeStamp { get; private set; }
        public Vector3 ScreenPosition { get; private set; }

        public Ray CombinedGazeRayScreen
        {
            get { return Camera.main != null ? Camera.main.ScreenPointToRay(ScreenPosition) : default(Ray); }
        }

        public bool CombinedGazeRayScreenValid { get { return Camera.main != null; } }

        public MouseGazeData(Vector3 mousePosition)
        {
            ScreenPosition = mousePosition;
            TimeStamp = DateTime.UtcNow.Ticks / 10; // Microseconds; simulated clock, not Tobii device time.
            Vector2 displayPoint = new Vector2(mousePosition.x / Screen.width, 1f - mousePosition.y / Screen.height);
            Left = new MouseGazeEye(displayPoint, mousePosition);
            Right = new MouseGazeEye(displayPoint, mousePosition);
        }

        private sealed class MouseGazeEye : IGazeDataEye
        {
            private readonly Vector3 _screenPosition;
            public Vector3 GazeOriginInUserCoordinates { get { return Vector3.zero; } }
            public Vector3 GazeOriginInTrackBoxCoordinates { get { return Vector3.zero; } }
            public bool GazeOriginValid { get { return false; } }
            public Vector3 GazePointInUserCoordinates { get { return Vector3.zero; } }
            public Vector2 GazePointOnDisplayArea { get; private set; }
            public bool GazePointValid { get { return true; } }
            public Ray GazeRayScreen
            {
                get { return Camera.main != null ? Camera.main.ScreenPointToRay(_screenPosition) : default(Ray); }
            }
            public float PupilDiameter { get { return 0f; } }
            public bool PupilDiameterValid { get { return false; } }

            public MouseGazeEye(Vector2 displayPoint, Vector3 screenPosition)
            {
                GazePointOnDisplayArea = displayPoint;
                _screenPosition = screenPosition;
            }
        }
    }
}
