# -*- coding: utf-8 -*-
"""그림에 뼈대(리깅) 심기 — 좀비·카피바라 한 장 그림을 뼈로 휘게 만들 데이터.

결과: Kachi_Gun/Assets/Resources/Art/Rig/<이름>.txt  (게임의 Core/Rig.cs 가 읽어 Unity 2D Animation SpriteSkin 으로 움직임)
  첫 줄들: src <그림 경로> <칸 번호> / cell <폭> <높이> / pivot <x> <y> / ppu <값> / kind <walk|robe|crawl|capy>
  bone <이름> <부모 번호> <x> <y>              (칸 픽셀, 아래가 0 — Unity 와 같은 위쪽 +)
  v <x> <y> <뼈0> <무게0> <뼈1> <무게1> <뼈2> <무게2> <뼈3> <무게3>
  t <a> <b> <c>                                (그리는 순서 = 줄 순서: 뒷팔 → 뒷다리 → 몸 → 앞다리 → 앞팔)
미리보기(게임과 같은 자세 계산): tools/_preview/rig_<이름>.png  (걷기 8장)

사용: python tools/rig_gen.py [이름...]     (이름 없으면 전부)
뼈 자리 숫자는 아래 ZOMBIE_RIG / capy_rig(). 그림은 slice_assets.py 가 만든 시트(좀비 = 걷기 1번 칸 = 원래 자세).
"""
import os, sys, math
import numpy as np
import cv2
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
ART = os.path.join(ROOT, 'Kachi_Gun', 'Assets', 'Resources', 'Art')
OUT = os.path.join(ART, 'Rig')
PREV = os.path.join(os.path.dirname(__file__), '_preview')
G = 8                       # 그물 칸(px)

# 좀비 뼈 자리 (칸 400x380, 위가 0 인 그림 좌표로 적음 — 내보낼 때 아래가 0 으로 바꿈)
#   neck 목 · hip 엉덩이(골반) · crotch 가랑이(이 아래부터 두 다리 분리) · knee 무릎 · split 앞/뒷다리 나누는 x
#   armB / armF = ([(x0, y0, x1, y1), ...], (어깨x, 어깨y)) 팔(+든 무기) 범위 상자들, 없으면 None. swing = 팔 흔들기 세기
ZOMBIE_RIG = {
    'armor':  dict(neck=212, hip=292, crotch=306, knee=338, split=215, armB=([(70, 185, 144, 345)], (132, 205)), armF=([(290, 185, 362, 345)], (296, 205)), legA=14, swing=9),
    'basic':  dict(neck=250, hip=304, crotch=316, knee=343, split=186, armB=None, armF=([(218, 252, 290, 300)], (205, 266)), legA=20, swing=4),
    'bat':    dict(neck=226, hip=300, crotch=312, knee=340, split=224, armB=([(118, 232, 178, 336)], (172, 242)), armF=([(280, 236, 400, 318), (318, 150, 400, 236)], (276, 248)), legA=18, swing=6),
    'farmer': dict(neck=236, hip=300, crotch=312, knee=340, split=212, armB=([(110, 242, 168, 334)], (164, 250)), armF=([(262, 244, 400, 318), (300, 170, 400, 244)], (252, 252)), legA=18, swing=6),
    'fat':    dict(neck=202, hip=318, crotch=326, knee=350, split=210, armB=([(80, 196, 134, 322)], (128, 208)), armF=([(282, 196, 336, 322)], (284, 208)), legA=12, swing=7),
    'hat':    dict(neck=240, hip=302, crotch=315, knee=342, split=212, armB=None, armF=([(232, 256, 310, 302)], (218, 268)), legA=19, swing=4),
    'knife':  dict(neck=236, hip=300, crotch=312, knee=340, split=215, armB=([(110, 240, 164, 336)], (160, 250)), armF=([(282, 244, 400, 318), (330, 160, 400, 244)], (278, 252)), legA=18, swing=6),
    'mummy':  dict(neck=222, hip=298, crotch=310, knee=340, split=178, armB=None, armF=([(232, 246, 300, 332)], (220, 258)), legA=19, swing=4),
    'ogre':   dict(neck=236, hip=310, crotch=320, knee=346, split=190, armB=([(20, 196, 132, 370)], (128, 214)), armF=([(262, 262, 350, 362)], (262, 266)), legA=12, swing=7),
    'reaper': dict(neck=262, hip=300, crotch=999, knee=340, split=200, armB=None, armF=None, legA=0, swing=0, robe=True),
}
CRAWL_RIG = {
    # 기어 다니는 좀비: 몸이 가로 — legs(뒤로 끌리는 다리) · pelvis(엉덩이) · spine(몸) · head · armB/armF(번갈아 뻗는 팔)
    #   legs_x 왼쪽 = 다리, neck_x 오른쪽·arm_top 위 = 머리, 팔 = (상자들, 어깨)
    'crawler': dict(hip=(125, 318), legs_x=118, neck_x=182, arm_top=318,
                    armB=([(165, 318, 228, 380)], (195, 320)), armF=([(228, 316, 300, 380)], (238, 320))),
    'bucket':  dict(hip=(130, 318), legs_x=125, neck_x=186, arm_top=318,
                    armB=([(175, 318, 236, 380)], (202, 320)), armF=([(236, 316, 304, 380)], (246, 320))),
}


