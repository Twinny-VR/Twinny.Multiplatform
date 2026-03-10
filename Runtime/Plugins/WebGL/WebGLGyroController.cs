using UnityEngine;
using System.Runtime.InteropServices;

// Agora é uma classe estática pura, sem herdar de MonoBehaviour
public static class WebGLGyroAPI
{
    [DllImport("__Internal")]
    private static extern void InitGyroscope();

    [DllImport("__Internal")]
    private static extern float GetGyroAlpha();

    [DllImport("__Internal")]
    private static extern float GetGyroBeta();

    [DllImport("__Internal")]
    private static extern float GetGyroGamma();

    [DllImport("__Internal")]
    private static extern int GetGyroHasData();

    [DllImport("__Internal")]
    private static extern int GetGyroPermissionState();

    private static bool _requestedPermission;

    // 0 = not requested/unknown, 1 = granted, 2 = denied, 3 = unsupported, 4 = error
    public static int PermissionState
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!_requestedPermission) return 0;
            return GetGyroPermissionState();
#else
            return 0;
#endif
        }
    }

    // Initialized means the browser granted access and at least one sensor event arrived.
    public static bool IsInitialized
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!_requestedPermission) return false;
            return PermissionState == 1 && GetGyroHasData() == 1;
#else
            return false;
#endif
        }
    }

    // O seu CallbackHub vai chamar esse método aqui
    public static void RequestGyroPermission()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        _requestedPermission = true;
        InitGyroscope();
#endif
    }

    // Qualquer script do seu jogo pode chamar isso para pegar a rotação atual
    public static Quaternion GetRotation()
    {
        if (!IsInitialized)
            return Quaternion.identity; // Retorna rotação zerada se não inicializou

#if UNITY_WEBGL && !UNITY_EDITOR
        float alpha = GetGyroAlpha();
        float beta = GetGyroBeta();
        float gamma = GetGyroGamma();

        return ConvertBrowserRotation(alpha, beta, gamma);
#else
        return Quaternion.identity;
#endif
    }

    private static Quaternion ConvertBrowserRotation(float alpha, float beta, float gamma)
    {
        // Browser deviceorientation uses a portrait-centric frame:
        // alpha = Z, beta = X, gamma = Y.
        // Our mobile experience runs primarily in landscape, so pitch/roll need
        // to be remapped or horizontal motion is interpreted as vertical and vice-versa.
        bool isLandscape = Screen.width > Screen.height;

        float pitch = isLandscape ? gamma : beta;
        float yaw = -alpha;
        float roll = isLandscape ? -beta : -gamma;

        return Quaternion.Euler(pitch, yaw, roll);
    }
}
