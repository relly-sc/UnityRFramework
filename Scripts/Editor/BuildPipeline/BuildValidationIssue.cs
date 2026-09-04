namespace UnityRFramework.Editor
{
    /// <summary>
    /// 构建前校验问题级别：Error 阻止构建，Warning 不阻止构建。
    /// </summary>
    public enum BuildValidationLevel
    {
        /// <summary>确定性问题：必须修复后才能构建。</summary>
        Error,

        /// <summary>非确定性问题：仅提示，不阻止构建。</summary>
        Warning
    }

    /// <summary>
    /// 构建前校验的问题条目，承载级别、错误码、描述与分组。
    /// 不可变结构，通过静态工厂 <see cref="Error"/> 与 <see cref="Warning"/> 创建。
    /// </summary>
    public readonly struct BuildValidationIssue
    {
        /// <summary>
        /// 创建校验问题条目。
        /// </summary>
        /// <param name="level">问题级别。</param>
        /// <param name="code">稳定错误码，用于报告归类与自动化处理。</param>
        /// <param name="message">人类可读的问题描述。</param>
        /// <param name="group">问题分组，用于窗口分组展示；可为空字符串。</param>
        public BuildValidationIssue(
            BuildValidationLevel level,
            string code,
            string message,
            string group)
        {
            Level = level;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            Group = group ?? string.Empty;
        }

        /// <summary>获取问题级别。</summary>
        public BuildValidationLevel Level { get; }

        /// <summary>获取稳定错误码。</summary>
        public string Code { get; }

        /// <summary>获取问题描述。</summary>
        public string Message { get; }

        /// <summary>获取问题分组；为空表示未分组。</summary>
        public string Group { get; }

        /// <summary>
        /// 创建 Error 级问题条目。
        /// </summary>
        /// <param name="code">稳定错误码。</param>
        /// <param name="message">问题描述。</param>
        /// <param name="group">问题分组；可为空。</param>
        /// <returns>Error 级问题条目。</returns>
        public static BuildValidationIssue Error(
            string code,
            string message,
            string group = "")
        {
            return new BuildValidationIssue(
                BuildValidationLevel.Error,
                code,
                message,
                group);
        }

        /// <summary>
        /// 创建 Warning 级问题条目。
        /// </summary>
        /// <param name="code">稳定错误码。</param>
        /// <param name="message">问题描述。</param>
        /// <param name="group">问题分组；可为空。</param>
        /// <returns>Warning 级问题条目。</returns>
        public static BuildValidationIssue Warning(
            string code,
            string message,
            string group = "")
        {
            return new BuildValidationIssue(
                BuildValidationLevel.Warning,
                code,
                message,
                group);
        }
    }
}
