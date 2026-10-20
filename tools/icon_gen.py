# -*- coding: utf-8 -*-
"""원본 그림에 없는 작은 도트 아이콘·효과를 직접 그린다 (작게 그려서 8배로 키움, 1px 진한 외곽선).
결과(설정 톱니·미션·자물쇠·화살표·보급품 상자·자동강화·폭발 등): Kachi_Gun/Assets/Resources/Art/Gen/*.png   미리보기: tools/_preview/gen.png
"""
import os, math
import numpy as np
from PIL import Image, ImageDraw

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
OUT = os.path.join(ROOT, 'Kachi_Gun', 'Assets', 'Resources', 'Art', 'Gen')
os.makedirs(OUT, exist_ok=True)
OUTLINE = (40, 24, 16, 255)
N = 24
SCALE = 8

def canvas(n=N):
    im = Image.new('RGBA', (n, n), (0, 0, 0, 0))
    return im, ImageDraw.Draw(im)

def outline(im, col=OUTLINE):
    a = np.array(im)
    al = a[..., 3] > 0
    grow = al.copy()
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        grow |= np.roll(np.roll(al, dx, 1), dy, 0)
    edge = grow & ~al
    a[edge] = col
    return Image.fromarray(a)

# 게임에서 더 이상 안 쓰는 아이콘(10-19 UI 팩으로 교체) — 만들지 않음
RETIRED = {'gear', 'mission', 'lock', 'bolt', 'moon', 'crate', 'auto', 'round_line', 'paw'}

def finish(im, name, outl=True, scale=SCALE):
    if outl:
        im = outline(im)
    im = im.resize((im.width * scale, im.height * scale), Image.NEAREST)
    if name not in RETIRED:
        im.save(os.path.join(OUT, name + '.png'))
    return im

def gear():
    im, d = canvas()
    c = 11.5
    for k in range(8):
        ang = k * math.pi / 4
        x, y = c + math.cos(ang) * 8.5, c + math.sin(ang) * 8.5
        d.rectangle([x - 2, y - 2, x + 2, y + 2], fill=(170, 175, 185, 255))
    d.ellipse([c - 7.5, c - 7.5, c + 7.5, c + 7.5], fill=(170, 175, 185, 255))
    d.ellipse([c - 6, c - 6, c + 3, c + 3], fill=(205, 210, 220, 255))
    d.ellipse([c - 3, c - 3, c + 3, c + 3], fill=(0, 0, 0, 0))
    return finish(im, 'gear')

def scroll():
    im, d = canvas()
    d.rectangle([5, 3, 18, 20], fill=(250, 236, 200, 255))
    d.rectangle([4, 2, 19, 4], fill=(205, 160, 105, 255))
    d.rectangle([4, 19, 19, 21], fill=(205, 160, 105, 255))
    for y in (7, 11, 15):
        d.rectangle([7, y, 8, y + 1], fill=(80, 170, 80, 255))
        d.line([10, y + 1, 16, y + 1], fill=(150, 120, 90, 255))
    return finish(im, 'mission')

def lock():
    im, d = canvas()
    d.arc([6, 2, 17, 14], 180, 360, fill=(170, 175, 185, 255), width=3)
    d.rectangle([6, 8, 7, 11], fill=(170, 175, 185, 255)); d.rectangle([16, 8, 17, 11], fill=(170, 175, 185, 255))
    d.rectangle([4, 11, 19, 21], fill=(240, 190, 60, 255))
    d.rectangle([4, 11, 19, 12], fill=(255, 225, 120, 255))
    d.rectangle([11, 14, 12, 18], fill=(110, 70, 20, 255))
    return finish(im, 'lock')

def paw():
    im, d = canvas()
    pink = (255, 170, 190, 255); white = (255, 245, 240, 255)
    d.ellipse([5, 9, 19, 21], fill=white)
    for (x, y) in ((4, 4), (9, 1), (15, 2), (19, 7)):
        d.ellipse([x - 2, y, x + 3, y + 5], fill=white)
    d.ellipse([8, 12, 16, 19], fill=pink)
    for (x, y) in ((4, 5), (9, 2), (15, 3), (19, 8)):
        d.ellipse([x - 1, y + 1, x + 2, y + 4], fill=pink)
    return finish(im, 'paw')

def bolt():
    im, d = canvas()
    d.polygon([(13, 1), (5, 13), (11, 13), (8, 22), (19, 9), (13, 9), (16, 1)], fill=(255, 220, 60, 255))
    d.polygon([(13, 2), (8, 12), (11, 12)], fill=(255, 245, 170, 255))
    return finish(im, 'bolt')

