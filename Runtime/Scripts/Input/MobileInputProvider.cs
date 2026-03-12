using Twinny.Core.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System;
using Concept.Core;
using UnityInput = UnityEngine.Input;
using TouchPhase = UnityEngine.TouchPhase;
using Concept.Helpers;

namespace Twinny.Mobile.Input
{
    /// <summary>
    /// Provides mobile input handling, including multitouch gestures, edge swipes, and device sensor events.
    /// Integrates with the InputRouter and CallbackHub for processing input callbacks.
    /// Also provides static Action events for selective subscription.
    /// </summary>
    public class MobileInputProvider : TSingleton<MobileInputProvider>
    {
        private const string SettingsResourceName = "MobileInputSettings";

        [SerializeField] private MobileInputSettings _settings;

        // Internal state variables
        private Vector2 _touchStartPos;
        private float _touchStartTime;
        private Vector2 _lastMousePos;
        private bool _isDragging;
        private float _lastPinchDist;
        private DeviceOrientation _lastOrientation;
        private bool _isZooming;
        private bool _isPanning;
        private float _startPinchDist;
        private Vector2 _startPanCenter;
        private Vector2 _lastPanCenter;

        // New state variables for missing features
        private Dictionary<int, TouchHistory> _touchHistories = new Dictionary<int, TouchHistory>();
        private float _lastShakeTime;
        private Vector3 _lastAcceleration;
        private bool _isDevicePickedUp;
        private float _devicePutDownTime;
        private float _lastTwoFingerPressTime;
        private bool _twoFingerLongPressDetected;
        private Vector2 _lastThreeFingerCenter;
        private float _lastThreeFingerDistance;
        private Vector3 _lastDeviceRotation;
        [SerializeField] private float _gyroTiltThreshold = 0.5f;
        [SerializeField] private float _twoFingerSwipeSensitivity = 10.0f;
        private bool _suppressSingleTouchUntilAllReleased;
        private bool _isScreenReaderActive;
        private bool _warnedMissingSettings;
        private bool _warnedMissingRouter;
        private UIDocument[] _uiDocuments;
        private int _uiDocumentsFrame;
        private readonly HashSet<int> _uiBlockedTouchIds = new HashSet<int>();
        private bool _mouseBlockedByUi;

#if (UNITY_ANDROID || UNITY_IOS || UNITY_WEBGL) && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
#if UNITY_WEBGL
            if (!IsWebGlMobileBrowser()) return;
#endif
            if (FindAnyObjectByType<MobileInputProvider>() != null) return;
            var providerObject = new GameObject("MobileInputProvider");
            providerObject.AddComponent<MobileInputProvider>();
            DontDestroyOnLoad(providerObject);
        }
#endif

        private struct TouchHistory
        {
            public Vector2 startPosition;
            public float startTime;
            public bool isLongPressTriggered;
        }

        #region Current Data
        /// <summary>
        /// Current input state data accessible from anywhere
        /// </summary>
        public static class CurrentData
        {
            public static int TouchCount => UnityInput.touchCount;
            public static Touch[] Touches => UnityInput.touches;
            public static Vector3 Acceleration => UnityInput.acceleration;
            public static DeviceOrientation Orientation
            {
                get
                {
#if UNITY_WEBGL && !UNITY_EDITOR
                    return DeviceOrientation.Unknown;
#else
                    return UnityInput.deviceOrientation;
#endif
                }
            }
            public static bool IsDragging => Instance?._isDragging ?? false;
            public static bool IsDevicePickedUp => Instance?._isDevicePickedUp ?? false;
            public static bool IsScreenReaderActive => Instance?._isScreenReaderActive ?? false;

            public static Vector2? PrimaryTouchPosition
            {
                get
                {
                    if (UnityInput.touchCount > 0)
                        return UnityInput.GetTouch(0).position;
                    return null;
                }
            }

            public static float? TouchPressure
            {
                get
                {
                    if (UnityInput.touchCount > 0 && UnityInput.touchPressureSupported)
                        return UnityInput.GetTouch(0).pressure;
                    return null;
                }
            }

            public static float? TimeSinceTouchStart
            {
                get
                {
                    if (Instance != null && UnityInput.touchCount > 0)
                        return Time.time - Instance._touchStartTime;
                    return null;
                }
            }

            public static Vector2? TouchStartPosition => Instance?._touchStartPos;

