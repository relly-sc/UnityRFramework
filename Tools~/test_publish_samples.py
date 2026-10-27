"""用临时本地 Git 仓库验证 Samples Tag 的不可覆盖性，不连接 GitHub。"""

import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest


SCRIPT = Path(__file__).with_name("publish_samples.py")


class SamplesPublicationTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="urf-samples-tags-")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.remote = self.root / "remote.git"
        self.git(self.root, "init", "--bare", "--initial-branch=main", str(self.remote))
        self.count = 0

    @staticmethod
    def git(repository, *args):
        return subprocess.run(
            ["git", *args], cwd=repository, check=True, capture_output=True,
            encoding="utf-8",
        ).stdout.strip()

    def package(self, version="1.12.3", content="示例内容"):
        self.count += 1
        repository = self.root / f"package-{self.count}"
        repository.mkdir()
        self.git(repository, "init", "--initial-branch=main")
        self.git(repository, "config", "user.name", "测试")
        self.git(repository, "config", "user.email", "test@example.invalid")
        self.git(repository, "config", "core.autocrlf", "false")
        (repository / "package.json").write_text(json.dumps({
            "name": "com.relly-sc.unityrframework.samples", "version": version,
        }), encoding="utf-8")
        (repository / "README.md").write_text(content, encoding="utf-8")
        self.git(repository, "add", ".")
        self.git(repository, "commit", "-m", f"生成包 {self.count}")
        self.git(repository, "remote", "add", "origin", str(self.remote))
        return repository

    def publish(self, repository):
        return subprocess.run(
            [sys.executable, str(SCRIPT), str(repository)], capture_output=True,
            encoding="utf-8", env={**os.environ, "PYTHONIOENCODING": "utf-8"},
        )

    def ref(self, name):
        return self.git(self.remote, "rev-parse", name)

    def test_first_publication_creates_annotated_tag_and_no_version_branch(self):
        repository = self.package()
        result = self.publish(repository)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(self.ref("refs/tags/1.12.3^{}"), self.ref("refs/heads/main"))
        self.assertEqual(self.git(self.remote, "cat-file", "-t", "refs/tags/1.12.3"), "tag")
        self.assertEqual(self.git(self.remote, "for-each-ref", "--format=%(refname)", "refs/heads"), "refs/heads/main")

    def test_same_content_reuses_tag_but_updates_main(self):
        first = self.package()
        self.assertEqual(self.publish(first).returncode, 0)
        original = self.ref("refs/tags/1.12.3")
        second = self.package()
        result = self.publish(second)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(self.ref("refs/tags/1.12.3"), original)
        self.assertEqual(self.ref("refs/heads/main"), self.git(second, "rev-parse", "HEAD"))
        self.assertNotEqual(self.ref("refs/tags/1.12.3^{}"), self.ref("refs/heads/main"))

    def test_changed_content_at_same_version_preserves_tag_and_main(self):
        self.assertEqual(self.publish(self.package()).returncode, 0)
        tag, main = self.ref("refs/tags/1.12.3"), self.ref("refs/heads/main")
        result = self.publish(self.package(content="内容已经变化"))
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("不允许覆盖 Tag", result.stderr)
        self.assertEqual(self.ref("refs/tags/1.12.3"), tag)
        self.assertEqual(self.ref("refs/heads/main"), main)

    def test_new_version_preserves_previous_tag(self):
        self.assertEqual(self.publish(self.package()).returncode, 0)
        original = self.ref("refs/tags/1.12.3")
        result = self.publish(self.package("1.12.4", "新版本内容"))
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(self.ref("refs/tags/1.12.3"), original)
        self.assertEqual(self.ref("refs/tags/1.12.4^{}"), self.ref("refs/heads/main"))


if __name__ == "__main__":
    unittest.main()
