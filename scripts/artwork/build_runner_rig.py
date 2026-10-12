"""Bake generated legless runner drawings on the duck's authored walking rig.

Each runner's body comes from one generated drawing. Its head, and optionally a
tail, is cut from that drawing with an overlapping mask so it can turn on a pivot
without exposing a seam. Legs reuse the duck's generated stick and foot cutouts on
fixed-length two-bone legs. Motion is authored, not generated.
"""
from __future__ import annotations

import argparse
import json
import math
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw
from sprite_gen.curate.curation import apply_transform, empty_curation, write_curation_atomic
from sprite_gen.frames.unpack_atlas import import_png_groups
from sprite_gen.compose.compose_layers import bake
from build_duck_rig import CELL, SCALE, TAU, dump, instance, plus, rotate

ROOT = Path(__file__).resolve().parents[2]
CENTER_X = 96.0
LEG_WIDTH = 1.94  # Runtime width of the duck's stick leg.
LEG_OVERHANG = 1.1  # Rounded cap past each joint, as in the duck's 18 px segments.


def load_config(path):
    return json.loads(Path(path).read_text(encoding='utf-8'))


def foot_path(phase, gait, x, ground):
    duty, stride, lift = gait['duty'], gait['stride'], gait['lift']
    p = phase % 1
    if p < duty:
        return {'ankle': [x+stride*(1-2*p/duty), ground], 'angle': 0.0, 'stance': True}
    u = (p-duty)/(1-duty)
    # Matching horizontal velocity at lift-off and touchdown eliminates resets.
    v = -2*stride/duty*(1-duty)
    h00 = 2*u**3-3*u*u+1
    h10 = u**3-2*u*u+u
    h01 = -2*u**3+3*u*u
    h11 = u**3-u*u
    dx = h00*(-stride) + h10*v + h01*stride + h11*v
    return {'ankle': [x+dx, ground-lift*math.sin(math.pi*u)**2],
            'angle': 18*math.sin(TAU*u)*math.sin(math.pi*u)**2, 'stance': False}


def knee(hip, ankle, length, bend):
    dx, dy = ankle[0]-hip[0], ankle[1]-hip[1]
    d = math.hypot(dx, dy)
    if d >= 2*length:
        raise ValueError(f'Unreachable foot target: {d} >= {2*length}')
    h = bend*math.sqrt(length*length-d*d/4)
    return ((hip[0]+ankle[0])/2+dy/d*h, (hip[1]+ankle[1])/2-dx/d*h)


class Body:
    """Maps normalized drawing coordinates to runtime pixels around the body center."""

    def __init__(self, base, spec, ground):
        self.base = base
        self.width = spec['width']
        self.height = self.width*base.height/base.width
        inset = spec['gait']['inset']*base.width/self.width
        self.hips = {leg['name']: self.hip(leg['x'], inset) for leg in spec['legs']}
        first = self.hips[spec['legs'][0]['name']]
        hip_y = ground-spec['gait']['hipHeight']
        self.center = (CENTER_X+spec.get('offsetX', 0.0), hip_y-(first[1]-.5)*self.height)

    def hip(self, u, inset):
        # A hip sits a fixed runtime distance above the drawn silhouette's lower edge.
        alpha = self.base.getchannel('A')
        x = round(u*(self.base.width-1))
        bottom = next(y for y in range(self.base.height-1, -1, -1) if alpha.getpixel((x, y)) > 128)
        y = round(bottom-inset)
        # A slanted or scalloped edge reads as a thicker outline; step up into the white body.
        while min(self.base.getpixel((x, y))[:3]) <= 200:
            y -= 1
        return (u, y/self.base.height)

    def offset(self, uv):
        return ((uv[0]-.5)*self.width, (uv[1]-.5)*self.height)

    def source(self, uv):
        return (uv[0]*self.base.width, uv[1]*self.base.height)


def body_pose(spec, p):
    m = spec['body']
    y = -m['bob']/2*(1-math.cos(TAU*m.get('bobFreq', 2)*p))
    angle = m.get('tiltBase', 0.0)+m['tilt']*math.sin(TAU*m.get('tiltFreq', 2)*p-m.get('tiltLag', 0.0))
    return y, angle


