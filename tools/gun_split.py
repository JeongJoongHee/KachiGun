# -*- coding: utf-8 -*-
"""캐릭터 시트에서 총을 떼어 낸다 (slice_assets.py do_chars 가 부른다).

- 몸 시트: 대기 0~3 · 사격 4~7 칸에서 총(+ 앞발이 쥔 손잡이)을 지우고 구멍을 주변 색으로 메운 뒤 테두리를 그린다.
  사격 칸의 총구 불꽃도 지운다(불꽃은 총에 붙여 따로 그림).
- 총 그림: 사용자 그림 `총 이미지.png`(개머리 산탄총)를 NEW_GUN_SCALE 배로 키우고, 스킨마다 사격 4번 칸의 앞발을 잘라
  총 손잡이(펌프) 위에 얹어 Char/gun_XX.png (앞발 색이 스킨마다 달라서 스킨별, 앞발도 총과 같이 돈다).
  GUN_W x GUN_H 캔버스에 회전 중심(방아쇠 뒤 손목)이 GUN_PIVOT 에 오게 저장.
- 칸마다 총이 놓인 자리(회전 중심, 칸 px)는 Char/gun_pos.txt — 줄마다 "skin_XX x0,y0 x1,y1 ... x7,y7".
- 총구 불꽃: Char/muzzle_flash.png (기본 스킨 5번 칸에서 잘라 냄, 왼쪽 가운데가 총구).
"""
import numpy as np
import cv2

C = 360
# 기본 스킨 사격 4번 칸 기준 총 외곽(칸 px). 개머리 뒤 → 총열 위 → 총구 → 손잡이(펌프)·앞발 → 아래
GUN_POLY = np.array([(131, 233), (149, 217), (159, 213), (240, 212), (274, 211), (277, 204), (293, 202),
                     (299, 211), (304, 212), (305, 243), (286, 247), (276, 249), (274, 263), (259, 272),
                     (231, 270), (221, 259), (221, 243), (200, 241), (186, 247), (160, 249), (139, 247)], np.int32)
# 위 외곽 중 회색 금속 부분만(위치 찾기용 본보기) — 손잡이·앞발 제외
METAL_BOX = (135, 202, 305, 247)
PIVOT = (162, 230)          # 회전 중심(기본 스킨 4번 칸 px) — 뒤 손이 쥔 자리
MUZZLE = (304, 227)         # 총구 끝
# 새 총(총 이미지.png) — 그림 안 좌표(px)
NEW_GUN = '총 이미지.png'
NEW_GUN_SCALE = 1.6
NEW_GRIP = (70, 88)         # 손목(회전 중심)
NEW_MUZZLE = (156, 77)      # 총구 끝
NEW_PIVOT_CELL = (162, 242) # 몸 칸에서 회전 중심이 올 자리(기본 스킨 4번 칸 기준) — 총열이 예전 총 높이에 오게
PAW_BOX = (226, 249, 274, 274)   # 기본 스킨 4번 칸에서 앞발 범위 (x0, y0, x1, y1)
GUN_W, GUN_H = 260, 130
GUN_PIVOT = (90, 55)        # 총 캔버스 안 회전 중심
SEARCH = 26


def _cell(sheet, i):
    r, c = i // 4, i % 4
    return sheet[r * C:(r + 1) * C, c * C:(c + 1) * C]


def _template(ref):
    pm = np.zeros((C, C), np.uint8)
    cv2.fillPoly(pm, [GUN_POLY], 1)
    rgb = ref[..., :3].astype(int)
    sat = rgb.max(-1) - rgb.min(-1)
    x0, y0, x1, y1 = METAL_BOX
    box = np.zeros((C, C), bool); box[y0:y1, x0:x1] = True
    tm = (pm > 0) & box & (sat < 30) & (ref[..., 3] > 0)
    ys, xs = np.nonzero(tm)
    y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
    T = ref[y0:y1, x0:x1, :3].astype(np.float32)
    M = np.repeat(tm[y0:y1, x0:x1, None].astype(np.float32), 3, 2)
    return T, M, (x0, y0, x1, y1)


