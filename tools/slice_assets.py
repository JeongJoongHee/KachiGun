# -*- coding: utf-8 -*-
"""원본 그림(폴더 맨 위의 PNG)을 게임용 낱장/정리 시트로 잘라 Kachi_Gun/Assets/Resources/Art 에 저장한다.
다시 돌려도 같은 결과가 나온다. 원본은 건드리지 않는다.
사용: python tools/slice_assets.py   (미리보기: tools/_preview/sliced_*.png)

결과
- Art/Char/skin_00.png ~ skin_16.png : 캐릭터 시트(4열 x 4줄, 칸 CELL x CELL, 칸 아래 가운데가 발)
    줄0 대기 4장 · 줄1 사격 4장 · 줄2 기타 3장 · 줄3 쓰러짐 4장   (skin_00 = Main_Character)
- Art/Zombie/z_<이름>.png : 좀비 시트(3열: 걷기·맞기·죽기, 칸 ZW x ZH, 아래 가운데 정렬)
- Art/UI/*.png, Art/Bullet/*.png, Art/Map/main_map.png
"""
import os, sys, glob, math
import numpy as np
import cv2
from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from bgcut import cut_white, load
import gun_split

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
OUT = os.path.join(ROOT, 'Kachi_Gun', 'Assets', 'Resources', 'Art')

def src(name):
    """원본 그림 경로: 폴더 맨 위 또는 리소스 폴더"""
    p = os.path.join(ROOT, name)
    return p if os.path.exists(p) else os.path.join(ROOT, '리소스', name)
PREV = os.path.join(os.path.dirname(__file__), '_preview')
os.makedirs(PREV, exist_ok=True)

# 게임에서 더 이상 안 쓰는 그림(10-19 UI 팩으로 교체·가로 맵으로 교체) — 다른 그림을 만드는 중간 재료라
# 게임 폴더 대신 tools/_build 에 저장한다
BUILD = os.path.join(os.path.dirname(__file__), '_build')
RETIRED = {'UI/%s.png' % n for n in ('btn_boss', 'btn_challenge', 'btn_mine', 'btn_ok', 'btn_pistol', 'btn_play', 'btn_shop', 'btn_sword', 'btn_weapon', 'card_1', 'card_2', 'card_3', 'card_base', 'edge_left', 'edge_right', 'icon_sword', 'panel_big', 'panel_mid', 'panel_small', 'panel_wide', 'plank_a', 'plank_b', 'plank_c', 'plank_d', 'plank_frame', 'plank_thin', 'stat_spd', 'statbox_atk', 'statbox_gold', 'statbox_spd')} | {'Map/main_map.png'}

def out_path(rel):
    return os.path.join(BUILD if rel in RETIRED else OUT, rel)

def save(arr, rel):
    p = out_path(rel)
    os.makedirs(os.path.dirname(p), exist_ok=True)
    Image.fromarray(arr).save(p)
    return p

def trim(a, pad=2):
    ys, xs = np.nonzero(a[..., 3])
    if len(xs) == 0:
        return a
    y0, y1, x0, x1 = max(ys.min() - pad, 0), ys.max() + pad + 1, max(xs.min() - pad, 0), xs.max() + pad + 1
    return a[y0:y1, x0:x1]

def crop(a, box):
    x, y, w, h = box
    return a[y:y + h, x:x + w].copy()

def keep_largest(a, close=3, min_ratio=0.02):
    """잘라낸 칸 안에서 이웃 그림 조각(글자 등)을 버리고 큰 덩어리와 붙은 것만 남긴다."""
    m = (a[..., 3] > 0).astype(np.uint8)
    mm = cv2.dilate(m, np.ones((close, close), np.uint8)) if close else m
    n, lab, st, _ = cv2.connectedComponentsWithStats(mm, connectivity=8)
    if n <= 2:
        return a
    big = st[1:, 4].max()
    keep = np.zeros(n, bool)
    for i in range(1, n):
        if st[i, 4] >= big * min_ratio:
            keep[i] = True
    out = a.copy()
    out[~keep[lab]] = 0
    return out

def remove_labels(a):
    """시트에 같이 적힌 검은 글자(WALK·HIT·DIE)를 지운다: 키 50px 이하·거의 검정·색 없는 덩어리"""
    m = (a[..., 3] > 0).astype(np.uint8)
    n, lab, st, _ = cv2.connectedComponentsWithStats(cv2.dilate(m, np.ones((7, 3), np.uint8)), connectivity=8)
    out = a.copy()
    rgb = a[..., :3].astype(int)
    for i in range(1, n):
        x, y, w, h, ar = st[i]
        if h > 52:
            continue
        sel = (lab == i) & (a[..., 3] > 0)
        px = rgb[sel]
        if len(px) == 0:
            continue
        sat = px.max(-1) - px.min(-1)
        if px.max(-1).mean() < 90 and sat.mean() < 30:
            out[lab == i] = 0
    return out

