using System.Reflection;
using System.Collections.Generic;
using System.Collections;
using Concept.Core;
using Twinny.Core.Input;
using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.SceneManagement;

namespace Twinny.Mobile.Cameras
{
    /// <summary>
    /// Editor/mobile input bridge for Cinemachine Orbital Follow + Hard Look At setups.
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera))]
    [RequireComponent(typeof(CinemachineOrbitalFollow))]
    public class MobileCinemachineOrbitalHandler : MonoBehaviour, IMobileInputCallbacks, ITwinnyMobileCallbacks
    {
        private static int s_activeInstanceCount;

        public static bool HasActiveInstance => s_activeInstanceCount > 0;

        public enum PanTargetMode
        {
            CameraTransform,
            TrackingTarget,
            LookAtTarget,
            CustomTransform
        }

        [Header("Cinemachine")]
        [SerializeField] private CinemachineCamera _cinemachineCamera;
        [SerializeField] private CinemachineOrbitalFollow _orbitalFollow;
        [SerializeField] private CinemachineTracker _pointOfInterest;

        [Header("Mode")]
        [SerializeField] private int _activePriority = 20;
        [SerializeField] private int _inactivePriority = 5;

        [Header("Tuning")]
        [SerializeField] private float _rotateSpeed = 0.1f;
        [SerializeField] private float _tiltSpeed = 0.1f;
        [SerializeField] private bool _returnTrackingTargetToOriginOnRelease = false;
        [SerializeField] private float _panSpeed = 4.2f;
        [SerializeField] private float _panReturnSpeed = 3f;
        [SerializeField] private float _zoomSpeed = 3f;
        [SerializeField] private bool _lockRotationWhileTwoFingerPan = true;
        [SerializeField] private float _hardLookRestoreDelay = 0.08f;
        [SerializeField] private float _radiusTransitionSpeed = 29.25f;
        [SerializeField] private float _radiusTransitionEpsilon = 0.01f;
        [SerializeField] private float _radiusEaseOutDistance = 1.5f;
        [SerializeField] private float _radiusEaseOutSmoothTime = 0.12f;
        [SerializeField] private bool _applyFloorRotationOnSelect = false;
        [SerializeField] private float _rotationTransitionSpeed = 270f;
        [SerializeField] private float _rotationTransitionEpsilon = 0.2f;
        [SerializeField] private bool _moveLookAtWithFloorTarget = true;
        [SerializeField] private float _floorTargetTransitionSpeed = 14.4f;
        [SerializeField] private float _floorTargetTransitionEpsilon = 0.01f;
        [SerializeField] private bool _enablePanLimit;
        [SerializeField] private float _maxPanDistance = 10f;
        [SerializeField] private Vector2 _verticalAxisLimits = new Vector2(-80f, 80f);
        [SerializeField] private Vector2 _radiusLimits = new Vector2(0.5f, 50f);
        [SerializeField] private PanTargetMode _panTargetMode = PanTargetMode.TrackingTarget;
        [SerializeField] private bool _lockPanX;
        [SerializeField] private bool _lockPanY = true;
        [SerializeField] private bool _lockPanZ;
        [SerializeField] private Transform _customPanTarget;
        [SerializeField] private float _maxWallHeight = 3.0f;
        private Vector2 _defaultVerticalAxisLimits;
        private Vector2 _defaultRadiusLimits;
        private float _defaultMaxPanDistance;
        private bool _defaultEnablePanLimit;
        private bool _defaultLockPanX;
        private bool _defaultLockPanY;
        private bool _defaultLockPanZ;
        private CinemachineDeoccluder m_deoccluder;
        private float _defaultDeoccluderRadius;
        private bool _hasDefaultDeoccluderRadius;
        private bool _restoreDeoccluderAfterFloorTransition;

        private PropertyInfo _radiusProperty;
        private FieldInfo _radiusField;
        private bool _warnedMissingRadius;
        private bool _isPanning;
        private bool _isReturningPan;
        private Vector3 _panOriginPosition;
        private Vector3 _panReturnVelocity;
        private bool _isModeActive = true;
        private Vector3 _initialPanTargetPosition;
        private bool _hasInitialPosition;
        private Vector3 _lastValidPanForward = Vector3.forward;
        private float _panLockHorizontalAxis;
        private float _panLockVerticalAxis;
        private bool _hasPanLockAxes;
        private readonly List<SuspendedHardLookState> _suspendedHardLookStates = new List<SuspendedHardLookState>();
        private bool _isHardLookSuspended;
        private Transform _suspendedLookAtTarget;
        private Coroutine _hardLookRestoreRoutine;
        private CinemachineFloor _selectedFloor;
        private bool _isRadiusTransitioning;
        private float _targetRadius;
        private float _radiusTransitionVelocity;
        private bool _deferRadiusClampUntilFloorRadiusSettles;
        private bool _isRotationTransitioning;
        private bool _hasPendingRotationTransition;
        private float _targetHorizontalAxis;
        private float _targetVerticalAxis;
        private float _rotationHorizontalVelocity;
        private float _rotationVerticalVelocity;
        private bool _isFloorTargetTransitioning;
        private Vector3 _floorTargetStartPosition;
        private Vector3 _targetPanPosition;
        private CinemachineTracker _activePoi;
        private Transform _activeTrackingTarget;
        private CinemachineTracker _trackingTargetPoi;
        private bool _notifyPoiFocusedOnTransitionComplete;

        private struct SuspendedHardLookState
        {
            public Behaviour behaviour;
            public bool wasEnabled;
        }

        private void Update()
        {
            if (!IsActiveCamera()) return;
            RefreshTargetOverrides();
            EnforceRotationLockWhilePanning();
            UpdatePanReturn();
            UpdateFloorTargetTransition();
            UpdateRadiusTransition();
            UpdateRotationTransition();
            TryNotifyFloorFocused();
        }

        private void OnEnable()
        {
            s_activeInstanceCount++;
            EnsureReferences();
            CacheDeoccluderMembers();
            CacheDefaultSettings();
            CacheOrbitalMembers();
            InitializePanLimit();
            RefreshTargetOverrides(forceRefresh: true);
            ApplyMode(_isModeActive);
            CallbackHub.RegisterCallback<IMobileInputCallbacks>(this);
            CallbackHub.RegisterCallback<ITwinnyMobileCallbacks>(this);
        }

        private void OnDisable()
        {
            s_activeInstanceCount = Mathf.Max(0, s_activeInstanceCount - 1);
            if (_hardLookRestoreRoutine != null)
            {
                StopCoroutine(_hardLookRestoreRoutine);
                _hardLookRestoreRoutine = null;
            }
            RestoreHardLookAfterPan();
            CallbackHub.UnregisterCallback<IMobileInputCallbacks>(this);
            CallbackHub.UnregisterCallback<ITwinnyMobileCallbacks>(this);
        }

        private void OnValidate()
        {
            EnsureReferences();
            ClampLimits();
            CacheDeoccluderMembers();
            CacheDefaultSettings();
            CacheOrbitalMembers();
        }

        public void OnPrimaryDown(float x, float y)
        {
            if (IsInputBlockedDuringTransition())
                return;

            // Defensive recovery for editor/emulator edge cases where pan release was lost.
            if (_isPanning)
                EndPan(skipReturnToOrigin: true);
        }
        public void OnPrimaryUp(float x, float y)
        {
            if (IsInputBlockedDuringTransition())
                return;
        }
        public void OnSelect(SelectionData selection)
        {
            if (IsInputBlockedDuringTransition())
                return;

            if (selection.Target == null) return;

            CinemachineFloor floor = selection.Target.GetComponentInParent<CinemachineFloor>();
            if (floor == null) return;

            floor.Select();
        }
        public void OnCancel()
        {
            // Input providers may cancel when pointer/button state is lost (e.g., releasing outside Game View).
            // Ensure pan lock state is always cleared so single-finger rotation remains responsive.
            // Do not restore Hard Look immediately here to avoid a snap/twitch on focus-loss cancel.
            EndPan(skipReturnToOrigin: true);
        }

        public void OnPrimaryDrag(float dx, float dy)
        {
            if (IsInputBlockedDuringTransition())
                return;

            ApplyRotation(dx, dy);
        }

        public void OnZoom(float delta)
        {
            if (IsInputBlockedDuringTransition())
                return;

            ApplyZoom(delta);
        }

        public CinemachineTracker PointOfInterest
        {
            get => _pointOfInterest;
            set
            {
                if (_pointOfInterest == value) return;
                _pointOfInterest = value;
                RefreshTargetOverrides(forceRefresh: true);
            }
        }

        public CinemachineTracker ActivePointOfInterest => _activePoi;

        public void OnTwoFingerTap(Vector2 position) { }
        public void OnTwoFingerLongPress(Vector2 position) { }

        public void OnTwoFingerSwipe(Vector2 direction, Vector2 startPosition)
        {
            if (IsInputBlockedDuringTransition())
            {
                if (_isPanning)
                    EndPan(skipReturnToOrigin: true);
                return;
            }

            if (!IsActiveCamera()) return;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                EndPan();
                return;
            }

            BeginPanIfNeeded();
            ApplyPan(direction);
        }

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
        public void OnTilt(Vector3 tiltRotation) { }
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

        public void OnEnterImmersiveMode() {}
        public void OnExitImmersiveMode() {}
        public void OnEnterMockupMode() => ApplyMode(true);
        public void OnExitMockupMode() => ApplyMode(false);
        public void OnEnterDemoMode() { }
        public void OnExitDemoMode() { }

        public void OnFloorSelected(Interactables.Floor floor)
        {
            if (floor is not CinemachineFloor cinematicFloor)
                return;

            MoveTrackingTargetToFloor(cinematicFloor);
        }
        public void OnFloorFocused(Interactables.Floor floor) { }
        public void OnFloorUnselected(Interactables.Floor floor) { }

        private void ApplyRotation(float dx, float dy)
        {
            if (!IsActiveCamera()) return;
            if (_orbitalFollow == null) return;
            RestoreHardLookAfterPan();
            if (_lockRotationWhileTwoFingerPan && _isPanning) return;
            _isRotationTransitioning = false;

            var horizontal = _orbitalFollow.HorizontalAxis;
            horizontal.Value += dx * _rotateSpeed;
            _orbitalFollow.HorizontalAxis = horizontal;

            var vertical = _orbitalFollow.VerticalAxis;
            float next = vertical.Value - dy * _tiltSpeed;
            vertical.Value = Mathf.Clamp(next, _verticalAxisLimits.x, _verticalAxisLimits.y);
            _orbitalFollow.VerticalAxis = vertical;
        }

        private void ApplyPan(Vector2 direction)
        {
            if (!IsActiveCamera()) return;

            Transform panTarget = GetTrackingTarget();
            if (panTarget == null) return;

            if (!TryGetStablePanAxes(out Vector3 right, out Vector3 forward))
                return;
            
            // Initialize limit origin if not set yet
            if (_enablePanLimit && !_hasInitialPosition)
            {
                _initialPanTargetPosition = panTarget.position;
                _hasInitialPosition = true;
            }

            // Normalize sensitivity based on screen height (Reference: 1080p)
            float screenScale = 1080f / Mathf.Max(Screen.height, 1);

            // Dynamic speed based on zoom (radius)
            float zoomFactor = 1f;
            float currentRadius = GetRadius();
            // Use max radius as baseline to preserve the "perfect" speed at distance
            if (!float.IsNaN(currentRadius) && _radiusLimits.y > 0.001f)
            {
                zoomFactor = Mathf.Clamp(currentRadius / _radiusLimits.y, 0.01f, 1f);
            }

            // Invert input for natural "drag world" feel and scale for pixel coordinates
            Vector3 move = (right * -direction.x + forward * -direction.y) * (_panSpeed * 0.002f * screenScale * zoomFactor);
            Vector3 startPos = panTarget.position;
            Vector3 finalPos = ApplyPanAxisLocks(startPos + move, startPos);

            if (_enablePanLimit)
            {
                Vector3 offset = finalPos - _initialPanTargetPosition;
                offset = ApplyPanAxisLocks(offset, Vector3.zero);
                finalPos = _initialPanTargetPosition + Vector3.ClampMagnitude(offset, _maxPanDistance);
                finalPos = ApplyPanAxisLocks(finalPos, startPos);
            }

            panTarget.position = finalPos;
        }

        private bool TryGetStablePanAxes(out Vector3 right, out Vector3 forward)
        {
            right = Vector3.right;
            forward = Vector3.forward;
            bool lockYOnly = _lockPanY && !_lockPanX && !_lockPanZ;
            bool onlyYFree = _lockPanX && !_lockPanY && _lockPanZ;

            // Prefer orbital yaw: it's stable even when camera tilt approaches 90 degrees.
            if (lockYOnly && _orbitalFollow != null)
            {
                float yaw = _orbitalFollow.HorizontalAxis.Value;
                Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);
                forward = yawRot * Vector3.forward;
                right = yawRot * Vector3.right;
                _lastValidPanForward = forward;
                return true;
            }

            Transform reference = GetPanReference();
            if (reference == null) return false;

            Vector3 candidateForward;
            if (lockYOnly)
                candidateForward = Vector3.ProjectOnPlane(reference.forward, Vector3.up);
            else if (onlyYFree)
                candidateForward = Vector3.up;
            else
                candidateForward = reference.forward;

            if (candidateForward.sqrMagnitude > 0.0001f)
            {
                forward = candidateForward.normalized;
                _lastValidPanForward = forward;
            }
            else if (_lastValidPanForward.sqrMagnitude > 0.0001f)
            {
                forward = _lastValidPanForward.normalized;
            }
            else
            {
                forward = Vector3.forward;
            }

            right = reference.right.normalized;

            if (right.sqrMagnitude <= 0.0001f)
            {
                right = Vector3.right;
                forward = Vector3.forward;
            }

            return true;
        }

        private Vector3 ApplyPanAxisLocks(Vector3 value, Vector3 reference)
        {
            if (_lockPanX) value.x = reference.x;
            if (_lockPanY) value.y = reference.y;
            if (_lockPanZ) value.z = reference.z;
            return value;
        }

        private void BeginPanIfNeeded()
        {
            // Se estava voltando, cancela o retorno para dar prioridade ao dedo do usuário.
            bool wasReturning = _isReturningPan;
            _isReturningPan = false;

            if (_isPanning) return;
            _isPanning = true;
            Transform panTarget = GetTrackingTarget();
            // Só redefine a origem se não estivesse no meio de um retorno (evita drift da origem)
            if (panTarget != null && !wasReturning)
                _panOriginPosition = panTarget.position;

            CachePanLockAxes();
            SuspendHardLookWhilePanning();
        }

        private void EndPan(bool skipReturnToOrigin = false)
        {
            if (!_isPanning) return;
            _isPanning = false;
            _hasPanLockAxes = false;
            if (_hardLookRestoreRoutine != null)
                StopCoroutine(_hardLookRestoreRoutine);
            if (!skipReturnToOrigin && _returnTrackingTargetToOriginOnRelease && GetTrackingTarget() != null)
                _isReturningPan = true;
        }

        private void ScheduleHardLookRestore()
        {
            if (_hardLookRestoreRoutine != null)
                StopCoroutine(_hardLookRestoreRoutine);

            _hardLookRestoreRoutine = StartCoroutine(RestoreHardLookAfterPanDelayed());
        }

        private IEnumerator RestoreHardLookAfterPanDelayed()
        {
            float delay = Mathf.Max(0f, _hardLookRestoreDelay);
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            RestoreHardLookAfterPan();
            _hardLookRestoreRoutine = null;
        }

        private void SuspendHardLookWhilePanning()
        {
            if (_cinemachineCamera == null) return;
            if (_isHardLookSuspended) return;

            _isHardLookSuspended = true;
            _suspendedLookAtTarget = _cinemachineCamera.LookAt;
            _cinemachineCamera.LookAt = null;

            var components = _cinemachineCamera.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null) continue;

                // Avoid hard dependency on a specific Cinemachine version/type name.
                if (!component.GetType().Name.Contains("HardLookAt")) continue;
                if (component is not Behaviour behaviour) continue;

                SuspendedHardLookState state = new SuspendedHardLookState
                {
                    behaviour = behaviour,
                    wasEnabled = behaviour.enabled
                };

                _suspendedHardLookStates.Add(state);
                behaviour.enabled = false;
            }
        }

        private void RestoreHardLookAfterPan()
        {
            if (!_isHardLookSuspended) return;

            if (_cinemachineCamera != null) _cinemachineCamera.LookAt = _suspendedLookAtTarget;
            _suspendedLookAtTarget = null;
            _isHardLookSuspended = false;

            for (int i = 0; i < _suspendedHardLookStates.Count; i++)
            {
                SuspendedHardLookState state = _suspendedHardLookStates[i];
                Behaviour behaviour = state.behaviour;
                if (behaviour == null) continue;
                behaviour.enabled = state.wasEnabled;
            }

            _suspendedHardLookStates.Clear();
        }

        private void CachePanLockAxes()
        {
            if (_orbitalFollow == null) return;
            _panLockHorizontalAxis = _orbitalFollow.HorizontalAxis.Value;
            _panLockVerticalAxis = _orbitalFollow.VerticalAxis.Value;
            _hasPanLockAxes = true;
        }

        private void EnforceRotationLockWhilePanning()
        {
            if (!_lockRotationWhileTwoFingerPan || !_isPanning) return;
            if (_orbitalFollow == null || !_hasPanLockAxes) return;

            var horizontal = _orbitalFollow.HorizontalAxis;
            horizontal.Value = _panLockHorizontalAxis;
            _orbitalFollow.HorizontalAxis = horizontal;

            var vertical = _orbitalFollow.VerticalAxis;
            vertical.Value = _panLockVerticalAxis;
            _orbitalFollow.VerticalAxis = vertical;
        }

        private void ApplyZoom(float delta)
        {
            if (!IsActiveCamera()) return;
            if (_orbitalFollow == null) return;
            RestoreHardLookAfterPan();

            float radius = GetRadius();
            if (float.IsNaN(radius))
            {
                WarnMissingRadiusOnce();
                return;
            }

            float next = radius - delta * _zoomSpeed;
            _isRadiusTransitioning = false;
            SetRadius(Mathf.Clamp(next, _radiusLimits.x, _radiusLimits.y));
            // Test mode: keep HardLook suspended after floor transition.
        }

        private Transform GetPanReference()
        {
            EnsureReferences();
            if (_cinemachineCamera != null) return _cinemachineCamera.transform;
            if (UnityEngine.Camera.main != null) return UnityEngine.Camera.main.transform;
            return null;
        }

        private void EnsureReferences()
        {
            if (_cinemachineCamera == null)
                _cinemachineCamera = GetComponent<CinemachineCamera>();

            if (_orbitalFollow == null && _cinemachineCamera != null)
                _orbitalFollow = _cinemachineCamera.GetComponent<CinemachineOrbitalFollow>();

            if (_orbitalFollow == null)
                _orbitalFollow = GetComponent<CinemachineOrbitalFollow>();
        }

        private void ApplyMode(bool isActive)
        {
            if(isActive) CallbackHub.CallAction<IMobileUICallbacks>(callback => callback.OnMaxWallHeightRequested(_maxWallHeight));
            _isModeActive = isActive;
            if (_cinemachineCamera != null)
                _cinemachineCamera.Priority = isActive ? _activePriority : _inactivePriority;
        }

        private void ClampLimits()
        {
            if (_verticalAxisLimits.y < _verticalAxisLimits.x)
                _verticalAxisLimits.x = _verticalAxisLimits.y;

            if (_radiusLimits.y < _radiusLimits.x)
                _radiusLimits.x = _radiusLimits.y;
        }

        private void CacheDefaultSettings()
        {
            _defaultVerticalAxisLimits = _verticalAxisLimits;
            _defaultRadiusLimits = _radiusLimits;
            _defaultMaxPanDistance = _maxPanDistance;
            _defaultEnablePanLimit = _enablePanLimit;
            _defaultLockPanX = _lockPanX;
            _defaultLockPanY = _lockPanY;
            _defaultLockPanZ = _lockPanZ;

            float deoccluderRadius = GetDeoccluderRadius();
            if (!float.IsNaN(deoccluderRadius))
            {
                _defaultDeoccluderRadius = deoccluderRadius;
                _hasDefaultDeoccluderRadius = true;
            }
        }

        private void RefreshTargetOverrides(bool forceRefresh = false)
        {
            Transform trackingTarget = GetTrackingTarget();
            _trackingTargetPoi = trackingTarget != null ? trackingTarget.GetComponent<CinemachineTracker>() : null;
            CinemachineTracker poi = ResolveActivePointOfInterest(trackingTarget);
            bool targetChanged = _activeTrackingTarget != trackingTarget;
            if (!forceRefresh && _activePoi == poi && !targetChanged) return;

            _activeTrackingTarget = trackingTarget;
            _activePoi = poi;
            ApplyPoiOverrides(_activePoi);
            SyncOrbitalStateToCurrentLimits();
            InitializePanLimitForTarget(trackingTarget, targetChanged);

            if (_isModeActive)
            {
                CallbackHub.CallAction<IMobileUICallbacks>(
                    callback => callback.OnMaxWallHeightRequested(_maxWallHeight)
                );
            }
        }

        private CinemachineTracker ResolveActivePointOfInterest(Transform trackingTarget)
        {
            if (_pointOfInterest != null)
                return _pointOfInterest;

            if (_selectedFloor != null)
            {
                if (!_selectedFloor.UseFocusPoint || _selectedFloor.TrackerPoint == null)
                    return null;

                return _selectedFloor.TrackerPoint;
            }

            return trackingTarget != null ? trackingTarget.GetComponent<CinemachineTracker>() : null;
        }

        private void ApplyPoiOverrides(CinemachineTracker poi)
        {
            _verticalAxisLimits = _defaultVerticalAxisLimits;
            _radiusLimits = _defaultRadiusLimits;
            _maxPanDistance = _defaultMaxPanDistance;
            _enablePanLimit = _defaultEnablePanLimit;
            _lockPanX = _defaultLockPanX;
            _lockPanY = _defaultLockPanY;
            _lockPanZ = _defaultLockPanZ;

            if (poi == null)
            {
                ClampLimits();
                return;
            }

            if (poi.HasVerticalAxisLimitsOverride)
                _verticalAxisLimits = poi.VerticalAxisLimits;

            if (poi.HasRadiusLimitsOverride)
                _radiusLimits = poi.RadiusLimits;

            if (poi.HasMaxPanDistanceOverride)
                _maxPanDistance = poi.MaxPanDistance;

            if (poi.HasEnablePanLimitOverride)
                _enablePanLimit = poi.EnablePanLimitValue;

            if (poi.HasPanConstraintOverride)
            {
                _lockPanX = poi.LockPanX;
                _lockPanY = poi.LockPanY;
                _lockPanZ = poi.LockPanZ;
            }

            ClampLimits();
        }

        private void SyncOrbitalStateToCurrentLimits()
        {
            if (_orbitalFollow != null && !_isRotationTransitioning)
            {
                var vertical = _orbitalFollow.VerticalAxis;
                vertical.Value = Mathf.Clamp(vertical.Value, _verticalAxisLimits.x, _verticalAxisLimits.y);
                _orbitalFollow.VerticalAxis = vertical;
            }

            if (!_isRadiusTransitioning && !_deferRadiusClampUntilFloorRadiusSettles)
            {
                float radius = GetRadius();
                if (!float.IsNaN(radius))
                    SetRadius(Mathf.Clamp(radius, _radiusLimits.x, _radiusLimits.y));
            }

            _targetRadius = Mathf.Clamp(_targetRadius, _radiusLimits.x, _radiusLimits.y);
            _targetVerticalAxis = Mathf.Clamp(_targetVerticalAxis, _verticalAxisLimits.x, _verticalAxisLimits.y);
        }

        private void CacheOrbitalMembers()
        {
            if (_orbitalFollow == null) return;

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var type = _orbitalFollow.GetType();

            _radiusProperty = type.GetProperty("Radius", flags)
                ?? type.GetProperty("OrbitRadius", flags);

            _radiusField = type.GetField("Radius", flags)
                ?? type.GetField("OrbitRadius", flags);
        }

        private void CacheDeoccluderMembers()
        {
            if (_cinemachineCamera == null) return;
            if (m_deoccluder == null)
                m_deoccluder = _cinemachineCamera.GetComponent<CinemachineDeoccluder>();
        }

        private float GetDeoccluderRadius()
        {
            if (m_deoccluder == null) return float.NaN;
            return m_deoccluder.AvoidObstacles.CameraRadius;
        }

        private void SetDeoccluderRadius(float value)
        {
            if (m_deoccluder == null) return;
            var avoidObstacles = m_deoccluder.AvoidObstacles;
            avoidObstacles.CameraRadius = value;
            m_deoccluder.AvoidObstacles = avoidObstacles;
        }

        private void SetDeoccluderEnabledForFloorTransition(bool enabled)
        {
            if (m_deoccluder == null) return;

            if (!enabled)
            {
                _restoreDeoccluderAfterFloorTransition = m_deoccluder.enabled;
                m_deoccluder.enabled = false;
                return;
            }

            if (_restoreDeoccluderAfterFloorTransition)
                m_deoccluder.enabled = true;

            _restoreDeoccluderAfterFloorTransition = false;
        }

        private float GetRadius()
        {
            if (_orbitalFollow != null)
                return _orbitalFollow.Radius;

            if (_radiusProperty != null)
                return (float)_radiusProperty.GetValue(_orbitalFollow);

            if (_radiusField != null)
                return (float)_radiusField.GetValue(_orbitalFollow);

            return float.NaN;
        }

        private void SetRadius(float value)
        {
            if (_orbitalFollow != null)
            {
                _orbitalFollow.Radius = value;
                return;
            }

            if (_radiusProperty != null)
            {
                _radiusProperty.SetValue(_orbitalFollow, value);
                return;
            }

            if (_radiusField != null)
                _radiusField.SetValue(_orbitalFollow, value);
        }

        private void WarnMissingRadiusOnce()
        {
            if (_warnedMissingRadius) return;
            _warnedMissingRadius = true;
            Debug.LogWarning(
                "[MobileCinemachineOrbitalHandler] Could not find radius field on CinemachineOrbitalFollow."
            );
        }

        private void UpdatePanReturn()
        {
            if (!IsActiveCamera()) return;
            Transform panTarget = GetTrackingTarget();
            if (!_isReturningPan || panTarget == null) return;

            panTarget.position = Vector3.SmoothDamp(
                panTarget.position,
                _panOriginPosition,
                ref _panReturnVelocity,
                1f / Mathf.Max(0.01f, _panReturnSpeed)
            );

            if (Vector3.SqrMagnitude(panTarget.position - _panOriginPosition) <= 0.0001f)
            {
                panTarget.position = _panOriginPosition;
                _panReturnVelocity = Vector3.zero;
                _isReturningPan = false;
            }
        }

        private void MoveTrackingTargetToFloor(CinemachineFloor floor)
        {
            Transform panTarget = GetTrackingTarget();
            if (panTarget == null) return;
            if (floor == null) return;
            CinemachineTracker targetPoi = floor.TrackerPoint;
            CinemachineFloor previousFloor = _selectedFloor;

            _selectedFloor = floor;
            _pointOfInterest = floor.UseFocusPoint ? targetPoi : null;
            _deferRadiusClampUntilFloorRadiusSettles = true;
            RefreshTargetOverrides(forceRefresh: true);
            ApplyDeoccluderRadiusOverride(floor);

            if (previousFloor != null && previousFloor != floor)
            {

                previousFloor.Unselect();
            }

            _isReturningPan = false;
            _panReturnVelocity = Vector3.zero;
            _isRotationTransitioning = false;
            _rotationHorizontalVelocity = 0f;
            _rotationVerticalVelocity = 0f;
            RestoreHardLookAfterPan();
            SetDeoccluderEnabledForFloorTransition(false);

            _floorTargetStartPosition = panTarget.position;
            _targetPanPosition = floor.TargetPosition;
            _isFloorTargetTransitioning = true;
            _notifyPoiFocusedOnTransitionComplete = true;

            _panOriginPosition = _targetPanPosition;
            _initialPanTargetPosition = _targetPanPosition;
            _hasInitialPosition = true;

            float desiredRadius = targetPoi != null ? targetPoi.TargetRadius : GetRadius();
            float clampedRadius = Mathf.Clamp(desiredRadius, _radiusLimits.x, _radiusLimits.y);
            _targetRadius = clampedRadius;
            _isRadiusTransitioning = true;
            _radiusTransitionVelocity = 0f;

            bool shouldApplyRotationOverride = _applyFloorRotationOnSelect || (targetPoi != null && targetPoi.OverrideRotation);
            if (shouldApplyRotationOverride)
            {
                Vector3 targetEuler = floor.TargetRotation.eulerAngles;
                float targetPanDegrees = targetPoi != null && targetPoi.HasTargetPanOverride
                    ? Mathf.Rad2Deg * targetPoi.TargetPan
                    : NormalizeSignedAngle(targetEuler.y);
                float targetTiltDegrees = targetPoi != null && targetPoi.HasTargetTiltOverride
                    ? Mathf.Rad2Deg * targetPoi.TargetTilt
                    : NormalizeSignedAngle(targetEuler.x);

                _targetHorizontalAxis = NormalizeSignedAngle(targetPanDegrees);
                _targetVerticalAxis = Mathf.Clamp(targetTiltDegrees, _verticalAxisLimits.x, _verticalAxisLimits.y);
                _hasPendingRotationTransition = false;
                _isRotationTransitioning = true;
            }
            else
            {
                _hasPendingRotationTransition = false;
                _isRotationTransitioning = false;
            }

            _maxWallHeight = floor.MaxWallHeight;
            CallbackHub.CallAction<IMobileUICallbacks>(
                callback => callback.OnMaxWallHeightRequested(_maxWallHeight)
            );

            TryNotifyFloorFocused();
        }

        private void ApplyDeoccluderRadiusOverride(CinemachineFloor floor)
        {
            if (!_hasDefaultDeoccluderRadius)
            {
                float currentRadius = GetDeoccluderRadius();
                if (!float.IsNaN(currentRadius))
                {
                    _defaultDeoccluderRadius = currentRadius;
                    _hasDefaultDeoccluderRadius = true;
                }
            }

            if (!_hasDefaultDeoccluderRadius) return;

            CinemachineTracker focusPoi = null;
            if (floor != null && floor.UseFocusPoint)
                focusPoi = floor.TrackerPoint;

            if (focusPoi != null && focusPoi.HasDeoccluderRadiusOverride)
            {
                SetDeoccluderRadius(focusPoi.OverrideDeoccluderRadius);
                return;
            }

            SetDeoccluderRadius(_defaultDeoccluderRadius);
        }

        private void UpdateRadiusTransition()
        {
            if (!_isRadiusTransitioning) return;

            float currentRadius = GetRadius();
            if (float.IsNaN(currentRadius))
            {
                _isRadiusTransitioning = false;
                _deferRadiusClampUntilFloorRadiusSettles = false;
                WarnMissingRadiusOnce();
                return;
            }

            float remaining = Mathf.Abs(currentRadius - _targetRadius);
            float nextRadius;

            if (remaining <= Mathf.Max(_radiusEaseOutDistance, _radiusTransitionEpsilon))
            {
                float smoothTime = Mathf.Max(0.01f, _radiusEaseOutSmoothTime);
                nextRadius = Mathf.SmoothDamp(
                    currentRadius,
                    _targetRadius,
                    ref _radiusTransitionVelocity,
                    smoothTime,
                    Mathf.Infinity,
                    Time.deltaTime
                );
            }
            else
            {
                float step = Mathf.Max(_radiusTransitionSpeed, 0f) * Time.deltaTime;
                nextRadius = Mathf.MoveTowards(currentRadius, _targetRadius, step);
                _radiusTransitionVelocity = 0f;
            }

            SetRadius(nextRadius);

            if (Mathf.Abs(nextRadius - _targetRadius) <= _radiusTransitionEpsilon)
            {
                SetRadius(_targetRadius);
                _isRadiusTransitioning = false;
                _radiusTransitionVelocity = 0f;
                _deferRadiusClampUntilFloorRadiusSettles = false;
                // Test mode: keep HardLook suspended after floor transition.
            }
        }

        private void UpdateFloorTargetTransition()
        {
            if (!_isFloorTargetTransitioning) return;

            Transform panTarget = GetTrackingTarget();
            if (panTarget == null)
            {
                _isFloorTargetTransitioning = false;
                SetDeoccluderEnabledForFloorTransition(true);
                return;
            }

            float step = Mathf.Max(_floorTargetTransitionSpeed, 0f) * Time.deltaTime;
            panTarget.position = Vector3.MoveTowards(panTarget.position, _targetPanPosition, step);

            if (Vector3.SqrMagnitude(panTarget.position - _targetPanPosition) <= _floorTargetTransitionEpsilon * _floorTargetTransitionEpsilon)
            {
                panTarget.position = _targetPanPosition;
                _isFloorTargetTransitioning = false;
                SetDeoccluderEnabledForFloorTransition(true);
            }
        }

        private void UpdateRotationTransition()
        {
            if (!_isRotationTransitioning) return;
            if (_orbitalFollow == null)
            {
                _isRotationTransitioning = false;
                _rotationHorizontalVelocity = 0f;
                _rotationVerticalVelocity = 0f;
                return;
            }

            var horizontal = _orbitalFollow.HorizontalAxis;
            var vertical = _orbitalFollow.VerticalAxis;

            float currentHorizontal = NormalizeSignedAngle(horizontal.Value);
            float smoothTime = Mathf.Clamp(90f / Mathf.Max(_rotationTransitionSpeed, 0.01f), 0.08f, 0.35f);
            if (_isFloorTargetTransitioning)
            {
                float progress = GetFloorTargetTransitionProgress();
                smoothTime *= Mathf.Lerp(2.2f, 1f, progress);
            }
            float nextHorizontal = Mathf.SmoothDampAngle(
                currentHorizontal,
                _targetHorizontalAxis,
                ref _rotationHorizontalVelocity,
                smoothTime,
                Mathf.Infinity,
                Time.deltaTime
            );
            float nextVertical = Mathf.SmoothDamp(
                vertical.Value,
                _targetVerticalAxis,
                ref _rotationVerticalVelocity,
                smoothTime,
                Mathf.Infinity,
                Time.deltaTime
            );

            horizontal.Value = nextHorizontal;
            vertical.Value = nextVertical;
            _orbitalFollow.HorizontalAxis = horizontal;
            _orbitalFollow.VerticalAxis = vertical;

            bool horizontalDone = Mathf.Abs(Mathf.DeltaAngle(nextHorizontal, _targetHorizontalAxis)) <= _rotationTransitionEpsilon;
            bool verticalDone = Mathf.Abs(nextVertical - _targetVerticalAxis) <= _rotationTransitionEpsilon;

            if (horizontalDone && verticalDone)
            {
                horizontal.Value = _targetHorizontalAxis;
                vertical.Value = _targetVerticalAxis;
                _orbitalFollow.HorizontalAxis = horizontal;
                _orbitalFollow.VerticalAxis = vertical;
                _isRotationTransitioning = false;
                _rotationHorizontalVelocity = 0f;
                _rotationVerticalVelocity = 0f;
                // Test mode: keep HardLook suspended after floor transition.
            }
        }

        private float GetFloorTargetTransitionProgress()
        {
            float totalDistance = Vector3.Distance(_floorTargetStartPosition, _targetPanPosition);
            if (totalDistance <= _floorTargetTransitionEpsilon)
                return 1f;

            Transform panTarget = GetTrackingTarget();
            if (panTarget == null)
                return 1f;

            float remainingDistance = Vector3.Distance(panTarget.position, _targetPanPosition);
            return Mathf.Clamp01(1f - (remainingDistance / totalDistance));
        }

        private static float NormalizeSignedAngle(float angle)
        {
            if (angle > 180f) angle -= 360f;
            return angle;
        }

        private bool IsTransitionActive()
        {
            return _isFloorTargetTransitioning ||
                   _isRadiusTransitioning ||
                   _isRotationTransitioning ||
                   _hasPendingRotationTransition;
        }

        private bool IsInputBlockedDuringTransition()
        {
            return IsActiveCamera() && IsTransitionActive();
        }

        private void TryNotifyFloorFocused()
        {

            if (!_notifyPoiFocusedOnTransitionComplete)
                return;

            if (IsTransitionActive())
                return;

            _selectedFloor?.Focus();
            _notifyPoiFocusedOnTransitionComplete = false;
        }


        private Transform GetPanTarget()
        {
            EnsureReferences();
            switch (_panTargetMode)
            {
                case PanTargetMode.CameraTransform:
                    return _cinemachineCamera != null ? _cinemachineCamera.transform : null;
                case PanTargetMode.TrackingTarget:
                    return _cinemachineCamera != null ? _cinemachineCamera.Follow : null;
                case PanTargetMode.LookAtTarget:
                    return _cinemachineCamera != null ? _cinemachineCamera.LookAt : null;
                case PanTargetMode.CustomTransform:
                    return _customPanTarget;
                default:
                    return null;
            }
        }

        private void InitializePanLimit()
        {
            InitializePanLimitForTarget(GetTrackingTarget(), true);
        }

        private void InitializePanLimitForTarget(Transform target, bool forceReset)
        {
            if (target == null)
            {
                _hasInitialPosition = false;
                return;
            }

            if (forceReset || !_hasInitialPosition)
            {
                _initialPanTargetPosition = target.position;
                _hasInitialPosition = true;
            }
        }


        private Transform GetTrackingTarget()
        {
            EnsureReferences();
            return _cinemachineCamera != null ? _cinemachineCamera.Follow : null;
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