def crawl_rig(name):
    p = CRAWL_RIG[name]
    img = load_cell(os.path.join(ART, 'Zombie', 'z_%s.png' % name), 1, 400, 380)
    rig = Rig('z_' + name, img, 'crawl', 165, (200, 4))
    rig.crotch_img = 9999
    hx, hy = p['hip']
    pel = rig.bone('pelvis', -1, hx, hy)
    legs = rig.bone('legs', pel, p['legs_x'], hy)
    spine = rig.bone('spine', pel, hx, hy)
    head = rig.bone('head', spine, p['neck_x'], p['arm_top'] - 20)
    arms = {}
    for k in ('armB', 'armF'):
        boxes, (sx, sy) = p[k]
        arms[k] = (rig.bone(k, spine, sx, sy), boxes)

    def region(cx, cy):
        for k in ('armB', 'armF'):
            for x0, y0, x1, y1 in arms[k][1]:
                if x0 <= cx < x1 and y0 <= cy < y1:
                    return k
        return 'upper'

    def weight(x, y, side):
        lg = smooth(p['legs_x'] + 8, p['legs_x'] - 14, x)
        hd = smooth(p['neck_x'] - 10, p['neck_x'] + 12, x) * smooth(p['arm_top'] + 6, p['arm_top'] - 6, y)
        pv = smooth(hx + 30, hx - 6, x) * (1 - lg)
        w = {legs: lg, head: hd * (1 - lg), pel: pv * (1 - hd), spine: (1 - lg) * (1 - hd) * (1 - pv)}
        for k, (bi, boxes) in arms.items():
            a = 0.0
            for x0, y0, x1, y1 in boxes:
                a = max(a, smooth(-4, 10, y - y0) * smooth(-4, 6, x - x0) * smooth(-4, 6, x1 - x))
            if a > 0:
                for b in w: w[b] *= (1 - a)
                w[bi] = w.get(bi, 0) + a
        return {b: val for b, val in w.items() if val > 1e-4}

    rig.anim = {'A': 12, 'S': 0}
    build_mesh(rig, region, weight, ['armB', 'upper', 'armF'])
    rig.region_fn = region
    rig.write('Zombie/z_%s' % name, (400, 0, 400, 380))
    return rig, p


# 카피바라(스킨 0~16): 시트 4번 칸(사격 자세, 총은 떼어 냄), 칸 360, 발 = (160, 바닥 8px 위)
CAPY = dict(neck=198, hip=300, crotch=318, split=165)


def load_cell_grid(path, idx, cell):
    a = np.array(Image.open(path).convert('RGBA'))
    r, c = idx // 4, idx % 4
    return a[r * cell:(r + 1) * cell, c * cell:(c + 1) * cell].copy()


