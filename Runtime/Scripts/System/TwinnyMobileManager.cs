using Concept.Core;
using System.Threading.Tasks;
using Twinny.Core;
using Twinny.Mobile.Interactables;
using Twinny.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Twinny.Mobile
{
    public class TwinnyMobileManager : MonoBehaviour, IMobileUICallbacks
    {
        private static string s_pendingLandmarkGuid;

        public static string PendingLandmarkGuid => s_pendingLandmarkGuid;

        private void OnEnable()
        {
            CallbackHub.RegisterCallback<IMobileUICallbacks>(this);
        }

        private void OnDisable()
        {
            CallbackHub.UnregisterCallback<IMobileUICallbacks>(this);
        }

        private async void Start()
        {
            await InitializeAsync();
        }

        private void Update()
        {
        }

        public async Task InitializeAsync()
        {
            StateMachine.ChangeState(new IdleState(this));
            CallbackHub.CallAction<IMobileUICallbacks>(callback => callback.OnStartExperienceRequested(TwinnyMobileRuntime.GetDefaultSceneName()));
        }




        public static void SceneRequest(FloorData data)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.ImmersionSceneName))
                return;

            SyncPendingLandmarkGuid(data);

            CallbackHub.CallAction<IMobileUICallbacks>(callback =>
            {
                switch (data.SceneOpenMode)
                {
                    case FloorData.FloorSceneOpenMode.Immersive:
                    callback.OnImmersiveRequested(data);
                        break;
                    case FloorData.FloorSceneOpenMode.Mockup:
                    callback.OnMockupRequested(data);
                        break;
                    default:
                        Debug.LogError($"[TwinnyMobileManager] Scene open mode '{data.SceneOpenMode.ToString()}' not implemented!");
                        break;
                }
            });

        }






        public async void OnImmersiveRequested(FloorData data = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!WebGLGyroAPI.IsInitialized)
                WebGLGyroAPI.RequestGyroPermission();
#endif
            SyncPendingLandmarkGuid(data);
            string sceneName = data?.ImmersionSceneName ?? string.Empty;
            await CanvasTransition.FadeScreenAsync(true,1f,renderMode:RenderMode.ScreenSpaceOverlay);
            await EnsureSceneLoadedAsync(sceneName);
            StateMachine.ChangeState(new MobileImmersiveState(this));
            await CanvasTransition.FadeScreenAsync(false,1f, renderMode:RenderMode.ScreenSpaceOverlay);

        }

        public void OnMaxWallHeightRequested(float height) { }

        public async void OnMockupRequested(FloorData data = null)
        {
            SyncPendingLandmarkGuid(data);
            string sceneName = data?.ImmersionSceneName ?? string.Empty;
            await CanvasTransition.FadeScreenAsync(true,1f,renderMode:RenderMode.ScreenSpaceOverlay);
            await EnsureSceneLoadedAsync(sceneName);
            StateMachine.ChangeState(new MobileMockupState(this));
            await CanvasTransition.FadeScreenAsync(false,1f,renderMode:RenderMode.ScreenSpaceOverlay);

        }

        public async void OnStartExperienceRequested(string sceneName = "")
        {
            if (string.IsNullOrEmpty(sceneName)) sceneName = TwinnyMobileRuntime.GetDefaultSceneName();
            await StartExperienceSequenceAsync(sceneName);
        }

        public void OnLoadingProgressChanged(float progress) { }
        public void OnExperienceLoaded() { }
        public void OnGyroscopeToggled(bool enabled) { }

        public static void SetPendingLandmarkGuid(string landmarkGuid)
        {
            s_pendingLandmarkGuid = string.IsNullOrWhiteSpace(landmarkGuid) ? string.Empty : landmarkGuid;
        }

        public static void ClearPendingLandmarkGuid()
        {
            s_pendingLandmarkGuid = string.Empty;
        }

        public static bool TryGetPendingLandmarkGuid(out string landmarkGuid)
        {
            landmarkGuid = s_pendingLandmarkGuid;
            return !string.IsNullOrWhiteSpace(landmarkGuid);
        }

        public static bool TryConsumePendingLandmarkGuid(out string landmarkGuid)
        {
            landmarkGuid = s_pendingLandmarkGuid;
            s_pendingLandmarkGuid = string.Empty;
            return !string.IsNullOrWhiteSpace(landmarkGuid);
        }

        private static void SyncPendingLandmarkGuid(FloorData data)
        {
            if (data == null)
                return;

            SetPendingLandmarkGuid(data.UseLandMark ? data.LandmarkGuid : string.Empty);
        }

        private static async Task LoadSceneWithProgressAsync(string sceneName, LoadSceneMode mode)
        {
            const float maxProgressBeforeLoaded = 0.95f;

            CallbackHub.CallAction<ITwinnyMobileCallbacks>(callback => callback.OnSceneLoadStart(sceneName));
            CallbackHub.CallAction<IMobileUICallbacks>(callback => callback.OnLoadingProgressChanged(0f));
            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName, mode);
            if (loadOperation == null)
                return;

            while (!loadOperation.isDone)
            {
                float normalized = Mathf.Clamp01(loadOperation.progress / 0.9f);
                float visibleProgress = normalized * maxProgressBeforeLoaded;
                CallbackHub.CallAction<IMobileUICallbacks>(callback => callback.OnLoadingProgressChanged(visibleProgress));
                await Task.Yield();
            }

            Scene loadedScene = SceneManager.GetSceneByName(sceneName);
            if (loadedScene.IsValid())
            {
                CallbackHub.CallAction<IMobileUICallbacks>(callback => callback.OnLoadingProgressChanged(1f));
                CallbackHub.CallAction<ITwinnyMobileCallbacks>(callback => callback.OnSceneLoaded(loadedScene));
            }

          //  await CanvasTransition.FadeScreenAsync(false, 1f, renderMode: RenderMode.ScreenSpaceOverlay);
        }
        public static async Task UnloadAdditiveScenesExceptMainAsync()
        {
            for (int index = SceneManager.sceneCount - 1; index >= 0; index--)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                if (scene.buildIndex == 0)
                    continue;

                AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(scene);
                if (unloadOperation == null)
                    continue;

                while (!unloadOperation.isDone)
                    await Task.Yield();
            }
        }

        private static bool IsSceneLoaded(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return true;

            Scene scene = SceneManager.GetSceneByName(sceneName);
            return scene.IsValid() && scene.isLoaded;
        }

        private static async Task EnsureSceneLoadedAsync(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                //await UnloadAdditiveScenesExceptMainAsync();
                return;
            }

            if (IsSceneLoaded(sceneName))
                return;

            await UnloadAdditiveScenesExceptMainAsync();
            await LoadSceneWithProgressAsync(sceneName, LoadSceneMode.Additive);
        }

        private async Task StartExperienceSequenceAsync(string sceneName)
        {
            await EnsureSceneLoadedAsync(sceneName);
            StateMachine.ChangeState(new MobileMockupState(this));
            CallbackHub.CallAction<ITwinnyMobileCallbacks>(callback => callback.OnExperienceLoaded());
        }
    }
}

