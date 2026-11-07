# -*- coding: utf-8 -*-
"""모바일 광고 이미지 (11-07) — 실제 게임 화면(광고 촬영 장면)으로 3가지 컨셉 x 4가지 크기

  PYTHONUTF8=1 python tools/ad_images.py      (먼저 PlayTest.RunAd 로 촬영 — tools/ad_video.py 참고)
  결과 = 광고 소재/이미지/  컨셉_크기.png
    크기: 1200x628 (가로 1.91:1) · 1200x1200 (정사각) · 1200x1500 (세로 4:5) · 1080x1920 (세로 9:16)
    — 구글 앱 캠페인 · 메타(페이스북·인스타그램) · 카카오 등에서 흔히 쓰는 비율
"""
import os
from PIL import Image, ImageDraw, ImageFilter
import ad_video as V   # 글꼴 · 캐릭터 · 버튼 · 둥근 모서리 함께 씀

ROOT = V.ROOT
OUT = os.path.join(ROOT, '광고 소재', '이미지')

# 컨셉: (이름, 장면, 장면 안 초, 게임 화면에서 보일 곳(x0,y0,x1,y1 — 1080x1920 기준), 큰 문구, 작은 문구)
CONCEPTS = [
    ('01_좀비떼', 'a_horde', 0.25, (0, 330, 1080, 1380), '좀비 떼를 쓸어버려라!', '켜 두면 알아서 싸우는 카피바라 분대'),
    ('02_합성', 'c_merge', 1.62, (40, 470, 1040, 1180), '같은 탄알 2개를 겹치면?', 'D부터 SSU까지 13등급 합성'),
    ('03_보스', 'd_boss', 1.2, (0, 330, 1080, 1380), '챕터마다 거대 보스!', '150챕터, 750스테이지 도전'),
]
SIZES = [(1200, 628), (1200, 1200), (1200, 1500), (1080, 1920)]
font, text_outline, GOLD, CREAM, INK, GREEN = V.font, V.text_outline, V.GOLD, V.CREAM, V.INK, V.GREEN


def frame(clip, t):
    return V.frame_at(dict(clip=clip, start=0.0, dur=99), t)


def crop_to(im, box, ratio):
    """box 안에서 가로:세로 = ratio 가 되게 가운데를 잘라 냄"""
    x0, y0, x1, y1 = box
    w, h = x1 - x0, y1 - y0
    if w / h > ratio:
        nw = int(h * ratio); x0 += (w - nw) // 2; w = nw
    else:
        nh = int(w / ratio); y0 += (h - nh) // 2; h = nh
    return im.crop((x0, y0, x0 + w, y0 + h))


def warm_bg(W, H):
    bg = Image.new('RGB', (W, H))
    d = ImageDraw.Draw(bg)
    for y in range(H):
        k = y / H
        d.line([(0, y), (W, y)], fill=(int(255 - 28 * k), int(230 - 66 * k), int(186 - 104 * k)))
    return bg.convert('RGBA')


def card(im, w, h, r):
    """게임 화면 판: 그림자 + 둥근 모서리 + 갈색 테두리"""
    g = V.rounded(im.resize((w, h), Image.LANCZOS), r)
    out = Image.new('RGBA', (w + 40, h + 46), (0, 0, 0, 0))
    sh = Image.new('RGBA', out.size, (0, 0, 0, 0))
    ImageDraw.Draw(sh).rounded_rectangle([20, 26, w + 20, h + 26], r, fill=(60, 30, 10, 150))
    out.alpha_composite(sh.filter(ImageFilter.GaussianBlur(10)))
    ImageDraw.Draw(out).rounded_rectangle([14, 14, w + 26, h + 26], r + 6, fill=INK)
    out.alpha_composite(g, (20, 20))
    return out


