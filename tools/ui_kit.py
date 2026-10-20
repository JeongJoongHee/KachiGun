# -*- coding: utf-8 -*-
"""사용자 UI 팩(리소스/ 의 Woods Journey 계열 도트 UI)에서 게임 UI 부품을 잘라 5배로 키워
Kachi_Gun/Assets/Resources/Art/Kit/*.png 로 저장한다 (원본은 건드리지 않음, 다시 돌려도 같은 결과).
9-slice 테두리(원본 px)는 BORDER — 게임 Art.Kit() 이 같은 값 × S 로 자른다.
미리보기: tools/_preview/ui_kit.png
"""
import os
import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
SRC = os.path.join(ROOT, '리소스')
OUT = os.path.join(ROOT, 'Kachi_Gun', 'Assets', 'Resources', 'Art', 'Kit')
PREV = os.path.join(os.path.dirname(__file__), '_preview', 'ui_kit.png')
S = 5   # 키우는 배수 (도트 1칸 = 5px)

W = 'Woods Journey UI Basic Pack.png'
B = 'Buttons.png'
# 이름: (그림, x, y, w, h)
CROPS = {
    # 창·판
    'window':       ('Settings.png', 7, 0, 98, 149),      # 초록 제목줄 + X + 양피지
    'panel_tan':    ('Main_tiles.png', 208, 196, 48, 40), # 양피지 판(갈색 테두리)
    'panel_brown':  ('Main_tiles.png', 299, 155, 26, 26), # 진한 나무 테두리 판
    'slot':         ('Main_tiles.png', 337, 209, 14, 14), # 진한 황토 칸
    'slot_light':   ('Main_tiles.png', 337, 225, 14, 14),
    'wood':         (W, 16, 16, 28, 30),                  # 둥근 나무판(그림자)
    'wood_dark':    (W, 16, 77, 28, 27),
    'tile':         (W, 80, 16, 18, 20),                  # 작은 나무 받침(아이콘 버튼)
    'plank':        (W, 80, 48, 30, 14),                  # 나무 막대 버튼
    'plank_thin':   (W, 80, 83, 30, 11),
    # 버튼
    'btn_green':    (B, 212, 142, 42, 13),
    'btn_green_hi': (B, 308, 142, 42, 13),
    'sq_green':     (B, 13, 12, 22, 23),
    'btn_down':     (B, 339, 364, 13, 14),                # 초록 ▼ (안내 화살표)
    'btn_x':        (W, 112, 192, 16, 16),                # 나무 X (닫기)
    'btn_play':     (W, 208, 192, 16, 16),                # 나무 ▶ (자동강화)
    'badge':        (W, 112, 224, 16, 16),                # 나무 ! → 빨갛게 (알림 표시)
    'check_on':     (B, 336, 323, 11, 12),
    'check_off':    (B, 336, 339, 11, 12),
    'outline':      (B, 336, 419, 11, 12),                # → 흰색으로 바꿔 등급색 테두리
    # 막대
    'bar_track':    (W, 368, 192, 30, 4),
    'bar_red':      (W, 304, 192, 30, 4),
    'bar_teal':     (W, 304, 198, 30, 4),
    'bar_green':    (W, 304, 204, 30, 4),
    # 결과
    'star':         ('Win_loose.png', 25, 386, 29, 28),
    'star_off':     ('Win_loose.png', 105, 386, 29, 28),
    'star_s':       ('Win_loose.png', 9, 396, 17, 17),
    'star_s_off':   ('Win_loose.png', 89, 396, 17, 17),
    'lose':         ('Win_loose.png', 316, 387, 25, 25),
    # 작은 그림
    'lock':         ('Levels.png', 211, 193, 9, 13),
    'node':         ('Levels.png', 321, 160, 30, 31),
    'node_dark':    ('Levels.png', 353, 160, 30, 31),
    'sound':        ('Settings.png', 64, 227, 16, 12),
    'sound_off':    ('Settings.png', 0, 227, 16, 12),
    'music':        ('Settings.png', 64, 242, 13, 13),
    'music_off':    ('Settings.png', 0, 242, 13, 13),
}
# Icons.png 16px 칸 (열, 줄)
ICONS = {
    'i_gear': (5, 3), 'i_trophy': (1, 3), 'i_chest': (2, 0), 'i_lock': (3, 0), 'i_star': (1, 1),
    'i_play': (1, 2), 'i_pause': (2, 2), 'i_check': (3, 2), 'i_x': (4, 2), 'i_skull': (0, 0),
    'i_heart': (5, 0), 'i_down': (3, 4), 'i_info': (0, 4), 'i_question': (1, 4), 'i_music': (3, 5),
    'i_sound': (0, 2), 'i_diamond': (4, 1), 'i_sword': (1, 0), 'i_shield': (0, 3), 'i_person': (2, 3),
}
# 9-slice 테두리 (원본 px: 왼, 아래, 오른, 위) — 게임 Art.Kit 과 같게
BORDER = {
    'window': (25, 8, 13, 16), 'panel_tan': (5, 5, 5, 5), 'panel_brown': (5, 5, 5, 5),
    'slot': (2, 2, 2, 2), 'slot_light': (2, 2, 2, 2), 'wood': (6, 7, 6, 5), 'wood_dark': (6, 7, 6, 5),
    'tile': (5, 6, 5, 5), 'plank': (5, 5, 5, 4), 'plank_thin': (5, 4, 5, 3),
    'btn_green': (5, 5, 5, 4), 'btn_green_hi': (5, 5, 5, 4), 'sq_green': (6, 7, 6, 5),
    'outline': (4, 4, 4, 4), 'bar_track': (2, 1, 2, 1), 'bar_red': (2, 1, 2, 1), 'bar_teal': (2, 1, 2, 1),
    'bar_green': (2, 1, 2, 1), 'check_on': (4, 4, 4, 4), 'check_off': (4, 4, 4, 4),
}


