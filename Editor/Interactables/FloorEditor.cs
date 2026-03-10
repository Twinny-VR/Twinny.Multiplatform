using Twinny.Mobile.Interactables;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Twinny.Mobile.Editor.Interactables
{
    [CustomEditor(typeof(Floor))]
    [CanEditMultipleObjects]
    public class FloorEditor : UnityEditor.Editor
    {
        private const string UxmlPath = "Packages/com.twinny.twe26/Editor/Shared/MobileCinemachineSharedEditor.uxml";
        private const string UssPath = "Packages/com.twinny.twe26/Editor/Shared/MobileCinemachineSharedEditor.uss";
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

            AddIdentityFields(root.Q<VisualElement>("handlerFields"));
            AddCameraTargetFields(root.Q<VisualElement>("cutoffFields"));
            AddEventFields(root.Q<VisualElement>("actionsFields"));
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
            if (title != null) title.text = "Floor";
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
            SerializedProperty focusPointProp = serializedObject.FindProperty("_focusPoint");

            AddSlider(container, "_maxWallHeight", 0f, 20f, "Max Wall Height");
            AddProperty(container, useFocusPointProp, serializedObject);
            AddProperty(container, focusPointProp, serializedObject);
            AddSlider(container, "_targetRadius", 0.1f, 200f, "Target Radius");
            AddProperty(container, serializedObject.FindProperty("_targetPositionOffset"), serializedObject);
            AddProperty(container, serializedObject.FindProperty("_targetRotationOffset"), serializedObject);
        }

        private void AddEventFields(VisualElement container)
        {
            if (container == null) return;
            AddProperty(container, serializedObject.FindProperty("_onSelect"), serializedObject);
            AddProperty(container, serializedObject.FindProperty("_onUnselect"), serializedObject);
        }

        private void AddComputedInfo(VisualElement container)
        {
            if (container == null) return;
            if (targets != null && targets.Length != 1) return;
            var floor = target as Floor;
            if (floor == null) return;

            container.Add(new Label("Runtime Preview"));
            AddHelpLabel(container, $"Target Position: {floor.TargetPosition}");
            AddHelpLabel(container, $"Target Rotation (Euler): {floor.TargetRotation.eulerAngles}");
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
