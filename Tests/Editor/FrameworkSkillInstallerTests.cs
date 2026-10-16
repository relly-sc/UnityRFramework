using System;
using System.IO;
using NUnit.Framework;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>在隔离目录中验证技能安装、更新、卸载及用户文件保护。</summary>
    public sealed class FrameworkSkillInstallerTests
    {
        private string root;
        private string source;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "UnityRFrameworkSkillTests", Guid.NewGuid().ToString("N"));
            source = Path.Combine(root, "Package", "Skills~", FrameworkSkillInstaller.SkillName);
            Directory.CreateDirectory(Path.Combine(source, "references"));
            File.WriteAllText(Path.Combine(source, "SKILL.md"),
                "---\nname: unityrframework-skills\ndescription: test\n---\n# Test");
            File.WriteAllText(Path.Combine(source, "references", "core.md"), "first");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        [Test]
        public void AllAgentScopesInstallCompleteSkillAndUninstallOnlyIt()
        {
            FrameworkSkillAgent[] agents = FrameworkSkillInstaller.GetAgents(
                Path.Combine(root, "Project"), Path.Combine(root, "Home"));
            Assert.That(agents.Length, Is.EqualTo(6));
            Assert.That(FrameworkSkillInstaller.SkillName, Is.EqualTo("unityrframework-skills"));
            foreach (FrameworkSkillAgent agent in agents)
            {
                foreach (string scope in new[] { agent.ProjectPath, agent.GlobalPath })
                {
                    string other = Path.Combine(scope, "another-skill");
                    Directory.CreateDirectory(other);
                    File.WriteAllText(Path.Combine(other, "SKILL.md"), "untouched");
                    string installed = FrameworkSkillInstaller.Install(source, scope);
                    Assert.That(Path.GetFileName(installed), Is.EqualTo("unityrframework-skills"));
                    Assert.That(File.ReadAllText(Path.Combine(installed, "references", "core.md")), Is.EqualTo("first"));
                    Assert.That(FrameworkSkillInstaller.GetState(scope, source), Is.EqualTo(FrameworkSkillInstallState.Installed));
                    Assert.That(FrameworkSkillInstaller.Install(source, scope), Is.EqualTo(installed));
                    FrameworkSkillInstaller.Uninstall(scope, source);
                    Assert.That(Directory.Exists(installed), Is.False);
                    Assert.That(File.ReadAllText(Path.Combine(other, "SKILL.md")), Is.EqualTo("untouched"));
                }
            }
        }

        [Test]
        public void AgentPathsFollowProjectAndUserConventions()
        {
            string project = Path.Combine(root, "Project");
            string home = Path.Combine(root, "Home");
            FrameworkSkillAgent[] agents = FrameworkSkillInstaller.GetAgents(project, home);
            Assert.That(Path.GetFullPath(agents[0].ProjectPath), Is.EqualTo(Path.Combine(project, ".claude", "skills")));
            Assert.That(Path.GetFullPath(agents[1].GlobalPath), Is.EqualTo(Path.Combine(home, ".agents", "skills")));
            Assert.That(agents[1].ProjectPath, Is.EqualTo(agents[2].ProjectPath));
            Assert.That(Path.GetFullPath(agents[2].GlobalPath), Is.EqualTo(Path.Combine(home, ".gemini", "config", "skills")));
            Assert.That(Path.GetFullPath(agents[3].GlobalPath), Is.EqualTo(Path.Combine(home, ".cursor", "skills")));
            Assert.That(Path.GetFullPath(agents[4].GlobalPath), Is.EqualTo(Path.Combine(home, ".config", "opencode", "skills")));
            Assert.That(Path.GetFullPath(agents[5].ProjectPath), Is.EqualTo(Path.Combine(project, ".kimi-code", "skills")));
            Assert.That(Path.GetFullPath(agents[5].GlobalPath), Is.EqualTo(Path.Combine(home, ".kimi-code", "skills")));
            string customHome = Path.Combine(root, "KimiHome");
            Assert.That(FrameworkSkillInstaller.GetAgents(project, home, customHome)[5].GlobalPath,
                Is.EqualTo(Path.Combine(customHome, "skills")));
        }

        [Test]
        public void UpdateReplacesBundledFilesAndRemovesObsoleteReferences()
        {
            string scope = Path.Combine(root, "skills");
            string target = FrameworkSkillInstaller.Install(source, scope);
            File.Delete(Path.Combine(source, "references", "core.md"));
            File.WriteAllText(Path.Combine(source, "references", "new.md"), "second");
            Assert.That(FrameworkSkillInstaller.GetState(scope, source), Is.EqualTo(FrameworkSkillInstallState.UpdateAvailable));
            FrameworkSkillInstaller.Install(source, scope);
            Assert.That(File.Exists(Path.Combine(target, "references", "core.md")), Is.False);
            Assert.That(File.ReadAllText(Path.Combine(target, "references", "new.md")), Is.EqualTo("second"));
            Assert.That(FrameworkSkillInstaller.GetState(scope, source), Is.EqualTo(FrameworkSkillInstallState.Installed));
        }

        [Test]
        public void LocalChangesPreventUpdateAndUninstall()
        {
            string scope = Path.Combine(root, "skills");
            string target = FrameworkSkillInstaller.Install(source, scope);
            string edited = Path.Combine(target, "references", "core.md");
            File.WriteAllText(edited, "user changes");
            Assert.That(FrameworkSkillInstaller.GetState(scope, source), Is.EqualTo(FrameworkSkillInstallState.Modified));
            Assert.Throws<IOException>(() => FrameworkSkillInstaller.Install(source, scope));
            Assert.Throws<IOException>(() => FrameworkSkillInstaller.Uninstall(scope, source));
            Assert.That(File.ReadAllText(edited), Is.EqualTo("user changes"));
        }

        [Test]
        public void ExistingManualSkillIsNotOverwrittenOrRemoved()
        {
            string scope = Path.Combine(root, "skills");
            string target = Path.Combine(scope, FrameworkSkillInstaller.SkillName);
            Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(target, "SKILL.md"), "manual install");
            Assert.That(FrameworkSkillInstaller.GetState(scope, source), Is.EqualTo(FrameworkSkillInstallState.Unmanaged));
            Assert.Throws<IOException>(() => FrameworkSkillInstaller.Install(source, scope));
            Assert.Throws<IOException>(() => FrameworkSkillInstaller.Uninstall(scope, source));
            Assert.That(File.ReadAllText(Path.Combine(target, "SKILL.md")), Is.EqualTo("manual install"));
        }

        [Test]
        public void SourceDiscoveryWorksUnderAssetsAndPackageCache()
        {
            string scripts = Path.Combine(root, "Package", "Scripts", "Editor");
            Directory.CreateDirectory(scripts);
            Assert.That(FrameworkSkillInstaller.FindSourceDirectory(scripts), Is.EqualTo(source));
            Assert.That(FrameworkSkillInstaller.FindSourceDirectory(Path.Combine(root, "Package")), Is.EqualTo(source));
        }

        [Test]
        public void RelativeDestinationIsRejected()
        {
            Assert.Throws<ArgumentException>(() => FrameworkSkillInstaller.Install(source, "relative-skills"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CustomDirectorySupportsUpdateAndUninstallWithoutAffectingOtherSkills(bool trailingSeparator)
        {
            string scope = Path.Combine(root, "自定义 Agent", "my skills");
            if (trailingSeparator) scope += Path.DirectorySeparatorChar;
            Directory.CreateDirectory(scope);
            string neighbor = Path.Combine(scope, "keep.txt");
            File.WriteAllText(neighbor, "keep");
            string target = FrameworkSkillInstaller.Install(source, scope);
            Assert.That(Directory.GetDirectories(scope).Length, Is.EqualTo(1), "skills 内不应残留安装临时目录。");
            File.WriteAllText(Path.Combine(source, "references", "core.md"), "updated");
            FrameworkSkillInstaller.Install(source, scope);
            Assert.That(File.ReadAllText(Path.Combine(target, "references", "core.md")), Is.EqualTo("updated"));
            FrameworkSkillInstaller.Uninstall(scope, source);
            Assert.That(File.ReadAllText(neighbor), Is.EqualTo("keep"));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ExplicitOverwriteReplacesModifiedOrManuallyInstalledSkill(bool managed)
        {
            string scope = Path.Combine(root, "skills");
            string target = Path.Combine(scope, FrameworkSkillInstaller.SkillName);
            if (managed) FrameworkSkillInstaller.Install(source, scope);
            else Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(target, "SKILL.md"), "local changes");
            File.WriteAllText(Path.Combine(target, "obsolete.md"), "obsolete");
            Assert.Throws<IOException>(() => FrameworkSkillInstaller.Install(source, scope));
            FrameworkSkillInstaller.Install(source, scope, true);
            Assert.That(File.ReadAllText(Path.Combine(target, "SKILL.md")), Is.EqualTo(File.ReadAllText(Path.Combine(source, "SKILL.md"))));
            Assert.That(File.Exists(Path.Combine(target, "obsolete.md")), Is.False);
            Assert.That(FrameworkSkillInstaller.GetState(scope, source), Is.EqualTo(FrameworkSkillInstallState.Installed));
        }

        [Test]
        public void ExplicitOverwriteRejectsSourceOverlap()
        {
            Assert.Throws<IOException>(() => FrameworkSkillInstaller.Install(source, Path.GetDirectoryName(source), true));
            Assert.Throws<IOException>(() => FrameworkSkillInstaller.Install(source, Path.Combine(source, "nested"), true));
            Assert.That(File.Exists(Path.Combine(source, "SKILL.md")), Is.True);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void AgentScopesAreExclusiveAndCanSwitchAfterUninstall(bool global)
        {
            FrameworkSkillAgent[] agents = FrameworkSkillInstaller.GetAgents(
                Path.Combine(root, "Project"), Path.Combine(root, "Home"));
            foreach (FrameworkSkillAgent agent in agents)
            {
                string chosen = global ? agent.GlobalPath : agent.ProjectPath;
                string other = global ? agent.ProjectPath : agent.GlobalPath;
                string installed = FrameworkSkillInstaller.Install(source, chosen, false, agents);
                Assert.That(FrameworkSkillInstaller.GetScopeConflict(other, source, agents), Is.Not.Empty);
                Assert.Throws<IOException>(() => FrameworkSkillInstaller.Install(source, other, true, agents));
                Assert.That(Directory.Exists(Path.Combine(other, FrameworkSkillInstaller.SkillName)), Is.False);
                File.WriteAllText(Path.Combine(source, "references", "core.md"), agent.Name);
                FrameworkSkillInstaller.Install(source, chosen, false, agents);
                Assert.That(File.ReadAllText(Path.Combine(installed, "references", "core.md")), Is.EqualTo(agent.Name));
                FrameworkSkillInstaller.Uninstall(chosen, source);
                FrameworkSkillInstaller.Install(source, other, false, agents);
                FrameworkSkillInstaller.Uninstall(other, source);
            }
        }

        [Test]
        public void SelectingSkillDirectoryItselfIsRejectedBeforeWriting()
        {
            string scope = Path.Combine(root, "skills");
            string target = FrameworkSkillInstaller.Install(source, scope);
            Assert.Throws<ArgumentException>(() => FrameworkSkillInstaller.GetState(target, source));
            Assert.Throws<ArgumentException>(() => FrameworkSkillInstaller.Install(source, target));
            Assert.That(Directory.Exists(Path.Combine(target, FrameworkSkillInstaller.SkillName)), Is.False);
            Assert.That(FrameworkSkillInstaller.GetState(scope, source), Is.EqualTo(FrameworkSkillInstallState.Installed));
        }

        [Test]
        public void FileAtParentOrTargetIsRejectedAndPreserved()
        {
            string scope = Path.Combine(root, "skills");
            File.WriteAllText(scope, "parent file");
            Assert.Throws<IOException>(() => FrameworkSkillInstaller.GetState(scope, source));
            Assert.Throws<IOException>(() => FrameworkSkillInstaller.Install(source, scope, true));
            Assert.That(File.ReadAllText(scope), Is.EqualTo("parent file"));
            File.Delete(scope);
            Directory.CreateDirectory(scope);
            string target = Path.Combine(scope, FrameworkSkillInstaller.SkillName);
            File.WriteAllText(target, "target file");
            Assert.Throws<IOException>(() => FrameworkSkillInstaller.Install(source, scope, true));
            Assert.That(File.ReadAllText(target), Is.EqualTo("target file"));
        }

        [Test]
        public void UnrelatedInvalidAgentPathDoesNotBlockAnotherAgent()
        {
            string invalid = Path.Combine(root, "invalid");
            File.WriteAllText(invalid, "keep");
            var agents = new[]
            {
                new FrameworkSkillAgent("不可用", invalid, Path.Combine(root, "invalid-global")),
                new FrameworkSkillAgent("可用", Path.Combine(root, "project-skills"), Path.Combine(root, "global-skills"))
            };
            Assert.Throws<IOException>(() => FrameworkSkillInstaller.GetState(agents[0].ProjectPath, source));
            Assert.That(FrameworkSkillInstaller.GetScopeConflict(agents[1].ProjectPath, source, agents), Is.Null);
            string target = FrameworkSkillInstaller.Install(source, agents[1].ProjectPath, false, agents);
            Assert.That(File.Exists(Path.Combine(target, "SKILL.md")), Is.True);
            Assert.That(File.ReadAllText(invalid), Is.EqualTo("keep"));
        }
    }
}
