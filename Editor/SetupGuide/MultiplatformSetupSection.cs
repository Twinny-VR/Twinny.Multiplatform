#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using ConceptFactory.UIEssentials.Editor;
using System.Linq;
using Twinny.Core.Editor;
using Twinny.Multiplatform;
using Twinny.Multiplatform.Input;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;

namespace Twinny.Editor
{
    [InitializeOnLoad]
    public static class MultiplatformSetupSectionRegister
    {
        private const string PackageName = "com.twinny.multiplatform";
        private const string IconsAtlasPath = "Packages/com.twinny.twe26/Editor/SetupGuide/Resources/Sprites/Icons.png";
        private const string IconSpriteName = "Ico_Multi";

        static MultiplatformSetupSectionRegister()
        {
            SetupGuideWindow.RegisterModule(new ModuleInfo
            {
                sortOrder = 20,
                moduleName = PackageName,
                moduleDisplayName = "Multiplatform",
                moduleIcon = LoadIcon(),
                moduleInstallPath = "https://github.com/Twinny-VR/Twinny.Multiplatform.git"
            }, typeof(MultiplatformSetupSection));
        }

        private static Sprite LoadIcon()
        {
            return AssetDatabase.LoadAllAssetsAtPath(IconsAtlasPath)
                .OfType<Sprite>()
                .FirstOrDefault(sprite => sprite.name == IconSpriteName);
        }
    }

    [UxmlElement]
    public partial class MultiplatformSetupSection : VisualElement, IModuleSetup
    {
        private const string UxmlAssetPath = "Packages/com.twinny.multiplatform/Editor/SetupGuide/MultiplatformSetupSection.uxml";
        private const string InputSettingsAssetPath = "Assets/Resources/InputSettings.asset";
        private const string PackageRootPath = "Packages/com.twinny.multiplatform";
        private static readonly Dictionary<string, string> InputPropertyHints = new()
        {
            { "_dragThreshold", "Minimum movement before a touch is treated as a drag instead of a tap." },
            { "_longPressTime", "How long the user must hold one finger before a long press is triggered." },
            { "_edgeThreshold", "Screen-edge margin used to detect gestures that begin near the device borders." },
            { "_shakeThreshold", "Acceleration required for the device movement to count as a shake gesture." },
            { "_twoFingerLongPressTime", "How long two fingers must stay pressed before a two-finger hold is recognized." },
            { "_pickupAccelerationThreshold", "Acceleration needed to detect that the device has been picked up." },
            { "_putDownStableTime", "How long the device must stay stable before it is considered placed down." }
        };
        private readonly Label _title;
        private readonly Label _versionLabel;
        private readonly Button _updateButton;
        private readonly Label _description;
        private readonly Button _runtimeTabButton;
        private readonly Button _inputTabButton;
        private readonly VisualElement _runtimeTabContent;
        private readonly VisualElement _inputTabContent;
        private readonly VisualElement _runtimeInspectorRoot;
        private readonly VisualElement _inputInspectorRoot;
        private SerializedObject _runtimeSerializedObject;
        private SerializedObject _inputSerializedObject;

        public MultiplatformSetupSection()
        {
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlAssetPath);

            if (visualTree != null)
            {
                visualTree.CloneTree(this);
                _title = this.Q<Label>("MultiplatformTitle");
                _versionLabel = this.Q<Label>("MultiplatformVersion");
                _updateButton = this.Q<Button>("MultiplatformUpdateButton");
                _description = this.Q<Label>("MultiplatformDescription");
                _runtimeTabButton = this.Q<Button>("RuntimeTabButton");
                _inputTabButton = this.Q<Button>("InputTabButton");
                _runtimeTabContent = this.Q<VisualElement>("RuntimeTabContent");
                _inputTabContent = this.Q<VisualElement>("InputTabContent");
                _runtimeInspectorRoot = this.Q<VisualElement>("RuntimeInspectorRoot");
                _inputInspectorRoot = this.Q<VisualElement>("InputInspectorRoot");
                RegisterTabCallbacks();

                if (_updateButton != null)
                    _updateButton.clicked += () => PackageUpdateUtility.RequestUpdate(PackageRootPath);
            }
            else
            {
                AddToClassList("content");
                Add(new Label("Multiplatform setup layout not found."));
            }

            if (_title != null)
                _title.text = "Twinny Multiplatform";

