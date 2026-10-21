"""Package the built launcher and the complete tracked source; exclude personal runtime state."""
from pathlib import Path
from io import BytesIO
import argparse
import hashlib
import json
import shutil
import subprocess
import zipfile

VERSION = '1.2.4'
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
    save(portable/'PCL.Release.json', dict(version=VERSION, file_version='1.2.4.7', tag='v'+VERSION, source_commit=commit,
        known_issues=['ui_navigation_stutter', 'microsoft_owner_authorization_required', 'curseforge_key_required'],
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
    notes = (repo/'docs/releases'/('v'+VERSION+'.md')).read_text(encoding='utf-8')
    assert '[ ? ]' in notes and '尚未解决' in notes, 'Known issues must remain explicit'
    notes = notes.replace('<!-- source-commit -->', '| 源码提交 | `'+commit+'` |')
    (directory/'release-notes.md').write_text(notes, encoding='utf-8')
    print(json.dumps(dict(directory=str(directory),version=VERSION,commit=commit,source_files=source_file_count),ensure_ascii=True))

if __name__ == '__main__':
    main()
