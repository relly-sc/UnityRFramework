using System;
using System.IO;
using System.Text;
using UnityEngine;
using Process = System.Diagnostics.Process;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 构建任务锁信息，写入锁文件用于陈旧锁诊断。
    /// </summary>
    [Serializable]
    public sealed class BuildPipelineLockInfo
    {
        /// <summary>持有锁的任务 Id。</summary>
        public string TaskId = string.Empty;

        /// <summary>持有锁的进程 Id。</summary>
        public int ProcessId;

        /// <summary>锁创建时刻（ISO 8601 字符串）。</summary>
        public string CreatedAt = string.Empty;
    }

    /// <summary>
    /// 构建任务检查点持久化：状态与锁文件统一存放在
    /// Library/UnityRFramework/BuildPipeline/ 目录，不污染 Assets 与 UPM 包。
    /// 根目录可注入（测试使用临时目录），默认为工程 Library 下的固定路径。
    /// </summary>
    public sealed class BuildPipelinePersistence
    {
        /// <summary>默认状态目录（相对工程根）。</summary>
        public const string DefaultRelativeDirectory =
            "Library/UnityRFramework/BuildPipeline";

        /// <summary>状态文件名称。</summary>
        public const string StateFileName = "task.json";

        /// <summary>锁文件名称。</summary>
        public const string LockFileName = "task.lock";

        /// <summary>持久化根目录绝对路径。</summary>
        public string RootDirectory { get; }

        /// <summary>状态文件绝对路径。</summary>
        public string StatePath
        {
            get
            {
                return Path.Combine(RootDirectory, StateFileName);
            }
        }

        /// <summary>锁文件绝对路径。</summary>
        public string LockPath
        {
            get
            {
                return Path.Combine(RootDirectory, LockFileName);
            }
        }

        /// <summary>是否存在状态文件。</summary>
        public bool HasState
        {
            get
            {
                return File.Exists(StatePath);
            }
        }

        /// <summary>是否存在锁文件。</summary>
        public bool HasLock
        {
            get
            {
                return File.Exists(LockPath);
            }
        }

        /// <summary>
        /// 创建持久化实例。
        /// </summary>
        /// <param name="rootDirectory">持久化根目录绝对路径；为空时使用工程默认目录。</param>
        public BuildPipelinePersistence(string rootDirectory = null)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
            {
                rootDirectory = DefaultRootDirectory();
            }
            RootDirectory = rootDirectory;
        }

        /// <summary>
        /// 计算工程默认持久化目录绝对路径。
        /// </summary>
        /// <returns>Library/UnityRFramework/BuildPipeline 的绝对路径。</returns>
        public static string DefaultRootDirectory()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(
                projectRoot,
                "Library",
                "UnityRFramework",
                "BuildPipeline");
        }

        /// <summary>
        /// 加载任务状态；状态文件不存在时返回空。
        /// </summary>
        /// <returns>任务状态；无状态文件时为空。</returns>
        public BuildPipelineState LoadState()
        {
            if (!HasState)
            {
                return null;
            }
            string json = File.ReadAllText(StatePath, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }
            return JsonUtility.FromJson<BuildPipelineState>(json);
        }

        /// <summary>
        /// 保存任务状态（检查点）：写入临时文件后替换目标文件，
        /// 避免写入中断产生损坏的状态文件。
        /// </summary>
        /// <param name="state">待保存的任务状态。</param>
        public void SaveState(BuildPipelineState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }
            Directory.CreateDirectory(RootDirectory);
            string json = JsonUtility.ToJson(state, true);
            string tempPath = StatePath + ".tmp";
            File.WriteAllText(tempPath, json, new UTF8Encoding(false));
            if (File.Exists(StatePath))
            {
                File.Delete(StatePath);
            }
            File.Move(tempPath, StatePath);
        }

        /// <summary>
        /// 删除状态文件；不存在时忽略。
        /// </summary>
        public void DeleteState()
        {
            TryDelete(StatePath);
        }

        /// <summary>
        /// 尝试获取项目级任务锁。
        /// 使用 FileMode.CreateNew 原子创建锁文件；文件已存在时视为已有任务持有锁。
        /// </summary>
        /// <param name="state">持有锁的任务状态，用于写入锁信息。</param>
        /// <param name="error">获取失败时的原因描述。</param>
        /// <returns>获取成功时返回 true。</returns>
        public bool TryAcquireLock(BuildPipelineState state, out string error)
        {
            error = string.Empty;
            try
            {
                Directory.CreateDirectory(RootDirectory);
                BuildPipelineLockInfo info = new BuildPipelineLockInfo
                {
                    TaskId = state != null ? state.TaskId : string.Empty,
                    ProcessId = Process.GetCurrentProcess().Id,
                    CreatedAt = DateTime.Now.ToString("o")
                };
                string json = JsonUtility.ToJson(info, true);
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                using (FileStream stream = new FileStream(
                    LockPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush();
                }
                return true;
            }
            catch (IOException)
            {
                error = File.Exists(LockPath)
                    ? $"已有构建任务持有锁（{LockPath}）。"
                    : $"创建构建任务锁失败：{LockPath}。";
                return false;
            }
            catch (UnauthorizedAccessException exception)
            {
                error = $"无法创建构建任务锁：{exception.Message}";
                return false;
            }
        }

        /// <summary>
        /// 释放任务锁；锁文件不存在时忽略。
        /// </summary>
        public void ReleaseLock()
        {
            TryDelete(LockPath);
        }

        /// <summary>
        /// 尝试删除指定文件并忽略删除失败（记录警告）。
        /// </summary>
        /// <param name="path">待删除文件路径。</param>
        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception exception)
            {
                Debug.Log(
                    $"构建任务文件清理失败：{path}，{exception.Message}");
            }
        }
    }
}
