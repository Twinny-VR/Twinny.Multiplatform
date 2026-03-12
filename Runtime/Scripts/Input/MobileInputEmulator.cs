#if UNITY_EDITOR || UNITY_WEBGL
using Concept.Core;
using Twinny.Core.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

namespace Twinny.Mobile.Input
{
    public class MobileInputEmulator : MonoBehaviour
    {
        private const string SettingsResourceName = "MobileInputSettings";
        private const float TapMaxTime = 0.25f;
        private const float TwoFingerTapMaxTime = 0.3f;
        private const float MiddlePanSensitivity = 1.0f;
        private const float MiddlePanDeltaThreshold = 1.0f;
        private const float EmulatorTwoFingerSwipeSensitivity = 10.0f;
        private const float EmulatorPanDeltaThreshold = 1.0f;
        private const float ScrollZoomMultiplier = 4.5f;
        private const float ScrollZoomSmoothing = 20.0f;
        private const float ScrollZoomEpsilon = 0.0001f;

        private MobileInputSettings _settings;
        private bool _warnedMissingSettings;
        private bool _warnedMissingRouter;
        [SerializeField] private bool _logTapDebug = false;
        [SerializeField] private bool _ignoreUiBlocking = false;
        private bool _loggedStartup;
        private UIDocument[] _uiDocuments;
        private int _uiDocumentsFrame;

        private bool _singleDown;
        private bool _singleDragging;
        private bool _singleBlockedByUi;
        private bool _suppressTap;
        private Vector3 _singleStartPos;
        private float _singleStartTime;
        private Vector3 _lastSinglePos;

        private bool _twoFingerDown;
        private bool _twoFingerDragging;
        private bool _twoFingerBlockedByUi;
        private Vector3 _lastTwoFingerPos;
        private float _twoFingerStartTime;
        private bool _twoFingerLongPressDetected;
        private bool _suppressSingleUntilRelease;

        private bool _threeFingerDown;
        private bool _threeFingerDragging;
        private bool _threeFingerBlockedByUi;
        private Vector3 _lastThreeFingerPos;
        private float _threeFingerStartTime;

        private bool _mousePinchDown;
        private bool _mousePinchBlockedByUi;
        private Vector3 _lastMousePinchPos;
        private bool _middlePanDragging;
        private float _pendingScrollZoom;

        private void Awake()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (Application.isMobilePlatform || SystemInfo.deviceType == DeviceType.Handheld)
            {
                Destroy(gameObject);
                return;
            }
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (Application.isMobilePlatform || SystemInfo.deviceType == DeviceType.Handheld) return;
#endif
            if (FindAnyObjectByType<MobileInputEmulator>() != null) return;
            var emulatorObject = new GameObject("MobileInputEmulator");
            emulatorObject.AddComponent<MobileInputEmulator>();
            DontDestroyOnLoad(emulatorObject);
        }

        private void Update()
        {
            if (_logTapDebug && !_loggedStartup)
                _loggedStartup = true;
            EnsureSettingsLoaded();
            ForceReleaseIfPointerLeavesGameView();
            ForceReleaseIfButtonsUp();
            ForceExclusivePrimaryMouseMode();

            // Recovery for stale suppression state after releasing buttons outside Game View.
            if (_suppressSingleUntilRelease &&
                !_twoFingerDown &&
                !_threeFingerDown &&
                !_mousePinchDown &&
                !UnityEngine.Input.GetMouseButton(1) &&
                !UnityEngine.Input.GetMouseButton(2))
            {
                _suppressSingleUntilRelease = false;
            }

            if (_suppressSingleUntilRelease && !UnityEngine.Input.GetMouseButton(0))
                _suppressSingleUntilRelease = false;

            if (HandleThreeFinger()) return;
            if (HandleTwoFinger()) return;
            if (HandleMiddlePan()) return;
            HandleScroll();
            ApplySmoothedScrollZoom();

            HandleSingleFinger();
        }

        private void ForceExclusivePrimaryMouseMode()
        {
            bool left = UnityEngine.Input.GetMouseButton(0);
            bool right = UnityEngine.Input.GetMouseButton(1);
            bool middle = UnityEngine.Input.GetMouseButton(2);

            if (!left || right || middle)
                return;

            if (_twoFingerDown)
                EndTwoFinger();

            if (_mousePinchDown)
                EndMiddlePan();

            _suppressSingleUntilRelease = false;
        }

