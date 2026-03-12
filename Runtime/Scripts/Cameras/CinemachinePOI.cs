using UnityEngine;

namespace Twinny.Mobile.Cameras
{
    public class CinemachinePOI : MonoBehaviour
    {
        [Header("Focus")]
        [SerializeField] private float _targetRadius = 0f;
        [SerializeField] private Vector2 _radiusLimits;

        [Header("Orbital Overrides")]
        [SerializeField] private bool _overrideRotation;
        [SerializeField] private float _targetPan;
        [SerializeField] private float _targetTilt;

        [SerializeField] private Vector2 _verticalAxisLimits;
        [SerializeField] private float _maxPanDistance;
        [SerializeField] private int _enablePanLimit;
        [SerializeField] private bool _overridePanConstraint;
        [SerializeField] private bool _lockPanX;
        [SerializeField] private bool _lockPanY;
        [SerializeField] private bool _lockPanZ;
        [SerializeField] private bool _overrideDeoccluder;
        [SerializeField] private float _overrideDeoccluderRadius = 100f;

        [Header("Demo Mode")]
        [SerializeField] private bool _avoidDemoMode;
        [SerializeField] private float _demoIdleSecondsOverride;

        public float TargetRadius => _targetRadius;
        public bool OverrideRotation => _overrideRotation;
        public float TargetPan => _targetPan;
        public float TargetTilt => _targetTilt;
        public Vector2 VerticalAxisLimits => _verticalAxisLimits;
        public Vector2 RadiusLimits => _radiusLimits;
        public float MaxPanDistance => _maxPanDistance;
        public int EnablePanLimit => _enablePanLimit;
        public bool OverridePanConstraint => _overridePanConstraint;
        public bool LockPanX => _lockPanX;
        public bool LockPanY => _lockPanY;
        public bool LockPanZ => _lockPanZ;
        public bool OverrideDeoccluder => _overrideDeoccluder;
        public float OverrideDeoccluderRadius => _overrideDeoccluderRadius;
        public bool AvoidDemoMode => _avoidDemoMode;
        public float DemoIdleSecondsOverride => _demoIdleSecondsOverride;

        public bool HasTargetPanOverride => _overrideRotation;
        public bool HasTargetTiltOverride => _overrideRotation;
        public bool HasVerticalAxisLimitsOverride => _verticalAxisLimits != Vector2.zero;
        public bool HasRadiusLimitsOverride => _radiusLimits != Vector2.zero;
        public bool HasMaxPanDistanceOverride => !Mathf.Approximately(_maxPanDistance, 0f);
        public bool HasEnablePanLimitOverride => _enablePanLimit != 0;
        public bool HasPanConstraintOverride => _overridePanConstraint;
        public bool HasDeoccluderRadiusOverride => _overrideDeoccluder && !Mathf.Approximately(_overrideDeoccluderRadius, 0f);
        public bool HasDemoIdleSecondsOverride => !Mathf.Approximately(_demoIdleSecondsOverride, 0f);

        public bool EnablePanLimitValue => _enablePanLimit > 0;
    }
}
