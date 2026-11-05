# -*- coding: utf-8 -*-
"""밝은 낙서풍(11-05 화풍 샘플) 게임 그림 만들기.

벡터(SVG)로 그린 뒤 Edge 헤드리스로 투명 PNG 를 뽑아, 지금 게임 그림과 같은 칸 크기·발 위치·총구 위치로 저장한다.
→ 게임 코드는 그대로, 그림만 바뀜. 뼈대는 그 다음 tools/rig_gen.py 로 다시 심음(ZOMBIE_RIG · CRAWL_RIG · CAPY_BY_SKIN).

사용: PYTHONUTF8=1 python tools/doodle_art.py [capy] [skins] [zombie] [map] [icon]     (없으면 capy·zombie·map) — PowerShell 에서(Bash 에선 Edge 가 안 뜸)
      → Kachi_Gun/Assets/Resources/Art/{Char,Zombie,Map}/...  · 미리보기 tools/_preview/doodle_*.png
원래 도트 그림 백업: tools/_build/pixel_orig/ (git 에는 원래 그림이 기록에 있음)

그림 규칙: 선 #3b281c · 굵은 선 + 단색 · 살짝 흔들리는 선(feTurbulence) · 파스텔(쨍한 색 피함)
"""
import os, sys, subprocess, tempfile, shutil, math
import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
ART = os.path.join(ROOT, 'Kachi_Gun', 'Assets', 'Resources', 'Art')
PREV = os.path.join(os.path.dirname(__file__), '_preview')
EDGE = r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
INK = '#3b281c'


# ------------------------------------------------------------------ SVG → 투명 PNG
WOB_K = 0.5     # 11-05 테두리 매끈하게: 흔들림 절반 (사용자 "테두리를 조금 매끈하게")
SS = 2          # 2배로 그려 줄임 = 가장자리 부드럽게 (흔들림 효과가 가장자리를 계단처럼 깨뜨려서)


def render(svg_body, w, h, wob=3.0, freq=0.022, ss=None):
    """svg_body(그림 조각들)를 w x h 투명 PNG(np RGBA)로. wob = 손그림 흔들림 세기(px, WOB_K 를 곱함) · ss = 몇 배로 그려 줄일지"""
    ss = ss or SS
    wob = wob * WOB_K
    tmp = tempfile.mkdtemp(prefix='doodle_')
    try:
        svg = f'''<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}">
<defs><filter id="wob" filterUnits="userSpaceOnUse" x="0" y="0" width="{w}" height="{h}">
<feTurbulence type="fractalNoise" baseFrequency="{freq}" numOctaves="2" seed="7"/>
<feDisplacementMap in="SourceGraphic" scale="{wob}"/></filter></defs>
<g filter="url(#wob)">{svg_body}</g></svg>'''
        html = f'<!doctype html><html><head><meta charset="utf-8"><style>html,body{{margin:0;background:transparent;overflow:hidden}}</style></head><body>{svg}</body></html>'
        hp = os.path.join(tmp, 'a.html'); open(hp, 'w', encoding='utf-8').write(html)
        out = os.path.join(tmp, 'a.png')
        url = 'file:///' + hp.replace('\\', '/')
        subprocess.run([EDGE, '--headless=new', '--disable-gpu', '--hide-scrollbars', '--default-background-color=00000000',
                        f'--window-size={w},{h}', f'--force-device-scale-factor={ss}', '--virtual-time-budget=2000',
                        f'--user-data-dir={os.path.join(tmp, "prof")}', f'--screenshot={out}', url],
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=120)
        big = Image.open(out).convert('RGBA').crop((0, 0, w * ss, h * ss))
        if ss != 1:   # 미리 곱한 알파로 줄여야 가장자리에 검은 테가 안 생김
            big = big.convert('RGBa').resize((w, h), Image.LANCZOS).convert('RGBA')
        return np.array(big)
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


# ------------------------------------------------------------------ 그리기 도구
def P(d, fill, sw=6.0, extra=''):
    return f'<path d="{d}" fill="{fill}" stroke="{INK}" stroke-width="{sw}" stroke-linejoin="round" stroke-linecap="round" {extra}/>'


def L(d, sw=6.0, color=INK):
    return f'<path d="{d}" fill="none" stroke="{color}" stroke-width="{sw}" stroke-linejoin="round" stroke-linecap="round"/>'


def C(cx, cy, r, fill, sw=6.0):
    return f'<circle cx="{cx}" cy="{cy}" r="{r}" fill="{fill}" stroke="{INK}" stroke-width="{sw}"/>'


def E(cx, cy, rx, ry, fill, sw=6.0, rot=0):
    t = f' transform="rotate({rot} {cx} {cy})"' if rot else ''
    return f'<ellipse cx="{cx}" cy="{cy}" rx="{rx}" ry="{ry}" fill="{fill}" stroke="{INK}" stroke-width="{sw}"{t}/>'


def F(d, fill):
    """테두리 없는 칠"""
    return f'<path d="{d}" fill="{fill}" stroke="none"/>'


def limb(d, w, fill, sw=6.0):
    """굵은 막대(팔·다리): 테두리 굵은 선 위에 칠 선"""
    return (f'<path d="{d}" fill="none" stroke="{INK}" stroke-width="{w + 2 * sw}" stroke-linecap="round" stroke-linejoin="round"/>'
            f'<path d="{d}" fill="none" stroke="{fill}" stroke-width="{w}" stroke-linecap="round" stroke-linejoin="round"/>')


def zig(x0, x1, y, n, amp):
    """찢어진 옷단: x1 → x0 로 지그재그"""
    pts = []
    for i in range(n + 1):
        x = x1 - (x1 - x0) * i / n
        pts.append(f'L{x:.1f} {y + (amp if i % 2 else 0):.1f}')
    return ' '.join(pts)


# ------------------------------------------------------------------ 카피바라 (기본 스킨 0)
# 샘플 좌표(로컬) → 칸(360) : X = 41 + x*1.65, Y = 74 + y*1.65   (발 가운데 x 72 → 칸 160, 발바닥 → 바닥 8px 위)
CS, CX0, CY0 = 1.65, 41.0, 74.0
CAPY_BODY = '#c49a6e'; CAPY_DARK = '#a97f58'; CAPY_BELLY = '#d9b88f'; CAPY_FAR = '#b9906a'
# 11-05 카피바라 선은 더 진하고 굵고 덜 흔들리게 (사용자 "캐릭터 선을 조금 더 진하고 선명하게")
CAPY_INK, CAPY_SW, CAPY_WOB, GUN_WOB = '#1c110a', 4.4, 1.0, 0.8


