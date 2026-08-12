using System;
using UnityEngine;

/// <summary>
/// 提供基于 Unity 原生 API 的常用扩展方法。
/// </summary>
public static class UnityExtension
{
    /// <summary>
    /// 获取指定组件；组件不存在时立即添加。
    /// </summary>
    /// <typeparam name="T">组件类型。</typeparam>
    /// <param name="gameObject">目标对象。</param>
    /// <returns>已有或新添加的组件。</returns>
    public static T GetOrAddComponent<T>(this GameObject gameObject) where T : Component
    {
        if (gameObject == null)
        {
            throw new ArgumentNullException(nameof(gameObject));
        }

        return gameObject.TryGetComponent(out T component)
            ? component
            : gameObject.AddComponent<T>();
    }

    /// <summary>
    /// 获取指定类型的组件；组件不存在时立即添加。
    /// </summary>
    /// <param name="gameObject">目标对象。</param>
    /// <param name="type">组件类型。</param>
    /// <returns>已有或新添加的组件。</returns>
    public static Component GetOrAddComponent(this GameObject gameObject, Type type)
    {
        if (gameObject == null)
        {
            throw new ArgumentNullException(nameof(gameObject));
        }

        if (type == null || !typeof(Component).IsAssignableFrom(type))
        {
            throw new ArgumentException("Type must derive from UnityEngine.Component.", nameof(type));
        }

        Component component = gameObject.GetComponent(type);
        return component != null ? component : gameObject.AddComponent(type);
    }

    /// <summary>
    /// 判断对象是否属于一个有效且已经加载的场景。
    /// </summary>
    /// <param name="gameObject">目标对象。</param>
    /// <returns>对象是否属于已加载场景。</returns>
    public static bool InScene(this GameObject gameObject)
    {
        return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
    }

