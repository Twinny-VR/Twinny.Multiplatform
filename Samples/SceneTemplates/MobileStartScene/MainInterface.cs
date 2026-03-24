using Concept.Core;
using System.Collections.Generic;
using System.Linq;
using Twinny.Core.Input;
using Twinny.Mobile.Cameras;
using Twinny.Mobile.Interactables;
using Twinny.Mobile.Navigation;
using Twinny.Shaders;
using UnityEngine;
using UnityEngine.UIElements;

namespace Twinny.Mobile.Samples
{
    [RequireComponent(typeof(UIDocument))]
    public class MainInterface : MonoBehaviour, IMobileUICallbacks, ITwinnyMobileCallbacks, IMobileInputCallbacks
    {
        private const string StartButtonName = "StartButton";
        private const string GyroToggleButtonName = "GyroToggleButton";
        private const string ModeToggleRowName = "ModeToggleRow";
        private const string ModeToggleName = "ModeToggle";
        private const string MainUiName = "MainUI";
        private const string ExperienceUiName = "ExperienceUI";
        private const string GlobalUiRootName = "GlobalUIRoot";
        private const string SceneOverlayRootName = "SceneOverlayRoot";
        private const string LoadingOverlayRootName = "LoadingOverlayRoot";
        private const string LoadingBarFillName = "LoadingBarFill";
        private const string CutoffSliderName = "CutoffSlider";
        private const string LevelSelectorName = "LevelSelector";
        private const string LevelSelectorButtonName = "LevelSelectorButton";
        private const string LevelSelectorMenuName = "LevelSelectorMenu";
        private const string LevelSelectorOptionClassName = "level-selector-option";
        private const string LevelSelectorOptionIsLastClassName = "is-last";
        private const string ModeToggleOnClassName = "mode-toggle--on";
        private const string ModeToggleOffClassName = "mode-toggle--off";
        private const string InjectedContentRootName = "InjectedContentRoot";
        private const string FloorHintRootName = "FloorHintRoot";
        private const float LoadingSortingOrder = 1000f;


        [SerializeField] private UIDocument _document;
        [SerializeField] private List<FloorData> _floorsData = new();
        [SerializeField] private float _floorHintScreenPadding = 16f;
        private float _defaultSortingOrder;
        private bool _hasDefaultSortingOrder;
        private float _defaultPanelSortingOrder;
        private bool _hasDefaultPanelSortingOrder;
        private Button _startButton;
        private Button _gyroToggleButton;
        private VisualElement _modeToggleRow;
        private Toggle _modeToggle;
        private VisualElement _mainUi;
        private VisualElement _experienceUi;
        private VisualElement _globalUiRoot;
        private VisualElement _sceneOverlayRoot;
        private VisualElement _loadingOverlayRoot;
        private VisualElement _loadingBarFill;
        private VisualElement _injectedContentRoot;
        private VisualElement _levelSelector;
        private VisualElement _levelSelectorMenu;
        private Button _levelSelectorButton;
        private readonly List<Button> _levelSelectorOptionButtons = new();
        private FloorHintWidget _floorHintWidget;
        private Slider _cutoffSlider;
        private Floor _selectedFloor;
        private bool _isFloorHintReadyToShow;
        private bool _hasFloorHintVisibilityState;
        private bool _isFloorHintVisible;
        private bool _isCutoffPointerDragging;
        private int _cutoffPointerId = -1;
        private bool _warnedMissingRoot;
        private bool _warnedMissingStart;
        private bool _warnedMissingGyroToggle;
        private bool _warnedMissingMainUi;
        private bool _warnedMissingExperienceUi;
        private bool _warnedMissingLoadingOverlayRoot;
        private bool _warnedMissingLoadingBar;
        private bool _warnedMissingCutoffSlider;
        private bool _warnedMissingInjectedRoot;
        private bool _gyroEnabled = true;
        private bool _isMockupMode;
        private bool _isDemoModeActive;

        private void OnEnable()
        {
            EnsureDocument();
            CaptureDefaultSortingOrder();
            CacheElements();
            RegisterCallbacks();
            CallbackHub.RegisterCallback<ITwinnyMobileCallbacks>(this);
            CallbackHub.RegisterCallback<IMobileUICallbacks>(this);
            CallbackHub.RegisterCallback<IMobileInputCallbacks>(this);
        }

