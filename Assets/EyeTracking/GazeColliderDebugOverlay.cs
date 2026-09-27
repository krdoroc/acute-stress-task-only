using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tobii.Research.Unity
{
    /// <summary>
    /// Optional Game-view diagnostic for checking the exact collider geometry used
    /// by gaze hit classification. Enable it in EyeTrackingSettings.
    /// </summary>
    public sealed class GazeColliderDebugOverlay : MonoBehaviour
    {
        private const float RefreshIntervalSeconds = 0.5f;
        private const float BorderThickness = 2f;
        private static readonly Color ValueColor = new Color(0.2f, 1f, 0.35f, 1f);
        private static readonly Color WeightColor = new Color(0.2f, 0.75f, 1f, 1f);
        private static readonly Color OtherColor = new Color(1f, 0.75f, 0.15f, 1f);

        private readonly List<TargetCollider> _targetColliders = new List<TargetCollider>();
        private float _nextRefreshTime;
        private Camera _camera;

        private struct TargetCollider
        {
            public readonly GazeTarget Target;
            public readonly Collider2D Collider2D;
            public readonly Collider Collider3D;

            public TargetCollider(GazeTarget target, Collider2D collider2D, Collider collider3D)
            {
                Target = target;
                Collider2D = collider2D;
                Collider3D = collider3D;
            }

            public bool TryGetBounds(out Bounds bounds)
            {
                if (Collider2D != null && Collider2D.enabled && Collider2D.gameObject.activeInHierarchy)
                {
                    bounds = Collider2D.bounds;
                    return true;
                }

                if (Collider3D != null && Collider3D.enabled && Collider3D.gameObject.activeInHierarchy)
                {
                    bounds = Collider3D.bounds;
                    return true;
                }

                bounds = default(Bounds);
                return false;
            }
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            RefreshTargets();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RefreshTargets();
        }

        private void Update()
        {
            if (!EyeTrackingSettings.ShowGazeTargetColliders) return;
            if (Time.unscaledTime >= _nextRefreshTime) RefreshTargets();
        }

        private void RefreshTargets()
        {
            _camera = Camera.main;
            _targetColliders.Clear();

            GazeTarget[] targets = FindObjectsByType<GazeTarget>(FindObjectsSortMode.None);
            foreach (GazeTarget target in targets)
            {
                foreach (Collider2D collider2D in target.GetComponents<Collider2D>())
                    _targetColliders.Add(new TargetCollider(target, collider2D, null));
                foreach (Collider collider3D in target.GetComponents<Collider>())
                    _targetColliders.Add(new TargetCollider(target, null, collider3D));
            }

            _nextRefreshTime = Time.unscaledTime + RefreshIntervalSeconds;
        }

        private void OnGUI()
        {
            if (!EyeTrackingSettings.ShowGazeTargetColliders || _camera == null) return;

            Color previousColor = GUI.color;
            foreach (TargetCollider targetCollider in _targetColliders)
            {
                Bounds bounds;
                if (!targetCollider.TryGetBounds(out bounds)) continue;

                Rect screenRect;
                if (!TryGetScreenRect(bounds, out screenRect)) continue;

                string targetId = targetCollider.Target.TargetId;
                GUI.color = GetTargetColor(targetId);
                DrawOutline(screenRect);
                GUI.Box(new Rect(screenRect.xMin, screenRect.yMin - 20f,
                    Mathf.Max(120f, screenRect.width), 20f), targetId);
            }
            GUI.color = previousColor;
        }

        private bool TryGetScreenRect(Bounds bounds, out Rect screenRect)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            Vector3[] corners =
            {
                new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z),
                new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z),
                new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z)
            };

            float xMin = float.PositiveInfinity;
            float xMax = float.NegativeInfinity;
            float yMin = float.PositiveInfinity;
            float yMax = float.NegativeInfinity;
            bool visible = false;

            foreach (Vector3 corner in corners)
            {
                Vector3 point = _camera.WorldToScreenPoint(corner);
                if (point.z < 0f) continue;
                visible = true;
                xMin = Mathf.Min(xMin, point.x);
                xMax = Mathf.Max(xMax, point.x);
                float guiY = Screen.height - point.y;
                yMin = Mathf.Min(yMin, guiY);
                yMax = Mathf.Max(yMax, guiY);
            }

            screenRect = visible ? Rect.MinMaxRect(xMin, yMin, xMax, yMax) : default(Rect);
            return visible;
        }

        private static Color GetTargetColor(string targetId)
        {
            if (targetId.EndsWith("_value")) return ValueColor;
            if (targetId.EndsWith("_weight")) return WeightColor;
            return OtherColor;
        }

        private static void DrawOutline(Rect rect)
        {
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, rect.width, BorderThickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMax - BorderThickness, rect.width, BorderThickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, BorderThickness, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - BorderThickness, rect.yMin, BorderThickness, rect.height), Texture2D.whiteTexture);
        }
    }
}
