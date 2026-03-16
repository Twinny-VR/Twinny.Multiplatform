using Concept.Core;
using Twinny.Core.Input;
using Twinny.Mobile;
using Unity.Cinemachine;
using UnityEngine;

namespace Twinny.Mobile.Cameras
{
    /// <summary>
    /// Starts a lightweight camera demo when the user is idle and stops it on interaction.
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera))]
    [RequireComponent(typeof(CinemachineOrbitalFollow))]
    public class MobileCinemachineDemoMode : MonoBehaviour, IMobileInputCallbacks
    {
        [Header("Idle")]
        [SerializeField] private float _idleSeconds = 10f;

        [Header("Demo Motion")]
        [SerializeField] private float _demoRadius = 12f;
        [SerializeField] private float _radiusTransitionSpeed = 10f;
        [SerializeField] private float _targetTransitionSpeed = 5f;
        [SerializeField] private float _demoYawSpeed = 6f;
        [SerializeField] private Transform _demoTargetPoint;
        [SerializeField] private Transform _demoLookAtPoint;
        [SerializeField] private bool _forceLookAtDuringDemo = true;

        [Header("Debug")]
        [SerializeField] private bool _logState;

        [Header("References")]
        [SerializeField] private CinemachineCamera _cinemachineCamera;
        [SerializeField] private CinemachineOrbitalFollow _orbitalFollow;
        [SerializeField] private MobileCinemachineOrbitalHandler _orbitalHandler;

        private float _lastInteractionTime;
        private bool _isDemoActive;
        private Transform _previousLookAt;
        private Transform _runtimeLookAtProxy;

        private void OnEnable()
        {
            EnsureReferences();
            _lastInteractionTime = Time.unscaledTime;
            _isDemoActive = false;
            CallbackHub.RegisterCallback<IMobileInputCallbacks>(this);
        }

        private void OnDisable()
        {
            CallbackHub.UnregisterCallback<IMobileInputCallbacks>(this);
            if (_isDemoActive)
                CallbackHub.CallAction<ITwinnyMobileCallbacks>(callback => callback.OnExitDemoMode());
            _isDemoActive = false;
            RestoreCameraTargets();
        }

        private void OnValidate()
        {
            EnsureReferences();
            if (_idleSeconds < 0f) _idleSeconds = 0f;
            if (_radiusTransitionSpeed < 0f) _radiusTransitionSpeed = 0f;
            if (_targetTransitionSpeed < 0f) _targetTransitionSpeed = 0f;
        }

        private void Update()
        {
            if (!IsActiveCamera()) return;
            if (_orbitalFollow == null) return;

            CinemachineTracker activePoi = GetActivePointOfInterest();
            if (activePoi != null && activePoi.AvoidDemoMode)
            {
                if (_isDemoActive)
                    StopDemo(resetIdleTimer: false);
                return;
            }

            if (!_isDemoActive)
            {
                float idleSeconds = GetEffectiveIdleSeconds(activePoi);
                if (Time.unscaledTime - _lastInteractionTime >= idleSeconds)
                    StartDemo();
                return;
            }

            UpdateDemoMotion();
        }

        private void UpdateDemoMotion()
        {
            Transform followTarget = _cinemachineCamera != null ? _cinemachineCamera.Follow : null;
            if (_demoTargetPoint != null && followTarget != null && followTarget != _demoTargetPoint)
            {
                float moveStep = _targetTransitionSpeed * Time.unscaledDeltaTime;
                followTarget.position = Vector3.MoveTowards(followTarget.position, _demoTargetPoint.position, moveStep);
            }

            if (_forceLookAtDuringDemo && _cinemachineCamera != null && _cinemachineCamera.LookAt != null)
            {
                Vector3 desiredLookAtPosition = _cinemachineCamera.LookAt.position;
                if (_demoLookAtPoint != null) desiredLookAtPosition = _demoLookAtPoint.position;
                else if (_demoTargetPoint != null) desiredLookAtPosition = _demoTargetPoint.position;

                float lookAtStep = _targetTransitionSpeed * Time.unscaledDeltaTime;
                _cinemachineCamera.LookAt.position = Vector3.MoveTowards(
                    _cinemachineCamera.LookAt.position,
                    desiredLookAtPosition,
                    lookAtStep
                );
            }

            float step = _radiusTransitionSpeed * Time.unscaledDeltaTime;
            _orbitalFollow.Radius = Mathf.MoveTowards(_orbitalFollow.Radius, _demoRadius, step);

            var horizontal = _orbitalFollow.HorizontalAxis;
            horizontal.Value += _demoYawSpeed * Time.unscaledDeltaTime;
            _orbitalFollow.HorizontalAxis = horizontal;
        }

        private void StartDemo()
        {
            if (_cinemachineCamera != null)
            {
                _previousLookAt = _cinemachineCamera.LookAt;

                if (_forceLookAtDuringDemo)
                    EnsureLookAtTargetAssigned();

                ForceEnableHardLook();
            }

            _isDemoActive = true;
            CallbackHub.CallAction<ITwinnyMobileCallbacks>(callback => callback.OnEnterDemoMode());
            if (_logState)
                Debug.Log("[MobileCinemachineDemoMode] Demo started.");
        }

        private void NotifyInteraction()
        {
            _lastInteractionTime = Time.unscaledTime;
            if (_isDemoActive)
                StopDemo(resetIdleTimer: false);
        }

        private void StopDemo(bool resetIdleTimer)
        {
            if (resetIdleTimer)
                _lastInteractionTime = Time.unscaledTime;

            if (!_isDemoActive) return;

            _isDemoActive = false;
            CallbackHub.CallAction<ITwinnyMobileCallbacks>(callback => callback.OnExitDemoMode());
            RestoreCameraTargets();
            if (_logState)
                Debug.Log("[MobileCinemachineDemoMode] Demo stopped.");
        }

        private void RestoreCameraTargets()
        {
            if (_cinemachineCamera == null) return;
            _cinemachineCamera.LookAt = _previousLookAt;

            if (_runtimeLookAtProxy != null)
            {
                Destroy(_runtimeLookAtProxy.gameObject);
                _runtimeLookAtProxy = null;
            }
        }

        private void ForceEnableHardLook()
        {
            if (_cinemachineCamera == null) return;

            Component[] components = _cinemachineCamera.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null) continue;
                if (!component.GetType().Name.Contains("HardLookAt")) continue;
                if (component is Behaviour behaviour)
                    behaviour.enabled = true;
            }
        }

        private void EnsureLookAtTargetAssigned()
        {
            if (_cinemachineCamera == null) return;
            if (_cinemachineCamera.LookAt != null) return;

            GameObject proxy = new GameObject("DemoLookAtProxy");
            proxy.hideFlags = HideFlags.HideAndDontSave;

            Transform camTransform = _cinemachineCamera.transform;
            proxy.transform.position = camTransform.position + camTransform.forward * 10f;
            _runtimeLookAtProxy = proxy.transform;
            _cinemachineCamera.LookAt = _runtimeLookAtProxy;
        }

        private void EnsureReferences()
        {
            if (_cinemachineCamera == null)
                _cinemachineCamera = GetComponent<CinemachineCamera>();

            if (_orbitalFollow == null && _cinemachineCamera != null)
                _orbitalFollow = _cinemachineCamera.GetComponent<CinemachineOrbitalFollow>();

            if (_orbitalFollow == null)
                _orbitalFollow = GetComponent<CinemachineOrbitalFollow>();

            if (_orbitalHandler == null)
                _orbitalHandler = GetComponent<MobileCinemachineOrbitalHandler>();
        }

        private CinemachineTracker GetActivePointOfInterest()
        {
            EnsureReferences();
            if (_orbitalHandler != null && _orbitalHandler.ActivePointOfInterest != null)
                return _orbitalHandler.ActivePointOfInterest;

            Transform followTarget = _cinemachineCamera != null ? _cinemachineCamera.Follow : null;
            return followTarget != null ? followTarget.GetComponent<CinemachineTracker>() : null;
        }

        private float GetEffectiveIdleSeconds(CinemachineTracker activePoi)
        {
            float idleSeconds = _idleSeconds;
            if (activePoi != null && activePoi.HasDemoIdleSecondsOverride)
                idleSeconds = activePoi.DemoIdleSecondsOverride;

            return Mathf.Max(0f, idleSeconds);
        }

        private bool IsActiveCamera()
        {
            EnsureReferences();
            if (_cinemachineCamera == null) return false;

            int count = CinemachineBrain.ActiveBrainCount;
            for (int i = 0; i < count; i++)
            {
                var brain = CinemachineBrain.GetActiveBrain(i);
                if (brain != null && brain.ActiveVirtualCamera == _cinemachineCamera)
                    return true;
            }

            return false;
        }

        public void OnPrimaryDown(float x, float y) => NotifyInteraction();
        public void OnPrimaryUp(float x, float y) => NotifyInteraction();
        public void OnPrimaryDrag(float dx, float dy) => NotifyInteraction();
        public void OnSelect(SelectionData selection) => NotifyInteraction();
        public void OnCancel() => NotifyInteraction();
        public void OnZoom(float delta) => NotifyInteraction();
        public void OnTwoFingerTap(Vector2 position) => NotifyInteraction();
        public void OnTwoFingerLongPress(Vector2 position) => NotifyInteraction();
        public void OnTwoFingerSwipe(Vector2 direction, Vector2 startPosition) => NotifyInteraction();
        public void OnThreeFingerTap(Vector2 position) => NotifyInteraction();
        public void OnThreeFingerSwipe(Vector2 direction, Vector2 startPosition) => NotifyInteraction();
        public void OnThreeFingerPinch(float delta) => NotifyInteraction();
        public void OnFourFingerTap() => NotifyInteraction();
        public void OnFourFingerSwipe(Vector2 direction) => NotifyInteraction();
        public void OnEdgeSwipe(EdgeDirection edge) => NotifyInteraction();
        public void OnForceTouch(float pressure) => NotifyInteraction();
        public void OnHapticTouch() => NotifyInteraction();
        public void OnBackTap(int tapCount) => NotifyInteraction();

        // Device/system callbacks are ignored to avoid stopping demo due to sensor noise.
        public void OnShake() { }
        public void OnTilt(Vector3 tiltRotation) { }
        public void OnDeviceRotated(DeviceOrientation orientation) { }
        public void OnPickUp() { }
        public void OnPutDown() { }
        public void OnAccessibilityAction(string actionName) { }
        public void OnScreenReaderGesture(string gestureType) { }
        public void OnNotificationAction(bool isQuickAction) { }
    }
}
