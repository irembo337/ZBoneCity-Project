using UnityEngine;

namespace BonelabAdvancedHealth
{
    public static class PlatformCompatibility
    {
        public static bool IsAndroidRuntime
        {
            get
            {
#if UNITY_ANDROID
                return true;
#else
                return Application.platform == RuntimePlatform.Android;
#endif
            }
        }

        public static bool IsStandaloneRuntime
        {
            get
            {
#if UNITY_STANDALONE
                return true;
#else
                return Application.platform == RuntimePlatform.WindowsPlayer ||
                       Application.platform == RuntimePlatform.WindowsEditor ||
                       Application.platform == RuntimePlatform.LinuxPlayer ||
                       Application.platform == RuntimePlatform.OSXPlayer;
#endif
            }
        }

        public static bool SupportsMelonHarmonyRuntime => !IsAndroidRuntime;
        public static bool SupportsExternalDllScanning => !IsAndroidRuntime;
        public static bool SupportsRuntimeMedicalGripInjection => !IsAndroidRuntime;
        public static bool SupportsFileAudioOverrides => !IsAndroidRuntime;

        public static string RuntimeLabel => IsAndroidRuntime ? "Quest/Android" : "PCVR/Standalone";
    }
}
