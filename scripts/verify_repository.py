"""Repository packaging checks; no network or app credentials required."""
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
files = subprocess.check_output(['git', 'ls-files', '--cached', '--others', '--exclude-standard', '-z'], cwd=root).decode().split('\0')
legacy = re.compile(r'\b' + 'run' + r'cat(?:neo|365|\s+neo|\s+365)?\b', re.I)
legal_dir = 'LocalPackage/Sources/UserInterface/Resources/Legal/'
errors = []
for rel in sorted(set(filter(None, files))):
    p = root / rel
    if not p.is_file():
        continue
    if legacy.search(rel):
        errors.append('Legacy filename: ' + rel)
    if any(part in {'.env', '.venv', '.sprite-work', 'bin', 'obj'} for part in p.relative_to(root).parts):
        errors.append('Generated or private file: ' + rel)
    try:
        text = p.read_text(encoding='utf-8-sig')
    except (UnicodeError, ValueError):
        continue
    if rel != 'NOTICE' and not rel.startswith(legal_dir):
        for number, line in enumerate(text.splitlines(), 1):
            if legacy.search(line) and not line.startswith('// Adapted from '):
                errors.append(f'Legacy product text: {rel}:{number}')
    if re.search(r'gh[pousr]_[A-Za-z0-9]{36,}|sk-proj-[A-Za-z0-9_-]{40,}', text):
        errors.append('Possible credential: ' + rel)
for source, bundled in [('NOTICE', 'NOTICE.txt'), ('LICENSE', 'LICENSE.txt')]:
    if (root/source).read_text() != (root/legal_dir/bundled).read_text():
        errors.append('Legal copy differs: ' + bundled)
manifest = ET.parse(root/'Windows/RunnerDuck.Desktop/Package.appxmanifest').getroot()
ns = {'p':'http://schemas.microsoft.com/appx/manifest/foundation/windows10',
      'com':'http://schemas.microsoft.com/appx/manifest/com/windows10'}
identity = manifest.find('p:Identity', ns)
assert identity is not None
if identity.get('Name') != 'RunnerDuck' or identity.get('Publisher') != 'CN=duks93':
    errors.append('Wrong Windows identity')
provider = (root/'Windows/RunnerDuck.Desktop/Widget/UsageWidgetProvider.cs').read_text()
guid = re.search(r'\[Guid\("([A-Fa-f0-9-]+)"\)\]', provider).group(1)
if manifest.find('.//com:Class', ns).get('Id') != guid:
    errors.append('Widget COM IDs differ')
if errors:
    raise SystemExit('\n'.join(errors))
print(f'Repository checks passed ({len(set(filter(None, files)))} files).')
