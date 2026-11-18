# -*- coding: utf-8 -*-
"""11-18 카피친구 15마리 얼굴 아이콘 (밝은 낙서풍 · 카치건 화풍) → Kachi_Gun/Assets/Resources/Art/Friend/friend_01~15.png (256x256)
순서 = Table/Friend.xlsx Friend_List (Icon 칸 이름과 같음).
사용: PowerShell 에서  $env:PYTHONUTF8='1'; python tools/friend_icons.py      (Bash 에선 Edge 가 안 뜸)
미리보기: tools/_preview/friend_icons.png
화풍: 진갈색 선 · 단색 파스텔 칠 · 점 눈 · 작은 코 + ㅅ/ω 입 · 볼터치 없음 · 무늬는 테두리 없는 한 단계 짙은 색(머리 모양 안으로 자름)
"""
import os, sys
from PIL import Image
sys.path.insert(0, os.path.dirname(__file__))
from doodle_art import render, P, L, C, E, F, INK, ART, PREV

SW = 3.4            # 100 칸 기준 선 굵기 (아이콘)
OUT = os.path.join(ART, 'Friend')
NAMES = ['토끼', '다람쥐', '햄스터', '양', '염소', '사슴', '나무늘보', '코알라', '웜뱃', '기니피그', '너구리', '고양이', '강아지', '젖소', '당나귀']


def head(cx=50, cy=60, rx=30, ry=26):
    """머리 = 아래가 조금 넓은 둥근 모양 (path 글자)"""
    return (f'M{cx - rx} {cy} Q{cx - rx} {cy - ry} {cx} {cy - ry} Q{cx + rx} {cy - ry} {cx + rx} {cy} '
            f'Q{cx + rx} {cy + ry * .95} {cx} {cy + ry} Q{cx - rx} {cy + ry * .95} {cx - rx} {cy} Z')


def clip(d, inner, k):
    """d 모양 안에서만 보이는 무늬 (테두리 없는 칠)"""
    return f'<clipPath id="cl{k}"><path d="{d}"/></clipPath><g clip-path="url(#cl{k})">{inner}</g>'


def eyes(y=58, dx=12, r=3.4, cx=50):
    return C(cx - dx, y, r, INK, 0) + C(cx + dx, y, r, INK, 0) + C(cx - dx + 1.1, y - 1.2, 1.0, '#ffffff', 0) + C(cx + dx + 1.1, y - 1.2, 1.0, '#ffffff', 0)


def nose_mouth(y=66, nose='#6b4a3a', w=5, cx=50, open_mouth=False):
    s = P(f'M{cx - w} {y - 2} Q{cx} {y - 4.5} {cx + w} {y - 2} Q{cx + w * .5} {y + 2.6} {cx} {y + 3} Q{cx - w * .5} {y + 2.6} {cx - w} {y - 2} Z', nose, 2.4)
    s += L(f'M{cx} {y + 3} L{cx} {y + 6} M{cx - 6} {y + 6.5} Q{cx - 3} {y + 9.5} {cx} {y + 6} Q{cx + 3} {y + 9.5} {cx + 6} {y + 6.5}', 2.4)
    return s


def ear(x, y, w, h, rot, fill, inner=None, sw=SW):
    s = f'<g transform="rotate({rot} {x} {y})">' + P(f'M{x - w} {y} Q{x - w} {y - h} {x} {y - h} Q{x + w} {y - h} {x + w} {y} Z', fill, sw)
    if inner: s += F(f'M{x - w * .5} {y - 1} Q{x - w * .5} {y - h * .78} {x} {y - h * .78} Q{x + w * .5} {y - h * .78} {x + w * .5} {y - 1} Z', inner)
    return s + '</g>'


def tri_ear(x, y, w, h, lean, fill, inner=None, tip=None):
    s = P(f'M{x - w} {y} L{x + lean} {y - h} L{x + w} {y} Z', fill, SW)
    if tip: s += F(f'M{x + lean - w * .35} {y - h * .7} L{x + lean} {y - h} L{x + lean + w * .35} {y - h * .7} Z', tip)
    if inner: s += F(f'M{x - w * .5} {y - 1} L{x + lean * .8} {y - h * .7} L{x + w * .5} {y - 1} Z', inner)
    return s


def side_ear(x, y, rx, ry, rot, fill, inner=None):
    s = E(x, y, rx, ry, fill, SW, rot)
    if inner: s += f'<ellipse cx="{x}" cy="{y}" rx="{rx * .55}" ry="{ry * .5}" fill="{inner}" transform="rotate({rot} {x} {y})"/>'
    return s


