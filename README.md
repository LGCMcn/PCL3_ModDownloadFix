# Plain Craft Launcher (PCL) LGCMcn 下载修复版

> **第三方版本声明：** 本项目由 LGCMcn 基于 PCL 公开源码独立修改。原项目作者仍为 **PCL 官方（龙腾猫跃及原项目贡献者）**。第三方修改与原作者及 PCL 官方无关，**不代表 PCL 官方发布**。保留原署名与 [LICENCE](LICENCE)，详见 [第三方声明](THIRD_PARTY_NOTICE.md)。

原作者：[龙腾猫跃](https://github.com/LTCatt) · [赞助原作者](https://meloong.com/afd/a/LTCat) · [原项目](https://github.com/Meloong-Git/PCL)。

## 版本与下载

当前第三方版本：**v1.2.0**，基于公开源码版本 **2.99.0.0**；这两种版本号分别表示第三方维护版本和上游源码版本。

- [GitHub 最新发行版](https://github.com/LGCMcn/PCL3_ModDownloadFix/releases/latest)：运行包 `PCL.DownloadFix.Windows-x64.zip`，完整源码 `PCL.DownloadFix.Source.zip`。
- [NAS 备用下载](https://ug.link/lgcm/filemgr/share-download/?id=3fb99fd396a54ed0835d5300e18540ce)：帮助新手获取文件和改善下载体验。NAS 由维护者手动同步，版本以 GitHub 为准；请核对同一发行版的 `SHA256SUMS.txt`。
- 适用于 64 位 Windows，需要 .NET Framework 4.8。解压运行时保留 EXE 旁的配置与“组件”文件夹。

| 第三方版本 | 内容 | 状态 |
| --- | --- | --- |
| v1.0.0 | Minecraft／Mod 下载修复、本地服务配置、EasyTier 组件 | 历史版本 |
| v1.1.0 | 曾提供主题换色 | 已撤回；Windows 主题表现不符合上游指南，不再分发 |
| v1.2.0 | 固定蓝色、多账号、微软认证修复、GitHub／FTB 目录、凭据保护 | 当前版本 |

## 下载流程演示

以下视频展示已有下载流程，不代表已完成微软登录或新版全部功能验收。

https://github.com/user-attachments/assets/038b78dc-8733-42c9-af97-6c9f67c0e2af

## 本次功能

- **遵循上游指南**：Windows 界面固定蓝色，移除免费主题换色及类似赞助主题的表现；保留原有背景图片、背景音乐、标题栏和自定义主页。
- **第三方版本标识**：主页标题栏与关于页显示第三方身份及 v1.2.0；原作者署名、赞助链接和 Minecraft 正版购买劝导保留。
- **微软登录代码修复**：设备授权、令牌轮询和刷新使用一致的微软端点，遵循轮询间隔，识别应用未获接入许可等错误。**仍需本项目自己的 Client ID 与 Minecraft API 授权，目前尚未完成真实账号登录验收。**
- **微软多账号管理**：保存多个账号，按 Minecraft UUID 区分，支持切换、添加和单独移除本机记录；令牌使用当前 Windows 用户加密。
- **资源来源**：Modrinth 可直接搜索下载；CurseForge 需维护者申请 Key；新增无需 Key 的 GitHub 发布目录及 FTB 官方整合包目录。
- GitHub 目录支持项目搜索、作者仓库定位和下载 JAR／ZIP／MRPACK，校对大小及服务端提供的 SHA256，不自动运行下载文件。FTB 目录支持搜索和官方安装入口，**尚未实现 FTB 包自动安装**。
- 保留此前服务配置、整合包导出、新闻替代与联机改动。带账号凭据的请求不使用公共磁盘缓存，跨站重定向不会转发 API Key。

## 源码与实现

本项目当前运行代码、构建脚本、测试与依赖源码均可查看和自行构建。源码公开仍受原 [LICENCE](LICENCE) 约束，并不表示可以任意解除上游限制。

| 部分 | 语言／技术 | 实现方式 |
| --- | --- | --- |
| 启动器界面与主体 | VB.NET、WPF／XAML、.NET Framework 4.8 | 事件驱动界面、原有 Minecraft 启动及下载流程 |
| 辅助组件 | C# 13、MeloongCore、PCLCS | 公共文件、网络、界面与平台能力 |
| 微软账号 | OAuth 2.0 Device Authorization | 浏览器授权、轮询、刷新、Xbox Live／XSTS／Minecraft 正版链路 |
| 多账号存储 | Windows DPAPI CurrentUser | UUID 与 Client ID 分组，凭据绑定 Windows 用户；旧设置兼容读取，新写入加密 |
| 下载与资源目录 | HTTPS REST、JSON | Modrinth／CurseForge 原搜索；GitHub、FTB 独立目录 |
| 文件校验 | 文件大小、SHA1／SHA256 | 根据来源元数据核对；哈希校验不能证明第三方内容无恶意 |
| 构建与复核 | PowerShell、Python、GitHub Actions | 构建、加载实际程序集测试、打包与发行文件哈希 |

具体调用链、计算与边界见 [项目分析](docs/analysis.md)，申请与填写步骤见 [使用说明](docs/usage.md)。

## 安全与源码检查声明

维护者 LGCMcn 承诺：**本第三方版本不主动植入病毒、木马、挖矿程序、盗号功能或隐蔽后门。源码公开，欢迎随时检查和复核。**

维护者表态：**“藏毒作者迪克爆炸。”**

声明属于维护者对本项目的安全承诺，不构成对任意电脑环境、第三方 Mod、下载站或未来依赖的绝对安全保证。请从本仓库发行页获取文件，核对源码提交和 SHA256。安全检查范围、凭据保护与剩余风险见 [使用说明](docs/usage.md#安全风险与保护范围)。

## 构建与测试

需要 .NET SDK 9、Visual Studio 的 .NET 桌面构建工具和 .NET Framework 4.8 Targeting Pack。

```powershell
git clone https://github.com/LGCMcn/PCL3_ModDownloadFix.git
cd PCL3_ModDownloadFix
.\build.ps1 -Configuration Release
.\tests\run-smoke-tests.ps1
# 可选：实际查询与下载校验
.\tests\run-smoke-tests.ps1 -Network -ExtraNetwork
```

输出：`Plain Craft Launcher 2/bin/Plain Craft Launcher 2.exe`。当前发行的具体测试结果与源码提交号见 Release。微软真实账号登录、全部加载器、完整游戏启动和跨电脑公网联机仍有未完成验收的范围。

[使用说明](docs/usage.md) · [实现与验证](docs/analysis.md) · [演示说明](docs/promo.md) · [原项目 README](docs/UPSTREAM_README.md)
