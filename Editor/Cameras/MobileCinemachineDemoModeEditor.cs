using Twinny.Mobile.Cameras;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Twinny.Mobile.Editor.Camera
{
    [CustomEditor(typeof(MobileCinemachineDemoMode))]
    public class MobileCinemachineDemoModeEditor : UnityEditor.Editor
    {
        private const string UxmlPath = "Packages/com.twinny.twe26/Editor/Shared/MobileCinemachineSharedEditor.uxml";
        private const string UssPath = "Packages/com.twinny.twe26/Editor/Shared/MobileCinemachineSharedEditor.uss";
        private const string IconsPath = "Packages/com.twinny.mobile/Editor/Cameras/Icons/icons.png";
        private const string DemoIconName = "icons_4";
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

            AddHandlerFields(root.Q<VisualElement>("handlerFields"));
            AddCinemachineFields(root.Q<VisualElement>("cinemachineFields"));
            AddNotes(root.Q<VisualElement>("actionsFields"));

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
            if (title != null) title.text = "Demo Mode";
            var subtitle = root.Q<Label>(className: "hero-subtitle");
            if (subtitle != null) subtitle.text = "Idle auto-orbit camera";
        }

        private void AddHandlerFields(VisualElement container)
        {
            if (container == null) return;

            AddSlider(container, "_idleSeconds", 0f, 120f, "Idle Seconds");
            AddSlider(container, "_demoRadius", 0.1f, 200f, "Demo Radius");
            AddSlider(container, "_radiusTransitionSpeed", 0f, 80f, "Radius Transition Speed");
            AddSlider(container, "_targetTransitionSpeed", 0f, 40f, "Target Transition Speed");
            AddSlider(container, "_demoYawSpeed", 0f, 45f, "Demo Yaw Speed");
            AddProperty(container, serializedObject.FindProperty("_logState"), serializedObject);
        }

        private void AddCinemachineFields(VisualElement container)
        {
            if (container == null) return;
            container.Add(new Label("References"));
            AddProperty(container, serializedObject.FindProperty("_cinemachineCamera"), serializedObject);
            AddProperty(container, serializedObject.FindProperty("_orbitalFollow"), serializedObject);
            AddProperty(container, serializedObject.FindProperty("_demoTargetPoint"), serializedObject);
            AddProperty(container, serializedObject.FindProperty("_demoLookAtPoint"), serializedObject);
            AddProperty(container, serializedObject.FindProperty("_forceLookAtDuringDemo"), serializedObject);
        }

        private void AddNotes(VisualElement container)
        {
            if (container == null) return;

            container.Add(new Label("Behavior"));
            AddHelpLabel(container, "Starts after idle timeout and stops at first user interaction.");
            AddHelpLabel(container, "Applies only when this Cinemachine camera is active.");
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

            Sprite sprite = LoadSprite(IconsPath, DemoIconName);
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