def brand(canvas, x, y, s, anchor_left=True):
    icon = V.rounded(Image.open(V.ICON).convert('RGBA').resize((int(110 * s), int(110 * s)), Image.LANCZOS), int(24 * s))
    canvas.alpha_composite(icon, (x, y))
    d = ImageDraw.Draw(canvas)
    text_outline(d, (x + icon.width + int(18 * s), y + icon.height // 2), '카피바라 vs 좀비', font(int(50 * s)), GOLD, w=5, anchor='lm')


def fit_font(text, size, maxw):
    while font(size).getlength(text) > maxw and size > 24: size -= 2
    return size


def make(concept, W, H):
    name, clip, t, box, l1, l2 = concept
    src = frame(clip, t)
    canvas = warm_bg(W, H)
    d = ImageDraw.Draw(canvas)
    s = min(W, H) / 1080
    if W > H * 1.5:          # 가로 1.91:1 — 왼쪽 글자 · 오른쪽 게임 화면
        gw, gh = int(H * 0.86 * 1.15), int(H * 0.86)
        g = card(crop_to(src, box, gw / gh), gw, gh, 22)
        canvas.alpha_composite(g, (W - g.width - 18, (H - g.height) // 2 + 4))
        lw = W - g.width - 40
        f1 = fit_font(l1, 66, lw - 50)
        text_outline(d, (lw // 2 + 14, int(H * 0.30)), l1, font(f1), GOLD, w=7)
        text_outline(d, (lw // 2 + 14, int(H * 0.45)), l2, font(fit_font(l2, 34, lw - 50)), CREAM, w=4)
        b = V.cta_button(int(360), int(90), '무료 플레이', 44)
        canvas.alpha_composite(b, (lw // 2 + 14 - b.width // 2, int(H * 0.56)))
        brand(canvas, 34, H - 120, 0.85)
    elif H > W * 1.6:        # 9:16 — 게임 화면 가득 · 위 문구 띠 · 아래 브랜드 + 버튼
        canvas = src.resize((W, H)).convert('RGBA')
        canvas.alpha_composite(V.top_shade(W, 470), (0, 0))
        bot = V.top_shade(W, 520).transpose(Image.FLIP_TOP_BOTTOM)
        canvas.alpha_composite(bot, (0, H - 520))
        d = ImageDraw.Draw(canvas)
        text_outline(d, (W // 2, 150), l1, font(fit_font(l1, 96, W - 80)), GOLD, w=8)
        text_outline(d, (W // 2, 268), l2, font(fit_font(l2, 58, W - 80)), CREAM, w=6)
        b = V.cta_button(560, 130, '지금 무료 플레이', 62)
        canvas.alpha_composite(b, ((W - b.width) // 2, H - 330))
        brand(canvas, (W - 520) // 2, H - 170, 1.0)
    else:                    # 정사각 · 4:5 — 위 문구 · 가운데 게임 화면 · 아래 브랜드 + 버튼
        top = int(250 * s * (1.0 if H == W else 1.1))
        bottom = int(190 * s)
        gh = H - top - bottom - 30
        gw = W - int(90 * s)
        g = card(crop_to(src, box, gw / gh), gw, gh, int(26 * s))
        canvas.alpha_composite(g, ((W - g.width) // 2, top))
        text_outline(d, (W // 2, int(top * 0.40)), l1, font(fit_font(l1, int(88 * s), W - 80)), GOLD, w=8)
        text_outline(d, (W // 2, int(top * 0.78)), l2, font(fit_font(l2, int(50 * s), W - 80)), INK, outline=CREAM, w=4)
        brand(canvas, int(46 * s), H - bottom + int(30 * s), s * 1.05)
        b = V.cta_button(int(400 * s), int(108 * s), '무료 플레이', int(50 * s))
        canvas.alpha_composite(b, (W - b.width - int(36 * s), H - bottom + int(22 * s)))
    return canvas.convert('RGB')


def main():
    os.makedirs(OUT, exist_ok=True)
    n = 0
    for c in CONCEPTS:
        for (W, H) in SIZES:
            p = os.path.join(OUT, f'{c[0]}_{W}x{H}.png')
            make(c, W, H).save(p, optimize=True)
            n += 1
    print('광고 이미지', n, '장 →', OUT)


if __name__ == '__main__':
    main()
