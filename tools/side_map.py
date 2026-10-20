# -*- coding: utf-8 -*-
"""가로 전장 배경 만들기: 원본 Main_Map(대각선 숲길)의 조각을 이어 붙여
위·아래 = 숲, 가운데 = 흙과 밝은 길(가로)인 배경을 만든다. 원본은 건드리지 않는다.

방식: 조각 이어 붙이기(Efros-Freeman 텍스처 전이). 1/4 크기에서 조각을 고르고(겹친 부분 색이 비슷 +
'숲에서 떨어진 거리'가 목표 배치와 비슷한 조각), 겹친 곳은 가장 티 안 나는 선으로 잘라 붙인다.
조각은 원본 그대로(좌우 뒤집기만) 옮겨 붙이므로 도트가 뭉개지지 않는다. 나무가 거꾸로 서지 않게 위아래 뒤집기는 안 씀.

사용: python tools/side_map.py [seed]   → Kachi_Gun/Assets/Resources/Art/Map/side_map.png (1728x2448)
미리보기: tools/_preview/side_map.png
배치 숫자(숲 경계·밝은 길)는 아래 LAYOUT. 게임 쪽 Field.cs 의 FieldTop/FieldBottom 과 맞춘다.
"""
import os, sys
import numpy as np
import cv2
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
SRC = os.path.join(ROOT, '리소스', 'Main_Map.png')
OUT = os.path.join(ROOT, 'Kachi_Gun', 'Assets', 'Resources', 'Art', 'Map', 'side_map.png')
PREV = os.path.join(os.path.dirname(__file__), '_preview', 'side_map.png')

W, H = 1728, 2448          # 결과 크기(맵 픽셀) — 원본과 같게(카메라 계산 그대로)
Q = 4                      # 1/4 크기에서 조각 고르기 (원본 도트 1칸 ≈ 4~5px)
P, O = 40, 10              # 조각 크기·겹침 (1/4 크기 기준 → 실제 160px·40px)
CANDS = 1800               # 조각마다 살펴볼 후보 수

# 목표 배치(맵 픽셀, 위가 0). 화면에서 위 15% ≈ 367px 은 HUD, 아래 19% ≈ 1983px 부터는 강화 카드가 덮는다.
LAYOUT = dict(
    top_edge=330,          # 위 숲 경계(이 선 아래로 ~250px 는 바위 턱·마른 풀)
    bottom_edge=2140,      # 아래 숲 경계
    wave=38,               # 경계 출렁임(px)
)

# 원본 숲 경계선(맵 픽셀) — Field.cs 의 옛 길 계산과 같은 두 대각선
A0, A1 = (0, 788), (774, 0)          # 왼쪽 위 숲
B0, B1 = (890, 2448), (1728, 1432)   # 오른쪽 아래 숲


def line_dist(a, b, px, py):
    """선까지 거리(길 쪽 +)"""
    d = (b[0] - a[0]) * (py - a[1]) - (b[1] - a[1]) * (px - a[0])
    return d / np.hypot(b[0] - a[0], b[1] - a[1])