        private void ForceReleaseIfPointerLeavesGameView()
        {
            if (!HasActiveGestureState())
                return;

            Vector3 mousePosition = UnityEngine.Input.mousePosition;
            if (!IsOutsideGameView(mousePosition))
                return;

            var router = TryGetRouter();
            if (router != null)
                router.Cancel();

            ResetAllStates(notifyCancellation: true);
        }

        private static bool IsOutsideGameView(Vector3 mousePosition)
        {
            return mousePosition.x < 0f ||
                   mousePosition.y < 0f ||
                   mousePosition.x > Screen.width ||
                   mousePosition.y > Screen.height;
        }

        private bool HasActiveGestureState()
        {
            return _singleDown ||
                   _twoFingerDown ||
                   _threeFingerDown ||
                   _mousePinchDown ||
                   _singleDragging ||
                   _twoFingerDragging ||
                   _threeFingerDragging;
        }

        private bool HandleTwoFinger()
        {
            bool twoPressed = UnityEngine.Input.GetMouseButton(0) && UnityEngine.Input.GetMouseButton(1);
            if (!twoPressed && !_twoFingerDown) return false;

            if (twoPressed && !_twoFingerDown)            
                BeginTwoFinger();
            if (twoPressed) UpdateTwoFinger();
            if (!twoPressed && _twoFingerDown) EndTwoFinger();
            return true;
        }

        private void BeginTwoFinger()
        {
            _twoFingerDown = true;
            _twoFingerDragging = false;
            _twoFingerStartTime = Time.time;
            _twoFingerLongPressDetected = false;
            _lastTwoFingerPos = UnityEngine.Input.mousePosition;
            _twoFingerBlockedByUi = !_ignoreUiBlocking && IsPointerOverUiAt(_lastTwoFingerPos);
            _suppressSingleUntilRelease = true;
            _suppressTap = true;
            _singleDown = false;
            _singleDragging = false;
        }

        private void UpdateTwoFinger()
        {
            Vector3 current = UnityEngine.Input.mousePosition;
            if (_twoFingerBlockedByUi)
            {
                _lastTwoFingerPos = current;
                return;
            }

            Vector2 delta = (Vector2)(current - _lastTwoFingerPos);
            EmitTwoFingerPanFromDelta(delta, current, ref _twoFingerDragging);

            if (!_twoFingerLongPressDetected &&
                Time.time - _twoFingerStartTime > _settings.TwoFingerLongPressTime)
            {
                _twoFingerLongPressDetected = true;
                Vector2 center = current;
                CallbackHub.CallAction<IMobileInputCallbacks>(
                    cb => cb.OnTwoFingerLongPress(center)
                );
                MobileInputEvents.Tap(center);
            }

            _lastTwoFingerPos = current;
        }

        private void EndTwoFinger()
        {
            if (_twoFingerBlockedByUi)
            {
                _twoFingerDown = false;
                _twoFingerDragging = false;
                _twoFingerBlockedByUi = false;
                return;
            }

            Vector2 center = _lastTwoFingerPos;
            float elapsed = Time.time - _twoFingerStartTime;
            if (!_twoFingerDragging && elapsed <= TwoFingerTapMaxTime)
            {
                CallbackHub.CallAction<IMobileInputCallbacks>(
                    cb => cb.OnTwoFingerTap(center)
                );
                MobileInputEvents.Tap(center);
            }
            else if (_twoFingerDragging)
            {
                CallbackHub.CallAction<IMobileInputCallbacks>(
                    cb => cb.OnTwoFingerSwipe(Vector2.zero, center)
                );
                MobileInputEvents.TwoFingerSwipe(Vector2.zero, center);
                MobileInputEvents.Drag(Vector2.zero, center);
            }
            _twoFingerDown = false;
            _twoFingerDragging = false;
        }

