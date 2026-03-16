using Twinny.Core;
using Twinny.Mobile.Interactables;
using UnityEngine;

namespace Twinny.Mobile
{
    /// <summary>
    /// Project-level callbacks for mobile gameplay actions and mode changes.
    /// </summary>
    public interface ITwinnyMobileCallbacks : ICallbacks
    {
        /// <summary>
        /// Called when an interactable starts being interacted with.
        /// </summary>
        void OnStartInteract(GameObject gameObject);

        /// <summary>
        /// Called when an interactable stops being interacted with.
        /// </summary>
        void OnStopInteract(GameObject gameObject);

        /// <summary>
        /// Called when a teleport action starts.
        /// </summary>
        void OnStartTeleport();

        /// <summary>
        /// Called when a teleport action completes.
        /// </summary>
        void OnTeleport();

        /// <summary>
        /// Called when the experience scene has finished loading.
        /// </summary>
        void OnExperienceLoaded();


        /// <summary>
        /// Called when the immersive mode becomes active.
        /// </summary>
        void OnEnterImmersiveMode();
        void OnExitImmersiveMode();

        /// <summary>
        /// Called when the mockup mode becomes active.
        /// </summary>
        void OnEnterMockupMode();
        void OnExitMockupMode();

        /// <summary>
        /// Called when automatic camera demo mode becomes active/inactive.
        /// </summary>
        void OnEnterDemoMode();
        void OnExitDemoMode();
        void OnFloorFocused(Floor floor);
        void OnFloorSelected(Floor floor);
        void OnFloorUnselected(Floor floor);
    }
}
