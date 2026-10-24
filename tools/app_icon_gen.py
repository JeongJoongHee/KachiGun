# -*- coding: utf-8 -*-
"""앱 아이콘 만들기 (10-24, 서바이벌 app_icon_gen.py 방식) — 사용: PYTHONUTF8=1 python tools/app_icon_gen.py [미리보기 폴더]
게임 속 그림(카피바라 Art/Char/skin_00 총 든 칸 + 좀비 Art/Zombie/z_basic)을 도트 바탕 위에 얹어
  Kachi_Gun/Assets/Art/AppIcon/app_icon.png        512 정사각형 (플레이 스토어 · 옛 안드로이드 아이콘)
  Kachi_Gun/Assets/Art/AppIcon/app_icon_round.png  512 동그라미 (옛 안드로이드 둥근 아이콘)
  Kachi_Gun/Assets/Art/AppIcon/app_icon_bg.png     432 적응형 아이콘 바탕
  Kachi_Gun/Assets/Art/AppIcon/app_icon_fg.png     432 적응형 아이콘 앞 그림(투명)
Unity 쪽 연결은 Editor/AppIconSetup.cs (빌드 때 자동)."""
import os, sys, random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(ROOT, 'Kachi_Gun', 'Assets', 'Resources', 'Art')
DST = os.path.join(ROOT, 'Kachi_Gun', 'Assets', 'Art', 'AppIcon')

STORE, ADAPT, PX = 512, 432, 8
CELL = 360                      # 캐릭터 시트 칸
CAPY_CELL = 8                   # 총 든 자세 (3번째 줄 첫 칸)
ZW, ZH = 400, 380               # 좀비 시트 칸

BG_IN = (246, 170, 70)          # 가운데 노을빛
BG_MID = (170, 80, 50)
BG_OUT = (48, 30, 40)           # 가장자리 짙은 갈색·자주
GLOW = (255, 236, 170)


