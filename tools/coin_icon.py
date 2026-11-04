# -*- coding: utf-8 -*-
"""11-04 요청_02 "달러 아이콘 → 금전": 초록 지폐 그림을 금화로 바꾼다 (11-04 밤: 더미 → 둥근 동전 한 닢).
 ※ 다시 돌린 뒤엔 tools/_build/card_green_orig/ 를 지우고 python tools/card_green.py (카드 녹색 채도)
 - UI/icon_money.png      : 금화 더미 아이콘을 새로 그림 (작게 그려 3배, 1px 진한 외곽선 — icon_gen.py 와 같은 화풍)
 - UI/card_atk·spd·gold   : 아래 금액 칸의 지폐 · card_gold 왼쪽 위 칸의 지폐
 - UI/stat_gold.png       : 골드 버프 버튼의 지폐
   → 지폐(초록 덩어리 + 둘레 외곽선)를 지우고(cv2.inpaint) 그 자리에 금화 아이콘을 올림
원본은 처음 한 번 tools/_build/money_orig/ 에 복사해 두고, 다시 돌려도 늘 원본에서 만든다.
사용: python tools/coin_icon.py      미리보기: tools/_preview/coin.png
"""
import os, shutil
import numpy as np, cv2
from PIL import Image, ImageDraw

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
UI = os.path.join(ROOT, 'Kachi_Gun', 'Assets', 'Resources', 'Art', 'UI')
ORIG = os.path.join(ROOT, 'tools', '_build', 'money_orig')
PREV = os.path.join(ROOT, 'tools', '_preview')
FILES = ['icon_money.png', 'card_atk.png', 'card_spd.png', 'card_gold.png', 'stat_gold.png']

OUT = (40, 24, 16, 255)
G_DARK = (176, 104, 22, 255)
G_MID = (232, 162, 36, 255)
G_BASE = (252, 200, 58, 255)
G_LIGHT = (255, 232, 128, 255)
G_SHINE = (255, 252, 214, 255)


def outline(im):
    a = np.array(im)
    al = a[..., 3] > 0
    grow = al.copy()
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        grow |= np.roll(np.roll(al, dx, 1), dy, 0)
    a[grow & ~al] = OUT
    return Image.fromarray(a)


def coin_face(d, cx, cy, r):
    """정면 금화: 바깥 테 · 안쪽 원 · 가운데 네모 구멍(엽전 느낌) · 반짝임"""
    d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=G_MID)
    d.ellipse((cx - r + 1, cy - r + 1, cx + r - 1, cy + r - 1), fill=G_BASE)
    d.ellipse((cx - r + 3, cy - r + 3, cx + r - 3, cy + r - 3), outline=G_DARK)
    d.rectangle((cx - 1, cy - 1, cx + 1, cy + 1), fill=G_DARK)
    d.point([(cx - r + 3, cy - r + 5), (cx - r + 4, cy - r + 4), (cx - r + 5, cy - r + 3)], fill=G_SHINE)
    d.point([(cx - r + 4, cy - r + 5), (cx - r + 5, cy - r + 4)], fill=G_LIGHT)


