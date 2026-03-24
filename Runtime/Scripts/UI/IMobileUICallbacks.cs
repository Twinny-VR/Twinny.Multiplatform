using Twinny.Mobile.Interactables;

namespace Twinny.Mobile
{
    /// <summary>
    /// Interface for UI-driven actions in the mobile experience.
    /// </summary>
    public interface IMobileUICallbacks
    {
        /// <summary>
        /// Called when the user requests immersive mode from the UI.
        /// </summary>
        void OnImmersiveRequested(FloorData data = null);

        /// <summary>
        /// Called when the user requests mockup mode from the UI.
        /// </summary>
        void OnMockupRequested(FloorData data = null);

        /// <summary>
        /// Called when the user requests to start an experience.
        /// </summary>
        void OnStartExperienceRequested(string sceneName = "");


        /// <summary>
        /// Called when the loading progress changes (0-1).
        /// </summary>
        void OnLoadingProgressChanged(float progress);

        /// <summary>
        /// Called when the gyroscope toggle changes.
        /// </summary>
        /// <param name="enabled">True if gyroscope should be enabled.</param>
        void OnGyroscopeToggled(bool enabled);

        void OnMaxWallHeightRequested(float height);
    }
}
