using System.Threading.Tasks;
using RFramework;
using UnityRFramework.Runtime;
using UnityEngine;
/// <summary>
/// 公告数据模型。对应 StreamingAssets/Demo/Demo_Notice.json。
/// </summary>
[System.Serializable]
public class DemoNotice
{
    /// <summary>
    /// 公告标题本地化键。
    /// </summary>
    public string TitleKey;

    /// <summary>
    /// 公告多行正文的本地化键。
    /// </summary>
    public string[] LineKeys;
}

/// <summary>
/// Demo 公告加载器。通过 WebRequest 从 StreamingAssets 拉取公告 JSON，演示 HTTP 通信链路。
/// 使用默认 WebRequestHelper（UnityWebRequest），本地文件走 file:// 协议。
/// </summary>
public static class DemoNoticeLoader
{
    /// <summary>
    /// 异步加载公告文本。
    /// </summary>
    /// <returns>解析后的公告数据；请求失败或解析失败时返回 null。</returns>
    public static async Task<DemoNotice> LoadNoticeAsync()
    {
        string url = BuildNoticeUrl();
        Log.Info("[Demo] Notice: requesting {0}", url);

        WebResponse response = await GameEntry.WebRequest.GetAsync(url);
        if (response == null || !response.IsSuccess)
        {
            Log.Error("[Demo] Notice: request failed. status={0}, error={1}",
                response != null ? response.StatusCode : -1,
                response != null ? response.ErrorMessage : "null");
            return null;
        }

        DemoNotice notice = JsonUtility.FromJson<DemoNotice>(response.Text);
        if (notice == null)
        {
            Log.Error("[Demo] Notice: json parse failed.");
            return null;
        }

        Log.Info("[Demo] Notice: loaded titleKey='{0}', lines={1}", notice.TitleKey, notice.LineKeys?.Length ?? 0);
        return notice;
    }

    /// <summary>
    /// 构造公告文件 URL。编辑器/单机使用 file:// 协议，WebGL 使用 streamingAssets 直链。
    /// </summary>
    private static string BuildNoticeUrl()
    {
        string basePath = Application.streamingAssetsPath + "/Demo/Demo_Notice.json";
        if (Application.platform == RuntimePlatform.WebGLPlayer)
        {
            return basePath;
        }

        return "file:///" + Utility.Path.GetRegularPath(basePath);
    }
}
