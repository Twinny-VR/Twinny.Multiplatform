using Concept.Core;
using System.Threading.Tasks;
using Twinny.Core;
using Twinny.Multiplatform.Interactables;
using Twinny.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Twinny.Multiplatform
{
    /// <summary>
    /// Aplica os globais do SimpleSky (Skybox/Simple) para o visual "Matrix construct".
    /// Reaplica ao descarregar qualquer cena: os globais do shader não são repostos sozinhos quando o Weather sai da hierarquia.
    /// </summary>
    [ExecuteAlways]
    public class PlatformManager : MonoBehaviour, IPlatformUICallbacks
    {
        private static string s_pendingLandmarkGuid;

        public static string PendingLandmarkGuid => s_pendingLandmarkGuid;

        [Header("Matrix construct sky (Weather SimpleSky shader)")]
        [Tooltip("Branco no zenite, cinza junto ao horizonte. Para não competir com o Weather a cada frame, nesta cena desativa \"Drive Skybox Material\" nos controladores Weather (Solar/Lunar).")]
        [SerializeField] private bool _applyMatrixConstructSky = true;

        [SerializeField] private Color _matrixSkyZenith = new Color(0.98f, 0.98f, 0.99f, 1f);
        [SerializeField] private Color _matrixSkyHorizon = new Color(0.84f, 0.84f, 0.87f, 1f);
        [SerializeField] private Color _matrixSkyBelowHorizon = new Color(0.72f, 0.72f, 0.76f, 1f);
        [SerializeField] private float _matrixSkyIntensity = 1f;
        [SerializeField] private float _matrixTopFalloff = 8f;
        [SerializeField] private float _matrixBottomFalloff = 22f;

        [Tooltip("Enquanto o Matrix sky estiver ativo, desliga o componente Volume na Main Camera (ex.: nuvens/post URP), para não competir com o céu branco.")]
        [SerializeField] private bool _disableMainCameraVolumeWhenMatrix = true;

        private Volume _suppressedMainCameraVolume;
        private bool _suppressedMainCameraVolumePreviousEnabled;

        private static readonly int s_IdAmbientSky = Shader.PropertyToID("_WeatherAmbientSky");
        private static readonly int s_IdAmbientHorizon = Shader.PropertyToID("_WeatherAmbientHorizon");
        private static readonly int s_IdAmbientGround = Shader.PropertyToID("_WeatherAmbientGround");
        private static readonly int s_IdSkyIntensity = Shader.PropertyToID("_WeatherSkyIntensity");
        private static readonly int s_IdTopFalloff = Shader.PropertyToID("_WeatherTopSkyFalloff");
        private static readonly int s_IdBottomFalloff = Shader.PropertyToID("_WeatherBottomSkyFalloff");
        private static readonly int s_IdSunDirection = Shader.PropertyToID("_WeatherSunDirection");
        private static readonly int s_IdSunColor = Shader.PropertyToID("_WeatherSunColor");
        private static readonly int s_IdSunIntensity = Shader.PropertyToID("_WeatherSunIntensity");
        private static readonly int s_IdSunFalloff = Shader.PropertyToID("_WeatherSunFalloff");
        private static readonly int s_IdSunSize = Shader.PropertyToID("_WeatherSunSize");
        private static readonly int s_IdNightValue = Shader.PropertyToID("_WeatherNightValue");
        private static readonly int s_IdStarSize = Shader.PropertyToID("_WeatherStarSize");
        private static readonly int s_IdStarDensity = Shader.PropertyToID("_WeatherStarDensity");
        private static readonly int s_IdStarTwinkle = Shader.PropertyToID("_WeatherStarTwinkleAmount");
        private static readonly int s_IdStarFlicker = Shader.PropertyToID("_WeatherStarColorFlicker");
        private static readonly int s_IdStarHorizonChroma = Shader.PropertyToID("_WeatherStarHorizonChromaticShift");
        private static readonly int s_IdMoonDirection = Shader.PropertyToID("_WeatherMoonDirection");
        private static readonly int s_IdMoonColor = Shader.PropertyToID("_WeatherMoonColor");
        private static readonly int s_IdMoonIntensity = Shader.PropertyToID("_WeatherMoonIntensity");
        private static readonly int s_IdMoonHaloIntensity = Shader.PropertyToID("_WeatherMoonHaloIntensity");
        private static readonly int s_IdMoonBorderHaloIntensity = Shader.PropertyToID("_WeatherMoonBorderHaloIntensity");
        private static readonly int s_IdMoonSkyVisibility = Shader.PropertyToID("_WeatherMoonSkyVisibility");
        private static readonly int s_IdMoonDarkSkyVisibility = Shader.PropertyToID("_WeatherMoonDarkSkyVisibility");

        private void OnEnable()
        {
            SceneManager.sceneUnloaded += OnSceneUnloadedRestoreMatrixSky;
            CallbackHub.RegisterCallback<IPlatformUICallbacks>(this);
            ApplyMatrixConstructSky();
        }

        private void OnDisable()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloadedRestoreMatrixSky;
            CallbackHub.UnregisterCallback<IPlatformUICallbacks>(this);
            RestoreMainCameraVolumeAfterMatrix();
        }

        private void OnSceneUnloadedRestoreMatrixSky(Scene scene)
        {
            if (!isActiveAndEnabled || !_applyMatrixConstructSky)
                return;
            ApplyMatrixConstructSky();
        }

        private void OnValidate()
        {
            if (!isActiveAndEnabled || !_applyMatrixConstructSky)
                return;
            ApplyMatrixConstructSky();
        }

        /// <summary>
        /// Reaplica os globais do SimpleSky (ex.: após fechar cena aditiva no editor).
        /// </summary>
        public void ApplyMatrixSkyNow()
        {
            ApplyMatrixConstructSky();
        }

        private void ApplyMatrixConstructSky()
        {
            if (!_applyMatrixConstructSky)
            {
                RestoreMainCameraVolumeAfterMatrix();
                return;
            }

            Shader.SetGlobalColor(s_IdAmbientSky, _matrixSkyZenith);
            Shader.SetGlobalColor(s_IdAmbientHorizon, _matrixSkyHorizon);
            Shader.SetGlobalColor(s_IdAmbientGround, _matrixSkyBelowHorizon);
            Shader.SetGlobalFloat(s_IdSkyIntensity, _matrixSkyIntensity);
            Shader.SetGlobalFloat(s_IdTopFalloff, _matrixTopFalloff);
            Shader.SetGlobalFloat(s_IdBottomFalloff, _matrixBottomFalloff);

            Shader.SetGlobalVector(s_IdSunDirection, Vector3.up);
            Shader.SetGlobalColor(s_IdSunColor, Color.white);
            Shader.SetGlobalFloat(s_IdSunIntensity, 0f);
            Shader.SetGlobalFloat(s_IdSunFalloff, 1000f);
            Shader.SetGlobalFloat(s_IdSunSize, 0f);

            Shader.SetGlobalFloat(s_IdNightValue, 0f);
            Shader.SetGlobalFloat(s_IdStarSize, 0f);
            Shader.SetGlobalFloat(s_IdStarDensity, 0f);
            Shader.SetGlobalFloat(s_IdStarTwinkle, 0f);
            Shader.SetGlobalFloat(s_IdStarFlicker, 0f);
            Shader.SetGlobalFloat(s_IdStarHorizonChroma, 0f);

            Shader.SetGlobalVector(s_IdMoonDirection, Vector3.down);
            Shader.SetGlobalColor(s_IdMoonColor, Color.black);
            Shader.SetGlobalFloat(s_IdMoonIntensity, 0f);
            Shader.SetGlobalFloat(s_IdMoonHaloIntensity, 0f);
            Shader.SetGlobalFloat(s_IdMoonBorderHaloIntensity, 0f);
            Shader.SetGlobalFloat(s_IdMoonSkyVisibility, 0f);
            Shader.SetGlobalFloat(s_IdMoonDarkSkyVisibility, 0f);

            ApplyMainCameraVolumeForMatrixSky();
        }

        /// <summary>
        /// Alinha com o céu Matrix: sem Volume na main (ou repõe quando o Matrix fica desligado).
        /// </summary>
        private void ApplyMainCameraVolumeForMatrixSky()
        {
            if (!_disableMainCameraVolumeWhenMatrix || !_applyMatrixConstructSky)
            {
                RestoreMainCameraVolumeAfterMatrix();
                return;
            }

            Camera main = Camera.main;
            Volume volume = main != null ? main.GetComponent<Volume>() : null;

            if (volume == null)
            {
                RestoreMainCameraVolumeAfterMatrix();
                return;
            }

            if (_suppressedMainCameraVolume != null && _suppressedMainCameraVolume != volume)
                RestoreMainCameraVolumeAfterMatrix();

            if (_suppressedMainCameraVolume != volume)
            {
                _suppressedMainCameraVolume = volume;
                _suppressedMainCameraVolumePreviousEnabled = volume.enabled;
            }

            volume.enabled = false;
        }

        private void RestoreMainCameraVolumeAfterMatrix()
        {
            if (_suppressedMainCameraVolume == null)
                return;

            _suppressedMainCameraVolume.enabled = _suppressedMainCameraVolumePreviousEnabled;
            _suppressedMainCameraVolume = null;
        }

        private async void Start()
        {
            await InitializeAsync();
        }

        public async Task InitializeAsync()
        {
            StateMachine.ChangeState(new IdleState(this));
            CallbackHub.CallAction<IPlatformUICallbacks>(callback => callback.OnStartExperienceRequested(PlatformRuntime.GetDefaultSceneName()));
        }




        public static void SceneRequest(FloorData data)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.ImmersionSceneName))
                return;

            SyncPendingLandmarkGuid(data);

            CallbackHub.CallAction<IPlatformUICallbacks>(callback =>
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
                        Debug.LogError($"[PlatformManager] Scene open mode '{data.SceneOpenMode.ToString()}' not implemented!");
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
            StateMachine.ChangeState(new ImmersiveState(this));
            await CanvasTransition.FadeScreenAsync(false,1f, renderMode:RenderMode.ScreenSpaceOverlay);

        }

        public void OnMaxWallHeightRequested(float height) { }

        public async void OnMockupRequested(FloorData data = null)
        {
            SyncPendingLandmarkGuid(data);
            string sceneName = data?.ImmersionSceneName ?? string.Empty;
            await CanvasTransition.FadeScreenAsync(true,1f,renderMode:RenderMode.ScreenSpaceOverlay);
            await EnsureSceneLoadedAsync(sceneName);
            StateMachine.ChangeState(new MockupState(this));
            await CanvasTransition.FadeScreenAsync(false,1f,renderMode:RenderMode.ScreenSpaceOverlay);

        }

        public async void OnStartExperienceRequested(string sceneName = "")
        {
            if (string.IsNullOrEmpty(sceneName)) sceneName = PlatformRuntime.GetDefaultSceneName();
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

            CallbackHub.CallAction<IPlatformCallbacks>(callback => callback.OnSceneLoadStart(sceneName));
            CallbackHub.CallAction<IPlatformUICallbacks>(callback => callback.OnLoadingProgressChanged(0f));
            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName, mode);
            if (loadOperation == null)
                return;

            while (!loadOperation.isDone)
            {
                float normalized = Mathf.Clamp01(loadOperation.progress / 0.9f);
                float visibleProgress = normalized * maxProgressBeforeLoaded;
                CallbackHub.CallAction<IPlatformUICallbacks>(callback => callback.OnLoadingProgressChanged(visibleProgress));
                await Task.Yield();
            }

            Scene loadedScene = SceneManager.GetSceneByName(sceneName);
            if (loadedScene.IsValid())
            {
                CallbackHub.CallAction<IPlatformUICallbacks>(callback => callback.OnLoadingProgressChanged(1f));
                CallbackHub.CallAction<IPlatformCallbacks>(callback => callback.OnSceneLoaded(loadedScene));
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
            StateMachine.ChangeState(new MockupState(this));
            CallbackHub.CallAction<IPlatformCallbacks>(callback => callback.OnExperienceLoaded());
        }
    }
}

