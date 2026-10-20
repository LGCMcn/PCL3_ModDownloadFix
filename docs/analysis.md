# 项目分析与验证

记录日期：2026-10-20（Asia/Shanghai）。

## 来源与快照

仓库：https://github.com/LGCMcn/PCL3_ModDownloadFix

来源分支：main；下载归档中记录的提交：`6b9a50df911c9d68b69b24c979efba477fb14b67`。

MeloongCore 使用仓库指定的 `98157d6` 版本，已实际下载解包补齐，不使用随意取来的新版本代替。

本地保留三个阶段：v1 为原始源码及补齐的子模块；v2 为独立下载与运行配置阶段；v3 为集成修复、运行验证和最终打包阶段。v1／v2 是留档源码，最终运行程序以“成品”为准。未把仓库自带的“最新正式版.zip”冒充此次编译结果。

对于用户提及的服务器关闭原因，本次没有作事实调查；结论依据源码、实际接口响应和运行验证，不推断停服动机。

## 架构

主程序为 VB.NET + WPF，目标 .NET Framework 4.8。`PCLCS` 使用 C#，包含 Minecraft 模型、Java 与资源相关能力；`MeloongCore` 提供任务、文件、路径、日志和 WPF 基础组件。主程序集通过资源嵌入依赖库，程序启动时解析加载。

### Mod 下载调用链

1. `Pages/PageDownload/Resource/PageDownloadMod.xaml` 和 `PageResource` 提供 Mod、版本、加载器、资源来源等筛选界面。
2. `Modules/Resource/ResourceSearcher.vb` 将搜索条件分别转换为 Modrinth `/v2/search` 和 CurseForge `/v1/mods/search`，并发查询、合并去重。中文检索使用本地 WikiEntries 索引匹配名称，而不是依赖 PCL 服务器翻译每次搜索。
3. `Modules/Resource/ResourceProject.vb` 解析项目、分类、图标和支持版本；`ResourceVersion.vb` 取得项目版本列表、文件地址、文件大小、SHA1／MD5 及必要依赖。
4. `Modules/Minecraft/ModDownload.vb` 中的 `DlModRequest` 处理官方接口、镜像接口和重试；`DlSourceModGet` 生成兼容镜像 URL。
5. `ResourceVersion.ToNetFile` 将下载请求交给 `Modules/Base/ModNet.vb` 的 `NetFile`、`LoaderDownload` 和 `NetManager`。下载器支持多线程／分段、源切换、失败重试、已有文件查找、大小与哈希校验。
6. `LocalResourceLoaders.vb` 利用 Modrinth SHA1 查询和 CurseForge MurmurHash2 指纹查询实现本地 Mod 识别与更新；`ModModpack.vb` 处理整合包清单、依赖文件和后续安装任务。

因此，Mod 文件没有必须经由 PCL 服务器的固有要求。但 CurseForge 的 API Key 和可用文件服务器仍不可凭空替代；没有它们就无法保证其独占内容可获取。

### 游戏下载调用链

1. `PageDownloadInstall.xaml.vb` 负责安装选择；`ModDownload.vb` 通过 Mojang／BMCLAPI 加载版本清单、Forge／NeoForge／Fabric／OptiFine 等组件信息。
2. `Pages/PageDownload/ModDownloadLib.vb` 组装安装任务，下载版本 JSON、客户端 JAR、资源索引、资源文件和依赖库，并执行所选加载器的安装逻辑。
3. `DlSourceLauncherOrMetaGet`、`DlSourceLibraryGet` 等根据用户下载源设置排列官方与镜像地址，沿用原来的源切换设计。
4. `ModNet.vb` 负责实际下载和校验；Java 管理和游戏启动路径继续保留原实现。

原分支 `ModSecret.vb` 的 `RestrictMcDownload = True` 会关闭下载入口、整合包安装和部分相关操作。本次将其恢复为 False；原有入口都继续使用同一开关，避免只改界面而任务仍被拒绝。

## PCL 服务依赖如何替代

