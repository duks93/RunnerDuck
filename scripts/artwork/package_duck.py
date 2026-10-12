"""Pack sprite-gen layer bakes into the shared runtime atlas; no pose generation."""
from pathlib import Path
import argparse
import hashlib
import json
import shutil

from PIL import Image, ImageChops, ImageOps, ImageFilter
from sprite_gen.util.gif_utils import save_clean_gif
from build_duck_rig import CLIPS

ROOT=Path(__file__).resolve().parents[2]


def dump(path,value):
    path.write_text(json.dumps(value,indent=2)+'\n',encoding='utf-8')


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--run-dir',type=Path,required=True)
    args=parser.parse_args()
    run=args.run_dir.resolve()
    art=ROOT/'artwork/duck'
    assets=ROOT/'LocalPackage/Sources/UserInterface/Resources/Duck'
    source_frames=[]; ink_frames=[]; clips={};reports={}
    for name,(count,fps) in CLIPS.items():
        folder=run/name/'layers'
        manifest=json.loads((folder/f'{name}.manifest.json').read_text())
        report=json.loads((folder/'layers.report.json').read_text())
        assert report['ok']
        entry=report['composites'][0]
        assert all(e['clipped_pixels']==0 for e in entry['elements'])
        sheet=Image.open(folder/f'{name}.png').convert('RGBA')
        indices=[]
        for r in manifest['frame_layout']['rows'][name]:
            cell=sheet.crop((r['x'],r['y'],r['x']+r['w'],r['y']+r['h']))
            indices.append(len(source_frames))
            source_frames.append(cell.resize((192,128),Image.Resampling.LANCZOS))
            # Subpixel stroke expansion: 2 high-resolution pixels = 2/3 runtime px.
            # This keeps thin generated outlines legible at tray/menu-bar sizes.
            ink=ImageChops.multiply(cell.getchannel('A'),ImageOps.invert(ImageOps.grayscale(cell)))
            ink=ink.filter(ImageFilter.MaxFilter(5)).resize((192,128),Image.Resampling.LANCZOS)
            ink_frames.append(ink)
        assert len(indices)==count
        clips[name]={'frames':indices,'fps':fps,'loop':True}
        reports[name]={'frames':entry['frames'],'fps':fps,'clippedPixels':0,
                       'layerSheetSha256':hashlib.sha256((folder/f'{name}.png').read_bytes()).hexdigest(),
                       'stack':[{k:e[k] for k in ['state','track','state_revision','clipped_pixels']}
                                for e in entry['elements']]}
    columns=len(source_frames)
    source=Image.new('RGBA',(columns*192,((len(source_frames)+columns-1)//columns)*128))
    rects=[]
    for i,frame in enumerate(source_frames):
        x=i%columns*192;y=i//columns*128
        source.alpha_composite(frame,(x,y));rects.append({'x':x,'y':y,'width':192,'height':128})
    source.save(art/'source-atlas.png')
    shutil.copy2(art/'source-atlas.png', assets/'duck-filled-atlas.png')
    atlas=Image.new('RGBA',source.size)
    alpha=Image.new('L',source.size)
    for i,ink in enumerate(ink_frames): alpha.paste(ink,(i%columns*192,i//columns*128))
    atlas.putalpha(alpha)
    atlas.save(assets/'duck-atlas.png')
    animation={'version':1,'atlas':'duck-atlas.png','frames':rects,'clips':clips}
    dump(assets/'animation.json',animation)
    dump(art/'source-manifest.json',{'kind':'runnerduck-layer-aggregate','source':'sprite-gen compose-layers',
                                    'downsample':'3x Lanczos','templateStrokeExpansionPx':2/3,'frames':rects,'clips':clips})
    report_dir=ROOT/'artifacts/asset-check'
    report_dir.mkdir(parents=True,exist_ok=True)
    dump(report_dir/'layer-validation.json',reports)
    shutil.copy2(run/'rig-poses.json',art/'rig-poses.json')
    dump(art/'motion-config.json',{'version':3,'renderScale':3,
         'clips':{name:{'frames':count,'fps':fps} for name,(count,fps) in CLIPS.items()}})
    frames=[]
    for r in rects:
        f=atlas.crop((r['x'],r['y'],r['x']+192,r['y']+128))
        bg=Image.new('RGBA',f.size,'#fffdf6');bg.alpha_composite(f);frames.append(bg)
    # GIF has centisecond timing; resample at 50 fps to keep the exact loop period.
    for name in clips:
        c=clips[name];n=round(len(c['frames'])/c['fps']*50)
        samples=[frames[c['frames'][min(len(c['frames'])-1,int(i/50*c['fps']))]] for i in range(n)]
        save_clean_gif(samples,art/f'{name}.gif',duration_ms=20,loop=0,alpha_threshold=0)
    first=rects[clips['walk']['frames'][0]]
    thumbnail=atlas.crop((first['x'],first['y'],first['x']+192,first['y']+128))
    media=ROOT/'LocalPackage/Sources/UserInterface/Resources/Media.xcassets'
    for name in ['runnerduck','resting-duck']:
        thumbnail.save(media/f'{name}.imageset'/f'{name}.png')
    print(f'Packaged {len(rects)} atlas cells, {len(clips)} walking loop.')


if __name__=='__main__':main()
