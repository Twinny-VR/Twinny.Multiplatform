using Concept.Core;
using Twinny.Core.Input;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Twinny.Mobile.Cameras
{
    /// <summary>
    /// Mobile input bridge for Cinemachine Pan Tilt-based FPS cameras.
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera))]
    [RequireComponent(typeof(CinemachinePanTilt))]
    public class MobileCinemachineFpsHandler : MonoBehaviour, IMobileInputCallbacks, ITwinnyMobileCallbacks, IMobileUICallbacks
    {
        [Header("Cinemachine")]
        [SerializeField] private CinemachineCamera _cinemachineCamera;
        [SerializeField] private CinemachinePanTilt _panTilt;

        [Header("Mode")]
        [SerializeField] private int _activePriority = 20;
        [SerializeField] private int _inactivePriority = 5;

        [Header("Tuning")]
        [SerializeField] private float _rotateSpeed = 0.1f;
        [SerializeField] private float _tiltSpeed = 0.1f;
        [SerializeField] private Vector2 _verticalAxisLimits = new Vector2(-80f, 80f);
        [SerializeField] private float _zoomFov = 45f;
        [SerializeField] private float _zoomSpeed = 90f;
        [SerializeField] private float _zoomReleaseDelay = 0.15f;
        [SerializeField] private bool _useGyroscope = true;
        [SerializeField] private float _gyroYawSpeed = 0.02f;
        [SerializeField] private float _gyroPitchSpeed = 0.02f;
        [SerializeField] private float _gyroDeadZone = 0.5f;
        [SerializeField] private float _gyroScale = 0.25f;

        private bool _hasDefaultFov;
        private float _defaultFov;
        private float _lastZoomInputTime;
        private bool _zoomRequested;
        private bool _isModeActive;

        private void Update()
        {
            if (!IsActiveCamera()) return;
            UpdateZoom();
        }

        private void OnEnable()
        {
            EnsureReferences();
            ClampLimits();
            CacheDefaultFov();
            DisableRecentering();
            ApplyMode(_isModeActive);
            CallbackHub.RegisterCallback<IMobileInputCallbacks>(this);
            CallbackHub.RegisterCallback<ITwinnyMobileCallbacks>(this);
            CallbackHub.RegisterCallback<IMobileUICallbacks>(this);
        }

        private void OnDisable()
        {
            CallbackHub.UnregisterCallback<IMobileInputCallbacks>(this);
            CallbackHub.UnregisterCallback<ITwinnyMobileCallbacks>(this);
            CallbackHub.UnregisterCallback<IMobileUICallbacks>(this);
        }

        private void OnValidate()
        {
            EnsureReferences();
            ClampLimits();
        }

        public void OnPrimaryDown(float x, float y) { }
        public void OnPrimaryUp(float x, float y) { }
        public void OnSelect(SelectionData selection) { }
        public void OnCancel() { }
        public void OnPrimaryDrag(float dx, float dy) => ApplyRotation(dx, dy);
        public void OnZoom(float delta) => RegisterZoomInput(delta);
        public void OnTwoFingerTap(Vector2 position) { }
        public void OnTwoFingerLongPress(Vector2 position) { }
        public void OnTwoFingerSwipe(Vector2 direction, Vector2 startPosition) { }
        public void OnThreeFingerTap(Vector2 position) { }
        public void OnThreeFingerSwipe(Vector2 direction, Vector2 startPosition) { }
        public void OnThreeFingerPinch(float delta) { }
        public void OnFourFingerTap() { }
        public void OnFourFingerSwipe(Vector2 direction) { }
        public void OnEdgeSwipe(EdgeDirection edge) { }
        public void OnForceTouch(float pressure) { }
        public void OnHapticTouch() { }
        public void OnBackTap(int tapCount) { }
        public void OnShake() { }
        public void OnTilt(Vector3 tiltRotation) => ApplyGyroRotation(tiltRotation);
        public void OnDeviceRotated(DeviceOrientation orientation) { }
        public void OnPickUp() { }
        public void OnPutDown() { }
        public void OnAccessibilityAction(string actionName) { }
        public void OnScreenReaderGesture(string gestureType) { }
        public void OnNotificationAction(bool isQuickAction) { }

        public void OnStartInteract(GameObject gameObject) { }
        public void OnStopInteract(GameObject gameObject) { }
        public void OnStartTeleport() { }
        public void OnTeleport() { }

        public void OnPlatformInitializing() { }
        public void OnPlatformInitialized() { }
        public void OnExperienceReady() { }
        public void OnExperienceStarting() { }
        public void OnExperienceStarted() { }
        public void OnExperienceEnding() { }
        public void OnExperienceEnded(bool isRunning) { }
        public void OnExperienceLoaded() { }
        public void OnSceneLoadStart(string sceneName) { }
        public void OnSceneLoaded(Scene scene) { }
        public void OnTeleportToLandMark(int landMarkIndex) { }
        public void OnSkyboxHDRIChanged(Material material) { }

        public void OnEnterImmersiveMode() => ApplyMode(true);
        public void OnExitImmersiveMode() => ApplyMode(false);
        public void OnEnterMockupMode() { }
        public void OnExitMockupMode() { }
        public void OnEnterDemoMode() { }
        public void OnExitDemoMode() { }
         public void OnPOIFocused() { }

        public void OnMaxWallHeightRequested(float height) { }
        public void OnImmersiveRequested(string sceneName) { }
        public void OnMockupRequested(string sceneName) { }
        public void OnStartExperienceRequested(string sceneName) { }
        public void OnLoadingProgressChanged(float progress) { }
        public void OnGyroscopeToggled(bool enabled) => _useGyroscope = enabled;

        private void ApplyRotation(float dx, float dy)
        {
            if (!IsActiveCamera()) return;
            if (_panTilt == null) return;

            var horizontal = _panTilt.PanAxis;
            horizontal.Value -= dx * _rotateSpeed;
            _panTilt.PanAxis = horizontal;

            var vertical = _panTilt.TiltAxis;
            float next = vertical.Value + dy * _tiltSpeed;
            vertical.Value = Mathf.Clamp(next, _verticalAxisLimits.x, _verticalAxisLimits.y);
            _panTilt.TiltAxis = vertical;
        }

        private void ApplyGyroRotation(Vector3 tiltRotation)
        {
            if (!_useGyroscope) return;
            if (!IsActiveCamera()) return;
            if (_panTilt == null) return;
            if (Twinny.Mobile.Input.MobileInputProvider.CurrentData.TouchCount > 0) return;

            float yawDelta = Mathf.Abs(tiltRotation.y) > _gyroDeadZone ? tiltRotation.y * _gyroYawSpeed * _gyroScale : 0f;
            float pitchDelta = Mathf.Abs(tiltRotation.x) > _gyroDeadZone ? tiltRotation.x * _gyroPitchSpeed * _gyroScale : 0f;
            if (Mathf.Approximately(yawDelta, 0f) && Mathf.Approximately(pitchDelta, 0f)) return;

            var horizontal = _panTilt.PanAxis;
            horizontal.Value += yawDelta;
            _panTilt.PanAxis = horizontal;

            var vertical = _panTilt.TiltAxis;
            float next = vertical.Value + pitchDelta;
            vertical.Value = Mathf.Clamp(next, _verticalAxisLimits.x, _verticalAxisLimits.y);
            _panTilt.TiltAxis = vertical;
        }

        private void SyncCameraToCurrentGyroRotation()
        {
            if (!_useGyroscope) return;
            if (_panTilt == null) return;

            Vector3? gyroRotation = Twinny.Mobile.Input.MobileInputProvider.CurrentData.GyroRotation;
            if (!gyroRotation.HasValue) return;

            Vector3 euler = gyroRotation.Value;
            float yaw = NormalizeSignedAngle(euler.y);
            float pitch = Mathf.Clamp(NormalizeSignedAngle(euler.x), _verticalAxisLimits.x, _verticalAxisLimits.y);

            var horizontal = _panTilt.PanAxis;
            horizontal.Value = yaw;
            _panTilt.PanAxis = horizontal;

            var vertical = _panTilt.TiltAxis;
            vertical.Value = pitch;
            _panTilt.TiltAxis = vertical;
        }

        private void DisableRecentering()
        {
            if (_panTilt == null) return;
            var pan = _panTilt.PanAxis;
            var tilt = _panTilt.TiltAxis;
            pan.Recentering.Enabled = false;
            tilt.Recentering.Enabled = false;
            _panTilt.PanAxis = pan;
            _panTilt.TiltAxis = tilt;
        }

        private void EnsureReferences()
        {
            if (_cinemachineCamera == null)
                _cinemachineCamera = GetComponent<CinemachineCamera>();

            if (_panTilt == null && _cinemachineCamera != null)
                _panTilt = _cinemachineCamera.GetComponent<CinemachinePanTilt>();

            if (_panTilt == null)
                _panTilt = GetComponent<CinemachinePanTilt>();
        }

        private void ApplyMode(bool isActive)
        {
            _isModeActive = isActive;
            if (_cinemachineCamera != null)
                _cinemachineCamera.Priority = isActive ? _activePriority : _inactivePriority;

            if (isActive)
                SyncCameraToCurrentGyroRotation();
        }

        private static float NormalizeSignedAngle(float angle)
        {
            angle %= 360f;
            if (angle > 180f) angle -= 360f;
            if (angle < -180f) angle += 360f;
            return angle;
        }

        private void ClampLimits()
        {
            if (_verticalAxisLimits.y < _verticalAxisLimits.x)
                _verticalAxisLimits.x = _verticalAxisLimits.y;
        }

        private void CacheDefaultFov()
        {
            if (_cinemachineCamera == null) return;
            _defaultFov = _cinemachineCamera.Lens.FieldOfView;
            _hasDefaultFov = true;
        }

        private void RegisterZoomInput(float delta)
        {
            if (!IsActiveCamera()) return;
            if (Mathf.Abs(delta) <= 0.0001f) return;
            _zoomRequested = true;
            _lastZoomInputTime = Time.unscaledTime;
        }

        private void UpdateZoom()
        {
            if (!IsActiveCamera()) return;
            if (_cinemachineCamera == null) return;
            if (!_hasDefaultFov) CacheDefaultFov();

            // Keep sight zoom while pinch fingers are still on screen.
            bool isHoldingPinchTouch = Twinny.Mobile.Input.MobileInputProvider.CurrentData.TouchCount >= 2;
            bool zoomActive = _zoomRequested &&
                ((Time.unscaledTime - _lastZoomInputTime) <= _zoomReleaseDelay || isHoldingPinchTouch);

#if UNITY_EDITOR
            if (UnityEngine.Input.GetMouseButton(2))
                zoomActive = true;
#endif

            float target = zoomActive ? _zoomFov : _defaultFov;
            var lens = _cinemachineCamera.Lens;
            float current = lens.FieldOfView;
            float step = _zoomSpeed * Time.unscaledDeltaTime;
            lens.FieldOfView = Mathf.MoveTowards(current, target, step);
            _cinemachineCamera.Lens = lens;

            if (!zoomActive && Mathf.Approximately(lens.FieldOfView, _defaultFov))
                _zoomRequested = false;
        }

        private bool IsActiveCamera()
        {
            if (!_isModeActive) return false;
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

    }
}
