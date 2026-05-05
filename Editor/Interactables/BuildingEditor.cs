using Twinny.Multiplatform.Interactables;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Twinny.Multiplatform.Editor.Interactables
{
    [CustomEditor(typeof(Building))]
    [CanEditMultipleObjects]
    public class BuildingEditor : UnityEditor.Editor
    {
        private const string UxmlPath = "Packages/com.twinny.twe26/Editor/Shared/ComponentInspectorTemplate.uxml";
        private const string UssPath = "Packages/com.twinny.twe26/Editor/Shared/ComponentInspectorTemplate.uss";
        private const string IconsPath = "Packages/com.twinny.multiplatform/Editor/Cameras/Icons/icons.png";
        private const string BuildingIconName = "icons_7";
        private const string TitleFontPath = "Packages/com.twinny.twe26/Editor/SetupGuide/Resources/Fonts/DINNextLTPro-Condensed.otf";

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
            AddStructureFields(CreateSection(contentRoot, "Structure"));
            AddRuntimeInfo(CreateSection(contentRoot, "Runtime Preview"));

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
            if (title != null) title.text = "Building";

            var subtitle = root.Q<Label>(className: "hero-subtitle");
            if (subtitle != null) subtitle.text = "Building Features and Runtime Info";
        }

        private void AddStructureFields(VisualElement container)
        {
            if (container == null) return;
            AddProperty(container, serializedObject.FindProperty("_floors"), serializedObject);
            AddProperty(container, serializedObject.FindProperty("_filterVisibilityBySelectedFloor"), serializedObject);
            AddProperty(container, serializedObject.FindProperty("_showAllFloorsWhenUnselected"), serializedObject);
        }

        private void AddRuntimeInfo(VisualElement container)
        {
            if (container == null) return;
            if (targets != null && targets.Length != 1) return;

            var building = target as Building;
            if (building == null) return;

            BuildingFloorEntry[] floors = building.Floors;
            int count = floors != null ? floors.Length : 0;
            int interactiveCount = 0;

            if (floors != null)
            {
                for (int i = 0; i < floors.Length; i++)
                {
                    if (floors[i] != null && floors[i].HasInteractiveFloor)
                        interactiveCount++;
                }
            }

            AddHelpLabel(container, $"Floors Assigned: {count}");
            AddHelpLabel(container, $"Interactive Floors: {interactiveCount}");
            AddHelpLabel(container, $"Static Floors: {Mathf.Max(0, count - interactiveCount)}");
            AddHelpLabel(container, $"Transform Children: {building.transform.childCount}");
        }

        private void AddProperty(VisualElement container, SerializedProperty property, SerializedObject owner)
        {
            if (container == null || property == null) return;
            var field = new PropertyField(property);
            field.Bind(owner);
            container.Add(field);
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

            Sprite sprite = LoadSprite(IconsPath, BuildingIconName);
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