def _find(f, tpl):
    """칸 f 에서 본보기 총이 기본 위치에서 얼마나 옮겨 있는지 (dx, dy, 오차)"""
    T, M, (x0, y0, x1, y1) = tpl
    R = SEARCH
    pad = np.zeros((C + 2 * R, C + 2 * R, 3), np.float32)
    pad[R:R + C, R:R + C] = f[..., :3]
    win = pad[y0:y1 + 2 * R, x0:x1 + 2 * R]
    s = cv2.matchTemplate(win, T, cv2.TM_SQDIFF, mask=M)
    mn, _, loc, _ = cv2.minMaxLoc(s)
    return loc[0] - R, loc[1] - R, mn / M.sum()


def _gun_mask(f, dx, dy):
    pm = np.zeros((C, C), np.uint8)
    cv2.fillPoly(pm, [GUN_POLY + np.array([dx, dy])], 1)
    m = (pm > 0) & (f[..., 3] > 0)
    # 외곽 바로 바깥의 회색·검정 찌꺼기(총 테두리)도 같이
    ring = (cv2.dilate(pm, np.ones((5, 5), np.uint8)) > 0) & ~(pm > 0)
    rgb = f[..., :3].astype(int)
    grayish = (rgb.max(-1) - rgb.min(-1) < 26) & (rgb.max(-1) < 120) & (f[..., 3] > 0)
    return m | (ring & grayish), pm > 0


def _fill_hole(f, hole):
    """총이 있던 자리 메우기: 모양(알파)은 주변에서 이어 붙이고, 색은 주변 몸 색으로 번지게, 바깥엔 검은 테두리"""
    out = f.copy()
    out[hole] = 0
    h8 = hole.astype(np.uint8)
    alpha = (out[..., 3] > 0).astype(np.uint8) * 255
    a2 = cv2.inpaint(alpha, h8, 6, cv2.INPAINT_TELEA)
    body = (alpha > 0) | (hole & (a2 > 150))
    # 구멍 안 몸 부분: 가장 가까운 '밝은 몸 색'(테두리·벨트 같은 어두운 색 말고)으로 → 픽셀 그림처럼 납작하게
    rgb = out[..., :3].astype(int)
    lum = rgb.mean(-1)
    src = (alpha > 0) & ~hole & (lum > 70)
    _, lab = cv2.distanceTransformWithLabels((~src).astype(np.uint8), cv2.DIST_L2, 5, labelType=cv2.DIST_LABEL_PIXEL)
    sy, sx = np.nonzero(src)
    table = np.zeros((lab.max() + 1, 3), np.uint8)
    ids = lab[sy, sx]
    table[ids] = out[sy, sx, :3]
    near = table[lab]
    fill = hole & body
    out[..., :3][fill] = near[fill]
    out[..., 3][fill] = 255
    # 새로 생긴 가장자리에 테두리(원본 테두리 색)
    edge = fill & (cv2.erode(body.astype(np.uint8), np.ones((3, 3), np.uint8)) == 0)
    out[edge] = (28, 18, 14, 255)
    # 아주 작은 떨어진 조각 정리
    m = (out[..., 3] > 0).astype(np.uint8)
    n, lab, st, _ = cv2.connectedComponentsWithStats(m, connectivity=8)
    if n > 2:
        big = np.argmax(st[1:, 4]) + 1
        for i in range(1, n):
            if i != big and st[i, 4] < 30:
                out[lab == i] = 0
    return out


def _load_new_gun():
    import os
    from PIL import Image
    root = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
    im = Image.open(os.path.join(root, NEW_GUN)).convert('RGBA')
    w, h = im.size
    big = im.resize((round(w * NEW_GUN_SCALE), round(h * NEW_GUN_SCALE)), Image.NEAREST)
    return np.array(big)


def gun_muzzle_px():
    """회전 중심에서 총구까지 (오른쪽 +, 위 +) px — 게임 Art.GunMuzzle 과 같아야 함"""
    return ((NEW_MUZZLE[0] - NEW_GRIP[0]) * NEW_GUN_SCALE, (NEW_GRIP[1] - NEW_MUZZLE[1]) * NEW_GUN_SCALE)


