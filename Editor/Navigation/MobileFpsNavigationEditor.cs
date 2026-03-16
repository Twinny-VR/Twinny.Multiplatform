using Twinny.Mobile.Navigation;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Twinny.Mobile.Editor.Navigation
{
    [CustomEditor(typeof(MobileFpsNavigation))]
    public class MobileFpsNavigationEditor : UnityEditor.Editor
    {
        private const string UxmlPath = "Packages/com.twinny.twe26/Editor/Shared/ComponentInspectorTemplate.uxml";
        private const string UssPath = "Packages/com.twinny.twe26/Editor/Shared/ComponentInspectorTemplate.uss";
        private const string IconsPath = "Packages/com.twinny.mobile/Editor/Cameras/Icons/icons.png";
        private const string IconName = "icons_2";
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
            AddNavigationFields(CreateSection(contentRoot, "Navigation"));
            AddRuntimeFields(CreateSection(contentRoot, "Runtime"));
            AddNotes(CreateSection(contentRoot, "Notes"));

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
            if (title != null) title.text = "FPS Navigation";

            var subtitle = root.Q<Label>(className: "hero-subtitle");
            if (subtitle != null) subtitle.text = "NavMesh click-to-move";
        }

        private void AddNavigationFields(VisualElement container)
        {
            if (container == null) return;

            AddProperty(container, serializedObject.FindProperty("_agent"), serializedObject);
            AddSlider(container, "_maxSampleDistance", 0f, 20f, "Max Sample Distance");
            AddProperty(container, serializedObject.FindProperty("_navMeshAreaMask"), serializedObject);
            AddProperty(container, serializedObject.FindProperty("_raycastCamera"), serializedObject);
            AddProperty(container, serializedObject.FindProperty("_interactableMask"), serializedObject);
            AddProperty(container, serializedObject.FindProperty("_targetDecalPrefab"), serializedObject);
        }

        private void AddRuntimeFields(VisualElement container)
        {
            if (container == null) return;
            container.Add(new Label("Runtime"));
            AddHelpLabel(container, "Uses OnSelect(SelectionData) from MobileInputProvider/Emulator.");
        }

        private void AddNotes(VisualElement container)
        {
            if (container == null) return;
            container.Add(new Label("Tap on colliders to move. Interactables are used when no NavMesh hit.") { name = "note" });
            container.Q<Label>("note")?.AddToClassList("inline-note");
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

        private void AddHelpLabel(VisualElement container, string text)
        {
            var label = new Label(text);
            label.AddToClassList("inline-note");
            container.Add(label);
        }

        private static float Snap(float value, float step)
        {
            if (step <= 0f) return value;
            return Mathf.Round(value / step) * step;
        }

        private void ApplyHeroIcon(VisualElement root)
        {
            if (root == null) return;
            var icon = root.Q<VisualElement>("heroIcon");
            if (icon == null) return;

            Sprite sprite = LoadSprite(IconsPath, IconName);
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
            if (assets == null || assets.Length == 0)
                return null;

            foreach (Object asset in assets)
            {
                if (asset is Sprite sprite && sprite.name == spriteName)
                    return sprite;
            }

            return null;
        }
    }
}
