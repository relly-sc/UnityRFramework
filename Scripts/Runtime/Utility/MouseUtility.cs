using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>鼠标位置、可见性和锁定状态辅助。</summary>
    public static class MouseUtility
    {
        public static Vector3 Position => Input.mousePosition;

        public static bool IsButtonDown(int button)
        {
            return Input.GetMouseButtonDown(button);
        }

        public static bool IsButtonUp(int button)
        {
            return Input.GetMouseButtonUp(button);
        }

        public static void SetVisible(bool visible)
        {
            Cursor.visible = visible;
        }

        public static void SetLockState(CursorLockMode lockMode)
        {
            Cursor.lockState = lockMode;
        }
    }
}
