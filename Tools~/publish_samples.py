#!/usr/bin/env python3
"""发布已生成并提交的 Samples 仓库：版本 Tag 不变，main 保持最新，不创建 Release。"""

import json
from pathlib import Path
import subprocess
import sys

from build_upm_packages import version_tuple


def publish(repository: Path) -> None:
    """复用内容一致的 Tag，拒绝同版本不同内容；不更换旧 Tag 的提交。"""
    def git(*args: str) -> str:
        return subprocess.run(
            ["git", *args], cwd=repository, check=True,
            stdout=subprocess.PIPE, encoding="utf-8",
        ).stdout.strip()

    manifest = json.loads((repository / "package.json").read_text(encoding="utf-8-sig"))
    if manifest.get("name") != "com.relly-sc.unityrframework.samples":
        raise ValueError("只能发布 UnityRFramework Samples 包。")
    version = manifest["version"]
    version_tuple(version)
    tag = "refs/tags/" + version
    if git("ls-remote", "--tags", "origin", tag):
        git("fetch", "origin", tag)
        unchanged = subprocess.run(
            ["git", "diff", "--quiet", "FETCH_HEAD", "HEAD", "--", "."], cwd=repository,
        )
        if unchanged.returncode != 0:
            raise RuntimeError(f"Samples {version} 已发布，内容不同或无法比较；请增加 Samples 版本，不允许覆盖 Tag。")
        git("push", "origin", "+HEAD:refs/heads/main")
    else:
        git("tag", "-a", version, "-m", f"UnityRFramework Samples {version}")
        # 只允许更新生成的 main；Tag 和 main 一起发布，Tag 从不强推。
        git("push", "--atomic", "origin", tag, "+HEAD:refs/heads/main")
    print(f"Samples {version} Tag 已就绪；未创建 GitHub Release。")


if __name__ == "__main__":
    publish(Path(sys.argv[1]).resolve())
