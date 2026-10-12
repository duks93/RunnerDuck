"""Bake generated duck cutouts with sprite-gen's curation and layer composer.

The artwork is generated. Motion is an authored biped rig, not AI pose rows.
No drawing, optical-flow interpolation, or per-frame ground refitting is used.
"""
from __future__ import annotations

import argparse
import json
import math
from pathlib import Path

from PIL import Image, ImageDraw
from sprite_gen.curate.curation import apply_transform, empty_curation, write_curation_atomic
from sprite_gen.frames.unpack_atlas import import_png_groups
from sprite_gen.compose.compose_layers import bake

SCALE = 3
CELL = (192 * SCALE, 128 * SCALE)
CLIPS = {'walk': (8,8)}
TAU = math.tau
LENGTH = 18.0  # Fixed hip-knee and knee-ankle distances, in runtime pixels.


def dump(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + '\n', encoding='utf-8')


def rotate(v, angle):
    a = math.radians(angle)
    return (math.cos(a)*v[0]+math.sin(a)*v[1], -math.sin(a)*v[0]+math.cos(a)*v[1])


def plus(a,b):
    return (a[0]+b[0], a[1]+b[1])


def foot_path(phase, duty, stride, lift, x):
    p = phase % 1
    if p < duty:
        return {'ankle': [x+stride*(1-2*p/duty),115.0], 'angle':0.0, 'stance':True}
    u = (p-duty)/(1-duty)
    # Matching horizontal velocity at lift-off and touchdown eliminates resets.
    v = -2*stride/duty*(1-duty)
    h00 = 2*u**3-3*u*u+1
    h10 = u**3-2*u*u+u
    h01 = -2*u**3+3*u*u
    h11 = u**3-u*u
    dx = h00*(-stride) + h10*v + h01*stride + h11*v
    return {'ankle':[x+dx,115-lift*math.sin(math.pi*u)**2],
            'angle':18*math.sin(TAU*u)*math.sin(math.pi*u)**2, 'stance':False}


def pose(phase):
    p = phase % 1
    duty,stride,lift = .62,14.,12.
    # Feet alternate by exactly half a cycle; no foot identity swapping.
    feet = [foot_path(p+.5,duty,stride,lift,66.), foot_path(p,duty,stride,lift,70.)]
    body = [72.,72.-1.1*(1-math.cos(2*TAU*p))]
    angle = -1.0 + .8*math.sin(2*TAU*p)
    head = 2.5*math.sin(2*TAU*p-.65)
    return {'body':body,'bodyAngle':angle,'headAngle':head,'feet':feet}


def knee(hip,ankle):
    dx,dy = ankle[0]-hip[0],ankle[1]-hip[1]
    d = math.hypot(dx,dy)
    if d >= 2*LENGTH:
        raise ValueError(f'Unreachable foot target: {d}')
    h = math.sqrt(LENGTH*LENGTH-d*d/4)
    return ((hip[0]+ankle[0])/2+dy/d*h, (hip[1]+ankle[1])/2-dx/d*h)


def canonical_parts(source, out):
    specs = {'torso': (70.,(.5,.5)), 'head': (82.,(.16,.66)),
             'leg': (1.94,(.5,.5)), 'foot': (16.,(.32,.52))}
    paths = {}
    for name,(width,pivot) in specs.items():
        im = Image.open(source/f'{name}.png').convert('RGBA')
        box = im.getchannel('A').getbbox()
        im = im.crop(box)
        # Source cutout masks remove only the concealed neck tab caps, not the art.
        if name == 'head':
            alpha = im.getchannel('A')
            ImageDraw.Draw(alpha).polygon([(0,im.height*.50),(im.width*.455,im.height),(0,im.height)],fill=0)
            im.putalpha(alpha)
        scale = width*SCALE/im.width
        point = (pivot[0]*im.width,pivot[1]*im.height)
        transform = {'scale':scale,
                     'dx': -(point[0]-im.width/2)*scale,
                     'dy': -(point[1]-im.height/2)*scale}
        framed = apply_transform(im, transform, CELL)
        path = out/f'{name}.png'
        path.parent.mkdir(parents=True,exist_ok=True)
        framed.save(path)
        paths[name] = path
    return paths


