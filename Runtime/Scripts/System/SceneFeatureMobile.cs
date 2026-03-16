using System.Runtime.Serialization;
using Concept.Core;
using Twinny.Core;
using Twinny.Mobile.Interactables;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Twinny.Mobile
{
    public class SceneFeatureMobile : SceneFeature, ITwinnyMobileCallbacks
    {
        [SerializeField] private Material m_fpsSkyBox;
        [SerializeField, OptionalField] private Material m_thirdPersonSkyBox;



        private void OnEnable()
        {
            CallbackHub.RegisterCallback<ITwinnyMobileCallbacks>(this);
        }

        protected override void Awake()
        {
            base.Awake();
        }

        protected override void Start()
        {
            base.Start();
        }

        private void OnDisable()
        {
            CallbackHub.UnregisterCallback<ITwinnyMobileCallbacks>(this);
        }


        public override void TeleportToLandMark(int landMarkIndex)
        {
        }

        #region ITwinnyMobileCallbacks
        public void OnExperienceEnded(bool isRunning)
        {
            throw new System.NotImplementedException();
        }

        public void OnExperienceEnding()
        {
            throw new System.NotImplementedException();
        }

        public void OnExperienceReady()
        {
            throw new System.NotImplementedException();
        }

        public void OnExperienceStarted()
        {
            throw new System.NotImplementedException();
        }

        public void OnExperienceLoaded()
        {
            throw new System.NotImplementedException();
        }

        public void OnExperienceStarting()
        {
            throw new System.NotImplementedException();
        }

        public void OnPlatformInitialized()
        {
            throw new System.NotImplementedException();
        }

        public void OnPlatformInitializing()
        {
            throw new System.NotImplementedException();
        }

        public void OnSceneLoaded(Scene scene)
        {
            throw new System.NotImplementedException();
        }

        public void OnSceneLoadStart(string sceneName)
        {
            throw new System.NotImplementedException();
        }

        public void OnSkyboxHDRIChanged(Material material)
        {
            throw new System.NotImplementedException();
        }

        public void OnStartInteract(GameObject gameObject)
        {
            throw new System.NotImplementedException();
        }

        public void OnStartTeleport()
        {
            throw new System.NotImplementedException();
        }

        public void OnStopInteract(GameObject gameObject)
        {
            throw new System.NotImplementedException();
        }

        public void OnTeleport()
        {
            throw new System.NotImplementedException();
        }

        public void OnEnterImmersiveMode()
        {
            throw new System.NotImplementedException();
        }
        public void OnExitImmersiveMode() { }

        public void OnEnterMockupMode()
        {
            throw new System.NotImplementedException();
        }
        public void OnExitMockupMode() { }
        public void OnEnterDemoMode() { }
        public void OnExitDemoMode() { }
        public void OnFloorSelected(Floor floor) { }
        public void OnFloorFocused(Floor floor) { }
        public void OnFloorUnselected(Floor floor) { }

        public void OnTeleportToLandMark(int landMarkIndex)
        {
            throw new System.NotImplementedException();
        }
        #endregion

    }
}