            public static Vector3? GyroRotation
            {
                get
                {
#if UNITY_WEBGL && !UNITY_EDITOR
                    if (Instance != null && WebGLGyroAPI.IsInitialized)
                        return WebGLGyroAPI.GetRotation().eulerAngles;
                    return null;
#else
                    if (Instance != null && Instance.CanUseGyroscope() && UnityInput.gyro.enabled)
                        return UnityInput.gyro.attitude.eulerAngles;
                    return null;
#endif
                }
            }
        }
        #endregion


        /// <summary>
        /// Initializes the device orientation and enables gyroscope if supported.
        /// </summary>

        protected override void Awake()
        {
            base.Awake();
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!IsWebGlMobileBrowser())
            {
                Destroy(gameObject);
                return;
            }
#endif
        }

        protected override void Start()
        {
            base.Start();
            EnsureSettingsLoaded();
            _lastOrientation = CanUseDeviceOrientation() ? UnityInput.deviceOrientation : DeviceOrientation.Unknown;
            if (CanUseGyroscope())
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                _lastDeviceRotation = Vector3.zero;
#else
                UnityInput.gyro.enabled = true;
                _lastDeviceRotation = UnityInput.gyro.attitude.eulerAngles;
#endif
            }

            _lastAcceleration = UnityInput.acceleration;
            _isDevicePickedUp = false;

            // Check for screen reader/accessibility
#if UNITY_ANDROID && !UNITY_EDITOR
            CheckAndroidAccessibility();
#elif UNITY_IOS && !UNITY_EDITOR
            CheckIOSAccessibility();
