"""Pack the runner layer bakes into per-runner runtime atlases; no pose generation."""
from pathlib import Path
import argparse
import hashlib
import json
import shutil

from PIL import Image
from sprite_gen.util.gif_utils import save_clean_gif

ROOT = Path(__file__).resolve().parents[2]
CELL = (192, 128)


def dump(path, value):
    path.write_text(json.dumps(value, indent=2)+'\n', encoding='utf-8')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--run-dir', type=Path, required=True)
    parser.add_argument('--runners', default='')
    args = parser.parse_args()
    run = args.run_dir.resolve()
    art = ROOT/'artwork/runners'
    assets = ROOT/'LocalPackage/Sources/UserInterface/Resources/Runners'
    assets.mkdir(parents=True, exist_ok=True)
    config = json.loads((art/'runners.json').read_text(encoding='utf-8'))
    names = args.runners.split(',') if args.runners else list(config['runners'])
    reports_path = ROOT/'artifacts/asset-check/runners-layer-validation.json'
    reports_path.parent.mkdir(parents=True, exist_ok=True)
    reports = json.loads(reports_path.read_text()) if reports_path.exists() else {}
    for name in names:
        spec = config['runners'][name]
        count, fps = spec['frames'], spec.get('fps', spec['frames'])
        folder = run/name/'walk/layers'
        manifest = json.loads((folder/'walk.manifest.json').read_text())
        report = json.loads((folder/'layers.report.json').read_text())
        assert report['ok']
        entry = report['composites'][0]
        assert all(e['clipped_pixels'] == 0 for e in entry['elements'])
        sheet = Image.open(folder/'walk.png').convert('RGBA')
        cells = manifest['frame_layout']['rows']['walk']
        assert len(cells) == count
        atlas = Image.new('RGBA', (count*CELL[0], CELL[1]))
        rects = []
        for i, r in enumerate(cells):
            cell = sheet.crop((r['x'], r['y'], r['x']+r['w'], r['y']+r['h']))
            # White runners keep their generated fill and ink, like the filled duck.
            atlas.alpha_composite(cell.resize(CELL, Image.Resampling.LANCZOS), (i*CELL[0], 0))
            rects.append({'x': i*CELL[0], 'y': 0, 'width': CELL[0], 'height': CELL[1]})
        atlas.save(assets/f'{name}.png')
        dump(assets/f'{name}.json', {'version': 1, 'atlas': f'{name}.png', 'frames': rects,
                                     'clips': {'walk': {'frames': list(range(count)), 'fps': fps, 'loop': True}}})
        shutil.copy2(run/name/'rig-poses.json', art/name/'rig-poses.json')
        frames = []
        for r in rects:
            frame = atlas.crop((r['x'], r['y'], r['x']+r['width'], r['y']+r['height']))
            background = Image.new('RGBA', frame.size, '#dfe7ef')
            background.alpha_composite(frame)
            frames.append(background)
        # GIF has centisecond timing; resample at 50 fps to keep the exact loop period.
        samples = [frames[min(count-1, int(i/50*fps))] for i in range(round(count/fps*50))]
        save_clean_gif(samples, art/name/'walk.gif', duration_ms=20, loop=0, alpha_threshold=0)
        reports[name] = {'frames': entry['frames'], 'fps': fps, 'clippedPixels': 0,
                         'layerSheetSha256': hashlib.sha256((folder/'walk.png').read_bytes()).hexdigest(),
                         'stack': [{k: e[k] for k in ['state', 'track', 'state_revision', 'clipped_pixels']}
                                   for e in entry['elements']]}
        print(f'Packaged {name}: {count} frames at {fps} fps.')
    dump(reports_path, reports)


if __name__ == '__main__':
    main()
