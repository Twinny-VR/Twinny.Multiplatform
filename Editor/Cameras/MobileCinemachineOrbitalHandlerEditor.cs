using System.Collections.Generic;
using Twinny.Mobile.Cameras;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Twinny.Mobile.Editor.Camera
{
    [InitializeOnLoad]
    [CustomEditor(typeof(MobileCinemachineOrbitalHandler))]
    public class MobileCinemachineOrbitalHandlerEditor : UnityEditor.Editor
    {
        private const string UxmlPath = "Packages/com.twinny.twe26/Editor/Shared/MobileCinemachineSharedEditor.uxml";
        private const string UssPath = "Packages/com.twinny.twe26/Editor/Shared/MobileCinemachineSharedEditor.uss";
        private const string IconsPath = "Packages/com.twinny.mobile/Editor/Cameras/Icons/icons.png";
        private const string OrbitalIconName = "icons_0";
        private const string TitleFontPath = "Packages/com.twinny.twe26/Editor/SetupGuide/Resources/Fonts/DINNextLTPro-Condensed.otf";
        private const string ApplyKeyPrefix = "Twinny.Mobile.RuntimeApply.";
        private const string ApplyKeysList = "Twinny.Mobile.RuntimeApply.Keys";
        private const float SliderStep = 0.1f;

        [System.Serializable]
        private class RuntimeApplyPayload
        {
            public string handlerId;
            public string camId;
            public string orbitId;
            public string trackingTargetId;
            public string lookAtTargetId;
            public string customPanTargetId;
            public float rotateSpeed;
            public float tiltSpeed;
            public int activePriority;
            public int inactivePriority;
            public float panSpeed;
            public float panReturnSpeed;
            public float zoomSpeed;
            public float hardLookRestoreDelay;
            public bool enablePanLimit;
            public float maxPanDistance;
            public bool returnTrackingTargetToOriginOnRelease;
            public int panTargetMode;
            public bool lockPanX;
            public bool lockPanY;
            public bool lockPanZ;
            public float maxWallHeight;
            public Vector2 verticalAxisLimits;
            public Vector2 radiusLimits;
            public float fov;
            public float nearClip;
            public float farClip;
            public float orbitRadius;
            public Vector2 orbitVerticalRange;
            public Vector2 orbitRadialRange;
        }

        static MobileCinemachineOrbitalHandlerEditor()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private VisualTreeAsset _visualTree;
        private StyleSheet _styleSheet;

        public override VisualElement CreateInspectorGUI()
        {
            LoadAssets();
            var root = _visualTree.CloneTree();
            root.styleSheets.Add(_styleSheet);

            AddHandlerFields(root.Q<VisualElement>("handlerFields"));
            AddCutoffFields(root.Q<VisualElement>("cutoffFields"));
            AddCinemachineFields(root.Q<VisualElement>("cinemachineFields"));
            AddActionButtons(root.Q<VisualElement>("actionsFields"));
            ApplyHeroIcon(root);
            ApplyTitleFont(root);
            ApplyRuntimeStyling(root);

            return root;
        }

        private void LoadAssets()
        {
            if (_visualTree == null)
                _visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);

            if (_styleSheet == null)
                _styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
        }

        private void AddHandlerFields(VisualElement container)
        {
            if (container == null) return;

            AddSlider(container, "_rotateSpeed", 0f, 2f, "Rotate Speed");
            AddSlider(container, "_tiltSpeed", 0f, 2f, "Tilt Speed");
            AddProperty(container, serializedObject.FindProperty("_activePriority"), serializedObject);
            AddProperty(container, serializedObject.FindProperty("_inactivePriority"), serializedObject);
            AddSlider(container, "_panSpeed", 0f, 10f, "Target Move Speed");
            AddSlider(container, "_panReturnSpeed", 0f, 20f, "Pan Return Speed");
            AddSlider(container, "_zoomSpeed", 0f, 10f, "Zoom Speed");
            AddSlider(container, "_radiusTransitionSpeed", 0f, 80f, "Radius Transition Speed");
            AddSlider(container, "_radiusEaseOutDistance", 0f, 10f, "Radius Ease-Out Dist");
            AddSlider(container, "_radiusEaseOutSmoothTime", 0.01f, 1f, "Radius Ease-Out Time");
            AddSlider(container, "_hardLookRestoreDelay", 0f, 0.5f, "HardLook Restore Delay");

            AddProperty(container, serializedObject.FindProperty("_returnTrackingTargetToOriginOnRelease"), serializedObject);
            AddProperty(container, serializedObject.FindProperty("_enablePanLimit"), serializedObject);
            AddSlider(container, "_maxPanDistance", 0f, 50f, "Max Pan Dist");
            AddProperty(container, serializedObject.FindProperty("_panTargetMode"), serializedObject);
            AddPanConstraintToggles(container);
            AddProperty(container, serializedObject.FindProperty("_customPanTarget"), serializedObject);

            AddMinMaxSlider(
                container,
                "_verticalAxisLimits",
                -120f,
                120f,
                "Tilt Limits"
            );

            AddMinMaxSlider(
                container,
                "_radiusLimits",
                0.1f,
                200f,
                "Zoom Limits"
            );
        }

        private IEnumerable<string> GetHandlerPropertyNames()
        {
            yield return "_cinemachineCamera";
            yield return "_orbitalFollow";
        }

        private void AddCutoffFields(VisualElement container)
        {
            if (container == null) return;
            AddProperty(container, serializedObject.FindProperty("_maxWallHeight"), serializedObject);
        }

        private void AddCinemachineFields(VisualElement container)
        {
            if (container == null) return;

            var handler = (MobileCinemachineOrbitalHandler)target;
            var cam = GetCinemachineCamera(handler);
            var orbit = GetOrbitalFollow(handler);

            if (cam == null && orbit == null)
            {
                container.Add(new HelpBox("Assign a Cinemachine Camera or Orbital Follow component.", HelpBoxMessageType.Info));
                return;
            }

            if (cam != null) AddCameraFields(container, cam);
            if (orbit != null) AddOrbitalFields(container, orbit);
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
            AddSlider(container, lensProp, "FarClipPlane", 10f, 5000f, "Far Clip");
        }

        private void AddOrbitalFields(VisualElement container, CinemachineOrbitalFollow orbit)
        {
            var orbitSo = new SerializedObject(orbit);
            container.Add(new Label("Orbital Follow"));

            AddSlider(container, orbitSo, "Radius", 0.1f, 200f, "Radius");
            var verticalRange = orbitSo.FindProperty("VerticalAxis")?.FindPropertyRelative("Range");
            var radialRange = orbitSo.FindProperty("RadialAxis")?.FindPropertyRelative("Range");
            AddMinMaxSlider(container, verticalRange, -120f, 120f, "Vertical Range", orbitSo);
            AddMinMaxSlider(container, radialRange, 0.1f, 4f, "Radial Range", orbitSo);
        }

        private void AddActionButtons(VisualElement container)
        {
            if (container == null) return;

            var row = new VisualElement();
            row.AddToClassList("button-row");

            if (EditorApplication.isPlaying)
            {
                var applyRuntimeButton = new Button(ApplyRuntimeValues)
                {
                    text = "Apply Runtime Values"
                };
                row.Add(applyRuntimeButton);
            }

            var applyButton = new Button(ApplyHandlerLimitsToOrbital)
            {
                text = "Sync Limits To Orbital"
            };
            row.Add(applyButton);

            container.Add(row);
            container.Add(new Label("Zoom/tilt limits are enforced by the handler in runtime.") { name = "note" });
            container.Q<Label>("note")?.AddToClassList("inline-note");
        }

        private void ApplyHandlerLimitsToOrbital()
        {
            var handler = (MobileCinemachineOrbitalHandler)target;
            var orbit = GetOrbitalFollow(handler);
            if (orbit == null) return;

            Undo.RecordObject(orbit, "Sync Orbital Limits");

            var limitsProp = serializedObject.FindProperty("_verticalAxisLimits");
            var radiusProp = serializedObject.FindProperty("_radiusLimits");
            if (limitsProp == null || radiusProp == null) return;

            Vector2 tilt = limitsProp.vector2Value;
            Vector2 radius = radiusProp.vector2Value;

            orbit.VerticalAxis.Range = tilt;
            orbit.Radius = Mathf.Clamp(orbit.Radius, radius.x, radius.y);

            EditorUtility.SetDirty(orbit);
        }

        private void ApplyRuntimeValues()
        {
            var handler = (MobileCinemachineOrbitalHandler)target;
            if (handler == null) return;

            SaveRuntimeSnapshot(handler);

            EditorApplication.isPlaying = false;
        }


        private CinemachineCamera GetCinemachineCamera(MobileCinemachineOrbitalHandler handler)
        {
            return handler != null ? handler.GetComponent<CinemachineCamera>() : null;
        }

        private CinemachineOrbitalFollow GetOrbitalFollow(MobileCinemachineOrbitalHandler handler)
        {
            if (handler == null) return null;
            var orbit = handler.GetComponent<CinemachineOrbitalFollow>();
            if (orbit != null) return orbit;
            return handler.GetComponent<CinemachineCamera>()?.GetComponent<CinemachineOrbitalFollow>();
        }

        private void AddProperty(VisualElement container, SerializedProperty root, SerializedObject owner)
        {
            if (root == null) return;
            var field = new PropertyField(root);
            field.Bind(owner);
            container.Add(field);
        }

        private void AddPanConstraintToggles(VisualElement container)
        {
            SerializedProperty lockX = serializedObject.FindProperty("_lockPanX");
            SerializedProperty lockY = serializedObject.FindProperty("_lockPanY");
            SerializedProperty lockZ = serializedObject.FindProperty("_lockPanZ");
            if (lockX == null || lockY == null || lockZ == null) return;

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
            container.Add(row);
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
            AddSlider(container, prop, min, max, label, serializedObject);
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
            AddSlider(container, prop, min, max, label, root.serializedObject);
        }

        private void AddSlider(
            VisualElement container,
            SerializedObject owner,
            string propertyName,
            float min,
            float max,
            string label
        )
        {
            SerializedProperty prop = owner.FindProperty(propertyName);
            if (prop == null) return;
            AddSlider(container, prop, min, max, label, owner);
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
            SerializedObject owner,
            string propertyName,
            float min,
            float max,
            string label
        )
        {
            SerializedProperty prop = owner.FindProperty(propertyName);
            if (prop == null) return;
            AddMinMaxSlider(container, prop, min, max, label, owner);
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

        private static void SaveRuntimeSnapshot(MobileCinemachineOrbitalHandler handler)
        {
            if (handler == null) return;

            var cam = handler.GetComponent<CinemachineCamera>();
            var orbit = handler.GetComponent<CinemachineOrbitalFollow>();

            var payload = new RuntimeApplyPayload
            {
                handlerId = GetId(handler),
                camId = GetId(cam),
                orbitId = GetId(orbit),
                trackingTargetId = GetId(cam != null ? cam.Follow : null),
                lookAtTargetId = GetId(cam != null ? cam.LookAt : null),
                customPanTargetId = GetId(GetCustomPanTarget(handler)),
                rotateSpeed = GetFloat(handler, "_rotateSpeed"),
                tiltSpeed = GetFloat(handler, "_tiltSpeed"),
                activePriority = GetInt(handler, "_activePriority"),
                inactivePriority = GetInt(handler, "_inactivePriority"),
                panSpeed = GetFloat(handler, "_panSpeed"),
                panReturnSpeed = GetFloat(handler, "_panReturnSpeed"),
                zoomSpeed = GetFloat(handler, "_zoomSpeed"),
                hardLookRestoreDelay = GetFloat(handler, "_hardLookRestoreDelay"),
                enablePanLimit = GetBool(handler, "_enablePanLimit"),
                maxPanDistance = GetFloat(handler, "_maxPanDistance"),
                returnTrackingTargetToOriginOnRelease = GetBool(handler, "_returnTrackingTargetToOriginOnRelease"),
                panTargetMode = GetInt(handler, "_panTargetMode"),
                lockPanX = GetBool(handler, "_lockPanX"),
                lockPanY = GetBool(handler, "_lockPanY"),
                lockPanZ = GetBool(handler, "_lockPanZ"),
                maxWallHeight = GetFloat(handler, "_maxWallHeight"),
                verticalAxisLimits = GetVector2(handler, "_verticalAxisLimits"),
                radiusLimits = GetVector2(handler, "_radiusLimits"),
                fov = cam != null ? cam.Lens.FieldOfView : 0f,
                nearClip = cam != null ? cam.Lens.NearClipPlane : 0f,
                farClip = cam != null ? cam.Lens.FarClipPlane : 0f,
                orbitRadius = orbit != null ? orbit.Radius : 0f,
                orbitVerticalRange = orbit != null ? orbit.VerticalAxis.Range : default,
                orbitRadialRange = orbit != null ? orbit.RadialAxis.Range : default
            };

            string key = ApplyKeyPrefix + payload.handlerId;
            SessionState.SetString(key, EditorJsonUtility.ToJson(payload));
            RegisterApplyKey(key);
        }

        private static Transform GetCustomPanTarget(MobileCinemachineOrbitalHandler handler)
        {
            if (handler == null) return null;
            var so = new SerializedObject(handler);
            var prop = so.FindProperty("_customPanTarget");
            return prop != null ? prop.objectReferenceValue as Transform : null;
        }

        private static void RegisterApplyKey(string key)
        {
            string existing = SessionState.GetString(ApplyKeysList, string.Empty);
            if (string.IsNullOrEmpty(existing))
            {
                SessionState.SetString(ApplyKeysList, key);
                return;
            }

            if (existing.Contains(key))
                return;

            SessionState.SetString(ApplyKeysList, existing + "|" + key);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode)
                return;

            string keys = SessionState.GetString(ApplyKeysList, string.Empty);
            if (string.IsNullOrEmpty(keys))
                return;

            string[] list = keys.Split('|');
            foreach (string key in list)
            {
                if (string.IsNullOrEmpty(key)) continue;
                string json = SessionState.GetString(key, string.Empty);
                if (string.IsNullOrEmpty(json)) continue;

                var payload = new RuntimeApplyPayload();
                EditorJsonUtility.FromJsonOverwrite(json, payload);
                ApplyPayload(payload);
            }

            SessionState.SetString(ApplyKeysList, string.Empty);
        }

        private static void ApplyPayload(RuntimeApplyPayload payload)
        {
            if (payload == null || string.IsNullOrEmpty(payload.handlerId)) return;

            var handler = GetObjectFromId<MobileCinemachineOrbitalHandler>(payload.handlerId);
            if (handler == null) return;

            Undo.RecordObject(handler, "Apply Runtime Values");
            var handlerSo = new SerializedObject(handler);
            SetFloat(handlerSo, "_rotateSpeed", payload.rotateSpeed);
            SetFloat(handlerSo, "_tiltSpeed", payload.tiltSpeed);
            SetInt(handlerSo, "_activePriority", payload.activePriority);
            SetInt(handlerSo, "_inactivePriority", payload.inactivePriority);
            SetFloat(handlerSo, "_panSpeed", payload.panSpeed);
            SetFloat(handlerSo, "_panReturnSpeed", payload.panReturnSpeed);
            SetFloat(handlerSo, "_zoomSpeed", payload.zoomSpeed);
            SetFloat(handlerSo, "_hardLookRestoreDelay", payload.hardLookRestoreDelay);
            SetBool(handlerSo, "_enablePanLimit", payload.enablePanLimit);
            SetFloat(handlerSo, "_maxPanDistance", payload.maxPanDistance);
            SetBool(handlerSo, "_returnTrackingTargetToOriginOnRelease", payload.returnTrackingTargetToOriginOnRelease);
            SetInt(handlerSo, "_panTargetMode", payload.panTargetMode);
            SetBool(handlerSo, "_lockPanX", payload.lockPanX);
            SetBool(handlerSo, "_lockPanY", payload.lockPanY);
            SetBool(handlerSo, "_lockPanZ", payload.lockPanZ);
            SetFloat(handlerSo, "_maxWallHeight", payload.maxWallHeight);
            SetVector2(handlerSo, "_verticalAxisLimits", payload.verticalAxisLimits);
            SetVector2(handlerSo, "_radiusLimits", payload.radiusLimits);
            SetObjectRef(handlerSo, "_customPanTarget", GetObjectFromId<Transform>(payload.customPanTargetId));
            handlerSo.ApplyModifiedProperties();
            EditorUtility.SetDirty(handler);
            PrefabUtility.RecordPrefabInstancePropertyModifications(handler);
            EditorSceneManager.MarkSceneDirty(handler.gameObject.scene);

            var cam = GetObjectFromId<CinemachineCamera>(payload.camId);
            if (cam != null)
            {
                Undo.RecordObject(cam, "Apply Runtime Values");
                var camSo = new SerializedObject(cam);
                var targetProp = camSo.FindProperty("Target");
                if (targetProp != null)
                {
                    SetObjectRef(targetProp, "TrackingTarget", GetObjectFromId<Transform>(payload.trackingTargetId));
                    SetObjectRef(targetProp, "LookAtTarget", GetObjectFromId<Transform>(payload.lookAtTargetId));
                }
                var lensProp = camSo.FindProperty("Lens");
                if (lensProp != null)
                {
                    SetFloat(lensProp, "FieldOfView", payload.fov);
                    SetFloat(lensProp, "NearClipPlane", payload.nearClip);
                    SetFloat(lensProp, "FarClipPlane", payload.farClip);
                }
                camSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(cam);
                PrefabUtility.RecordPrefabInstancePropertyModifications(cam);
                EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
            }

            var orbit = GetObjectFromId<CinemachineOrbitalFollow>(payload.orbitId);
            if (orbit != null)
            {
                Undo.RecordObject(orbit, "Apply Runtime Values");
                var orbitSo = new SerializedObject(orbit);
                SetFloat(orbitSo, "Radius", payload.orbitRadius);
                SetVector2(orbitSo, "VerticalAxis.Range", payload.orbitVerticalRange);
                SetVector2(orbitSo, "RadialAxis.Range", payload.orbitRadialRange);
                orbitSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(orbit);
                PrefabUtility.RecordPrefabInstancePropertyModifications(orbit);
                EditorSceneManager.MarkSceneDirty(orbit.gameObject.scene);
            }
        }

        private static string GetId(Object obj)
        {
            if (obj == null) return string.Empty;
            return GlobalObjectId.GetGlobalObjectIdSlow(obj).ToString();
        }

        private static T GetObjectFromId<T>(string idString) where T : Object
        {
            if (string.IsNullOrEmpty(idString)) return null;
            if (!GlobalObjectId.TryParse(idString, out GlobalObjectId id))
                return null;
            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as T;
        }

        private static void SetFloat(SerializedObject so, string path, float value)
        {
            var prop = so.FindProperty(path);
            if (prop != null) prop.floatValue = value;
        }

        private static void SetFloat(SerializedProperty root, string relativePath, float value)
        {
            if (root == null) return;
            var prop = root.FindPropertyRelative(relativePath);
            if (prop != null) prop.floatValue = value;
        }

        private static void SetInt(SerializedObject so, string path, int value)
        {
            var prop = so.FindProperty(path);
            if (prop != null) prop.intValue = value;
        }

        private static void SetBool(SerializedObject so, string path, bool value)
        {
            var prop = so.FindProperty(path);
            if (prop != null) prop.boolValue = value;
        }

        private static void SetVector2(SerializedObject so, string path, Vector2 value)
        {
            var prop = so.FindProperty(path);
            if (prop != null) prop.vector2Value = value;
        }

        private static void SetVector2(SerializedProperty root, string relativePath, Vector2 value)
        {
            if (root == null) return;
            var prop = root.FindPropertyRelative(relativePath);
            if (prop != null) prop.vector2Value = value;
        }

        private static void SetObjectRef(SerializedProperty root, string relativePath, Object value)
        {
            if (root == null) return;
            var prop = root.FindPropertyRelative(relativePath);
            if (prop != null) prop.objectReferenceValue = value;
        }

        private static void SetObjectRef(SerializedObject so, string path, Object value)
        {
            var prop = so.FindProperty(path);
            if (prop != null) prop.objectReferenceValue = value;
        }

        private static float GetFloat(Object obj, string path)
        {
            if (obj == null) return 0f;
            var so = new SerializedObject(obj);
            var prop = so.FindProperty(path);
            return prop != null ? prop.floatValue : 0f;
        }

        private static int GetInt(Object obj, string path)
        {
            if (obj == null) return 0;
            var so = new SerializedObject(obj);
            var prop = so.FindProperty(path);
            return prop != null ? prop.intValue : 0;
        }

        private static bool GetBool(Object obj, string path)
        {
            if (obj == null) return false;
            var so = new SerializedObject(obj);
            var prop = so.FindProperty(path);
            return prop != null && prop.boolValue;
        }

        private static Vector2 GetVector2(Object obj, string path)
        {
            if (obj == null) return default;
            var so = new SerializedObject(obj);
            var prop = so.FindProperty(path);
            return prop != null ? prop.vector2Value : default;
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

        private void ApplyHeroIcon(VisualElement root)
        {
            if (root == null) return;
            var icon = root.Q<VisualElement>("heroIcon");
            if (icon == null) return;

            Sprite sprite = LoadSprite(IconsPath, OrbitalIconName);
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

        private void ApplyRuntimeStyling(VisualElement root)
        {
            if (!EditorApplication.isPlaying) return;
            if (root == null) return;
            root.AddToClassList("is-runtime");
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