            if (_versionLabel != null)
                _versionLabel.text = PackageUpdateUtility.GetPackageVersionLabel(PackageRootPath);

            if (_description != null)
            {
                _description.text =
                    "Configure the screen-based Twinny runtime for Windows, WebGL, Android, and iOS. " +
                    "This package centralizes input, navigation, cameras, UI, and scene flow outside XR.";
            }
        }

        public void OnShowSection(SetupGuideWindow guideWindow, int tabIndex = 0)
        {
            if (_updateButton != null)
                _updateButton.style.display = PackageUpdateUtility.CanShowUpdateButton(PackageRootPath) ? DisplayStyle.Flex : DisplayStyle.None;

            RebuildRuntimeSection();
            RebuildInputSection();
            ShowTab(tabIndex == 1 ? "input" : "runtime");
        }

        public void OnApply()
        {
        }

        private void RebuildRuntimeSection()
        {
            if (_runtimeInspectorRoot == null)
                return;

            _runtimeInspectorRoot.Clear();

            PlatformRuntime runtimePreset = PlatformRuntime.GetInstance(true);
            if (runtimePreset == null)
            {
                _runtimeInspectorRoot.Add(new Label("PlatformRuntimePreset could not be loaded."));
                return;
            }

            _runtimeSerializedObject = new SerializedObject(runtimePreset);
            SerializedProperty iterator = _runtimeSerializedObject.GetIterator();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.name == "m_Script")
                    continue;

                if (iterator.name == "defaultSceneName")
                {
                    AddScenePickerField(_runtimeInspectorRoot, iterator.Copy(), _runtimeSerializedObject, "Default Scene Name");
                    continue;
                }

