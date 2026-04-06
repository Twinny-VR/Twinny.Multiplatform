#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Twinny.Multiplatform
{
    /// <summary>
    /// Em modo edição, ao fazer Unload/Close da cena na Hierarchy, o Unity nem sempre usa
    /// <c>removingScene == true</c> em <see cref="EditorSceneManager.sceneClosing"/> — por isso
    /// também usamos <see cref="EditorSceneManager.sceneClosed"/> e um fallback pela contagem de cenas.
    /// </summary>
    [InitializeOnLoad]
    internal static class PlatformManagerSkyRestoreEditor
    {
        private static int s_LastLoadedSceneCount = -1;
        private static bool s_RestorePending;

        static PlatformManagerSkyRestoreEditor()
        {
            EditorSceneManager.sceneClosed += OnEditorSceneClosed;
            EditorSceneManager.sceneClosing += OnEditorSceneClosing;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            s_LastLoadedSceneCount = SceneManager.sceneCount;
        }

        private static void OnEditorSceneClosed(Scene scene)
        {
            ScheduleRestore();
        }

        private static void OnEditorSceneClosing(Scene scene, bool removingScene)
        {
            // Não filtrar por removingScene: com "Unload Scene" na Hierarchy o Unity pode passar false.
            ScheduleRestore();
        }

        private static void OnHierarchyChanged()
        {
            if (Application.isPlaying)
                return;

            int count = SceneManager.sceneCount;
            if (count < s_LastLoadedSceneCount)
                ScheduleRestore();

            s_LastLoadedSceneCount = count;
        }

        private static void ScheduleRestore()
        {
            if (Application.isPlaying)
                return;

            if (s_RestorePending)
                return;

            s_RestorePending = true;
            EditorApplication.delayCall += () =>
            {
                s_RestorePending = false;
                RestoreMatrixSkyAfterSceneRemoved();
            };
        }

        private static void RestoreMatrixSkyAfterSceneRemoved()
        {
            s_LastLoadedSceneCount = SceneManager.sceneCount;

            foreach (PlatformManager manager in Object.FindObjectsByType<PlatformManager>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (manager != null && manager.isActiveAndEnabled)
                    manager.ApplyMatrixSkyNow();
            }
        }
    }
}
#endif