        private bool HandleThreeFinger()
        {
            bool threePressed = UnityEngine.Input.GetMouseButton(2) &&
                (UnityEngine.Input.GetKey(KeyCode.LeftAlt) || UnityEngine.Input.GetKey(KeyCode.RightAlt));
            if (!threePressed && !_threeFingerDown) return false;

            if (threePressed && !_threeFingerDown)            
                BeginThreeFinger();
            if (threePressed) UpdateThreeFinger();
            if (!threePressed && _threeFingerDown) EndThreeFinger();
            return true;
        }

        private void BeginThreeFinger()
        {
            _threeFingerDown = true;
            _threeFingerDragging = false;
            _threeFingerStartTime = Time.time;
            _lastThreeFingerPos = UnityEngine.Input.mousePosition;
            _threeFingerBlockedByUi = !_ignoreUiBlocking && IsPointerOverUiAt(_lastThreeFingerPos);
        }

        private void UpdateThreeFinger()
        {
            Vector3 current = UnityEngine.Input.mousePosition;
            if (_threeFingerBlockedByUi)
            {
                _lastThreeFingerPos = current;
                return;
            }

            Vector2 delta = (Vector2)(current - _lastThreeFingerPos);
            if (delta.sqrMagnitude > 0f)
            {
                _threeFingerDragging = true;
                Vector2 direction = delta.normalized;
                Vector2 center = current;
                CallbackHub.CallAction<IMobileInputCallbacks>(
                    cb => cb.OnThreeFingerSwipe(direction, center)
                );
                MobileInputEvents.Drag(direction, center);
            }
            _lastThreeFingerPos = current;
        }

        private void EndThreeFinger()
        {
            if (_threeFingerBlockedByUi)
            {
                _threeFingerDown = false;
                _threeFingerDragging = false;
                _threeFingerBlockedByUi = false;
                _suppressTap = false;
                return;
            }

            Vector2 center = _lastThreeFingerPos;
            float elapsed = Time.time - _threeFingerStartTime;
            if (!_threeFingerDragging && elapsed <= TapMaxTime)
            {
                CallbackHub.CallAction<IMobileInputCallbacks>(
                    cb => cb.OnThreeFingerTap(center)
                );
                MobileInputEvents.Tap(center);
            }
            _threeFingerDown = false;
            _threeFingerDragging = false;
            _suppressTap = false;
        }

        private bool HandleMiddlePan()
        {
            bool middlePanPressed = UnityEngine.Input.GetMouseButton(2) &&
                !UnityEngine.Input.GetKey(KeyCode.LeftAlt) &&
                !UnityEngine.Input.GetKey(KeyCode.RightAlt);
            if (!middlePanPressed && !_mousePinchDown) return false;

            if (middlePanPressed && !_mousePinchDown)
                BeginMousePinch();
            if (middlePanPressed) UpdateMiddlePan();
            if (!middlePanPressed && _mousePinchDown) EndMiddlePan();
            return true;
        }

        private void BeginMousePinch()
        {
            _mousePinchDown = true;
            _lastMousePinchPos = UnityEngine.Input.mousePosition;
            _mousePinchBlockedByUi = !_ignoreUiBlocking && IsPointerOverUiAt(_lastMousePinchPos);
            _suppressSingleUntilRelease = true;
            _singleDown = false;
            _singleDragging = false;
        }

        private void UpdateMiddlePan()
        {
            if (_mousePinchBlockedByUi)
            {
                _lastMousePinchPos = UnityEngine.Input.mousePosition;
                return;
            }

            Vector3 current = UnityEngine.Input.mousePosition;
            Vector2 delta = (Vector2)(current - _lastMousePinchPos);
            Vector2 scaledDelta = delta * MiddlePanSensitivity;
            EmitTwoFingerPanFromDelta(scaledDelta, current, ref _middlePanDragging, MiddlePanDeltaThreshold);
            _lastMousePinchPos = current;
        }

        private void EndMiddlePan()
        {
            if (_middlePanDragging)
            {
                Vector2 center = _lastMousePinchPos;
                CallbackHub.CallAction<IMobileInputCallbacks>(
                    cb => cb.OnTwoFingerSwipe(Vector2.zero, center)
                );
                MobileInputEvents.TwoFingerSwipe(Vector2.zero, center);
                MobileInputEvents.Drag(Vector2.zero, center);
            }

            _mousePinchDown = false;
            _mousePinchBlockedByUi = false;
            _middlePanDragging = false;
            _suppressTap = false;
        }

