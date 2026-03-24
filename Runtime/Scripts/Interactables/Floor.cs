using Concept.Core;
using System;
using Twinny.Shaders;
using UnityEngine;
using UnityEngine.Events;

namespace Twinny.Mobile.Interactables
{

    [Serializable]
    public class FloorData
    {
        public enum FloorSceneOpenMode
        {
            Immersive = 0,
            Mockup = 1
        }

        [Header("Identity")]
        [field: SerializeField] public string Title { get; set; } = "Pavement";
        [field: SerializeField] public string Subtitle { get; set; } = "Floor";
        [field: SerializeField] public string ImmersionSceneName { get; set; }
        public bool HasImmersionScene => !string.IsNullOrWhiteSpace(ImmersionSceneName);

        [field: SerializeField] public FloorSceneOpenMode SceneOpenMode { get; set; } = FloorSceneOpenMode.Immersive;
        [field: SerializeField] public bool UseLandMark { get; set; }
        [field: SerializeField] public string LandmarkGuid { get; set; }



    }

    public abstract class Floor : MonoBehaviour
    {
        public static event Action<Floor> Selected;
        public static event Action<Floor> Focused;
        public static event Action<Floor> Unselected;

        [Header("Identity")]
        [SerializeField] private FloorData _data = new();
        public FloorData Data => _data ??= new FloorData();

        /*
        [SerializeField] private float _maxWallHeight = 3f;
        public float MaxWallHeight => _maxWallHeight;
        */
        [SerializeField] private bool _applyAlphaClip;
        [SerializeField] private float _alphaClipHeight = 3f;
        [SerializeField] private bool _requestOnSelect;

        public bool ApplyAlphaClip => _applyAlphaClip;
        public float AlphaClipHeight => _alphaClipHeight;
        public bool RequestOnSelect => _requestOnSelect;

        [SerializeField] private bool _showHint = true;
        public bool ShowHint => _showHint;


        [Header("Events")]
        [SerializeField] private UnityEvent _onSelect;
        [SerializeField] private UnityEvent _onFocused;
        [SerializeField] private UnityEvent _onUnselect;



        public virtual void Select()
        {
            float targetCutoffHeight = _applyAlphaClip
                ? _alphaClipHeight
                : AlphaClipper.MinMaxWallHeight.y;

            AlphaClipper.TransitionCutoffHeight(targetCutoffHeight);

            _onSelect?.Invoke();
            Selected?.Invoke(this);
            CallbackHub.CallAction<ITwinnyMobileCallbacks>(callback => callback.OnFloorSelected(this));

            if (_requestOnSelect && !string.IsNullOrWhiteSpace(Data.ImmersionSceneName))
                Request();
        }

        public virtual void Focus()
        {
            _onFocused?.Invoke();
            Focused?.Invoke(this);
            CallbackHub.CallAction<ITwinnyMobileCallbacks>(callback => callback.OnFloorFocused(this));
        }

        public virtual void Unselect()
        {
            _onUnselect?.Invoke();
            Unselected?.Invoke(this);
            CallbackHub.CallAction<ITwinnyMobileCallbacks>(callback => callback.OnFloorUnselected(this));

        }

        public virtual void Request() => TwinnyMobileManager.SceneRequest(_data);
        

    }
}
