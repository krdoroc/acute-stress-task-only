using System;
using UnityEngine;

namespace Tobii.Research.Unity
{
    public enum GazeRaySource { Invalid, Binocular, LeftEye, RightEye, MouseSimulation }

    /// <summary>Immutable hit information associated with one gaze sample.</summary>
    public sealed class GazeHit
    {
        public static readonly GazeHit None = new GazeHit(false, string.Empty, string.Empty, Vector3.zero, Vector3.zero, Vector3.zero);
        public bool IsHit { get; private set; }
        public string TargetId { get; private set; }
        public string ObjectName { get; private set; }
        public Vector3 Point { get; private set; }
        public Vector3 BoundsMin { get; private set; }
        public Vector3 BoundsMax { get; private set; }

        public GazeHit(bool isHit, string targetId, string objectName, Vector3 point, Vector3 boundsMin, Vector3 boundsMax)
        {
            IsHit = isHit; TargetId = targetId ?? string.Empty; ObjectName = objectName ?? string.Empty;
            Point = point; BoundsMin = boundsMin; BoundsMax = boundsMax;
        }
    }

    /// <summary>A complete scientific record passed to storage adapters.</summary>
    public sealed class GazeSampleRecord
    {
        public const string CurrentSchemaVersion = "2";
        public IGazeData GazeData { get; private set; }
        public GazeHit Hit { get; private set; }
        public GazeRaySource RaySource { get; private set; }
        public string ParticipantId { get; private set; }
        public string Scene { get; private set; }
        public int Block { get; private set; }
        public int Trial { get; private set; }

        public GazeSampleRecord(IGazeData gazeData, GazeHit hit, GazeRaySource raySource, string participantId, string scene, int block, int trial)
        {
            GazeData = gazeData; Hit = hit ?? GazeHit.None; RaySource = raySource;
            ParticipantId = participantId ?? string.Empty; Scene = scene ?? string.Empty; Block = block; Trial = trial;
        }
    }

    /// <summary>Implement on a MonoBehaviour for a future DHive or other output adapter.</summary>
    public interface IGazeDataSink { void Write(GazeSampleRecord record); void Close(); }
    public interface IGazeHitClassifier { GazeHit Classify(IGazeData gazeData, out GazeRaySource raySource); }

    /// <summary>Classifies the exact sample being saved. Task 2D colliders are queried before legacy 3D colliders.</summary>
    public sealed class ScreenBasedGazeHitClassifier : IGazeHitClassifier
    {
        private readonly Camera _camera;
        private readonly int _layerMask;
        public ScreenBasedGazeHitClassifier(Camera camera, int layerMask) { _camera = camera; _layerMask = layerMask; }

        public GazeHit Classify(IGazeData gazeData, out GazeRaySource raySource)
        {
            Ray ray;
            if (!TryCreateRay(gazeData, _camera, out ray, out raySource)) return GazeHit.None;
            RaycastHit2D hit2D = Physics2D.GetRayIntersection(ray, Mathf.Infinity, _layerMask);
            if (hit2D.collider != null) return FromCollider(hit2D.collider.transform, hit2D.point, hit2D.collider.bounds);
            RaycastHit hit3D;
            if (Physics.Raycast(ray, out hit3D, Mathf.Infinity, _layerMask, QueryTriggerInteraction.Collide))
                return FromCollider(hit3D.transform, hit3D.point, hit3D.collider.bounds);
            return GazeHit.None;
        }

        public static bool TryCreateRay(IGazeData gazeData, Camera camera, out Ray ray, out GazeRaySource source)
        {
            ray = default(Ray); source = GazeRaySource.Invalid;
            if (gazeData == null || camera == null) return false;
            MouseGazeData mouseData = gazeData as MouseGazeData;
            if (mouseData != null)
            {
                source = GazeRaySource.MouseSimulation;
                ray = camera.ScreenPointToRay(mouseData.ScreenPosition);
                return true;
            }
            bool leftValid = gazeData.Left != null && gazeData.Left.GazePointValid;
            bool rightValid = gazeData.Right != null && gazeData.Right.GazePointValid;
            Vector2 point;
            if (leftValid && rightValid) { point = (gazeData.Left.GazePointOnDisplayArea + gazeData.Right.GazePointOnDisplayArea) * 0.5f; source = GazeRaySource.Binocular; }
            else if (leftValid) { point = gazeData.Left.GazePointOnDisplayArea; source = GazeRaySource.LeftEye; }
            else if (rightValid) { point = gazeData.Right.GazePointOnDisplayArea; source = GazeRaySource.RightEye; }
            else return false;
            ray = camera.ScreenPointToRay(new Vector3(Screen.width * point.x, Screen.height * (1f - point.y), 0f));
            return true;
        }

        private static GazeHit FromCollider(Transform hitTransform, Vector3 point, Bounds bounds)
        {
            GazeTarget target = hitTransform.GetComponentInParent<GazeTarget>();
            return new GazeHit(true, target != null ? target.TargetId : GetHierarchyPath(hitTransform),
                target != null ? target.gameObject.name : hitTransform.name, point, bounds.min, bounds.max);
        }

        private static string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null) { transform = transform.parent; path = transform.name + "/" + path; }
            return path;
        }
    }
}