def remove_white_holes(a, min_area=40):
    """팔과 몸 사이처럼 바깥과 막혀서 남은 흰 배경 조각 지우기 (좀비만 — 흰 몸 캐릭터가 없음)"""
    white = ((a[..., :3].min(-1) >= 238) & (a[..., 3] > 0)).astype(np.uint8)
    n, lab, st, _ = cv2.connectedComponentsWithStats(white, connectivity=4)
    out = a.copy()
    for i in range(1, n):
        if st[i, 4] >= min_area:
            m = lab == i
            m = cv2.dilate(m.astype(np.uint8), np.ones((3, 3), np.uint8)).astype(bool) & (a[..., :3].min(-1) >= 200)
            out[m] = 0
    return out

# ---------------- 캐릭터(스킨) 시트 ----------------
CELL = 360          # 정리된 칸 크기(px)
SRC_CELL = 300      # 원본을 줄인 기준(캐릭터 키 ≈ 230px)
def char_sheet(path, scale):
    a = load(path)
    a = cut_white(a)
    if scale != 1:
        h, w = a.shape[:2]
        a = np.array(Image.fromarray(a).resize((int(w * scale), int(h * scale)), Image.LANCZOS))
        a[..., 3] = np.where(a[..., 3] < 40, 0, a[..., 3])
    h, w = a.shape[:2]
    m = (a[..., 3] > 0).astype(np.uint8)
    n, lab, st, cen = cv2.connectedComponentsWithStats(cv2.dilate(m, np.ones((3, 3), np.uint8)), connectivity=8)
    areas = st[1:, 4]
    big_thr = np.median(np.sort(areas)[-15:]) * 0.4
    cw, ch = w / 4.0, h / 4.0
    # 몸통 덩어리 → (줄, 열). 두 칸에 걸친 덩어리는 칸 경계로 나눈다
    bodies = {}     # (r,c) -> mask
    smalls = []
    for i in range(1, n):
        x, y, bw, bh, ar = st[i]
        comp = lab == i
        if ar >= big_thr:
            ys, xs = np.nonzero(comp)
            rows = (ys / ch).astype(int).clip(0, 3)
            cols = (xs / cw).astype(int).clip(0, 3)
            # 두 칸에 크게 걸친 덩어리(이웃 프레임이 붙은 것)는 칸 경계로 나누고,
            # 조금만 넘어간 부분(총구 불꽃)은 많이 겹친 칸에 붙인다
            for r in np.unique(rows):
                sel = rows == r
                if sel.sum() < ar * 0.15:
                    continue
                cnt = np.bincount(cols[sel], minlength=4)
                main_cols = [c for c in range(4) if cnt[c] >= sel.sum() * 0.22]
                for c in range(4):
                    if cnt[c] == 0:
                        continue
                    tgt = c if c in main_cols else min(main_cols, key=lambda m: abs(m - c))
                    s2 = sel & (cols == c)
                    mk = np.zeros_like(comp)
                    mk[ys[s2], xs[s2]] = True
                    key = (int(r), int(tgt))
                    bodies[key] = bodies.get(key, np.zeros_like(comp)) | mk
        else:
            smalls.append(i)
    # 작은 조각(불꽃·먼지) → 가장 가까운 몸통
    keys = list(bodies.keys())
    bbs = {}
    for k in keys:
        ys, xs = np.nonzero(bodies[k]); bbs[k] = (xs.min(), ys.min(), xs.max(), ys.max())
    for i in smalls:
        cx, cy = cen[i]
        best, bd = None, 1e9
        for k, (x0, y0, x1, y1) in bbs.items():
            dx = max(x0 - cx, 0, cx - x1); dy = max(y0 - cy, 0, cy - y1)
            d = math.hypot(dx, dy)
            if d < bd:
                bd, best = d, k
        if best is not None and bd < cw * 0.35:
            bodies[best] |= (lab == i)
    sheet = np.zeros((CELL * 4, CELL * 4, 4), np.uint8)
    # 각 프레임: 칸의 발 기준선(칸 바닥) + 몸통 폭 가운데가 아니라 '칸 가운데' 기준으로 옮겨 정렬 유지
    # 줄마다 발 위치 = 그 줄 대기 프레임 기준. 열 가운데 = 원본 칸 가운데
    for (r, c), mk in bodies.items():
        ys, xs = np.nonzero(mk)
        sub = np.zeros((h, w, 4), np.uint8)
        sub[mk] = a[mk]
        # 발 기준 정렬: 서 있는 프레임(줄 0~2)은 발(맨 아래 12% 띠)의 가운데, 쓰러짐(줄 3)은 몸 가운데를
        # 칸 가운데(FOOT_X)에, 맨 아래를 칸 바닥 8px 위에 둔다 → 프레임이 바뀌어도 발이 제자리
        bottom = ys.max(); top = ys.min()
        if r < 3:
            band = ys >= bottom - (bottom - top) * 0.12
            ax = float(np.median(xs[band]))
        else:
            ax = (xs.min() + xs.max()) / 2.0
        dx = int(round(FOOT_X - ax)) + c * CELL
        dy = int(round(CELL - 8 - bottom)) + r * CELL
        ty, tx = ys + dy, xs + dx
        ok = (tx >= c * CELL) & (tx < (c + 1) * CELL) & (ty >= r * CELL) & (ty < (r + 1) * CELL)
        sheet[ty[ok], tx[ok]] = a[ys[ok], xs[ok]]
    return sheet, sorted(bodies.keys())