        private void OnDisable()
        {
            UnregisterCallbacks();
            CallbackHub.UnregisterCallback<IMobileInputCallbacks>(this);
            CallbackHub.UnregisterCallback<IMobileUICallbacks>(this);
            CallbackHub.UnregisterCallback<ITwinnyMobileCallbacks>(this);
        }

        private void Update()
        {
            UpdateFloorHintPosition();
        }

        private void EnsureDocument()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>();
        }

        private void CaptureDefaultSortingOrder()
        {
            if (_document == null || _hasDefaultSortingOrder)
                return;

            _defaultSortingOrder = _document.sortingOrder;
            _hasDefaultSortingOrder = true;

            if (_document.panelSettings != null)
            {
                _defaultPanelSortingOrder = _document.panelSettings.sortingOrder;
                _hasDefaultPanelSortingOrder = true;
            }
        }

        private void CacheElements()
        {
            if (_document == null || _document.rootVisualElement == null)
            {
                WarnMissingRoot();
                return;
            }

            var root = _document.rootVisualElement;
            _startButton = root.Q<Button>(StartButtonName);
            _gyroToggleButton = root.Q<Button>(GyroToggleButtonName);
            _modeToggleRow = root.Q<VisualElement>(ModeToggleRowName);
            _modeToggle = _modeToggleRow?.Q<Toggle>(ModeToggleName);
            _mainUi = root.Q<VisualElement>(MainUiName);
            _experienceUi = root.Q<VisualElement>(ExperienceUiName);
            _globalUiRoot = root.Q<VisualElement>(GlobalUiRootName);
            _sceneOverlayRoot = root.Q<VisualElement>(SceneOverlayRootName);
            _loadingOverlayRoot = root.Q<VisualElement>(LoadingOverlayRootName);
            _loadingBarFill = root.Q<VisualElement>(LoadingBarFillName);
            _cutoffSlider = root.Q<Slider>(CutoffSliderName);
            _injectedContentRoot = root.Q<VisualElement>(InjectedContentRootName);
            _levelSelector = root.Q<VisualElement>(LevelSelectorName);
            _levelSelectorButton = root.Q<Button>(LevelSelectorButtonName);
            _levelSelectorMenu = root.Q<VisualElement>(LevelSelectorMenuName);
            PopulateLevelSelectorMenu();

            if (_startButton == null) WarnMissingStart();
            if (_gyroToggleButton == null) WarnMissingGyroToggle();
            if (_mainUi == null) WarnMissingMainUi();
            if (_experienceUi == null) WarnMissingExperienceUi();
            if (_loadingOverlayRoot == null) WarnMissingLoadingOverlayRoot();
            if (_loadingBarFill == null) WarnMissingLoadingBar();
            if (_cutoffSlider == null) WarnMissingCutoffSlider();
            if (_injectedContentRoot == null) WarnMissingInjectedRoot();

            if (_cutoffSlider != null)
            {
                ApplyCutoffSliderRange();
                _cutoffSlider.pageSize = 0.001f;
                SyncCutoffSliderAndShader(Shader.GetGlobalFloat("_CutoffHeight"));
            }

            if (_loadingOverlayRoot != null)
                _loadingOverlayRoot.style.display = DisplayStyle.None;

            if (_loadingBarFill != null)
                _loadingBarFill.style.width = Length.Percent(0f);

            if (_levelSelectorMenu != null)
                _levelSelectorMenu.style.display = DisplayStyle.None;
            SetLevelSelectorOpenState(false);

            // Initialize mode UI before first callback to avoid one-frame button flicker.
            _isMockupMode = true;
            ApplyModeButtons();
        }

        private void HandleFloorSelected(Floor floor)
        {
            if (floor == null) return;
            _selectedFloor = floor;
            _isFloorHintReadyToShow = false;
            EnsureFloorHintCreated();
            RefreshFloorHintContent();
            SetFloorHintVisibility(false);
        }

        private void HandleFloorUnselected(Floor floor)
        {
            if (_selectedFloor != floor) return;
            _selectedFloor = null;
            _isFloorHintReadyToShow = false;
            SetFloorHintVisibility(false);
        }

