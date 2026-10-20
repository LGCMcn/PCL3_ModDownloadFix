# Plain Craft Launcher (PCL) LGCMcn 下载修复版

**第三方版本声明：原项目作者仍为 PCL 官方（龙腾猫跃及原项目贡献者）。本版本由 LGCMcn 独立修改，第三方修改与 PCL 官方及原作者无关，不代表 PCL 官方发布。**

这是 LGCMcn 基于 PCL 公开源码独立修改的第三方版本，原作者为龙腾猫跃及原项目贡献者。本版本与 PCL 官方无隶属关系。

## 直接使用

1. 将 Release 中的 `PCL.DownloadFix.Windows-x64.zip` 完整解压，双击 `Plain Craft Launcher (PCL) LGCMcn 下载修复版.exe`。
2. 首次使用时，由你本人阅读并决定是否接受原项目协议。
3. 在“下载”中选择 Minecraft 版本，或搜索 Mod。Mod 建议先将来源选为 Modrinth。
4. 下载源仍可在“设置 → 其他”中选择官方源或镜像源。

适用于 64 位 Windows，需要 .NET Framework 4.8。当前成品附带 x64 EasyTier 组件。启动器仍会自动查找／下载所需 Java，本次未实际验证完整游戏启动。

## 项目版本与自动更新

打开“更多 → 关于与鸣谢”，自动显示本机第三方版本、本项目最新正式发行版和更新内容。`[ + ]` 表示新增功能，`[ - ]` 表示修复问题。“检查更新”可重新获取；不下载草稿和预发行版。

发现更新后，点击“下载并更新”：从本项目 GitHub 发行版下载固定运行包，核对大小、SHA256、包内逐文件哈希及版本，再退出程序、备份旧文件、替换和重新启动。请等下载、登录和游戏启动任务完成。网络受限时可打开“发行版详情”手动下载；不使用不明代理或要求管理员权限。

`PCL.Update.exe` 必须与主程序一起解压。个人 `PCL.Services.json`、`PCL.Link.json`、账号和游戏目录不会被覆盖。旧程序备份位于程序目录 `.pcl-update-backups`；失败会尝试恢复，日志位于备份目录和 `%LOCALAPPDATA%/PCLDownloadFix/Updates`。断电、磁盘损坏或权限改变可能仍需从备份手动恢复。更新不是安全担保，维护者 GitHub 账号和发行文件仍需妥善保护。

## Windows 个性化与版本标识

本版遵循上游指南固定使用蓝色主色调，不提供免费赞助主题、自由主色或彩虹换色。旧主题设置会回到蓝色。背景图片、背景音乐、标题栏设置及自定义主页继续保留。主页 PCL 标识旁显示“第三方版本 v1.2.3”，正式名称保留 Plain Craft Launcher (PCL) 前缀与 LGCMcn 第三方后缀。

## 界面效果与工具箱

在“设置 → 个性化”或“更多 → 百宝箱”可以独立开关毛玻璃、启动动画、错误提示动画、图标状态动画、页面与导航动画、平滑滚动、关闭动画和最小化动画。毛玻璃默认开启，只模糊背景层；文字保持清晰。关闭系统动画或启用高对比度时减少效果；低渲染能力设备使用轻量背景。无需联网获取图标。Windows 版继续保持蓝色主调，不提供赞助主题换色。

关闭动画约 180 毫秒，窗口缩至 94% 并淡出；最小化约 380 毫秒，窗口快照先收窄、再向本程序任务栏按钮中心变形收拢，结束后转入真实最小化状态。使用 Windows UI Automation 读取属于本程序的按钮边界，终点对齐按钮中心；查询在后台执行并有等待上限，不调用 macOS 私有接口。替代任务栏、系统未提供按钮位置或超时时，退回任务栏边缘。恢复时显示原窗口，不改变保存的尺寸和透明度。远程会话或低渲染能力时直接最小化，定位不可用且自动隐藏未保留工作区时回退到屏幕底边。

顶部选中胶囊在独立绘制线程运行，采用约 210 毫秒的柔和加减速；页面忙碌时仍继续移动。文字和图标随胶囊覆盖区域改变颜色。关闭“页面与导航动画”后直接定位；组件卸载后退出绘制线程，不创建常驻逐帧计时器。

