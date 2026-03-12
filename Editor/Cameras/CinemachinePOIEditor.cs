using Twinny.Mobile.Cameras;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Twinny.Mobile.Editor.Cameras
{
    [CustomEditor(typeof(CinemachinePOI))]
    public class CinemachinePOIEditor : UnityEditor.Editor
    {
        private const string UxmlPath = "Packages/com.twinny.twe26/Editor/Shared/MobileCinemachineSharedEditor.uxml";
        private const string UssPath = "Packages/com.twinny.twe26/Editor/Shared/MobileCinemachineSharedEditor.uss";
        private const string IconsPath = "Packages/com.twinny.mobile/Editor/Cameras/Icons/icons.png";
        private const string PoiIconName = "icons_6";
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
            AddZeroValueWarning(root.Q<VisualElement>("handlerFields"));

            AddOrbitalFields(root.Q<VisualElement>("handlerFields"));
            AddPanConstraintFields(root.Q<VisualElement>("cutoffFields"));
            AddDemoFields(root.Q<VisualElement>("actionsFields"));
            AddComputedInfo(root.Q<VisualElement>("cinemachineFields"));

            return root;
        }

        private void LoadAssets()
        {
            if (_visualTree == null)
                _visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);

            if (_styleSheet == null)
                _styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
        }

        private void ApplyTitle(VisualElement root)
        {
            if (root == null) return;
            var title = root.Q<Label>(className: "hero-title");
            if (title != null) title.text = "Cinemachine POI";
            var subtitle = root.Q<Label>(className: "hero-subtitle");
            if (subtitle != null) subtitle.text = "Point of interest camera overrides";
        }

        private void AddZeroValueWarning(VisualElement container)
        {
            if (container == null) return;

            var helpBox = new HelpBox(
                "Only non-zero override values are considered at runtime. Zero-valued fields are treated as unset and will fall back to the handler defaults.",
                HelpBoxMessageType.Warning
            );
            container.Add(helpBox);
        }

        private void AddOrbitalFields(VisualElement container)
        {
            if (container == null) return;

            AddSlider(container, "_targetRadius", 0f, 200f, "Target Radius");
            AddProperty(container, serializedObject.FindProperty("_radiusLimits"), serializedObject);
            SerializedProperty overrideRotationProp = serializedObject.FindProperty("_overrideRotation");
            AddProperty(container, overrideRotationProp, serializedObject);

            var rotationContainer = new VisualElement();
            container.Add(rotationContainer);
            AddRadiansAsDegreesSlider(rotationContainer, "_targetPan", "Target Pan");
            AddRadiansAsDegreesSlider(rotationContainer, "_targetTilt", "Target Tilt");
            AddProperty(container, serializedObject.FindProperty("_verticalAxisLimits"), serializedObject);
            AddSlider(container, "_maxPanDistance", 0f, 100f, "Max Pan Distance");
            AddProperty(container, serializedObject.FindProperty("_enablePanLimit"), serializedObject);
            if (overrideRotationProp != null)
            {
                void RefreshRotationVisibility()
                {
                    serializedObject.Update();
                    rotationContainer.style.display = overrideRotationProp.boolValue
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                }

                RefreshRotationVisibility();
                container.TrackPropertyValue(overrideRotationProp, _ => RefreshRotationVisibility());
            }

            SerializedProperty overrideDeoccluderProp = serializedObject.FindProperty("_overrideDeoccluder");
            AddProperty(container, overrideDeoccluderProp, serializedObject);

            var deoccluderContainer = new VisualElement();
            container.Add(deoccluderContainer);
            AddSlider(deoccluderContainer, "_overrideDeoccluderRadius", 0f, 500f, "Deoccluder Radius");

            if (overrideDeoccluderProp != null)
            {
                void RefreshVisibility()
                {
                    serializedObject.Update();
                    deoccluderContainer.style.display = overrideDeoccluderProp.boolValue
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                }

                RefreshVisibility();
                container.TrackPropertyValue(overrideDeoccluderProp, _ => RefreshVisibility());
            }
        }

        private void AddPanConstraintFields(VisualElement container)
        {
            if (container == null) return;

            SerializedProperty overrideProp = serializedObject.FindProperty("_overridePanConstraint");
            SerializedProperty lockX = serializedObject.FindProperty("_lockPanX");
            SerializedProperty lockY = serializedObject.FindProperty("_lockPanY");
            SerializedProperty lockZ = serializedObject.FindProperty("_lockPanZ");
            AddProperty(container, overrideProp, serializedObject);

            var locksContainer = new VisualElement();
            container.Add(locksContainer);

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
                locksContainer.Add(row);
            }

            if (overrideProp != null)
            {
                void RefreshVisibility()
                {
                    serializedObject.Update();
                    locksContainer.style.display = overrideProp.boolValue
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                }

                RefreshVisibility();
                container.TrackPropertyValue(overrideProp, _ => RefreshVisibility());
            }
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

        private void AddDemoFields(VisualElement container)
        {
            if (container == null) return;

            SerializedProperty avoidProp = serializedObject.FindProperty("_avoidDemoMode");
            AddProperty(container, avoidProp, serializedObject);

            var idleContainer = new VisualElement();
            container.Add(idleContainer);
            AddSlider(idleContainer, serializedObject.FindProperty("_demoIdleSecondsOverride"), 0f, 120f, "Demo Idle Seconds");

            if (avoidProp != null)
            {
                void RefreshVisibility()
                {
                    serializedObject.Update();
                    idleContainer.style.display = avoidProp.boolValue
                        ? DisplayStyle.None
                        : DisplayStyle.Flex;
                }

                RefreshVisibility();
                container.TrackPropertyValue(avoidProp, _ => RefreshVisibility());
            }
        }

        private void AddComputedInfo(VisualElement container)
        {
            if (container == null) return;
            if (targets != null && targets.Length != 1) return;
            var poi = target as CinemachinePOI;
            if (poi == null) return;

            container.Add(new Label("Runtime Preview"));
            AddHelpLabel(container, $"Target Radius: {poi.TargetRadius}");
            AddHelpLabel(container, $"Target Pan (rad): {poi.TargetPan}");
            AddHelpLabel(container, $"Target Tilt (rad): {poi.TargetTilt}");
            AddHelpLabel(container, $"Vertical Limits: {poi.VerticalAxisLimits}");
            AddHelpLabel(container, $"Radius Limits: {poi.RadiusLimits}");
        }

        private void AddProperty(VisualElement container, SerializedProperty property, SerializedObject owner)
        {
            if (container == null || property == null) return;
            var field = new PropertyField(property);
            field.Bind(owner);
            container.Add(field);
        }

        private void AddSlider(VisualElement container, string propertyName, float min, float max, string label)
        {
            SerializedProperty prop = serializedObject.FindProperty(propertyName);
            if (prop == null) return;
            AddSlider(container, prop, min, max, label);
        }

        private void AddRadiansAsDegreesSlider(VisualElement container, string propertyName, string label)
        {
            SerializedProperty prop = serializedObject.FindProperty(propertyName);
            if (container == null || prop == null) return;

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
                serializedObject.ApplyModifiedProperties();
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

        private void AddSlider(VisualElement container, SerializedProperty prop, float min, float max, string label)
        {
            if (container == null || prop == null) return;

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
                serializedObject.ApplyModifiedProperties();
                field.SetValueWithoutNotify(snapped);
            });

            field.RegisterValueChangedCallback(evt =>
            {
                float snapped = Snap(evt.newValue, SliderStep);
                if (!Mathf.Approximately(evt.newValue, snapped))
                    field.SetValueWithoutNotify(snapped);
                prop.floatValue = snapped;
                serializedObject.ApplyModifiedProperties();
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

        private void ApplyHeroIcon(VisualElement root)
        {
            if (root == null) return;
            var icon = root.Q<VisualElement>("heroIcon");
            if (icon == null) return;

            Sprite sprite = LoadSprite(IconsPath, PoiIconName);
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
