namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 为 LocalizationComponent 提供语言代码到资源位置的默认映射。
    /// 未实现该接口的 Helper 仍可通过显式 location 重载加载语言。
    /// </summary>
    public interface ILocalizationLocationProvider
    {
        /// <summary>
        /// 获取指定语言的默认资源位置。
        /// 返回值由当前 ResourceHelper 解释，可以是资源路径、可寻址地址或自定义标识。
        /// </summary>
        /// <param name="language">语言代码。</param>
        /// <returns>资源位置。</returns>
        string GetLanguageLocation(string language);
    }
}
