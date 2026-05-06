using System.Collections;
using UnityEngine;

namespace Twinny.Multiplatform.Env
{
    /// <summary>
    /// Smoothly toggles a global shader blend factor between 0 and 1 on demand.
    /// </summary>
    public static class SkyboxHandler
    {
        private const string m_defaultPropertyName = "_blendFactor";
        private const float m_defaultDurationSeconds = 0f;

        private static SkyboxHandlerRunner _runner;
        private static bool _hasOriginal;
        private static float _originalValue;

#if UNITY_EDITOR
        static SkyboxHandler()
        {
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }
#endif

        /// <summary>
        /// Toggles the global blend factor between 0 and 1 with a smooth transition.
        /// </summary>
        public static void SwitchSkybox() => SwitchSkybox(m_defaultDurationSeconds);
        

        /// <summary>
        /// Toggles the global blend factor between 0 and 1 using the given speed (seconds).
        /// </summary>
        public static void SwitchSkybox(float speed)
        {
            EnsureRunner();
            _runner.ToggleBlend(m_defaultPropertyName, Mathf.Max(0f, speed));
        }
        public static void SwitchSkybox(int factor) => SwitchSkybox(m_defaultDurationSeconds, factor);

        /// <summary>
        /// Forces the global blend factor to 0 or 1 using the given speed (seconds).
        /// </summary>
        public static void SwitchSkybox(float speed, float factor)
        {
            EnsureRunner();
            float target = Mathf.Clamp01(factor);
            _runner.BlendTo(m_defaultPropertyName, target, Mathf.Max(0f, speed));
        }

        private static void EnsureRunner()
        {
            if (_runner != null) return;

            var go = new GameObject("[SkyboxHandler]");
            go.hideFlags = HideFlags.HideAndDontSave;
            Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<SkyboxHandlerRunner>();
        }

        private sealed class SkyboxHandlerRunner : MonoBehaviour
        {
            private Coroutine _blendRoutine;

            public void ToggleBlend(string property, float duration)
            {
                CacheOriginalValue(property);
                float current = Shader.GetGlobalFloat(property);
                float target = current >= 0.5f ? 0f : 1f;

                if (_blendRoutine != null)
                    StopCoroutine(_blendRoutine);

                _blendRoutine = StartCoroutine(BlendRoutine(property, current, target, duration));
            }

            public void BlendTo(string property, float target, float duration)
            {
                CacheOriginalValue(property);
                float current = Shader.GetGlobalFloat(property);

                if (_blendRoutine != null)
                    StopCoroutine(_blendRoutine);

                _blendRoutine = StartCoroutine(BlendRoutine(property, current, target, duration));
            }

            private IEnumerator BlendRoutine(string property, float from, float to, float duration)
            {
                if (duration <= 0f)
                {
                    Shader.SetGlobalFloat(property, to);
                    yield break;
                }

                float t = 0f;
                while (t < duration)
                {
                    float alpha = t / duration;
                    float smooth = Mathf.SmoothStep(0f, 1f, alpha);
                    Shader.SetGlobalFloat(property, Mathf.Lerp(from, to, smooth));
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }

                Shader.SetGlobalFloat(property, to);
            }
        }

        private static void CacheOriginalValue(string property)
        {
            if (_hasOriginal) return;
            _originalValue = Shader.GetGlobalFloat(property);
            _hasOriginal = true;
        }

#if UNITY_EDITOR
        private static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state != UnityEditor.PlayModeStateChange.ExitingPlayMode) return;
            if (!_hasOriginal) return;
            Shader.SetGlobalFloat(m_defaultPropertyName, _originalValue);
            _hasOriginal = false;
        }
#endif
    }
}
