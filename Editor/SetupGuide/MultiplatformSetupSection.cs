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
        private const string UssAssetPath = "Packages/com.twinny.multiplatform/Editor/SetupGuide/MultiplatformSetupSection.uss";
        private const string InputSettingsAssetPath = "Assets/Resources/InputSettings.asset";
        private const string PackageRootPath = "Packages/com.twinny.multiplatform";
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
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssAssetPath);

            if (styleSheet != null)
                styleSheets.Add(styleSheet);

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

                PropertyField field = new PropertyField(iterator.Copy());
                field.Bind(_inputSerializedObject);
                field.AddToClassList("multiplatform-runtime-field");
                _inputInspectorRoot.Add(field);
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
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.justifyContent = Justify.SpaceBetween;

            var label = new Label(labelText);
            label.AddToClassList("row-label");
            label.style.minWidth = 140f;
            label.style.marginRight = 8f;
            label.style.flexShrink = 0f;
            row.Add(label);

            var field = new TextField
            {
                value = sceneProp.stringValue
            };
            field.AddToClassList("row-field");
            field.style.flexGrow = 1f;
            field.style.flexShrink = 1f;
            field.style.minWidth = 0f;
            field.BindProperty(sceneProp);

            var values = new VisualElement();
            values.AddToClassList("inline-values");
            values.style.flexDirection = FlexDirection.Row;
            values.style.alignItems = Align.Center;
            values.style.flexGrow = 1f;
            values.style.flexShrink = 1f;
            values.style.minWidth = 0f;
            values.Add(field);

            var searchButton = new Button
            {
                text = string.Empty
            };
            searchButton.tooltip = "Search Scene";
            searchButton.clicked += () => ShowScenePicker(searchButton, sceneProp.propertyPath, owner);
            searchButton.style.width = 28f;
            searchButton.style.minWidth = 28f;
            searchButton.style.unityTextAlign = TextAnchor.MiddleCenter;

            Texture searchIcon = EditorGUIUtility.IconContent("Search Icon").image;
            if (searchIcon is Texture2D searchTexture)
            {
                searchButton.style.backgroundImage = new StyleBackground(searchTexture);
                searchButton.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
                searchButton.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
                searchButton.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
                searchButton.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            }
            else
            {
                searchButton.text = "Q";
            }

            values.Add(searchButton);
            row.Add(values);
            container.Add(row);
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