def capy_rig(skin):
    p = CAPY
    img = load_cell_grid(os.path.join(ART, 'Char', 'skin_%02d.png' % skin), 4, 360)
    rig = Rig('c_%02d' % skin, img, 'capy', 150, (160, 8))
    rig.crotch_img = p['crotch']
    ys, xs = np.nonzero(img[..., 3] > 0)
    bottom = ys.max()
    pel = rig.bone('pelvis', -1, p['split'], p['hip'])
    spine = rig.bone('spine', pel, p['split'], p['hip'])
    row = np.nonzero(img[p['neck'], :, 3] > 0)[0]
    head = rig.bone('head', spine, float(row.mean()) if len(row) else 170, p['neck'])
    lB = rig.bone('legB', pel, p['split'] - 14, p['crotch'] - 8)
    lF = rig.bone('legF', pel, p['split'] + 14, p['crotch'] - 8)

    def region(cx, cy):
        if cy > p['crotch']:
            return 'legB' if cx < p['split'] else 'legF'
        return 'upper'

    def weight(x, y, side):
        h = smooth(p['neck'] + 14, p['neck'] - 10, y)
        v = smooth(p['hip'] - 46, p['hip'] + 4, y)
        w = {head: h, spine: (1 - h) * (1 - v), pel: (1 - h) * v}
        fs = smooth(p['split'] - 8, p['split'] + 8, x) if side is None else (1.0 if side == 'F' else 0.0)
        u = smooth(p['crotch'] - 12, p['crotch'] + 4, y)
        for b in w: w[b] *= (1 - u)
        w[lF] = w.get(lF, 0) + u * fs
        w[lB] = w.get(lB, 0) + u * (1 - fs)
        return {b: val for b, val in w.items() if val > 1e-4}

    rig.anim = {'A': 16, 'S': 0, 'groundY': float(rig.H - bottom)}
    build_mesh(rig, region, weight, ['legB', 'upper', 'legF'])
    rig.region_fn = region
    rig.write('Char/skin_%02d' % skin, (0, 1440 - 720, 360, 360))
    return rig, p


def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def load_cell(path, col, cw, ch):
    a = np.array(Image.open(path).convert('RGBA'))
    return a[0:ch, col * cw:(col + 1) * cw].copy()


# ------------------------------------------------------------------ 그물 + 무게
class Rig:
    def __init__(self, name, img, kind, ppu, pivot):
        self.name, self.img, self.kind, self.ppu, self.pivot = name, img, kind, ppu, pivot
        self.H, self.W = img.shape[:2]
        self.bones = []      # (이름, 부모, x, y_up)
        self.verts = []      # (x, y_up, {bone: w})
        self.tris = []

    def bone(self, name, parent, x, y_img):
        self.bones.append((name, parent, float(x), float(self.H - y_img)))
        return len(self.bones) - 1

    def write(self, src_rel, rect):
        os.makedirs(OUT, exist_ok=True)
        lines = [f'src {src_rel}', 'rect %d %d %d %d' % rect, f'cell {self.W} {self.H}', f'pivot {self.pivot[0]} {self.pivot[1]}',
                 f'ppu {self.ppu}', f'kind {self.kind}']
        a = getattr(self, 'anim', {})
        lines.append('anim ' + ' '.join('%s=%.2f' % kv for kv in a.items()))
        for n, p, x, y in self.bones:
            lines.append(f'bone {n} {p} {x:.1f} {y:.1f}')
        for x, y, w in self.verts:
            items = sorted(w.items(), key=lambda kv: -kv[1])[:4]
            s = sum(v for _, v in items)
            items = [(b, v / s) for b, v in items] + [(0, 0.0)] * (4 - len(items))
            lines.append('v %.1f %.1f ' % (x, y) + ' '.join('%d %.4f' % (b, v) for b, v in items))
        for a, b, c in self.tris:
            lines.append(f't {a} {b} {c}')
        with open(os.path.join(OUT, self.name + '.txt'), 'w', encoding='utf-8') as f:
            f.write('\n'.join(lines) + '\n')