FOOT_X = 160   # 발 가운데 x (총구 쪽 오른편에 여유)

def fix_char_baseline(sheet):
    """원본마다 칸 안의 발 높이가 조금씩 달라서, 대기 0번 프레임의 발이 칸 바닥에서 8px 위에 오도록 시트 전체를 맞춘다.
    가로도 대기 0번 몸통 가운데가 칸 가운데에 오게."""
    f = sheet[0:CELL, 0:CELL, 3]
    ys, xs = np.nonzero(f)
    if len(ys) == 0:
        return sheet
    shift_y = (CELL - 8) - ys.max()
    shift_x = CELL // 2 - int((xs.min() + xs.max()) / 2)
    out = np.zeros_like(sheet)
    for r in range(4):
        for c in range(4):
            cell = sheet[r * CELL:(r + 1) * CELL, c * CELL:(c + 1) * CELL]
            m = np.float32([[1, 0, shift_x], [0, 1, shift_y]])
            moved = cv2.warpAffine(cell, m, (CELL, CELL), flags=cv2.INTER_NEAREST, borderValue=(0, 0, 0, 0))
            out[r * CELL:(r + 1) * CELL, c * CELL:(c + 1) * CELL] = moved
    return out

# ---------------- 색 톤 (맵과 어울리게) ----------------
# 비교 그림 C(캡처/카피바라_이질감_비교.png) 와 같은 값. 0 이면 끄기
CHAR_TONE = dict(mul=(0.86, 0.80, 0.74), sat=0.82, top=1.05, bottom=0.80)
ZOMBIE_TONE = dict(mul=(0.95, 0.95, 0.95), sat=0.95, top=1.0, bottom=1.0)

def apply_tone(cell, t, keep=None):
    """칸 하나에 톤: 채도 낮추고 곱하기 + 몸 위→아래로 밝기 그라데이션(위에서 빛이 오는 느낌). keep = 건드리지 않을 픽셀"""
    a = cell.astype(float)
    al = cell[..., 3] > 0
    if not al.any():
        return cell
    ys = np.nonzero(al)[0]
    top, bot = ys.min(), ys.max()
    rgb = a[..., :3]
    g = rgb.mean(-1, keepdims=True)
    rgb = g + (rgb - g) * t['sat']
    rgb = rgb * np.array(t['mul'])
    k = np.clip((np.arange(cell.shape[0]) - top) / max(1, bot - top), 0, 1)
    grad = (t['top'] + (t['bottom'] - t['top']) * k)[:, None, None]
    rgb = rgb * grad
    out = cell.copy()
    m = al if keep is None else (al & ~keep)
    out[..., :3][m] = np.clip(rgb, 0, 255).astype(np.uint8)[m]
    return out

def tone_char_sheet(sheet):
    for r in range(4):
        for c in range(4):
            cell = sheet[r * CELL:(r + 1) * CELL, c * CELL:(c + 1) * CELL]
            keep = None
            if r == 1:   # 사격 줄: 총구 불꽃(총 끝보다 오른쪽)은 밝게 그대로
                keep = np.zeros(cell.shape[:2], bool); keep[:, FOOT_X + 135:] = True
            sheet[r * CELL:(r + 1) * CELL, c * CELL:(c + 1) * CELL] = apply_tone(cell, CHAR_TONE, keep)
    return sheet

