using System;
using Concept.Core;
using Twinny.Mobile;
using UnityEngine;

namespace Twinny.Mobile.Interactables
{
    [Serializable]
    public class BuildingFloorEntry
    {
        [SerializeField] private GameObject _root;

        public GameObject Root => _root;
        public Transform RootTransform => _root != null ? _root.transform : null;
        public Floor FloorComponent => _root != null ? _root.GetComponent<Floor>() : null;
        public bool HasInteractiveFloor => FloorComponent != null;

        public bool Matches(Floor floor)
        {
            if (floor == null || _root == null)
                return false;

            return floor.transform == _root.transform || floor.transform.IsChildOf(_root.transform);
        }

        public void SetVisible(bool isVisible)
        {
            if (_root != null)
                _root.SetActive(isVisible);
        }
    }

    public class Building : MonoBehaviour, ITwinnyMobileCallbacks
    {
        [SerializeField] private BuildingFloorEntry[] _floors;
        [SerializeField] private bool _showAllFloorsWhenUnselected = true;

        public BuildingFloorEntry[] Floors => _floors;

        private void OnEnable()
        {
            CallbackHub.RegisterCallback<ITwinnyMobileCallbacks>(this);
        }

        private void OnDisable()
        {
            CallbackHub.UnregisterCallback<ITwinnyMobileCallbacks>(this);
        }

        public void ShowFloorsAtOrAbove(Floor selectedFloor)
        {
            if (_floors == null || _floors.Length == 0)
                return;

            int selectedIndex = GetFloorIndex(selectedFloor);
            if (selectedIndex < 0)
                return;

            for (int i = 0; i < _floors.Length; i++)
            {
                BuildingFloorEntry entry = _floors[i];
                if (entry == null)
                    continue;

                entry.SetVisible(i >= selectedIndex);
            }
        }

        public void ShowAllFloors()
        {
            if (_floors == null)
                return;

            for (int i = 0; i < _floors.Length; i++)
            {
                BuildingFloorEntry entry = _floors[i];
                if (entry == null)
                    continue;

                entry.SetVisible(true);
            }
        }

        private int GetFloorIndex(Floor floor)
        {
            if (floor == null || _floors == null)
                return -1;

            for (int i = 0; i < _floors.Length; i++)
            {
                BuildingFloorEntry entry = _floors[i];
                if (entry != null && entry.Matches(floor))
                    return i;
            }

            return -1;
        }

        public void OnFloorSelected(Floor floor)
        {
            ShowFloorsAtOrAbove(floor);
        }

        public void OnFloorUnselected(Floor floor)
        {
            if (_showAllFloorsWhenUnselected && GetFloorIndex(floor) >= 0)
                ShowAllFloors();
        }

        public void OnPlatformInitializing() { }
        public void OnPlatformInitialized() { }
        public void OnExperienceReady() { }
        public void OnExperienceStarting() { }
        public void OnExperienceStarted() { }
        public void OnExperienceEnding() { }
        public void OnExperienceEnded(bool isRunning) { }
        public void OnSceneLoadStart(string sceneName) { }
        public void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene) { }
        public void OnTeleportToLandMark(int landMarkIndex) { }
        public void OnSkyboxHDRIChanged(Material material) { }
        public void OnStartInteract(GameObject gameObject) { }
        public void OnStopInteract(GameObject gameObject) { }
        public void OnStartTeleport() { }
        public void OnTeleport() { }
        public void OnExperienceLoaded() { }
        public void OnEnterImmersiveMode() { }
        public void OnExitImmersiveMode() { }
        public void OnEnterMockupMode() { }
        public void OnExitMockupMode() { }
        public void OnEnterDemoMode() { }
        public void OnExitDemoMode() { }
        public void OnFloorFocused(Floor floor) { }
    }
}