def scale2x(a):
    """Scale2x(EPX): 2배로 키우며 대각선 계단을 메워 둥근 모서리를 매끈하게"""
    p = np.pad(a, ((1, 1), (1, 1), (0, 0)), mode='edge')
    B, D, E, F, H = p[:-2, 1:-1], p[1:-1, :-2], p[1:-1, 1:-1], p[1:-1, 2:], p[2:, 1:-1]
    eq = lambda x, y: np.all(x == y, axis=-1)
    c = ~eq(B, H) & ~eq(D, F)
    def pick(cond, src):
        return np.where((c & cond)[..., None], src, E)
    e0 = pick(eq(D, B), D); e1 = pick(eq(B, F), F)
    e2 = pick(eq(D, H), D); e3 = pick(eq(H, F), F)
    h, w = a.shape[:2]
    out = np.empty((h * 2, w * 2, a.shape[2]), a.dtype)
    out[0::2, 0::2] = e0; out[0::2, 1::2] = e1; out[1::2, 0::2] = e2; out[1::2, 1::2] = e3
    return out


def big(a):
    """5배: Scale2x 두 번(4배, 대각선 매끈) → 5배로 부드럽게 늘림(가장자리 안티에일리어싱).
    투명 경계에 검은 테가 안 생기게 알파를 곱해서 늘린 뒤 되돌림"""
    h, w = a.shape[:2]
    x = scale2x(scale2x(a)).astype(np.float32)
    al = x[..., 3:4] / 255.0
    pm = np.concatenate([x[..., :3] * al, x[..., 3:4]], -1)
    chans = [np.array(Image.fromarray(pm[..., i]).resize((w * S, h * S), Image.BICUBIC)) for i in range(4)]
    r = np.stack(chans, -1)
    al2 = np.clip(r[..., 3:4], 0, 255) / 255.0
    rgb = np.where(al2 > 1e-3, r[..., :3] / np.maximum(al2, 1e-3), 0)
    out = np.concatenate([rgb, np.clip(r[..., 3:4], 0, 255)], -1)
    out[..., 3][out[..., 3] < 8] = 0
    return np.clip(out, 0, 255).astype(np.uint8)


def mode_color(px):
    """가장 흔한 색 (px: N x 4)"""
    v, c = np.unique(px.reshape(-1, 4), axis=0, return_counts=True)
    return v[np.argmax(c)]


