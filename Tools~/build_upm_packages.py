#!/usr/bin/env python3
"""从开发仓库生成核心与 Samples 两个独立 UPM 包。"""

from __future__ import annotations

import argparse
import json
import shutil
import subprocess
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
CORE_PACKAGE_NAME = "com.relly-sc.unityrframework"
SAMPLES_PACKAGE_NAME = "com.relly-sc.unityrframework.samples"
EXCLUDED_CORE_ROOTS = {
    ".github",
    ".gitignore",
    ".gitmodules",
    "Samples",
    "Samples.meta",
    "Tools~",
}


def git_files() -> list[Path]:
    result = subprocess.run(
        ["git", "ls-files", "--recurse-submodules", "-z"],
        cwd=ROOT,
        check=True,
        capture_output=True,
    )
    return [Path(item.decode("utf-8")) for item in result.stdout.split(b"\0") if item]


def copy_file(relative_path: Path, destination_root: Path, destination_path: Path | None = None) -> None:
    source = ROOT / relative_path
    if not source.is_file():
        raise FileNotFoundError(f"Git 跟踪文件不存在：{relative_path.as_posix()}")
    target = destination_root / (destination_path or relative_path)
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, target)


def load_manifest(path: Path) -> dict:
    with path.open("r", encoding="utf-8-sig") as stream:
        return json.load(stream)


def load_manifests() -> tuple[dict, dict]:
    core_manifest = load_manifest(ROOT / "Package.json")
    samples_manifest = load_manifest(ROOT / "Tools~" / "SamplesPackage.json")
    if core_manifest.get("name") != CORE_PACKAGE_NAME:
        raise ValueError(f"核心包名不正确：{core_manifest.get('name')!r}")
    if samples_manifest.get("name") != SAMPLES_PACKAGE_NAME:
        raise ValueError(f"Samples 包名不正确：{samples_manifest.get('name')!r}")

    version = core_manifest.get("version")
    samples_version = samples_manifest.get("version")
    dependency_version = samples_manifest.get("dependencies", {}).get(CORE_PACKAGE_NAME)
    if not version or samples_version != version or dependency_version != version:
        raise ValueError(
            "核心版本、Samples 版本及 Samples 对核心的依赖版本必须完全一致："
            f"core={version!r}, samples={samples_version!r}, dependency={dependency_version!r}"
        )
    return core_manifest, samples_manifest


def write_manifest(path: Path, manifest: dict) -> None:
    path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def build_core_package(output: Path, files: list[Path], source_manifest: dict) -> None:
    for relative_path in files:
        if relative_path.name in {"Package.json", "Package.json.meta"}:
            continue
        if relative_path.parts[0] in EXCLUDED_CORE_ROOTS:
            continue
        copy_file(relative_path, output)

    write_manifest(output / "package.json", source_manifest)


def build_samples_package(output: Path, files: list[Path], samples_manifest: dict) -> None:
    samples = samples_manifest.get("samples")
    if not isinstance(samples, list) or not samples:
        raise ValueError("源 Package.json 未声明 Samples。")

    declared_paths: set[str] = set()
    for sample in samples:
        sample_path = sample.get("path", "")
        if not sample_path.startswith("Samples~/"):
            raise ValueError(f"Sample 路径必须位于 Samples~/：{sample_path!r}")
        source_path = ROOT / "Samples" / sample_path.removeprefix("Samples~/")
        if not source_path.is_dir():
            raise FileNotFoundError(f"Sample 目录不存在：{source_path}")
        if sample_path in declared_paths:
            raise ValueError(f"Sample 路径重复：{sample_path}")
        declared_paths.add(sample_path)

    for relative_path in files:
        if not relative_path.parts or relative_path.parts[0] != "Samples":
            continue
        target_path = Path("Samples~", *relative_path.parts[1:])
        copy_file(relative_path, output, target_path)

    for shared_file in ("LICENSE", "LICENSE.meta", "THIRD-PARTY-NOTICES.md", "THIRD-PARTY-NOTICES.md.meta"):
        source = ROOT / shared_file
        if source.is_file():
            copy_file(Path(shared_file), output)

    write_manifest(output / "package.json", samples_manifest)
    (output / "README.md").write_text(
        "# UnityRFramework Samples\n\n"
        "UnityRFramework 官方示例、验收场景与可选扩展。请先安装相同版本的核心包，"
        "再通过 Unity Package Manager 按需导入 Sample。\n",
        encoding="utf-8",
    )


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=ROOT / "Release", help="发布目录")
    args = parser.parse_args()

    output_root = args.output.resolve()
    if output_root == ROOT or ROOT in output_root.parents and output_root.name == "Assets":
        raise ValueError("发布目录不能覆盖开发仓库或 Unity Assets。")

    core_output = output_root / CORE_PACKAGE_NAME
    samples_output = output_root / SAMPLES_PACKAGE_NAME
    for directory in (core_output, samples_output):
        if directory.exists():
            shutil.rmtree(directory)
        directory.mkdir(parents=True)

    core_manifest, samples_manifest = load_manifests()
    files = git_files()
    build_core_package(core_output, files, core_manifest)
    build_samples_package(samples_output, files, samples_manifest)

    print(f"核心包：{core_output}")
    print(f"Samples 包：{samples_output}")
    print(f"版本：{core_manifest['version']}")


if __name__ == "__main__":
    main()