    /// <summary>
    /// 将对象及其所有子对象设置到指定层级。
    /// </summary>
    /// <param name="gameObject">层级根对象。</param>
    /// <param name="layer">Unity 层级编号，范围为 0～31。</param>
    public static void SetLayerRecursively(this GameObject gameObject, int layer)
    {
        if (gameObject == null)
        {
            throw new ArgumentNullException(nameof(gameObject));
        }

        if (layer < 0 || layer > 31)
        {
            throw new ArgumentOutOfRangeException(nameof(layer), layer, "Unity layer must be between 0 and 31.");
        }

        Transform[] transforms = gameObject.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            transforms[i].gameObject.layer = layer;
        }
    }

    /// <summary>
    /// 将 Vector3 的 XZ 平面分量转换为 Vector2。
    /// </summary>
    /// <param name="value">待转换的三维向量。</param>
    /// <returns>由 XZ 分量组成的二维向量。</returns>
    public static Vector2 ToVector2(this Vector3 value)
    {
        return new Vector2(value.x, value.z);
    }

    /// <summary>
    /// 将 Vector2 映射到 Vector3 的 XZ 平面。
    /// </summary>
    /// <param name="value">待转换的二维向量。</param>
    /// <returns>Y 分量为零的三维向量。</returns>
    public static Vector3 ToVector3(this Vector2 value)
    {
        return value.ToVector3(0f);
    }

    /// <summary>
    /// 将 Vector2 映射到 Vector3 的 XZ 平面，并指定 Y 分量。
    /// </summary>
    /// <param name="value">待转换的二维向量。</param>
    /// <param name="y">三维向量的 Y 分量。</param>
    /// <returns>转换后的三维向量。</returns>
    public static Vector3 ToVector3(this Vector2 value, float y)
    {
        return new Vector3(value.x, y, value.y);
    }

    /// <summary>设置世界坐标的 X 分量。</summary>
    /// <param name="transform">目标 Transform。</param>
    /// <param name="value">新的 X 分量。</param>
    public static void SetPositionX(this Transform transform, float value)
    {
        transform.position = WithX(transform.position, value);
    }

    /// <summary>设置世界坐标的 Y 分量。</summary>
    /// <param name="transform">目标 Transform。</param>
    /// <param name="value">新的 Y 分量。</param>
    public static void SetPositionY(this Transform transform, float value)
    {
        transform.position = WithY(transform.position, value);
    }

    /// <summary>设置世界坐标的 Z 分量。</summary>
    /// <param name="transform">目标 Transform。</param>
    /// <param name="value">新的 Z 分量。</param>
    public static void SetPositionZ(this Transform transform, float value)
    {
        transform.position = WithZ(transform.position, value);
    }

    /// <summary>增加世界坐标的 X 分量。</summary>
    /// <param name="transform">目标 Transform。</param>
    /// <param name="value">X 分量增量。</param>
    public static void AddPositionX(this Transform transform, float value)
    {
        transform.position += new Vector3(value, 0f, 0f);
    }

    /// <summary>增加世界坐标的 Y 分量。</summary>
    /// <param name="transform">目标 Transform。</param>
    /// <param name="value">Y 分量增量。</param>
    public static void AddPositionY(this Transform transform, float value)
    {
        transform.position += new Vector3(0f, value, 0f);
    }

    /// <summary>增加世界坐标的 Z 分量。</summary>
    /// <param name="transform">目标 Transform。</param>
    /// <param name="value">Z 分量增量。</param>
    public static void AddPositionZ(this Transform transform, float value)
    {
        transform.position += new Vector3(0f, 0f, value);
    }

    /// <summary>设置本地坐标的 X 分量。</summary>
    /// <param name="transform">目标 Transform。</param>
    /// <param name="value">新的 X 分量。</param>
    public static void SetLocalPositionX(this Transform transform, float value)
    {
        transform.localPosition = WithX(transform.localPosition, value);
    }

    /// <summary>设置本地坐标的 Y 分量。</summary>
    /// <param name="transform">目标 Transform。</param>
    /// <param name="value">新的 Y 分量。</param>
    public static void SetLocalPositionY(this Transform transform, float value)
    {
        transform.localPosition = WithY(transform.localPosition, value);
    }

    /// <summary>设置本地坐标的 Z 分量。</summary>
    /// <param name="transform">目标 Transform。</param>
    /// <param name="value">新的 Z 分量。</param>
    public static void SetLocalPositionZ(this Transform transform, float value)
    {
        transform.localPosition = WithZ(transform.localPosition, value);
    }

    /// <summary>增加本地坐标的 X 分量。</summary>
    /// <param name="transform">目标 Transform。</param>
    /// <param name="value">X 分量增量。</param>
    public static void AddLocalPositionX(this Transform transform, float value)
    {
        transform.localPosition += new Vector3(value, 0f, 0f);
    }

    /// <summary>增加本地坐标的 Y 分量。</summary>
    /// <param name="transform">目标 Transform。</param>
    /// <param name="value">Y 分量增量。</param>
    public static void AddLocalPositionY(this Transform transform, float value)
    {
        transform.localPosition += new Vector3(0f, value, 0f);
    }

    /// <summary>增加本地坐标的 Z 分量。</summary>
    /// <param name="transform">目标 Transform。</param>
    /// <param name="value">Z 分量增量。</param>
    public static void AddLocalPositionZ(this Transform transform, float value)
    {
        transform.localPosition += new Vector3(0f, 0f, value);
    }

    /// <summary>设置本地缩放的 X 分量。</summary>
    /// <param name="transform">目标 Transform。</param>
    /// <param name="value">新的 X 分量。</param>
    public static void SetLocalScaleX(this Transform transform, float value)
    {
        transform.localScale = WithX(transform.localScale, value);
    }

    /// <summary>设置本地缩放的 Y 分量。</summary>
    /// <param name="transform">目标 Transform。</param>
    /// <param name="value">新的 Y 分量。</param>
    public static void SetLocalScaleY(this Transform transform, float value)
    {
        transform.localScale = WithY(transform.localScale, value);
    }

    /// <summary>设置本地缩放的 Z 分量。</summary>
    /// <param name="transform">目标 Transform。</param>
    /// <param name="value">新的 Z 分量。</param>
    public static void SetLocalScaleZ(this Transform transform, float value)
    {
        transform.localScale = WithZ(transform.localScale, value);
    }

    /// <summary>增加本地缩放的 X 分量。</summary>
    /// <param name="transform">目标 Transform。</param>
    /// <param name="value">X 分量增量。</param>
    public static void AddLocalScaleX(this Transform transform, float value)
    {
        transform.localScale += new Vector3(value, 0f, 0f);
    }

    /// <summary>增加本地缩放的 Y 分量。</summary>
    /// <param name="transform">目标 Transform。</param>
    /// <param name="value">Y 分量增量。</param>
    public static void AddLocalScaleY(this Transform transform, float value)
    {
        transform.localScale += new Vector3(0f, value, 0f);
    }

    /// <summary>增加本地缩放的 Z 分量。</summary>
    /// <param name="transform">目标 Transform。</param>
    /// <param name="value">Z 分量增量。</param>
    public static void AddLocalScaleZ(this Transform transform, float value)
    {
        transform.localScale += new Vector3(0f, 0f, value);
    }

    /// <summary>
    /// 在 XZ 平面上朝向指定二维坐标。
    /// </summary>
    /// <param name="transform">目标 Transform。</param>
    /// <param name="target">目标点的 XZ 坐标。</param>
    public static void LookAt2D(this Transform transform, Vector2 target)
    {
        Vector3 position = transform.position;
        Vector3 direction = new Vector3(target.x - position.x, 0f, target.y - position.z);
        if (direction.sqrMagnitude > Mathf.Epsilon)
        {
            transform.forward = direction.normalized;
        }
    }

    private static Vector3 WithX(Vector3 value, float x)
    {
        value.x = x;
        return value;
    }

    private static Vector3 WithY(Vector3 value, float y)
    {
        value.y = y;
        return value;
    }

    private static Vector3 WithZ(Vector3 value, float z)
    {
        value.z = z;
        return value;
    }
}