# 11-05 두상 = 추가 요청 5번 (눈 높이~턱 앞쪽 윤곽을 5번 그림에서 따옴, 위·뒤통수·몸은 그대로)
BODY_D = 'M38 156 C20 144 18 112 26 92 C30 74 32 52 42 36 C52 22 70 14 90 15 C108 16 121 24 127 38 C127.6 38.3 129.7 39.0 130.7 40.0 C131.7 41.0 132.3 42.7 133.0 44.0 C133.7 45.3 134.2 46.7 134.6 48.0 C135.0 49.3 135.4 50.7 135.7 52.0 C136.0 53.3 136.1 54.7 136.2 56.0 C136.3 57.3 136.2 58.7 136.2 60.0 C136.1 61.3 136.0 62.7 135.9 64.0 C135.8 65.3 135.9 66.7 135.7 68.0 C135.5 69.3 135.0 70.7 134.6 72.0 C134.2 73.3 133.8 74.7 133.0 76.0 C132.2 77.3 131.4 78.7 130.1 80.0 C128.8 81.3 126.7 82.8 125.1 84.0 C123.5 85.2 121.7 86.5 120.5 87.3 C119.3 88.1 118.4 88.7 118.0 89.0 C125 108 122 138 106 154 C92 166 54 166 38 156 Z'
SNOUT_D = 'M116 43 C108 44.5 104.5 54 105.5 64 C106.5 74 110.5 80 119 80.5 C128 80.5 134 74 135 64 C136 52 128 42 116 43 Z'


def capy_body_svg(sw=3.2, cos=None):
    """몸(총·손 빼고) — 로컬 좌표. 오른팔은 어깨~팔뚝 가운데까지(나머지 팔뚝·손은 총 그림에)
    cos(스킨 의상): body·dark·belly 색, back(몸 뒤), front(몸 위), top(맨 위), onesie(잠옷: 몸 전체 의상색 + 얼굴 구멍), ears(귀 대신)"""
    cos = cos or {}
    body = cos.get('body', CAPY_BODY); dark = cos.get('dark', CAPY_DARK); belly = cos.get('belly', CAPY_BELLY)
    one = cos.get('onesie')
    ears = cos.get('ears')
    if ears is None:
        ears = P('M50 32 Q44 20 54 18 Q60 20 59 28', dark, sw) + P('M78 18 Q78 6 88 7 Q93 10 90 17', dark, sw)
    snout = f'<clipPath id="snClip"><path d="{BODY_D}"/></clipPath><g clip-path="url(#snClip)">' + F(SNOUT_D, CAPY_DARK) + '</g>'
    face = ''
    if one:   # 잠옷: 얼굴 쪽만 카피바라 색 (몸 모양 안으로 자름) + 모자 테두리
        face = (f'<clipPath id="bodyClip"><path d="{BODY_D}"/></clipPath><g clip-path="url(#bodyClip)">'
                f'<ellipse cx="106" cy="56" rx="33" ry="31" fill="{CAPY_BODY}" stroke="{INK}" stroke-width="{sw}"/>'
                + snout + '</g>'
                + f'<path d="{BODY_D}" fill="none" stroke="{INK}" stroke-width="{sw}" stroke-linejoin="round"/>')
    ex = 8 if one else 0      # 잠옷은 얼굴 구멍 안으로 눈을 옮김
    eyes = f'<circle cx="{83.3 + ex}" cy="40.2" r="2.7" fill="{INK}"/>'
    if cos.get('angry'):      # 4번: 눈썹 + 찡그린 코 + 처진 입
        eyes += L(f'M{81.7 + ex} 30 L{91.8 + ex} 35.6', 2.8)
    blush = ''

    return ''.join([
        cos.get('back', ''),
        E(90, 161, 14, 7.5, dark, sw),
        E(54, 160, 14, 7.5, dark, sw),
        ears if one else '',
        P(BODY_D, body, sw),
        f'<ellipse cx="72" cy="126" rx="27" ry="29" fill="{belly}"/>',
        face if one else snout,
        '' if one else ears,
        face_marks(cos.get('angry')),
        eyes, blush,
        cos.get('front', ''),
        # 오른팔 전체 (11-05: 몸 그림에 둠 — 총 그림엔 손만, 손이 팔 끝을 덮음)
        F('M36 88 Q40 104 48 111 Q55 117 63 116 Q75 115 79 109 L79 97 Q76 91 69 90 Q62 88 57 82 Z', cos.get('arm', body))
        + L('M36 88 Q40 104 48 111 Q55 117 63 116 Q75 115 79 109', sw) + L('M57 82 Q59 88 65 90 Q69 91 70 94', sw),
        cos.get('top', ''),
    ])


def face_marks(angry=False):
    """주둥이의 코·입 (3번 = Y자 코 + ㅅ 입, 4번 = 찡그린 코 + 처진 입)"""
    if angry:
        return L('M116.5 46 L120.5 48.8 M128.1 47.3 L131.6 45.3', 2.6) + L('M124 50 L124 69.5', 2.4) + L('M117.4 74.5 L124 70.5 L127.6 73.5', 2.6)
    return L('M117 50.7 L122.8 56 L129.4 50.7 M122.8 56 L122.8 69.1', 2.4) + L('M118.1 73.6 L122.8 69.1 L126.7 73', 2.6)


def _tri(x, y, w, h, fill, sw=3.2):
    return P(f'M{x} {y} L{x + w / 2} {y - h} L{x + w} {y} Z', fill, sw)


def rr_(x, y, w, h, r, fill, sw=3.2):
    return f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{r}" fill="{fill}" stroke="{INK}" stroke-width="{sw}"/>'


def _teeth(x0, x1, y):
    return L(' '.join(f'M{x} {y} l3 5 l3 -5' for x in range(x0, x1, 6)), 2.2)


