using UnityEngine;
using UnityEngine.UIElements;

namespace Twinny.Mobile.Samples
{
    [UxmlElement]
    public partial class WSLabel : VisualElement
    {
        private const string WidgetResourceName = "WS_Label";
        private const string WidgetStyleResourceName = "WS_Label";
        private const string RootName = "WSLabelRoot";
        private const string TitleName = "WSLabelTitle";
        private const string SubtitleName = "WSLabelSubtitle";

        private VisualElement _rootContainer;
        private Label _title;
        private Label _subtitle;

        public string Title
        {
            get
            {
                EnsureReferences();
                return _title.text;
            }
            set => SetInfo(value, Subtitle);
        }

        public string Subtitle
        {
            get
            {
                EnsureReferences();
                return _subtitle.text;
            }
            set => SetInfo(Title, value);
        }

        public WSLabel()
        {
            InitializeFromResources();
        }

        public void SetInfo(string title, string subtitle)
        {
            EnsureReferences();
            _title.text = string.IsNullOrWhiteSpace(title) ? "Point of Interest" : title;
            _subtitle.text = string.IsNullOrWhiteSpace(subtitle) ? string.Empty : subtitle;
            _subtitle.style.display = string.IsNullOrWhiteSpace(subtitle) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void InitializeFromResources()
        {
            VisualTreeAsset visualTree = Resources.Load<VisualTreeAsset>(WidgetResourceName);
            if (visualTree != null)
            {
                Clear();
                visualTree.CloneTree(this);

                _rootContainer = this.Q<VisualElement>(RootName);
                _title = this.Q<Label>(TitleName);
                _subtitle = this.Q<Label>(SubtitleName);

                if (_rootContainer == null || _title == null || _subtitle == null)
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
            SetInfo("Point of Interest", "Tap to inspect");
        }

        private void BuildFallbackHierarchy()
        {
            Clear();

            _rootContainer = new VisualElement { name = RootName };
            _rootContainer.AddToClassList("ws-label-root");

            var accent = new VisualElement { name = "WSLabelAccent" };
            accent.AddToClassList("ws-label-accent");

            var textRoot = new VisualElement { name = "WSLabelText" };
            textRoot.AddToClassList("ws-label-text");

            _title = new Label("Point of Interest") { name = TitleName };
            _title.AddToClassList("ws-label-title");

            _subtitle = new Label("Tap to inspect") { name = SubtitleName };
            _subtitle.AddToClassList("ws-label-subtitle");

            textRoot.Add(_title);
            textRoot.Add(_subtitle);
            _rootContainer.Add(accent);
            _rootContainer.Add(textRoot);
            Add(_rootContainer);
        }

        private void EnsureReferences()
        {
            _rootContainer ??= this.Q<VisualElement>(RootName);
            _title ??= this.Q<Label>(TitleName);
            _subtitle ??= this.Q<Label>(SubtitleName);

            if (_rootContainer == null || _title == null || _subtitle == null)
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