新增开关立即生效。启动动画等需要重启的设置，在手动勾选后通过左下角提示“设置已保存，下次启动生效”；打开设置页不会重复弹出。滚轮动画中反向操作会抛弃原方向待滚距离，拖动滚动条、按键滚动、返回顶部和卸载页面会结束旧动画。

工具箱按需读取本机系统、架构、处理器、物理内存、显卡驱动及程序所在磁盘容量，不读取序列号、MAC 或账号标识，不上传设备配置。没有常驻硬件监控循环，可手动刷新；显卡信息受系统 WMI 和权限影响。

动画结束或卸载时释放动画时钟；200 次背景切换和 100 次错误动画的对象回收检查通过。这不等于对全部硬件、驱动、第三方 Mod 和长时间运行的无泄漏／无卡顿保证。遇到渲染问题时可先关闭相关效果。

## 微软多账号管理

在“设置 → 个性化 → 微软多账号管理”打开管理窗口；登录页和已登录账号的信息菜单也有入口。支持添加并网页登录、切换选中账号、单独移除本机记录。正在登录或启动游戏时暂不允许切换，避免两条登录流程同时覆盖令牌。

同一玩家改名后按 UUID 更新记录；不同 Client ID 的授权记录分开保存。更换应用 Client ID 后需重新授权，不会把旧令牌发给另一应用。删除本机记录不会删除微软账号，也不等于服务端撤销授权；需要时请在微软账户的应用权限页面撤销授权。

新增账号列表保存在当前 Windows 用户的 LocalAppData/PCLDownloadFix/Accounts，按启动器安装目录分开，用 Windows DPAPI 加密。请勿把凭据文件提交到仓库；迁移到其他 Windows 用户／电脑时请重新登录。原有旧账号下拉列表保留兼容，不向其他启动器导入凭据。

## 更多资源来源

资源搜索页的“全部”会使用已配置的来源。无 CurseForge Key 时默认使用 Modrinth，页面会显示原因；不是只有一个来源筛选选项。

- Modrinth：可直接搜索、选择版本并下载，无需 Key。
- CurseForge：申请审核通过后填写本项目自己的 Key；没有 Key 的情况下不保证独占资源及整合包依赖可用。
- GitHub 发布目录：输入关键词，或直接输入 `CaffeineMC/sodium` 这样的作者／仓库名。支持查看最新发布的 JAR、ZIP、MRPACK，选择保存位置，校验大小和来源提供的 SHA256。不会自动运行或安装；需自己选择游戏版本及加载器。匿名搜索存在限流，直接输入仓库名可减少搜索请求。
- FTB 官方整合包目录：搜索整合包、浏览简介并打开官方安装入口。目前是目录浏览，不是本启动器内的 FTB 自动安装器。

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
- 官方赞助等级为 None，不重建赞助解锁体系或类似表现；Windows 主色固定为蓝色。
- LiteLoader 等旧服务、第三方自定义主页的可用性，仍由对应运营者决定。

详细调用链、修改内容和验证边界见[项目分析与验证](analysis.md)。源码在本仓库及 Release 的 `PCL.DownloadFix.Source.zip`，保留原许可和署名。


## 维护者需要完成的申请

### Microsoft Client ID 与 Minecraft 接入