def clean_window(a):
    """창 그림의 찢긴 자국 펴기: 제목줄은 줄마다 가장 흔한 색으로(위쪽 홈 없앰, 오른쪽 X 칸은 그대로),
    양피지 왼·오른 테두리는 열마다, 아래 테두리는 줄마다 가장 흔한 색으로 → 반듯한 테두리"""
    a = a.copy()
    h, w = a.shape[:2]
    TB = 15            # 제목줄 높이
    for y in range(0, TB):
        seg = a[y, 3:w - 26]
        a[y, 3:w - 26] = mode_color(seg)
    body = slice(TB + 3, h - 8)
    for x in list(range(0, 8)) + list(range(w - 8, w)):
        a[body, x] = mode_color(a[body, x])
    for y in range(h - 8, h):
        a[y, 8:w - 8] = mode_color(a[y, 8:w - 8])
    return a


def trim(a):
    ys, xs = np.nonzero(a[..., 3])
    return a[ys.min():ys.max() + 1, xs.min():xs.max() + 1]


def to_white(a):
    """테두리를 흰색 명암으로 (게임에서 등급 색을 곱해 씀)"""
    out = a.copy().astype(float)
    lum = out[..., :3].mean(-1)
    m = out[..., 3] > 0
    v = lum[m]
    k = (v - v.min()) / max(1e-6, v.max() - v.min())
    g = 150 + 105 * k
    out[..., 0][m] = g; out[..., 1][m] = g; out[..., 2][m] = g
    return out.astype(np.uint8)


def main():
    os.makedirs(OUT, exist_ok=True)
    sheets = {}
    made = {}
    for name, (sheet, x, y, w, h) in CROPS.items():
        if sheet not in sheets:
            sheets[sheet] = np.array(Image.open(os.path.join(SRC, sheet)).convert('RGBA'))
        a = sheets[sheet][y:y + h, x:x + w].copy()
        if name == 'window':
            a = clean_window(a)
        if name == 'outline':
            a = to_white(a)
            a[2:-3, 2:-2] = 0          # 안쪽 비우기(테두리만)
        made[name] = big(a)
    ic = np.array(Image.open(os.path.join(SRC, 'Icons.png')).convert('RGBA'))
    for name, (cx, cy) in ICONS.items():
        a = ic[cy * 16:(cy + 1) * 16, cx * 16:(cx + 1) * 16].copy()
        made[name] = big(trim(a))
    # 갈색 받침 위에 올릴 밝은 아이콘(_w): 테두리(어두운 색)는 그대로, 안쪽 갈색을 크림색 명암으로
    for name in ('i_gear', 'i_trophy', 'i_chest', 'i_lock', 'i_pause'):
        a = made[name].astype(float)
        m = a[..., 3] > 0
        lum = a[..., :3].mean(-1)
        inner = m & (lum > 70)
        k = np.clip((lum - 70) / 90.0, 0, 1)
        cream = np.stack([235 + 15 * k, 210 + 25 * k, 160 + 40 * k], -1)
        a[..., :3][inner] = cream[inner]
        made[name + '_w'] = np.clip(a, 0, 255).astype(np.uint8)
    # 알림 표시: 나무 ! 를 빨간색으로
    a = made['badge'].astype(float)
    m = a[..., 3] > 0
    lum = a[..., :3].mean(-1)
    red = np.stack([lum * 1.55 + 20, lum * 0.45, lum * 0.38], -1)
    a[..., :3][m] = red[m]
    made['badge'] = np.clip(a, 0, 255).astype(np.uint8)
    for name, a in made.items():
        Image.fromarray(a).save(os.path.join(OUT, name + '.png'))
    # 미리보기
    tiles = list(made.items())
    W_, x, y, rowh = 1600, 10, 10, 0
    pos = []
    for n, a in tiles:
        h, w = a.shape[:2]
        if x + w > W_: x, y, rowh = 10, y + rowh + 30, 0
        pos.append((n, a, x, y)); x += w + 20; rowh = max(rowh, h)
    pv = Image.new('RGBA', (W_, y + rowh + 40), (50, 60, 80, 255))
    from PIL import ImageDraw
    d = ImageDraw.Draw(pv)
    for n, a, x, y in pos:
        pv.alpha_composite(Image.fromarray(a), (x, y))
        d.text((x, y + a.shape[0] + 2), n, fill=(255, 255, 0, 255))
    pv.convert('RGB').save(PREV)
    print(len(made), 'parts ->', OUT)


if __name__ == '__main__':
    main()