def costumes():
    """스킨 1~17 의상 (Skin_Table 이름 순서)"""
    sw = CAPY_SW
    C_ = {}
    # 1 밀짚모자
    C_[1] = dict(top=P('M30 24 Q86 6 140 22 Q144 30 132 30 Q86 20 38 34 Q24 32 30 24 Z', '#f0c86a', sw)
                 + P('M58 24 Q60 0 86 -2 Q112 0 114 20 Q86 14 58 24 Z', '#f0c86a', sw) + L('M60 20 Q86 12 113 16', 3.4, '#d0603e'))
    # 2 천사 날개
    C_[2] = dict(back=P('M30 70 Q-14 40 -20 74 Q-14 86 0 86 Q-16 96 -8 108 Q6 112 14 104 Q4 118 18 124 Q32 118 32 100 Z', '#fffaf0', sw)
                 + L('M0 86 Q12 84 22 82 M14 104 Q22 98 28 94', 2.4),
                 top=f'<ellipse cx="86" cy="2" rx="24" ry="6.5" fill="none" stroke="{INK}" stroke-width="7"/>'
                 f'<ellipse cx="86" cy="2" rx="24" ry="6.5" fill="none" stroke="#ffd34d" stroke-width="3.6"/>')
    # 3 새싹
    C_[3] = dict(top=L('M88 16 Q86 4 90 -6', 3.2) + P('M90 -4 Q104 -18 114 -8 Q104 4 90 -4 Z', '#8fcf6a', sw)
                 + P('M88 -2 Q72 -16 64 -6 Q74 4 88 -2 Z', '#8fcf6a', sw))
    # 4 꿀꿀 돼지 (잠옷)
    C_[4] = dict(onesie=1, body='#f2b8c0', dark='#e597a3', belly='#f8d3d8', paw='#f2b8c0',
                 ears=P('M44 36 L40 12 L62 26 Z', '#e597a3', sw) + P('M76 18 L82 -4 L98 16 Z', '#e597a3', sw),
                 back=L('M24 128 q-10 -2 -8 -10 q6 -6 10 2 q2 8 -8 8', 3.2))
    # 5 악어 잠옷
    C_[5] = dict(onesie=1, body='#86bf6a', dark='#6aa452', belly='#cfe6a8', paw='#86bf6a',
                 ears=C(60, 22, 7, '#86bf6a', sw) + C(84, 14, 7, '#86bf6a', sw)
                 + f'<circle cx="60" cy="21" r="2.4" fill="{INK}"/><circle cx="84" cy="13" r="2.4" fill="{INK}"/>',
                 top=_teeth(80, 128, 25))
    # 6 원시인
    C_[6] = dict(front=P('M24 100 L106 80 L118 102 Q122 138 106 154 Q84 166 52 162 Q32 156 26 132 Z', '#e0a050', sw)
                 + ''.join(f'<ellipse cx="{x}" cy="{y}" rx="4.5" ry="3" fill="#9a5e2a"/>' for x, y in ((48, 112), (72, 104), (96, 98), (60, 132), (86, 126), (104, 120), (52, 150), (80, 150))),
                 top=L('M46 32 Q86 16 126 30', 7, INK) + L('M46 32 Q86 16 126 30', 3.6, '#d0603e'))
    # 7 검은 두건
    C_[7] = dict(top=P('M38 42 Q46 12 90 11 Q122 12 130 40 Q92 26 40 48 Z', '#3a3440', sw)
                 + P('M40 40 Q24 34 18 44 Q28 48 38 46 Z', '#3a3440', sw) + P('M40 44 Q26 52 26 62 Q36 58 42 48 Z', '#3a3440', sw))
    # 8 그림자 망토
    C_[8] = dict(back=P('M44 74 Q10 100 4 168 L70 168 Q40 120 60 76 Z', '#4a4458', sw),
                 front=P('M40 78 Q70 94 104 82 L100 92 Q70 104 42 88 Z', '#4a4458', sw),
                 top=P('M40 38 Q48 6 90 6 Q126 8 132 40 Q118 22 90 20 Q58 22 40 38 Z', '#4a4458', sw))
    # 9 공룡 잠옷 (등 가시)
    spikes = ''.join(_tri(x, y, 14, 14, '#f2a35a') for x, y in ((78, 16), (56, 20), (36, 36), (22, 60), (16, 88)))
    C_[9] = dict(onesie=1, body='#8fcf7a', dark='#73b25e', belly='#d6eeb4', paw='#8fcf7a', ears='', back=spikes)
    # 10 모험가 배낭
    C_[10] = dict(back=P('M-2 76 Q-4 64 10 64 L34 64 Q42 66 40 78 L38 136 Q36 142 28 142 L4 142 Q-4 142 -4 134 Z', '#b07a48', sw)
                  + P('M-2 76 Q14 70 40 78 L38 96 Q16 94 -2 98 Z', '#c99060', sw) + rr_(12, 108, 16, 12, 3, '#ffd34d', 2.4),
                  front=L('M40 76 Q50 98 46 128', 6.4, INK) + L('M40 76 Q50 98 46 128', 3, '#8a5a36'))
    # 11 박쥐 날개
    C_[11] = dict(back=P('M34 68 Q4 30 -22 54 Q-10 62 -6 74 Q-18 78 -12 92 Q0 88 6 96 Q4 108 16 108 Q22 92 34 92 Z', '#5d4a7a', sw)
                  + L('M32 72 L-4 60 M30 80 L-4 82 M30 88 L8 100', 2.4))
    # 12 멜빵바지
    C_[12] = dict(front=P('M28 116 Q72 108 118 112 Q122 140 106 154 Q84 166 52 162 Q32 156 26 132 Z', '#6f8fc8', sw)
                  + P('M52 92 L94 88 L96 114 L54 118 Z', '#6f8fc8', sw) + L('M54 94 L42 70 M92 90 L102 72', 6, INK)
                  + L('M54 94 L42 70 M92 90 L102 72', 2.6, '#6f8fc8') + C(56, 96, 3.4, '#ffd34d', 2) + C(91, 92, 3.4, '#ffd34d', 2))
    # 13 꼬꼬 잠옷
    C_[13] = dict(onesie=1, body='#fff6e6', dark='#ecdcc0', belly='#ffffff', paw='#fff6e6', ears='',
                  top=P('M76 16 Q74 -2 84 2 Q88 -8 96 0 Q104 -6 106 6 Q108 14 100 18 Z', '#e8735f', sw)
                  + P('M128 34 L140 38 L128 42 Z', '#ffcf3a', 2.6))
    # 14 빨간 망토
    C_[14] = dict(back=P('M44 74 Q12 104 10 166 L64 166 Q38 120 58 78 Z', '#d9604e', sw),
                  front=P('M44 80 Q70 94 104 84 L100 92 Q70 102 46 88 Z', '#d9604e', sw)
                  + P('M70 92 L62 104 L74 100 L82 108 L80 94 Z', '#d9604e', sw))
    # 15 상어 잠옷 (등지느러미)
    C_[15] = dict(onesie=1, body='#8fb8d8', dark='#7299bb', belly='#e6f0f8', paw='#8fb8d8', ears='',
                  back=P('M58 22 Q60 -6 84 -12 Q76 4 82 16 Z', '#8fb8d8', sw), top=_teeth(80, 128, 25))
    # 16 줄무늬 고양이 (잠옷)
    C_[16] = dict(onesie=1, body='#f0b26a', dark='#d8964e', belly='#f8dcb4', paw='#f0b26a',
                  ears=P('M42 38 L44 8 L66 24 Z', '#f0b26a', sw) + P('M76 18 L90 -8 L102 16 Z', '#f0b26a', sw),
                  back=L('M24 140 Q0 140 2 116 Q4 100 14 104', 7.4, INK) + L('M24 140 Q0 140 2 116 Q4 100 14 104', 3.4, '#f0b26a'),
                  front=L('M30 70 l14 4 M24 96 l16 2 M26 122 l16 0', 4, '#c47a3a'))
    # 17 해적 선장
    C_[17] = dict(top=P('M30 30 Q40 2 86 0 Q132 2 142 30 Q120 22 112 26 Q86 12 60 26 Q50 22 30 30 Z', '#3a3440', sw)
                  + C(86, 14, 6, '#fff4dc', 2.2) + L('M83 13 h2 M88 13 h2', 1.6)
                  + f'<ellipse cx="84" cy="40" rx="8" ry="7" fill="{INK}"/>' + L('M78 35 L60 26 M90 36 L126 30', 2.4),
                  front=P('M24 96 Q60 84 112 86 Q122 120 106 154 Q84 166 52 162 Q30 156 26 132 Z', '#c75a4a', sw)
                  + L('M60 90 L58 160', 2.4) + C(64, 110, 3, '#ffd34d', 1.6) + C(64, 130, 3, '#ffd34d', 1.6))
    return C_