def stack(d, x0, ybot, w, n):
    """옆에서 본 금화 n 닢 더미 (아래에서 위로) + 맨 위 금화의 윗면(타원)"""
    for i in range(n):
        y = ybot - i * 2
        d.rectangle((x0, y - 1, x0 + w, y), fill=G_BASE)
        d.line((x0, y, x0 + w, y), fill=G_MID)
        d.point([(x0, y - 1), (x0 + w, y - 1)], fill=G_MID)
        d.point([(x0 + 2, y - 1)], fill=G_LIGHT)
    top = ybot - n * 2
    d.ellipse((x0, top - 2, x0 + w, top + 2), fill=G_LIGHT, outline=G_MID)
    d.point([(x0 + w // 2, top)], fill=G_BASE)


def coin_pile():
    """금화 더미: 뒤 오른쪽 높은 더미 · 왼쪽 낮은 더미 + 앞 정면 금화 → 그림 크기에 딱 맞게 잘라 3배"""
    n = 30
    im = Image.new('RGBA', (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    stack(d, 14, 24, 10, 8)                  # 오른쪽 높은 더미
    stack(d, 2, 24, 9, 4)                    # 왼쪽 낮은 더미
    coin_face(d, 11, 18, 7)                  # 앞 금화
    im = outline(im)
    im = im.crop(im.getbbox())
    return im.resize((im.width * 3, im.height * 3), Image.NEAREST)


def coin_round():
    """11-04 밤 사용자 "금전 아이콘은 동전 형태로": 둥근 금화 한 닢 (살짝 기울여 아래쪽 두께가 보임) → 3배"""
    n = 26
    im = Image.new('RGBA', (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    cx, cy, rx, ry = 12, 11, 11, 10
    d.ellipse((cx - rx, cy - ry + 3, cx + rx, cy + ry + 3), fill=G_DARK)          # 두께(옆면)
    d.ellipse((cx - rx, cy - ry + 1, cx + rx, cy + ry + 1), fill=G_MID)
    d.ellipse((cx - rx, cy - ry, cx + rx, cy + ry), fill=G_MID)                   # 앞면 테
    d.ellipse((cx - rx + 2, cy - ry + 2, cx + rx - 2, cy + ry - 2), fill=G_BASE)   # 앞면
    d.ellipse((cx - rx + 4, cy - ry + 4, cx + rx - 4, cy + ry - 4), outline=G_DARK)   # 안쪽 고리 (새김)
    d.ellipse((cx - rx + 5, cy - ry + 5, cx + rx - 5, cy + ry - 5), outline=G_LIGHT)
    # 가운데 새김: 세로 막대 (도트 동전 흔한 모양)
    d.rectangle((cx - 1, cy - 4, cx + 1, cy + 4), fill=G_DARK)
    d.rectangle((cx - 1, cy - 4, cx, cy + 3), fill=G_LIGHT)
    # 왼쪽 위 반짝임
    d.line((cx - rx + 3, cy - 2, cx - 3, cy - ry + 3), fill=G_SHINE)
    d.point([(cx - rx + 4, cy - 1), (cx - 2, cy - ry + 4)], fill=G_LIGHT)
    im = outline(im)
    im = im.crop(im.getbbox())
    return im.resize((im.width * 3, im.height * 3), Image.NEAREST)


def bill_mask(a, x, y, w, h):
    """bbox 안 초록 덩어리 + 둘레 3px 안의 어두운 외곽선 = 지울 곳"""
    r, g, b, al = (a[..., i].astype(int) for i in range(4))
    green = (al > 200) & (g > r + 25) & (g > b + 25)
    m = np.zeros(green.shape, np.uint8)
    m[y:y + h, x:x + w] = green[y:y + h, x:x + w]
    near = cv2.dilate(m, np.ones((9, 9), np.uint8)) > 0
    dark = (r + g + b) < 330
    return ((m > 0) | (near & dark)).astype(np.uint8)


def replace(name, boxes, coin, fit):
    src = os.path.join(ORIG, name)
    a = np.array(Image.open(src).convert('RGBA'))
    mask = np.zeros(a.shape[:2], np.uint8)
    for bx in boxes: mask |= bill_mask(a, *bx)
    mask = cv2.dilate(mask, np.ones((5, 5), np.uint8))
    # 지운 곳 = 가장 가까운 남은 칸의 색 (도트 그림이라 흐리게 번지지 않게)
    _, lab = cv2.distanceTransformWithLabels(mask, cv2.DIST_L2, 5, labelType=cv2.DIST_LABEL_PIXEL)   # 0(남은 칸)마다 번호 → 지운 칸엔 가장 가까운 남은 칸 번호
    ys, xs = np.nonzero(mask == 0)
    src_by_label = np.zeros((lab.max() + 1, 4), np.uint8)
    src_by_label[lab[ys, xs]] = a[ys, xs]
    out = a.copy()
    my, mx = np.nonzero(mask)
    out[my, mx] = src_by_label[lab[my, mx]]
    im = Image.fromarray(out)
    for (x, y, w, h) in boxes:
        s = fit * min(w / coin.width, h / coin.height)
        c = coin.resize((max(1, int(coin.width * s)), max(1, int(coin.height * s))), Image.NEAREST)
        im.alpha_composite(c, (x + (w - c.width) // 2, y + (h - c.height) // 2))
    im.save(os.path.join(UI, name))
    return im


def main():
    os.makedirs(ORIG, exist_ok=True); os.makedirs(PREV, exist_ok=True)
    for f in FILES:
        if not os.path.exists(os.path.join(ORIG, f)):
            shutil.copy(os.path.join(UI, f), os.path.join(ORIG, f))
    coin = coin_round()   # 11-04 밤: 더미(coin_pile) → 둥근 동전 한 닢
    # 아이콘: 원래 크기(79x74) 그대로 가운데에
    icon = Image.new('RGBA', (79, 74), (0, 0, 0, 0))
    icon.alpha_composite(coin, ((79 - coin.width) // 2, (74 - coin.height) // 2))
    icon.save(os.path.join(UI, 'icon_money.png'))
    price = (97, 230, 69, 65)                 # 강화 카드 아래 금액 칸의 지폐 (세 카드 같은 자리)
    ims = [icon]
    for f in ('card_atk.png', 'card_spd.png'): ims.append(replace(f, [price], coin, 1.05))
    ims.append(replace('card_gold.png', [price, (28, 55, 67, 64)], coin, 1.05))
    ims.append(replace('stat_gold.png', [(7, 11, 67, 64)], coin, 1.0))
    # 미리보기 (원본 | 새것)
    W = sum(i.width for i in ims) + 20 * len(ims)
    pv = Image.new('RGBA', (W, 2 * 380), (60, 60, 70, 255)); x = 0
    for f, im in zip(FILES, ims):
        pv.alpha_composite(Image.open(os.path.join(ORIG, f)).convert('RGBA'), (x, 0)); pv.alpha_composite(im, (x, 380)); x += im.width + 20
    pv.save(os.path.join(PREV, 'coin.png'))
    print('금화 아이콘 + 카드 3 · 버프 버튼 바꿈 → 미리보기', os.path.join(PREV, 'coin.png'))


if __name__ == '__main__':
    main()
