using System.Runtime.InteropServices;
using UnityEngine;

namespace Breathe.Input
{
    /// <summary>
    /// Bridge to the WebGL microphone API implemented in BreatheWebGL.jslib.
    /// Uses the Web Audio API (getUserMedia + AnalyserNode) to capture microphone
    /// input and compute RMS amplitude, since Unity's Microphone class isn't
    /// available in WebGL builds.
    /// </summary>
    public static class WebGLMicrophoneBridge
    {
        public enum PermissionState
        {
            Unknown = 0,
            Requesting = 1,
            Granted = 2,
            Denied = 3
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int BreatheWebGL_StartMicrophone();

        [DllImport("__Internal")]
        private static extern void BreatheWebGL_StopMicrophone();

        [DllImport("__Internal")]
        private static extern float BreatheWebGL_GetMicAmplitude();

        [DllImport("__Internal")]
        private static extern int BreatheWebGL_IsMicRecording();

        [DllImport("__Internal")]
        private static extern int BreatheWebGL_GetMicPermissionState();

        [DllImport("__Internal")]
        private static extern System.IntPtr BreatheWebGL_GetMicErrorMessage();

        [DllImport("__Internal")]
        private static extern int BreatheWebGL_IsMicSupported();

        public static bool IsSupported => BreatheWebGL_IsMicSupported() == 1;
        public static bool IsRecording => BreatheWebGL_IsMicRecording() == 1;
        public static float Amplitude => BreatheWebGL_GetMicAmplitude();
        public static PermissionState Permission => (PermissionState)BreatheWebGL_GetMicPermissionState();

        public static string ErrorMessage
        {
            get
            {
                var ptr = BreatheWebGL_GetMicErrorMessage();
                return ptr != System.IntPtr.Zero ? Marshal.PtrToStringUTF8(ptr) : "";
            }
        }

        public static bool StartMicrophone()
        {
            return BreatheWebGL_StartMicrophone() == 1;
        }

        public static void StopMicrophone()
        {
            BreatheWebGL_StopMicrophone();
        }
#else
        // Editor/standalone stubs — these should never be called in production
        // since MicBreathInput uses the native Unity Microphone API there.
        public static bool IsSupported => false;
        public static bool IsRecording => false;
        public static float Amplitude => 0f;
        public static PermissionState Permission => PermissionState.Unknown;
        public static string ErrorMessage => "WebGL microphone bridge only works in WebGL builds";

        public static bool StartMicrophone()
        {
            Debug.LogWarning("[WebGLMicrophoneBridge] StartMicrophone called outside WebGL — use Unity's Microphone API instead");
            return false;
        }

        public static void StopMicrophone()
        {
            Debug.LogWarning("[WebGLMicrophoneBridge] StopMicrophone called outside WebGL");
        }
#endif
    }
}