# 총 그림 260x130, 회전 중심(손목) = (90,55), 총구 = 중심 + (137.6, 위 17.6) — Art.GunMuzzle 그대로 쓰려고 맞춤
#   로컬 총구 (162, 98) · 회전 중심 로컬 (78.6, 108.7)
GPX, GPY = 78.6, 108.7
# 화난 얼굴 조각 = 칸(360) 안 이 사각형 (x0, y0, x1, y1) — 게임은 머리 뼈에 붙여 좀비가 가까우면 켬 (Art.FaceRect 와 같게)
FACE_RECT = (140, 110, 290, 216)


def capy_gun_svg(sw=3.2, paw=CAPY_BODY, far=CAPY_FAR):
    return ''.join([
        '<clipPath id="stockTip"><rect x="0" y="0" width="41" height="200"/></clipPath>',   # 개머리판은 팔 밖으로 나온 끝만 (나머지는 팔 뒤)
        '<g clip-path="url(#stockTip)">' + P('M34 98 L60 94 L64 108 L40 112 Q32 110 34 98 Z', '#8a5a36', sw) + '</g>',
        f'<rect x="66" y="92" width="34" height="13" rx="3" fill="#5d6470" stroke="{INK}" stroke-width="{sw}"/>',
        f'<rect x="96" y="94" width="66" height="8" rx="3" fill="#7b8592" stroke="{INK}" stroke-width="{sw}"/>',
        f'<rect x="98" y="102" width="42" height="6" rx="2.5" fill="#8a5a36" stroke="{INK}" stroke-width="{sw}"/>',
        P('M64 90 Q70 88 74 92 Q75 96 71 98 Q67 98 66 95', paw, sw),                  # 엄지
        P('M60 106 Q68 104 78 106 Q80 112 74 114 Q66 116 60 112 Z', paw, sw),          # 손가락
        L('M65 108 q3 1 5 0 M66 111.5 q3 1 5 0', 2.0),
        L('M117 116 Q118 122 119 128', sw),                                           # 왼손목
        P('M111 104 Q111 99 116 99 L130 99 Q135 100 135 106 Q135 116 124 117 Q112 117 111 108 Z', far, sw),
        L('M117 101 v6 M122 100.5 v6.5 M127 101 v6', 2.0),
    ])


