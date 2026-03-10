using System;
using UnityEngine;
using UnityEngine.Events;

namespace Twinny.Mobile.Interactables
{
    [Serializable]
    public class FloorData
    {
        [Header("Identity")]
        [field: SerializeField] public string Title { get; set; } = "Pavement";
        [field: SerializeField] public string Subtitle { get; set; } = "Floor";
        [field: SerializeField] public string ImmersionSceneName { get; set; }
        public bool HasImmersionScene => !string.IsNullOrWhiteSpace(ImmersionSceneName);

        [field: SerializeField] public Floor.FloorSceneOpenMode SceneOpenMode { get; set; } = Floor.FloorSceneOpenMode.Immersive;
    }

    /// <summary>
    /// Represents a selectable floor/area with camera target metadata and selection events.
    /// </summary>
    public class Floor : MonoBehaviour
    {
        public enum FloorSceneOpenMode
        {
            Immersive = 0,
            Mockup = 1
        }

        public static event Action<Floor> Selected;
        public static event Action<Floor> Unselected;

        [Header("Identity")]
        [SerializeField] private FloorData _data = new();

        [Header("Camera Target")]
        [SerializeField] private bool _useFocusPoint = true;
        [SerializeField] private Transform _focusPoint;
        [SerializeField] private float _targetRadius = 16f;
        [SerializeField] private float _maxWallHeight = 3f;
        [SerializeField] private Vector3 _targetPositionOffset;
        [SerializeField] private Vector3 _targetRotationOffset;

        [Header("Events")]
        [SerializeField] private UnityEvent _onSelect;
        [SerializeField] private UnityEvent _onUnselect;

        public bool UseFocusPoint => _useFocusPoint;
        public Transform FocusPoint => _focusPoint != null ? _focusPoint : transform;
        public float TargetRadius => _targetRadius;
        public float MaxWallHeight => _maxWallHeight;
        public Vector3 TargetPositionOffset => _targetPositionOffset;
        public Vector3 TargetRotationOffset => _targetRotationOffset;

        public Transform TargetTransform => _useFocusPoint && _focusPoint != null ? _focusPoint : transform;
        public Vector3 TargetPosition => TargetTransform.position + _targetPositionOffset;
        public Quaternion TargetRotation => TargetTransform.rotation * Quaternion.Euler(_targetRotationOffset);

        public FloorData Data => _data ??= new FloorData();

        public void Select()
        {
            _onSelect?.Invoke();
            Selected?.Invoke(this);
        }

        public void Unselect()
        {
            _onUnselect?.Invoke();
            Unselected?.Invoke(this);
        }

        public bool TryGetWorldBounds(out Bounds bounds)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(false);
            if (renderers == null || renderers.Length == 0)
            {
                bounds = new Bounds(transform.position, Vector3.zero);
                return false;
            }

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            return true;
        }

        public bool TryGetScreenRect(Camera camera, out Rect rect)
        {
            rect = default;
            if (camera == null) return false;
            if (!TryGetWorldBounds(out Bounds bounds)) return false;

            Vector3 c = bounds.center;
            Vector3 e = bounds.extents;
            Vector3[] corners = new Vector3[8]
            {
                c + new Vector3(-e.x, -e.y, -e.z),
                c + new Vector3(-e.x, -e.y,  e.z),
                c + new Vector3(-e.x,  e.y, -e.z),
                c + new Vector3(-e.x,  e.y,  e.z),
                c + new Vector3( e.x, -e.y, -e.z),
                c + new Vector3( e.x, -e.y,  e.z),
                c + new Vector3( e.x,  e.y, -e.z),
                c + new Vector3( e.x,  e.y,  e.z)
            };

            bool hasAnyVisible = false;
            float minX = float.PositiveInfinity;
            float minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;

            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 screen = camera.WorldToScreenPoint(corners[i]);
                if (screen.z <= 0f) continue;

                hasAnyVisible = true;
                if (screen.x < minX) minX = screen.x;
                if (screen.y < minY) minY = screen.y;
                if (screen.x > maxX) maxX = screen.x;
                if (screen.y > maxY) maxY = screen.y;
            }

            if (!hasAnyVisible) return false;
            rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return true;
        }
    }
}