def muzzle(y, rx, ry, fill, cx=50):
    return f'<ellipse cx="{cx}" cy="{y}" rx="{rx}" ry="{ry}" fill="{fill}"/>'


def animals():
    A = {}
    PINK = '#f2b8b0'
    # 1 토끼 — 흰 머리 · 곧은 긴 귀(분홍 안)
    h = head(50, 62, 28, 25); W = '#f7f2ea'
    A[1] = (ear(39, 44, 8, 38, -10, W, PINK) + ear(61, 44, 8, 38, 10, W, PINK) + P(h, W, SW)
            + eyes(59, 11) + nose_mouth(68, '#e8968a'))
    # 2 다람쥐 — 뒤에 큰 꼬리 · 뾰족 귀 끝 털 · 크림 주둥이
    h = head(46, 62, 27, 24); Bc = '#d08a52'
    tail = P('M66 84 Q96 84 94 52 Q92 22 70 18 Q56 18 60 30 Q76 32 78 50 Q80 70 62 72 Z', '#c27a44', SW) + F('M70 26 Q84 32 84 52 Q84 64 74 70 Q80 54 76 42 Q74 32 70 26 Z', '#e0a46e')
    A[2] = (tail + tri_ear(30, 46, 8, 16, -3, Bc, '#e8b88a', '#8a5a34') + tri_ear(60, 46, 8, 16, 3, Bc, '#e8b88a', '#8a5a34') + P(h, Bc, SW)
            + clip(h, muzzle(74, 16, 12, '#f3dcb8'), 2) + eyes(58, 10, cx=46) + nose_mouth(68, '#6b4a3a', 4, cx=46))
    # 3 햄스터 — 넓적한 노랑 머리 · 흰 아래 얼굴 · 작은 둥근 귀
    h = head(50, 62, 33, 25); Hc = '#f0c27a'
    A[3] = (side_ear(28, 40, 8, 8, 0, Hc, PINK) + side_ear(72, 40, 8, 8, 0, Hc, PINK) + P(h, Hc, SW)
            + clip(h, f'<path d="M14 70 Q30 58 50 64 Q70 58 86 70 L86 100 L14 100 Z" fill="#fbf1de"/>', 3)
            + eyes(57, 12) + nose_mouth(67, '#e8968a', 4))
    # 4 양 — 뭉게 털 · 베이지 얼굴 · 옆으로 누운 귀
    wool = ''.join(C(50 + 32 * __import__('math').cos(a / 10), 52 + 26 * __import__('math').sin(a / 10), 12, '#fbf7ee', SW) for a in range(0, 63, 7))
    face = 'M34 56 Q34 36 50 36 Q66 36 66 56 Q66 80 50 82 Q34 80 34 56 Z'; Sc = '#ead6bf'
    A[4] = (wool + C(50, 52, 30, '#fbf7ee', 0) + side_ear(28, 54, 11, 5, -18, Sc) + side_ear(72, 54, 11, 5, 18, Sc) + P(face, Sc, SW)
            + ''.join(C(x, 34, 8, '#fbf7ee', SW * .8) for x in (40, 50, 60)) + eyes(58, 8) + nose_mouth(70, '#6b4a3a', 4))
    # 5 염소 — 긴 얼굴 · 뒤로 휜 뿔 · 처진 귀 · 턱수염
    face = 'M32 54 Q32 32 50 32 Q68 32 68 54 Q68 82 50 86 Q32 82 32 54 Z'; Gc = '#efe9df'
    horn = lambda x, s: P(f'M{x} 38 Q{x + s * 4} 16 {x + s * 20} 14 Q{x + s * 10} 22 {x + s * 9} 40 Z', '#c9b79a', SW)
    A[5] = (horn(42, -1) + horn(58, 1) + side_ear(24, 50, 12, 5, 20, Gc, '#e3cfc4') + side_ear(76, 50, 12, 5, -20, Gc, '#e3cfc4')
            + P('M44 82 L50 98 L56 82 Z', '#ddd3c4', SW) + P(face, Gc, SW) + clip(face, muzzle(76, 14, 10, '#e2d8ca'), 5)
            + eyes(56, 10) + nose_mouth(71, '#8a6a5a', 4))
    # 6 사슴 — 갈색 · 뿔(가지) · 흰 점 · 밝은 주둥이
    h = head(50, 62, 26, 24); Dc = '#d9a46c'
    ant = lambda s: L(f'M{50 + s * 12} 42 L{50 + s * 20} 18 M{50 + s * 17} 28 L{50 + s * 30} 22 M{50 + s * 19} 22 L{50 + s * 14} 12', 8) + L(f'M{50 + s * 12} 42 L{50 + s * 20} 18 M{50 + s * 17} 28 L{50 + s * 30} 22 M{50 + s * 19} 22 L{50 + s * 14} 12', 3.4, '#9a6a44')
    A[6] = (ant(-1) + ant(1) + side_ear(24, 50, 11, 6, 25, Dc, '#f0cfa8') + side_ear(76, 50, 11, 6, -25, Dc, '#f0cfa8') + P(h, Dc, SW)
            + clip(h, muzzle(76, 15, 11, '#f3dcb8') + ''.join(C(x, y, 2.2, '#f7e8d0', 0) for x, y in ((40, 44), (50, 42), (60, 44))), 6)
            + eyes(58, 10) + nose_mouth(70, INK, 4))
    # 7 나무늘보 — 회갈색 · 크림 얼굴 가면 · 눈 옆 짙은 줄
    h = head(50, 60, 30, 27); Lc = '#b8a58a'
    mask = 'M26 60 Q26 42 50 42 Q74 42 74 60 Q74 80 50 82 Q26 80 26 60 Z'
    A[7] = (P(h, Lc, SW) + clip(h, f'<path d="{mask}" fill="#efe3cc"/>' + E(37, 58, 10, 5, '#6e5a46', 0, 18) + E(63, 58, 10, 5, '#6e5a46', 0, -18), 7)
            + C(38, 57, 3, INK, 0) + C(62, 57, 3, INK, 0) + C(39, 56, 1, '#ffffff', 0) + C(63, 56, 1, '#ffffff', 0)
            + nose_mouth(68, '#5a4334', 4) + L('M30 40 Q36 34 42 38 M58 38 Q64 34 70 40', 2.4, '#8a7660'))
    # 8 코알라 — 회색 · 큰 털 귀(흰 안) · 큰 까만 코
    h = head(50, 62, 28, 24); Kc = '#adadb5'
    A[8] = (C(24, 46, 14, Kc, SW) + C(76, 46, 14, Kc, SW) + C(24, 47, 8, '#ecebef', 0) + C(76, 47, 8, '#ecebef', 0) + P(h, Kc, SW)
            + eyes(58, 13) + P('M43 62 Q43 58 50 58 Q57 58 57 62 L56 72 Q50 76 44 72 Z', '#4a4048', 2.6) + L('M45 80 Q50 83 55 80', 2.4))
    # 9 웜뱃 — 갈색 · 작은 귀 · 넓적한 네모 코
    h = head(50, 62, 30, 25); Wc = '#a8805c'
    A[9] = (side_ear(30, 40, 7, 8, -15, Wc, '#c99a74') + side_ear(70, 40, 7, 8, 15, Wc, '#c99a74') + P(h, Wc, SW)
            + clip(h, muzzle(74, 17, 12, '#c49a74'), 9) + eyes(56, 12)
            + P('M40 64 Q40 60 50 60 Q60 60 60 64 Q60 71 50 71 Q40 71 40 64 Z', '#5a4334', 2.6) + L('M44 78 Q50 81 56 78', 2.4))
    # 10 기니피그 — 흰 바탕 · 주황 얼룩 · 꽃잎 귀
    h = head(50, 62, 31, 24)
    A[10] = (side_ear(26, 44, 8, 6, -30, '#e7a25a') + side_ear(74, 44, 8, 6, 30, '#fbf4ea') + P(h, '#fbf4ea', SW)
             + clip(h, '<path d="M14 30 L50 30 Q52 50 44 58 Q30 66 14 74 Z" fill="#e7a25a"/><path d="M64 34 Q78 36 86 48 L86 60 Q72 56 64 34 Z" fill="#8a6446"/>', 10)
             + eyes(58, 13) + nose_mouth(67, '#e8968a', 4))
    # 11 너구리 — 회갈색 · 눈가 짙은 띠 · 흰 주둥이 · 뾰족 귀
    h = head(50, 62, 30, 25); Rc = '#a8957e'
    A[11] = (tri_ear(30, 46, 9, 16, -4, Rc, '#d8c8b0', '#5a4a3c') + tri_ear(70, 46, 9, 16, 4, Rc, '#d8c8b0', '#5a4a3c') + P(h, Rc, SW)
             + clip(h, muzzle(72, 16, 13, '#f2ebdf') + '<path d="M18 58 Q34 48 46 56 L54 56 Q66 48 82 58 Q70 66 54 62 L46 62 Q30 66 18 58 Z" fill="#5a4a3c"/>', 11)
             + C(37, 57, 3.4, '#f2ebdf', 0) + C(63, 57, 3.4, '#f2ebdf', 0) + C(37, 57, 2.4, INK, 0) + C(63, 57, 2.4, INK, 0) + nose_mouth(68, INK, 4))
    # 12 고양이 — 주황 · 세모 귀(분홍) · 이마 줄무늬 · 수염
    h = head(50, 62, 30, 24); Cc = '#f2b06a'
    A[12] = (tri_ear(30, 48, 10, 18, -4, Cc, PINK) + tri_ear(70, 48, 10, 18, 4, Cc, PINK) + P(h, Cc, SW)
             + clip(h, '<path d="M44 38 L46 50 L48 38 Z M49 38 L50 52 L51 38 Z M52 38 L54 50 L56 38 Z" fill="#d98f4a"/>' + muzzle(72, 13, 9, '#fbe9d2'), 12)
             + eyes(60, 11) + nose_mouth(68, '#e8968a', 3.6) + L('M22 66 L36 68 M22 72 L36 71 M78 66 L64 68 M78 72 L64 71', 1.8))
    # 13 강아지 — 크림 · 늘어진 갈색 귀 · 혀
    h = head(50, 60, 27, 25); Pc = '#f3dcb0'
    droop = lambda s: P(f'M{50 + s * 20} 40 Q{50 + s * 36} 40 {50 + s * 34} 64 Q{50 + s * 30} 74 {50 + s * 24} 66 Q{50 + s * 22} 52 {50 + s * 18} 46 Z', '#b07a4a', SW)
    A[13] = (P(h, Pc, SW) + droop(-1) + droop(1) + clip(h, muzzle(72, 14, 10, '#fbf0dc') + C(36, 50, 7, '#e6c08a', 0), 13)
             + eyes(57, 10) + P('M47 76 L47 82 Q50 86 53 82 L53 76 Z', '#e8968a', 2.2) + nose_mouth(68, INK, 4.6))
    # 14 젖소 — 흰 바탕 · 까만 얼룩 · 분홍 큰 주둥이 · 작은 뿔
    h = head(50, 58, 28, 26); Mc = '#f8f6f0'
    A[14] = (P('M36 36 Q30 26 34 20 Q40 26 42 34 Z', '#efe3c4', SW) + P('M64 36 Q70 26 66 20 Q60 26 58 34 Z', '#efe3c4', SW)
             + side_ear(22, 46, 11, 6, 20, Mc, PINK) + side_ear(78, 46, 11, 6, -20, '#4a4040') + P(h, Mc, SW)
             + clip(h, '<path d="M60 30 Q80 34 80 52 Q70 54 62 46 Q56 40 60 30 Z" fill="#4a4040"/><ellipse cx="32" cy="40" rx="7" ry="5" fill="#4a4040"/>', 14)
             + eyes(54, 11) + E(50, 74, 18, 10, '#f2b8b0', SW) + C(44, 74, 2.2, '#b07a72', 0) + C(56, 74, 2.2, '#b07a72', 0))
    # 15 당나귀 — 회색 · 긴 귀(끝 짙음) · 밝은 주둥이 · 앞머리
    face = 'M32 52 Q32 34 50 34 Q68 34 68 52 Q68 84 50 86 Q32 84 32 52 Z'; Dk = '#b4aca2'
    A[15] = (ear(40, 40, 7, 30, -18, Dk, '#d8d0c6') + ear(60, 40, 7, 30, 18, Dk, '#d8d0c6') + P(face, Dk, SW)
             + clip(face, muzzle(78, 17, 12, '#ebe5dc'), 15) + P('M42 36 Q46 26 50 34 Q54 26 58 36 Q50 42 42 36 Z', '#6e6258', SW * .8)
             + eyes(56, 10) + C(44, 76, 2, '#6e6258', 0) + C(56, 76, 2, '#6e6258', 0) + L('M45 82 Q50 85 55 82', 2.4))
    return A


def main():
    os.makedirs(OUT, exist_ok=True)
    tiles = []
    S = 256
    for k, svg in animals().items():
        im = render(f'<g transform="scale({S / 100})">{svg}</g>', S, S, wob=1.2, freq=0.04)
        n = 'friend_%02d' % k
        Image.fromarray(im).save(os.path.join(OUT, n + '.png'))
        tiles.append(Image.fromarray(im))
        print(n, NAMES[k - 1])
    T = 200; cols = 5
    pv = Image.new('RGBA', (cols * T, 3 * T), (236, 220, 184, 255))
    for i, t in enumerate(tiles):
        t2 = t.resize((T - 16, T - 16), Image.LANCZOS)
        pv.alpha_composite(t2, ((i % cols) * T + 8, (i // cols) * T + 8))
    os.makedirs(PREV, exist_ok=True)
    pv.save(os.path.join(PREV, 'friend_icons.png'))


if __name__ == '__main__':
    main()
