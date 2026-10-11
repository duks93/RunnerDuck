"""Check shipped pixels, loop/transition seams, and the authored motion tracks."""
from pathlib import Path
import hashlib
import json
import math
from PIL import Image, ImageChops, ImageDraw

root=Path(__file__).resolve().parents[1]
art=root/'artwork/duck'
assets=root/'LocalPackage/Sources/UserInterface/Resources/Duck'
animation=json.loads((assets/'animation.json').read_text())
config=json.loads((art/'motion-config.json').read_text())
rig=json.loads((art/'rig-poses.json').read_text())
atlas=Image.open(assets/animation['atlas']).convert('RGBA')
filled_path=assets/'duck-filled-atlas.png'
assert filled_path.read_bytes()==(art/'source-atlas.png').read_bytes(), 'Filled runner must use the actual source artwork.'
source_manifest=json.loads((art/'source-manifest.json').read_text())
assert source_manifest['frames']==animation['frames'] and source_manifest['clips']==animation['clips']
filled_atlas=Image.open(filled_path).convert('RGBA')
assert filled_atlas.size==atlas.size
expected={'idle','walk','run','idle_to_walk','walk_to_idle','walk_to_run','run_to_walk'}
assert animation['version']==1
assert set(animation['clips'])==set(config['clips'])==set(rig['clips'])==expected
assert len(animation['frames'])<=256, 'Both app parsers cap the cache at 256 frames.'
frames=[]
for rect in animation['frames']:
    x,y,w,h=(rect[k] for k in ['x','y','width','height'])
    assert (w,h)==(192,128)
    assert 0<=x<=atlas.width-w and 0<=y<=atlas.height-h
    f=atlas.crop((x,y,x+w,y+h));bounds=f.getchannel('A').getbbox()
    assert bounds and bounds[0]>0 and bounds[1]>0 and bounds[2]<w and bounds[3]<h,bounds
    assert all(c.getextrema()==(0,0) for c in f.split()[:3]),'Runtime art must be tintable ink alpha.'
    frames.append(f)
    filled=filled_atlas.crop((x,y,x+w,y+h))
    assert filled.getchannel('A').getbbox(), 'Filled frame is empty.'
    r,g,b,a=filled.getpixel((70,70))
    assert min(r,g,b,a)>240, 'Filled runner lost its white body.'
    assert filled.getpixel((0,0))[3]==0, 'Filled runner background must stay transparent.'


def eye_center(frame):
    # Measure the isolated eye's ink in the rendered image, independently of rig targets.
    # Its fixed ROI excludes the bill and body; connected components exclude the head outline.
    alpha=frame.getchannel('A');pixels=alpha.load();remaining=set()
    for y in range(22,61):
        for x in range(88,121):
            if pixels[x,y]>90:remaining.add((x,y))
    candidates=[]
    while remaining:
        q=[remaining.pop()];component=[]
        while q:
            x,y=q.pop();component.append((x,y))
            for pt in [(x-1,y),(x+1,y),(x,y-1),(x,y+1)]:
                if pt in remaining:remaining.remove(pt);q.append(pt)
        xs=[p[0] for p in component];ys=[p[1] for p in component]
        if 3<=max(xs)-min(xs)<=9 and 3<=max(ys)-min(ys)<=12 and len(component)>=10:
            candidates.append(component)
    assert len(candidates)==1, f'Eye missing or disconnected: {len(candidates)}'
    points=candidates[0];mass=sum(pixels[x,y] for x,y in points)
    return [sum(p[k]*pixels[p[0],p[1]] for p in points)/mass for k in [0,1]]