def build_mesh(rig, region_of_cell, weight_fn, order):
    """region_of_cell(cx, cy_img) → 구역 이름(그리는 순서용) / weight_fn(x, y_img, side) → {뼈: 무게}
    side = 'B'·'F'(가랑이 아래 다리 꼭짓점) 또는 None. 다리 구역(legB·legF)은 가랑이 아래에서 꼭짓점을 따로 가져
    두 다리가 떨어져 움직인다. 나머지 꼭짓점은 이웃 칸과 공유(무게는 자리로만 정해서 이음매가 안 벌어짐)"""
    m = rig.img[..., 3] > 0
    m = cv2.dilate(m.astype(np.uint8), np.ones((3, 3), np.uint8)) > 0
    H, W = m.shape
    cells = {}
    for gy in range(0, H, G):
        for gx in range(0, W, G):
            if m[gy:gy + G, gx:gx + G].any():
                cells[(gx, gy)] = region_of_cell(gx + G / 2, gy + G / 2)
    vid = {}

    def vert(x, y, side):
        key = (x, y, side)
        if key not in vid:
            vid[key] = len(rig.verts)
            rig.verts.append((float(x), float(H - y), weight_fn(x, y, side)))
        return vid[key]
    by_region = {}
    for (gx, gy), r in cells.items():
        by_region.setdefault(r, []).append((gx, gy))
    for r in order:
        for gx, gy in sorted(by_region.get(r, [])):
            leg = r in ('legB', 'legF')

            def V(x, y):
                side = (r[-1] if leg and y > rig.crotch_img else None)
                return vert(x, y, side)
            a, b, c, d = V(gx, gy), V(gx + G, gy), V(gx + G, gy + G), V(gx, gy + G)
            rig.tris += [(a, c, b), (a, d, c)]


