using System;
using System.Text.RegularExpressions;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 构建敏感信息提供者，统一从环境变量读取密码与密钥，避免敏感值进入
    /// Profile 资产、Console 日志或构建报告。本类不保存任何密码本体。
    /// </summary>
    public static class BuildSecretProvider
    {
        /// <summary>环境变量名格式校验规则：字母或下划线开头，后跟字母数字下划线。</summary>
        private static readonly Regex EnvVarNamePattern =
            new Regex(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

        /// <summary>
        /// 从环境变量读取敏感值。
        /// </summary>
        /// <param name="environmentVariableName">环境变量名。</param>
        /// <returns>环境变量值；变量未设置时抛出异常，不返回空值。</returns>
        public static string ReadSecret(string environmentVariableName)
        {
            ValidateVariableName(environmentVariableName);

            string value = Environment.GetEnvironmentVariable(
                environmentVariableName);
            if (string.IsNullOrEmpty(value))
            {
                throw new InvalidOperationException(
                    $"构建敏感信息缺失：环境变量 '{environmentVariableName}' "
                    + "未设置或为空。请在系统环境或 CI 配置中提供。");
            }

            return value;
        }

        /// <summary>
        /// 校验环境变量名格式，防止非法配置进入环境查询。
        /// </summary>
        /// <param name="environmentVariableName">待校验的环境变量名。</param>
        public static void ValidateVariableName(string environmentVariableName)
        {
            if (string.IsNullOrWhiteSpace(environmentVariableName))
            {
                throw new ArgumentException(
                    "环境变量名不能为空。",
                    nameof(environmentVariableName));
            }

            if (!EnvVarNamePattern.IsMatch(environmentVariableName))
            {
                throw new ArgumentException(
                    $"环境变量名 '{environmentVariableName}' 非法，"
                    + "必须以字母或下划线开头，只允许字母、数字与下划线。",
                    nameof(environmentVariableName));
            }
        }

        /// <summary>
        /// 将敏感值统一脱敏，用于日志与报告输出。
        /// </summary>
        /// <param name="value">原始敏感值；空值返回空字符串。</param>
        /// <returns>脱敏后的固定掩码。</returns>
        public static string Mask(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : "******";
        }
    }
}
