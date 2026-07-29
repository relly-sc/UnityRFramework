using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Demo UI 构建原语。封装 UGUI 元素的快速创建，供各面板组件复用。
/// 同时支持运行时动态构建与 Prefab 绑定两种模式：
/// 找不到同名子对象时创建，找到时直接复用。
/// </summary>
public static class DemoUIHelper
{
    private const int MinimumFontSize = 25;

    private static UnityEngine.Font cachedFont;

    /// <summary>
    /// 内置默认 UGUI 字体，避免引用缺失导致文字不显示。
    /// 优先加载 Resources/Fonts 下的 NotoSansSC（正确显示中文），
    /// 缺失时回退到 Unity 内置 Arial。
    /// </summary>
    private static UnityEngine.Font DefaultFont
    {
        get
        {
            if (cachedFont == null)
            {
                cachedFont = Resources.Load<UnityEngine.Font>("Fonts/NotoSansSC-Regular");
                if (cachedFont == null)
                {
                    cachedFont = Resources.GetBuiltinResource<UnityEngine.Font>("LegacyRuntime.ttf");
                }
            }
            return cachedFont;
        }
    }

    /// <summary>
    /// 查找或创建带背景色的面板（Image）。
    /// </summary>
    public static RectTransform FindOrMakePanel(Transform parent, string name, Color bg)
    {
        Transform found = parent.Find(name);
        if (found != null)
        {
            Image img = found.GetComponent<Image>();
            if (img != null)
            {
                img.color = bg;
            }
            return found.GetComponent<RectTransform>();
        }

        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        go.GetComponent<Image>().color = bg;
        return rt;
    }

    /// <summary>
    /// 创建带背景色的面板（Image）。
    /// </summary>
    public static RectTransform MakePanel(Transform parent, string name, Color bg)
    {
        return FindOrMakePanel(parent, name, bg);
    }

    /// <summary>
    /// 查找或创建空 RectTransform 容器（无背景）。
    /// </summary>
    public static RectTransform FindOrMakeRect(Transform parent, string name)
    {
        Transform found = parent.Find(name);
        if (found != null)
        {
            return found.GetComponent<RectTransform>();
        }

        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        return rt;
    }

    /// <summary>
    /// 创建空 RectTransform 容器（无背景）。
    /// </summary>
    public static RectTransform MakeRect(Transform parent, string name)
    {
        return FindOrMakeRect(parent, name);
    }

    /// <summary>
    /// 查找或创建文本。父级下无 UGUI Text 时创建新的。
    /// </summary>
    public static UnityEngine.UI.Text FindOrMakeText(Transform parent, string content, int fontSize, Color color, TextAnchor align = TextAnchor.MiddleLeft)
    {
        Transform found = parent.Find("Text");
        UnityEngine.UI.Text tmp = null;
        if (found != null)
        {
            tmp = found.GetComponent<UnityEngine.UI.Text>();
        }

        if (tmp == null)
        {
            tmp = CreateText(parent);
        }

        ConfigureText(tmp, content, fontSize, color, align);
        return tmp;
    }

    private static UnityEngine.UI.Text CreateText(Transform parent)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform), typeof(UnityEngine.UI.Text));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        return go.GetComponent<UnityEngine.UI.Text>();
    }

    private static void ConfigureText(UnityEngine.UI.Text tmp, string content, int fontSize, Color color, TextAnchor align)
    {
        tmp.text = content;
        tmp.fontSize = Mathf.Max(MinimumFontSize, fontSize);
        tmp.color = color;
        tmp.alignment = align;
        if (DefaultFont != null)
        {
            tmp.font = DefaultFont;
        }
        tmp.horizontalOverflow = HorizontalWrapMode.Overflow;
        tmp.verticalOverflow = VerticalWrapMode.Overflow;
    }

    /// <summary>
    /// 创建文本（默认拉伸占满父级，由调用方加 LayoutElement 控制高度）。
    /// </summary>
    public static UnityEngine.UI.Text MakeText(Transform parent, string content, int fontSize, Color color, TextAnchor align = TextAnchor.MiddleLeft)
    {
        UnityEngine.UI.Text text = CreateText(parent);
        ConfigureText(text, content, fontSize, color, align);
        return text;
    }

    /// <summary>
    /// 查找或创建按钮（自带文字标签，点击回调由 onClick 提供）。
    /// </summary>
    public static Button FindOrMakeButton(Transform parent, string label, Color bg, UnityEngine.Events.UnityAction onClick)
    {
        Transform found = parent.Find("Button");
        Button btn = null;
        if (found != null)
        {
            btn = found.GetComponent<Button>();
        }

        if (btn == null)
        {
            GameObject go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            go.GetComponent<Image>().color = bg;
            btn = go.GetComponent<Button>();
        }

        Image img = btn.GetComponent<Image>();
        if (img != null)
        {
            img.color = bg;
        }

        FindOrMakeText(btn.transform, label, 14, Color.white, TextAnchor.MiddleCenter);

        if (onClick != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(onClick);
        }
        return btn;
    }

    /// <summary>
    /// 创建按钮（自带文字标签，点击回调由 onClick 提供）。
    /// </summary>
    public static Button MakeButton(Transform parent, string label, Color bg, UnityEngine.Events.UnityAction onClick)
    {
        return FindOrMakeButton(parent, label, bg, onClick);
    }

}
