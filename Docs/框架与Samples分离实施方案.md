# UnityRFramework 框架与 Samples 分离实施方案

## 1. 文档目的

随着 `Samples/` 下的 Demo、验收场景和可选扩展持续增加，框架仓库体积不断增大。本文确定开发期、发布期和用户安装期的统一目录方案，目标是：

- 开发期保持框架与 Samples 同仓库，方便同步修改和联调。
- 发布期将框架与 Samples 作为两个独立的 Unity UPM 包发布。
- 用户安装框架时不下载 Samples，需要时再从 Samples 仓库安装。
- 不引入 Git 子模块、UPM 私有服务器或用户上传生态。

本方案在当前本地安全防护实施计划完成后执行。

## 2. 最终方案

采用“一个开发仓库、两个 UPM 包、两个发布仓库”的结构。

```text
开发仓库 UnityRFramework
├── Project/                                      # 开发用 Unity 工程
├── Packages/
│   ├── com.relly-sc.unityrframework/             # 核心框架包
│   └── com.relly-sc.unityrframework.samples/     # Samples 包
├── Tests/                                        # 核心框架测试
└── Tools/                                        # 发布脚本和辅助工具
```

发布后对应两个仓库：

```text
UnityRFramework
└── com.relly-sc.unityrframework

UnityRFramework-Samples
└── com.relly-sc.unityrframework.samples
```

开发仓库负责联调和持续开发；发布仓库只保存可供用户安装的包内容。

## 3. 开发期目录

开发期不拆仓库，不通过远程 Git URL 引用自己的包。开发工程通过本地路径引用两个包：

```json
{
  "dependencies": {
    "com.relly-sc.unityrframework": "file:../../Packages/com.relly-sc.unityrframework",
    "com.relly-sc.unityrframework.samples": "file:../../Packages/com.relly-sc.unityrframework.samples"
  }
}
```

因此框架和 Samples 的修改可以立即在同一个 Unity 工程中验证。开发期可以使用 `Samples/` 目录；发布时再转换为 Unity UPM 约定的 `Samples~/` 目录。

```text
Packages/com.relly-sc.unityrframework/
├── package.json
├── Runtime/
├── Editor/
└── Tests/

Packages/com.relly-sc.unityrframework.samples/
├── package.json
├── Editor/
└── Samples/
    ├── Sample.Demo/
    ├── Sample.Download/
    ├── Sample.Storage/
    ├── Sample.UI/
    ├── Expansion.UI/
    ├── Expansion.YooAsset/
    └── Expansion.HybridCLR/
```

## 4. 包边界

核心框架包只包含框架运行所需的 Library、Runtime、Editor、核心测试、文档和许可证文件。不得包含 Demo 场景、示例资源、`Sample.*` 代码、`Expansion.*` 代码或示例配置产物。

Samples 包集中保存当前由项目维护者提供的全部示例和可选扩展。它可以依赖核心框架包和必要的第三方包，核心框架不能反向依赖 Samples 包。

```text
Samples 包
    ↓
核心框架包
    ↓
Unity 和必要的第三方包
```

核心框架中的编辑器工具只提供“安装 Samples”入口，不复制和维护 Samples 文件。

## 5. 发布流程

发布时从开发仓库生成两个目录：

```text
Release/
├── com.relly-sc.unityrframework/
└── com.relly-sc.unityrframework.samples/
```

发布脚本复制两个包、将 Samples 包中的 `Samples/` 转换为 `Samples~/`，校验版本和依赖，然后分别推送到两个发布仓库。

两个仓库使用相同版本号和 Tag：

```text
UnityRFramework          1.4.0
UnityRFramework-Samples  1.4.0
```

开发期不要求每次修改都发布版本；只有对外发布时才创建对应 Tag。

## 6. 用户安装流程

用户先安装核心框架包，安装结果不包含 Samples。需要示例时，在框架菜单中点击“安装 Samples”，使用固定版本的 Samples Git URL：

```text
https://github.com/<组织或账号>/UnityRFramework-Samples.git#1.4.0
```

Unity Package Manager 负责下载和解析包。第一版只需要一个安装按钮，不实现在线市场、用户投稿、审核、评分或付费功能。

## 7. 版本兼容规则

Samples 与核心框架保持同版本发布：

```text
核心框架 1.4.0  ↔  Samples 1.4.0
```

Samples 的 `package.json` 声明兼容的核心框架版本。安装入口默认指向匹配 Tag，不直接指向 `main` 或 `develop`。开发期允许使用未发布代码，发布期必须通过版本号和 Tag 固定关系。

## 8. 实施顺序

当前安全防护实施计划完成后，按以下顺序整改：

1. 在现有仓库内创建 Samples 包目录。
2. 将当前 `Samples/` 内容迁移到 Samples 包，保持命名空间和资源引用不变。
3. 创建 `Project/` 开发工程，改用两个本地包路径引用。
4. 验证框架、Samples、第三方扩展和现有验收场景。
5. 添加发布脚本，生成两个发布目录。
6. 创建 `UnityRFramework-Samples` 发布仓库并推送首个匹配版本。
7. 从干净工程验证“只安装框架”和“再安装 Samples”。
8. 在核心框架 Editor 中加入最小的 Samples 安装入口。
9. 更新 README、安装说明和版本发布流程。

## 9. 明确不做的事情

本方案暂不实施 Git 子模块作为用户安装方式、UPM 私有服务器、自建 Registry、第三方用户上传、每个 Sample 独立仓库、在线市场、审核、评分和付费系统，也不要求开发期远程安装自己的 Samples 包。

## 10. 验收标准

- 开发工程可以同时本地引用核心框架和 Samples。
- 框架与 Samples 可以在同一分支中联调。
- 核心框架发布包不包含 Samples 内容。
- 用户只安装核心框架时没有 Demo 和示例资源。
- 用户安装 Samples 后可以正常导入并运行现有示例。
- 核心框架和 Samples 使用相同版本 Tag，依赖关系可追溯。
- 从干净工程完成一次安装和基本运行验证。