                PropertyField field = new PropertyField(iterator.Copy());
                field.Bind(_runtimeSerializedObject);
                field.AddToClassList("multiplatform-runtime-field");
                _runtimeInspectorRoot.Add(field);
            }
        }

        private void RebuildInputSection()
        {
            if (_inputInspectorRoot == null)
                return;

            _inputInspectorRoot.Clear();

            InputSettings inputSettings = AssetDatabase.LoadAssetAtPath<InputSettings>(InputSettingsAssetPath);
            if (inputSettings == null)
            {
                _inputInspectorRoot.Add(new Label($"InputSettings could not be loaded at '{InputSettingsAssetPath}'."));
                return;
            }

            _inputSerializedObject = new SerializedObject(inputSettings);
            SerializedProperty iterator = _inputSerializedObject.GetIterator();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.name == "m_Script")
                    continue;

                AddInputField(_inputInspectorRoot, iterator.Copy(), _inputSerializedObject);
            }
        }

        private void RegisterTabCallbacks()
        {
            if (_runtimeTabButton != null)
                _runtimeTabButton.clicked += () => ShowTab("runtime");

            if (_inputTabButton != null)
                _inputTabButton.clicked += () => ShowTab("input");
        }

        private void ShowTab(string tabName)
        {
            bool showInput = tabName == "input";

            if (_runtimeTabContent != null)
                _runtimeTabContent.style.display = showInput ? DisplayStyle.None : DisplayStyle.Flex;

            if (_inputTabContent != null)
                _inputTabContent.style.display = showInput ? DisplayStyle.Flex : DisplayStyle.None;

            _runtimeTabButton?.EnableInClassList("active", !showInput);
            _inputTabButton?.EnableInClassList("active", showInput);
        }

        private void AddScenePickerField(VisualElement container, SerializedProperty sceneProp, SerializedObject owner, string labelText)
        {
            if (container == null || sceneProp == null || owner == null)
                return;

            var row = new VisualElement();
            row.AddToClassList("row");
            row.AddToClassList("multiplatform-runtime-field");
            row.AddToClassList("multiplatform-scene-picker-row");

            var label = new Label(labelText);
            label.AddToClassList("row-label");
            label.AddToClassList("multiplatform-scene-picker-label");
            row.Add(label);

            var field = new TextField
            {
                value = sceneProp.stringValue
            };
            field.AddToClassList("row-field");
            field.AddToClassList("multiplatform-scene-picker-text-field");
            field.BindProperty(sceneProp);

            var values = new VisualElement();
            values.AddToClassList("inline-values");
            values.AddToClassList("multiplatform-scene-picker-values");
            values.Add(field);

            var searchButton = new Button
            {
                text = string.Empty
            };
            searchButton.AddToClassList("multiplatform-scene-picker-button");
            searchButton.tooltip = "Search Scene";
            searchButton.clicked += () => ShowScenePicker(searchButton, sceneProp.propertyPath, owner);

            Texture searchIcon = EditorGUIUtility.IconContent("Search Icon").image;
            if (searchIcon is Texture2D searchTexture)
            {
                searchButton.iconImage = searchTexture;
            }
            else
            {
                searchButton.text = "?";
            }

            values.Add(searchButton);
            row.Add(values);
            container.Add(row);
        }

        private void AddInputField(VisualElement container, SerializedProperty property, SerializedObject owner)
        {
            if (container == null || property == null || owner == null)
                return;

            var fieldGroup = new VisualElement();
            fieldGroup.AddToClassList("multiplatform-input-field-group");

            var field = new PropertyField(property);
            field.Bind(owner);
            field.AddToClassList("multiplatform-runtime-field");
            fieldGroup.Add(field);

            if (InputPropertyHints.TryGetValue(property.name, out string hintText))
            {
                var hintBox = new VisualElement();
                hintBox.AddToClassList("multiplatform-input-hint-box");

                var hintLabel = new Label(hintText);
                hintLabel.AddToClassList("multiplatform-input-hint");
                hintBox.Add(hintLabel);
                fieldGroup.Add(hintBox);
            }

            container.Add(fieldGroup);
        }

        private void ShowScenePicker(Button anchor, string propertyPath, SerializedObject owner)
        {
            if (anchor == null || string.IsNullOrWhiteSpace(propertyPath) || owner == null)
                return;

            SerializedProperty sceneProp = owner.FindProperty(propertyPath);
            if (sceneProp == null)
                return;

            UnityEditor.PopupWindow.Show(anchor.worldBound, new ScenePickerPopup(sceneProp.stringValue, entry => OnSceneSelected(propertyPath, owner, entry)));
        }

        private void OnSceneSelected(string propertyPath, SerializedObject owner, ScenePickerEntry scene)
        {
            if (string.IsNullOrWhiteSpace(propertyPath) || owner == null || scene == null)
                return;

            if (scene.IsMissing)
            {
                bool removeScene = EditorUtility.DisplayDialog(
                    "Scene Missing",
                    $"The scene entry '{scene.Name}' no longer exists in the project.\n\nDo you want to remove it from Build Settings?",
                    "Remove Scene",
                    "Keep It");

                if (removeScene)
                {
                    RemoveSceneFromBuildSettings(scene);
                }

                return;
            }

            if (!scene.InBuildSettings)
            {
                bool addToBuild = EditorUtility.DisplayDialog(
                    "Scene Not In Build Settings",
                    $"The scene '{scene.Name}' is not in Build Settings.\n\nDo you want to add it now?",
                    "Add Scene",
                    "Cancel");

                if (!addToBuild)
                {
                    return;
                }

                AddSceneToBuildSettings(scene);
            }

            owner.Update();
            SerializedProperty property = owner.FindProperty(propertyPath);
            if (property == null)
                return;

            property.stringValue = scene.Name;
            owner.ApplyModifiedProperties();
        }

        private static void AddSceneToBuildSettings(ScenePickerEntry scene)
        {
            if (scene == null || string.IsNullOrWhiteSpace(scene.Path))
                return;

            EditorBuildSettingsScene[] existingScenes = EditorBuildSettings.scenes ?? Array.Empty<EditorBuildSettingsScene>();
            var updatedScenes = new List<EditorBuildSettingsScene>(existingScenes)
            {
                new EditorBuildSettingsScene(scene.Path, true)
            };

            EditorBuildSettings.scenes = updatedScenes.ToArray();
        }

        private static void RemoveSceneFromBuildSettings(ScenePickerEntry scene)
        {
            if (scene == null || string.IsNullOrWhiteSpace(scene.Path))
                return;

            EditorBuildSettingsScene[] existingScenes = EditorBuildSettings.scenes ?? Array.Empty<EditorBuildSettingsScene>();
            var updatedScenes = new List<EditorBuildSettingsScene>();

            for (int i = 0; i < existingScenes.Length; i++)
            {
                EditorBuildSettingsScene buildScene = existingScenes[i];
                if (buildScene == null || string.Equals(buildScene.path, scene.Path, StringComparison.OrdinalIgnoreCase))
                    continue;

                updatedScenes.Add(buildScene);
            }

            EditorBuildSettings.scenes = updatedScenes.ToArray();
        }
    }
}
#endif