def make_capy(skin=0):
    cos = costumes().get(skin, {}) if skin else {}
    ink = lambda svg: svg.replace(INK, CAPY_INK)
    body = render(ink(f'<g transform="translate({CX0},{CY0}) scale({CS})">{capy_body_svg(CAPY_SW, cos=cos)}</g>'), 360, 360, wob=CAPY_WOB)
    angry = render(ink(f'<g transform="translate({CX0},{CY0}) scale({CS})">{capy_body_svg(CAPY_SW, cos=dict(cos, angry=1))}</g>'), 360, 360, wob=CAPY_WOB)
    x0, y0, x1, y1 = FACE_RECT
    diff = np.abs(body.astype(int) - angry.astype(int)).sum(axis=2) > 24
    outside = diff.copy(); outside[y0:y1, x0:x1] = False
    if outside.sum() > 40:
        print('  주의: 화난 얼굴이 조각 밖에서도 다름', int(outside.sum()))
    Image.fromarray(angry[y0:y1, x0:x1]).save(os.path.join(ART, 'Char', 'face_angry_%02d.png' % skin))
    gx, gy = 90 - GPX * CS, 55 - GPY * CS
    paw = cos.get('paw', CAPY_BODY)
    far = shade(paw, .94) if 'paw' in cos else CAPY_FAR
    gun = render(ink(f'<g transform="translate({gx:.2f},{gy:.2f}) scale({CS})">{capy_gun_svg(CAPY_SW, paw=paw, far=far)}</g>'), 260, 130, wob=GUN_WOB)
    # 시트 16칸: 0~11 = 같은 몸 (뼈대가 움직임), 12~15 = 쓰러짐(옆으로 눕힘)
    sheet = Image.new('RGBA', (1440, 1440), (0, 0, 0, 0))
    b = Image.fromarray(body)
    lying = b.rotate(90, resample=Image.BICUBIC, expand=False, center=(160, 300))
    ys, xs = np.nonzero(np.array(lying)[..., 3] > 0)
    lying = lying.transform(lying.size, Image.AFFINE, (1, 0, 0, 0, 1, -(352 - ys.max())), resample=Image.BICUBIC) if len(ys) else lying
    for i in range(16):
        sheet.alpha_composite(lying if i >= 12 else b, ((i % 4) * 360, (i // 4) * 360))
    sheet.save(os.path.join(ART, 'Char', 'skin_%02d.png' % skin))
    Image.fromarray(gun).save(os.path.join(ART, 'Char', 'gun_%02d.png' % skin))
    # 총 회전 중심 자리(칸 픽셀, 위가 0): 8칸 모두 같음
    px, py = CX0 + GPX * CS, CY0 + GPY * CS
    gp = os.path.join(ART, 'Char', 'gun_pos.txt')
    lines = open(gp, encoding='utf-8').read().splitlines()
    rec = 'skin_%02d ' % skin + ' '.join(['%d,%d' % (round(px), round(py))] * 8)
    lines = [rec if ln.startswith('skin_%02d ' % skin) else ln for ln in lines]
    open(gp, 'w', encoding='utf-8', newline='\n').write('\n'.join(lines) + '\n')
    # 미리보기: 몸 + 총(쉬는 자세)
    pv = Image.new('RGBA', (360, 360), (233, 220, 188, 255))
    pv.alpha_composite(b)
    g = Image.fromarray(gun)
    pv.alpha_composite(g, (int(round(px - 90)), int(round(py - 55))))
    os.makedirs(PREV, exist_ok=True)
    pv.save(os.path.join(PREV, 'doodle_capy%02d.png' % skin))
    print('capy', skin, 'gun pivot', round(px), round(py))


# ------------------------------------------------------------------ 좀비 (칸 400x380, 오른쪽을 봄, 발바닥 = y 376)
SKIN = '#8cc56a'


def zhead(cx, cy, r, skin, eyes='zombie', sw=6.0):
    """둥근 좀비 머리 (오른쪽을 봄)"""
    out = [E(cx, cy, r * 1.06, r, skin, sw)]
    if eyes == 'zombie':
        out += [L(f'M{cx - r * .35} {cy - r * .78} l{r * .2} {r * .16} M{cx - r * .32} {cy - r * .66} l{r * .08} {-r * .08} M{cx - r * .22} {cy - r * .6} l{r * .08} {-r * .08}', sw * .7)]
        out += [C(cx + r * .2, cy - r * .05, r * .27, '#fffbe8', sw * .8), C(cx + r * .66, cy - r * .1, r * .23, '#fffbe8', sw * .8),
                f'<circle cx="{cx + r * .29}" cy="{cy - r * .02}" r="{r * .1}" fill="{INK}"/>',
                f'<circle cx="{cx + r * .73}" cy="{cy - r * .07}" r="{r * .09}" fill="{INK}"/>']
        mx, my = cx + r * .45, cy + r * .5
        out += [P(f'M{mx - r * .3} {my - r * .04} Q{mx} {my + r * .26} {mx + r * .32} {my - r * .08} Q{mx} {my + r * .06} {mx - r * .3} {my - r * .04} Z', '#5a2b2b', sw * .8),
                f'<path d="M{mx - r * .14} {my} l{r * .06} {r * .1} l{r * .06} {-r * .1} M{mx + r * .06} {my} l{r * .06} {r * .1} l{r * .06} {-r * .1}" fill="#fff" stroke="none"/>']
        out += [f'<ellipse cx="{cx - r * .35}" cy="{cy + r * .38}" rx="{r * .16}" ry="{r * .1}" fill="#000" opacity=".12"/>']
    return ''.join(out)


def walker(skin=SKIN, skin2=None, shirt='#c98a5a', pants='#5a6ea6', shoes='#4a3628', head_extra='', hand_item_back='',
           hand_item='', torso_extra='', head='zombie', hr=54, wide=0, arms=True, belly=False):
    """걷는 좀비 기본 몸. 뼈대 기준: split 200 · hip 300 · crotch 308 · knee 336 · neck 242 · 팔 = 앞으로 뻗음"""
    skin2 = skin2 or shade(skin, .9)
    w = wide
    out = []
    if arms:   # 뒷팔 (몸 뒤, 조금 어둡게)
        out.append(limb('M226 247 Q262 244 298 244', 17, skin2))
        out.append(C(302, 244, 10, skin2))
        out.append(hand_item_back)
    # 다리 · 신발
    out.append(P('M176 296 L174 364 L196 364 L198 296 Z', shade(pants, .92)))
    out.append(P('M204 296 L210 364 L232 364 L228 296 Z', pants))
    out.append(P('M168 360 Q168 378 182 378 L200 378 Q202 368 196 360 Z', shoes))
    out.append(P('M206 360 Q206 378 220 378 L244 378 Q248 368 236 360 Z', shoes))
    # 몸통 (찢어진 셔츠)
    if belly:
        out.append(P(f'M{160 - w} 250 Q{200} 226 {240 + w} 250 Q{262 + w} 286 {244 + w} 312 ' + zig(160 - w, 244 + w, 312, 8, 7) + f' Q{140 - w} 286 {160 - w} 250 Z', shirt))
    else:
        out.append(P(f'M{168 - w} 246 Q{202} 232 {238 + w} 246 L{240 + w} 308 ' + zig(166 - w, 240 + w, 308, 8, 8) + ' Z', shirt))
    out.append(torso_extra)
    out.append(zhead(204, 188, hr, skin, head))
    out.append(head_extra)
    if arms:   # 앞팔
        out.append(limb('M218 266 Q262 262 304 264', 19, skin))
        out.append(C(309, 265, 11, skin))
        out.append(hand_item)
    return ''.join(out)


def shade(hexc, k):
    h = hexc.lstrip('#')
    r, g, b = (int(h[i:i + 2], 16) for i in (0, 2, 4))
    return '#%02x%02x%02x' % tuple(max(0, min(255, int(v * k))) for v in (r, g, b))


def crawler(skin=SKIN, shirt='#c98a5a', pants='#6a5aa0', head_extra='', head_dy=0):
    """기어 오는 좀비 (몸이 가로): 다리 x 30~125 · 몸 120~205 · 머리 오른쪽 · 팔 두 개 아래로 뻗어 땅 짚음
    뼈대 기준: hip (125,318) · legs_x 118 · neck_x 182 · arm_top 318 · 뒷팔 어깨 (195,320) · 앞팔 어깨 (238,320)"""
    out = [
        limb('M200 322 Q210 350 222 366', 15, shade(skin, .9)), C(224, 368, 9, shade(skin, .9)),
        P('M40 338 Q36 360 50 368 L120 366 L128 330 Q80 324 40 338 Z', pants),
        P('M30 350 Q24 372 40 376 L58 376 Q62 362 56 352 Z', '#4a3628'),
        P('M116 322 Q160 300 206 318 L208 356 ' + zig(118, 208, 356, 6, 6) + ' Z', shirt),
        zhead(232 + 0, 300 + head_dy, 44, skin), head_extra,
        limb('M238 330 Q262 352 282 368', 16, skin), C(286, 370, 10, skin),
    ]
    return ''.join(out)


def zombies():
    Z = {}
    Z['basic'] = walker()
    Z['knife'] = walker(skin='#a6cf72', shirt='#7f9fbf',
                        hand_item=P('M312 256 L346 206 L356 214 L322 264 Z', '#d9dee6') + P('M304 258 L318 268 L314 274 L300 264 Z', '#6b4a32'))
    Z['hat'] = walker(skin='#93c66c', shirt='#b5795a', pants='#4f6290',
                      head_extra=P('M142 150 Q204 132 268 146 Q272 156 262 158 Q204 146 148 162 Q136 160 142 150 Z', '#8a5a36') + P('M166 148 Q170 104 204 100 Q240 102 244 144 Q204 138 166 148 Z', '#9a6a44') + F('M168 138 Q204 128 242 134 L243 142 Q204 134 168 146 Z', '#5d4030'))
    Z['bat'] = walker(skin='#86bf74', shirt='#d39b62', pants='#4f6290',
                      hand_item=P('M300 270 L322 150 Q330 128 346 134 Q358 142 352 162 L316 274 Z', '#c08a52') + L('M306 246 L312 250 M310 232 L316 236', 3.0))
    Z['farmer'] = walker(skin='#9ccc74', shirt='#c46a5a', pants='#5a7ab0',
                         torso_extra=P('M178 258 L178 306 L226 306 L226 258 Z', '#5a7ab0') + L('M180 258 L172 244 M224 258 L232 244', 5) + C(184, 268, 3.5, '#ffd34d', 2) + C(220, 268, 3.5, '#ffd34d', 2),
                         head_extra=P('M128 156 Q204 126 282 152 Q286 164 270 164 Q204 146 140 170 Q120 168 128 156 Z', '#f0c86a') + P('M164 150 Q168 110 204 106 Q240 108 244 146 Q204 138 164 150 Z', '#f0c86a') + L('M166 142 Q204 132 242 138', 5, '#c0663e'),
                         hand_item=L('M316 330 L332 150', 7, INK) + L('M316 330 L332 150', 3.5, '#9a6a44') + P('M318 156 L346 146 L344 136 M326 152 L322 118 M334 150 L338 118 M342 148 L352 120', 'none', 5))
    Z['mummy'] = walker(skin='#e9dfc6', skin2='#d8ccb0', shirt='#e9dfc6', pants='#e2d6ba', shoes='#cfc2a2',
                        torso_extra=L('M170 262 L236 254 M170 280 L238 272 M172 298 L238 290', 3, '#b9ab8c'),
                        head_extra=L('M160 170 Q204 158 254 166 M152 200 Q204 190 258 198 M164 228 Q204 220 246 226', 3, '#b9ab8c'),
                        head='zombie')
    Z['armor'] = walker(skin='#94b86a', shirt='#9aa3ad', pants='#59606b', shoes='#3f3a36', wide=12, hr=50,
                        torso_extra=P('M176 270 L228 266 L230 300 L178 304 Z', '#b9c1ca') + E(160, 252, 22, 16, '#7d8792') + E(244, 250, 20, 15, '#7d8792') + P('M162 300 L242 296 L242 308 L162 312 Z', '#7a5536'),
                        hand_item=C(309, 265, 14, '#7d8792'), hand_item_back=C(302, 244, 12, '#6b747e'))
    Z['fat'] = walker(skin='#9cc27a', shirt='#9cc27a', pants='#7a5a40', wide=24, hr=46, belly=True,
                      torso_extra=E(204, 286, 34, 26, '#b4d494', 0) + L('M202 288 q4 4 8 0', 3.5))
    Z['reaper'] = reaper()
    Z['crawler'] = crawler()
    Z['bucket'] = crawler(skin='#a6cf72', shirt='#7f9fbf', pants='#4f6290',
                          head_extra=P('M192 280 L198 236 L268 236 L274 280 Z', '#a9b3be') + E(233, 236, 35, 7, '#c9d1da') + L('M204 260 L262 260', 3, '#7d8792'))
    Z['ogre'] = ogre()
    return Z


def reaper():
    """사신: 후드 달린 긴 망토(다리 없음) · 해골 얼굴 · 낫. 뼈대 robe — neck 236 · hip 300 · knee 340"""
    return ''.join([
        L('M296 372 L330 132', 8, INK), L('M296 372 L330 132', 4.5, '#8a5a36'),
        P('M330 132 Q290 118 262 150 Q300 140 326 150 Z', '#d9dee6'),
        P('M150 376 Q156 300 160 250 Q160 196 182 160 Q204 130 230 134 Q262 140 268 182 Q274 230 262 270 Q262 330 274 376 Z', '#4a4458'),
        P('M184 182 Q186 150 214 148 Q246 150 250 184 Q252 222 216 230 Q186 228 184 182 Z', '#2c2836'),
        P('M196 186 Q198 162 218 162 Q240 164 240 188 Q238 214 218 216 Q198 214 196 186 Z', '#e6e0cf', 4.5),
        f'<circle cx="212" cy="186" r="6" fill="{INK}"/><circle cx="230" cy="185" r="5.5" fill="{INK}"/>',
        L('M212 204 h18 M216 200 v8 M222 200 v8 M228 200 v8', 2.5),
        L('M168 300 Q210 290 262 300', 4, '#3a3446'),
        limb('M236 262 Q270 268 300 270', 16, '#4a4458'), C(304, 268, 10, '#e6e0cf', 4.5),
    ])


def ogre():
    """변이 오우거(보스): 큰 몸 · 뿔 · 뒷손에 방망이. 뼈대: neck 236 · hip 310 · crotch 320 · knee 346 · split 190
    뒷팔 상자 (20,196)-(132,370) 어깨 (128,214) · 앞팔 상자 (262,262)-(350,362) 어깨 (262,266)"""
    sk, sk2 = '#94b86a', '#82a65c'
    return ''.join([
        # 뒷팔 + 방망이 (왼쪽 아래로)
        P('M40 210 Q30 260 48 330 Q60 360 84 352 Q104 340 96 300 Q86 250 70 206 Z', '#c47a6a'),
        L('M52 250 l10 4 M56 286 l12 2 M66 320 l10 -2', 3.5),
        limb('M128 216 Q100 250 92 300', 30, sk2), C(90, 308, 20, sk2),
        # 다리
        P('M146 306 L140 364 L184 366 L186 306 Z', '#6a4f3a'), P('M196 306 L200 366 L244 364 L238 306 Z', '#7a5a40'),
        P('M128 358 Q126 378 146 378 L190 378 Q192 364 184 358 Z', '#3f3a36'),
        P('M198 358 Q196 378 214 378 L256 378 Q262 364 246 358 Z', '#3f3a36'),
        # 몸통 (근육)
        P('M112 230 Q150 196 200 198 Q256 200 286 236 Q300 276 276 314 L122 314 Q100 276 112 230 Z', sk),
        L('M160 250 Q178 262 196 250 M204 250 Q222 262 240 250 M190 270 L190 300', 4),
        P('M128 304 L272 304 L266 330 ' + zig(132, 266, 330, 8, 10) + ' Z', '#8a5a36'),
        # 머리 + 뿔
        P('M146 96 Q120 70 132 40 Q148 64 170 84 Z', '#efe1c0'), P('M262 96 Q290 70 280 40 Q262 64 240 84 Z', '#efe1c0'),
        E(206, 150, 74, 68, sk),
        P('M178 118 L212 128 M232 124 L262 112', 'none', 5),
        C(200, 140, 13, '#ffe08a', 5), C(244, 136, 12, '#ffe08a', 5),
        f'<circle cx="204" cy="141" r="5.5" fill="{INK}"/><circle cx="248" cy="137" r="5" fill="{INK}"/>',
        P('M176 178 Q218 206 262 174 Q220 186 176 178 Z', '#5a2b2b', 5),
        '<path d="M188 182 l6 12 l6 -10 M246 180 l-5 12 l-6 -10" fill="#fff" stroke="none"/>',
        # 앞팔 (오른쪽 아래로)
        limb('M262 266 Q290 300 304 340', 32, sk), C(308, 346, 21, sk),
    ])


def make_zombie(name, svg):
    im = render(svg, 400, 380, wob=4.0, freq=0.018)
    a = Image.fromarray(im)
    sheet = Image.new('RGBA', (2800, 380), (0, 0, 0, 0))
    for i in range(4):
        sheet.alpha_composite(a, (i * 400, 0))
    # 4 맞기 = 살짝 뒤로 젖힘
    hit = a.rotate(6, resample=Image.BICUBIC, center=(200, 376))
    sheet.alpha_composite(hit, (1600, 0))
    # 5 죽기 = 눕힘(머리 오른쪽), 바닥에 붙임
    ly = a.rotate(-90, resample=Image.BICUBIC, expand=True)
    arr = np.array(ly); ys, xs = np.nonzero(arr[..., 3] > 0)
    ly = ly.crop((xs.min(), ys.min(), xs.max() + 1, ys.max() + 1))
    if ly.width > 396:
        k = 396 / ly.width; ly = ly.resize((396, int(ly.height * k)), Image.BICUBIC)
    sheet.alpha_composite(ly, (2000 + (400 - ly.width) // 2, 376 - ly.height))
    # 6 = 흰 실루엣
    al = im[..., 3]
    wh = np.zeros_like(im); wh[..., :3] = 255; wh[..., 3] = al
    sheet.alpha_composite(Image.fromarray(wh), (2400, 0))
    sheet.save(os.path.join(ART, 'Zombie', 'z_%s.png' % name))
    return a


def make_zombies(only=None):
    Z = zombies()
    tiles = []
    for name, svg in Z.items():
        if only and name not in only:
            continue
        tiles.append((name, make_zombie(name, svg)))
        print('zombie', name)
    os.makedirs(PREV, exist_ok=True)
    pv = Image.new('RGBA', (400 * 6, 380 * ((len(tiles) + 5) // 6)), (233, 220, 188, 255))
    for i, (n, t) in enumerate(tiles):
        pv.alpha_composite(t, ((i % 6) * 400, (i // 6) * 380))
    pv.save(os.path.join(PREV, 'doodle_zombies.png'))


# ------------------------------------------------------------------ 배경 (1728x2448, 흙길 y 820~1760 그대로)
def make_map():
    W, H = 1728, 2448
    import random
    rnd = random.Random(5)
    s = [f'<rect width="{W}" height="{H}" fill="#e9dcbc"/>',
         f'<rect width="{W}" height="760" fill="#a9cf86"/>',
         f'<rect y="1840" width="{W}" height="{H - 1840}" fill="#a9cf86"/>']
    # 위 숲: 나무 + 덤불 줄
    for i in range(14):
        x = i * 135 + rnd.randint(-30, 30); y = 380 + rnd.randint(-160, 120); r = rnd.randint(80, 120)
        s.append(f'<rect x="{x - 14}" y="{y}" width="28" height="{r + 40}" fill="#9a6a44" stroke="{INK}" stroke-width="7"/>')
        s.append(f'<circle cx="{x}" cy="{y}" r="{r}" fill="{rnd.choice(["#79b05a", "#86ba62", "#8fc06c"])}" stroke="{INK}" stroke-width="7"/>')
    for i in range(16):
        x = i * 118 + rnd.randint(-20, 20); r = rnd.randint(70, 105)
        s.append(f'<circle cx="{x}" cy="{720 + rnd.randint(-10, 20)}" r="{r}" fill="{rnd.choice(["#8fc06c", "#9ccb78"])}" stroke="{INK}" stroke-width="7"/>')
    # 흙길 (위 경계 물결)
    s.append(f'<path d="M0 790 Q300 760 600 785 Q900 810 1200 782 Q1500 760 {W} 790 L{W} 1880 Q1400 1850 1000 1874 Q600 1896 300 1866 Q120 1850 0 1872 Z" fill="#ecdcb8" stroke="{INK}" stroke-width="7"/>')
    for _ in range(9):
        x = rnd.randint(100, W - 100); y = rnd.randint(900, 1760)
        s.append(f'<ellipse cx="{x}" cy="{y}" rx="{rnd.randint(150, 320)}" ry="{rnd.randint(40, 80)}" fill="#e1cda3"/>')
    for _ in range(26):
        x = rnd.randint(40, W - 40); y = rnd.randint(860, 1820)
        s.append(f'<ellipse cx="{x}" cy="{y}" rx="{rnd.randint(14, 30)}" ry="{rnd.randint(9, 18)}" fill="#c9c1b3" stroke="{INK}" stroke-width="5"/>')
    for _ in range(22):
        x = rnd.randint(40, W - 40); y = rnd.randint(860, 1820)
        s.append(f'<path d="M{x} {y} l10 -26 M{x + 14} {y} l0 -30 M{x + 28} {y} l-10 -26" fill="none" stroke="#7fae5c" stroke-width="7" stroke-linecap="round"/>')
    # 아래 숲: 덤불 줄 + 나무
    for i in range(16):
        x = i * 118 + rnd.randint(-20, 20); r = rnd.randint(70, 105)
        s.append(f'<circle cx="{x}" cy="{1930 + rnd.randint(-10, 30)}" r="{r}" fill="{rnd.choice(["#8fc06c", "#9ccb78"])}" stroke="{INK}" stroke-width="7"/>')
    for i in range(14):
        x = i * 135 + rnd.randint(-30, 30); y = 2150 + rnd.randint(-60, 160); r = rnd.randint(80, 120)
        s.append(f'<rect x="{x - 14}" y="{y}" width="28" height="{r + 40}" fill="#9a6a44" stroke="{INK}" stroke-width="7"/>')
        s.append(f'<circle cx="{x}" cy="{y}" r="{r}" fill="{rnd.choice(["#79b05a", "#86ba62", "#8fc06c"])}" stroke="{INK}" stroke-width="7"/>')
    im = render(''.join(s), W, H, wob=6.0, freq=0.008)
    Image.fromarray(im).convert('RGB').save(os.path.join(ART, 'Map', 'side_map.png'))
    Image.fromarray(im).convert('RGB').resize((432, 612)).save(os.path.join(PREV, 'doodle_map.png'))
    print('map')



# ------------------------------------------------------------------ 앱 아이콘 (11-05 낙서풍) — Kachi_Gun/Assets/Art/AppIcon/ (연결은 Editor/AppIconSetup.cs)
def _fg_layer(size):
    """카피바라(총 든 자세) + 뒤에서 오는 좀비 — size 정사각형 투명"""
    capy = Image.open(os.path.join(ART, 'Char', 'skin_00.png')).convert('RGBA').crop((0, 0, 360, 360))
    gun = Image.open(os.path.join(ART, 'Char', 'gun_00.png')).convert('RGBA')
    px, py = CX0 + GPX * CS, CY0 + GPY * CS
    capy.alpha_composite(gun, (int(round(px - 90)), int(round(py - 55))))
    z = Image.open(os.path.join(ART, 'Zombie', 'z_basic.png')).convert('RGBA').crop((0, 0, 400, 380)).transpose(Image.FLIP_LEFT_RIGHT)
    lay = Image.new('RGBA', (600, 600), (0, 0, 0, 0))
    lay.alpha_composite(z.resize((300, 285), Image.LANCZOS), (300, 150))
    lay.alpha_composite(capy.resize((560, 560), Image.LANCZOS), (-60, 20))
    return lay.resize((size, size), Image.LANCZOS)


def make_app_icon():
    dst = os.path.join(ROOT, 'Kachi_Gun', 'Assets', 'Art', 'AppIcon')
    bg_svg = lambda s: (f'<rect width="{s}" height="{s}" fill="#ffe3a0"/>'
                        f'<circle cx="{s * .5}" cy="{s * .42}" r="{s * .36}" fill="#fff1c8"/>'
                        f'<path d="M0 {s * .78} Q{s * .25} {s * .72} {s * .5} {s * .77} Q{s * .75} {s * .82} {s} {s * .76} L{s} {s} L0 {s} Z" fill="#a9cf86" stroke="{INK}" stroke-width="{s * .014}"/>')
    # 512 정사각형 · 동그라미
    bg = Image.fromarray(render(bg_svg(512), 512, 512, wob=4, freq=.01))
    fg = _fg_layer(512)
    sq = bg.copy(); sq.alpha_composite(fg)
    sq.convert('RGB').save(os.path.join(dst, 'app_icon.png'))
    mask = Image.new('L', (512, 512), 0)
    from PIL import ImageDraw
    ImageDraw.Draw(mask).ellipse((0, 0, 511, 511), fill=255)
    rd = Image.new('RGBA', (512, 512), (0, 0, 0, 0)); rd.paste(sq, (0, 0), mask)
    rd.save(os.path.join(dst, 'app_icon_round.png'))
    # 적응형 432: 바탕 + 앞 그림(가운데 66% 안전 영역에 들어가게 작게)
    Image.fromarray(render(bg_svg(432), 432, 432, wob=4, freq=.01)).convert('RGB').save(os.path.join(dst, 'app_icon_bg.png'))
    f2 = Image.new('RGBA', (432, 432), (0, 0, 0, 0))
    f2.alpha_composite(_fg_layer(300), (66, 70))
    f2.save(os.path.join(dst, 'app_icon_fg.png'))
    sq.convert('RGB').resize((256, 256)).save(os.path.join(PREV, 'doodle_app_icon.png'))
    print('app icon')



# ------------------------------------------------------------------ 앱 아이콘 2 (11-05 저녁): 아이콘 크기로 바로 그림 (선명) — A 상반신 크게 · B 전신
def _icon_bg(s):
    rays = ''.join(f'<path d="M{s/2} {s*.46} L{s/2 + s*.9*math.cos(math.radians(a - 7)):.1f} {s*.46 + s*.9*math.sin(math.radians(a - 7)):.1f} '
                   f'L{s/2 + s*.9*math.cos(math.radians(a + 7)):.1f} {s*.46 + s*.9*math.sin(math.radians(a + 7)):.1f} Z" fill="#ffe9b0"/>' for a in range(0, 360, 30))
    return (f'<rect width="{s}" height="{s}" fill="#ffd98a"/>{rays}'
            f'<circle cx="{s*.5}" cy="{s*.46}" r="{s*.3}" fill="#fff3d0"/>'
            f'<path d="M0 {s*.8} Q{s*.25} {s*.75} {s*.5} {s*.79} Q{s*.75} {s*.83} {s} {s*.78} L{s} {s} L0 {s} Z" fill="#a9cf86" stroke="{INK}" stroke-width="{s*.012}"/>')


def _icon_fg(s, variant):
    capy = (capy_body_svg(CAPY_SW, cos={'angry': 1}) + capy_gun_svg(CAPY_SW)).replace(INK, CAPY_INK)
    zom = zombies()['basic']
    flash = lambda x, y, r: (f'<g transform="translate({x},{y})">' + ''.join(
        f'<path d="M0 0 L{r*math.cos(math.radians(a-14)):.1f} {r*math.sin(math.radians(a-14)):.1f} L{r*1.5*math.cos(math.radians(a)):.1f} {r*1.5*math.sin(math.radians(a)):.1f} L{r*math.cos(math.radians(a+14)):.1f} {r*math.sin(math.radians(a+14)):.1f} Z" fill="#ffd84d" stroke="{CAPY_INK}" stroke-width="{r*.12:.1f}" stroke-linejoin="round"/>'
        for a in (-50, 0, 50)) + f'<circle r="{r*.55:.1f}" fill="#fff3b0"/></g>')
    if variant == 'A':
        k = s / 512
        return (f'<g transform="translate({660*k:.1f},{60*k:.1f}) scale({-0.95*k:.3f},{0.95*k:.3f})">{zom}</g>'   # 좀비 (왼쪽을 봄, 오른쪽 끝)
                + flash(458 * k, 334 * k, s * .05)
                + f'<g transform="translate({-40*k:.1f},{40*k:.1f}) scale({3.0*k:.3f})">{capy}</g>')
    k = s / 512
    return (f'<g transform="translate({612*k:.1f},{s*.36:.1f}) scale({-0.85*k:.3f},{0.85*k:.3f})">{zom}</g>'
            + flash((10 + 162 * 2.35) * k, (100 + 98 * 2.35) * k, s * .035)
            + f'<g transform="translate({10*k:.1f},{100*k:.1f}) scale({2.35*k:.3f})">{capy}</g>')


def make_app_icon2(variant='A', apply=True):
    from PIL import ImageDraw
    dst = os.path.join(ROOT, 'Kachi_Gun', 'Assets', 'Art', 'AppIcon')
    sq = Image.fromarray(render(_icon_bg(512) + _icon_fg(512, variant), 512, 512, wob=2.0, freq=.01))
    sq.convert('RGB').save(os.path.join(PREV, f'icon_{variant}.png'))
    if not apply:
        return
    sq.convert('RGB').save(os.path.join(dst, 'app_icon.png'))
    mask = Image.new('L', (512, 512), 0); ImageDraw.Draw(mask).ellipse((0, 0, 511, 511), fill=255)
    rd = Image.new('RGBA', (512, 512), (0, 0, 0, 0)); rd.paste(sq, (0, 0), mask)
    rd.save(os.path.join(dst, 'app_icon_round.png'))
    # 적응형 432: 바탕 + 앞 그림(가운데 66% 안전 영역 안으로 줄여서)
    Image.fromarray(render(_icon_bg(432), 432, 432, wob=2.0, freq=.01)).convert('RGB').save(os.path.join(dst, 'app_icon_bg.png'))
    fg = Image.fromarray(render(_icon_fg(512, variant), 512, 512, wob=2.0, freq=.01))
    f2 = Image.new('RGBA', (432, 432), (0, 0, 0, 0))
    f2.alpha_composite(fg.resize((330, 330), Image.LANCZOS), (51, 62))   # 아래 잘린 몸 끝이 보이는 영역(72~360) 밖으로
    f2.save(os.path.join(dst, 'app_icon_fg.png'))
    print('app icon', variant)


if __name__ == '__main__':
    what = set(sys.argv[1:]) or {'capy', 'zombie', 'map'}
    if 'capy' in what:
        make_capy(0)
    if 'skins' in what:
        for k in range(1, 18):
            make_capy(k)
        pv = Image.new('RGBA', (360 * 6, 360 * 3), (233, 220, 188, 255))
        for k in range(18):
            pv.alpha_composite(Image.open(os.path.join(PREV, 'doodle_capy%02d.png' % k)), ((k % 6) * 360, (k // 6) * 360))
        pv.save(os.path.join(PREV, 'doodle_skins.png'))
    if 'zombie' in what:
        make_zombies()
    if 'map' in what:
        make_map()
    if 'icon' in what:
        make_app_icon2('B', apply=False)
        make_app_icon2('A')   # 11-05 저녁: A(상반신) 적용, B 는 미리보기만