def _paw(f, dx, dy):
    """칸 f 에서 펌프를 쥔 앞발만 (회색 금속 빼고)"""
    x0, y0, x1, y1 = PAW_BOX
    x0, x1, y0, y1 = x0 + dx, x1 + dx, y0 + dy, y1 + dy
    sub = f[y0:y1, x0:x1].copy()
    rgb = sub[..., :3].astype(int)
    gray = (rgb.max(-1) - rgb.min(-1) < 22) & (rgb.max(-1) < 150)
    sub[gray & (rgb.max(-1) > 60)] = 0      # 금속 부분 제거(검은 테두리는 남김)
    return sub, (x0, y0)


def _make_gun(f, dx, dy):
    """새 총 + 이 스킨의 앞발 → 총 캔버스 (회전 중심 = GUN_PIVOT)"""
    g = np.zeros((GUN_H, GUN_W, 4), np.uint8)
    ng = _load_new_gun()
    gx = GUN_PIVOT[0] - round(NEW_GRIP[0] * NEW_GUN_SCALE)
    gy = GUN_PIVOT[1] - round(NEW_GRIP[1] * NEW_GUN_SCALE)
    _blit(g, ng, gx, gy)
    paw, (px0, py0) = _paw(f, dx, dy)
    # 앞발: 몸 칸의 회전 중심(NEW_PIVOT_CELL+옮김) 기준 같은 자리
    cx, cy = NEW_PIVOT_CELL[0] + dx, NEW_PIVOT_CELL[1] + dy
    _blit(g, paw, GUN_PIVOT[0] + (px0 - cx), GUN_PIVOT[1] + (py0 - cy))
    return g


def _blit(dst, src, x, y):
    h, w = src.shape[:2]
    H, W = dst.shape[:2]
    sx0, sy0 = max(0, -x), max(0, -y)
    dx0, dy0 = max(0, x), max(0, y)
    w2, h2 = min(w - sx0, W - dx0), min(h - sy0, H - dy0)
    if w2 <= 0 or h2 <= 0:
        return
    s = src[sy0:sy0 + h2, sx0:sx0 + w2]
    d = dst[dy0:dy0 + h2, dx0:dx0 + w2]
    m = s[..., 3] > 0
    d[m] = s[m]


def muzzle_flash(sheet0):
    """기본 스킨 5번 칸에서 총구 오른쪽 불꽃만 (왼쪽 가운데 = 총구)"""
    tpl = _template(_cell(sheet0, 4))
    f = _cell(sheet0, 5)
    dx, dy, _ = _find(f, tpl)
    mx, my = MUZZLE[0] + dx, MUZZLE[1] + dy
    sub = f[my - 55:my + 55, mx - 4:mx + 86].copy()     # 90x110, 총구 = (4, 55)
    rgb = sub[..., :3].astype(int)
    warm = (rgb[..., 0] > 150) & (rgb[..., 0] > rgb[..., 2] + 40)
    sub[~warm] = 0
    return sub


def split(sheet, ref_sheet):
    """sheet: 정리된 캐릭터 시트(4x4). ref_sheet: 기본 스킨 시트(본보기). → (총 지운 시트, 총 그림, 칸별 회전 중심 8개)"""
    tpl = _template(_cell(ref_sheet, 4))
    out = sheet.copy()
    offs = []
    for i in range(8):
        dx, dy, err = _find(_cell(sheet, i), tpl)
        offs.append((dx, dy, err))
    # 6번(반동) 칸은 총이 들려 있어 본보기와 안 맞음 → 몸은 7번 칸 것을 쓰고 반동은 코드로
    gun = _make_gun(_cell(sheet, 4), offs[4][0], offs[4][1])
    for i in range(8):
        dx, dy, _ = offs[i]
        f = _cell(sheet, i).copy()
        if i >= 4:   # 총구 불꽃·튀는 조각(총구보다 오른쪽) 지우기
            f[:, MUZZLE[0] + dx - 12:] = 0
        m, _ = _gun_mask(f, dx, dy)
        r, c = i // 4, i % 4
        out[r * C:(r + 1) * C, c * C:(c + 1) * C] = _fill_hole(f, m)
    out[1 * C:2 * C, 2 * C:3 * C] = out[1 * C:2 * C, 3 * C:4 * C]
    offs[6] = offs[7]
    pos = [(NEW_PIVOT_CELL[0] + dx, NEW_PIVOT_CELL[1] + dy) for dx, dy, _ in offs]
    return out, gun, pos, [e for _, _, e in offs]
