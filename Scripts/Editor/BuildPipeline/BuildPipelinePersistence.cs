using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using RFramework;
using UnityEngine;
using Process = System.Diagnostics.Process;
using Debug = UnityEngine.Debug;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 构建任务锁信息，写入锁文件用于并发与陈旧锁判定。
    /// 锁同时记录任务 Id、进程 Id、工程标识和创建时间；
    /// 恢复前必须验证持锁进程是否仍存活且工程标识匹配。
    /// </summary>
    [Serializable]
    public sealed class BuildPipelineLockInfo
    {
        /// <summary>持有锁的任务 Id；与状态文件中的 TaskId 必须一致。</summary>
        public string TaskId = string.Empty;

        /// <summary>持有锁的进程 Id。</summary>
        public int ProcessId;

        /// <summary>持锁进程的工程标识（见 <see cref="BuildPipelinePersistence.GetProjectIdentity"/>）。</summary>
        public string ProjectId = string.Empty;

        /// <summary>锁创建时刻（ISO 8601 字符串）。</summary>
        public string CreatedAt = string.Empty;

        /// <summary>
        /// 判断持锁进程是否仍然存活。
        /// 进程不存在，或存在但进程名不是 Unity（进程 Id 被复用）时返回 false。
        /// </summary>
        /// <returns>持锁进程存活时返回 true。</returns>
        public bool IsHolderProcessAlive()
        {
            if (ProcessId <= 0)
            {
                return false;
            }

            try
            {
                Process holder = Process.GetProcessById(ProcessId);
                using (holder)
                {
                    return !holder.HasExited
                        && holder.ProcessName.IndexOf(
                            "Unity",
                            StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch (ArgumentException)
            {
                // 进程 Id 不存在：持锁进程已退出。
                return false;
            }
            catch (InvalidOperationException)
            {
                // 进程在查询过程中退出。
                return false;
            }
        }

        /// <summary>
        /// 判断锁记录的工程标识是否与当前工程一致。
        /// 工程标识不一致说明锁文件来自其他工程（如 Library 目录被复制或同步）。
        /// </summary>
        /// <param name="currentProjectId">当前工程的标识。</param>
        /// <returns>一致时返回 true。</returns>
        public bool MatchesProject(string currentProjectId)
        {
            return !string.IsNullOrEmpty(ProjectId)
                && string.Equals(ProjectId, currentProjectId, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// 状态文件加载结果。
    /// </summary>
    public enum BuildPipelineStateLoadResult
    {
        /// <summary>状态文件存在且成功解析。</summary>
        Success,

        /// <summary>状态文件损坏或版本不识别，已从备份文件恢复。</summary>
        LoadedFromBackup,

        /// <summary>状态文件与备份文件都不存在。</summary>
        Missing,

        /// <summary>状态文件损坏且备份不可用或不存在。</summary>
        Corrupt,

        /// <summary>状态文件序列化版本与当前实现不一致，无法安全恢复。</summary>
        VersionMismatch
    }

    /// <summary>
    /// 构建任务检查点持久化：状态与锁文件统一存放在
    /// Library/UnityRFramework/BuildPipeline/ 目录，不污染 Assets 与 UPM 包。
    /// 状态保存使用临时文件、安全替换（File.Replace）和可恢复备份（task.json.bak）；
    /// 加载失败时自动回退到备份文件。根目录可注入（测试使用临时目录），
    /// 默认为工程 Library 下的固定路径。
    /// </summary>
    public sealed class BuildPipelinePersistence
    {
        /// <summary>默认状态目录（相对工程根）。</summary>
        public const string DefaultRelativeDirectory =
            "Library/UnityRFramework/BuildPipeline";

        /// <summary>状态文件名称。</summary>
        public const string StateFileName = "task.json";

        /// <summary>状态备份文件名称；保存时由安全替换自动写入上一份状态。</summary>
        public const string StateBackupFileName = "task.json.bak";

        /// <summary>状态临时文件名称。</summary>
        public const string StateTempFileName = "task.json.tmp";

        /// <summary>设置事务快照文件名称。</summary>
        public const string SnapshotFileName = "settings-snapshot.json";

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

        /// <summary>状态备份文件绝对路径。</summary>
        public string StateBackupPath
        {
            get
            {
                return Path.Combine(RootDirectory, StateBackupFileName);
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

        /// <summary>是否存在状态文件或其备份。</summary>
        public bool HasState
        {
            get
            {
                return File.Exists(StatePath) || File.Exists(StateBackupPath);
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
        /// 计算当前工程的稳定标识：工程根目录绝对路径的 CRC32 十六进制表示。
        /// 标识只用于本机判定锁文件是否属于当前工程，不作为跨机器唯一键。
        /// </summary>
        /// <returns>8 位十六进制工程标识。</returns>
        public static string GetProjectIdentity()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            byte[] bytes = Encoding.UTF8.GetBytes(projectRoot ?? string.Empty);
            return Utility.Verifier.GetCrc32(bytes).ToString("x8");
        }

        /// <summary>
        /// 加载任务状态；状态文件不存在、损坏或版本不识别时返回空。
        /// 需要区分加载结果时使用 <see cref="LoadStateDetailed"/>。
        /// </summary>
        /// <returns>任务状态；不可用时为空。</returns>
        public BuildPipelineState LoadState()
        {
            BuildPipelineStateLoadResult result =
                LoadStateDetailed(out BuildPipelineState state);
            return result == BuildPipelineStateLoadResult.Success
                || result == BuildPipelineStateLoadResult.LoadedFromBackup
                ? state
                : null;
        }

        /// <summary>
        /// 加载任务状态并区分结果：主文件损坏或版本不识别时自动回退到备份文件。
        /// 任意版本不一致均不迁移；损坏状态不自动删除，由恢复流程决定作废或保留。
        /// </summary>
        /// <param name="state">加载成功的任务状态；不可用时为空。</param>
        /// <returns>加载结果。</returns>
        public BuildPipelineStateLoadResult LoadStateDetailed(out BuildPipelineState state)
        {
            state = null;
            BuildPipelineStateLoadResult primary = TryLoadStateFile(
                StatePath, out BuildPipelineState loaded);
            if (primary == BuildPipelineStateLoadResult.Success)
            {
                state = loaded;
                return BuildPipelineStateLoadResult.Success;
            }

            BuildPipelineStateLoadResult backup = TryLoadStateFile(
                StateBackupPath, out BuildPipelineState fromBackup);
            if (backup == BuildPipelineStateLoadResult.Success)
            {
                state = fromBackup;
                Debug.Log(
                    $"构建任务状态文件损坏（{primary}），已从备份恢复：{StateBackupPath}");
                return BuildPipelineStateLoadResult.LoadedFromBackup;
            }

            if (primary == BuildPipelineStateLoadResult.Missing)
            {
                return backup == BuildPipelineStateLoadResult.Missing
                    ? BuildPipelineStateLoadResult.Missing
                    : BuildPipelineStateLoadResult.Corrupt;
            }

            return primary;
        }

        /// <summary>
        /// 保存任务状态（检查点）：写入临时文件后通过 File.Replace 安全替换主文件，
        /// 旧状态自动写入备份文件；主文件不存在时直接落盘。
        /// 写入中断最多损失当前检查点，主文件与备份文件总有一份完整可用。
        /// </summary>
        /// <param name="state">待保存的任务状态。</param>
        public void SaveState(BuildPipelineState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            Directory.CreateDirectory(RootDirectory);
            string tempPath = Path.Combine(RootDirectory, StateTempFileName);
            string json = JsonUtility.ToJson(state, true);
            File.WriteAllText(tempPath, json, new UTF8Encoding(false));

            try
            {
                if (File.Exists(StatePath))
                {
                    File.Replace(tempPath, StatePath, StateBackupPath, true);
                }
                else
                {
                    File.Move(tempPath, StatePath);
                }
            }
            finally
            {
                // 替换失败时清理残留临时文件，保留旧主文件与备份不被破坏。
                TryDeleteFile(tempPath);
            }
        }

        /// <summary>
        /// 删除状态文件与备份文件；不存在时忽略。
        /// </summary>
        public void DeleteState()
        {
            TryDeleteFile(StatePath);
            TryDeleteFile(StateBackupPath);
            TryDeleteFile(Path.Combine(RootDirectory, SnapshotFileName));
        }

        /// <summary>
        /// 保存设置事务快照到任务目录（安全替换）。
        /// </summary>
        /// <param name="snapshot">待保存的快照。</param>
        public void SaveSnapshot(BuildSettingsSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            Directory.CreateDirectory(RootDirectory);
            string path = Path.Combine(RootDirectory, SnapshotFileName);
            string json = JsonUtility.ToJson(snapshot, true);
            File.WriteAllText(path, json, new UTF8Encoding(false));
        }

        /// <summary>
        /// 读取设置事务快照；文件缺失或解析失败时返回空。
        /// </summary>
        /// <returns>快照；不可用时为空。</returns>
        public BuildSettingsSnapshot LoadSnapshot()
        {
            string path = Path.Combine(RootDirectory, SnapshotFileName);
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                return JsonUtility.FromJson<BuildSettingsSnapshot>(json);
            }
            catch (Exception exception)
            {
                Debug.Log($"构建设置快照读取失败：{path}，{exception.Message}");
                return null;
            }
        }

        /// <summary>
        /// 尝试获取项目级任务锁。
        /// 使用 FileMode.CreateNew 原子创建锁文件；文件已存在时视为已有任务持有锁。
        /// 锁信息记录任务 Id、当前进程 Id、工程标识和创建时间。
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
                    ProjectId = GetProjectIdentity(),
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
                    ? DescribeExistingLock()
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
        /// 读取锁文件内容。
        /// </summary>
        /// <param name="lockInfo">读取成功时的锁信息；失败时为空。</param>
        /// <returns>锁文件存在且解析成功时返回 true。</returns>
        public bool ReadLockInfo(out BuildPipelineLockInfo lockInfo)
        {
            lockInfo = null;
            if (!File.Exists(LockPath))
            {
                return false;
            }

            try
            {
                string json = File.ReadAllText(LockPath, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return false;
                }

                lockInfo = JsonUtility.FromJson<BuildPipelineLockInfo>(json);
                return lockInfo != null;
            }
            catch (Exception exception)
            {
                Debug.Log($"构建任务锁文件读取失败：{LockPath}，{exception.Message}");
                return false;
            }
        }

        /// <summary>
        /// 释放任务锁；锁文件不存在时忽略。
        /// </summary>
        public void ReleaseLock()
        {
            TryDeleteFile(LockPath);
        }

        /// <summary>
        /// 描述已存在的锁：持锁进程存活时给出任务与进程信息；
        /// 持锁进程已退出或工程标识不匹配时明确说明锁已残留、可安全作废。
        /// </summary>
        /// <returns>锁状态描述文本。</returns>
        private string DescribeExistingLock()
        {
            if (!ReadLockInfo(out BuildPipelineLockInfo info))
            {
                return $"已有构建任务持有锁（{LockPath}），但锁文件无法读取，"
                    + "可作废该残留锁后重试。";
            }

            if (!info.MatchesProject(GetProjectIdentity()))
            {
                return $"锁文件（{LockPath}）属于其他工程（工程标识 {info.ProjectId}），"
                    + "可作废该残留锁后重试。";
            }

            if (!info.IsHolderProcessAlive())
            {
                return $"锁文件（{LockPath}）的持锁进程（PID {info.ProcessId}）已退出，"
                    + "属于残留锁，可作废后重试。";
            }

            return $"已有构建任务持有锁（任务 {info.TaskId}，PID {info.ProcessId}）。";
        }

        /// <summary>
        /// 读取并解析单个状态文件，并严格判定序列化版本。
        /// </summary>
        /// <param name="path">状态文件绝对路径。</param>
        /// <param name="state">解析成功且版本可用的任务状态；失败时为空。</param>
        /// <returns>单文件加载结果。</returns>
        private BuildPipelineStateLoadResult TryLoadStateFile(
            string path,
            out BuildPipelineState state)
        {
            state = null;
            if (!File.Exists(path))
            {
                return BuildPipelineStateLoadResult.Missing;
            }

            BuildPipelineState parsed;
            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return BuildPipelineStateLoadResult.Corrupt;
                }

                parsed = JsonUtility.FromJson<BuildPipelineState>(json);
            }
            catch (Exception exception)
            {
                Debug.Log($"构建任务状态文件解析失败：{path}，{exception.Message}");
                return BuildPipelineStateLoadResult.Corrupt;
            }

            if (parsed == null || string.IsNullOrEmpty(parsed.PhaseName))
            {
                return BuildPipelineStateLoadResult.Corrupt;
            }

            if (parsed.SerializedVersion != BuildPipelineState.CurrentSerializedVersion)
            {
                return BuildPipelineStateLoadResult.VersionMismatch;
            }

            state = parsed;
            return BuildPipelineStateLoadResult.Success;
        }

        /// <summary>
        /// 尝试删除指定文件并忽略删除失败（记录警告）。
        /// </summary>
        /// <param name="path">待删除文件路径。</param>
        private static void TryDeleteFile(string path)
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