| 项目 | 原状态 | 最终处理 |
|---|---|---|
| 游戏版本清单 | 已有 Mojang／BMCLAPI | 官方清单改用 piston-meta 的 v2 清单，保留镜像路径和重试 |
| Modrinth | 原生接口 + MCIM | 默认直接使用官方 API，保留自定义镜像能力 |
| CurseForge | 环境变量 Key + MCIM | 本地 JSON／环境变量配置个人 Key；兼容新 CDN 鉴权 |
| 镜像地址 | 硬编码 | 新增 `ModServices.vb` 统一读取本地配置与前缀映射 |
| 请求 Referer | `*.open.pcl2.server` 字符串 | 移除该标识；它本来也不是一次服务器请求 |
| PCL 配置加载 | 公开源码为空实现 | 联机读取本地配置和内置固定版本配置 |
| 联机 | 分支检查拒绝、入口强制隐藏、解压与 CLI 调用被注释 | 补齐以上路径，保留公开节点／自定义节点逻辑 |
| EasyTier 下载 | 依赖远端配置 | 固定官方版本、SHA256，支持成品附带离线压缩包 |
| 整合包附带启动器 | 部分构建调用空的官方最新版下载函数 | 所有构建直接复制当前修复版本；只导出公开配置字段 |
| Minecraft 新闻 | 作者新闻域名 | Mojang／BMCLAPI 版本信息 + 官方新闻链接 |
| 启动器更新 | 公开源码为空 | 按钮链接本分支 Releases；没有实现自动替换更新 |
| 赞助／主题 | 公开代码缺失 | 保留原代码限制，不虚构已恢复 |

修改没有删除 Mod、整合包、资源包、光影、数据包、版本管理或离线启动相关页面。保留页面和实现路径并不代表外部服务、私有能力和所有游戏场景都已完成验收。

## 配置和文件处理

- `PCL.Services.json` 放在可执行文件旁，缺省值不需要 PCL 配置服务器。
- 微软 Client ID 和 CurseForge Key 的环境变量优先于文件，首次读取后配置缓存，修改后重启。
- 自定义服务地址验证为 HTTPS；源地址替换只匹配完整前缀，保留路径与查询参数，不替换查询参数中碰巧出现的域名。
- Key 只主动添加到配置的 CurseForge API 路径和明确列出的 ForgeCDN 主机；不主动添加到 Modrinth、MCIM 或域名后缀伪装目标。
- 导出整合包使用公开字段允许列表，排除 Key 及未知私密字段。
- 注册表根更改为 `PCLDownloadFix`，避免与官方／原开源版登录缓存冲突。每个安装目录生成自己的设备标识。
- 原署名、原许可和正版购买劝导保留；标题／关于页明确为 LGCMcn 第三方修改版。

## 实际验证

### 构建

已用 Visual Studio Build Tools 的完整 MSBuild + .NET SDK 9.0.319 + .NET Framework 4.8 工具链完成 Release 构建，并通过 `v3/build.ps1` 再次构建成功。源码使用 C# 13，因此仅有系统自带 .NET Framework MSBuild 不够。

构建输出无错误；存在原项目引用的 `BasicDesignGuidelineRules.ruleset` 缺失警告，不影响本次构建。最终源码归档已另行解压到全新目录，实际执行其中的 build.ps1 构建成功，验证交付归档具备构建所需的子模块与资源。GitHub Actions 文件使用无需嵌入凭据的构建；运行状态以本仓库 Actions 页面为准。

构建环境使用 Visual Studio .NET 桌面构建工具与 .NET SDK 9；没有向原项目添加真实凭据。

### 程序自身代码测试

`tests/SmokeTests.cs` 加载最终程序集并运行真实方法，覆盖默认配置、定制配置和错误配置：

- 默认配置及真实网络链路共 24 项通过。
- 自定义端点、环境变量优先级、导出私密字段排除共 9 项通过。
- 错误 JSON 的可操作错误提示 1 项通过。