1. 用你自己的账号进入 [Microsoft Entra 管理中心](https://entra.microsoft.com/)，进入“应用注册 → 新注册”。需要可注册应用的租户／账号权限，具体条件以微软页面为准。
2. 应用名称建议填写 `Plain Craft Launcher (PCL) LGCMcn DownloadFix`；账号类型选择支持个人 Microsoft 账号的类型。记录 **Application (client) ID**，不是 Directory ID、Object ID 或 Client Secret。
3. 在 Authentication／高级设置中启用公共客户端流程（Allow public client flows）。本版使用设备代码授权，无需把 Client Secret 放进桌面程序。
4. 按 [Minecraft 应用接入说明](https://aka.ms/AppRegInfo) 的当前要求申请本项目的 API 使用资格。Entra 注册完成不等于 Minecraft Services 已允许该 Client ID。若最终步骤返回 `Invalid app registration`，需等待或核实接入许可，不是简单更换网络就能解决。
5. 在启动器“独立服务配置”界面填写 Client ID，保存后重启，再用持有正版 Minecraft 的账号在微软网页完成授权。维护者配置好项目级应用后，普通用户不需要各自注册应用。

可直接用于申请的项目介绍（联系邮箱请填你自己的）：

> Project: Plain Craft Launcher (PCL) LGCMcn DownloadFix. Repository: https://github.com/LGCMcn/PCL3_ModDownloadFix. This is an independently maintained third-party Windows desktop launcher based on publicly available PCL source, retaining upstream attribution and licensing conditions. Authentication uses Microsoft's public-client device authorization flow, Xbox Live/XSTS and Minecraft Services. Users sign in on Microsoft-hosted pages with their own accounts; the launcher does not collect passwords or bypass game ownership checks. Refresh tokens are stored locally with Windows user-bound encryption. Please authorize our own application Client ID for Minecraft Java Edition authentication. Contact: [your email].

技术资料：[微软设备代码流程](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-device-code)、[注册应用](https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-register-app)。

### CurseForge API Key

1. 阅读 [官方 Key 申请说明](https://support.curseforge.com/support/solutions/articles/9000208346-about-the-curseforge-api-and-how-to-apply-for-a-key)，从说明页进入第三方开发者申请表。
2. 申请由项目维护者提交，填写自己的联系邮箱、仓库链接、功能描述、预期用途，并亲自阅读和确认 API 条款。具体审批及使用条件由服务方决定。
3. 获批后，在启动器的服务配置界面填写 Key，保存并重启。界面不显示已有 Key，新值会用当前 Windows 用户加密存储。环境变量 `PCL_CURSEFORGE_API_KEY` 继续优先。不要把 Key 发到聊天、README、源码或 Release。
4. GUI 会保存 `CurseForgeApiKeyProtected`；旧的明文字段仍兼容读取，但不建议在分发目录存放个人 Key。公开配置模板保持空 Key。

可用于申请的功能描述：

> A third-party Windows Minecraft Java launcher maintained by LGCMcn. We request API access to search CurseForge projects, display project and version metadata, resolve dependencies, and download files when the author's distribution settings allow it. Source repository: https://github.com/LGCMcn/PCL3_ModDownloadFix. The launcher preserves source attribution and author controls, does not scrape private content or bypass disabled distribution, and never sends the API key to Modrinth, GitHub, FTB or unrelated redirected hosts. We request guidance on approved key deployment for a public desktop launcher. Contact: [your email].

无需为 Modrinth、匿名 GitHub 公开项目目录或 FTB 官方公开目录注册账户。

## 安全风险与保护范围

| 风险 | 当前措施与边界 |
| --- | --- |
| 登录页钓鱼或令牌被代理截取 | 设备登录网址限定为微软官方 HTTPS 域名；始终验证证书；密码只在微软网页填写。仍应核对网页显示的应用名称与授权内容 |
| 本地账号缓存泄露 | 新保存的凭据与账号列表使用 Windows DPAPI CurrentUser；旧 DES 设置兼容读取，新写入使用 DPAPI。相同 Windows 用户下的恶意软件或管理员仍可能读取内存／解密数据 |
| API Key 泄露 | 配置界面加密存储；导出使用公开字段允许列表；带凭据请求不使用公共磁盘缓存；重定向重新核对目标，不跨站转发 Key。公开桌面程序无法替维护者永久保密一个共享 Key，应按服务方批准的部署方式使用 |
| 恶意 Mod 或整合包 | 下载不等于安全，Mod 可获得游戏进程权限。GitHub 目录排除 EXE，只保存文件不自动执行；显示作者仓库并做大小／哈希校验，仍需自行判断作者与内容 |
| 自定义主页 | 原有 XAML 主页能力可触发程序功能或命令，不应加载陌生人的远程主页。此能力保留，不能按普通图片看待 |
| 第三方镜像、公共联机节点 | 镜像和节点由各自运营者管理；自定义 API 地址意味着信任该服务。联机组件可能需要系统网络权限；公共节点和公网互通不在本次端到端验收范围 |
| 误报与软件签名 | 构建、代码测试或单次查杀不能保证任意环境绝对无风险。未购买或伪造代码签名证书；Windows 可能显示未签名程序提示 |

维护者承诺不主动植入病毒、木马、挖矿、盗号或隐蔽后门，源码公开接受检查。该承诺仅针对本第三方项目，不为外部 Mod、下载站或第三方依赖作绝对安全担保。具体本地安全扫描与代码测试证据见本次 Release。
