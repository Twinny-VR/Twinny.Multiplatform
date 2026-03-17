using Twinny.Mobile.Cameras;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Twinny.Mobile.Editor.Cameras
{
    [CustomEditor(typeof(CinemachineFloor))]
    [CanEditMultipleObjects]
    public class CinemachineFloorEditor : UnityEditor.Editor
    {
        private const string UxmlPath = "Packages/com.twinny.twe26/Editor/Shared/ComponentInspectorTemplate.uxml";
        private const string UssPath = "Packages/com.twinny.twe26/Editor/Shared/ComponentInspectorTemplate.uss";
        private const string IconsPath = "Packages/com.twinny.mobile/Editor/Cameras/Icons/icons.png";
        private const string FloorIconName = "icons_5";
        private const string TitleFontPath = "Packages/com.twinny.twe26/Editor/SetupGuide/Resources/Fonts/DINNextLTPro-Condensed.otf";
        private const float SliderStep = 0.1f;

        private VisualTreeAsset _visualTree;
        private StyleSheet _styleSheet;

        public override VisualElement CreateInspectorGUI()
        {
            LoadAssets();
            var root = _visualTree.CloneTree();
            root.styleSheets.Add(_styleSheet);

            ApplyTitle(root);
            ApplyHeroIcon(root);
            ApplyTitleFont(root);

            VisualElement contentRoot = GetContentRoot(root);
            AddIdentityFields(CreateSection(contentRoot, "Identity"));
            AddCameraTargetFields(CreateSection(contentRoot, "Camera Target"));
            AddAlphaClipFields(CreateSection(contentRoot, "Alpha Clipper"));
            AddEventFields(CreateSection(contentRoot, "Events"));
            AddComputedInfo(CreateSection(contentRoot, "Runtime Preview"));

            return root;
        }

        private void LoadAssets()
        {
            if (_visualTree == null)
                _visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);

            if (_styleSheet == null)
                _styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
        }

        private static VisualElement GetContentRoot(VisualElement root)
        {
            return root.Q<VisualElement>(className: "root") ?? root;
        }

        private static VisualElement CreateSection(VisualElement root, string title)
        {
            var section = new VisualElement();
            section.AddToClassList("section");

            var label = new Label(title);
            label.AddToClassList("section-title");
            section.Add(label);

            var fields = new VisualElement();
            fields.AddToClassList("fields");
            section.Add(fields);

            root.Add(section);
            return fields;
        }

        private void ApplyTitle(VisualElement root)
        {
            if (root == null) return;
            var title = root.Q<Label>(className: "hero-title");
            if (title != null) title.text = "Cinemachine Floor";
            var subtitle = root.Q<Label>(className: "hero-subtitle");
            if (subtitle != null) subtitle.text = "Interactable floor metadata";
        }

        private void AddIdentityFields(VisualElement container)
        {
            if (container == null) return;
            SerializedProperty dataProp = serializedObject.FindProperty("_data");
            if (dataProp == null) return;

            SerializedProperty titleProp = dataProp.FindPropertyRelative("<Title>k__BackingField");
            SerializedProperty subtitleProp = dataProp.FindPropertyRelative("<Subtitle>k__BackingField");
            SerializedProperty immersionSceneProp = dataProp.FindPropertyRelative("<ImmersionSceneName>k__BackingField");
            SerializedProperty sceneOpenModeProp = dataProp.FindPropertyRelative("<SceneOpenMode>k__BackingField");

            AddProperty(container, titleProp, serializedObject);
            AddProperty(container, subtitleProp, serializedObject);
            AddProperty(container, immersionSceneProp, serializedObject);

            if (sceneOpenModeProp != null)
            {
                var sceneOpenModeField = new PropertyField(sceneOpenModeProp);
                sceneOpenModeField.Bind(serializedObject);
                container.Add(sceneOpenModeField);

                void RefreshSceneOpenModeVisibility()
                {
                    serializedObject.Update();
                    bool hasSceneName = !string.IsNullOrWhiteSpace(immersionSceneProp?.stringValue);
                    sceneOpenModeField.style.display = hasSceneName ? DisplayStyle.Flex : DisplayStyle.None;
                }

                RefreshSceneOpenModeVisibility();
                sceneOpenModeField.TrackPropertyValue(immersionSceneProp, _ => RefreshSceneOpenModeVisibility());
            }
        }

        private void AddCameraTargetFields(VisualElement container)
        {
            if (container == null) return;

            SerializedProperty useFocusPointProp = serializedObject.FindProperty("_useFocusPoint");
            SerializedProperty trackerPointProp = serializedObject.FindProperty("_trackerPoint");

            AddSlider(container, "_maxWallHeight", 0f, 20f, "Max Wall Height");
            AddProperty(container, useFocusPointProp, serializedObject);
            AddProperty(container, trackerPointProp, serializedObject);
            AddCreateTrackerPointButton(container, trackerPointProp);
            AddTrackerPointProperties(container, trackerPointProp);
        }

        private void AddEventFields(VisualElement container)
        {
            if (container == null) return;
            AddProperty(container, serializedObject.FindProperty("_onSelect"), serializedObject);
            AddProperty(container, serializedObject.FindProperty("_onFocused"), serializedObject);
            AddProperty(container, serializedObject.FindProperty("_onUnselect"), serializedObject);
        }

        private void AddAlphaClipFields(VisualElement container)
        {
            if (container == null) return;

            SerializedProperty applyAlphaClipProp = serializedObject.FindProperty("_applyAlphaClip");
            SerializedProperty alphaClipHeightProp = serializedObject.FindProperty("_alphaClipHeight");

            AddProperty(container, applyAlphaClipProp, serializedObject);
            if (applyAlphaClipProp == null || alphaClipHeightProp == null)
                return;

            var alphaClipHeightField = new PropertyField(alphaClipHeightProp);
            alphaClipHeightField.Bind(serializedObject);
            container.Add(alphaClipHeightField);

            void RefreshAlphaClipVisibility()
            {
                serializedObject.Update();
                alphaClipHeightField.style.display = applyAlphaClipProp.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
            }

            RefreshAlphaClipVisibility();
            container.TrackPropertyValue(applyAlphaClipProp, _ => RefreshAlphaClipVisibility());
        }

        private void AddComputedInfo(VisualElement container)
        {
            if (container == null) return;
            if (targets != null && targets.Length != 1) return;
            var floor = target as CinemachineFloor;
            if (floor == null) return;

            container.Add(new Label("Runtime Preview"));
            AddHelpLabel(container, $"Target Position: {floor.TargetPosition}");
        }

        private void AddProperty(VisualElement container, SerializedProperty root, SerializedObject owner)
        {
            if (root == null) return;
            var field = new PropertyField(root);
            field.Bind(owner);
            container.Add(field);
        }

        private void AddSlider(
            VisualElement container,
            string propertyName,
            float min,
            float max,
            string label
        )
        {
            SerializedProperty prop = serializedObject.FindProperty(propertyName);
            if (prop == null) return;
            AddSlider(container, prop, min, max, label, serializedObject);
        }

        private void AddSlider(
            VisualElement container,
            SerializedProperty prop,
            float min,
            float max,
            string label,
            SerializedObject owner
        )
        {
            var row = new VisualElement();
            row.AddToClassList("row");

            var labelEl = new Label(label);
            labelEl.AddToClassList("row-label");

            var slider = new Slider(min, max);
            slider.AddToClassList("row-field");
            var field = new FloatField();
            field.AddToClassList("mini-field");
            slider.BindProperty(prop);
            field.BindProperty(prop);

            slider.RegisterValueChangedCallback(evt =>
            {
                float snapped = Snap(evt.newValue, SliderStep);
                if (!Mathf.Approximately(evt.newValue, snapped))
                    slider.SetValueWithoutNotify(snapped);
                prop.floatValue = snapped;
                owner.ApplyModifiedProperties();
                field.SetValueWithoutNotify(snapped);
            });

            field.RegisterValueChangedCallback(evt =>
            {
                float snapped = Snap(evt.newValue, SliderStep);
                if (!Mathf.Approximately(evt.newValue, snapped))
                    field.SetValueWithoutNotify(snapped);
                prop.floatValue = snapped;
                owner.ApplyModifiedProperties();
                slider.SetValueWithoutNotify(snapped);
            });

            var fieldRow = new VisualElement();
            fieldRow.AddToClassList("inline-values");
            fieldRow.Add(slider);
            fieldRow.Add(field);

            row.Add(labelEl);
            row.Add(fieldRow);
            container.Add(row);
        }

        private void AddRadiansAsDegreesSlider(
            VisualElement container,
            SerializedObject owner,
            string propertyName,
            string label
        )
        {
            if (container == null || owner == null) return;

            SerializedProperty prop = owner.FindProperty(propertyName);
            if (prop == null) return;

            var row = new VisualElement();
            row.AddToClassList("row");

            var labelEl = new Label(label);
            labelEl.AddToClassList("row-label");

            var slider = new Slider(0f, 360f);
            slider.AddToClassList("row-field");
            var field = new FloatField();
            field.AddToClassList("mini-field");

            float currentDegrees = Mathf.Rad2Deg * prop.floatValue;
            slider.SetValueWithoutNotify(currentDegrees);
            field.SetValueWithoutNotify(currentDegrees);

            void ApplyDegrees(float degrees)
            {
                float snapped = Snap(Mathf.Clamp(degrees, 0f, 360f), SliderStep);
                prop.floatValue = Mathf.Deg2Rad * snapped;
                owner.ApplyModifiedProperties();
                slider.SetValueWithoutNotify(snapped);
                field.SetValueWithoutNotify(snapped);
            }

            slider.RegisterValueChangedCallback(evt => ApplyDegrees(evt.newValue));
            field.RegisterValueChangedCallback(evt => ApplyDegrees(evt.newValue));

            var fieldRow = new VisualElement();
            fieldRow.AddToClassList("inline-values");
            fieldRow.Add(slider);
            fieldRow.Add(field);

            row.Add(labelEl);
            row.Add(fieldRow);
            container.Add(row);
        }

        private static float Snap(float value, float step)
        {
            if (step <= 0f) return value;
            return Mathf.Round(value / step) * step;
        }

        private void AddHelpLabel(VisualElement container, string text)
        {
            var label = new Label(text);
            label.AddToClassList("inline-note");
            container.Add(label);
        }

        private static VisualElement CreateAxisToggle(string axis, SerializedProperty property)
        {
            var wrap = new VisualElement();
            wrap.AddToClassList("axis-toggle-item");

            var axisLabel = new Label(axis);
            axisLabel.AddToClassList("axis-toggle-label");

            var toggle = new Toggle();
            toggle.AddToClassList("axis-toggle");
            toggle.BindProperty(property);

            wrap.Add(axisLabel);
            wrap.Add(toggle);
            return wrap;
        }

        private void AddCreateTrackerPointButton(VisualElement container, SerializedProperty trackerPointProp)
        {
            if (container == null || trackerPointProp == null) return;

            var button = new Button(CreateTrackerPoint)
            {
                text = "Add Tracker Point"
            };
            container.Add(button);

            void RefreshVisibility()
            {
                serializedObject.Update();
                bool showButton = targets != null
                    && targets.Length == 1
                    && trackerPointProp.objectReferenceValue == null;
                button.style.display = showButton ? DisplayStyle.Flex : DisplayStyle.None;
            }

            RefreshVisibility();
            container.TrackPropertyValue(trackerPointProp, _ => RefreshVisibility());
        }

        private void CreateTrackerPoint()
        {
            if (targets == null || targets.Length != 1) return;

            var floor = target as CinemachineFloor;
            if (floor == null) return;

            SerializedProperty trackerPointProp = serializedObject.FindProperty("_trackerPoint");
            if (trackerPointProp == null || trackerPointProp.objectReferenceValue != null) return;

            GameObject trackerGo = new GameObject("TrackerPoint");
            Undo.RegisterCreatedObjectUndo(trackerGo, "Create Tracker Point");
            Undo.SetTransformParent(trackerGo.transform, floor.transform, "Parent Tracker Point");
            trackerGo.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            CinemachineTracker poi = Undo.AddComponent<CinemachineTracker>(trackerGo);
            Undo.RecordObject(floor, "Assign Tracker Point");
            trackerPointProp.objectReferenceValue = poi;
            serializedObject.ApplyModifiedProperties();

            EditorUtility.SetDirty(floor);
            Selection.activeGameObject = trackerGo;
        }

        private void AddTrackerPointProperties(VisualElement container, SerializedProperty trackerPointProp)
        {
            if (container == null || trackerPointProp == null) return;

            var trackerContainer = new VisualElement();
            container.Add(trackerContainer);

            void RebuildTrackerProperties()
            {
                trackerContainer.Clear();
                serializedObject.Update();

                if (trackerPointProp.objectReferenceValue is not CinemachineTracker poi)
                {
                    trackerContainer.style.display = DisplayStyle.None;
                    return;
                }

                trackerContainer.style.display = DisplayStyle.Flex;
                trackerContainer.Add(new Label("Tracker Point Settings"));
                trackerContainer.Add(new HelpBox(
                    "Only non-zero override values are considered at runtime. Zero-valued fields are treated as unset and will fall back to the handler defaults.",
                    HelpBoxMessageType.Warning
                ));

                SerializedObject poiObject = new SerializedObject(poi);
                AddSlider(trackerContainer, poiObject.FindProperty("_targetRadius"), 0f, 200f, "Target Radius", poiObject);
                AddProperty(trackerContainer, poiObject.FindProperty("_radiusLimits"), poiObject);

                SerializedProperty overrideRotationProp = poiObject.FindProperty("_overrideRotation");
                AddProperty(trackerContainer, overrideRotationProp, poiObject);

                var rotationContainer = new VisualElement();
                trackerContainer.Add(rotationContainer);
                AddRadiansAsDegreesSlider(rotationContainer, poiObject, "_targetPan", "Target Pan");
                AddRadiansAsDegreesSlider(rotationContainer, poiObject, "_targetTilt", "Target Tilt");
                AddProperty(trackerContainer, poiObject.FindProperty("_verticalAxisLimits"), poiObject);
                AddProperty(trackerContainer, poiObject.FindProperty("_maxPanDistance"), poiObject);
                AddProperty(trackerContainer, poiObject.FindProperty("_enablePanLimit"), poiObject);

                void RefreshRotationVisibility()
                {
                    poiObject.Update();
                    rotationContainer.style.display = overrideRotationProp != null && overrideRotationProp.boolValue
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                }

                RefreshRotationVisibility();
                if (overrideRotationProp != null)
                    trackerContainer.TrackPropertyValue(overrideRotationProp, _ => RefreshRotationVisibility());

                SerializedProperty overridePanConstraintProp = poiObject.FindProperty("_overridePanConstraint");
                AddProperty(trackerContainer, overridePanConstraintProp, poiObject);

                var panConstraintContainer = new VisualElement();
                trackerContainer.Add(panConstraintContainer);

                SerializedProperty lockX = poiObject.FindProperty("_lockPanX");
                SerializedProperty lockY = poiObject.FindProperty("_lockPanY");
                SerializedProperty lockZ = poiObject.FindProperty("_lockPanZ");

                if (lockX != null && lockY != null && lockZ != null)
                {
                    var row = new VisualElement();
                    row.AddToClassList("row");

                    var label = new Label("Pan Constraint");
                    label.AddToClassList("row-label");

                    var values = new VisualElement();
                    values.AddToClassList("inline-values");
                    values.AddToClassList("axis-toggle-group");

                    values.Add(CreateAxisToggle("X", lockX));
                    values.Add(CreateAxisToggle("Y", lockY));
                    values.Add(CreateAxisToggle("Z", lockZ));

                    row.Add(label);
                    row.Add(values);
                    panConstraintContainer.Add(row);
                }

                void RefreshPanConstraintVisibility()
                {
                    poiObject.Update();
                    panConstraintContainer.style.display = overridePanConstraintProp != null && overridePanConstraintProp.boolValue
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                }

                RefreshPanConstraintVisibility();
                if (overridePanConstraintProp != null)
                    trackerContainer.TrackPropertyValue(overridePanConstraintProp, _ => RefreshPanConstraintVisibility());

                SerializedProperty overrideDeoccluderProp = poiObject.FindProperty("_overrideDeoccluder");
                AddProperty(trackerContainer, overrideDeoccluderProp, poiObject);

                var deoccluderContainer = new VisualElement();
                trackerContainer.Add(deoccluderContainer);
                AddSlider(deoccluderContainer, poiObject.FindProperty("_overrideDeoccluderRadius"), 0f, 500f, "Deoccluder Radius", poiObject);

                void RefreshDeoccluderVisibility()
                {
                    poiObject.Update();
                    deoccluderContainer.style.display = overrideDeoccluderProp != null && overrideDeoccluderProp.boolValue
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                }

                RefreshDeoccluderVisibility();
                if (overrideDeoccluderProp != null)
                    trackerContainer.TrackPropertyValue(overrideDeoccluderProp, _ => RefreshDeoccluderVisibility());

                SerializedProperty avoidDemoModeProp = poiObject.FindProperty("_avoidDemoMode");
                AddProperty(trackerContainer, avoidDemoModeProp, poiObject);

                var demoContainer = new VisualElement();
                trackerContainer.Add(demoContainer);
                AddSlider(demoContainer, poiObject.FindProperty("_demoIdleSecondsOverride"), 0f, 120f, "Demo Idle Seconds", poiObject);

                void RefreshDemoVisibility()
                {
                    poiObject.Update();
                    demoContainer.style.display = avoidDemoModeProp != null && avoidDemoModeProp.boolValue
                        ? DisplayStyle.None
                        : DisplayStyle.Flex;
                }

                RefreshDemoVisibility();
                if (avoidDemoModeProp != null)
                    trackerContainer.TrackPropertyValue(avoidDemoModeProp, _ => RefreshDemoVisibility());
            }

            RebuildTrackerProperties();
            container.TrackPropertyValue(trackerPointProp, _ => RebuildTrackerProperties());
        }


        private void ApplyHeroIcon(VisualElement root)
        {
            if (root == null) return;
            var icon = root.Q<VisualElement>("heroIcon");
            if (icon == null) return;

            Sprite sprite = LoadSprite(IconsPath, FloorIconName);
            if (sprite == null) return;

            icon.style.backgroundImage = new StyleBackground(sprite);
            icon.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            icon.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
            icon.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            icon.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
        }

        private void ApplyTitleFont(VisualElement root)
        {
            if (root == null) return;
            var title = root.Q<Label>(className: "hero-title");
            if (title == null) return;

            var font = AssetDatabase.LoadAssetAtPath<Font>(TitleFontPath);
            if (font == null) return;

            title.style.unityFontDefinition = FontDefinition.FromFont(font);
        }

        private Sprite LoadSprite(string path, string spriteName)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            if (assets == null || assets.Length == 0) return null;

            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite && sprite.name == spriteName)
                    return sprite;
            }

            return null;
        }
    }
}
