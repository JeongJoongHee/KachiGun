# -*- coding: utf-8 -*-
"""플레이 스토어 기본 스토어 등록정보용 그림 (10-24, 서바이벌 store_graphics.py 방식)

  PYTHONUTF8=1 python tools/store_graphics.py

  결과 = 스토어 등록/
    앱아이콘_512.png           — Kachi_Gun/Assets/Art/AppIcon/app_icon.png 복사 (tools/app_icon_gen.py)
    그래픽이미지_1024x500.png  — 전투 화면을 가로 띠로 + 카피바라·좀비 + 게임 글꼴 제목
    스크린샷_01~08.png         — Logs/PlayTest/store 캡처(1080x2340)를 1080x1920 에 문구와 함께 (구글: 긴 변 ≤ 짧은 변 x2)
  캡처 다시 찍기 = bash tools/unity_batch.sh KachiGun.EditorTools.PlayTest.RunStore --play (Unity 닫고)
"""
import os, shutil
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GAME = os.path.join(ROOT, 'Kachi_Gun')
SHOT_DIR = os.path.join(GAME, 'Logs', 'PlayTest', 'store')
ART = os.path.join(GAME, 'Assets', 'Resources', 'Art')
FONT = os.path.join(GAME, 'Assets', 'Resources', 'Font', 'MonaS12TextKR.ttf')
OUT = os.path.join(ROOT, '스토어 등록')

# (캡처 이름, 문구 1줄, 문구 2줄)
SHOTS = [
    ('01_battle', '켜 두면 알아서 싸운다!', '카피바라 분대 방치형 슈팅'),
    ('02_boss', '챕터마다 기다리는 보스', '150챕터 · 750스테이지'),
    ('03_weapon', '같은 등급 두 개를 겹치면', '한 단계 위로 합성!'),
    ('04_high', 'D부터 SSU까지 13등급', '레이저·미사일의 화끈한 화력'),
    ('05_supply', '25가지 보급품 수집', '모을수록 강해지는 보유 효과'),
    ('06_skin', '18가지 카피바라 스킨', '해적 선장부터 상어 잠옷까지'),
    ('07_mine', '광산에서 다이아를 캐자', '광석을 부수면 보석이 쏟아진다'),
    ('08_challenge', '도전 던전', '제한 시간 안에 좀비를 쓸어라!'),
]
CREAM = (255, 244, 214)
GOLD = (255, 205, 80)
INK = (40, 26, 18)


def font(size):
    return ImageFont.truetype(FONT, size)


def text_outline(d, xy, s, f, fill, outline=INK, w=6, anchor='mm'):
    x, y = xy
    for dx in range(-w, w + 1, 2):
        for dy in range(-w, w + 1, 2):
            if dx * dx + dy * dy <= w * w: d.text((x + dx, y + dy), s, font=f, fill=outline, anchor=anchor)
    d.text((x, y), s, font=f, fill=fill, anchor=anchor)


