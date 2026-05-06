using Concept.Core;
using System.Collections.Generic;
using Twinny.Multiplatform.Interactables;
using Twinny.Navigation;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace Twinny.Multiplatform
{
    [System.Serializable]
    public class PlatformAction
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
            FloorSelected,
            FloorFocused,
            FloorUnselected

        }

        public ActionType type;
        public UnityEvent onTriggered;
    }

    public class PlatformActionTriggers : MonoBehaviour, IPlatformCallbacks
    {
        [SerializeField] private List<PlatformAction> _mobileActions = new List<PlatformAction>();

        private void OnEnable() => CallbackHub.RegisterCallback<IPlatformCallbacks>(this);
        private void OnDisable() => CallbackHub.UnregisterCallback<IPlatformCallbacks>(this);

        private void TriggerAction(PlatformAction.ActionType type)
        {
            foreach (var action in _mobileActions)
            {
                if (action.type == type)
                    action.onTriggered?.Invoke();
            }
        }

        public void AddAction(PlatformAction.ActionType type, UnityAction callback)
        {
            var action = new PlatformAction { type = type };
            action.onTriggered.AddListener(callback);
            _mobileActions.Add(action);
        }

        public void ClearActions()
        {
            foreach (var action in _mobileActions)
                action.onTriggered.RemoveAllListeners();

            _mobileActions.Clear();
        }

        public void OnPlatformInitializing() => TriggerAction(PlatformAction.ActionType.PlatformInitializing);
        public void OnPlatformInitialized() => TriggerAction(PlatformAction.ActionType.PlatformInitialized);
        public void OnExperienceReady() => TriggerAction(PlatformAction.ActionType.ExperienceReady);
        public void OnExperienceStarting() => TriggerAction(PlatformAction.ActionType.ExperienceStarting);
        public void OnExperienceStarted() => TriggerAction(PlatformAction.ActionType.ExperienceStarted);
        public void OnExperienceEnding() => TriggerAction(PlatformAction.ActionType.ExperienceEnding);
        public void OnExperienceEnded(bool isRunning) => TriggerAction(PlatformAction.ActionType.ExperienceEnded);
        public void OnSceneLoadStart(string sceneName) => TriggerAction(PlatformAction.ActionType.SceneLoadStart);
        public void OnSceneLoaded(Scene scene) => TriggerAction(PlatformAction.ActionType.SceneLoaded);
        public void OnRequestLandMark(string landmarkGuid) { }
        public void OnRequestLandMark(Landmark landmark) { }
        public void OnTeleportToLandMark(int landMarkIndex) => TriggerAction(PlatformAction.ActionType.TeleportToLandMark);
        public void OnSkyboxHDRIChanged(Material material) => TriggerAction(PlatformAction.ActionType.SkyboxHDRIChanged);

        public void OnStartInteract(GameObject gameObject) => TriggerAction(PlatformAction.ActionType.StartInteract);
        public void OnStopInteract(GameObject gameObject) => TriggerAction(PlatformAction.ActionType.StopInteract);
        public void OnStartTeleport() => TriggerAction(PlatformAction.ActionType.StartTeleport);
        public void OnTeleport() => TriggerAction(PlatformAction.ActionType.Teleport);
        public void OnExperienceLoaded() => TriggerAction(PlatformAction.ActionType.ExperienceLoaded);
        public void OnEnterImmersiveMode() => TriggerAction(PlatformAction.ActionType.EnterImmersiveMode);
        public void OnExitImmersiveMode() => TriggerAction(PlatformAction.ActionType.ExitImmersiveMode);
        public void OnEnterMockupMode() => TriggerAction(PlatformAction.ActionType.EnterMockupMode);
        public void OnExitMockupMode() => TriggerAction(PlatformAction.ActionType.ExitMockupMode);
        public void OnEnterDemoMode() => TriggerAction(PlatformAction.ActionType.EnterDemoMode);
        public void OnFloorSelected(Floor floor) => TriggerAction(PlatformAction.ActionType.FloorSelected);
        public void OnFloorFocused(Floor floor) => TriggerAction(PlatformAction.ActionType.FloorFocused);
        public void OnFloorUnselected(Floor floor) => TriggerAction(PlatformAction.ActionType.FloorUnselected);
        public void OnExitDemoMode() => TriggerAction(PlatformAction.ActionType.ExitDemoMode);
    }
}