report={}
for name,clip in animation['clips'].items():
    n=len(clip['frames']);fps=clip['fps'];poses=rig['clips'][name]
    assert n==config['clips'][name]['frames']==len(poses)
    assert fps==config['clips'][name]['fps'] and 0<fps<=60
    assert clip['loop']==('_to_' not in name)
    assert all(0<=i<len(frames) for i in clip['frames'])
    played=[frames[i] for i in clip['frames']]
    duration=n/fps
    if not clip['loop']:
        assert abs(duration-.3)<1e-8
        a,b=name.split('_to_')
        for at,state in [(0,a),(-1,b)]:
            assert played[at].tobytes()==frames[animation['clips'][state]['frames'][0]].tobytes()
            assert poses[at]==rig['clips'][state][0]
    assert len({f.tobytes() for f in played})>=3
    eye=[eye_center(f) for f in played]
    pairs=list(zip(range(n),list(range(1,n))+[0])) if clip['loop'] else list(zip(range(n-1),range(1,n)))
    foot_step=max(math.dist(poses[a]['feet'][j]['ankle'],poses[b]['feet'][j]['ankle']) for a,b in pairs for j in [0,1])
    knee_step=max(math.dist(poses[a]['knees'][j],poses[b]['knees'][j]) for a,b in pairs for j in [0,1])
    eye_step=max(math.dist(eye[a],eye[b]) for a,b in pairs)
    assert foot_step<4.2,(name,'foot snap',foot_step)
    assert knee_step<7.5,(name,'knee snap',knee_step)
    assert eye_step<2,(name,'rendered head snap',eye_step)
    for p in poses:
        for j,f in enumerate(p['feet']):
            for a,b in [(p['hips'][j],p['knees'][j]),(p['knees'][j],f['ankle'])]:
                assert abs(math.dist(a,b)-rig['segmentLength'])<1e-6,'Leg length changed.'
            assert f['ankle'][1]<=115+1e-8,'Foot target penetrates the ground.'
            if f['stance']:
                assert abs(f['ankle'][1]-115)<1e-8 and abs(f['angle'])<1e-8
    if name=='idle':
        assert foot_step==0
        # Actual rendered sole pixels stay fixed, not only the rig coordinates.
        sole=[f.crop((25,116,115,123)).tobytes() for f in played]
        assert len(set(sole))==1,'Idle soles visibly move.'
    if name in ['walk','run']:
        for j in [0,1]:
            assert any(not p['feet'][j]['stance'] for p in poses)
            for a,b in pairs:
                fa,fb=poses[a]['feet'][j],poses[b]['feet'][j]
                if fa['stance'] and fb['stance']:
                    assert fb['ankle'][0]<fa['ankle'][0], 'Grounded foot resets forward.'
        airborne=any(all(not f['stance'] for f in p['feet']) for p in poses)
        assert airborne==(name=='run')
    report[name]={'frames':n,'durationSeconds':duration,'maxFootTargetStepPx':round(foot_step,4),
                  'maxKneeStepPx':round(knee_step,4),'maxRenderedEyeStepPx':round(eye_step,4),
                  'eyeVerticalRangePx':round(max(p[1] for p in eye)-min(p[1] for p in eye),4)}

out=root/'artifacts/asset-check';out.mkdir(parents=True,exist_ok=True)
# Every frame at 16/24/32 pixel display height, with enough rows to avoid clipping.
for name,clip in animation['clips'].items():
    cols=12;rows=(len(clip['frames'])+cols-1)//cols
    sheet=Image.new('RGB',(cols*74,rows*104),'#eeeeee');d=ImageDraw.Draw(sheet)
    for i,index in enumerate(clip['frames']):
        x=i%cols*74;y=i//cols*104;d.text((x+4,y+2),str(i+1),fill='black')
        offset=18
        for size in [16,24,32]:
            f=frames[index].resize((round(size*1.5),size),Image.Resampling.LANCZOS)
            sheet.paste(f,(x+4,y+offset),f);offset+=size+2
    sheet.save(out/f'{name}-small.png')
report['atlasSha256']=hashlib.sha256((assets/'duck-atlas.png').read_bytes()).hexdigest()
(out/'validation.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
