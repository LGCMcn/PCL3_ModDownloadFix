# Plain Craft Launcher (PCL) LGCMcn 下载修复版

LGCMcn 基于 PCL 公开代码独立修改的第三方版本。原作者为龙腾猫跃及原项目贡献者；原许可和署名保留。本版本与官方无隶属关系。

恢复 Minecraft 下载入口，支持 Mojang／BMCLAPI、Modrinth／CurseForge 的独立配置；补齐公开联机代码的本地配置、解压和 CLI 调用。由于原 MCIM 镜像实测停服，默认直接访问官方服务。

将 `PCL.Services.example.json` 复制为运行程序旁的 `PCL.Services.json`。微软登录应用标识、CurseForge API Key 由使用者在本地配置，不嵌入源码。联机可使用 `PCL.Link.example.json` 或程序内置配置，并填写自己的可用自定义节点。

使用 .NET SDK 9、Visual Studio .NET 桌面构建工具和 .NET Framework 4.8 Targeting Pack，运行 `build.ps1`。MeloongCore 为来源仓库固定的 98157d6 版本。

正式版私有赞助功能、原自动更新及云端服务并非完整公开，不能宣称所有功能与正式版完全一致。详细验证记录随成品交付。源码仓库：https://github.com/LGCMcn/PCL3_ModDownloadFix