        private void EnsureFloorHintCreated()
        {
            if (_floorHintWidget != null) return;
            if (_injectedContentRoot == null) return;

            _floorHintWidget = new FloorHintWidget { name = FloorHintRootName };
            _floorHintWidget.style.position = Position.Absolute;
            _floorHintWidget.RegisterCallback<ClickEvent>(_ => HandleFloorHintClicked());
            _injectedContentRoot.Add(_floorHintWidget);
            SetFloorHintVisibility(false);
        }

        private void RefreshFloorHintContent()
        {
            if (_selectedFloor == null || _floorHintWidget == null) return;
            _floorHintWidget.SetFloor(_selectedFloor);
        }

        private static bool CanShowFloorHint(Floor floor)
        {
            if (floor == null)
                return false;

            if (floor is CinemachineFloor cinemachineFloor)
                return cinemachineFloor.ShowHint;

            return true;
        }

        private void UpdateFloorHintPosition()
        {
            if (_floorHintWidget == null || _selectedFloor == null || _document == null) return;
            if (_document.rootVisualElement?.panel == null) return;
            if (!CanShowFloorHint(_selectedFloor))
            {
                if (_isFloorHintVisible)
                    SetFloorHintVisibility(false);
                return;
            }

            if (!_isFloorHintReadyToShow)
            {
                if (_isFloorHintVisible)
                    SetFloorHintVisibility(false);
                return;
            }

            if (_isDemoModeActive)
            {
                if (_isFloorHintVisible)
                    SetFloorHintVisibility(false);
                return;
            }

            if (!_isFloorHintVisible)
                return;

            Camera cam = Camera.main;
            if (cam == null) cam = FindAnyObjectByType<Camera>();
            if (cam == null) return;

            Transform tracker = _selectedFloor.transform;
            if(_selectedFloor is CinemachineFloor cmFloor)
                 tracker = 
                cmFloor.TrackerPoint != null
                ? cmFloor.TrackerPoint.transform
                : cmFloor.TargetTransform;
            Vector3 anchorScreen = cam.WorldToScreenPoint(tracker.position);
            if (anchorScreen.z <= 0f)
            {
                if (_isFloorHintVisible)
                    SetFloorHintVisibility(false);
                return;
            }

            SetFloorHintVisibility(true);

            float x = anchorScreen.x + _floorHintScreenPadding;
            float y = anchorScreen.y;
            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(_document.rootVisualElement.panel, new Vector2(x, y));

            float hintWidth = _floorHintWidget.resolvedStyle.width > 0f ? _floorHintWidget.resolvedStyle.width : 220f;
            float hintHeight = _floorHintWidget.resolvedStyle.height > 0f ? _floorHintWidget.resolvedStyle.height : 64f;
            float panelWidth = _document.rootVisualElement.resolvedStyle.width;
            float panelHeight = _document.rootVisualElement.resolvedStyle.height;

            float left = Mathf.Clamp(panelPos.x, 0f, Mathf.Max(0f, panelWidth - hintWidth));
            float top = Mathf.Clamp(panelPos.y - (hintHeight * 0.5f), 0f, Mathf.Max(0f, panelHeight - hintHeight));

            _floorHintWidget.style.left = left;
            _floorHintWidget.style.top = top;
        }