def source():
    S = np.array(Image.open(SRC).convert('RGB'))
    h, w = S.shape[:2]
    yy, xx = np.mgrid[0:h // Q, 0:w // Q] * float(Q) + Q / 2
    da = line_dist(A0, A1, xx, yy)            # 위 숲에서 + 쪽이 길
    db = -line_dist(B0, B1, xx, yy)           # 아래 숲
    dist = np.minimum(da, db)
    q = cv2.resize(S, (w // Q, h // Q), interpolation=cv2.INTER_AREA).astype(np.float32)
    return S, q, dist.astype(np.float32)


def target_dist(rng):
    xs = np.arange(W // Q) * Q + Q / 2
    ys = np.arange(H // Q) * Q + Q / 2

    def wavy(base, phase):
        k = LAYOUT['wave']
        v = base + k * np.sin(xs / 210 + phase) + k * 0.5 * np.sin(xs / 77 + phase * 2.3)
        return v + rng.normal(0, 6, len(xs)).cumsum() * 0.15
    top = wavy(LAYOUT['top_edge'], rng.uniform(0, 6))
    bot = wavy(LAYOUT['bottom_edge'], rng.uniform(0, 6))
    d = np.minimum(ys[:, None] - top[None, :], bot[None, :] - ys[:, None])
    return d.astype(np.float32)


def min_cut(err, axis):
    """겹친 부분 오차에서 가장 티 안 나는 자르는 선. axis=1: 세로 선(왼쪽 겹침), 0: 가로 선(위 겹침).
    돌려주는 mask: 새 조각을 쓸 곳 = True"""
    e = err if axis == 1 else err.T
    n, m = e.shape
    cost = e.copy()
    for i in range(1, n):
        prev = cost[i - 1]
        left = np.r_[np.inf, prev[:-1]]
        right = np.r_[prev[1:], np.inf]
        cost[i] += np.minimum(np.minimum(left, prev), right)
    path = np.zeros(n, int)
    path[-1] = int(np.argmin(cost[-1]))
    for i in range(n - 2, -1, -1):
        j = path[i + 1]
        lo, hi = max(j - 1, 0), min(j + 2, m)
        path[i] = lo + int(np.argmin(cost[i, lo:hi]))
    mask = np.zeros((n, m), bool)
    for i in range(n):
        mask[i, path[i]:] = True
    return mask if axis == 1 else mask.T


def build(seed=7):
    rng = np.random.default_rng(seed)
    S, q, dist = source()
    # 좌우 뒤집은 원본도 후보로
    Sf, qf, df = S[:, ::-1].copy(), q[:, ::-1].copy(), dist[:, ::-1].copy()
    srcs = [(S, q, dist), (Sf, qf, df)]
    # 너무 깊은 숲 속(나무만 빽빽)은 목표에서도 깊은 숲에서만 쓰이게 거리 그대로 둠, 범위만 자름
    clipd = lambda d: np.clip(d, -260, 820) / 100.0

    tgt = clipd(target_dist(rng))
    hq, wq = H // Q, W // Q
    outq = np.zeros((hq, wq, 3), np.float32)
    filled = np.zeros((hq, wq), bool)
    out = np.zeros((H, W, 3), np.uint8)

    views = []
    for (s_full, s_q, s_d) in srcs:
        vq = np.lib.stride_tricks.sliding_window_view(s_q, (P, P), axis=(0, 1))   # (y, x, 3, P, P)
        vd = np.lib.stride_tricks.sliding_window_view(clipd(s_d), (P, P), axis=(0, 1))
        views.append((s_full, vq, vd))

    step = P - O
    ys = list(range(0, hq - P, step)) + [hq - P]
    xs = list(range(0, wq - P, step)) + [wq - P]
    for ty in ys:
        for tx in xs:
            t_d = tgt[ty:ty + P, tx:tx + P]
            have = filled[ty:ty + P, tx:tx + P]
            cur = outq[ty:ty + P, tx:tx + P]
            best = None
            for si, (s_full, vq, vd) in enumerate(views):
                ny, nx = vq.shape[0], vq.shape[1]
                cy = rng.integers(0, ny, CANDS // 2)
                cx = rng.integers(0, nx, CANDS // 2)
                cand = np.transpose(vq[cy, cx], (0, 2, 3, 1))           # (n, P, P, 3)
                cd = vd[cy, cx]                                         # (n, P, P)
                if have.any():
                    diff = ((cand - cur[None]) ** 2).sum(-1)
                    e_ov = (diff * have[None]).sum((1, 2)) / have.sum()
                else:
                    e_ov = np.zeros(len(cy))
                e_g = ((cd - t_d[None]) ** 2).mean((1, 2))
                e = e_ov + 2600.0 * e_g
                for k in np.argsort(e)[:3]:
                    if best is None or e[k] < best[0]:
                        best = (e[k], si, int(cy[k]), int(cx[k]))
            _, si, sy, sx = best
            s_full, vq, _ = views[si]
            patch = np.transpose(vq[sy, sx], (1, 2, 0))
            # 잘라 붙일 모양
            m = np.ones((P, P), bool)
            if have.any():
                err = ((patch - cur) ** 2).sum(-1)
                if tx > 0 and have[:, :O].all():
                    m &= np.c_[min_cut(err[:, :O], 1), np.ones((P, P - O), bool)]
                if ty > 0 and have[:O, :].all():
                    m &= np.r_[min_cut(err[:O, :], 0), np.ones((P - O, P), bool)]
                m |= ~have
            outq[ty:ty + P, tx:tx + P][m] = patch[m]
            filled[ty:ty + P, tx:tx + P] = True
            mf = np.kron(m, np.ones((Q, Q), bool))
            fy, fx, sfy, sfx = ty * Q, tx * Q, sy * Q, sx * Q
            src_block = s_full[sfy:sfy + P * Q, sfx:sfx + P * Q]
            dst = out[fy:fy + P * Q, fx:fx + P * Q]
            dst[mf] = src_block[mf]
    return out


# 밝기 보정 (10-19 사용자 "배경이 너무 어두워서 톤을 올려줘"): 어두운 곳을 더 많이 올리는 감마 + 살짝 밝게
TONE = dict(gamma=0.78, gain=1.06, lift=6)


def tone(img):
    x = img.astype(np.float32) / 255.0
    x = np.power(x, TONE['gamma']) * TONE['gain'] + TONE['lift'] / 255.0
    return np.clip(x * 255.0, 0, 255).astype(np.uint8)


if __name__ == '__main__':
    seed = int(sys.argv[1]) if len(sys.argv) > 1 else 7
    img = tone(build(seed))
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    Image.fromarray(img).save(OUT)
    os.makedirs(os.path.dirname(PREV), exist_ok=True)
    Image.fromarray(img).resize((W // 3, H // 3), Image.LANCZOS).save(PREV)
    print('done ->', OUT)
