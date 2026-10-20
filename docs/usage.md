# Plain Craft Launcher (PCL) LGCMcn 下载修复版

**第三方版本声明：原项目作者仍为 PCL 官方（龙腾猫跃及原项目贡献者）。本版本由 LGCMcn 独立修改，第三方修改与 PCL 官方及原作者无关，不代表 PCL 官方发布。**

这是 LGCMcn 基于 PCL 公开源码独立修改的第三方版本，原作者为龙腾猫跃及原项目贡献者。本版本与 PCL 官方无隶属关系。

## 直接使用

1. 将 Release 中的 `PCL.DownloadFix.Windows-x64.zip` 完整解压，双击 `Plain Craft Launcher (PCL) LGCMcn 下载修复版.exe`。
2. 首次使用时，由你本人阅读并决定是否接受原项目协议。
3. 在“下载”中选择 Minecraft 版本，或搜索 Mod。Mod 建议先将来源选为 Modrinth。
4. 下载源仍可在“设置 → 其他”中选择官方源或镜像源。

适用于 64 位 Windows，需要 .NET Framework 4.8。当前成品附带 x64 EasyTier 组件。启动器仍会自动查找／下载所需 Java，本次未实际验证完整游戏启动。

## 哪些已经验证

- 成功构建 Release 程序，实际启动界面正常。
- 通过启动器自己的 HTTP 客户端查询 Modrinth。
- 通过启动器自己的多线程下载引擎下载 Mod 和 Minecraft 1.0 客户端 JAR，文件大小与 SHA1 匹配。
- 配置覆盖、密钥发送目标、导出配置排除私密字段等运行检查通过。
- EasyTier 官方固定版本组件在本机两个测试节点间成功建立连接，CLI 输出格式兼容。

“客户端下载通过”代表已验证文件下载链路，不等于已经完成所有游戏版本安装、全部加载器安装和游戏启动验收。

## 微软正版登录

编辑启动器旁的 `PCL.Services.json`，将自己的、具备相应 Xbox／Minecraft API 权限且支持公共客户端设备代码登录的应用 Client ID 填入 `MicrosoftClientId`，保存后重启。环境变量 `PCL_MS_CLIENT_ID` 的优先级更高。

Client ID 是应用标识；不要填写账号密码、Client Secret、访问令牌或刷新令牌。本版本保留原有微软／Xbox／Minecraft 正版认证流程，不绕过正版验证。由于没有你的应用配置和账号，本次未验证实际登录及令牌刷新。普通游戏下载和离线账号无需此项。

## CurseForge 下载

此次实测，原分支使用的 MCIM 镜像返回 HTTP 503，页面显示“MCIM 暂时停止服务”。最终版因此默认直接访问官方服务，`ModMirrorBase` 默认为空。

CurseForge 官方 API 需要你自己的 API Key；其官方公告还说明 CDN 文件下载从 2026 年 7 月 16 日起要求有效 Key。

- 有自己的 Key：在本地 `PCL.Services.json` 的 `CurseForgeApiKey` 中填写，保存重启。也可用 `PCL_CURSEFORGE_API_KEY` 环境变量。
- 有其他可用兼容镜像：将其 HTTPS 根地址填到 `ModMirrorBase`。镜像需提供 `/modrinth/v2/...`、`/curseforge/v1/...` 和对应文件路径。
- 无 Key 且无可用镜像：使用 Modrinth 来源。CurseForge 独占项目及依赖这些项目的整合包仍无法保证下载。

不要把个人 Key 提交到 GitHub。附带的源码不包含任何真实凭据。整合包导出仅保留允许公开的服务配置字段，不导出 CurseForge Key。

## 自定义下载服务

可以在“设置 → 其他 → 独立服务配置”打开配置文件，保存后重启生效。

| 字段 | 用途 |
|---|---|
| `MicrosoftClientId` | 你自己的微软公共客户端应用标识 |
| `CurseForgeApiKey` | 你自己的 CurseForge API Key |
| `BmclApiBase` | BMCLAPI 兼容服务根地址 |
| `ModMirrorBase` | Mod 信息／文件兼容镜像；空字符串表示不使用 |
| `ModrinthApiBase` | Modrinth 兼容 API 根地址 |
| `CurseForgeApiBase` | CurseForge 兼容 API 根地址 |

服务地址使用 HTTPS，不带用户名密码、查询参数或片段。程序在本机读取配置，无需 PCL 配置服务器。

## 联机

联机配置改为读取旁边的 `PCL.Link.json`，不再请求 PCL 配置服务器。已修复原公开代码中被注释的解压步骤和 CLI 查询步骤，并恢复原本强制隐藏的联机入口。

“组件”文件夹附带官方 EasyTier 2.6.4 x64 压缩包，程序校验 SHA256 后使用。其他架构按配置从 EasyTier 官方 GitHub 下载。

公共节点仍是外部服务，不保证长期可用。原公共节点列表域名此次无法解析，源码中保留的回退节点只确认了 DNS 解析，未完成公网连通性验证。更可靠的做法是在“联机 → 设置 → 自定义节点”填写自己可用的节点，房主和加入者使用相同配置。也可修改 `PCL.Link.json` 的节点地址与 `NodesApi`。需要将原生联机端口、驱动和权限问题交由实际网络环境验证。

本次的两个节点测试是在同一台电脑、不创建 TUN 网卡的条件下进行；尚未验证两台电脑跨网络联机或 Minecraft 房间加入。

## 与官方正式版的差别

公开仓库本来就不包含官方更新、赞助验证及部分云端功能的完整实现。这次恢复的是公开源码可实现的下载和服务替代路径，不能宣称与官方正式版所有功能完全相同。

- “检查更新”打开本分支 GitHub Releases；未实现官方原有的自动下载／替换更新。
- Minecraft 新闻预设改为直接查询 Mojang／BMCLAPI 版本信息，并链接 Minecraft 官方新闻，展示内容与原作者新闻服务不同。
- 官方赞助等级和赞助主题的公开代码仍为原来的留空实现；未重建官方服务器、密钥或赞助系统。
- LiteLoader 等旧服务、第三方自定义主页的可用性，仍由对应运营者决定。

详细调用链、修改内容和验证边界见[项目分析与验证](analysis.md)。源码在本仓库及 Release 的 `PCL.DownloadFix.Source.zip`，保留原许可和署名。
