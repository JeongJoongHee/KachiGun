# -*- coding: utf-8 -*-
"""보급품 그림 시트(리소스/Supply.png, 5x5 칸·이름 글자 포함) → 칸마다 그림만 잘라 투명 배경 png 로.
결과: Kachi_Gun/Assets/Resources/Art/Supply/supply_01.png ~ supply_25.png (왼쪽 위부터 오른쪽으로 = Supply_Id 순서)
Supply.xlsx 의 Icon 칸에 "supply_01" 처럼 그림 이름을 넣으면 게임이 그 그림을 씀.
사용: python tools/supply_icons.py [--preview 미리보기.png]"""
import os, sys
import numpy as np
from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')
SRC = os.path.join(ROOT, '리소스', 'Supply.png')
OUT = os.path.join(ROOT, 'Kachi_Gun', 'Assets', 'Resources', 'Art', 'Supply')
COLS, ROWS = 5, 5
SIZE = 256              # 저장 크기(긴 변)
MARGIN = 4              # 찾은 칸 안쪽에서 조금 더 버림 (남은 테두리 조각은 drop_edge_bits 가 지움)
BAND = 44               # 가장자리 띠 두께
BG = np.array([56, 58, 57])   # 칸 안쪽 배경색 (시트에서 잰 값)


def grow(seed, cand):
    """seed 에서 cand 안으로 이어진 곳 전부 (scipy 없이 반복 팽창)"""
    reach = seed & cand
    while True:
        g = reach.copy()
        g[1:] |= reach[:-1]; g[:-1] |= reach[1:]; g[:, 1:] |= reach[:, :-1]; g[:, :-1] |= reach[:, 1:]
        g &= cand
        if (g == reach).all(): return reach
        reach = g


def edge_mask(h, w):
    e = np.ones((h, w), bool)
    e[BAND:h - BAND, BAND:w - BAND] = False
    return e


def flood_bg(rgb, tol=13):
    """가장자리 띠 안의 배경색 픽셀에서 출발해 이어진 '배경색 비슷한' 픽셀 = 배경.
    칸 테두리(밝은 회색)는 배경색이 아니라 출발점이 안 되므로 테두리 안쪽 배경에서 퍼짐"""
    h, w, _ = rgb.shape
    cand = np.abs(rgb - BG).max(-1) < tol
    return grow(edge_mask(h, w), cand)


def drop_edge_bits(part):
    """보이는 픽셀 덩어리 중 가장자리 띠 안에만 있는 것(칸 테두리 조각·워터마크 찌꺼기)을 지움. 그림 본체는 가운데까지 이어져 남음"""
    fg = part[..., 3] > 0
    h, w = fg.shape
    keep = grow(fg & ~edge_mask(h, w), fg)
    part[..., 3] = np.where(keep, 255, 0)


def erase_watermark(part):
    """마지막 칸 오른쪽 아래의 AI 워터마크 별(채도 없는 밝은 회색)을 배경색으로 덮음 → 배경 지우기 때 같이 지워짐
    (성배는 금색이라 채도가 있어 안 건드림)"""
    h, w = part.shape[:2]
    reg = part[int(h * 0.4):int(h * 0.86), int(w * 0.64):, :3]
    gray = (reg.max(-1) - reg.min(-1) < 30) & (reg.mean(-1) > BG.mean() + 6)
    reg[gray] = BG


def cut_text(part):
    """칸 아래쪽 이름 글자 줄이 남아 있으면 잘라냄 (보통은 drop_edge_bits 가 이미 지움).
    아래에서 위로 맨 아래 덩어리를 찾아, 그 덩어리가 아래쪽 28% 안에 있을 때만 글자로 봄"""
    h = part.shape[0]
    rows = (part[:, BAND:-BAND, 3] > 0).any(1)
    y = h - 1
    while y > 0 and not rows[y]: y -= 1          # 맨 아래 덩어리의 아래
    while y > 0 and rows[y]: y -= 1              # 덩어리 위
    if y < h * 0.72: return part                 # 덩어리가 위까지 이어짐 = 그림 → 자를 것 없음
    part[y:, :, 3] = 0
    return part


def cell_spans(rgb, axis):
    """칸 사이 테두리 줄(배경이 아닌 픽셀이 85% 넘는 줄) 사이 = 칸 안쪽 범위들. 칸 높이가 일정하지 않아 직접 찾음"""
    f = (np.abs(rgb - BG).max(-1) > 14).mean(axis)
    spans, s = [], None
    for i, v in enumerate(f):
        if v <= 0.85 and s is None: s = i
        if v > 0.85 and s is not None:
            if i - s > 150: spans.append((s, i))
            s = None
    if s is not None and len(f) - s > 150: spans.append((s, len(f)))
    return spans


def main():
    a = Image.open(SRC).convert('RGBA')
    W, H = a.size
    arr = np.array(a).astype(int)
    ys, xs = cell_spans(arr[..., :3], 1), cell_spans(arr[..., :3], 0)
    assert len(ys) == ROWS and len(xs) == COLS, ('칸을 못 찾음', ys, xs)
    os.makedirs(OUT, exist_ok=True)
    outs = []
    for r, (y0, y1) in enumerate(ys):
        for c, (x0, x1) in enumerate(xs):
            part = arr[y0 + MARGIN:y1 - MARGIN, x0 + MARGIN:x1 - MARGIN].copy()
            if r == ROWS - 1 and c == COLS - 1:
                erase_watermark(part)
            part[..., 3] = np.where(flood_bg(part[..., :3]), 0, 255)
            drop_edge_bits(part)                     # 칸 테두리 조각
            part = cut_text(part)                    # 이름 글자
            drop_edge_bits(part)                     # 글자와 이어져 남았던 테두리 조각
            im = Image.fromarray(part.astype(np.uint8), 'RGBA')
            im = im.crop(im.getbbox())
            im.thumbnail((SIZE, SIZE), Image.LANCZOS)
            n = r * COLS + c + 1
            im.save(os.path.join(OUT, 'supply_%02d.png' % n))
            outs.append(im)
    print('저장', len(outs), '장 →', os.path.normpath(OUT))
    if '--preview' in sys.argv:
        pv = sys.argv[sys.argv.index('--preview') + 1]
        P = Image.new('RGBA', (COLS * 270, ROWS * 270), (200, 60, 200, 255))
        for i, im in enumerate(outs):
            P.alpha_composite(im, ((i % COLS) * 270 + 7, (i // COLS) * 270 + 7))
        P.save(pv)


if __name__ == '__main__':
    main()
