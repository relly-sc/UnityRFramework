using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>安装目标 Agent 及其项目、用户级技能目录。</summary>
    internal sealed class FrameworkSkillAgent
    {
        internal readonly string Name;
        internal readonly string ProjectPath;
        internal readonly string GlobalPath;

        internal FrameworkSkillAgent(string name, string projectPath, string globalPath)
        {
            Name = name;
            ProjectPath = projectPath;
            GlobalPath = globalPath;
        }
    }

    /// <summary>技能文件安装状态；不表示 Agent 应用本身已安装。</summary>
    internal enum FrameworkSkillInstallState
    {
        NotInstalled,
        Installed,
        UpdateAvailable,
        Modified,
        Unmanaged
    }

    /// <summary>将随框架发布的技能安装到 Agent 约定目录。</summary>
    internal static class FrameworkSkillInstaller
    {
        internal const string SkillName = "unityrframework-skills";
        internal const string DisplayName = "UnityRFramework Skills";
        private const string MarkerFile = ".unityrframework-skills-install.json";

        [Serializable]
        private sealed class InstallRecord
        {
            public string owner;
            public string fingerprint;
        }

        /// <summary>返回已支持的 Agent 目录；调用者提供根目录，便于隔离测试。</summary>
        internal static FrameworkSkillAgent[] GetAgents(string projectRoot, string userRoot, string kimiCodeHome = null)
        {
            projectRoot = Path.GetFullPath(projectRoot);
            userRoot = Path.GetFullPath(userRoot);
            return new[]
            {
                Agent("Claude Code", ".claude/skills", ".claude/skills"),
                Agent("Codex", ".agents/skills", ".agents/skills"),
                Agent("Antigravity IDE", ".agents/skills", ".gemini/config/skills"),
                Agent("Cursor", ".cursor/skills", ".cursor/skills"),
                Agent("OpenCode", ".opencode/skills", ".config/opencode/skills"),
                new FrameworkSkillAgent("Kimi Code", Path.Combine(projectRoot, ".kimi-code", "skills"),
                    Path.Combine(string.IsNullOrWhiteSpace(kimiCodeHome) ? Path.Combine(userRoot, ".kimi-code")
                        : Path.GetFullPath(kimiCodeHome), "skills"))
            };

            FrameworkSkillAgent Agent(string name, string projectPath, string globalPath)
            {
                return new FrameworkSkillAgent(name,
                    Path.Combine(projectRoot, projectPath), Path.Combine(userRoot, globalPath));
            }
        }

        /// <summary>仅在当前框架根目录或其父目录中寻找技能，兼容 Assets 开发版和 UPM。</summary>
        internal static string FindSourceDirectory(string startDirectory)
        {
            for (DirectoryInfo directory = new DirectoryInfo(Path.GetFullPath(startDirectory));
                directory != null; directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, "Skills~", SkillName);
                if (File.Exists(Path.Combine(candidate, "SKILL.md"))) return candidate;
            }
            throw new DirectoryNotFoundException("当前框架包中未找到 Skills~/unityrframework-skills/SKILL.md。");
        }

        /// <summary>检查安装状态，并识别用户修改或非本工具创建的目录。</summary>
        internal static FrameworkSkillInstallState GetState(string skillsRoot, string sourceDirectory)
        {
            string target = GetTarget(skillsRoot);
            if (!Directory.Exists(target)) return FrameworkSkillInstallState.NotInstalled;
            string marker = Path.Combine(target, MarkerFile);
            if (!File.Exists(marker)) return FrameworkSkillInstallState.Unmanaged;
            try
            {
                InstallRecord record = JsonUtility.FromJson<InstallRecord>(File.ReadAllText(marker));
                if (record == null || record.owner != SkillName || string.IsNullOrEmpty(record.fingerprint))
                    return FrameworkSkillInstallState.Unmanaged;
                if (Fingerprint(target) != record.fingerprint) return FrameworkSkillInstallState.Modified;
                return Fingerprint(sourceDirectory) == record.fingerprint
                    ? FrameworkSkillInstallState.Installed : FrameworkSkillInstallState.UpdateAvailable;
            }
            catch (IOException) { return FrameworkSkillInstallState.Modified; }
            catch (ArgumentException) { return FrameworkSkillInstallState.Unmanaged; }
        }

        /// <summary>检查预置 Agent 是否已在另一个作用域安装，匹配自定义目录时也生效。</summary>
        internal static string GetScopeConflict(string skillsRoot, string sourceDirectory, FrameworkSkillAgent[] agents)
        {
            if (agents == null) return null;
            string target = GetTarget(skillsRoot);
            StringComparison comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            foreach (FrameworkSkillAgent agent in agents)
            {
                bool project = target.Equals(Path.Combine(Path.GetFullPath(agent.ProjectPath), SkillName), comparison);
                bool global = target.Equals(Path.Combine(Path.GetFullPath(agent.GlobalPath), SkillName), comparison);
                if (!project && !global) continue;
                string otherPath = project ? agent.GlobalPath : agent.ProjectPath;
                if (project && global) continue;
                if (GetState(otherPath, sourceDirectory) != FrameworkSkillInstallState.NotInstalled)
                    return agent.Name + " 已使用" + (project ? "全局" : "项目")
                        + "安装，请先卸载原位置，再切换安装范围。";
            }
            return null;
        }

        /// <summary>安装或更新技能；覆盖本地修改需调用者确认，替换失败时还原原目录。</summary>
        internal static string Install(string sourceDirectory, string skillsRoot, bool overwrite = false,
            FrameworkSkillAgent[] agents = null)
        {
            if (!File.Exists(Path.Combine(sourceDirectory, "SKILL.md")))
                throw new FileNotFoundException("技能源文件 SKILL.md 不存在。");
            string target = GetTarget(skillsRoot);
            string conflict = GetScopeConflict(skillsRoot, sourceDirectory, agents);
            if (!string.IsNullOrEmpty(conflict)) throw new IOException(conflict);
            string source = Path.GetFullPath(sourceDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string destination = target.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            StringComparison comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (source.Equals(destination, comparison)
                || source.StartsWith(destination + Path.DirectorySeparatorChar, comparison)
                || destination.StartsWith(source + Path.DirectorySeparatorChar, comparison))
                throw new IOException("安装目录不能与技能源目录重叠。");
            FrameworkSkillInstallState state = GetState(skillsRoot, sourceDirectory);
            if (!overwrite) RequireManaged(state);
            if (Directory.Exists(target)) GetFiles(target);
            if (state == FrameworkSkillInstallState.Installed) return target;

            string[] files = GetFiles(sourceDirectory);
            string fingerprint = Fingerprint(sourceDirectory);
            string skillsDirectory = Path.GetDirectoryName(target);
            Directory.CreateDirectory(skillsDirectory);
            // 临时目录放在 skills 外，避免被 Agent 当作另一份技能发现。
            string parent = Path.GetDirectoryName(skillsDirectory);
            string transactionRoot = Path.Combine(parent, ".unityrframework-skills-installer");
            EnsureRegularPath(transactionRoot);
            string transaction = Path.Combine(transactionRoot, Guid.NewGuid().ToString("N"));
            EnsureRegularPath(transaction);
            string staged = Path.Combine(transaction, "new");
            string previous = Path.Combine(transaction, "old");
            Directory.CreateDirectory(staged);
            try
            {
                foreach (string file in files)
                {
                    string relative = file.Substring(Path.GetFullPath(sourceDirectory).TrimEnd(
                        Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length + 1);
                    string output = Path.Combine(staged, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(output));
                    File.Copy(file, output);
                }
                File.WriteAllText(Path.Combine(staged, MarkerFile), JsonUtility.ToJson(
                    new InstallRecord { owner = SkillName, fingerprint = fingerprint }, true));
                if (Fingerprint(staged) != fingerprint)
                    throw new IOException("复制后的技能内容校验失败。");
                if (Directory.Exists(target)) Directory.Move(target, previous);
                try { Directory.Move(staged, target); }
                catch
                {
                    if (Directory.Exists(previous)) Directory.Move(previous, target);
                    throw;
                }
            }
            finally
            {
                if (!Directory.Exists(previous) || Directory.Exists(target))
                    Directory.Delete(transaction, true);
            }
            return target;
        }

        /// <summary>仅删除本工具安装且内容未被修改的技能目录。</summary>
        internal static void Uninstall(string skillsRoot, string sourceDirectory)
        {
            FrameworkSkillInstallState state = GetState(skillsRoot, sourceDirectory);
            RequireManaged(state);
            if (state != FrameworkSkillInstallState.NotInstalled)
                Directory.Delete(GetTarget(skillsRoot), true);
        }

        private static void RequireManaged(FrameworkSkillInstallState state)
        {
            if (state == FrameworkSkillInstallState.Modified)
                throw new IOException("已安装技能包含本地修改，未覆盖或删除。请先备份并移开该目录。");
            if (state == FrameworkSkillInstallState.Unmanaged)
                throw new IOException("目标目录已存在且不由本工具管理，未覆盖或删除。请先移开同名目录。");
        }

        private static string GetTarget(string skillsRoot)
        {
            if (string.IsNullOrWhiteSpace(skillsRoot) || !Path.IsPathRooted(skillsRoot))
                throw new ArgumentException("技能目录必须是绝对路径。");
            string pathRoot = Path.GetPathRoot(skillsRoot);
            if (Path.DirectorySeparatorChar == '\\' && pathRoot.Length < 3)
                throw new ArgumentException("技能目录必须包含完整盘符或网络共享路径。");
            string root = Path.GetFullPath(skillsRoot);
            string directory = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.IsNullOrEmpty(Path.GetDirectoryName(directory)))
                throw new ArgumentException("请选择技能父目录，不要选择磁盘根目录。");
            if (string.Equals(Path.GetFileName(directory), SkillName, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("请选择 unityrframework-skills 的上一级目录，避免重复嵌套技能文件夹。");
            if (File.Exists(root)) throw new IOException("技能父目录指向文件，请选择文件夹：" + root);
            EnsureRegularPath(root);
            string target = Path.Combine(root, SkillName);
            if (File.Exists(target)) throw new IOException("安装位置被同名文件占用，请先处理该文件：" + target);
            EnsureRegularPath(target);
            return target;
        }

        private static void EnsureRegularPath(string path)
        {
            if ((Directory.Exists(path) || File.Exists(path))
                && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("不对符号链接或联接目录执行安装操作：" + path);
        }

        private static string[] GetFiles(string directory)
        {
            var files = new List<string>();
            var pending = new Stack<string>();
            pending.Push(Path.GetFullPath(directory));
            while (pending.Count > 0)
            {
                string current = pending.Pop();
                EnsureRegularPath(current);
                foreach (string child in Directory.GetDirectories(current))
                {
                    EnsureRegularPath(child);
                    pending.Push(child);
                }
                foreach (string file in Directory.GetFiles(current))
                {
                    EnsureRegularPath(file);
                    if (Path.GetFileName(file) != MarkerFile) files.Add(file);
                }
            }
            files.Sort(StringComparer.Ordinal);
            return files.ToArray();
        }

        private static string Fingerprint(string directory)
        {
            string root = Path.GetFullPath(directory).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var contents = new StringBuilder();
            using (SHA256 hash = SHA256.Create())
            {
                foreach (string file in GetFiles(root))
                {
                    contents.Append(file.Substring(root.Length + 1).Replace('\\', '/')).Append('\n');
                    contents.Append(Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(file)))).Append('\n');
                }
                return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(contents.ToString())));
            }
        }
    }
}