        private void SetFloorHintVisibility(bool visible)
        {
            if (_floorHintWidget == null) return;
            if (_hasFloorHintVisibilityState && _isFloorHintVisible == visible) return;

            _hasFloorHintVisibilityState = true;
            _isFloorHintVisible = visible;
            _floorHintWidget.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void HandleFloorHintClicked()
        {
            if (_selectedFloor == null) return;
            
        
            if (!_selectedFloor.Data.HasImmersionScene) return;

            Floor selectedFloor = _selectedFloor;
            HandleFloorUnselected(selectedFloor);
            SetFloorHintVisibility(false);
            Debug.Log($"[MainInterface] Floor hint clicked: {selectedFloor.Data.ImmersionSceneName}");
            selectedFloor.Request();
        }



        private void RegisterCallbacks()
        {
            if (_startButton != null)
                _startButton.clicked += HandleStartClicked;

            if (_gyroToggleButton != null)
                _gyroToggleButton.clicked += HandleGyroToggleClicked;
            if (_modeToggle != null)
                _modeToggle.RegisterValueChangedCallback(HandleModeToggleChanged);

            if (_cutoffSlider != null)
                _cutoffSlider.RegisterValueChangedCallback(HandleCutoffChanged);

            if (_cutoffSlider != null)
            {
                _cutoffSlider.RegisterCallback<PointerDownEvent>(HandleCutoffPointerDown, TrickleDown.TrickleDown);
                _cutoffSlider.RegisterCallback<PointerMoveEvent>(HandleCutoffPointerMove, TrickleDown.TrickleDown);
                _cutoffSlider.RegisterCallback<PointerUpEvent>(HandleCutoffPointerUp, TrickleDown.TrickleDown);
                _cutoffSlider.RegisterCallback<PointerCancelEvent>(HandleCutoffPointerCancel, TrickleDown.TrickleDown);
            }

            if (_levelSelectorButton != null)
                _levelSelectorButton.clicked += HandleLevelSelectorButtonClicked;

            if (_document?.rootVisualElement != null)
                _document.rootVisualElement.RegisterCallback<PointerDownEvent>(HandleRootPointerDown, TrickleDown.TrickleDown);
        }

        private void UnregisterCallbacks()
        {
            if (_startButton != null)
                _startButton.clicked -= HandleStartClicked;

            if (_gyroToggleButton != null)
                _gyroToggleButton.clicked -= HandleGyroToggleClicked;
            if (_modeToggle != null)
                _modeToggle.UnregisterValueChangedCallback(HandleModeToggleChanged);

            if (_cutoffSlider != null)
                _cutoffSlider.UnregisterValueChangedCallback(HandleCutoffChanged);

            if (_cutoffSlider != null)
            {
                _cutoffSlider.UnregisterCallback<PointerDownEvent>(HandleCutoffPointerDown, TrickleDown.TrickleDown);
                _cutoffSlider.UnregisterCallback<PointerMoveEvent>(HandleCutoffPointerMove, TrickleDown.TrickleDown);
                _cutoffSlider.UnregisterCallback<PointerUpEvent>(HandleCutoffPointerUp, TrickleDown.TrickleDown);
                _cutoffSlider.UnregisterCallback<PointerCancelEvent>(HandleCutoffPointerCancel, TrickleDown.TrickleDown);
                ReleaseCutoffPointer();
            }

            if (_levelSelectorButton != null)
                _levelSelectorButton.clicked -= HandleLevelSelectorButtonClicked;

            if (_document?.rootVisualElement != null)
                _document.rootVisualElement.UnregisterCallback<PointerDownEvent>(HandleRootPointerDown, TrickleDown.TrickleDown);
        }

        private void HandleLevelSelectorButtonClicked()
        {
            if (_levelSelectorMenu == null)
                return;

            bool isOpening = _levelSelectorMenu.style.display != DisplayStyle.Flex;
            _levelSelectorMenu.style.display = isOpening ? DisplayStyle.Flex : DisplayStyle.None;
            SetLevelSelectorOpenState(isOpening);
        }

        private void HandleLevelSelectorOptionClicked(FloorData floorData)
        {
            if (floorData == null)
                return;

            string value = floorData.Title?.Trim();
            if (_levelSelectorButton != null)
                _levelSelectorButton.text = value;

            if (_levelSelectorMenu != null)
                _levelSelectorMenu.style.display = DisplayStyle.None;

            SetLevelSelectorOpenState(false);
            TwinnyMobileManager.SceneRequest(floorData);
        }

        private void HandleRootPointerDown(PointerDownEvent evt)
        {
            if (_levelSelector == null || _levelSelectorMenu == null)
                return;

            if (_levelSelectorMenu.style.display != DisplayStyle.Flex)
                return;

            if (_levelSelector.worldBound.Contains(evt.position))
                return;

            if (_levelSelectorMenu.worldBound.Contains(evt.position))
                return;

            _levelSelectorMenu.style.display = DisplayStyle.None;
            SetLevelSelectorOpenState(false);
        }

        private void SetLevelSelectorOpenState(bool isOpen)
        {
            if (_levelSelector == null)
                return;

            if (isOpen)
                _levelSelector.AddToClassList("is-open");
            else
                _levelSelector.RemoveFromClassList("is-open");
        }

        private void PopulateLevelSelectorMenu()
        {
            if (_levelSelectorMenu == null)
                return;

            _levelSelectorMenu.Clear();
            _levelSelectorOptionButtons.Clear();

            List<FloorData> validFloors = _floorsData
                .Where(floor => floor != null && !string.IsNullOrWhiteSpace(floor.Title))
                .ToList();

            if (validFloors.Count == 0)
            {
                if (_levelSelectorButton != null)
                    _levelSelectorButton.text = "Sem pisos";
                return;
            }

            for (int i = 0; i < validFloors.Count; i++)
            {
                FloorData floor = validFloors[i];
                string floorName = floor.Title.Trim();
                var optionButton = new Button(() => HandleLevelSelectorOptionClicked(floor))
                {
                    text = floorName
                };

                optionButton.AddToClassList(LevelSelectorOptionClassName);
                if (i == validFloors.Count - 1)
                    optionButton.AddToClassList(LevelSelectorOptionIsLastClassName);
                _levelSelectorMenu.Add(optionButton);
                _levelSelectorOptionButtons.Add(optionButton);
            }

            if (_levelSelectorButton != null)
                _levelSelectorButton.text = validFloors[0].Title.Trim();
        }

        private void SyncLevelSelectorWithLoadedScene(string loadedSceneName)
        {
            if (_levelSelectorButton == null)
                return;

            if (string.IsNullOrWhiteSpace(loadedSceneName))
                return;

            FloorData matchedFloor = _floorsData.FirstOrDefault(floor =>
                floor != null &&
                !string.IsNullOrWhiteSpace(floor.ImmersionSceneName) &&
                string.Equals(
                    floor.ImmersionSceneName.Trim(),
                    loadedSceneName,
                    System.StringComparison.OrdinalIgnoreCase
                ));

            if (matchedFloor == null || string.IsNullOrWhiteSpace(matchedFloor.Title))
                return;

            _levelSelectorButton.text = matchedFloor.Title.Trim();
        }

        private void HandleStartClicked()
        {
            CallbackHub.CallAction<IMobileUICallbacks>(callback => callback.OnStartExperienceRequested(TwinnyMobileRuntime.GetDefaultSceneName()));
        }

        private void HandleModeToggleChanged(ChangeEvent<bool> evt)
        {
            UpdateModeToggleVisualState(evt.newValue);

            if (evt.newValue)
                CallbackHub.CallAction<IMobileUICallbacks>(callback => callback.OnImmersiveRequested());
            else
                CallbackHub.CallAction<IMobileUICallbacks>(callback => callback.OnMockupRequested());
        }

        private void HandleGyroToggleClicked()
        {
            _gyroEnabled = !_gyroEnabled;
            UpdateGyroToggleLabel();
            CallbackHub.CallAction<IMobileUICallbacks>(callback => callback.OnGyroscopeToggled(_gyroEnabled));
        }

        private void HandleCutoffChanged(ChangeEvent<float> evt)
        {
            SyncCutoffSliderAndShader(evt.newValue);
        }

        private void HandleCutoffPointerDown(PointerDownEvent evt)
        {
            if (_cutoffSlider == null)
                return;

            _isCutoffPointerDragging = true;
            _cutoffPointerId = evt.pointerId;
            _cutoffSlider.CapturePointer(_cutoffPointerId);
            SetCutoffFromPointer(evt.position);
            evt.StopImmediatePropagation();
        }

        private void HandleCutoffPointerMove(PointerMoveEvent evt)
        {
            if (_cutoffSlider == null || !_isCutoffPointerDragging || evt.pointerId != _cutoffPointerId)
                return;

            SetCutoffFromPointer(evt.position);
            evt.StopImmediatePropagation();
        }

        private void HandleCutoffPointerUp(PointerUpEvent evt)
        {
            if (_cutoffSlider == null || evt.pointerId != _cutoffPointerId)
                return;

            SetCutoffFromPointer(evt.position);
            ReleaseCutoffPointer();
            evt.StopImmediatePropagation();
        }

        private void HandleCutoffPointerCancel(PointerCancelEvent evt)
        {
            if (_cutoffSlider == null || evt.pointerId != _cutoffPointerId)
                return;

            ReleaseCutoffPointer();
            evt.StopImmediatePropagation();
        }

        private void SetCutoffFromPointer(Vector2 pointerPosition)
        {
            if (_cutoffSlider == null)
                return;

            VisualElement dragContainer = _cutoffSlider.Q("unity-drag-container");
            Rect dragRect = dragContainer != null ? dragContainer.worldBound : _cutoffSlider.worldBound;
            if (dragRect.height <= 0f)
                return;

            float t = Mathf.InverseLerp(dragRect.yMax, dragRect.yMin, pointerPosition.y);
            float value = Mathf.Lerp(_cutoffSlider.lowValue, _cutoffSlider.highValue, t);
            _cutoffSlider.value = Mathf.Clamp(value, _cutoffSlider.lowValue, _cutoffSlider.highValue);
        }

        private void ReleaseCutoffPointer()
        {
            if (_cutoffSlider != null && _cutoffPointerId >= 0 && _cutoffSlider.HasPointerCapture(_cutoffPointerId))
                _cutoffSlider.ReleasePointer(_cutoffPointerId);

            _isCutoffPointerDragging = false;
            _cutoffPointerId = -1;
        }

        public void OnMaxWallHeightRequested(float height)
        {
            if (_cutoffSlider == null)
                return;

            ApplyCutoffSliderRange();
            SyncCutoffSliderAndShader(height);
        }

        public void OnImmersiveRequested(FloorData data)
        {
            SetModeButtons(isMockup: false);
            SetFloorHintVisibility(false);
        }

        public void OnMockupRequested(FloorData data)
        {
            SetModeButtons(isMockup: true);
            SetFloorHintVisibility(false);
        }

        public void OnStartExperienceRequested(string sceneName) { }

        public void OnEnterImmersiveMode()
        {
            if (_cutoffSlider == null)
                return;

            ApplyCutoffSliderRange();
            SyncCutoffSliderAndShader(_cutoffSlider.highValue);
        }
        public void OnExitImmersiveMode(){ }

        public void OnEnterMockupMode()
        {
            SetModeButtons(isMockup: true);
            if (_cutoffSlider != null)
            {
                ApplyCutoffSliderRange();
                SyncCutoffSliderAndShader(Shader.GetGlobalFloat("_CutoffHeight"));
            }
        }
        public void OnExitMockupMode()
        {
            SetModeButtons(isMockup: false);
        }
        public void OnEnterDemoMode()
        {
            _isDemoModeActive = true;
            _isFloorHintReadyToShow = false;
            SetFloorHintVisibility(false);
        }
        public void OnExitDemoMode()
        {
            _isDemoModeActive = false;
        }

        public void OnFloorSelected(Floor floor) { }
        public void OnFloorFocused(Floor floor)
        {
            if (floor == null)
            {
                _isFloorHintReadyToShow = false;
                SetFloorHintVisibility(false);
                return;
            }

            if (_selectedFloor != null && floor != _selectedFloor)
            {
                _isFloorHintReadyToShow = false;
                SetFloorHintVisibility(false);
                return;
            }

            if (!CanShowFloorHint(floor))
            {
                _selectedFloor = floor;
                _isFloorHintReadyToShow = false;
                SetFloorHintVisibility(false);
                return;
            }

            _selectedFloor ??= floor;

            _isFloorHintReadyToShow = true;
            EnsureFloorHintCreated();
            RefreshFloorHintContent();
            SetFloorHintVisibility(true);
            UpdateFloorHintPosition();
        }
        public void OnFloorUnselected(Floor floor)
        {
            HandleFloorUnselected(floor);
        }

        public void OnExperienceLoaded()
        {
            if (_mainUi != null)
                _mainUi.style.display = DisplayStyle.None;

            if (_experienceUi != null)
                _experienceUi.style.display = DisplayStyle.Flex;

            SetModeButtons(isMockup: true);
            UpdateGyroToggleLabel();
        }

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
        public void OnSceneLoadStart(string sceneName)
        {
            SetDocumentSortingOrder(LoadingSortingOrder);
            SetSceneRootsVisibility(false);

            if (_loadingOverlayRoot != null)
                _loadingOverlayRoot.style.display = DisplayStyle.Flex;

            if (_loadingBarFill != null)
                _loadingBarFill.style.width = Length.Percent(0f);
        }

        public void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene)
        {
            SetSceneRootsVisibility(true);

            if (_loadingOverlayRoot != null)
                _loadingOverlayRoot.style.display = DisplayStyle.None;

            RestoreSortingOrder();
            SyncLevelSelectorWithLoadedScene(scene.name);
            ApplyModeButtons();
        }
        public void OnTeleportToLandMark(int landMarkIndex) { }
        public void OnSkyboxHDRIChanged(Material material) { }