def zombie_rig(name):
    p = ZOMBIE_RIG[name]
    img = load_cell(os.path.join(ART, 'Zombie', 'z_%s.png' % name), 1, 400, 380)
    rig = Rig('z_' + name, img, 'robe' if p.get('robe') else 'walk', 165, (200, 4))
    rig.crotch_img = p['crotch']
    ys, xs = np.nonzero(img[..., 3] > 0)
    top, bottom = ys.min(), ys.max()
    hipx = p['split']
    pel = rig.bone('pelvis', -1, hipx, p['hip'])
    spine = rig.bone('spine', pel, hipx, p['hip'])
    # 목 x = 목 높이 줄의 가운데
    row = np.nonzero(img[p['neck'], :, 3] > 0)[0]
    neckx = float(row.mean()) if len(row) else hipx
    head = rig.bone('head', spine, neckx, p['neck'])
    if p.get('robe'):
        r1 = rig.bone('robe1', pel, hipx, p['hip'])
        r2 = rig.bone('robe2', r1, hipx, p['knee'])
        legs = None
    else:
        tB = rig.bone('thighB', pel, p['split'] - 10, p['hip'])
        sB = rig.bone('shinB', tB, p['split'] - 14, p['knee'])
        tF = rig.bone('thighF', pel, p['split'] + 10, p['hip'])
        sF = rig.bone('shinF', tF, p['split'] + 14, p['knee'])
        legs = (tB, sB, tF, sF)
    arms = {}
    for k in ('armB', 'armF'):
        if p.get(k):
            boxes, (sx, sy) = p[k]
            arms[k] = (rig.bone(k, spine, sx, sy), boxes)

    def region(cx, cy):
        for k in ('armB', 'armF'):
            if k in arms:
                for x0, y0, x1, y1 in arms[k][1]:
                    if x0 <= cx < x1 and y0 <= cy < y1:
                        return k
        if legs and cy > p['crotch']:
            return 'legB' if cx < p['split'] else 'legF'
        return 'upper'

    def weight(x, y, side):
        # 몸통: 머리 / 허리 / 골반
        h = smooth(p['neck'] + 10, p['neck'] - 10, y)
        v = smooth(p['hip'] - 34, p['hip'] + 2, y)
        w = {head: h, spine: (1 - h) * (1 - v), pel: (1 - h) * v}
        if legs:
            tB, sB, tF, sF = legs
            fs = smooth(p['split'] - 10, p['split'] + 10, x) if side is None else (1.0 if side == 'F' else 0.0)
            u = smooth(p['hip'] - 4, p['crotch'] + 8, y)
            kn = smooth(p['knee'] - 10, p['knee'] + 10, y)
            for b in w: w[b] *= (1 - u)
            for b, val in {tF: fs * (1 - kn), sF: fs * kn, tB: (1 - fs) * (1 - kn), sB: (1 - fs) * kn}.items():
                w[b] = w.get(b, 0) + val * u
        elif p.get('robe'):
            r1 = [b[0] for b in rig.bones].index('robe1'); r2 = r1 + 1
            u = smooth(p['hip'] - 6, p['hip'] + 20, y)
            k2 = smooth(p['knee'] - 14, p['knee'] + 30, y)
            for b in w: w[b] *= (1 - u)
            w[r1] = u * (1 - k2); w[r2] = u * k2
        # 팔(+든 무기): 범위 안 = 팔 뼈. 몸 쪽 가장자리·위아래 가장자리는 부드럽게 섞음
        if side is None:
            for k, (bi, boxes) in arms.items():
                a = 0.0
                for x0, y0, x1, y1 in boxes:
                    edge = (x - x0) if k == 'armF' else (x1 - x)
                    far = (x1 - x) if k == 'armF' else (x - x0)
                    a = max(a, smooth(-4, 10, edge) * smooth(-4, 6, y - y0) * smooth(-4, 6, y1 - y) * (1.0 if far >= -2 else 0.0))
                if a > 0:
                    for b in w: w[b] *= (1 - a)
                    w[bi] = w.get(bi, 0) + a
        return {b: val for b, val in w.items() if val > 1e-4}

    # 원래 그림의 다리 각도(세로 0°, 앞 +) · 다리 길이 — 걷기 자세 계산 기준
    rig.anim = {'A': p.get('legA', 18), 'S': p.get('swing', 5), 'L': 60.0, 'aF': 0.0, 'aB': 0.0}
    if legs:
        band = (ys >= bottom - 14)
        for k, sel, px in (('aB', xs < p['split'], p['split'] - 10), ('aF', xs >= p['split'], p['split'] + 10)):
            fx = xs[band & sel]
            if len(fx):
                rig.anim[k] = math.degrees(math.atan2(float(np.mean(fx)) - px, bottom - p['hip']))
        rig.anim['L'] = float(bottom - p['hip'])
        for k, sel in (('B', xs < p['split']), ('F', xs >= p['split'])):
            fx = xs[band & sel]
            rig.anim['foot' + k + 'x'] = float(np.mean(fx)) if len(fx) else float(p['split'])
        rig.anim['groundY'] = float(rig.H - bottom)
    order = ['armB', 'legB', 'upper', 'legF', 'armF']
    build_mesh(rig, region, weight, order)
    rig.region_fn = region
    rig.write('Zombie/z_%s' % name, (400, 0, 400, 380))
    return rig, p


