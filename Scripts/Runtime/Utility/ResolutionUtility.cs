using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>桌面或运行时查看器的分辨率辅助。</summary>
    public static class ResolutionUtility
    {
        public static Resolution[] GetSupportedResolutions()
        {
            return Screen.resolutions;
        }

        public static void SetResolution(int width, int height, FullScreenMode mode)
        {
            if (width <= 0 || height <= 0)
            {
                return;
            }

            Screen.SetResolution(width, height, mode);
        }
    }
}