        private void HandleSingleFinger()
        {
            var router = TryGetRouter();
            if (router == null) return;

            if (_suppressSingleUntilRelease)
            {
                if (!UnityEngine.Input.GetMouseButton(0))
                    _suppressSingleUntilRelease = false;
                return;
            }

            bool shouldStartSingle =
                UnityEngine.Input.GetMouseButtonDown(0) ||
                (!_singleDown &&
                 UnityEngine.Input.GetMouseButton(0) &&
                 !UnityEngine.Input.GetMouseButton(1) &&
                 !UnityEngine.Input.GetMouseButton(2) &&
                 !IsOutsideGameView(UnityEngine.Input.mousePosition));

            if (shouldStartSingle)
            {
                _singleDown = true;
                _singleDragging = false;
                _singleStartPos = UnityEngine.Input.mousePosition;
                _lastSinglePos = _singleStartPos;
                _singleStartTime = Time.time;
                _singleBlockedByUi = !_ignoreUiBlocking && IsPointerOverUiAt(_singleStartPos);
                if (_singleBlockedByUi)
                    return;
                router.PrimaryDown(_singleStartPos.x, _singleStartPos.y);
            }

            if (_singleDown && UnityEngine.Input.GetMouseButton(0))
            {
                Vector3 current = UnityEngine.Input.mousePosition;
                if (_singleBlockedByUi)
                {
                    _lastSinglePos = current;
                    return;
                }

                Vector2 delta = (Vector2)(current - _lastSinglePos);
                if (!_singleDragging &&
                    Vector2.Distance((Vector2)current, (Vector2)_singleStartPos) > _settings.DragThreshold)
                    _singleDragging = true;

                if (_singleDragging && delta.sqrMagnitude > 0.0001f)
                {
                    _suppressTap = true;
                    router.PrimaryDrag(delta.x, delta.y);
                    MobileInputEvents.Drag(delta, current);
                }
                _lastSinglePos = current;
            }

            if (_singleDown && UnityEngine.Input.GetMouseButtonUp(0))
            {
                Vector3 current = UnityEngine.Input.mousePosition;
                if (_singleBlockedByUi)
                {
                    _singleDown = false;
                    _singleDragging = false;
                    _singleBlockedByUi = false;
                    _suppressTap = false;
                    return;
                }

                router.PrimaryUp(current.x, current.y);
                if (!_singleDragging && !_suppressTap && Time.time - _singleStartTime <= TapMaxTime)
                {
                    TrySelect(current, router);
                    MobileInputEvents.Tap(current);
                }
                _singleDown = false;
                _singleDragging = false;
                _singleBlockedByUi = false;
                _suppressTap = false;
            }
        }

        private void ForceReleaseIfButtonsUp()
        {
            if (_twoFingerDown)
            {
                bool left = UnityEngine.Input.GetMouseButton(0);
                bool right = UnityEngine.Input.GetMouseButton(1);
                if (!left || !right)
                    EndTwoFinger();
            }

            if (_threeFingerDown && !UnityEngine.Input.GetMouseButton(2))
                EndThreeFinger();

            if (_mousePinchDown && !UnityEngine.Input.GetMouseButton(2))
                EndMiddlePan();

            if (_singleDown && !UnityEngine.Input.GetMouseButton(0))
            {
                var router = TryGetRouter();
                if (!_singleBlockedByUi && router != null && !_singleDragging && !_suppressTap && Time.time - _singleStartTime <= TapMaxTime)
                {
                    TrySelect(_lastSinglePos, router);
                    MobileInputEvents.Tap(_lastSinglePos);
                }
                _singleDown = false;
                _singleDragging = false;
                _singleBlockedByUi = false;
                _suppressSingleUntilRelease = false;
                _suppressTap = false;
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus) return;
            ResetAllStates(notifyCancellation: true);
        }

        private void EmitTwoFingerPanFromDelta(
            Vector2 delta,
            Vector2 center,
            ref bool draggingState,
            float threshold = EmulatorPanDeltaThreshold
        )
        {
            if (delta.sqrMagnitude <= threshold)
                return;

            draggingState = true;
            _suppressTap = true;
            Vector2 panDelta = delta * EmulatorTwoFingerSwipeSensitivity;
            CallbackHub.CallAction<IMobileInputCallbacks>(
                cb => cb.OnTwoFingerSwipe(panDelta, center)
            );
            MobileInputEvents.Drag(panDelta, center);
        }

