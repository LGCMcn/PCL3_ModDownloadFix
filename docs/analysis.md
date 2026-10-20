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

## v1.2.0 实现与计算方式

本版按上游要求恢复 Windows 固定蓝色主色，不提供旧版免费换色或类似赞助功能的表现。原 LICENCE、署名、赞助链接及 McLaunchPrecheck 劝导保留。源码公开受原许可约束。

### 微软认证

`ModMicrosoftAuth.vb` 统一消费者租户的 devicecode 和 token 端点。表单通过 FormUrlEncodedContent 编码，刷新令牌不作字符串拼接；刷新串行进行，避免令牌轮换并发竞争。`MyMsgLogin` 使用 Stopwatch 计算设备代码有效期，按服务返回 interval 等待；pending 保持间隔，slow_down 每次增加 5 秒，取消、拒绝、过期或无效 Client ID 立即结束。登录网页只接受明确列出的微软 HTTPS 主机；不读取其他启动器的令牌或借用其应用身份。

Xbox／XSTS／Minecraft 正版链路继续保留。Minecraft 返回 Invalid app registration 时单独指出应用未获接入资格。代码改进不等同于已经取得服务方授权；缺少本项目 Client ID 的情况下没有声称真实账号登录成功。

### 多账号

`ModAccounts.vb` 按 `(Minecraft UUID, Client ID)` 合并记录，同一 UUID 改名覆盖已有记录，不按显示名生成新账号。每个安装目录取规范化路径的 SHA256 前 24 个十六进制字符作为本地存储文件名；这只是路径分组，不是密码或认证凭据。

账号 JSON 整体通过 Windows DPAPI CurrentUser 加密；加密算法和主密钥管理由 Windows 提供，不自制加密算法，也不将其描述为固定某一种密码算法。文件先写入同目录临时文件，再调用 Windows 文件替换接口更新。Windows 支持同盘重命名时原子替换；跨设备的系统回退不能声称具备相同原子保证。解密失败时保留原文件。旧注册表设置兼容 DES 读取，新保存的加密设置采用 DPAPI。

管理器支持切换、添加及移除；登录／启动过程中拒绝修改，防止并行覆盖账号。移除当前账号清空当前缓存；移除其他账号不影响当前账号。退出本机记录不等同于撤销微软服务器上的授权。

### 网络与下载

`ModExtraSources.vb` 通过公开 REST API 搜索 GitHub 仓库、读取最新 Release 资源；FTB 使用官方 v1/modpacks API 搜索及读取简介。搜索词进行 URL 编码，GitHub 支持直接作者／仓库定位；二者不读取本机 GitHub 凭据，也不需要 Key。FTB 是目录与官方安装入口，尚未接入自动安装。

GitHub 文件只允许选中仓库下的官方发布路径，以及 JAR、ZIP、MRPACK 扩展名；文件名不能携带目录。下载至临时文件，先对比大小；若服务端提供 SHA256，再计算文件字节流的 SHA256 对比，完成后替换到用户选择的位置。原有 Minecraft／Mod 下载链路继续使用上游 SHA1 等元数据做校验。校验只能发现传输差异，不能证明内容无恶意。

`ModNet.vb` 对携带账号凭据、API Key 或微软认证主机的请求使用不带公共磁盘缓存的客户端，关闭隐式跳转。账号授权请求拒绝重定向；无账号 Authorization 的 GET 跳转只复制 User-Agent、Accept、Range，重新对目标主机做 Key 允许列表检查，最多跟随 8 次 HTTPS 跳转。HTTPS 证书不因旧设置而跳过。

### 复核与边界

PowerShell 测试编译 C# 校验程序，直接加载实际启动器程序集与 WPF 资源。测试显式取消正式 Application.Startup 事件订阅，防止 Dispatcher 处理测试任务时误触发完整启动器、协议或启动画面。多账号测试在当前测试进程把账号 Setting 的存储源改成测试目录文件，不读取或修改真实账号注册表；测试用 DPAPI 文件按测试安装目录隔离并清理。

本地最新完整测试：96 项通过，包括默认网络与 Mod／Minecraft 下载校验、Windows 蓝色限制、认证协议与重定向、多账号存取／改名／移除、真实 GitHub／FTB 查询及配置导出。通常不联网的检查为 89 项。实际微软账号登录、CurseForge 实 Key、完整游戏启动和跨电脑联机未验收。

源码中的 MeloongCore 保留原署名、许可和固定来源信息，以完整依赖源码交付；不包含付费快照程序、反编译文件、账号缓存或真实 Key。维护者自己的构建／打包脚本与测试同样公开。

## 项目发行版自动更新

VB.NET `ModProjectUpdate` 仅查询本项目 GitHub `/releases/latest`，比较数字版本并解析 `[ + ]` / `[ - ]` 更新记录；独立 HTTP 客户端不携带账号、Key 或 Cookie，限制 HTTPS 跳转域名与文件大小。运行 ZIP 先核对 GitHub SHA256（缺少时读取同版校验表），再校对解压白名单、版本、每个文件的 SHA256，拒绝越界路径、重复项和超大解压。

