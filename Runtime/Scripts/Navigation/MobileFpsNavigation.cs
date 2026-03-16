using System;
using Concept.Core;
using Twinny.Core.Input;
using Twinny.Mobile.Interactables;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Twinny.Mobile.Navigation
{
    /// <summary>
    /// Moves a NavMeshAgent to tapped positions and optionally reports interactable hits.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class MobileFpsNavigation : MonoBehaviour, IMobileInputCallbacks, ITwinnyMobileCallbacks
    {
        private static int s_activeInstanceCount;

        public static bool HasActiveInstance => s_activeInstanceCount > 0;

        [Header("Navigation")]
        [SerializeField] private NavMeshAgent _agent;
        [SerializeField] private float _maxSampleDistance = 3f;
        [SerializeField] private int _navMeshAreaMask = NavMesh.AllAreas;

        [Header("Raycast")]
        [SerializeField] private LayerMask _interactableMask;

        [Header("Visuals")]
        [SerializeField] private GameObject _targetDecalPrefab;

        /// <summary>
        /// Fired when a valid NavMesh destination is chosen.
        /// </summary>
        public event Action<Vector3> OnNavMeshClick;

        /// <summary>
        /// Fired when an interactable is clicked (based on layer mask).
        /// </summary>
        public event Action<Transform> OnInteractableClick;

        private bool _isModeActive;
        private GameObject _currentDecal;

        private void OnEnable()
        {
            s_activeInstanceCount++;
            EnsureReferences();
            CallbackHub.RegisterCallback<IMobileInputCallbacks>(this);
            CallbackHub.RegisterCallback<ITwinnyMobileCallbacks>(this);
        }

        private void OnDisable()
        {
            s_activeInstanceCount = Mathf.Max(0, s_activeInstanceCount - 1);
            CallbackHub.UnregisterCallback<IMobileInputCallbacks>(this);
            CallbackHub.UnregisterCallback<ITwinnyMobileCallbacks>(this);
        }

        private void OnValidate()
        {
            EnsureReferences();
            if (_maxSampleDistance < 0f) _maxSampleDistance = 0f;
        }

        private void Update()
        {
            if (_currentDecal != null && _agent != null && !_agent.pathPending)
            {
                if (_agent.remainingDistance <= _agent.stoppingDistance)
                {
                    Destroy(_currentDecal);
                    _currentDecal = null;
                }
            }
        }

        public void OnSelect(SelectionData selection)
        {
            if (!_isModeActive) return;
            if (_agent == null)
            {
                Debug.LogWarning("[MobileFpsNavigation] NavMeshAgent is null.");
                return;
            }
            if (!selection.HasHit) return;

            RaycastHit hit = selection.Hit;

            Debug.Log(
                $"[MobileFpsNavigation] OnSelect {hit.collider.name} at {hit.point} " +
                $"(agent enabled={_agent.enabled}, onNavMesh={_agent.isOnNavMesh})"
            );

            if (TryMoveTo(hit.point))
                return;

            if (IsInteractable(hit.transform))
            {
                Debug.Log($"[MobileFpsNavigation] Interactable hit {hit.transform.name}");
                OnInteractableClick?.Invoke(hit.transform);
            }
        }

        public void OnPrimaryDown(float x, float y) { }
        public void OnPrimaryUp(float x, float y) { }
        public void OnPrimaryDrag(float dx, float dy) { }
        public void OnCancel() { }
        public void OnZoom(float delta) { }
        public void OnTwoFingerTap(Vector2 position) { }
        public void OnTwoFingerLongPress(Vector2 position) { }
        public void OnTwoFingerSwipe(Vector2 direction, Vector2 startPosition) { }
        public void OnThreeFingerTap(Vector2 position) { }
        public void OnThreeFingerSwipe(Vector2 direction, Vector2 startPosition) { }
        public void OnThreeFingerPinch(float delta) { }
        public void OnFourFingerTap() { }
        public void OnFourFingerSwipe(Vector2 direction) { }
        public void OnEdgeSwipe(EdgeDirection edge) { }
        public void OnForceTouch(float pressure) { }
        public void OnHapticTouch() { }
        public void OnBackTap(int tapCount) { }
        public void OnShake() { }
        public void OnTilt(Vector3 tiltRotation) { }
        public void OnDeviceRotated(DeviceOrientation orientation) { }
        public void OnPickUp() { }
        public void OnPutDown() { }
        public void OnAccessibilityAction(string actionName) { }
        public void OnScreenReaderGesture(string gestureType) { }
        public void OnNotificationAction(bool isQuickAction) { }

        public void OnStartInteract(GameObject gameObject) { }
        public void OnStopInteract(GameObject gameObject) { }
        public void OnStartTeleport() { }
        public void OnTeleport() { }

        public void OnPlatformInitializing() { }
        public void OnPlatformInitialized() { }
        public void OnExperienceReady() { }
        public void OnExperienceStarting() { }
        public void OnExperienceStarted() { }
        public void OnExperienceEnding() { }
        public void OnExperienceEnded(bool isRunning) { }
        public void OnExperienceLoaded() { }
        public void OnSceneLoadStart(string sceneName) { }
        public void OnSceneLoaded(Scene scene) { }
        public void OnTeleportToLandMark(int landMarkIndex) { }
        public void OnSkyboxHDRIChanged(Material material) { }

        public void OnEnterImmersiveMode() => _isModeActive = true;
        public void OnExitImmersiveMode() => _isModeActive = false;
        public void OnEnterMockupMode() { }
        public void OnExitMockupMode() { }
        public void OnEnterDemoMode() { }
        public void OnExitDemoMode() { }
        public void OnFloorSelected(Floor floor) { }
        public void OnFloorFocused(Floor floor) { }
        public void OnFloorUnselected(Floor floor) { }


        private bool IsInteractable(Transform target)
        {
            if (target == null) return false;
            if (_interactableMask == 0) return false;
            int layerMask = 1 << target.gameObject.layer;
            return (_interactableMask.value & layerMask) != 0;
        }

        private bool TryMoveTo(Vector3 worldPos)
        {
            if (!NavMesh.SamplePosition(worldPos, out NavMeshHit navHit, _maxSampleDistance, _navMeshAreaMask))
            {
                Debug.Log($"[MobileFpsNavigation] NavMesh.SamplePosition failed at {worldPos}");
                return false;
            }

            Debug.Log(
                $"[MobileFpsNavigation] Moving to {navHit.position} " +
                $"(isStopped={_agent.isStopped}, speed={_agent.speed}, remaining={_agent.remainingDistance})"
            );
            if (_agent.isStopped) _agent.isStopped = false;
            _agent.SetDestination(navHit.position);

            if (_currentDecal != null) Destroy(_currentDecal);
            if (_targetDecalPrefab != null)
            {
                _currentDecal = Instantiate(_targetDecalPrefab, navHit.position, _targetDecalPrefab.transform.rotation);
            }

            OnNavMeshClick?.Invoke(navHit.position);
            return true;
        }

        private void EnsureReferences()
        {
            if (_agent == null)
                _agent = GetComponent<NavMeshAgent>();
        }
    }
}