        private void OnDisable()
        {
            ResetAllStates(notifyCancellation: true);
        }

        private void ResetAllStates(bool notifyCancellation = false)
        {
            if (notifyCancellation)
                NotifyGestureCancellation();

            _singleDown = false;
            _singleDragging = false;
            _singleBlockedByUi = false;
            _twoFingerDown = false;
            _twoFingerDragging = false;
            _twoFingerLongPressDetected = false;
            _twoFingerBlockedByUi = false;
            _threeFingerDown = false;
            _threeFingerDragging = false;
            _threeFingerBlockedByUi = false;
            _mousePinchDown = false;
            _mousePinchBlockedByUi = false;
            _middlePanDragging = false;
            _suppressSingleUntilRelease = false;
            _suppressTap = false;
        }

        private void NotifyGestureCancellation()
        {
            bool hadPanGesture =
                _twoFingerDown ||
                _twoFingerDragging ||
                _mousePinchDown ||
                _middlePanDragging;

            if (hadPanGesture)
            {
                Vector2 center = _mousePinchDown ? _lastMousePinchPos : _lastTwoFingerPos;
                CallbackHub.CallAction<IMobileInputCallbacks>(
                    cb => cb.OnTwoFingerSwipe(Vector2.zero, center)
                );
                MobileInputEvents.TwoFingerSwipe(Vector2.zero, center);
                MobileInputEvents.Drag(Vector2.zero, center);
            }

            if (!HasActiveGestureState())
                return;

            var router = TryGetRouter();
            if (router != null)
                router.Cancel();
        }

        private bool IsPointerOverUiAt(Vector2 screenPosition)
        {
            if (EventSystem.current?.IsPointerOverGameObject() == true)
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

        private void HandleScroll()
        {
            if (!_ignoreUiBlocking && IsPointerOverUiAt(UnityEngine.Input.mousePosition))
                return;

            float scroll = UnityEngine.Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) <= 0.0001f) return;

            _pendingScrollZoom += scroll * ScrollZoomMultiplier;
        }

        private void ApplySmoothedScrollZoom()
        {
            if (Mathf.Abs(_pendingScrollZoom) <= ScrollZoomEpsilon)
            {
                _pendingScrollZoom = 0f;
                return;
            }

            var router = TryGetRouter();
            if (router == null) return;

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            float alpha = 1f - Mathf.Exp(-ScrollZoomSmoothing * dt);
            float zoomStep = _pendingScrollZoom * alpha;
            _pendingScrollZoom -= zoomStep;

            router.Zoom(zoomStep);
            MobileInputEvents.PinchZoom(zoomStep);
        }

        private void TrySelect(Vector3 screenPosition, InputRouter router)
        {
            var camera = GetRaycastCamera();
            if (camera == null)
            {
                router.Cancel();
                return;
            }

            Ray ray = camera.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Debug.Log($"[MobileInputEmulator] Click hit {hit.collider.name} at {hit.point}");
                SelectionData selection = new SelectionData(hit);
                router.Select(selection);
                CallbackHub.CallAction<IMobileInputCallbacks>(cb => cb.OnSelect(selection));
            }
            else
            {
                Debug.Log("[MobileInputEmulator] Click hit nothing.");
                router.Cancel();
            }
        }

        private void EnsureSettingsLoaded()
        {
            if (_settings != null) return;

            _settings = Resources.Load<MobileInputSettings>(SettingsResourceName);
            if (_settings != null) return;

            if (!_warnedMissingSettings)
            {
                _warnedMissingSettings = true;
                Debug.LogWarning(
                    $"[MobileInputEmulator] Missing settings asset. " +
                    $"Expected Resources/{SettingsResourceName}.asset. Using in-memory defaults."
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
                Debug.LogWarning("[MobileInputEmulator] InputRouter.Instance is null. Input routing disabled.");
            }
            return router;
        }

        private Camera GetRaycastCamera()
        {
            if (Camera.main != null) return Camera.main;
            return FindAnyObjectByType<Camera>();
        }
    }
}
#endif