独立 C# `scripts/Updater.cs` 在父进程正常退出后备份全部目标，再以同目录临时文件替换。错误时反向恢复；保留服务 JSON、账号与游戏目录。安装目录不可写时失败并提示，不请求提权。保留恢复备份和错误日志。151 项本地检查包含真实下载、来源查询、更新包损坏／越界拒绝及部分替换失败后的恢复；没有把真实用户正在运行的程序用于覆盖试验。

## 本地界面效果、设备信息与离线图标

`ModVisualExperience` 给背景层使用 WPF BlurEffect，硬件渲染层级不足时降级，开关撤销时释放旧视觉树。错误反馈采用 340 ms 的平移关键帧，完成或卸载时移除动画时钟和事件；启动淡入与 18 px 位移可独立关闭，尊重系统减少动画及高对比度设置。不新增常驻计时器或 CompositionTarget.Rendering 订阅。

`MyOfflineMorphIcon` 将 morphicons 的 `src/core/spring.ts` 移植到原生 WPF：加速度 `a=420*(1-x)-30*v`，以 1/240 秒半隐式 Euler 子步积分，最多 90 帧，终点固定为 1。绘制固定本地线段，只在状态改变时生成有限关键帧；不包含 SVG 自动匹配的完整引擎，不加载外部图片或脚本。来源、提交与 MIT 许可位于 `ThirdParty/Morphicons`；运行版第三方声明包含完整许可。

`ModDeviceInfo` 按需读取本机注册表的系统／CPU 字段、Windows 物理内存与所在磁盘，显卡查询使用 WMI，并设等待上限及单一共享任务，不重叠创建查询。设备信息只展示在本机。原生页面截图在不同内容宽度检查；网页 DOM 检查器不适用于 WPF。资源释放检查覆盖 200 次背景切换与 100 次完成的错误动画，未观察到测试目标残留；尚未覆盖所有硬件／驱动和长时间真实游戏运行。


## v1.2.1 交互与窗口运动

VB.NET / WPF `ModNavigation` 在输入后同步挂载目标页面，Loaded 队列仅保留最后一次进入，不再等待旧页面退出与固定延时。内容以 6 DIP 位移、90 ms 淡入和 170 ms 归位反馈，分项延迟最多 24 ms。顶部选中胶囊通过单组 190 ms 三次缓动平移与宽度动画，从当前显示位置重新定向；完成或被替换即移除时钟。

`MyScrollViewer` 按系统滚轮行数累计目标（每行 20 DIP），实际范围为 ScrollableHeight。130–210 ms CubicEase 平滑滚动；反向输入改从可见偏移计算，避免继续向前滑。嵌套滚动区域优先消费事件，拖动、按键、卸载或尺寸变化取消旧动画，结束移除时钟。不添加常驻计时器或全局 Rendering 订阅。

`WindowMotion` 关闭动画在 180 ms 内将中心缩放从 1 降到 0.94、透明度降至 0；现有下载和联机退出确认仍先执行。最小化将窗口截图一次，图片尺寸最多 1280×900，建立 32×8 网格，以 WPF 正交相机、纹理映射与三次平滑函数进行 380 ms 收拢。进度由一个有限 DoubleAnimation 驱动，不按帧重新截图；完成、取消和窗口状态改变均清理时钟、纹理、临时窗口。重复点击不生成额外覆盖层，关闭可中断最小化。

屏幕边界使用 GetWindowRect、MonitorFromWindow、GetMonitorInfo；物理坐标按窗口 DPI 转换为覆盖层坐标，并以 SetWindowPos 定位覆盖层。按所在屏幕工作区预留边缘选择收拢方向，支持负坐标及四侧布局；自动隐藏未预留工作区时回到底边。这是向任务栏区域的效果，不保证精确命中特定任务栏按钮。低 WPF 渲染层级、远程会话、系统减少动画及高对比度条件触发轻量处理。真实异构 DPI 多屏和全部显卡驱动仍需使用者环境验证。

设置文字不内嵌重启说明。SettingService 仅对用户操作且确实需要重启的键返回提示，MyCheckBox 保存成功后复用左下角 Hint 动画；读取偏好不会提示。关闭与最小化、平滑滚动和导航开关即时读取，不要求重启。

本版本本地完整验证为 209 项，含真实 Mod／Minecraft 文件校验、GitHub／FTB 查询和新增的 58 项交互检查；新检查覆盖短标识三种宽度、快速切换、滚动改向、四边网格终点、负屏幕坐标、重复最小化、取消／恢复、关闭回调去重和 30 个窗口动画控制器释放。资源回收检查有范围，不代表所有硬件长时间运行的绝对无泄漏保证。
