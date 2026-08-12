# Third-Party Notices

UnityRFramework 原创代码以项目根目录中的 Apache License 2.0 授权。以下第三方内容仍分别
适用其原许可证；UnityRFramework 的 Apache-2.0 不替代这些许可证。

## 随包分发的第三方内容

### ExcelDataReader 3.9.0

- 组件：`ExcelDataReader.dll`、`ExcelDataReader.DataSet.dll`
- 用途：Expansion Sample 的 Editor Excel 导表工具
- 版权所有：Copyright (c) 2014 ExcelDataReader
- 许可证：MIT License
- 来源：https://github.com/ExcelDataReader/ExcelDataReader
- 完整许可证：`Samples/Expansion.ExcelDataReader/Plugins/ExcelDataReader/LICENSE.txt`
  （UPM 包中对应 `Samples~/Expansion.ExcelDataReader/Plugins/ExcelDataReader/LICENSE.txt`）

### SharpZipLib 1.4.2

- 组件：`ICSharpCode.SharpZipLib.dll`
- 用途：Expansion.SharpZipLib Sample 的可选 ZIP 解压辅助器
- 版权所有：Copyright (c) 2000-2018 SharpZipLib Contributors
- 许可证：MIT License
- 来源：https://github.com/icsharpcode/SharpZipLib/tree/v1.4.2
- 完整许可证：`Samples/Expansion.SharpZipLib/Plugins/SharpZipLib/LICENSE.txt`
  （UPM 包中对应 `Samples~/Expansion.SharpZipLib/Plugins/SharpZipLib/LICENSE.txt`）

### Noto Sans SC

- 文件：`NotoSansSC-Regular.ttf`、`NotoSansSC-Medium.ttf`、`NotoSansSC-Bold.ttf`
- 用途：Demo 中文字体
- 版权所有：Copyright 2014-2021 Adobe，保留字体名称 `Source`
- 许可证：SIL Open Font License 1.1
- 来源：https://github.com/notofonts/noto-cjk
- 完整许可证：`Samples/Sample.Demo/GameAssets/Fonts/Noto_Sans_SC/OFL.txt`
  （UPM 包中对应 `Samples~/Sample.Demo/GameAssets/Fonts/Noto_Sans_SC/OFL.txt`）

## 包管理器依赖

以下内容不复制到 UnityRFramework 目录，由 Unity Package Manager 安装并保留其自身
许可证文件。

### Newtonsoft.Json for Unity 3.2.1

- 包名：`com.unity.nuget.newtonsoft-json`
- 用途：可选 `NewtonsoftJsonHelper`
- 包许可证：Unity Companion License
- 包内第三方组件：Newtonsoft.Json、Json.Net.Unity3D、Newtonsoft.Json-for-Unity、
  com.newtonsoft.json，均为 MIT License
- 来源：https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json@3.2/manual/index.html
- 安装后完整声明：包目录中的 `LICENSE.md` 与 `Third Party Notices.md`

## 可选 Expansion 集成

以下依赖不会由 UnityRFramework 的 `package.json` 自动安装。只有导入并使用相应
Expansion Sample 时才需要安装。

### YooAsset 3.0.5

- 用途：`YooAssetResourceHelper`
- 版权所有：Copyright 2018-2021 何冠峰；Copyright 2021-2026 TuYoo Games
- 许可证：Apache License 2.0
- 来源：https://github.com/tuyoogame/YooAsset/tree/3.0.5
- 安装后完整许可证：YooAsset 包目录中的 `LICENSE.md`

### UniTask 2.5.11

- 用途：`UniTaskWebRequestHelper`
- 维护者：Cysharp, Inc.
- 许可证：MIT License
- 来源：https://github.com/Cysharp/UniTask
- 完整许可证：https://github.com/Cysharp/UniTask/blob/master/LICENSE

## 设计参考项目

README 中列出的 GameFramework、UniFramework、TEngine 等仓库仅作为架构参考，不作为
第三方源码或二进制随 UnityRFramework 分发，因此不属于本文件中的随包组件。