        public void OnLoadingProgressChanged(float progress)
        {
            if (_loadingBarFill == null)
                return;

            float clamped = Mathf.Clamp01(progress);
            _loadingBarFill.style.width = Length.Percent(clamped * 100f);
        }

        public void OnGyroscopeToggled(bool enabled) { }

        public void OnPrimaryDown(float x, float y) { }
        public void OnPrimaryUp(float x, float y) { }
        public void OnPrimaryDrag(float dx, float dy) { }
        public void OnSelect(SelectionData selection)
        {
            if (selection.Target == null)
            {
                if (_selectedFloor != null)
                    HandleFloorUnselected(_selectedFloor);
                return;
            }

            Floor floor = selection.Target.GetComponentInParent<Floor>();
            if (floor == null)
            {
                if (_selectedFloor != null)
                    HandleFloorUnselected(_selectedFloor);
                return;
            }

            HandleFloorSelected(floor);
        }
        public void OnCancel()
        {
            if (_selectedFloor != null)
                HandleFloorUnselected(_selectedFloor);
        }
        public void OnZoom(float delta) { }
        public void OnTwoFingerTap(Vector2 position) { }
        public void OnTwoFingerLongPress(Vector2 position) { }
        public void OnTwoFingerSwipe(Vector2 direction, Vector2 startPosition)
        {
            if (_selectedFloor != null)
                HandleFloorUnselected(_selectedFloor);
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

        private void WarnMissingRoot()
        {
            if (_warnedMissingRoot) return;
            _warnedMissingRoot = true;
            Debug.LogWarning("[MainInterface] UIDocument or rootVisualElement not found.");
        }

        private void WarnMissingStart()
        {
            if (_warnedMissingStart) return;
            _warnedMissingStart = true;
            Debug.LogWarning("[MainInterface] StartButton not found in UXML.");
        }

        private void WarnMissingGyroToggle()
        {
            if (_warnedMissingGyroToggle) return;
            _warnedMissingGyroToggle = true;
            Debug.LogWarning("[MainInterface] GyroToggleButton not found in UXML.");
        }

        private void WarnMissingMainUi()
        {
            if (_warnedMissingMainUi) return;
            _warnedMissingMainUi = true;
            Debug.LogWarning("[MainInterface] MainUI not found in UXML.");
        }

        private void WarnMissingExperienceUi()
        {
            if (_warnedMissingExperienceUi) return;
            _warnedMissingExperienceUi = true;
            Debug.LogWarning("[MainInterface] ExperienceUI not found in UXML.");
        }

        private void WarnMissingLoadingOverlayRoot()
        {
            if (_warnedMissingLoadingOverlayRoot) return;
            _warnedMissingLoadingOverlayRoot = true;
            Debug.LogWarning("[MainInterface] LoadingOverlayRoot not found in UXML.");
        }

        private void WarnMissingLoadingBar()
        {
            if (_warnedMissingLoadingBar) return;
            _warnedMissingLoadingBar = true;
            Debug.LogWarning("[MainInterface] LoadingBarFill not found in UXML.");
        }

        private void WarnMissingCutoffSlider()
        {
            if (_warnedMissingCutoffSlider) return;
            _warnedMissingCutoffSlider = true;
            Debug.LogWarning("[MainInterface] CutoffSlider not found in UXML.");
        }

        private void WarnMissingInjectedRoot()
        {
            if (_warnedMissingInjectedRoot) return;
            _warnedMissingInjectedRoot = true;
            Debug.LogWarning("[MainInterface] InjectedContentRoot not found in UXML.");
        }

        private void SetModeButtons(bool isMockup)
        {
            _isMockupMode = isMockup;
            ApplyModeButtons();
        }

        private void ApplyModeButtons()
        {
            bool canShowMockupControls = HasActiveOrbitalHandlerInstance() && MobileFpsNavigation.HasActiveInstance;
            bool canShowAlphaSlider = _isMockupMode && AlphaClipper.Instance;
            bool canShowGyroToggle = !_isMockupMode && IsMobileWebGlRuntime();

            if (_gyroToggleButton != null)
                _gyroToggleButton.style.display = canShowGyroToggle ? DisplayStyle.Flex : DisplayStyle.None;

            if (_cutoffSlider != null)
                _cutoffSlider.style.display = canShowAlphaSlider ? DisplayStyle.Flex : DisplayStyle.None;

            if (_modeToggle != null ) 
            {
                _modeToggle.SetValueWithoutNotify(!_isMockupMode);
                UpdateModeToggleVisualState(!_isMockupMode);
                _modeToggleRow.style.display = canShowMockupControls ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void UpdateModeToggleVisualState(bool isOn)
        {
            if (_modeToggle == null)
                return;

            if (isOn)
            {
                _modeToggle.RemoveFromClassList(ModeToggleOffClassName);
                _modeToggle.AddToClassList(ModeToggleOnClassName);
            }
            else
            {
                _modeToggle.RemoveFromClassList(ModeToggleOnClassName);
                _modeToggle.AddToClassList(ModeToggleOffClassName);
            }
        }

        private static bool HasActiveOrbitalHandlerInstance()
        {
            const string orbitalHandlerTypeName = "Twinny.Mobile.Cameras.MobileCinemachineOrbitalHandler";
            MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || !behaviour.isActiveAndEnabled)
                    continue;

                if (behaviour.GetType().FullName == orbitalHandlerTypeName)
                    return true;
            }

            return false;
        }

        private void UpdateGyroToggleLabel()
        {
            if (_gyroToggleButton == null) return;
            _gyroToggleButton.text = _gyroEnabled ? "Gyro On" : "Gyro Off";
        }

        private static bool IsMobileWebGlRuntime()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Application.isMobilePlatform || SystemInfo.deviceType == DeviceType.Handheld;
#else
            return false;
#endif
        }

