using Concept.Core;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace Twinny.Mobile
{
    [System.Serializable]
    public class MobileAction
    {
        public enum ActionType
        {
            PlatformInitializing,
            PlatformInitialized,
            ExperienceReady,
            ExperienceStarting,
            ExperienceStarted,
            ExperienceEnding,
            ExperienceEnded,
            SceneLoadStart,
            SceneLoaded,
            TeleportToLandMark,
            SkyboxHDRIChanged,

            StartInteract,
            StopInteract,
            StartTeleport,
            Teleport,
            ExperienceLoaded,
            EnterImmersiveMode,
            ExitImmersiveMode,
            EnterMockupMode,
            ExitMockupMode,
            EnterDemoMode,
            ExitDemoMode,
            POIFocused

        }

        public ActionType type;
        public UnityEvent onTriggered;
    }

    public class MobileActionTriggers : MonoBehaviour, ITwinnyMobileCallbacks
    {
        [SerializeField] private List<MobileAction> _mobileActions = new List<MobileAction>();

        private void OnEnable() => CallbackHub.RegisterCallback<ITwinnyMobileCallbacks>(this);
        private void OnDisable() => CallbackHub.UnregisterCallback<ITwinnyMobileCallbacks>(this);

        private void TriggerAction(MobileAction.ActionType type)
        {
            foreach (var action in _mobileActions)
            {
                if (action.type == type)
                    action.onTriggered?.Invoke();
            }
        }

        public void AddAction(MobileAction.ActionType type, UnityAction callback)
        {
            var action = new MobileAction { type = type };
            action.onTriggered.AddListener(callback);
            _mobileActions.Add(action);
        }

        public void ClearActions()
        {
            foreach (var action in _mobileActions)
                action.onTriggered.RemoveAllListeners();

            _mobileActions.Clear();
        }

        public void OnPlatformInitializing() => TriggerAction(MobileAction.ActionType.PlatformInitializing);
        public void OnPlatformInitialized() => TriggerAction(MobileAction.ActionType.PlatformInitialized);
        public void OnExperienceReady() => TriggerAction(MobileAction.ActionType.ExperienceReady);
        public void OnExperienceStarting() => TriggerAction(MobileAction.ActionType.ExperienceStarting);
        public void OnExperienceStarted() => TriggerAction(MobileAction.ActionType.ExperienceStarted);
        public void OnExperienceEnding() => TriggerAction(MobileAction.ActionType.ExperienceEnding);
        public void OnExperienceEnded(bool isRunning) => TriggerAction(MobileAction.ActionType.ExperienceEnded);
        public void OnSceneLoadStart(string sceneName) => TriggerAction(MobileAction.ActionType.SceneLoadStart);
        public void OnSceneLoaded(Scene scene) => TriggerAction(MobileAction.ActionType.SceneLoaded);
        public void OnTeleportToLandMark(int landMarkIndex) => TriggerAction(MobileAction.ActionType.TeleportToLandMark);
        public void OnSkyboxHDRIChanged(Material material) => TriggerAction(MobileAction.ActionType.SkyboxHDRIChanged);

        public void OnStartInteract(GameObject gameObject) => TriggerAction(MobileAction.ActionType.StartInteract);
        public void OnStopInteract(GameObject gameObject) => TriggerAction(MobileAction.ActionType.StopInteract);
        public void OnStartTeleport() => TriggerAction(MobileAction.ActionType.StartTeleport);
        public void OnTeleport() => TriggerAction(MobileAction.ActionType.Teleport);
        public void OnExperienceLoaded() => TriggerAction(MobileAction.ActionType.ExperienceLoaded);
        public void OnEnterImmersiveMode() => TriggerAction(MobileAction.ActionType.EnterImmersiveMode);
        public void OnExitImmersiveMode() => TriggerAction(MobileAction.ActionType.ExitImmersiveMode);
        public void OnEnterMockupMode() => TriggerAction(MobileAction.ActionType.EnterMockupMode);
        public void OnExitMockupMode() => TriggerAction(MobileAction.ActionType.ExitMockupMode);
        public void OnEnterDemoMode() => TriggerAction(MobileAction.ActionType.EnterDemoMode);
        public void OnPOIFocused() => TriggerAction(MobileAction.ActionType.POIFocused);
        public void OnExitDemoMode() => TriggerAction(MobileAction.ActionType.ExitDemoMode);
    }
}