def pose(spec, body, p, ground):
    p %= 1
    dy, angle = body_pose(spec, p)
    center = (body.center[0], body.center[1]+dy)
    gait = spec['gait']
    feet, hips, knees = [], [], []
    for leg in spec['legs']:
        anchor = body.offset(body.hips[leg['name']])
        foot = foot_path(p+leg['phase'], gait, body.center[0]+anchor[0]+leg.get('footDX', 0.0), ground)
        hip = plus(center, rotate(anchor, angle))
        feet.append(foot)
        hips.append(list(hip))
        knees.append(list(knee(hip, foot['ankle'], gait['segment'], leg.get('knee', 1))))
    parts = {}
    for part in spec.get('parts', []):
        swing = part['swing']*math.sin(TAU*part.get('freq', 2)*p-part.get('lag', 0.0))
        parts[part['name']] = {'pivot': list(plus(center, rotate(body.offset(part['pivot']), angle))),
                               'angle': angle+swing}
    return {'body': list(center), 'bodyAngle': angle, 'parts': parts,
            'feet': feet, 'hips': hips, 'knees': knees}


def region_mask(body, polygon, grow=0.0, steps=12):
    mask = Image.new('L', body.base.size, 0)
    draw = ImageDraw.Draw(mask)
    points = [body.source(uv) for uv in polygon]
    # The overlap stays opaque over the body's cut edge, then fades so a turned
    # part's outline blends into the body's outline instead of stepping.
    for i in range(steps, 0, -1) if grow else []:
        distance = grow*i/steps
        opacity = min(1.0, (1-distance/grow)/.65+.02)
        draw.line(points+points[:1], fill=round(255*opacity), width=round(2*distance), joint='curve')
    draw.polygon(points, fill=255)
    return mask


def frame_part(image, pivot, scale, path):
    box = image.getchannel('A').getbbox()
    crop = image.crop(box)
    point = (pivot[0]-box[0], pivot[1]-box[1])
    framed = apply_transform(crop, {'scale': scale, 'dx': -(point[0]-crop.width/2)*scale,
                                    'dy': -(point[1]-crop.height/2)*scale}, CELL)
    edges = framed.getchannel('A').getbbox()
    assert edges and edges[0] > 0 and edges[1] > 0 and edges[2] < CELL[0] and edges[3] < CELL[1], path
    path.parent.mkdir(parents=True, exist_ok=True)
    framed.save(path)
    return path


def canonical_parts(spec, body, duck_parts, out):
    scale = body.width*SCALE/body.base.width
    alpha = body.base.getchannel('A')
    removed = Image.new('L', body.base.size, 0)
    paths = {}
    for part in spec.get('parts', []):
        region = region_mask(body, part['region'])
        removed.paste(255, mask=region)
        # The cut part overlaps the body so its turn never opens a seam.
        grown = region_mask(body, part['region'], part.get('overlap', 4.0)*SCALE/scale)
        image = body.base.copy()
        image.putalpha(ImageChops.multiply(alpha, grown))
        paths[part['name']] = frame_part(image, body.source(part['pivot']), scale, out/f"{part['name']}.png")
    torso = body.base.copy()
    torso.putalpha(Image.composite(Image.new('L', alpha.size, 0), alpha, removed))
    paths['torso'] = frame_part(torso, body.source((.5, .5)), scale, out/'torso.png')
    for name, hip in body.hips.items():
        r, g, b, a = (int(c) for c in body.base.getpixel(body.source(hip)))
        assert a == 255 and min(r, g, b) > 200, f'{name} hip must sit inside the white body'
    leg = Image.open(duck_parts/'leg.png').convert('RGBA')
    leg = leg.crop(leg.getchannel('A').getbbox())
    length = spec['gait']['segment']+2*LEG_OVERHANG
    leg = leg.resize((leg.width, round(leg.width*length/LEG_WIDTH)), Image.Resampling.LANCZOS)
    paths['leg'] = frame_part(leg, (leg.width/2, leg.height/2), LEG_WIDTH*SCALE/leg.width, out/'leg.png')
    foot = Image.open(duck_parts/'foot.png').convert('RGBA')
    foot = foot.crop(foot.getchannel('A').getbbox())
    paths['foot'] = frame_part(foot, (.32*foot.width, .52*foot.height),
                               spec['gait']['foot']*SCALE/foot.width, out/'foot.png')
    return paths


