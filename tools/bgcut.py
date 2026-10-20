# -*- coding: utf-8 -*-
"""흰 배경 시트에서 배경만 투명하게 만드는 공통 함수 + 덩어리(그림) 위치 분석.
사용: python tools/bgcut.py <시트.png> [최소면적]  → 덩어리 목록 출력, tools/_preview/<이름>_boxes.png 저장
"""
import sys, os
import numpy as np
import cv2

def cut_white(rgba, tol=26):
    """가장자리에서 이어진 흰색(밝은 회색 포함)만 지운다. 그림 안쪽 흰색(눈·닭 털)은 외곽선 덕에 남는다."""
    a = rgba.copy()
    h, w = a.shape[:2]
    rgb = np.ascontiguousarray(a[..., :3])
    # 밝은 칸 = 배경 후보
    light = (rgb.min(-1) >= 255 - tol).astype(np.uint8)
    mask = np.zeros((h + 2, w + 2), np.uint8)
    fillimg = light.copy()
    # 가장자리의 배경 후보에서 채우기
    seeds = [(x, 0) for x in range(0, w, 7)] + [(x, h - 1) for x in range(0, w, 7)] + \
            [(0, y) for y in range(0, h, 7)] + [(w - 1, y) for y in range(0, h, 7)]
    for (x, y) in seeds:
        if fillimg[y, x] == 1:
            cv2.floodFill(fillimg, mask, (x, y), 2, flags=4)
    bg = fillimg == 2
    # 경계의 옅은 테두리(흰색과 섞인 픽셀) 한 겹 더 지우기
    soft = rgb.min(-1) >= 200
    k = np.ones((3, 3), np.uint8)
    for _ in range(2):
        grow = cv2.dilate(bg.astype(np.uint8), k).astype(bool)
        bg = bg | (grow & soft)
    a[..., 3] = np.where(bg, 0, a[..., 3])
    a[bg, :3] = 0
    return a

def components(alpha, min_area=400, close=0):
    m = (alpha > 0).astype(np.uint8)
    if close:
        m = cv2.dilate(m, np.ones((close, close), np.uint8))
    n, lab, st, _ = cv2.connectedComponentsWithStats(m, connectivity=8)
    out = []
    for i in range(1, n):
        x, y, w, h, area = st[i]
        if area >= min_area:
            out.append((int(x), int(y), int(w), int(h), int(area)))
    out.sort(key=lambda b: (b[1] // 120, b[0]))
    return out

def load(path):
    from PIL import Image
    return np.array(Image.open(path).convert('RGBA'))

if __name__ == '__main__':
    from PIL import Image, ImageDraw
    p = sys.argv[1]
    mn = int(sys.argv[2]) if len(sys.argv) > 2 else 400
    close = int(sys.argv[3]) if len(sys.argv) > 3 else 0
    a = load(p)
    if a[..., 3].min() > 0:
        a = cut_white(a)
    bx = components(a[..., 3], mn, close)
    for b in bx:
        print(b)
    im = Image.fromarray(a)
    bg = Image.new('RGBA', im.size, (90, 90, 120, 255)); bg.alpha_composite(im)
    d = ImageDraw.Draw(bg)
    for i, (x, y, w, h, _) in enumerate(bx):
        d.rectangle([x, y, x + w, y + h], outline=(255, 0, 0, 255), width=3)
        d.text((x + 4, y + 4), str(i), fill=(255, 255, 0, 255))
    os.makedirs(os.path.join(os.path.dirname(__file__), '_preview'), exist_ok=True)
    bg.thumbnail((1000, 1000))
    out = os.path.join(os.path.dirname(__file__), '_preview', os.path.splitext(os.path.basename(p))[0] + '_boxes.png')
    bg.convert('RGB').save(out)
    print('saved', out, len(bx))