#endif
        }

        /// <summary>
        /// Main update loop for touch input and device sensor detection.
        /// </summary>

        protected override void Update()
        {
            base.Update();
#if UNITY_EDITOR
            return;
#endif
            EnsureSettingsLoaded();

            int touchCount = UnityInput.touchCount;

            if (touchCount == 0)
                _suppressSingleTouchUntilAllReleased = false;
            else if (touchCount > 1)
                _suppressSingleTouchUntilAllReleased = true;

            // Handle touch gestures based on finger count
            if (touchCount == 1)
            {
                if (_suppressSingleTouchUntilAllReleased)
                    HandleSuppressedSingleTouch();
                else
                    HandleSingleTouch();
            }
            else if (touchCount == 2) HandleTwoFingers();
            else if (touchCount == 3) HandleThreeFingers();
            else if (touchCount == 4) HandleFourFingers();
            else if (touchCount == 0) HandleMouseInput();

            // Update touch histories for long press detection
            UpdateTouchHistories();

            // Detect device motion and edge gestures
            DetectDevicePhysics();
            DetectEdgeGestures();
            DetectPickupAndPutdown();

            // Check for accessibility actions
            CheckAccessibilityActions();
        }

        private void HandleSuppressedSingleTouch()
        {
            if (UnityInput.touchCount != 1)
                return;

            Touch t = UnityInput.GetTouch(0);
            if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
            {
                _touchHistories.Remove(t.fingerId);
                _uiBlockedTouchIds.Remove(t.fingerId);
            }
        }

        private bool IsPointerOverUi()
        {
            if (UnityInput.touchCount <= 0)
            {
                return IsPointerOverUi(-1, UnityInput.mousePosition);
            }

            for (int i = 0; i < UnityInput.touchCount; i++)
            {
                Touch touch = UnityInput.GetTouch(i);
                if (IsPointerOverUi(touch.fingerId, touch.position))
                    return true;
            }

            return false;
        }

        private bool IsPointerOverUi(int pointerId, Vector2 screenPosition)
        {
            if (EventSystem.current?.IsPointerOverGameObject(pointerId) == true)
                return true;

            return IsPointerOverUiToolkit(screenPosition);
        }

        private bool IsPointerOverUiToolkit(Vector2 screenPosition)
        {
            UIDocument[] documents = GetUiDocuments();
            if (documents == null || documents.Length == 0) return false;

            for (int i = 0; i < documents.Length; i++)
            {
                UIDocument document = documents[i];
                if (document == null || document.rootVisualElement == null) continue;

                var panel = document.rootVisualElement.panel;
                if (panel == null) continue;

                Vector2 panelPosition = RuntimePanelUtils.ScreenToPanel(panel, screenPosition);
                VisualElement picked = panel.Pick(panelPosition);
                if (picked != null)
                    return true;
            }

            return false;
        }

        private UIDocument[] GetUiDocuments()
        {
            if (_uiDocuments == null || (Time.frameCount - _uiDocumentsFrame) > 30)
            {
                _uiDocuments = FindObjectsOfType<UIDocument>();
                _uiDocumentsFrame = Time.frameCount;
            }

            return _uiDocuments;
        }

        #region Touch Handlers

        /// <summary>
        /// Processes single finger touches including tap, drag, and touch release.
        /// </summary>
        private void HandleSingleTouch()
        {
            if (_settings == null) return;

            var router = TryGetRouter();
            if (router == null) return;

            Touch t = UnityInput.GetTouch(0);

            if (t.phase == TouchPhase.Began)
            {
                if (IsPointerOverUi(t.fingerId, t.position))
                {
                    _uiBlockedTouchIds.Add(t.fingerId);
                    return;
                }

                _uiBlockedTouchIds.Remove(t.fingerId);
            }

            if (_uiBlockedTouchIds.Contains(t.fingerId))
            {
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                {
                    _uiBlockedTouchIds.Remove(t.fingerId);
                    _touchHistories.Remove(t.fingerId);
                }
                return;
            }

            switch (t.phase)
            {
                case TouchPhase.Began:
                    _touchStartPos = t.position;
                    _touchStartTime = Time.time;
                    _isDragging = false;

                    // Update touch history
                    _touchHistories[t.fingerId] = new TouchHistory
                    {
                        startPosition = t.position,
                        startTime = Time.time,
                        isLongPressTriggered = false
                    };

                    router.PrimaryDown(t.position.x, t.position.y);

                    // Haptic touch detection (quick tap with feedback)
                    if (t.tapCount == 1 && Time.time - _touchStartTime < 0.1f)
                    {
                        CallbackHub.CallAction<IMobileInputCallbacks>(cb => cb.OnHapticTouch());
                        MobileInputEvents.HapticTouch();
                    }
                    break;

                case TouchPhase.Moved:
                    if (Vector2.Distance(t.position, _touchStartPos) > _settings.DragThreshold)
                    {
                        _isDragging = true;
                        router.PrimaryDrag(t.deltaPosition.x, t.deltaPosition.y);
                        MobileInputEvents.Drag(t.deltaPosition, t.position);
                    }
                    break;

                case TouchPhase.Ended:
                    if (!_isDragging)
                    {
                        ProcessTap(t);
                        MobileInputEvents.Tap(t.position);
                    }
                    router.PrimaryUp(t.position.x, t.position.y);

                    // Remove from history
                    _touchHistories.Remove(t.fingerId);
                    _uiBlockedTouchIds.Remove(t.fingerId);
                    break;

                case TouchPhase.Canceled:
                    router.Cancel();
                    _touchHistories.Remove(t.fingerId);
                    _uiBlockedTouchIds.Remove(t.fingerId);
                    break;
            }
        }

        private void HandleMouseInput()
        {
            if (_settings == null) return;

            bool isMouseDown = UnityInput.GetMouseButtonDown(0);
            bool isMouseUp = UnityInput.GetMouseButtonUp(0);
            bool isMouseHeld = UnityInput.GetMouseButton(0);

            if (!isMouseDown && !isMouseUp && !isMouseHeld) return;

            var router = TryGetRouter();
            if (router == null) return;

            Vector2 currentMousePos = UnityInput.mousePosition;

            if (isMouseDown)
            {
                _mouseBlockedByUi = IsPointerOverUi(-1, currentMousePos);
                _touchStartPos = currentMousePos;
                _touchStartTime = Time.time;
                _isDragging = false;
                _lastMousePos = currentMousePos;

                if (_mouseBlockedByUi)
                    return;

                _touchHistories[-1] = new TouchHistory
                {
                    startPosition = currentMousePos,
                    startTime = Time.time,
                    isLongPressTriggered = false
                };

                router.PrimaryDown(currentMousePos.x, currentMousePos.y);
            }
            else if (isMouseHeld)
            {
                if (_mouseBlockedByUi)
                {
                    _lastMousePos = currentMousePos;
                    return;
                }

                Vector2 delta = currentMousePos - _lastMousePos;

                if (!_isDragging && Vector2.Distance(currentMousePos, _touchStartPos) > _settings.DragThreshold)
                {
                    _isDragging = true;
                }

                if (_isDragging)
                {
                    router.PrimaryDrag(delta.x, delta.y);
                    MobileInputEvents.Drag(delta, currentMousePos);
                }

                _lastMousePos = currentMousePos;
            }
            else if (isMouseUp)
            {
                if (_mouseBlockedByUi)
                {
                    _mouseBlockedByUi = false;
                    _touchHistories.Remove(-1);
                    return;
                }

                if (!_isDragging)
                {
                    ProcessTap(currentMousePos);
                    MobileInputEvents.Tap(currentMousePos);
                }
                router.PrimaryUp(currentMousePos.x, currentMousePos.y);

                _touchHistories.Remove(-1);
            }
        }

        /// <summary>
        /// Handles two-finger gestures including pinch, swipe, tap, and long press.
        /// </summary>
        private void HandleTwoFingers()
        {
            if (_settings == null) return;

            var router = TryGetRouter();
            if (router == null) return;

            Touch t0 = UnityInput.GetTouch(0);
            Touch t1 = UnityInput.GetTouch(1);

            if (t0.phase == TouchPhase.Began)
            {
                if (IsPointerOverUi(t0.fingerId, t0.position)) _uiBlockedTouchIds.Add(t0.fingerId);
                else _uiBlockedTouchIds.Remove(t0.fingerId);
            }

            if (t1.phase == TouchPhase.Began)
            {
                if (IsPointerOverUi(t1.fingerId, t1.position)) _uiBlockedTouchIds.Add(t1.fingerId);
                else _uiBlockedTouchIds.Remove(t1.fingerId);
            }

            bool hasUiBlockedTouch = _uiBlockedTouchIds.Contains(t0.fingerId) || _uiBlockedTouchIds.Contains(t1.fingerId);
            if (hasUiBlockedTouch)
            {
                if (t0.phase == TouchPhase.Ended || t0.phase == TouchPhase.Canceled) _uiBlockedTouchIds.Remove(t0.fingerId);
                if (t1.phase == TouchPhase.Ended || t1.phase == TouchPhase.Canceled) _uiBlockedTouchIds.Remove(t1.fingerId);
                return;
            }

            float currentDist = Vector2.Distance(t0.position, t1.position);

            // Initialize on begin
            if (t0.phase == TouchPhase.Began || t1.phase == TouchPhase.Began)
            {
                _lastPinchDist = currentDist;
                _lastTwoFingerPressTime = Time.time;
                _twoFingerLongPressDetected = false;
                _isZooming = false;
                _isPanning = false;
                _startPinchDist = currentDist;
                _startPanCenter = (t0.position + t1.position) / 2;
                _lastPanCenter = _startPanCenter;
            }

            // Two-finger pinch (zoom)
            if (t0.phase == TouchPhase.Moved || t1.phase == TouchPhase.Moved)
            {
                Vector2 avgDelta = (t0.deltaPosition + t1.deltaPosition) / 2;
                Vector2 centerPos = (t0.position + t1.position) / 2;
                Vector2 centerDelta = centerPos - _lastPanCenter;

                // Lock gesture type on start
                if (!_isZooming && !_isPanning)
                {
                    float totalZoom = Mathf.Abs(currentDist - _startPinchDist);
                    float totalPan = (centerPos - _startPanCenter).magnitude;
                    float threshold = _settings.DragThreshold;

                    if (totalZoom > threshold || totalPan > threshold)
                    {
                        // Bias towards panning: Zoom must be significantly stronger than pan to take precedence
                        // because it's hard to drag two fingers without slightly changing distance.
                        if (totalZoom > totalPan * 1.75f) _isZooming = true;
                        else
                        {
                            _isPanning = true;
                            _lastPanCenter = centerPos;
                        }
                        _lastPinchDist = currentDist; // Sync to avoid jump
                    }
                    else
                    {
                        _lastPinchDist = currentDist; // Keep syncing while waiting
                        return;
                    }
                }

                float distDelta = currentDist - _lastPinchDist;
                float pinchDelta = distDelta * 0.01f;
                _lastPinchDist = currentDist;

                // Zoom
                if (_isZooming && Mathf.Abs(pinchDelta) > 0.0001f)
                {
                    router.Zoom(pinchDelta);
                    MobileInputEvents.PinchZoom(pinchDelta);
                }
                // Pan (Two Finger Swipe)
                else if (_isPanning)
                {
                    if (centerDelta.sqrMagnitude <= 0.0001f && avgDelta.sqrMagnitude > 0.0001f)
                        centerDelta = avgDelta;

                    if (centerDelta.sqrMagnitude > 1.0f)
                    {
                        Vector2 panDelta = centerDelta * Mathf.Max(0.01f, _twoFingerSwipeSensitivity);
                        CallbackHub.CallAction<IMobileInputCallbacks>(
                            cb => cb.OnTwoFingerSwipe(panDelta, centerPos)
                        );
                        MobileInputEvents.Drag(panDelta, centerPos);
                    }
                }

                _lastPanCenter = centerPos;

                // Two-finger long press detection
                if (!_twoFingerLongPressDetected &&
                    Time.time - _lastTwoFingerPressTime > _settings.TwoFingerLongPressTime)
                {
                    _twoFingerLongPressDetected = true;

                    // Fire both events
                    CallbackHub.CallAction<IMobileInputCallbacks>(
                        cb => cb.OnTwoFingerLongPress(centerPos)
                    );
                    MobileInputEvents.Tap(centerPos);
                }
            }

            // Handle end of two-finger gesture (pan release)
            if (t0.phase == TouchPhase.Ended || t1.phase == TouchPhase.Ended ||
                t0.phase == TouchPhase.Canceled || t1.phase == TouchPhase.Canceled)
            {
                Vector2 center = (t0.position + t1.position) / 2;
                CallbackHub.CallAction<IMobileInputCallbacks>(
                    cb => cb.OnTwoFingerSwipe(Vector2.zero, center)
                );
                MobileInputEvents.Drag(Vector2.zero, center);
            }

            // Two-finger tap detection
            if (t0.phase == TouchPhase.Ended && t1.phase == TouchPhase.Ended)
            {
                if (Time.time - _lastTwoFingerPressTime < 0.3f)
                {
                    Vector2 center = (t0.position + t1.position) / 2;

                    // Fire both events
                    CallbackHub.CallAction<IMobileInputCallbacks>(cb => cb.OnTwoFingerTap(center));
                    MobileInputEvents.Tap(center);
                }
            }

            if (t0.phase == TouchPhase.Ended || t0.phase == TouchPhase.Canceled) _uiBlockedTouchIds.Remove(t0.fingerId);
            if (t1.phase == TouchPhase.Ended || t1.phase == TouchPhase.Canceled) _uiBlockedTouchIds.Remove(t1.fingerId);
        }

        /// <summary>
        /// Handles three-finger gestures including tap, swipe, and pinch.
        /// </summary>
        private void HandleThreeFingers()
        {
            Touch t0 = UnityInput.GetTouch(0);
            Touch t1 = UnityInput.GetTouch(1);
            Touch t2 = UnityInput.GetTouch(2);

            Vector2 centerPos = (t0.position + t1.position + t2.position) / 3;

            if (t0.phase == TouchPhase.Began &&
                t1.phase == TouchPhase.Began &&
                t2.phase == TouchPhase.Began)
            {
                // Three-finger tap detection
                CallbackHub.CallAction<IMobileInputCallbacks>(
                    cb => cb.OnThreeFingerTap(centerPos)
                );
                MobileInputEvents.Tap(centerPos);

                // Initialize three-finger pinch
                _lastThreeFingerCenter = centerPos;
                _lastThreeFingerDistance = CalculateThreeFingerDistance(t0.position, t1.position, t2.position);
            }

            if (t0.phase == TouchPhase.Moved ||
                t1.phase == TouchPhase.Moved ||
                t2.phase == TouchPhase.Moved)
            {
                // Three-finger swipe
                Vector2 avgDelta = (t0.deltaPosition + t1.deltaPosition + t2.deltaPosition) / 3;
                CallbackHub.CallAction<IMobileInputCallbacks>(
                    cb => cb.OnThreeFingerSwipe(avgDelta.normalized, centerPos)
                );
                MobileInputEvents.Drag(avgDelta.normalized, centerPos);

                // Three-finger pinch
                float currentDistance = CalculateThreeFingerDistance(t0.position, t1.position, t2.position);
                float pinchDelta = (currentDistance - _lastThreeFingerDistance) * 0.01f;

                if (Mathf.Abs(pinchDelta) > 0.01f)
                {
                    CallbackHub.CallAction<IMobileInputCallbacks>(
                        cb => cb.OnThreeFingerPinch(pinchDelta)
                    );
                    MobileInputEvents.ThreeFingerPinch(pinchDelta);
                    _lastThreeFingerDistance = currentDistance;
                }
            }
        }

        /// <summary>
        /// Handles four-finger gestures including tap and swipe.
        /// </summary>
        private void HandleFourFingers()
        {
            if (UnityInput.touchCount < 4) return;

            Touch t0 = UnityInput.GetTouch(0);
            Touch t1 = UnityInput.GetTouch(1);
            Touch t2 = UnityInput.GetTouch(2);
            Touch t3 = UnityInput.GetTouch(3);

            Vector2 centerPos = (t0.position + t1.position + t2.position + t3.position) / 4;

            if (t0.phase == TouchPhase.Began &&
                t1.phase == TouchPhase.Began &&
                t2.phase == TouchPhase.Began &&
                t3.phase == TouchPhase.Began)
            {
                // Four-finger tap
                CallbackHub.CallAction<IMobileInputCallbacks>(cb => cb.OnFourFingerTap());
                MobileInputEvents.FourFingerTap();
            }

            if (t0.phase == TouchPhase.Moved ||
                t1.phase == TouchPhase.Moved ||
                t2.phase == TouchPhase.Moved ||
                t3.phase == TouchPhase.Moved)
            {
                // Four-finger swipe
                Vector2 avgDelta = (t0.deltaPosition + t1.deltaPosition +
                                  t2.deltaPosition + t3.deltaPosition) / 4;
                CallbackHub.CallAction<IMobileInputCallbacks>(
                    cb => cb.OnFourFingerSwipe(avgDelta.normalized)
                );
                MobileInputEvents.Tap(avgDelta.normalized);
            }
        }

        #endregion

        #region Physics & Sensors

        /// <summary>
        /// Detects device movement such as shake, tilt, and orientation changes.
        /// </summary>
        private void DetectDevicePhysics()
        {
            // Detect shake with cooldown
            Vector3 acceleration = UnityInput.acceleration;
            Vector3 deltaAccel = acceleration - _lastAcceleration;

            if (deltaAccel.magnitude > _settings.ShakeThreshold &&
                Time.time - _lastShakeTime > 1.0f)
            {
                _lastShakeTime = Time.time;
                CallbackHub.CallAction<IMobileInputCallbacks>(cb => cb.OnShake());
                MobileInputEvents.Shake();
            }
            _lastAcceleration = acceleration;

            // Detect tilt using gyroscope
            if (CanUseGyroscope())
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                if (WebGLGyroAPI.IsInitialized)
                {
                    Quaternion currentRot = WebGLGyroAPI.GetRotation();
                    Quaternion lastRot = Quaternion.Euler(_lastDeviceRotation);
                    Quaternion deltaRot = Quaternion.Inverse(lastRot) * currentRot;

                    Vector3 deltaEuler = deltaRot.eulerAngles;
                    if (deltaEuler.x > 180) deltaEuler.x -= 360;
                    if (deltaEuler.y > 180) deltaEuler.y -= 360;
                    if (deltaEuler.z > 180) deltaEuler.z -= 360;

                    float dt = Time.deltaTime > 0.0001f ? Time.deltaTime : 0.0001f;
                    Vector3 rate = deltaEuler / dt;

                    if (rate.sqrMagnitude > _gyroTiltThreshold * _gyroTiltThreshold)
                    {
                        CallbackHub.CallAction<IMobileInputCallbacks>(
                            cb => cb.OnTilt(rate)
                        );
                        MobileInputEvents.Tilt(rate);
                    }
                    _lastDeviceRotation = currentRot.eulerAngles;
                }
#else
                Vector3 rate = UnityInput.gyro.rotationRateUnbiased * Mathf.Rad2Deg;
                if (rate.sqrMagnitude > _gyroTiltThreshold * _gyroTiltThreshold)
                {
                    CallbackHub.CallAction<IMobileInputCallbacks>(
                        cb => cb.OnTilt(rate)
                    );
                    MobileInputEvents.Tilt(rate);
                }

                _lastDeviceRotation = UnityInput.gyro.attitude.eulerAngles;
#endif
            }

            // Detect orientation change
            if (CanUseDeviceOrientation() &&
                UnityInput.deviceOrientation != _lastOrientation &&
                UnityInput.deviceOrientation != DeviceOrientation.Unknown)
            {
                _lastOrientation = UnityInput.deviceOrientation;
                CallbackHub.CallAction<IMobileInputCallbacks>(
                    cb => cb.OnDeviceRotated(_lastOrientation)
                );
                MobileInputEvents.DeviceRotated(_lastOrientation);
            }
        }

        /// <summary>
        /// Detects when device is picked up or put down using accelerometer.
        /// </summary>
        private void DetectPickupAndPutdown()
        {
            Vector3 accel = UnityInput.acceleration;

            // Device is picked up if acceleration changes significantly from resting state
            if (!_isDevicePickedUp && accel.magnitude > _settings.PickupAccelerationThreshold)
            {
                _isDevicePickedUp = true;
                _devicePutDownTime = 0f;

                CallbackHub.CallAction<IMobileInputCallbacks>(cb => cb.OnPickUp());
                MobileInputEvents.PickUp();
            }

            // Device is put down if acceleration is stable for a period
            if (_isDevicePickedUp && accel.magnitude < 0.2f)
            {
                if (_devicePutDownTime == 0f)
                    _devicePutDownTime = Time.time;

                if (Time.time - _devicePutDownTime > _settings.PutDownStableTime)
                {
                    _isDevicePickedUp = false;
                    CallbackHub.CallAction<IMobileInputCallbacks>(cb => cb.OnPutDown());
                    MobileInputEvents.PutDown();
                }
            }
            else
            {
                _devicePutDownTime = 0f;
            }
        }

        /// <summary>
        /// Detects edge swipes and invokes callbacks if touch starts near screen edges.
        /// </summary>
        private void DetectEdgeGestures()
        {
            if (UnityInput.touchCount > 0)
            {
                foreach (Touch touch in UnityInput.touches)
                {
                    if (touch.phase == TouchPhase.Began)
                    {
                        Vector2 pos = touch.position;
                        EdgeDirection? edge = null;

                        if (pos.x < _settings.EdgeThreshold) edge = EdgeDirection.Left;
                        else if (pos.x > Screen.width - _settings.EdgeThreshold) edge = EdgeDirection.Right;
                        else if (pos.y < _settings.EdgeThreshold) edge = EdgeDirection.Bottom;
                        else if (pos.y > Screen.height - _settings.EdgeThreshold) edge = EdgeDirection.Top;

                        if (edge.HasValue)
                        {
                            CallbackHub.CallAction<IMobileInputCallbacks>(cb => cb.OnEdgeSwipe(edge.Value));
                            MobileInputEvents.EdgeSwipe(edge.Value);
                        }
                    }
                }
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Processes a single tap, performs raycast selection, and invokes force touch callbacks if supported.
        /// </summary>
        private void ProcessTap(Vector2 position)
        {
            var router = TryGetRouter();
            if (router == null) return;
            if (_isDragging) return;

            // Raycast for object selection
            var cam = GetRaycastCamera();
            if (cam == null)
            {
                router.Cancel();
                return;
            }

            Ray ray = cam.ScreenPointToRay(position);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Debug.Log($"[MobileInputProvider] Tap hit {hit.collider.name} at {hit.point}");
                SelectionData selection = new SelectionData(hit);
                router.Select(selection);
                CallbackHub.CallAction<IMobileInputCallbacks>(cb => cb.OnSelect(selection));
            }
            else
            {
                Debug.Log("[MobileInputProvider] Tap hit nothing.");
                router.Cancel();
            }
        }

        private void ProcessTap(Touch t)
        {
            ProcessTap(t.position);

            // Force touch detection
            if (UnityInput.touchPressureSupported && t.maximumPossiblePressure > 0)
            {
                float pressure = t.pressure / t.maximumPossiblePressure;
                if (pressure > 0.5f) // Threshold for force touch
                {
                    CallbackHub.CallAction<IMobileInputCallbacks>(cb => cb.OnForceTouch(pressure));
                    MobileInputEvents.ForceTouch(pressure);
                }
            }
        }

        /// <summary>
        /// Updates touch histories for long press detection.
        /// </summary>
        private void UpdateTouchHistories()
        {
            if (_settings == null) return;

            List<int> toRemove = new List<int>();
            List<int> keys = new List<int>(_touchHistories.Keys);

            foreach (int fingerId in keys)
            {
                if (!_touchHistories.TryGetValue(fingerId, out TouchHistory history))
                    continue;

                // Check if this touch is still active
                bool touchExists = false;
                foreach (Touch t in UnityInput.touches)
                {
                    if (t.fingerId == fingerId)
                    {
                        touchExists = true;

                        // Check for long press
                        if (!history.isLongPressTriggered &&
                            Time.time - history.startTime > _settings.LongPressTime)
                        {
                            // Long press detected
                            history.isLongPressTriggered = true;
                            _touchHistories[fingerId] = history;

                            // Note: Single finger long press maps to IInputCallbacks.OnCancel()
                            // which is already handled by the core system
                            MobileInputEvents.LongPress();
                        }
                        break;
                    }
                }

                if (!touchExists)
                    toRemove.Add(fingerId);
            }

            // Clean up removed touches
            foreach (int fingerId in toRemove)
                _touchHistories.Remove(fingerId);
        }

        /// <summary>
        /// Calculates the average distance between three touch points from their center.
        /// </summary>
        private float CalculateThreeFingerDistance(Vector2 p1, Vector2 p2, Vector2 p3)
        {
            Vector2 center = (p1 + p2 + p3) / 3;
            float d1 = Vector2.Distance(p1, center);
            float d2 = Vector2.Distance(p2, center);
            float d3 = Vector2.Distance(p3, center);
            return (d1 + d2 + d3) / 3;
        }

        /// <summary>
        /// Checks for accessibility-related actions (stub implementation).
        /// </summary>
        private void CheckAccessibilityActions()
        {
            if (_settings == null) return;

            // This would normally interface with platform-specific accessibility APIs
            // For now, we'll provide a basic implementation

            // Simulate accessibility double-tap (common screen reader gesture)
            if (UnityInput.touchCount == 1 && UnityInput.GetTouch(0).tapCount == 2)
            {
                // Check if it might be an accessibility gesture
                // In real implementation, you'd check accessibility settings
                if (_isScreenReaderActive)
                {
                    CallbackHub.CallAction<IMobileInputCallbacks>(
                        cb => cb.OnAccessibilityAction("DoubleTap")
                    );
                    MobileInputEvents.AccessibilityAction("DoubleTap");
                }
            }

            // Simulate notification action (quick swipe from top)
            // This is a simplified detection
            if (UnityInput.touchCount == 1)
            {
                Touch t = UnityInput.GetTouch(0);
                if (t.phase == TouchPhase.Began && t.position.y > Screen.height - 100)
                {
                    CallbackHub.CallAction<IMobileInputCallbacks>(
                        cb => cb.OnNotificationAction(true)
                    );
                    MobileInputEvents.NotificationAction(true);
                }
            }
        }

        // Platform-specific accessibility checks (stubs)
#if UNITY_ANDROID && !UNITY_EDITOR
        private void CheckAndroidAccessibility()
        {
            // Android implementation would use:
            // AccessibilityManager.getInstance(context).isEnabled()
            // or similar Java/Android SDK calls
            // For now, we'll simulate
            _isScreenReaderActive = false;
        }
#elif UNITY_IOS && !UNITY_EDITOR
        private void CheckIOSAccessibility()
        {
            // iOS implementation would use:
            // UIAccessibilityIsVoiceOverRunning()
            // For now, we'll simulate
            _isScreenReaderActive = false;
        }
#else
        private void CheckAndroidAccessibility() { }
        private void CheckIOSAccessibility() { }
#endif

        #endregion

        private void EnsureSettingsLoaded()
        {
            if (_settings != null) return;

            _settings = Resources.Load<MobileInputSettings>(SettingsResourceName);
            if (_settings != null) return;

            if (!_warnedMissingSettings)
            {
                _warnedMissingSettings = true;
                Debug.LogWarning(
                    $"[MobileInputProvider] Missing settings asset. " +
                    $"Expected Resources/{SettingsResourceName}.asset. " +
                    "Using in-memory defaults."
                );
            }

            _settings = ScriptableObject.CreateInstance<MobileInputSettings>();
        }

        private InputRouter TryGetRouter()
        {
            var router = InputRouter.Instance;
            if (router == null && !_warnedMissingRouter)
            {
                _warnedMissingRouter = true;
                Debug.LogWarning("[MobileInputProvider] InputRouter.Instance is null. Input routing disabled.");
            }
            return router;
        }

        private Camera GetRaycastCamera()
        {
            if (Camera.main != null) return Camera.main;
            return FindAnyObjectByType<Camera>();
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private static bool IsWebGlMobileBrowser()
        {
            return Application.isMobilePlatform || SystemInfo.deviceType == DeviceType.Handheld;
        }
#endif

        private bool CanUseGyroscope()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return true;
#else
            return SystemInfo.supportsGyroscope;
#endif
        }

        private bool CanUseDeviceOrientation()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return false;
#else
            return true;
#endif
        }

        #region Public API

        /// <summary>
        /// Manually trigger a haptic touch event.
        /// </summary>
        public void TriggerHapticTouch()
        {
            CallbackHub.CallAction<IMobileInputCallbacks>(cb => cb.OnHapticTouch());
            MobileInputEvents.HapticTouch();
        }

        /// <summary>
        /// Set whether screen reader/accessibility features are active.
        /// </summary>
        public void SetScreenReaderActive(bool active)
        {
            _isScreenReaderActive = active;
        }

        /// <summary>
        /// Trigger a specific accessibility action.
        /// </summary>
        public void TriggerAccessibilityAction(string actionName)
        {
            CallbackHub.CallAction<IMobileInputCallbacks>(
                cb => cb.OnAccessibilityAction(actionName)
            );
            MobileInputEvents.AccessibilityAction(actionName);
        }

        /// <summary>
        /// Trigger a screen reader gesture.
        /// </summary>
        public void TriggerScreenReaderGesture(string gestureType)
        {
            CallbackHub.CallAction<IMobileInputCallbacks>(
                cb => cb.OnScreenReaderGesture(gestureType)
            );
            MobileInputEvents.ScreenReaderGesture(gestureType);
        }

        #endregion
    }
}
