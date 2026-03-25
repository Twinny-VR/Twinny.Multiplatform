#if UNITY_EDITOR
using Twinny.Core.Editor;
using UnityEditor;
using UnityEngine.UIElements;

namespace Twinny.Multiplatform.Editor
{
    [CustomEditor(typeof(PlatformRuntime))]
    [CanEditMultipleObjects]
    public class PlatformRuntimeEditor : TwinnyRuntimeEditor
    {
        protected override string InspectorTitle => "Platform Runtime";

        protected override string InspectorSubtitle => "Mobile preset fallback and scene defaults";

        protected override void AddNotesSection(VisualElement container)
        {
            base.AddNotesSection(container);
            AddHelpLabel(container, "When no platform preset is found, the runtime falls back to Twinny Runtime and then to MobileMockupScene.");
        }
    }
}
#endif
