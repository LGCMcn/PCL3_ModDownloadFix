"""Package the built launcher and the complete tracked source; exclude personal runtime state."""
from pathlib import Path
from io import BytesIO
import argparse
import hashlib
import json
import shutil
import subprocess
import zipfile

VERSION = '1.2.2'
EXECUTABLE = 'Plain Craft Launcher (PCL) LGCMcn 下载修复版.exe'

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def save(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--checkout', type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument('--output', type=Path)
    parser.add_argument('--component', type=Path, required=True, help='Official EasyTier x64 v2.6.4 ZIP')
    args = parser.parse_args()
    repo = args.checkout.resolve()
    directory = (args.output or repo/'artifacts/releases'/('v'+VERSION)).resolve()
    portable = directory/'portable'
    portable.mkdir(parents=True, exist_ok=True)
    assert not subprocess.check_output(['git','status','--porcelain'], cwd=repo).strip(), 'Commit source changes first'
    commit = subprocess.check_output(['git','rev-parse','HEAD'], cwd=repo, text=True).strip()
    assert sha(args.component) == '27af91e270e554709b048bd32327fefd2dfce5062ae1e8701af7550c6f525f84'
    output = repo/'Plain Craft Launcher 2/bin'
    for source, destination in ((output/'Plain Craft Launcher 2.exe', portable/EXECUTABLE),
        (output/'Plain Craft Launcher 2.exe.config', portable/(EXECUTABLE+'.config')),
        (repo/'LICENCE', portable/'LICENCE'), (repo/'THIRD_PARTY_NOTICE.md', portable/'第三方版本声明.md')):
        shutil.copy2(source, destination)
    for config in ('PCL.Services','PCL.Link'):
        shutil.copy2(repo/(config+'.example.json'), portable/(config+'.json'))
    shutil.copy2(output/'PCL.Update.exe', portable/'PCL.Update.exe')
    for document, title, old, new in (('usage.md','使用说明.md','(analysis.md)','(项目分析与验证.md)'),
        ('analysis.md','项目分析与验证.md','(usage.md)','(使用说明.md)')):
        (portable/title).write_text((repo/'docs'/document).read_text(encoding='utf-8').replace(old,new), encoding='utf-8')
    (portable/'组件').mkdir(exist_ok=True)
    shutil.copy2(args.component, portable/'组件/easytier-windows-x86_64-v2.6.4.zip')
    save(portable/'PCL.Release.json', dict(version=VERSION, tag='v'+VERSION, source_commit=commit,
        upstream_source_version='2.99.0.0', repository='LGCMcn/PCL3_ModDownloadFix', third_party=True,
        official_release=False, windows_theme='blue', microsoft_app_authorization='owner_application_required'))
    save(portable/'SHA256SUMS.json', {p.relative_to(portable).as_posix():sha(p) for p in sorted(portable.rglob('*')) if p.is_file() and p.name!='SHA256SUMS.json'})
    runtime = directory/'PCL.DownloadFix.Windows-x64.zip'
    with zipfile.ZipFile(runtime,'w',zipfile.ZIP_DEFLATED) as z:
        for file in sorted(portable.rglob('*')):
            if file.is_file(): z.write(file,file.relative_to(portable).as_posix())
    source = directory/'PCL.DownloadFix.Source.zip'
    data = subprocess.check_output(['git','archive','--format=zip','HEAD'], cwd=repo)
    with zipfile.ZipFile(BytesIO(data)) as original, zipfile.ZipFile(source,'w',zipfile.ZIP_DEFLATED) as z:
        for item in original.infolist():
            if not item.is_dir(): z.writestr(item.filename,original.read(item))
    with zipfile.ZipFile(source) as z:
        names = z.namelist()
        assert 'MeloongCore/Shared/MeloongCore.csproj' in names, 'Dependency source must be vendored'
        assert 'scripts/github_release.py' in names and 'tests/SmokeTests.cs' in names
        assert not any(n.startswith(('inspection/','artifacts/','.minecraft/','PCL/')) or 'decompiled' in n for n in names)
        assert z.testzip() is None
        source_file_count = len(names)
    with zipfile.ZipFile(runtime) as z:
        assert z.testzip() is None
        assert json.loads(z.read('PCL.Services.json')).get('CurseForgeApiKey','') == ''
    shutil.copy2(repo/'THIRD_PARTY_NOTICE.md', directory/'THIRD_PARTY_NOTICE.md')
    names = [runtime.name,source.name,'THIRD_PARTY_NOTICE.md']
    (directory/'SHA256SUMS.txt').write_text(''.join(sha(directory/n)+'  '+n+'\n' for n in names), encoding='utf-8')
    name = 'v'+VERSION+' · Plain Craft Launcher (PCL) LGCMcn 下载修复版（第三方）'
    save(directory/'manifest.json', dict(tag='v'+VERSION, version=VERSION, name=name, commit=commit,
        assets=[dict(name=n,size=(directory/n).stat().st_size,sha256=sha(directory/n)) for n in names+['SHA256SUMS.txt']]))
    (directory/'release-notes.md').write_text(f'''# v{VERSION} · 第三方下载修复版

> 原项目作者仍为 **PCL 官方（龙腾猫跃及原项目贡献者）**。本版本由 LGCMcn 基于公开源码独立修改，第三方修改与原作者无关，**不代表 PCL 官方发布**。保留原 LICENCE、署名、原作者赞助链接和正版购买劝导。

## 版本信息

| 字段 | 内容 |
| --- | --- |
| 第三方版本 | v{VERSION} |
| 上游源码版本 | 2.99.0.0 |
| 源码提交 | `{commit}` |
| 平台 | Windows x64 / .NET Framework 4.8 |
| Windows 主色 | 固定蓝色 |
| 本地验证 | 212 项检查通过；含真实下载、窗口动画、来源查询与更新恢复测试 |

## 更新内容

- [ + ] 用独立 AppUserModelID 和后台 Windows UI Automation 获取本程序任务栏按钮位置，收拢终点对齐按钮中心；不可用时回退边缘。
- [ - ] 动画线程从最高约 333 次／秒降到约 60 次／秒，并改为低于输入的调度优先级，减少抢占。
- [ - ] 顶部胶囊改为渲染变换，不逐帧改变布局宽度；移除叠加颜色动画，连续点击仅执行最后一次切页。
- [ + ] 空闲时提前准备常用页面，减少首次点击时集中创建控件。

- [ - ] 修复关闭平滑滚动或系统减少动画时，同一帧的连续滚动丢失累计距离。

- [ + ] 最小化时窗口向本程序任务栏按钮中心收拢变形，定位不可用时退回任务栏边缘，结束后释放快照；低渲染能力或远程会话自动降级。
- [ + ] 关闭时轻微缩至 94% 并淡出；关闭动画与最小化动画分别可关闭，立即生效。
- [ + ] 顶部选中胶囊短暂滑动，页面与导航动画和平滑滚动各有开关。
- [ - ] 页面立即响应切换，移除旧的固定等待；连续操作只进入最后选中的页面。
- [ - ] 滚轮平滑滚动，反向操作立即改向，拖动／按键／卸载时取消旧动画。
- [ - ] 缩短标题栏三根竖线标识，增加品牌、第三方声明与导航之间的间距。
- [ - ] 设置标签移除括号与重启说明；需要重启的用户操作改用左下角动画提示，加载设置不提示。
- [ - ] 防止重复最小化生成覆盖层、关闭回调重复触发和动画结束后残留时钟。
- [ + ] 保留 v1.2.0 的固定蓝色、多账号、公开下载源、设备信息、离线图标与项目校验更新。源码包含本次 VB.NET / WPF 实现和 C# 校验程序。

## 下载

- `PCL.DownloadFix.Windows-x64.zip`：完整解压，运行其中的 EXE；保留配置和“组件”文件夹。
- `PCL.DownloadFix.Source.zip`：完整源码，包含固定来源依赖，可按 README 构建。
- `SHA256SUMS.txt`：核对同名文件；`THIRD_PARTY_NOTICE.md`：第三方声明。

升级时保留个人 JSON 配置、游戏目录和账号数据。旧版换色设置会回到蓝色。

## 需要维护者完成

**尚未取得本项目的 Microsoft Client ID／Minecraft API 授权，真实账号登录未验收。** 维护者完成申请后填写项目级配置，用户再在微软网页登录。不借用其他启动器的身份，不绕过正版认证。CurseForge Key 同样需要维护者申请审核。申请步骤和可填写的项目介绍见 [使用说明](https://github.com/LGCMcn/PCL3_ModDownloadFix/blob/main/docs/usage.md)。

Modrinth、公开 GitHub 目录和 FTB 目录无需 Key。完整游戏启动、全部加载器及跨电脑公网联机仍有未验收范围。

## 安全与源码检查

维护者 LGCMcn 承诺不主动植入病毒、木马、挖矿、盗号或隐蔽后门，源码公开接受检查。维护者表态：“藏毒作者迪克爆炸。”承诺不等于对第三方 Mod、依赖或任意运行环境的绝对安全担保。哈希用于核对文件，不能代替代码审查和安全检测。

[技术实现与计算](https://github.com/LGCMcn/PCL3_ModDownloadFix/blob/main/docs/analysis.md) · [许可](https://github.com/LGCMcn/PCL3_ModDownloadFix/blob/main/LICENCE) · [NAS 备用下载](https://ug.link/lgcm/filemgr/share-download/?id=3fb99fd396a54ed0835d5300e18540ce)（由维护者手动同步，请核对版本和 SHA256）
''', encoding='utf-8')
    print(json.dumps(dict(directory=str(directory),version=VERSION,commit=commit,source_files=source_file_count),ensure_ascii=True))

if __name__ == '__main__':
    main()
