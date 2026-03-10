using Twinny.Mobile.Cameras;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Twinny.Mobile.Editor.Camera
{
    [CustomEditor(typeof(MobileCinemachineFpsHandler))]
    public class MobileCinemachineFpsHandlerEditor : UnityEditor.Editor
    {
        private const string UxmlPath = "Packages/com.twinny.twe26/Editor/Shared/MobileCinemachineSharedEditor.uxml";
        private const string UssPath = "Packages/com.twinny.twe26/Editor/Shared/MobileCinemachineSharedEditor.uss";
        private const string IconsPath = "Packages/com.twinny.mobile/Editor/Cameras/Icons/icons.png";
        private const string FpsIconName = "icons_1";
        private const string TitleFontPath = "Packages/com.twinny.twe26/Editor/SetupGuide/Resources/Fonts/DINNextLTPro-Condensed.otf";
        private const float SliderStep = 0.1f;
        private const float GyroSliderStep = 0.001f;

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
            AddActionButtons(root.Q<VisualElement>("actionsFields"));

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
            if (title != null) title.text = "FPS Camera Control";

            var subtitle = root.Q<Label>(className: "hero-subtitle");
            if (subtitle != null) subtitle.text = "Mobile gestures + Cinemachine 3";
        }

        private void AddHandlerFields(VisualElement container)
        {
            if (container == null) return;

            AddSlider(container, "_rotateSpeed", 0f, 2f, "Rotate Speed");
            AddSlider(container, "_tiltSpeed", 0f, 2f, "Tilt Speed");
            AddSlider(container, "_zoomFov", 10f, 120f, "Zoom FOV");
            AddSlider(container, "_zoomSpeed", 1f, 240f, "Zoom Speed");
            AddSlider(container, "_zoomReleaseDelay", 0f, 1f, "Zoom Release Delay");

            var useGyroProp = serializedObject.FindProperty("_useGyroscope");
            var useGyroField = AddPropertyField(container, useGyroProp, serializedObject);
            var gyroYaw = AddSliderField(container, "_gyroYawSpeed", 0f, 0.2f, "Gyro Yaw Speed", GyroSliderStep, false);
            var gyroPitch = AddSliderField(container, "_gyroPitchSpeed", 0f, 0.2f, "Gyro Pitch Speed", GyroSliderStep, false);
            var gyroDeadZone = AddSliderField(container, "_gyroDeadZone", 0f, 5f, "Gyro Dead Zone");
            UpdateGyroVisibility(useGyroProp, gyroYaw, gyroPitch, gyroDeadZone);
            if (useGyroField != null)
                useGyroField.RegisterValueChangeCallback(_ =>
                    UpdateGyroVisibility(useGyroProp, gyroYaw, gyroPitch, gyroDeadZone));

            AddMinMaxSlider(container, "_verticalAxisLimits", -90f, 90f, "Tilt Limits");
        }

        private void AddCinemachineFields(VisualElement container)
        {
            if (container == null) return;

            var handler = (MobileCinemachineFpsHandler)target;
            var cam = GetCinemachineCamera(handler);
            var panTilt = GetPanTilt(handler);

            if (cam == null && panTilt == null)
            {
                container.Add(new HelpBox("Assign a Cinemachine Camera or Pan Tilt component.", HelpBoxMessageType.Info));
                return;
            }

            if (cam != null) AddCameraFields(container, cam);
            if (panTilt != null) AddPanTiltFields(container, panTilt);
        }

        private void AddCameraFields(VisualElement container, CinemachineCamera cam)
        {
            var cameraSo = new SerializedObject(cam);
            var targetProp = cameraSo.FindProperty("Target");
            var lensProp = cameraSo.FindProperty("Lens");

            container.Add(new Label("Cinemachine Camera"));
            AddProperty(container, targetProp, "TrackingTarget", cameraSo);
            AddProperty(container, targetProp, "LookAtTarget", cameraSo);
            AddSlider(container, lensProp, "FieldOfView", 10f, 120f, "Field Of View");
            AddSlider(container, lensProp, "NearClipPlane", 0.01f, 5f, "Near Clip");
            AddSlider(container, lensProp, "FarClipPlane", 10f, 50000f, "Far Clip");
        }

        private void AddPanTiltFields(VisualElement container, CinemachinePanTilt panTilt)
        {
            var panSo = new SerializedObject(panTilt);
            container.Add(new Label("Pan Tilt"));

            var panRange = panSo.FindProperty("PanAxis")?.FindPropertyRelative("Range");
            var tiltRange = panSo.FindProperty("TiltAxis")?.FindPropertyRelative("Range");
            AddMinMaxSlider(container, panRange, -180f, 180f, "Pan Range", panSo);
            AddMinMaxSlider(container, tiltRange, -90f, 90f, "Tilt Range", panSo);
        }

        private void AddActionButtons(VisualElement container)
        {
            if (container == null) return;

            var row = new VisualElement();
            row.AddToClassList("button-row");

            var applyButton = new Button(SyncTiltLimitsToPanTilt)
            {
                text = "Sync Tilt Limits"
            };
            row.Add(applyButton);

            container.Add(row);
            container.Add(new Label("Pan/tilt limits are enforced by the handler in runtime.") { name = "note" });
            container.Q<Label>("note")?.AddToClassList("inline-note");
        }

        private void SyncTiltLimitsToPanTilt()
        {
            var handler = (MobileCinemachineFpsHandler)target;
            var panTilt = GetPanTilt(handler);
            if (panTilt == null) return;

            var limitsProp = serializedObject.FindProperty("_verticalAxisLimits");
            if (limitsProp == null) return;

            Undo.RecordObject(panTilt, "Sync Tilt Limits");
            Vector2 tilt = limitsProp.vector2Value;
            panTilt.TiltAxis.Range = tilt;
            EditorUtility.SetDirty(panTilt);
        }

        private CinemachineCamera GetCinemachineCamera(MobileCinemachineFpsHandler handler)
        {
            return handler != null ? handler.GetComponent<CinemachineCamera>() : null;
        }

        private CinemachinePanTilt GetPanTilt(MobileCinemachineFpsHandler handler)
        {
            if (handler == null) return null;
            var panTilt = handler.GetComponent<CinemachinePanTilt>();
            if (panTilt != null) return panTilt;
            return handler.GetComponent<CinemachineCamera>()?.GetComponent<CinemachinePanTilt>();
        }

        private void AddProperty(VisualElement container, SerializedProperty root, SerializedObject owner)
        {
            if (root == null) return;
            var field = new PropertyField(root);
            field.Bind(owner);
            container.Add(field);
        }

        private PropertyField AddPropertyField(
            VisualElement container,
            SerializedProperty root,
            SerializedObject owner
        )
        {
            if (root == null) return null;
            var field = new PropertyField(root);
            field.Bind(owner);
            container.Add(field);
            return field;
        }

        private void AddProperty(VisualElement container, SerializedProperty root, string relativeName, SerializedObject owner)
        {
            if (root == null) return;
            SerializedProperty prop = root.FindPropertyRelative(relativeName);
            if (prop == null) return;
            var field = new PropertyField(prop);
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
            AddSlider(container, prop, min, max, label, serializedObject, SliderStep, true);
        }

        private VisualElement AddSliderField(
            VisualElement container,
            string propertyName,
            float min,
            float max,
            string label
        )
        {
            SerializedProperty prop = serializedObject.FindProperty(propertyName);
            if (prop == null) return null;
            return AddSlider(container, prop, min, max, label, serializedObject, SliderStep, true);
        }

        private VisualElement AddSliderField(
            VisualElement container,
            string propertyName,
            float min,
            float max,
            string label,
            float step,
            bool snap
        )
        {
            SerializedProperty prop = serializedObject.FindProperty(propertyName);
            if (prop == null) return null;
            return AddSlider(container, prop, min, max, label, serializedObject, step, snap);
        }

        private void AddSlider(
            VisualElement container,
            SerializedProperty root,
            string relativeName,
            float min,
            float max,
            string label
        )
        {
            if (root == null) return;
            SerializedProperty prop = root.FindPropertyRelative(relativeName);
            if (prop == null) return;
            AddSlider(container, prop, min, max, label, root.serializedObject, SliderStep, true);
        }

        private VisualElement AddSlider(
            VisualElement container,
            SerializedProperty prop,
            float min,
            float max,
            string label,
            SerializedObject owner,
            float step,
            bool snap
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
                float value = snap ? Snap(evt.newValue, step) : evt.newValue;
                if (snap && !Mathf.Approximately(evt.newValue, value))
                    slider.SetValueWithoutNotify(value);
                prop.floatValue = value;
                owner.ApplyModifiedProperties();
                field.SetValueWithoutNotify(value);
            });

            field.RegisterValueChangedCallback(evt =>
            {
                float value = snap ? Snap(evt.newValue, step) : evt.newValue;
                if (snap && !Mathf.Approximately(evt.newValue, value))
                    field.SetValueWithoutNotify(value);
                prop.floatValue = value;
                owner.ApplyModifiedProperties();
                slider.SetValueWithoutNotify(value);
            });

            var fieldRow = new VisualElement();
            fieldRow.AddToClassList("inline-values");
            fieldRow.Add(slider);
            fieldRow.Add(field);

            row.Add(labelEl);
            row.Add(fieldRow);
            container.Add(row);
            return row;
        }

        private void AddMinMaxSlider(
            VisualElement container,
            string propertyName,
            float min,
            float max,
            string label
        )
        {
            SerializedProperty prop = serializedObject.FindProperty(propertyName);
            if (prop == null) return;
            AddMinMaxSlider(container, prop, min, max, label, serializedObject);
        }

        private void AddMinMaxSlider(
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

            var slider = new MinMaxSlider();
            slider.lowLimit = min;
            slider.highLimit = max;
            slider.AddToClassList("row-field");

            var minField = new FloatField();
            minField.AddToClassList("mini-field");
            var maxField = new FloatField();
            maxField.AddToClassList("mini-field");

            slider.RegisterValueChangedCallback(evt =>
            {
                Vector2 snapped = Snap(evt.newValue, SliderStep);
                prop.vector2Value = snapped;
                owner.ApplyModifiedProperties();
                minField.SetValueWithoutNotify(snapped.x);
                maxField.SetValueWithoutNotify(snapped.y);
            });

            minField.RegisterValueChangedCallback(evt =>
            {
                var val = prop.vector2Value;
                val.x = Mathf.Clamp(Snap(evt.newValue, SliderStep), min, max);
                if (val.y < val.x) val.y = val.x;
                prop.vector2Value = val;
                owner.ApplyModifiedProperties();
                slider.SetValueWithoutNotify(val);
            });

            maxField.RegisterValueChangedCallback(evt =>
            {
                var val = prop.vector2Value;
                val.y = Mathf.Clamp(Snap(evt.newValue, SliderStep), min, max);
                if (val.x > val.y) val.x = val.y;
                prop.vector2Value = val;
                owner.ApplyModifiedProperties();
                slider.SetValueWithoutNotify(val);
            });

            var inline = new VisualElement();
            inline.AddToClassList("inline-values");
            inline.Add(slider);
            inline.Add(minField);
            inline.Add(maxField);

            row.Add(labelEl);
            row.Add(inline);
            container.Add(row);

            var initial = prop.vector2Value;
            slider.SetValueWithoutNotify(initial);
            minField.SetValueWithoutNotify(initial.x);
            maxField.SetValueWithoutNotify(initial.y);
        }

        private static float Snap(float value, float step)
        {
            if (step <= 0f) return value;
            return Mathf.Round(value / step) * step;
        }

        private static Vector2 Snap(Vector2 value, float step)
        {
            return new Vector2(Snap(value.x, step), Snap(value.y, step));
        }

        private void UpdateGyroVisibility(
            SerializedProperty useGyroProp,
            VisualElement yaw,
            VisualElement pitch,
            VisualElement deadZone
        )
        {
            bool show = useGyroProp != null && useGyroProp.boolValue;
            if (yaw != null) yaw.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (pitch != null) pitch.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (deadZone != null) deadZone.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void ApplyHeroIcon(VisualElement root)
        {
            if (root == null) return;
            var icon = root.Q<VisualElement>("heroIcon");
            if (icon == null) return;

            Sprite sprite = LoadSprite(IconsPath, FpsIconName);
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
