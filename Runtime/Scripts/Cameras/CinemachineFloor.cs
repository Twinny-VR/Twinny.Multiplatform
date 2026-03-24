using Twinny.Mobile.Interactables;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;

namespace Twinny.Mobile.Cameras
{
 

    /// <summary>
    /// Represents a selectable floor/area with camera target metadata and selection events.
    /// </summary>
    [MovedFrom(true, "Twinny.Mobile.Interactables", "Twinny.Mobile", "Floor")]
    public class CinemachineFloor : Floor
    {
        [Header("Camera Target")]
        [SerializeField] private bool _useFocusPoint = true;
        [FormerlySerializedAs("_focusPoint")]
        [SerializeField] private CinemachineTracker _trackerPoint;

        public bool UseFocusPoint => _useFocusPoint;
        public CinemachineTracker TrackerPoint => _trackerPoint;

        public Transform TargetTransform => _useFocusPoint && _trackerPoint != null ? _trackerPoint.transform : transform;
        public Vector3 TargetPosition => TargetTransform.position;
        public Quaternion TargetRotation => TargetTransform.rotation;

  

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