        private void SetSceneRootsVisibility(bool visible)
        {
            DisplayStyle display = visible ? DisplayStyle.Flex : DisplayStyle.None;

            if (_globalUiRoot != null)
                _globalUiRoot.style.display = display;

            if (_injectedContentRoot != null)
                _injectedContentRoot.style.display = display;

            if (_sceneOverlayRoot != null)
                _sceneOverlayRoot.style.display = display;
        }

        private void SetDocumentSortingOrder(float sortingOrder)
        {
            if (_document == null)
                return;

            _document.sortingOrder = sortingOrder;

            if (_document.panelSettings != null)
                _document.panelSettings.sortingOrder = sortingOrder;
        }

        private void RestoreSortingOrder()
        {
            SetDocumentSortingOrder(_defaultSortingOrder);

            if (_document == null || _document.panelSettings == null || !_hasDefaultPanelSortingOrder)
                return;

            _document.panelSettings.sortingOrder = _defaultPanelSortingOrder;
        }

        private void ApplyCutoffSliderRange()
        {
            if (_cutoffSlider == null)
                return;

            Vector2 minMaxWallHeight = AlphaClipper.MinMaxWallHeight;
            float minHeight = Mathf.Min(minMaxWallHeight.x, minMaxWallHeight.y);
            float maxHeight = Mathf.Max(minMaxWallHeight.x, minMaxWallHeight.y);

            _cutoffSlider.lowValue = minHeight;
            _cutoffSlider.highValue = maxHeight;
        }

        private float ClampCutoffHeight(float height)
        {
            Vector2 minMaxWallHeight = AlphaClipper.MinMaxWallHeight;
            float minHeight = Mathf.Min(minMaxWallHeight.x, minMaxWallHeight.y);
            float maxHeight = Mathf.Max(minMaxWallHeight.x, minMaxWallHeight.y);
            return Mathf.Clamp(height, minHeight, maxHeight);
        }

        private void SyncCutoffSliderAndShader(float height)
        {
            float clampedHeight = ClampCutoffHeight(height);

            if (_cutoffSlider != null)
                _cutoffSlider.SetValueWithoutNotify(clampedHeight);

            AlphaClipper.SetCutoffHeight(clampedHeight);
        }
    }
}
