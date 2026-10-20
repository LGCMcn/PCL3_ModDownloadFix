# Plain Craft Launcher (PCL) LGCMcn 下载修复版

> **第三方版本声明：** 本项目是 LGCMcn 独立制作的第三方修改版本。原项目作者仍为 PCL 官方（龙腾猫跃及原项目贡献者）。第三方修改与 PCL 官方及原作者无关，**本版本不代表 PCL 官方发布，不是官方发行版**。完整声明见 [THIRD_PARTY_NOTICE.md](THIRD_PARTY_NOTICE.md)。

基于 PCL 公开源码独立修改的第三方版本，恢复 Minecraft 下载入口，并将可替代的服务依赖改为本地配置。本版本与 PCL 官方无隶属关系。

原作者：[龙腾猫跃](https://github.com/LTCatt) · [赞助原作者](https://meloong.com/afd/a/LTCat) · [原项目](https://github.com/Meloong-Git/PCL)。保留原许可、署名和正版购买劝导。

## 下载运行版

前往 [最新 Release](https://github.com/LGCMcn/PCL3_ModDownloadFix/releases/latest)，下载 **`PCL.DownloadFix.Windows-x64.zip`**，完整解压后运行其中的 EXE。请保留旁边的 `.exe.config`、JSON 配置及“组件”文件夹。

- 系统：64 位 Windows，.NET Framework 4.8。
- `PCL.DownloadFix.Source.zip` 是包含固定版本 MeloongCore 的完整源码包。
- `SHA256SUMS.txt` 用于检查下载文件的 SHA256。

## 本次修改

- 恢复 Minecraft 下载、整合包安装等原本受限制的入口，保留官方源和 BMCLAPI 切换。
- Modrinth 默认使用官方 API；支持自行配置 CurseForge API Key 和兼容镜像。
- 新增 `PCL.Services.json` 本地配置，支持微软 Client ID、CurseForge Key 和服务地址；不在源码或构建中嵌入个人凭据。
- 联机读取本地配置，补齐 EasyTier 解压和 CLI 调用，恢复联机入口；x64 运行包附带校验哈希的官方组件。
- 导出整合包携带当前修复版，不导出 CurseForge Key。
- Minecraft 新闻预设改为官方版本信息和官方新闻链接；更新入口指向本仓库 Releases。

## 使用前需要知道

**本版本不保证与 PCL 官方正式版所有功能完全一致。** 公开源码缺少的官方私有赞助功能和原自动更新没有重建。

微软正版登录需要自己的、具备相关权限的公共客户端应用 Client ID。CurseForge 官方接口和文件下载需要自己的 API Key；原 MCIM 镜像在本次验证中已返回停服提示，因此最终版默认不使用该镜像。没有 Key 时可使用 Modrinth 来源。

34 项程序检查通过，验证了 Modrinth 搜索、启动器自身下载引擎下载 Mod 和 Minecraft 客户端文件，以及大小和 SHA1 校验。界面启动与 EasyTier 本机两个节点连接也通过验证。完整游戏启动、全部加载器安装和跨电脑公网联机尚未完成验收。

配置步骤、服务状态和验证边界：

- [使用说明](docs/usage.md)
- [项目分析与验证](docs/analysis.md)
- [原项目 README](docs/UPSTREAM_README.md)

## 从源码构建

需要 .NET SDK 9、Visual Studio 的 .NET 桌面构建工具，以及 .NET Framework 4.8 Targeting Pack。

```powershell
git clone --recurse-submodules https://github.com/LGCMcn/PCL3_ModDownloadFix.git
cd PCL3_ModDownloadFix
.\build.ps1 -Configuration Release
.\tests\run-smoke-tests.ps1
```

输出：`Plain Craft Launcher 2/bin/Plain Craft Launcher 2.exe`。将配置模板复制到 EXE 旁后使用，或使用 Release 中已打包的运行版。

请勿将个人 `PCL.Services.json`、Key、账号缓存或游戏目录提交到仓库。许可与二次创作要求见 [LICENCE](LICENCE)。
