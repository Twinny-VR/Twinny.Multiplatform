using Twinny.Mobile.Cameras;
using Twinny.Mobile.Interactables;
using UnityEngine;
using UnityEngine.UIElements;

namespace Twinny.Mobile.Samples
{
    [UxmlElement]
    public partial class FloorHintWidget : VisualElement
    {
        private const string WidgetResourceName = "FloorHintWidget";
        private const string WidgetStyleResourceName = "FloorHintWidgetStyles";
        private const string RootName = "FloorHintRoot";
        private const string TitleName = "FloorHintTitle";
        private const string SubtitleName = "FloorHintSubtitle";
        private const string ImmersionIconName = "FloorHintImmersionIcon";
        private const string ImmersiveIconClass = "floor-hint-icon--immersive";
        private const string MockupIconClass = "floor-hint-icon--mockup";

        private VisualElement _rootContainer;
        private Label _title;
        private Label _subtitle;
        private VisualElement _immersionIcon;

        public FloorHintWidget()
        {
            InitializeFromResources();
        }

        private void InitializeFromResources()
        {
            VisualTreeAsset visualTree = Resources.Load<VisualTreeAsset>(WidgetResourceName);
            if (visualTree != null)
            {
                Clear();
                visualTree.CloneTree(this);

                _title = this.Q<Label>(TitleName);
                _subtitle = this.Q<Label>(SubtitleName);
                _immersionIcon = this.Q<VisualElement>(ImmersionIconName);
                _rootContainer = this.Q<VisualElement>(RootName);

                if (_title == null || _subtitle == null || _immersionIcon == null || _rootContainer == null)
                    BuildFallbackHierarchy();
            }
            else
            {
                Debug.LogError($"{WidgetResourceName} Resource not found.");
                BuildFallbackHierarchy();
            }

            StyleSheet styleSheet = Resources.Load<StyleSheet>(WidgetStyleResourceName);
            if (styleSheet == null)
            {
                Debug.LogError($"{WidgetStyleResourceName} Resource not found.");
                return;
            }

            AddStyleIfMissing(styleSheet);
        }

        public void SetFloor(Floor floor)
        {
            if (floor == null)
            {
                SetInfo("Pavement", "#0 Floor", false, false);
                return;
            }

            bool useMockupIcon = floor.Data.SceneOpenMode == FloorData.FloorSceneOpenMode.Mockup;
            SetInfo(floor.Data.Title, floor.Data.Subtitle, floor.Data.HasImmersionScene, useMockupIcon);
        }

        public void SetInfo(string title, string subtitle, bool hasImmersion, bool useMockupIcon)
        {
            EnsureReferences();
            _title.text = title;
            _subtitle.text = subtitle;
            _rootContainer.EnableInClassList("has-immersion", hasImmersion);
            _rootContainer.EnableInClassList("no-immersion", !hasImmersion);
            _immersionIcon.EnableInClassList(ImmersiveIconClass, hasImmersion && !useMockupIcon);
            _immersionIcon.EnableInClassList(MockupIconClass, hasImmersion && useMockupIcon);
            _immersionIcon.style.display = hasImmersion ? DisplayStyle.Flex : DisplayStyle.None;
            pickingMode = hasImmersion ? PickingMode.Position : PickingMode.Ignore;
        }

        private void BuildFallbackHierarchy()
        {
            Clear();
            name = RootName;
            AddToClassList("floor-hint-root");
            _rootContainer = this;

            var textRoot = new VisualElement { name = "FloorHintText" };
            textRoot.AddToClassList("floor-hint-text");

            _title = new Label("Floor") { name = TitleName };
            _title.AddToClassList("floor-hint-title");

            _subtitle = new Label("#00 Floor") { name = SubtitleName };
            _subtitle.AddToClassList("floor-hint-subtitle");

            _immersionIcon = new VisualElement { name = ImmersionIconName };
            _immersionIcon.AddToClassList("floor-hint-icon");
            _immersionIcon.AddToClassList(ImmersiveIconClass);

            textRoot.Add(_title);
            textRoot.Add(_subtitle);
            Add(textRoot);
            Add(_immersionIcon);
        }

        private void EnsureReferences()
        {
            _title ??= this.Q<Label>(TitleName);
            _subtitle ??= this.Q<Label>(SubtitleName);
            _immersionIcon ??= this.Q<VisualElement>(ImmersionIconName);
            _rootContainer ??= this.Q<VisualElement>(RootName);

            if (_title == null || _subtitle == null || _immersionIcon == null || _rootContainer == null)
                BuildFallbackHierarchy();
        }

        private void AddStyleIfMissing(StyleSheet styleSheet)
        {
            if (styleSheet == null) return;
            if (styleSheets.Contains(styleSheet)) return;
            styleSheets.Add(styleSheet);
        }
    }
}
