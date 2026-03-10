using UnityEngine;

namespace Twinny.Mobile.Samples
{
    public sealed class WSLabelBillboard : MonoBehaviour
    {
        private Camera m_targetCamera;
        [SerializeField] private bool _keepUpright = false;

        private void Awake()
        {
            m_targetCamera = Camera.main;
        }

        private void LateUpdate()
        {
            if (m_targetCamera == null) return;

            Vector3 directionToCamera = transform.position - m_targetCamera.transform.position;
            if (directionToCamera.sqrMagnitude <= 0.0001f) return;

            Vector3 forward = directionToCamera.normalized;
            Vector3 up = m_targetCamera.transform.up;

            if (_keepUpright)
            {
                forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
                if (forward.sqrMagnitude <= 0.0001f)
                    forward = directionToCamera.normalized;

                up = Vector3.up;
            }

            transform.rotation = Quaternion.LookRotation(forward, up);
        }
    }

}
