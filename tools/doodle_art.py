# -*- coding: utf-8 -*-
"""밝은 낙서풍(11-05 화풍 샘플) 게임 그림 만들기.

벡터(SVG)로 그린 뒤 Edge 헤드리스로 투명 PNG 를 뽑아, 지금 게임 그림과 같은 칸 크기·발 위치·총구 위치로 저장한다.
→ 게임 코드는 그대로, 그림만 바뀜. 뼈대는 그 다음 tools/rig_gen.py 로 다시 심음(ZOMBIE_RIG · CRAWL_RIG · CAPY_BY_SKIN).

사용: PYTHONUTF8=1 python tools/doodle_art.py [capy] [zombie] [map]     (없으면 전부)
      → Kachi_Gun/Assets/Resources/Art/{Char,Zombie,Map}/...  · 미리보기 tools/_preview/doodle_*.png
원래 도트 그림 백업: tools/_build/pixel_orig/ (git 에는 원래 그림이 기록에 있음)

그림 규칙: 선 #3b281c · 굵은 선 + 단색 · 살짝 흔들리는 선(feTurbulence) · 파스텔(쨍한 색 피함)
"""
import os, sys, subprocess, tempfile, shutil
import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
ART = os.path.join(ROOT, 'Kachi_Gun', 'Assets', 'Resources', 'Art')
PREV = os.path.join(os.path.dirname(__file__), '_preview')
EDGE = r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
INK = '#3b281c'


# ------------------------------------------------------------------ SVG → 투명 PNG
def render(svg_body, w, h, wob=3.0, freq=0.022):
    """svg_body(그림 조각들)를 w x h 투명 PNG(np RGBA)로. wob = 손그림 흔들림 세기(px)"""
    tmp = tempfile.mkdtemp(prefix='doodle_')
    try:
        svg = f'''<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}">
<defs><filter id="wob" x="-5%" y="-5%" width="110%" height="110%">
<feTurbulence type="fractalNoise" baseFrequency="{freq}" numOctaves="2" seed="7"/>
<feDisplacementMap in="SourceGraphic" scale="{wob}"/></filter></defs>
<g filter="url(#wob)">{svg_body}</g></svg>'''
        html = f'<!doctype html><html><head><meta charset="utf-8"><style>html,body{{margin:0;background:transparent;overflow:hidden}}</style></head><body>{svg}</body></html>'
        hp = os.path.join(tmp, 'a.html'); open(hp, 'w', encoding='utf-8').write(html)
        out = os.path.join(tmp, 'a.png')
        url = 'file:///' + hp.replace('\\', '/')
        subprocess.run([EDGE, '--headless=new', '--disable-gpu', '--hide-scrollbars', '--default-background-color=00000000',
                        f'--window-size={w},{h}', '--force-device-scale-factor=1', '--virtual-time-budget=2000',
                        f'--user-data-dir={os.path.join(tmp, "prof")}', f'--screenshot={out}', url],
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=120)
        im = np.array(Image.open(out).convert('RGBA'))
        if im.shape[0] != h or im.shape[1] != w:
            im = np.array(Image.fromarray(im).crop((0, 0, w, h)))
        return im
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


def capy_body_svg(sw=3.2):
    """몸(총·손 빼고) — 로컬 좌표. 오른팔은 어깨~팔뚝 가운데까지(나머지 팔뚝·손은 총 그림에)"""
    return ''.join([
        E(90, 161, 14, 7.5, CAPY_DARK, sw),
        E(54, 160, 14, 7.5, CAPY_DARK, sw),
        P('M38 156 C20 144 18 112 26 92 C30 74 32 52 42 36 C52 22 70 14 90 15 C108 16 121 24 127 38 C133 50 133 66 127 75 C121 82 116 83 116 89 C124 108 122 138 106 154 C92 166 54 166 38 156 Z', CAPY_BODY, sw),
        f'<ellipse cx="72" cy="126" rx="27" ry="29" fill="{CAPY_BELLY}"/>',
        F('M110 51 C123 49 132 57 131 67 C130 76 121 81 110 80 C104 72 104 58 110 51 Z', CAPY_DARK),
        P('M50 32 Q44 20 54 18 Q60 20 59 28', CAPY_DARK, sw),
        P('M78 18 Q78 6 88 7 Q93 10 90 17', CAPY_DARK, sw),
        L('M125 56 q3 2 3 5', 2.6), L('M124 75 q-5 3 -10 0', 2.6),
        L('M75 40 H86', 3.4), L('M97 38 H107', 3.4),
        f'<ellipse cx="80" cy="60" rx="6.5" ry="3.8" fill="#e08f7c" opacity=".55"/>',
        # 오른팔 위쪽 (팔뚝 가운데 x 52 까지, 나머지는 총 그림)
        '<clipPath id="armUp"><rect x="0" y="0" width="54" height="200"/></clipPath>',
        f'<g clip-path="url(#armUp)">'
        + F('M36 88 Q40 104 48 111 Q55 117 63 116 Q75 115 79 109 L79 97 Q76 91 69 90 Q62 88 57 82 Z', CAPY_BODY)
        + L('M36 88 Q40 104 48 111 Q55 117 63 116', sw) + L('M57 82 Q59 88 65 90', sw) + '</g>',
    ])


# 총 그림 260x130, 회전 중심(손목) = (90,55), 총구 = 중심 + (137.6, 위 17.6) — Art.GunMuzzle 그대로 쓰려고 맞춤
#   로컬 총구 (162, 98) · 회전 중심 로컬 (78.6, 108.7)
GPX, GPY = 78.6, 108.7


def capy_gun_svg(sw=3.2, paw=CAPY_BODY, far=CAPY_FAR):
    return ''.join([
        P('M34 98 L60 94 L64 108 L40 112 Q32 110 34 98 Z', '#8a5a36', sw),       # 개머리판 (팔뚝 뒤)
        '<clipPath id="armLo"><rect x="46" y="0" width="200" height="200"/></clipPath>',
        f'<g clip-path="url(#armLo)">'
        + F('M36 88 Q40 104 48 111 Q55 117 63 116 Q75 115 79 109 L79 97 Q76 91 69 90 Q62 88 57 82 Z', paw)
        + L('M36 88 Q40 104 48 111 Q55 117 63 116 Q75 115 79 109', sw) + L('M57 82 Q59 88 65 90 Q69 91 70 94', sw) + '</g>',
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
    body = render(f'<g transform="translate({CX0},{CY0}) scale({CS})">{capy_body_svg()}</g>', 360, 360, wob=3.0)
    gx, gy = 90 - GPX * CS, 55 - GPY * CS
    gun = render(f'<g transform="translate({gx:.2f},{gy:.2f}) scale({CS})">{capy_gun_svg()}</g>', 260, 130, wob=2.4)
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


if __name__ == '__main__':
    what = set(sys.argv[1:]) or {'capy', 'zombie', 'map'}
    if 'capy' in what:
        make_capy(0)
    if 'zombie' in what:
        make_zombies()
    if 'map' in what:
        make_map()
