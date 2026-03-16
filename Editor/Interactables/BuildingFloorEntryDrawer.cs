using Twinny.Mobile.Interactables;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Twinny.Mobile.Editor.Interactables
{
    [CustomPropertyDrawer(typeof(BuildingFloorEntry))]
    public class BuildingFloorEntryDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();
            root.AddToClassList("fields");

            SerializedProperty rootProp = property.FindPropertyRelative("_root");
            var rootField = new PropertyField(rootProp, "Root");
            rootField.BindProperty(rootProp);
            root.Add(rootField);

            var infoLabel = new Label();
            infoLabel.AddToClassList("inline-note");
            root.Add(infoLabel);

            var warningBox = new HelpBox(
                "The assigned object has no Floor component. This entry will behave as a static floor.",
                HelpBoxMessageType.Info
            );
            root.Add(warningBox);

            void Refresh()
            {
                Object value = rootProp.objectReferenceValue;
                GameObject gameObject = value as GameObject;

                if (gameObject == null)
                {
                    infoLabel.text = "Assign a root object for this floor entry.";
                    warningBox.style.display = DisplayStyle.None;
                    return;
                }

                Floor floor = gameObject.GetComponent<Floor>();
                if (floor != null)
                {
                    infoLabel.text = $"Detected interactive floor: {floor.GetType().Name}";
                    warningBox.style.display = DisplayStyle.None;
                    return;
                }

                infoLabel.text = "Detected static floor entry.";
                warningBox.style.display = DisplayStyle.Flex;
            }

            Refresh();
            root.TrackPropertyValue(rootProp, _ => Refresh());

            return root;
        }
    }
}