共 34 项程序代码检查通过。测试包括实际请求头对象构造；没有使用真实个人 Key。

真实下载结果：

| 文件 | 字节数 | SHA1 |
|---|---:|---|
| Modrinth Sodium 测试 JAR | 1,912,496 | `1e6aec741b4134258ba1d7d458bae96a2e8e187e` |
| Minecraft 1.0 客户端 JAR | 2,362,837 | `b679fea27f2284836202e9365e13a82552092e5d` |

源码下载引擎中的 FileChecker 以及测试中的独立 SHA1／文件大小检查均通过。测试文件保存在 `verification/default`，没有放入成品的游戏目录。

### GUI 与联机组件

- 成品可执行文件的副本实际启动，主窗口可响应，并正确显示首次使用协议页。没有替用户点击同意。测试程序随后关闭，未更改成品首次使用状态。
- 本机联机验证脚本 启动两个官方 EasyTier 2.6.4 本机节点，未创建 TUN 网卡，建立了 TCP P2P 连接，确认 CLI JSON 中的 hostname、ipv4、cost、lat_ms、nat_type 字段兼容 PCL。
- 附带 x64 组件 SHA256：`27af91e270e554709b048bd32327fefd2dfce5062ae1e8701af7550c6f525f84`，与 GitHub 官方 release 的 digest 匹配。

### 外部接口快照

初次并发 Python 探测中，Mojang、Fabric 等有短暂中断／截断；随后 Mojang 清单重试成功，启动器自身请求和下载链路通过。初次失败没有被改写成全通过，原始记录在 `reports/service-probe.json`。

MCIM 两个 API 路径返回 HTTP 503；另取项目详情确认响应页面显示“MCIM 暂时停止服务”。最终默认禁用该镜像。EasyTier 公共节点列表域名此次无法解析，回退节点 `mc1.easytier.cn` 的 DNS 成功解析，但未宣称其公网服务已验证可用。

## 尚不能宣称完成的事项

1. 没有你的微软应用授权、账号和 CurseForge Key，因此未验证真实正版登录、令牌刷新、CurseForge 官方搜索和下载。
2. 没有执行全部 Minecraft 版本、全部加载器及全部整合包的完整安装／启动矩阵；未启动 Minecraft 游戏。
3. 没有两台电脑跨网络联机验收，未验证全部公共节点、管理员权限、驱动和 NAT 环境。
4. 官方私有赞助／更新／云端服务无法从删减源码自动恢复；本次更新与新闻采用上述替代行为，和原正式版不完全一致。

因此，本次交付是已编译并验证关键下载路径的第三方修复版，不能以“正式版全部功能完全一致／零功能差异”作为验收结论。

## 外部资料

- 原仓库：https://github.com/LGCMcn/PCL3_ModDownloadFix
- Modrinth 官方 API：https://docs.modrinth.com/api/
- CurseForge 官方 API：https://docs.curseforge.com/rest-api/
- CurseForge CDN 鉴权公告：https://blog.curseforge.com/introducing-api-key-authentication-for-curseforge-file-downloads/
- MCIM 官方接入仓库：https://github.com/mcmod-info-mirror/mcim-api
- EasyTier 官方发布：https://github.com/EasyTier/EasyTier/releases/tag/v2.6.4
- EasyTier 官方用法：https://github.com/EasyTier/EasyTier

## 构建与复现

从 Git 克隆时需要保留 MeloongCore 子模块：

```powershell
git clone --recurse-submodules https://github.com/LGCMcn/PCL3_ModDownloadFix.git
cd PCL3_ModDownloadFix
.\build.ps1 -Configuration Release
.\tests\run-smoke-tests.ps1
```

Release 附带的 `PCL.DownloadFix.Source.zip` 已内含固定版本 MeloongCore，可直接解压后构建。GitHub 自动生成的 Source code 归档不包含子模块内容，请优先使用手动上传的完整源码包。

详细功能与配置见[使用说明](usage.md)。发行版固定为第三方修改版，不包含任何真实应用 Key。