def moon():
    im, d = canvas()
    d.ellipse([3, 3, 20, 20], fill=(255, 225, 120, 255))
    d.ellipse([8, 1, 23, 16], fill=(0, 0, 0, 0))
    for (x, y) in ((17, 16), (20, 11)):
        d.point((x, y), fill=(255, 255, 255, 255))
    return finish(im, 'moon')

def crate():
    im, d = canvas()
    d.rectangle([3, 6, 20, 20], fill=(160, 110, 60, 255))
    d.rectangle([3, 6, 20, 8], fill=(200, 150, 90, 255))
    d.line([3, 20, 20, 6], fill=(110, 70, 35, 255), width=2)
    d.rectangle([9, 11, 14, 15], fill=(220, 60, 60, 255))
    d.rectangle([11, 10, 12, 16], fill=(255, 255, 255, 255)); d.rectangle([10, 12, 13, 13], fill=(255, 255, 255, 255))
    return finish(im, 'crate')

def arrow():
    """아래를 가리키는 노란 화살표 (강화 안내) — 날렵하게: 가는 몸통 + 뾰족한 머리, 얇은 외곽선.
    4배 크게 그려 줄여서 가장자리가 매끈함(도트 아님). 192x192"""
    W, K = 192, 4
    n = W * K
    im = Image.new('RGBA', (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    cx = n / 2
    sw, top, neck = 13 * K, 14 * K, 104 * K          # 몸통 반폭 · 위 · 머리 시작
    hw, tip = 44 * K, 182 * K                         # 머리 반폭 · 끝
    shape = [(cx - sw, top), (cx + sw, top), (cx + sw, neck), (cx + hw, neck), (cx, tip), (cx - hw, neck), (cx - sw, neck)]
    o = 5 * K                                        # 외곽선 두께
    d.polygon(shape, fill=(46, 26, 12, 255))
    inner = [(cx - sw + o, top + o), (cx + sw - o, top + o), (cx + sw - o, neck + o * 0.6), (cx + hw - o * 2.2, neck + o * 0.6),
             (cx, tip - o * 2.0), (cx - hw + o * 2.2, neck + o * 0.6), (cx - sw + o, neck + o * 0.6)]
    d.polygon(inner, fill=(255, 205, 50, 255))
    # 왼쪽 밝은 면(빛)
    hl = [(cx - sw + o, top + o), (cx - 2 * K, top + o), (cx - 2 * K, neck + o * 0.6), (cx - sw + o, neck + o * 0.6)]
    d.polygon(hl, fill=(255, 238, 140, 255))
    d.polygon([(cx - hw + o * 2.2, neck + o * 0.6), (cx - 2 * K, neck + o * 0.6), (cx - 2 * K, tip - o * 3.2)], fill=(255, 232, 120, 255))
    im = im.resize((W, W), Image.LANCZOS)
    im.save(os.path.join(OUT, 'arrow.png'))
    return im

def auto():
    im, d = canvas()
    d.ellipse([2, 2, 21, 21], fill=(90, 160, 240, 255))
    d.ellipse([4, 4, 19, 19], fill=(120, 190, 255, 255))
    d.polygon([(8, 7), (17, 12), (8, 17)], fill=(255, 255, 255, 255))
    return finish(im, 'auto')

def boom(frame):
    """폭발 3장 (32px → 8배)"""
    n = 32
    im, d = canvas(n)
    c = n / 2
    rng = np.random.RandomState(7 + frame)
    r = [8, 13, 15][frame]
    cols = [(255, 250, 200, 255), (255, 215, 70, 255), (250, 140, 40, 255), (200, 60, 30, 255)]
    if frame == 2:
        cols = [(120, 110, 100, 200), (160, 150, 140, 200), (90, 80, 70, 180), (60, 55, 50, 160)]
    for k in range(4, 0, -1):
        rr = r * k / 4.0
        for _ in range(10):
            ang = rng.rand() * math.pi * 2
            ox, oy = math.cos(ang) * rr * 0.35, math.sin(ang) * rr * 0.35
            q = rr * (0.55 + rng.rand() * 0.25)
            d.ellipse([c + ox - q, c + oy - q, c + ox + q, c + oy + q], fill=cols[k - 1])
    if frame < 2:
        for _ in range(8):
            ang = rng.rand() * math.pi * 2
            dd = r + 1 + rng.rand() * 3
            x, y = c + math.cos(ang) * dd, c + math.sin(ang) * dd
            d.rectangle([x, y, x + 1, y + 1], fill=(255, 220, 90, 255))
    return finish(im, 'boom_%d' % frame, outl=False)

def spark():
    n = 16
    im, d = canvas(n)
    c = 7.5
    for ang in range(0, 360, 45):
        L = 7 if ang % 90 == 0 else 4
        x, y = c + math.cos(math.radians(ang)) * L, c + math.sin(math.radians(ang)) * L
        d.line([c, c, x, y], fill=(255, 230, 120, 255), width=2)
    d.ellipse([c - 2, c - 2, c + 2, c + 2], fill=(255, 255, 240, 255))
    return finish(im, 'spark', outl=False)

def white():
    Image.new('RGBA', (8, 8), (255, 255, 255, 255)).save(os.path.join(OUT, 'white.png'))

def round_box():
    """둥근 사각 판(9-slice 용, 흰색 → 코드에서 색 입힘)"""
    n = 48
    im = Image.new('RGBA', (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle([0, 0, n - 1, n - 1], radius=14, fill=(255, 255, 255, 255))
    im.save(os.path.join(OUT, 'round.png'))
    im = Image.new('RGBA', (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle([0, 0, n - 1, n - 1], radius=14, outline=(255, 255, 255, 255), width=6)
    pass   # round_line: 안 씀 (RETIRED)

def shadow():
    """발밑 그림자: 가장자리가 흐린 진한 갈색 타원 (128x40, 코드에서 크기 조절)"""
    from PIL import ImageFilter
    im = Image.new('RGBA', (128, 40), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.ellipse([10, 6, 118, 34], fill=(12, 6, 2, 215))
    im = im.filter(ImageFilter.GaussianBlur(4))
    im.save(os.path.join(OUT, 'shadow.png'))

def skin_icon():
    """스킨 메뉴 아이콘 = 기본 카피바라 얼굴"""
    p = os.path.join(ROOT, 'Kachi_Gun', 'Assets', 'Resources', 'Art', 'Char', 'skin_00.png')
    a = Image.open(p).convert('RGBA').crop((0, 0, 360, 360))
    bb = a.getbbox()
    head = a.crop((bb[0], bb[1], bb[2], bb[1] + int((bb[3] - bb[1]) * 0.5)))
    hb = head.getbbox(); head = head.crop(hb)
    head.save(os.path.join(OUT, 'capy_head.png'))

def ore_rock():
    """광산 회색 바위 = 푸른 보석 그림을 회색으로 (보상 없는 방해물)"""
    p = os.path.join(ROOT, 'Kachi_Gun', 'Assets', 'Resources', 'Art', 'UI', 'icon_gem.png')
    a = np.array(Image.open(p).convert('RGBA')).astype(float)
    g = a[..., :3].mean(-1, keepdims=True)
    g = (g - 128) * 0.75 + 118
    a[..., :3] = np.clip(np.concatenate([g * 0.98, g * 0.98, g * 1.02], -1), 0, 255)
    Image.fromarray(a.astype(np.uint8)).save(os.path.join(OUT, 'ore_rock.png'))

def gem_gold():
    """광산 거대 황금 크리스탈 = 푸른 보석 그림의 밝기를 금빛 단계로 (Mine_Ore_Spec 의 Huge_Crystal)"""
    p = os.path.join(ROOT, 'Kachi_Gun', 'Assets', 'Resources', 'Art', 'UI', 'icon_gem.png')
    a = np.array(Image.open(p).convert('RGBA')).astype(float)
    l = (a[..., 0] * 0.3 + a[..., 1] * 0.5 + a[..., 2] * 0.2) / 255.0
    l = np.clip((l - 0.15) / 0.8, 0, 1)[..., None]
    dark, mid, light = np.array([110, 58, 8.]), np.array([238, 172, 34.]), np.array([255, 246, 176.])
    col = np.where(l < 0.55, dark + (mid - dark) * (l / 0.55), mid + (light - mid) * ((l - 0.55) / 0.45))
    a[..., :3] = np.clip(col, 0, 255)
    Image.fromarray(a.astype(np.uint8)).save(os.path.join(OUT, 'gem_gold.png'))

if __name__ == '__main__':
    import sys
    if len(sys.argv) > 1:          # 이름을 주면 그것만: python tools/icon_gen.py gem_gold
        for n in sys.argv[1:]: globals()[n]()
        raise SystemExit
    out = [gear(), scroll(), lock(), arrow(), bolt(), moon(), crate(), auto(), spark()] + [boom(i) for i in range(3)]
    white(); round_box(); skin_icon(); ore_rock(); gem_gold(); shadow()
    W = 200
    pv = Image.new('RGBA', (W * 7, W * 2), (90, 90, 120, 255))
    for i, im in enumerate(out):
        im = im.copy(); im.thumbnail((W - 8, W - 8))
        pv.alpha_composite(im, ((i % 7) * W + 4, (i // 7) * W + 4))
    pv.convert('RGB').save(os.path.join(os.path.dirname(__file__), '_preview', 'gen.png'))
    print('done', OUT)
