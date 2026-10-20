"""Publish verified release files or update historical version labels using the owner's Git credentials."""
from pathlib import Path
import argparse
import hashlib
import json
import os
import subprocess
import urllib.error
import urllib.parse
import urllib.request

REPOSITORY = 'LGCMcn/PCL3_ModDownloadFix'

class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *_):
        raise RuntimeError('Authenticated API redirects are disabled')

class GitHub:
    def __init__(self, checkout):
        credential = subprocess.run(['git', 'credential', 'fill'], cwd=checkout,
            input='protocol=https\nhost=github.com\n\n', capture_output=True, text=True,
            check=True, timeout=20, env=dict(os.environ, GCM_INTERACTIVE='never', GIT_TERMINAL_PROMPT='0'))
        self.token = dict(s.split('=', 1) for s in credential.stdout.splitlines() if '=' in s).get('password')
        if not self.token:
            raise RuntimeError('Configure Git credentials for your own GitHub account first')
        self.opener = urllib.request.build_opener(NoRedirect())

    def request(self, path, method='GET', data=None, raw=None):
        url = path if path.startswith('https://') else 'https://api.github.com/repos/' + REPOSITORY + path
        if urllib.parse.urlparse(url).hostname not in {'api.github.com', 'uploads.github.com'}:
            raise ValueError('Unexpected API host')
        headers = {'Authorization': 'Bearer ' + self.token, 'User-Agent': 'LGCMcn-DownloadFix-Releases',
            'Accept': 'application/vnd.github+json', 'X-GitHub-Api-Version': '2022-11-28'}
        body = None
        if raw is not None:
            body = raw
            headers['Content-Type'] = 'application/octet-stream'
        elif data is not None:
            body = json.dumps(data, ensure_ascii=False).encode('utf-8')
            headers['Content-Type'] = 'application/json'
        with self.opener.open(urllib.request.Request(url, data=body, method=method, headers=headers), timeout=180) as response:
            payload = response.read()
            return json.loads(payload) if payload else None

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def label_history(client, output, apply):
    releases = client.request('/releases?per_page=100')
    output.mkdir(parents=True, exist_ok=True)
    (output/'release-metadata-before.json').write_text(json.dumps(releases, ensure_ascii=False, indent=2), encoding='utf-8')
    versions = {'downloadfix-2026.10.20': ('1.0.0', False), 'downloadfix-personalization-2026.10.20': ('1.1.0', True)}
    result = []
    for release in releases:
        if release['tag_name'] not in versions:
            continue
        version, withdrawn = versions[release['tag_name']]
        title = 'v' + version + ' · Plain Craft Launcher (PCL) LGCMcn 下载修复版' + ('（已撤回）' if withdrawn else '')
        body = release.get('body', '')
        if '<!-- release-version -->' in body:
            body = body.split('<!-- /release-version -->', 1)[-1].lstrip()
        status = '已撤回：Windows 主题换色表现与上游指南冲突，不再公开分发。请使用后续固定蓝色的版本。' if withdrawn else '历史下载修复版；新增功能与安全改进请查看最新发行版。'
        header = ('<!-- release-version -->\n## 版本信息\n\n'
            '| 字段 | 内容 |\n| --- | --- |\n'
            f'| 第三方版本 | v{version} |\n| 原标签 | `{release["tag_name"]}` |\n'
            f'| 源码提交／目标 | `{release["target_commitish"]}` |\n'
            f'| 发布状态 | {status} |\n| 身份 | LGCMcn 独立第三方修改，不代表 PCL 官方发布 |\n'
            '<!-- /release-version -->\n\n')
        patch = dict(name=title, body=header+body)
        if withdrawn:
            patch['draft'] = True  # reversible withdrawal; retains version history and files for the owner
        if apply:
            changed = client.request('/releases/'+str(release['id']), 'PATCH', data=patch)
            assert changed['name'] == title and (not withdrawn or changed['draft'])
        result.append(dict(version=version, id=release['id'], tag=release['tag_name'], withdrawn=withdrawn, applied=apply))
    return result

def publish(client, checkout, manifest_path):
    manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    directory = manifest_path.parent
    assert client.request('/git/ref/heads/main')['object']['sha'] == manifest['commit'], 'Remote source differs'
    for item in manifest['assets']:
        assert sha(directory/item['name']) == item['sha256']
    releases = client.request('/releases?per_page=100')
    release = next((r for r in releases if r['tag_name'] == manifest['tag']), None)
    body = (directory/'release-notes.md').read_text(encoding='utf-8')
    if release is None:
        release = client.request('/releases', 'POST', data=dict(tag_name=manifest['tag'], target_commitish=manifest['commit'],
            name=manifest['name'], body=body, draft=True, prerelease=False))
    else:
        assert release['target_commitish'] == manifest['commit']
    for item in manifest['assets']:
        assets = client.request('/releases/'+str(release['id'])+'/assets?per_page=100')
        asset = next((a for a in assets if a['name'] == item['name']), None)
        if asset is None:
            url = release['upload_url'].split('{', 1)[0] + '?' + urllib.parse.urlencode(dict(name=item['name']))
            print('Uploading '+item['name'], flush=True)
            asset = client.request(url, 'POST', raw=(directory/item['name']).read_bytes())
        assert asset['size'] == item['size'] and asset.get('digest') == 'sha256:'+item['sha256']
    if release['draft']:
        release = client.request('/releases/'+str(release['id']), 'PATCH', data=dict(draft=False, make_latest='true'))
    assert client.request('/releases/latest')['id'] == release['id']
    receipt = dict(url=release['html_url'], tag=manifest['tag'], commit=manifest['commit'], draft=release['draft'],
        assets=client.request('/releases/'+str(release['id'])+'/assets?per_page=100'))
    (directory/'published.json').write_text(json.dumps(receipt, ensure_ascii=False, indent=2), encoding='utf-8')
    return dict(url=receipt['url'], tag=receipt['tag'], commit=receipt['commit'], assets=len(receipt['assets']))

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('operation', choices=['versions', 'publish'])
    parser.add_argument('--checkout', type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument('--output', type=Path, default=Path('release-metadata'))
    parser.add_argument('--manifest', type=Path)
    parser.add_argument('--apply', action='store_true', help='Write metadata or publish after local validation')
    args = parser.parse_args()
    client = GitHub(args.checkout)
    if args.operation == 'versions':
        result = label_history(client, args.output, args.apply)
    else:
        if not args.apply or args.manifest is None:
            parser.error('Publishing requires --manifest and --apply')
        result = publish(client, args.checkout, args.manifest)
    print(json.dumps(result, ensure_ascii=True, indent=2))

if __name__ == '__main__':
    main()