def lerp(a, b, t):
    t = max(0.0, min(1.0, t))
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def background(size, seed=3):
    """도트 바탕: 가운데 노을빛 → 가장자리 어둡게(단계 + 체크 디더), 아래쪽 풀숲 실루엣"""
    n = size // PX
    img = Image.new('RGB', (n, n))
    pix = img.load()
    cx, cy, steps = n * 0.5, n * 0.45, 7
    for y in range(n):
        for x in range(n):
            d = ((x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2) ** 0.5 / (n * 0.72) + ((x + y) % 2) * 0.035
            t = min(1.0, int(d * steps) / (steps - 1))
            pix[x, y] = lerp(BG_IN, BG_MID, t / 0.5) if t < 0.5 else lerp(BG_MID, BG_OUT, (t - 0.5) / 0.5)
    rnd = random.Random(seed)
    d = ImageDraw.Draw(img)
    for x in range(n):
        h = int(n * 0.07 + abs(np.sin(x * 0.7 + 0.4)) * n * 0.05 + rnd.random() * 2)
        d.line([(x, n - h), (x, n - 1)], fill=(36, 52, 34))
        if h > 3: d.line([(x, n - h + 3), (x, n - 1)], fill=(26, 36, 26))
    return img.resize((size, size), Image.NEAREST)


def trim(im):
    return im.crop(im.getbbox())


def capybara():
    s = Image.open(os.path.join(ART, 'Char', 'skin_00.png')).convert('RGBA')
    c = CAPY_CELL
    return trim(s.crop(((c % 4) * CELL, (c // 4) * CELL, (c % 4 + 1) * CELL, (c // 4 + 1) * CELL)))


def zombie():
    z = Image.open(os.path.join(ART, 'Zombie', 'z_basic.png')).convert('RGBA')
    return trim(z.crop((0, 0, ZW, ZH))).transpose(Image.FLIP_LEFT_RIGHT)   # 왼쪽(카피바라)을 봄


def outline_glow(im, size, xy, radius, strength, color):
    a = Image.new('L', (size, size), 0)
    a.paste(im.getchannel('A'), xy)
    a = a.filter(ImageFilter.MaxFilter(7)).filter(ImageFilter.GaussianBlur(radius)).point(lambda v: int(min(255, v * strength)))
    g = Image.new('RGBA', (size, size), color + (0,))
    g.putalpha(a)
    return g


def scaled(im, h):
    return im.resize((int(im.width * h / im.height), h), Image.NEAREST)   # 도트 그대로 (뭉개지지 않게)


def compose(size, scale, with_bg=True):
    """scale = 캐릭터가 차지할 높이 비율 (적응형 앞 그림은 작게 — 원으로 잘려도 안전하게)"""
    base = background(size).convert('RGBA') if with_bg else Image.new('RGBA', (size, size), (0, 0, 0, 0))
    cap, zom = capybara(), zombie()
    zh = int(size * scale * 0.78)
    z = scaled(zom, zh)
    zl = Image.new('RGBA', z.size, (0, 0, 0, 0))
    zl.alpha_composite(z)
    zx, zy = int(size * 0.5 + size * scale * 0.08), int(size * 0.5 - zh * 0.62)
    base.alpha_composite(outline_glow(z, size, (zx, zy), size / 70, 0.8, (120, 255, 120)))
    base.alpha_composite(zl, (zx, zy))
    ch = int(size * scale)
    c = scaled(cap, ch)
    cx, cy = int(size * 0.5 - c.width * 0.66), int(size * 0.5 - ch * 0.46)
    base.alpha_composite(outline_glow(c, size, (cx, cy), size / 50, 1.2, GLOW))
    base.alpha_composite(c, (cx, cy))
    return base


def circle_mask(size, rate=0.5):
    m = Image.new('L', (size * 4, size * 4), 0)
    r = size * 4 * rate
    ImageDraw.Draw(m).ellipse([size * 2 - r, size * 2 - r, size * 2 + r, size * 2 + r], fill=255)
    return m.resize((size, size), Image.LANCZOS)


def rounded_mask(size, rate=0.22):
    m = Image.new('L', (size * 4, size * 4), 0)
    ImageDraw.Draw(m).rounded_rectangle([0, 0, size * 4 - 1, size * 4 - 1], radius=int(size * 4 * rate), fill=255)
    return m.resize((size, size), Image.LANCZOS)


def main():
    os.makedirs(DST, exist_ok=True)
    full = compose(STORE, 0.70)
    full.convert('RGB').save(os.path.join(DST, 'app_icon.png'))
    rnd = full.copy(); rnd.putalpha(circle_mask(STORE))
    rnd.save(os.path.join(DST, 'app_icon_round.png'))
    background(ADAPT).save(os.path.join(DST, 'app_icon_bg.png'))
    fg = compose(ADAPT, 0.46, with_bg=False)      # 108dp 중 원 66dp 안
    fg.save(os.path.join(DST, 'app_icon_fg.png'))
    if len(sys.argv) > 1:
        out = sys.argv[1]; os.makedirs(out, exist_ok=True)
        S = 300
        sheet = Image.new('RGB', (S * 4 + 50 * 5, S + 260), (236, 236, 240))
        a = full.resize((S, S), Image.LANCZOS)
        r1 = a.copy(); r1.putalpha(rounded_mask(S))
        adapt = background(ADAPT).convert('RGBA'); adapt.alpha_composite(fg)
        vis = adapt.crop((ADAPT // 6, ADAPT // 6, ADAPT * 5 // 6, ADAPT * 5 // 6)).resize((S, S), Image.LANCZOS)
        c1 = vis.copy(); c1.putalpha(circle_mask(S))
        sq = vis.copy(); sq.putalpha(rounded_mask(S, 0.3))
        for i, im in enumerate([a, r1, c1, sq]):
            sheet.paste(im, (50 + i * (S + 50), 30), im if im.mode == 'RGBA' else None)
        x = 50
        for s in (144, 96, 72, 48):
            sm = c1.resize((s, s), Image.LANCZOS)
            sheet.paste(sm, (x, S + 60 + (144 - s)), sm)
            x += s + 30
        p = os.path.join(out, 'app_icon_preview.png')
        sheet.save(p); print('미리보기', p)
    print('완료', DST)


if __name__ == '__main__':
    main()