def screenshot(name, l1, l2):
    """1080x1920: 위 문구 띠 + 아래 게임 화면(가운데), 양옆은 같은 화면을 흐리게"""
    src = Image.open(os.path.join(SHOT_DIR, name + '.png')).convert('RGB')
    W, H, TOP = 1080, 1920, 300
    bg = src.resize((W, int(src.height * W / src.width))).crop((0, 0, W, H)).filter(ImageFilter.GaussianBlur(28))
    bg = Image.blend(bg, Image.new('RGB', bg.size, (30, 20, 16)), 0.55)
    gh = H - TOP - 30
    g = src.resize((int(src.width * gh / src.height), gh), Image.LANCZOS)
    canvas = bg.copy()
    gx = (W - g.width) // 2
    sh = Image.new('RGBA', (g.width + 40, g.height + 40), (0, 0, 0, 0))
    ImageDraw.Draw(sh).rounded_rectangle([20, 20, g.width + 20, g.height + 20], 26, fill=(0, 0, 0, 170))
    canvas.paste(sh.filter(ImageFilter.GaussianBlur(12)), (gx - 20, TOP - 6), sh.filter(ImageFilter.GaussianBlur(12)))
    m = Image.new('L', g.size, 0)
    ImageDraw.Draw(m).rounded_rectangle([0, 0, g.width - 1, g.height - 1], 24, fill=255)
    canvas.paste(g, (gx, TOP), m)
    d = ImageDraw.Draw(canvas)
    text_outline(d, (W // 2, 112), l1, font(84), GOLD, w=7)
    text_outline(d, (W // 2, 214), l2, font(56), CREAM, w=5)
    return canvas


def capy_and_zombie(h):
    s = Image.open(os.path.join(ART, 'Char', 'skin_17.png')).convert('RGBA')   # 해적 선장
    c = 8
    cap = s.crop(((c % 4) * 360, (c // 4) * 360, (c % 4 + 1) * 360, (c // 4 + 1) * 360))
    cap = cap.crop(cap.getbbox())
    zs = []
    for zn in ('z_basic', 'z_fat', 'z_hat'):
        z = Image.open(os.path.join(ART, 'Zombie', zn + '.png')).convert('RGBA').crop((0, 0, 400, 380))
        zs.append(z.crop(z.getbbox()).transpose(Image.FLIP_LEFT_RIGHT))
    sc = lambda im, hh: im.resize((int(im.width * hh / im.height), hh), Image.NEAREST)
    return sc(cap, h), [sc(z, int(h * 0.82)) for z in zs]


def feature_graphic():
    W, H = 1024, 500
    src = Image.open(os.path.join(SHOT_DIR, '01_battle.png')).convert('RGB')
    band = src.resize((W, int(src.height * W / src.width)), Image.LANCZOS)
    y0 = int(band.height * 0.36)
    canvas = band.crop((0, y0, W, y0 + H)).filter(ImageFilter.GaussianBlur(3))
    canvas = Image.blend(canvas, Image.new('RGB', canvas.size, (24, 16, 12)), 0.45).convert('RGBA')
    # 왼쪽(제목 자리) 더 어둡게
    shade = Image.new('L', (W, H), 0)
    for x in range(W):
        a = int(max(0, min(1, 1 - (x - 80) / 520)) * 150)
        ImageDraw.Draw(shade).line([(x, 0), (x, H)], fill=a)
    canvas.alpha_composite(Image.merge('RGBA', (*Image.new('RGB', (W, H), (0, 0, 0)).split(), shade)))
    cap, zs = capy_and_zombie(330)
    for i, z in enumerate(zs):
        canvas.alpha_composite(z, (720 + i * 95 - (40 if i == 1 else 0), H - z.height - 30 - (25 if i == 1 else 0)))
    canvas.alpha_composite(cap, (540, H - cap.height - 20))
    d = ImageDraw.Draw(canvas)
    text_outline(d, (270, 185), '카피바라', font(96), GOLD, w=8)
    text_outline(d, (270, 290), 'vs 좀비', font(96), (150, 230, 120), w=8)
    text_outline(d, (270, 390), '방치형 분대 슈팅', font(40), CREAM, w=5)
    return canvas.convert('RGB')


def main():
    os.makedirs(OUT, exist_ok=True)
    shutil.copy(os.path.join(GAME, 'Assets', 'Art', 'AppIcon', 'app_icon.png'), os.path.join(OUT, '앱아이콘_512.png'))
    feature_graphic().save(os.path.join(OUT, '그래픽이미지_1024x500.png'))
    n = 0
    for i, (name, l1, l2) in enumerate(SHOTS, 1):
        p = os.path.join(SHOT_DIR, name + '.png')
        if not os.path.exists(p):
            print('캡처 없음 — 건너뜀:', name); continue
        screenshot(name, l1, l2).save(os.path.join(OUT, '스크린샷_%02d.png' % i)); n += 1
    print('완료 — 아이콘 · 그래픽 이미지 · 스크린샷', n, '장 →', OUT)


if __name__ == '__main__':
    main()
