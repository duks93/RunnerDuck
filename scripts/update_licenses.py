"""Refresh bundled legal texts from pinned dependency revisions."""
from pathlib import Path
import json
from urllib.request import urlopen

root = Path(__file__).resolve().parents[1]
legal = root / 'LocalPackage/Sources/UserInterface/Resources/Legal'
legal.mkdir(parents=True, exist_ok=True)
for source, dest in [('NOTICE', 'NOTICE.txt'), ('LICENSE', 'LICENSE.txt')]:
    (legal / dest).write_text((root / source).read_text(encoding='utf-8'), encoding='utf-8')
entries = [(p['location'].removeprefix('https://github.com/').removesuffix('.git'), p['state']['revision'])
           for p in json.loads((root / 'LocalPackage/Package.resolved').read_text())['pins']]
entries += [('microsoft/WindowsAppSDK', 'v2.5.1'), ('microsoft/WindowsAppSDK-Samples', 'main'),
            ('dotnet/runtime', 'v10.0.0'), ('dotnet/winforms', 'v10.0.0')]
parts = []
for repo, ref in entries:
    found = False
    for name in ['LICENSE', 'LICENSE.txt', 'LICENSE.md', 'LICENSE.TXT']:
        url = f'https://raw.githubusercontent.com/{repo}/{ref}/{name}'
        try:
            body = urlopen(url, timeout=30).read().decode('utf-8-sig')
        except Exception:
            continue
        parts.append(f'{repo}\nSource: {url}\n\n{body.strip()}\n')
        found = True
        print(repo, flush=True)
        break
    if not found:
        raise RuntimeError('License unavailable for ' + repo)
    for name in ['NOTICE', 'NOTICE.txt']:
        try:
            body = urlopen(f'https://raw.githubusercontent.com/{repo}/{ref}/{name}', timeout=20).read().decode('utf-8-sig')
        except Exception:
            continue
        parts.append(f'{repo} {name}\n\n{body.strip()}\n')
        break
(legal / 'THIRD_PARTY.txt').write_text('\n'.join(line.rstrip() for line in '\n\n'.join(parts).splitlines()).rstrip() + '\n', encoding='utf-8')
