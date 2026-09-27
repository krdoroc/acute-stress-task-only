using UnityEngine;

namespace Tobii.Research.Unity
{
    /// <summary>Stable area-of-interest identity for analysis and cross-task portability.</summary>
    [DisallowMultipleComponent]
    public sealed class GazeTarget : MonoBehaviour
    {
        [SerializeField, Tooltip("Stable ID, for example item_03_value. Object name is a migration fallback.")]
        private string _targetId;
        public string TargetId { get { return string.IsNullOrEmpty(_targetId) ? gameObject.name : _targetId; } }
        public void Configure(string targetId) { _targetId = targetId; }

        private void OnValidate()
        {
            if (GetComponentInChildren<Collider2D>() == null && GetComponentInChildren<Collider>() == null)
                Debug.LogWarning("GazeTarget '" + name + "' has no 2D or 3D collider.", this);
        }
    }
}