def do_chars():
    files = [('skin_00', src('Main_Character.png'), SRC_CELL / 256.0)]
    for p in sorted(glob.glob(os.path.join(ROOT, '스킨', 'Skin_*.png'))):
        n = int(os.path.splitext(os.path.basename(p))[0].split('_')[1])
        files.append(('skin_%02d' % n, p, SRC_CELL / 512.0))
    prev = []
    ref = None
    gun_lines = []
    for name, p, sc in files:
        sheet, keys = char_sheet(p, sc)
        # 마지막 쓰러짐 칸(줄3 열3)은 원본 구석의 생성 도구 표시(✦)가 겹쳐 있어 비우고 앞 칸을 복사
        sheet[3 * CELL:4 * CELL, 3 * CELL:4 * CELL] = sheet[3 * CELL:4 * CELL, 2 * CELL:3 * CELL]
        sheet = tone_char_sheet(sheet)
        # 총 떼어 내기(tools/gun_split.py): 몸 시트 + 스킨별 총 그림 + 칸별 총 자리
        if ref is None:
            ref = sheet.copy()
            save(gun_split.muzzle_flash(ref), 'Char/muzzle_flash.png')
        sheet, gun, pos, _ = gun_split.split(sheet, ref)
        save(gun, 'Char/gun_%s.png' % name.split('_')[1])
        gun_lines.append(name + ' ' + ' '.join('%d,%d' % p for p in pos))
        save(sheet, 'Char/%s.png' % name)
        cnt = [sum(1 for k in keys if k[0] == r) for r in range(4)]
        print(name, '프레임', cnt)
        prev.append(sheet)
    with open(os.path.join(OUT, 'Char', 'gun_pos.txt'), 'w', encoding='utf-8') as fp:
        fp.write(chr(10).join(gun_lines) + chr(10))
    # 미리보기: 각 스킨 대기0·사격1·쓰러짐3
    W = 150
    pv = Image.new('RGBA', (W * 6, W * ((len(prev) + 1) // 2)), (90, 90, 120, 255))
    for i, s in enumerate(prev):
        for j, (r, c) in enumerate([(0, 0), (1, 1), (3, 3)]):
            fr = Image.fromarray(s[r * CELL:(r + 1) * CELL, c * CELL:(c + 1) * CELL]).resize((W, W))
            pv.alpha_composite(fr, ((i % 2) * W * 3 + j * W, (i // 2) * W))
    pv.convert('RGB').save(os.path.join(PREV, 'sliced_chars.png'))

# ---------------- 좀비 ----------------
ZW, ZH = 400, 380
ZOMBIES = {
    # 이름: (시트, [걷기, 맞기, 죽기] 상자 x,y,w,h)
    'crawler':  ('Zombie_01.png', [(1270, 461, 256, 167), (1640, 455, 264, 173), (2013, 497, 281, 119)]),
    'armor':    ('Zombie_01.png', [(1258, 696, 279, 275), (1610, 692, 330, 280), (1995, 760, 335, 211)]),
    'knife':    ('Zombie_01.png', [(106, 1074, 263, 258), (478, 1074, 286, 258), (838, 1146, 256, 179)]),
    'ogre':     ('Zombie_01.png', [(1251, 1030, 317, 307), (1648, 1020, 316, 317), (1990, 1170, 388, 167)]),
    'basic':    ('Zombie_02.png', [(136, 66, 150, 238), (496, 53, 155, 251), (825, 186, 269, 112)]),
    'mummy':    ('Zombie_02.png', [(1306, 54, 172, 250), (1666, 48, 184, 256), (2002, 198, 298, 100)]),
    'hat':      ('Zombie_02.png', [(133, 378, 157, 255), (502, 378, 185, 255), (826, 498, 287, 142)]),
    'bucket':   ('Zombie_02.png', [(1269, 401, 256, 227), (1641, 390, 304, 238), (2012, 504, 300, 112)]),
    'bat':      ('Zombie_02.png', [(107, 708, 285, 269), (502, 714, 256, 263), (821, 816, 309, 166)]),
    'fat':      ('Zombie_02.png', [(1275, 696, 239, 275), (1665, 696, 215, 275), (2001, 792, 334, 179)]),
    'farmer':   ('Zombie_02.png', [(106, 1044, 297, 287), (484, 1044, 274, 288), (838, 1134, 322, 196)]),
    'reaper':   ('Zombie_02.png', [(1288, 1050, 310, 287), (1672, 1050, 328, 287), (2008, 1169, 350, 191)]),
}

ZCOLS = 7   # 걷기 0~3 · 맞기 · 죽기 · 맞기 흰 실루엣
CRAWL = ('crawler', 'bucket')     # 기어 다니는 좀비: 다리 대신 몸을 늘였다 줄였다
ROBE = ('reaper',)                # 다리가 안 보이는 옷: 옷자락만 흔들기

def feet_x(c):
    ys, xs = np.nonzero(c[..., 3])
    bottom, top = ys.max(), ys.min()
    band = ys >= bottom - (bottom - top) * 0.12
    return float(np.median(xs[band]))

def place_feet(out, c, col, standing=True):
    """칸 col 에 발 가운데 = 칸 가운데, 맨 아래 = 바닥 4px 위로 놓는다 (프레임이 바뀌어도 발이 제자리)"""
    ys, xs = np.nonzero(c[..., 3])
    ax = feet_x(c) if standing else (xs.min() + xs.max()) / 2.0
    dx = int(round(col * ZW + ZW / 2 - ax))
    dy = int(round(ZH - 4 - ys.max()))
    ty, tx = ys + dy, xs + dx
    ok = (tx >= col * ZW) & (tx < (col + 1) * ZW) & (ty >= 0) & (ty < ZH)
    out[ty[ok], tx[ok]] = c[ys[ok], xs[ok]]

def silhouette(c):
    w = c.copy()
    w[..., :3] = 255
    return w

def shift_rows(c, y0, y1, shift_fn, split=None, side=None):
    """y0~y1 줄의 픽셀을 가로로 민다. split 이 있으면 그 x 의 왼쪽(side=-1)/오른쪽(side=1)만"""
    out = np.zeros_like(c)
    h, w = c.shape[:2]
    for y in range(y0, y1):
        sft = shift_fn(y)
        row = c[y].copy()
        if split is not None:
            m = np.zeros(w, bool)
            if side < 0: m[:split] = True
            else: m[split:] = True
            row[~m] = 0
        if sft > 0: row[sft:] = row[:w - sft].copy(); row[:sft] = 0
        elif sft < 0: row[:w + sft] = row[-sft:].copy(); row[w + sft:] = 0
        out[y] = row
    return out

def make_walk(c, name):
    """걷기 그림 1장 → 4장. 서 있는 좀비: 엉덩이 아래를 발 사이에서 나눠 앞다리·뒷다리를 반대로 벌림
    (0 = 벌림, 1 = 모음, 2 = 반대로 벌림, 3 = 모음). 기어가는 좀비는 몸을 늘였다 줄임, 사신은 옷자락만 흔듦."""
    pad = 24
    c = np.pad(c, ((0, 0), (pad, pad), (0, 0)))
    h, w = c.shape[:2]
    ys, xs = np.nonzero(c[..., 3])
    top, bottom = ys.min(), ys.max()
    if name in CRAWL:
        frames = []
        for sx, sy in ((1.06, 0.96), (1.0, 1.0), (0.95, 1.04), (1.0, 1.0)):
            im = Image.fromarray(c).resize((int(w * sx), int(h * sy)), Image.NEAREST)
            frames.append(np.array(im))
        return frames
    hip_r = 0.70 if name in ('fat', 'ogre', 'armor') else 0.64
    hip = int(top + (bottom - top) * hip_r)
    leg_h = max(1, bottom - hip)
    amp = max(8, int(leg_h * 0.16))
    step = 4
    q = lambda v: int(round(v / step)) * step
    fx = int(round(feet_x(c)))
    frames = []
    for k in range(4):
        d = (1, 0, -1, 0)[k]
        upper = c.copy(); upper[hip:] = 0
        if name in ROBE:
            sway = shift_rows(c, hip, h, lambda y: q(d * amp * 0.6 * (y - hip) / leg_h))
            fr = upper.copy(); m = sway[..., 3] > 0; fr[m] = sway[m]
        else:
            ease = lambda y: ((y - hip) / leg_h) ** 1.4      # 허벅지는 조금, 발은 많이
            back = shift_rows(c, hip, h, lambda y: q(-d * amp * ease(y)), fx, -1)
            front = shift_rows(c, hip, h, lambda y: q(d * amp * ease(y)), fx, 1)
            fr = upper.copy()
            # 가랑이 틈 메우기: 다리 윗부분 40% 는 원래 그림을 밑에 깔아 둠
            under = c.copy(); under[:hip] = 0; under[int(hip + leg_h * 0.4):] = 0
            m = under[..., 3] > 0; fr[m] = under[m]
            for part in (back, front):
                m = part[..., 3] > 0; fr[m] = part[m]
        frames.append(fr)
    return frames

def do_zombies():
    cache = {}
    prev = []
    for name, (sheet, boxes) in ZOMBIES.items():
        if sheet not in cache:
            cache[sheet] = cut_white(load(src(sheet)))
            if sheet == 'Zombie_01.png':
                # 오우거 뿔에 붙은 'WALK' 글자 지우기 (검정·무채색 픽셀만)
                z = cache[sheet]; x, y, w, h = 1305, 978, 140, 54
                reg = z[y:y + h, x:x + w]; rgb = reg[..., :3].astype(int)
                dark = (rgb.max(-1) < 90) & ((rgb.max(-1) - rgb.min(-1)) < 30)
                reg[dark] = 0
        a = cache[sheet]
        crops = []
        for i, b in enumerate(boxes):
            x, y, w, h = b
            pad = 6
            c = crop(a, (x - pad, y - pad, w + pad * 2, h + pad * 2))
            c = remove_labels(c)
            c = remove_white_holes(c)
            c = keep_largest(c, close=9, min_ratio=0.01)
            c = trim(c, 0)
            ch_, cw_ = c.shape[:2]
            if cw_ > ZW - 40 or ch_ > ZH - 4:
                sc = min((ZW - 40) / cw_, (ZH - 4) / ch_)
                c = np.array(Image.fromarray(c).resize((int(cw_ * sc), int(ch_ * sc)), Image.NEAREST))
            crops.append(c)
        walk, hit, die = crops
        frames = [apply_tone(f, ZOMBIE_TONE) for f in make_walk(walk, name) + [hit, die]] + [silhouette(hit)]
        out = np.zeros((ZH, ZW * ZCOLS, 4), np.uint8)
        for i, c in enumerate(frames):
            place_feet(out, c, i, standing=(i != 5))
        save(out, 'Zombie/z_%s.png' % name)
        prev.append(out)
    W = ZW * ZCOLS // 3
    pv = Image.new('RGBA', (W, ZH // 3 * len(prev)), (90, 90, 120, 255))
    for i, s in enumerate(prev):
        im = Image.fromarray(s).resize((W, ZH // 3))
        pv.alpha_composite(im, (0, i * ZH // 3))
    pv.convert('RGB').save(os.path.join(PREV, 'sliced_zombies.png'))

# ---------------- UI ----------------
UI_BOXES = {
    'UI.png': {
        'btn_shop': (1201, 88, 360, 207), 'btn_weapon': (1616, 38, 325, 258), 'btn_mine': (2043, 88, 357, 207),
        'btn_sword': (1615, 374, 327, 258), 'btn_boss': (2043, 423, 357, 209), 'btn_challenge': (2006, 760, 394, 211),
        'btn_pistol': (1201, 423, 360, 209),
        'plank_a': (620, 41, 487, 254), 'plank_b': (619, 314, 488, 246), 'plank_thin': (617, 568, 491, 185),
        'plank_frame': (73, 779, 510, 268), 'plank_c': (617, 779, 521, 268), 'plank_d': (617, 1114, 523, 222),
        'card_1': (34, 1371, 431, 417), 'card_2': (481, 1405, 405, 376), 'card_3': (911, 1413, 497, 372),
    },
    'Zombie_01.png': {
        'panel_big': (33, 1368, 432, 419), 'panel_mid': (478, 1403, 379, 378), 'panel_small': (876, 1433, 309, 318),
        'panel_wide': (1223, 1412, 693, 351), 'btn_play': (1961, 1428, 388, 173), 'btn_ok': (1965, 1618, 384, 115),
    },
}

def do_ui():
    for sheet, boxes in UI_BOXES.items():
        a = cut_white(load(src(sheet)))
        for name, b in boxes.items():
            x, y, w, h = b
            c = crop(a, (x - 4, y - 4, w + 8, h + 8))
            c = keep_largest(c, close=5, min_ratio=0.003)
            save(trim(c, 1), 'UI/%s.png' % name)
    make_cards()

def make_cards():
    """강화 카드: card_3 의 글자(Lv·수치)를 같은 줄의 나무결로 덮어 글자 없는 바탕을 만들고,
    아이콘 칸 3종(공격력=card_1 폭발, 공속=card_2 칼, 돈=card_3 지폐)을 따로 저장."""
    c3 = np.array(Image.open(out_path('UI/card_3.png')).convert('RGBA'))
    h, w = c3.shape[:2]
    rgb = c3[..., :3].astype(int)
    # 글자 = 밝은 크림색(속) + 진한 갈색 외곽선. 나무판 위 영역(초록 막대 위, 아이콘 칸 오른쪽)에서만 찾는다
    bright = (rgb.min(-1) > 200) & (c3[..., 3] > 0)
    region = np.zeros((h, w), bool)
    # 나무판 위쪽 영역: 대략 y 15%~55%
    region[int(h * 0.12):int(h * 0.56), int(w * 0.05):int(w * 0.95)] = True
    txt = bright & region
    txt = cv2.dilate(txt.astype(np.uint8), np.ones((15, 15), np.uint8)).astype(bool) & region
    # 아이콘 칸 근처는 건드리지 않음
    txt[:, :int(w * 0.27)] &= np.zeros_like(txt[:, :int(w * 0.27)])
    out = c3.copy()
    ys, xs = np.nonzero(txt)
    # 같은 줄에서 글자 없는 나무 픽셀을 왼쪽/오른쪽으로 찾아 복사 (가로 나무결이라 자연스러움)
    for y in np.unique(ys):
        row_bad = txt[y]
        xs_bad = np.nonzero(row_bad)[0]
        src_x0 = int(w * 0.30)
        for x in xs_bad:
            # 같은 줄, 글자 덩어리 폭만큼 떨어진 곳
            for off in (-130, 130, -180, 180, -90, 90, -230, 230):
                sx = x + off
                if 0 <= sx < w and not txt[y, sx] and c3[y, sx, 3] > 0 and sx > w * 0.25:
                    out[y, x] = c3[y, sx]
                    break
    save(out, 'UI/card_base.png')
    do_icons()

def wood_mask(a):
    """나무판 색(주황 갈색 계열)·짙은 테두리·투명 = True"""
    r, g, b = [a[..., i].astype(int) for i in range(3)]
    wood = (r > g + 25) & (g > b) & (r - b > 55) & (r < 235)
    dark = a[..., :3].max(-1) < 70
    return wood | dark | (a[..., 3] == 0)

def icon_bbox(a, roi):
    """roi(비율 x0,y0,x1,y1) 안에서 나무가 아닌 색 픽셀의 상자 + 테두리 여유"""
    h, w = a.shape[:2]
    x0, y0, x1, y1 = int(roi[0] * w), int(roi[1] * h), int(roi[2] * w), int(roi[3] * h)
    sub = a[y0:y1, x0:x1]
    nm = ~wood_mask(sub)
    nm = cv2.morphologyEx(nm.astype(np.uint8), cv2.MORPH_OPEN, np.ones((3, 3), np.uint8)).astype(bool)
    ys, xs = np.nonzero(nm)
    pad = 9
    return (max(x0 + xs.min() - pad, 0), max(y0 + ys.min() - pad, 0), min(x0 + xs.max() + pad + 1, w), min(y0 + ys.max() + pad + 1, h))

def erase_icon(a, box):
    """아이콘 자리를 같은 줄의 나무 픽셀(테두리 아닌 것)로 덮어 빈 판을 만든다"""
    out = a.copy()
    x0, y0, x1, y1 = box
    r, g, b = [a[..., i].astype(int) for i in range(3)]
    woodish = (a[..., 3] > 0) & (a[..., :3].max(-1) >= 70) & (r > b + 30)
    for y in range(y0, y1):
        xs = [x for x in np.nonzero(woodish[y])[0] if not (x0 - 4 <= x < x1 + 4)]
        if not xs:
            continue
        for x in range(x0, x1):
            if a[y, x, 3] == 0:
                continue
            out[y, x] = a[y, xs[(x - x0) % len(xs)]]
    return out

def icon_only(a, box):
    """상자 가장자리에서 이어진 바탕(검정 외곽선이 아닌 픽셀)을 지워 아이콘만 남긴다"""
    x0, y0, x1, y1 = box
    c = a[y0:y1, x0:x1].copy()
    return trim(flood_bg(c), 1)

def flood_bg(c, dark=70):
    h, w = c.shape[:2]
    notdark = ((c[..., :3].max(-1) >= dark) | (c[..., 3] == 0)).astype(np.uint8)
    mask = np.zeros((h + 2, w + 2), np.uint8)
    img = notdark.copy()
    for x in range(w):
        for y in (0, h - 1):
            if img[y, x] == 1: cv2.floodFill(img, mask, (x, y), 2, flags=4)
    for y in range(h):
        for x in (0, w - 1):
            if img[y, x] == 1: cv2.floodFill(img, mask, (x, y), 2, flags=4)
    out = c.copy()
    out[img == 2] = 0
    return keep_largest(out, close=3, min_ratio=0.05)

def do_icons():
    L = lambda n: np.array(Image.open(out_path('UI/%s.png' % n)).convert('RGBA'))
    # 가장자리 버튼(왼쪽이 잘린 판 = 왼쪽 메뉴, 오른쪽이 잘린 판 = 오른쪽 메뉴)
    # 눈금으로 잰 아이콘 자리 (x0,y0,x1,y1 — 잘라 낸 버튼 그림 기준)
    for src, box, icon, blank in (
            ('btn_shop', (140, 22, 286, 176), 'icon_shop', 'edge_left'),
            ('btn_mine', (82, 30, 228, 166), 'icon_gem', 'edge_right'),
            ('btn_pistol', (132, 32, 290, 168), 'icon_pistol', None),
            ('btn_boss', (66, 24, 234, 174), 'icon_swords', None),
            ('btn_sword', (104, 70, 262, 228), 'icon_sword', None)):
        a = L(src)
        save(icon_only(a, box), 'UI/%s.png' % icon)
        if blank:
            save(erase_icon(a, box), 'UI/%s.png' % blank)
    # 강화 카드: 아이콘 상자(테두리 포함) 3종 → card_base 의 아이콘 자리(card_3 기준)에 맞춘 크기로
    boxes = {'card_1': (45, 42, 172, 168), 'card_2': (40, 25, 161, 150), 'card_3': (4, 27, 119, 143)}
    base = L('card_base')
    tw, th = boxes['card_3'][2] - boxes['card_3'][0], boxes['card_3'][3] - boxes['card_3'][1]
    for src, nm in (('card_1', 'atk'), ('card_2', 'spd'), ('card_3', 'gold')):
        x0, y0, x1, y1 = boxes[src]
        box = L(src)[y0:y1, x0:x1]
        box = np.array(Image.fromarray(box).resize((tw, th), Image.NEAREST))
        save(box, 'UI/statbox_%s.png' % nm)
        # 아이콘만(상자 안쪽)
        inner = box[int(th * 0.15):int(th * 0.85), int(tw * 0.15):int(tw * 0.85)]
        save(inner, 'UI/stat_%s.png' % nm)
        card = base.copy()
        bx0, by0 = boxes['card_3'][0], boxes['card_3'][1]
        reg = card[by0:by0 + th, bx0:bx0 + tw]
        m = box[..., 3] > 0
        reg[m] = box[m]
        save(card, 'UI/card_%s.png' % nm)
    # 지폐 아이콘(초록 막대 안의 것)
    c3 = L('card_3')
    # 카드 왼쪽 위 아이콘 칸(진한 갈색 바탕) 안의 지폐: 바탕색을 모서리에서부터 채워 지움
    box = L('statbox_gold')
    hh, ww = box.shape[:2]
    inner = box[int(hh * 0.12):int(hh * 0.9), int(ww * 0.1):int(ww * 0.9)].copy()
    ih, iw = inner.shape[:2]
    ring = np.concatenate([inner[6:12, 10:-10, :3].reshape(-1, 3), inner[-12:-6, 10:-10, :3].reshape(-1, 3)])
    bgc = np.median(ring, axis=0)
    dist = np.abs(inner[..., :3].astype(int) - bgc[None, None, :]).sum(-1)
    inner[dist < 60] = 0
    inner[:5] = 0; inner[-5:] = 0; inner[:, :5] = 0; inner[:, -5:] = 0
    save(trim(keep_largest(inner, close=3, min_ratio=0.1), 1), 'UI/icon_money.png')

# ---------------- 총알 ----------------
def rotate_keep(a, deg):
    im = Image.fromarray(a).rotate(deg, resample=Image.NEAREST, expand=True)
    return np.array(im)

def loadb(name):
    """총알 그림: 거의 투명한 잡티(알파 24 미만)를 지운다"""
    a = load(src(name))
    a[a[..., 3] < 24] = 0
    return a

def shrink(a, maxdim):
    h, w = a.shape[:2]
    s = maxdim / max(h, w)
    if s >= 1:
        return a
    return np.array(Image.fromarray(a).resize((max(1, int(w * s)), max(1, int(h * s))), Image.LANCZOS))

def do_bullets():
    b1 = loadb('Bullet_01.png')
    save(shrink(trim(b1, 2), 256), 'Bullet/bullet_basic.png')                         # 오른쪽(+x) 방향
    b2 = loadb('Bullet_02.png')
    save(shrink(trim(keep_largest(crop(b2, (1176, 663, 188, 189)), 0, 0.5), 1), 64), 'Bullet/pellet.png')
    save(shrink(trim(crop(b2, (440, 560, 600, 830)), 1), 200), 'Bullet/shotgun_icon.png')
    # 드릴: 과녁과 겹쳐 있어 다각형으로 드릴만 남기고, 끝이 +x 를 향하게 돌림
    b3 = loadb('Bullet_03.png')
    s = 2048 / 800.0
    poly = np.array([(170, 618), (232, 520), (320, 400), (372, 350), (410, 338), (448, 380), (470, 450),
                     (452, 490), (360, 545), (250, 600)], np.float32) * s
    mk = np.zeros(b3.shape[:2], np.uint8)
    cv2.fillPoly(mk, [poly.astype(np.int32)], 1)
    d = b3.copy(); d[mk == 0] = 0
    d = trim(d, 2)
    # 끝(왼쪽 아래) → 밑동(오른쪽 위) 방향 각도. 끝이 +x 로 가게 회전
    ang = math.degrees(math.atan2(-(618 - 420), (170 - 430)))   # 화면 좌표(y 아래) → 수학 각도
    d = rotate_keep(d, -ang)
    save(shrink(trim(d, 2), 256), 'Bullet/drill.png')
    # 레이저: 조준점(빨간 원)과 레이저 줄 색
    b4 = loadb('Bullet_04.png')
    a4 = b4[..., 3] > 0
    # 빨간 조준점: 빨강 성분
    red = (b4[..., 0] > 180) & (b4[..., 1] < 120) & (b4[..., 2] < 120) & a4
    ys, xs = np.nonzero(red)
    x0, x1, y0, y1 = xs.min(), xs.max(), ys.min(), ys.max()
    cr = b4[y0 - 4:y1 + 5, x0 - 4:x1 + 5].copy()
    cm = (cr[..., 0] > 150) & (cr[..., 1] < 140) & (cr[..., 2] < 140)
    cr[~cm] = 0
    save(shrink(trim(cr, 1), 160), 'Bullet/crosshair.png')
    blue = (b4[..., 2] > 150) & (b4[..., 0] < 150) & a4
    lz = b4.copy(); lz[~blue & ~((b4[..., :3].min(-1) > 200) & a4)] = 0
    lz[red] = 0
    save(shrink(trim(lz, 2), 200), 'Bullet/laser_icon.png')
    save(make_beam(), 'Bullet/laser_beam.png')
    # 미사일: 로켓 + 뒤 폭발 불꽃 전체. 코 방향(오른쪽 위 약 50도)을 +x 로
    b5 = loadb('Bullet_05.png')
    m5 = trim(b5, 2)
    m5 = rotate_keep(m5, -48)
    save(shrink(trim(m5, 2), 320), 'Bullet/missile.png')

def make_beam():
    """레이저 줄(가로로 늘려 쓰는 32x24 픽셀): 가운데 흰색 → 하늘색 → 파랑 → 투명 (Bullet_04 색)"""
    h = 24
    cols = [(0, 0, 0, 0), (30, 90, 255, 160), (30, 130, 255, 255), (20, 220, 255, 255), (150, 255, 255, 255), (255, 255, 255, 255)]
    prof = [0, 1, 2, 2, 3, 3, 4, 4, 5, 5, 5, 5]
    col = prof + prof[::-1]
    a = np.zeros((h, 32, 4), np.uint8)
    for y in range(h):
        a[y, :] = cols[col[y]]
    return a

def do_map():
    m = load(src('Main_Map.png'))
    save(m, 'Map/main_map.png')

if __name__ == '__main__':
    what = sys.argv[1:] or ['chars', 'zombies', 'ui', 'bullets', 'map']
    if 'chars' in what: do_chars()
    if 'zombies' in what: do_zombies()
    if 'ui' in what: do_ui()
    if 'bullets' in what: do_bullets()
    if 'map' in what: do_map()
    print('done ->', OUT)