def make_walk(name, spec, body, parts, run, ground):
    count = spec['frames']
    fps = spec.get('fps', count)
    poses = [pose(spec, body, i/count, ground) for i in range(count)]
    legs = [leg['name'] for leg in spec['legs']]
    order = (['body']+[f'{leg}_{bone}' for leg in legs for bone in ['thigh', 'shin', 'foot']]
             + ['body_cover']+[part['name'] for part in spec.get('parts', [])])
    sources = {'body': 'torso', 'body_cover': 'torso'}
    sources.update({f'{leg}_{bone}': 'foot' if bone == 'foot' else 'leg'
                    for leg in legs for bone in ['thigh', 'shin', 'foot']})
    sources.update({part['name']: part['name'] for part in spec.get('parts', [])})
    run.mkdir(parents=True, exist_ok=True)
    import_png_groups(run, [{'name': part, 'paths': [parts[sources[part]]]*count} for part in order])
    request = json.loads((run/'sprite-request.json').read_text(encoding='utf-8'))
    request['character']['id'] = f'runnerduck-{name}-walk'
    request['rig'] = {'profile': 'humanoid_biped', 'landmarks': {}}
    request['fit'] = {'pixel_unfake': False, 'resample': 'lanczos', 'ground_frames': False}
    curation = empty_curation()
    for part in order:
        request['states'][part].update(fps=fps, loop=True,
            action=f'Generated {name} cutout instances transformed by the authored runner rig',
            track='base' if part == 'body' else ('action_overlay' if part == 'body_cover' else 'prop_effect'))
        request['rig']['landmarks'][part] = {}
        curation['states'][part] = {'selected': list(range(count)), 'transforms': {}}
    for i, p in enumerate(poses):
        transforms = {'body': instance(p['body'], p['bodyAngle']), 'body_cover': instance(p['body'], p['bodyAngle'])}
        for part, state in p['parts'].items():
            transforms[part] = instance(state['pivot'], state['angle'])
        for leg, hip, joint, foot in zip(legs, p['hips'], p['knees'], p['feet']):
            ankle = foot['ankle']
            for bone, a, b in [('thigh', hip, joint), ('shin', joint, ankle)]:
                center = ((a[0]+b[0])/2, (a[1]+b[1])/2)
                transforms[f'{leg}_{bone}'] = instance(center, math.degrees(math.atan2(b[0]-a[0], b[1]-a[1])))
            transforms[f'{leg}_foot'] = instance(ankle, foot['angle'])
        top = plus(p['body'], rotate((0., -body.height/2), p['bodyAngle']))
        for part in order:
            curation['states'][part]['transforms'][str(i)] = transforms[part]
            # Sources are positioned by curation; a shared world pivot keeps composition's
            # integer translation an explicit zero.
            points = {'root': [96*SCALE, 64*SCALE]}
            if part in ['body', 'body_cover']:
                points['crown'] = [round(c*SCALE) for c in top]
            request['rig']['landmarks'][part][str(i)] = points
    request['layers'] = {'walk': {'stack': [{'state': part, 'allow_clip': False} for part in order],
                                  'fps': fps, 'loop': True}}
    dump(run/'sprite-request.json', request)
    write_curation_atomic(run, curation)
    report = bake(run)
    assert all(e['clipped_pixels'] == 0 for e in report['composites'][0]['elements'])
    print(f'{name}: {count} frames at {fps} fps, no clipped layers', flush=True)
    return poses


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--config', type=Path, default=ROOT/'artwork/runners/runners.json')
    parser.add_argument('--duck-parts', type=Path, default=ROOT/'artwork/duck/parts')
    parser.add_argument('--out-dir', type=Path, required=True)
    parser.add_argument('--runners', default='')
    args = parser.parse_args()
    config = load_config(args.config)
    ground = config['groundY']
    out = args.out_dir.resolve()
    names = args.runners.split(',') if args.runners else list(config['runners'])
    for name in names:
        spec = config['runners'][name]
        base = Image.open(args.config.parent/name/'base.png').convert('RGBA')
        body = Body(base.crop(base.getchannel('A').getbbox()), spec, ground)
        parts = canonical_parts(spec, body, args.duck_parts, out/name/'canonical')
        poses = make_walk(name, spec, body, parts, out/name/'walk', ground)
        dump(out/name/'rig-poses.json', {'runtimeCell': [192, 128], 'renderScale': SCALE,
                                         'groundY': ground, 'segmentLength': spec['gait']['segment'],
                                         'legs': [leg['name'] for leg in spec['legs']],
                                         'clips': {'walk': poses}})


if __name__ == '__main__':
    main()
