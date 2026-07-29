using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 启动失败统一错误屏。
/// 采用纯代码构建的自包含 Canvas，不依赖 UI 模块——因为 UI 模块本身可能就是启动失败的根因。
/// 文本使用内置 Arial 字体、颜色使用常量，确保资源系统未就绪时也能稳定弹出，
/// 避免"连报错界面都出不来"的二次故障。
/// </summary>
public static class DemoErrorPanel
{
    /// <summary>
    /// 当前错误屏实例（同一时刻仅存在一个）。
    /// </summary>
    private static GameObject instance;

    /// <summary>
    /// 展示启动错误屏与重试按钮。重复调用会先销毁已有实例，避免堆叠。
    /// </summary>
    /// <param name="exception">捕获到的异常（用于提取错误信息）。</param>
    /// <param name="onRetry">点击重试时的回调。</param>
    public static void Show(System.Exception exception, System.Action onRetry)
    {
        Hide();

        GameObject root = new GameObject("DemoErrorPanel");
        instance = root;

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;
        root.AddComponent<GraphicRaycaster>();

        // 背景遮罩
        GameObject bg = new GameObject("Background");
        bg.transform.SetParent(root.transform);
        Image bgImage = bg.AddComponent<Image>();
        bgImage.color = new Color(0.1f, 0.1f, 0.12f, 0.92f);
        RectTransform bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // 居中面板容器
        GameObject panel = new GameObject("Panel");
        panel.transform.SetParent(root.transform);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(560f, 320f);
        panelRect.anchoredPosition = Vector2.zero;

        // 标题
        GameObject title = MakeText(panel.transform, "启动失败 / Launch Failed", 24, Color.white, TextAnchor.MiddleCenter);
        RectTransform titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.sizeDelta = new Vector2(0f, 40f);
        titleRect.anchoredPosition = new Vector2(0f, -20f);

        // 错误详情
        string detail = exception != null ? exception.Message : "未知错误";
        GameObject body = MakeText(panel.transform, detail, 14, new Color(1f, 0.7f, 0.7f), TextAnchor.UpperLeft);
        RectTransform bodyRect = body.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0f, 0.25f);
        bodyRect.anchorMax = new Vector2(1f, 0.85f);
        bodyRect.sizeDelta = Vector2.zero;

        // 重试按钮
        GameObject btn = new GameObject("RetryButton");
        btn.transform.SetParent(panel.transform);
        Image btnImage = btn.AddComponent<Image>();
        btnImage.color = new Color(0.2f, 0.5f, 0.9f);
        RectTransform btnRect = btn.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0f);
        btnRect.anchorMax = new Vector2(0.5f, 0f);
        btnRect.sizeDelta = new Vector2(160f, 44f);
        btnRect.anchoredPosition = new Vector2(0f, 30f);
        Button button = btn.AddComponent<Button>();
        button.onClick.AddListener(() =>
        {
            Hide();
            onRetry?.Invoke();
        });
        MakeText(btn.transform, "重试 / Retry", 16, Color.white, TextAnchor.MiddleCenter);
    }

    /// <summary>
    /// 隐藏并销毁当前错误屏（重试点击或启动成功后调用）。
    /// 场景热重载后 instance 可能已是失效引用，Destroy 对失效对象安全，随后置空。
    /// </summary>
    public static void Hide()
    {
        if (instance != null)
        {
            Object.Destroy(instance);
            instance = null;
        }
    }

    /// <summary>
    /// 创建全拉伸文本对象（使用内置 Arial 字体，零外部资源依赖）。
    /// </summary>
    private static GameObject MakeText(Transform parent, string content, int fontSize, Color color, TextAnchor alignment)
    {
        GameObject go = new GameObject("Text");
        go.transform.SetParent(parent);
        Text text = go.AddComponent<Text>();
        text.text = content;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return go;
    }
}