# ------------------------------------------------------------------ 자세 계산 (게임 Rig.cs 와 같은 식)
def pose_angles(kind, an, phase):
    """phase 0~1 = 두 걸음(왼발·오른발). an = 파일의 anim 값(A 보폭°, S 팔 흔들기°, L 다리 길이 px, aF·aB 원래 다리 각도°).
    돌려주는 값: {뼈 이름: (각도°, dx, dy)} — 오른쪽을 보는 그림 기준, 위쪽 +, 각도 + = 반시계(앞으로).
    원래 그림 자세 = phase 0 (앞다리 앞, 뒷다리 뒤) → phase 0.5 에 두 다리가 자리를 바꿈.
    골반 높이 = 땅을 딛고 있는 발이 바닥에 붙도록(다리가 세로일 때 몸이 올라감)."""
    f = 2 * math.pi * phase
    A, S, L = an['A'], an.get('S', 0), an.get('L', 60)
    out = {}
    if kind == 'walk':
        # 3/4 앞모습 그림이라 두 다리가 좌우로 벌어져 있음 → 다리를 맞바꾸지 않고, 벌린 자세에서
        # 번갈아 발을 들어(무릎 굽힘) 앞으로 내딛고, 딛고 있는 다리는 뒤로 밂(터벅터벅 걷기)
        sw = A * 0.5
        lF, lB = max(0.0, math.sin(f)), max(0.0, -math.sin(f))       # 지금 드는 발
        out['thighF'] = (sw * math.sin(f) + 10 * lF, 0, 0)
        out['thighB'] = (-sw * math.sin(f) + 10 * lB, 0, 0)
        out['shinF'] = (-26 * lF, 0, 0)
        out['shinB'] = (-26 * lB, 0, 0)
        out['pelvis'] = (0, 0, 0)       # 골반 높이는 deform() 이 발 위치로 맞춤(땅에 디딘 발 = 바닥)
        out['spine'] = (-3 + 1.5 * math.sin(2 * f + 0.5), 0, 0)
        out['head'] = (2 - 3.0 * math.sin(2 * f - 0.4), 0, 0)
        out['armF'] = (S * 0.5 * math.sin(2 * f + 1.2) - S * 0.6 * math.cos(f), 0, 0)
        out['armB'] = (S * math.cos(f), 0, 0)
    elif kind == 'crawl':
        # 두 팔이 번갈아 앞으로 뻗고(뻗는 팔은 살짝 들림), 몸은 앞뒤로 출렁, 다리는 끌림
        out['armF'] = (14 * math.sin(f), 0, 2.5 * max(0.0, math.cos(f)))
        out['armB'] = (-14 * math.sin(f), 0, 2.5 * max(0.0, -math.cos(f)))
        out['spine'] = (2.0 * math.sin(2 * f + 0.6), 0, 0)
        out['head'] = (-2.5 * math.sin(2 * f + 1.2), 0, 1.5 * math.sin(2 * f))
        out['pelvis'] = (0, 2.0 * math.sin(2 * f), 0)
        out['legs'] = (3.0 * math.sin(f + 0.5), 0, 0)
    elif kind == 'capy':
        # 미리보기: 걷기 (게임에선 숨쉬기·반동·조준도 Capy.cs 가 같은 뼈로)
        lF, lB = max(0.0, math.sin(f)), max(0.0, -math.sin(f))
        out['legF'] = (A * 0.6 * math.sin(f) + 6 * lF, 0, 5 * lF)
        out['legB'] = (-A * 0.6 * math.sin(f) + 6 * lB, 0, 5 * lB)
        out['pelvis'] = (0, 0, 4 * abs(math.sin(f)))
        out['spine'] = (-2 * math.sin(f), 0, 0)
        out['head'] = (1.5 * math.sin(f - 0.5), 0, 0)
    elif kind == 'robe':
        out['pelvis'] = (0, 0, 2.0 * math.cos(2 * f))
        out['robe1'] = (3.5 * math.sin(f), 0, 0)
        out['robe2'] = (6 * math.sin(f - 0.9), 0, 0)
        out['spine'] = (-3 + 1.5 * math.sin(2 * f + 0.4), 0, 0)
        out['head'] = (2 - 2.5 * math.sin(2 * f - 0.6), 0, 0)
    return out


def bone_mats(rig, angles, lift=0.0):
    n = len(rig.bones)
    M = [None] * n
    for i, (name, par, x, y) in enumerate(rig.bones):
        a, dx, dy = angles.get(name, (0, 0, 0))
        if par < 0: dy += lift
        lx, ly = (x, y) if par < 0 else (x - rig.bones[par][2], y - rig.bones[par][3])
        r = math.radians(a)
        L = np.array([[math.cos(r), -math.sin(r), lx + dx], [math.sin(r), math.cos(r), ly + dy], [0, 0, 1]])
        M[i] = L if par < 0 else M[par] @ L
    return [M[i] @ np.array([[1, 0, -rig.bones[i][2]], [0, 1, -rig.bones[i][3]], [0, 0, 1]]) for i in range(n)]


def foot_lift(rig, angles):
    """다리를 돌린 뒤 낮은 쪽 발이 원래 바닥에 오도록 골반을 올리고 내릴 값 (게임 Rig.cs 도 같은 계산)"""
    an = getattr(rig, 'anim', {})
    if 'footFx' not in an:
        return 0.0
    names = [b[0] for b in rig.bones]
    K = bone_mats(rig, angles)
    g = an['groundY']
    ys = []
    for k in ('F', 'B'):
        sh = names.index('shin' + k)
        ys.append((K[sh] @ np.array([an['foot' + k + 'x'], g, 1.0]))[1])
    return g - min(ys)