def instance(position,angle):
    return {'rotate':angle,'dx':position[0]*SCALE-CELL[0]/2,
            'dy':position[1]*SCALE-CELL[1]/2}


def make_clip(name, parts, run):
    count,fps = CLIPS[name]
    poses = [pose(i/count) for i in range(count)]
    order = ['body','far_thigh','far_shin','far_foot','near_thigh','near_shin','near_foot','body_cover','head']
    sources = {'body':'torso','body_cover':'torso','head':'head',
               'far_thigh':'leg','far_shin':'leg','near_thigh':'leg','near_shin':'leg',
               'far_foot':'foot','near_foot':'foot'}
    run.mkdir(parents=True,exist_ok=True)
    import_png_groups(run,[{'name':part,'paths':[parts[sources[part]]]*count} for part in order])
    request = json.loads((run/'sprite-request.json').read_text(encoding='utf-8'))
    request['character']['id'] = 'runnerduck-walk-v3'
    request['rig'] = {'profile':'humanoid_biped','landmarks':{}}
    request['fit'] = {'pixel_unfake':False,'resample':'lanczos','ground_frames':False}
    curation = empty_curation()
    for part in order:
        request['states'][part].update(fps=fps,loop=True,
            action='Generated cutout instances transformed by the authored duck rig',
            track='base' if part=='body' else ('action_overlay' if part=='body_cover' else 'prop_effect'))
        request['rig']['landmarks'][part] = {}
        curation['states'][part] = {'selected':list(range(count)),'transforms':{}}
    for i,p in enumerate(poses):
        body = p['body']
        angle = p['bodyAngle']
        head = plus(body,rotate((21.,-19.),angle))
        crown = plus(head,rotate((11.,-24.),angle+p['headAngle']))
        transforms = {'body':instance(body,angle),'body_cover':instance(body,angle),
                      'head':instance(head,angle+p['headAngle'])}
        p['headPivot']=list(head)
        p['hips']=[];p['knees']=[]
        for leg,offset,foot in zip(['far','near'],[(-9.,19.),(4.,19.)],p['feet']):
            hip = plus(body,rotate(offset,angle));ankle=foot['ankle'];joint=knee(hip,ankle)
            p['hips'].append(list(hip));p['knees'].append(list(joint))
            for part,a,b in [(f'{leg}_thigh',hip,joint),(f'{leg}_shin',joint,ankle)]:
                center = ((a[0]+b[0])/2,(a[1]+b[1])/2)
                transforms[part] = instance(center,math.degrees(math.atan2(b[0]-a[0],b[1]-a[1])))
            transforms[f'{leg}_foot']=instance(ankle,foot['angle'])
        for part in order:
            t = transforms[part];curation['states'][part]['transforms'][str(i)] = t
            # All sources are already positioned by curation. A shared world pivot
            # makes composition's integer translation an explicit zero, not a guess.
            points = {'root':[96*SCALE,64*SCALE]}
            if part in ['body','body_cover']:
                points['crown'] = [round(c*SCALE) for c in crown]
            request['rig']['landmarks'][part][str(i)] = points
    request['layers'] = {name:{'stack':[{'state':part,'allow_clip':False} for part in order],
                              'fps':fps,'loop':True}}
    dump(run/'sprite-request.json',request)
    write_curation_atomic(run,curation)
    report = bake(run)
    assert all(e['clipped_pixels']==0 for e in report['composites'][0]['elements'])
    dump(run/'rig-poses.json',poses)
    print(f'{name}: {count} frames at {fps} fps, no clipped layers',flush=True)
    return poses


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--parts',type=Path,required=True)
    parser.add_argument('--out-dir',type=Path,required=True)
    parser.add_argument('--clips',default=','.join(CLIPS))
    args=parser.parse_args()
    out=args.out_dir.resolve();out.mkdir(parents=True,exist_ok=True)
    parts=canonical_parts(args.parts,out/'canonical')
    poses={}
    for name in args.clips.split(','):
        poses[name]=make_clip(name,parts,out/name)
    poses={name:json.loads((out/name/'rig-poses.json').read_text()) for name in CLIPS if (out/name/'rig-poses.json').exists()}
    dump(out/'rig-poses.json',{'runtimeCell':[192,128],'renderScale':SCALE,
                             'segmentLength':LENGTH,'clips':poses})


if __name__=='__main__':
    main()