def deform(rig, angles):
    """뼈 자세 → 꼭짓점 새 자리 (선형 블렌드 스키닝)"""
    K = bone_mats(rig, angles, foot_lift(rig, angles))
    out = []
    for x, y, w in rig.verts:
        v = np.array([x, y, 1.0])
        s = sum(val * (K[b] @ v) for b, val in w.items())
        tot = sum(w.values())
        out.append((s[0] / tot, s[1] / tot))
    return out


def region_png(rig):
    col = {'upper': (255, 255, 255), 'legB': (80, 140, 255), 'legF': (60, 220, 90), 'armB': (255, 160, 40), 'armF': (255, 60, 200)}
    im = rig.img.copy().astype(float)
    for gy in range(0, rig.H, G):
        for gx in range(0, rig.W, G):
            blk = im[gy:gy + G, gx:gx + G]
            m = blk[..., 3] > 0
            if m.any():
                c = np.array(col[rig.region_fn(gx + G / 2, gy + G / 2)], float)
                blk[..., :3][m] = blk[..., :3][m] * 0.45 + c * 0.55
    return im.astype(np.uint8)


def render(rig, pts, W=None, H=None):
    W = W or rig.W; H = H or rig.H
    out = np.zeros((H, W, 4), np.uint8)
    src = rig.img
    for a, b, c in rig.tris:
        s = np.float32([[rig.verts[i][0], rig.H - rig.verts[i][1]] for i in (a, b, c)])
        d = np.float32([[pts[i][0], H - pts[i][1]] for i in (a, b, c)])
        x0, y0 = np.floor(d.min(0)).astype(int); x1, y1 = np.ceil(d.max(0)).astype(int) + 1
        x0, y0 = max(x0, 0), max(y0, 0); x1, y1 = min(x1, W), min(y1, H)
        if x1 <= x0 or y1 <= y0:
            continue
        M = cv2.getAffineTransform(s, d - np.float32([x0, y0]))
        patch = cv2.warpAffine(src, M, (x1 - x0, y1 - y0), flags=cv2.INTER_NEAREST, borderMode=cv2.BORDER_CONSTANT, borderValue=(0, 0, 0, 0))
        mask = np.zeros((y1 - y0, x1 - x0), np.uint8)
        cv2.fillConvexPoly(mask, np.int32(np.round((d - np.float32([x0, y0])) * 4)), 1, shift=2)
        sel = (mask > 0) & (patch[..., 3] > 0)
        out[y0:y1, x0:x1][sel] = patch[sel]
    return out


def preview(rig, p, frames=8):
    tiles = []
    for k in range(frames):
        ang = pose_angles(rig.kind, rig.anim, k / frames)
        tiles.append(render(rig, deform(rig, ang)))
    W, H = rig.W, rig.H
    if hasattr(rig, 'region_fn'):
        tiles = [region_png(rig)] + tiles
    sheet = Image.new('RGBA', (W * len(tiles), H), (90, 90, 120, 255))
    for k, t in enumerate(tiles):
        sheet.alpha_composite(Image.fromarray(t), (k * W, 0))
    os.makedirs(PREV, exist_ok=True)
    sheet.convert('RGB').save(os.path.join(PREV, 'rig_%s.png' % rig.name))


if __name__ == '__main__':
    names = sys.argv[1:] or (list(ZOMBIE_RIG) + list(CRAWL_RIG) + ['capy%02d' % i for i in range(17)])
    for n in names:
        if n in ZOMBIE_RIG:
            rig, p = zombie_rig(n)
        elif n in CRAWL_RIG:
            rig, p = crawl_rig(n)
        elif n.startswith('capy'):
            rig, p = capy_rig(int(n[4:]))
        else:
            continue
        preview(rig, p)
        print(n, 'bones', len(rig.bones), 'verts', len(rig.verts), 'tris', len(rig.tris))
