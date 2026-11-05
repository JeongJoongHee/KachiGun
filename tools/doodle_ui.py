# -*- coding: utf-8 -*-
"""밝은 낙서풍 UI · 아이콘 · 효과 그림 (11-05). tools/doodle_art.py 의 render 를 씀.

지금 그림과 같은 이름·같은 크기로 덮어씀 → 코드·9분할 테두리(Art.kitBorder x5) 그대로.
사용: PYTHONUTF8=1 python tools/doodle_ui.py [이름 앞부분...]   (예: kit  gen  ui  bullet  i_gear)
      PowerShell 에서 실행(Bash 에선 Edge 가 안 뜸)
원래 그림 백업: tools/_build/pixel_orig/{Kit,Gen,UI,Bullet}/
미리보기: tools/_preview/doodle_ui.png
"""
import os, sys, math, shutil
import numpy as np
from PIL import Image
sys.path.insert(0, os.path.dirname(__file__))
from doodle_art import render, P, L, C, E, F, INK, ART, PREV, ROOT

CREAM, CREAM2, TAN = '#fff4dc', '#f3e3c3', '#e9d3a8'
ORANGE, ORANGE2 = '#f2ad55', '#d98f3c'
GREEN, GREEN2, GREEN_D = '#8ccb6e', '#6fae55', '#3f7d3a'
BROWN, BROWN2 = '#c99260', '#a87347'
ICON_B, ICON_W = '#8a5e3c', CREAM
RED, TEAL, TRACK = '#e8735f', '#6cc4c0', '#5b4433'
GOLD, GOLD2 = '#ffd34d', '#e0a722'


def rr(x, y, w, h, r, fill, sw=6, extra=''):
    return f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{r}" fill="{fill}" stroke="{INK}" stroke-width="{sw}" {extra}/>'


def rrf(x, y, w, h, r, fill, op=1):
    return f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{r}" fill="{fill}" opacity="{op}"/>'


def button(w, h, fill, lip, r=18, sw=6, lipH=8):
    """아래 턱이 있는 둥근 버튼/판"""
    m = sw / 2 + 1
    return rr(m, m, w - 2 * m, h - 2 * m, r, lip, sw) + rrf(m + sw / 2, m + sw / 2, w - 2 * m - sw, h - 2 * m - sw - lipH, r - 4, fill) \
        + rrf(m + sw / 2 + 6, m + sw / 2 + 4, w - 2 * m - sw - 12, 6, 3, '#ffffff', .35)


def coin(cx, cy, r, sw=None):
    sw = sw or max(3, r * .16)
    return C(cx, cy, r, GOLD, sw) + f'<circle cx="{cx}" cy="{cy}" r="{r * .6}" fill="none" stroke="{GOLD2}" stroke-width="{r * .2}"/>'


# ------------------------------------------------------------------ 아이콘 모양 (fill 로 색, 크기 w x h 안에)
def ic_check(w, h, f, s): d = f'M{w*.24} {h*.54} L{w*.42} {h*.72} L{w*.78} {h*.28}'; return L(d, w * .26) + L(d, w * .26 - 2 * s, f)
def ic_x(w, h, f, s): d = f'M{w*.3} {h*.3} L{w*.7} {h*.7} M{w*.7} {h*.3} L{w*.3} {h*.7}'; return L(d, w * .24) + L(d, w * .24 - 2 * s, f)
def ic_chest(w, h, f, s): return P(f'M{w*.1} {h*.42} L{w*.9} {h*.42} L{w*.9} {h*.9} L{w*.1} {h*.9} Z', f, s) + P(f'M{w*.1} {h*.42} Q{w*.1} {h*.1} {w*.5} {h*.1} Q{w*.9} {h*.1} {w*.9} {h*.42} Z', f, s) + rr(w * .42, h * .38, w * .16, h * .2, 3, GOLD, s * .8)
def ic_diamond(w, h, f, s): return P(f'M{w*.5} {h*.06} L{w*.92} {h*.4} L{w*.5} {h*.94} L{w*.08} {h*.4} Z', f, s) + L(f'M{w*.08} {h*.4} L{w*.92} {h*.4} M{w*.3} {h*.4} L{w*.5} {h*.94} L{w*.7} {h*.4}', s * .6)
def ic_down(w, h, f, s): return P(f'M{w*.35} {h*.08} L{w*.65} {h*.08} L{w*.65} {h*.5} L{w*.9} {h*.5} L{w*.5} {h*.92} L{w*.1} {h*.5} L{w*.35} {h*.5} Z', f, s)
def ic_play(w, h, f, s): return P(f'M{w*.2} {h*.1} L{w*.88} {h*.5} L{w*.2} {h*.9} Z', f, s)
def ic_pause(w, h, f, s): return rr(w * .12, h * .1, w * .28, h * .8, w * .1, f, s) + rr(w * .6, h * .1, w * .28, h * .8, w * .1, f, s)
def ic_heart(w, h, f, s): return P(f'M{w*.5} {h*.9} C{w*.1} {h*.62} {w*.02} {h*.38} {w*.18} {h*.2} C{w*.32} {h*.06} {w*.48} {h*.16} {w*.5} {h*.3} C{w*.52} {h*.16} {w*.68} {h*.06} {w*.82} {h*.2} C{w*.98} {h*.38} {w*.9} {h*.62} {w*.5} {h*.9} Z', f, s)
def ic_info(w, h, f, s): return C(w * .5, h * .14, w * .3, f, s) + rr(w * .22, h * .32, w * .56, h * .6, w * .25, f, s)
def ic_lock(w, h, f, s): return L(f'M{w*.28} {h*.48} L{w*.28} {h*.3} Q{w*.28} {h*.08} {w*.5} {h*.08} Q{w*.72} {h*.08} {w*.72} {h*.3} L{w*.72} {h*.48}', w * .14) + rr(w * .1, h * .44, w * .8, h * .5, w * .12, f, s) + C(w * .5, h * .64, w * .08, INK, 0) + rrf(w * .46, h * .66, w * .08, h * .14, 1, INK)
def ic_person(w, h, f, s): return C(w * .5, h * .3, w * .22, f, s) + P(f'M{w*.12} {h*.92} Q{w*.12} {h*.56} {w*.5} {h*.56} Q{w*.88} {h*.56} {w*.88} {h*.92} Z', f, s)
def ic_question(w, h, f, s): d = f'M{w*.26} {h*.3} Q{w*.26} {h*.08} {w*.52} {h*.08} Q{w*.8} {h*.1} {w*.78} {h*.32} Q{w*.76} {h*.48} {w*.52} {h*.56} L{w*.52} {h*.66}'; return L(d, w * .26) + L(d, w * .26 - 2 * s, f) + C(w * .52, h * .86, w * .12, f, s)
def ic_shield(w, h, f, s): return P(f'M{w*.5} {h*.06} L{w*.9} {h*.2} Q{w*.92} {h*.66} {w*.5} {h*.94} Q{w*.08} {h*.66} {w*.1} {h*.2} Z', f, s)
def ic_skull(w, h, f, s): return P(f'M{w*.5} {h*.08} Q{w*.92} {h*.08} {w*.92} {h*.46} Q{w*.92} {h*.66} {w*.74} {h*.72} L{w*.74} {h*.9} L{w*.26} {h*.9} L{w*.26} {h*.72} Q{w*.08} {h*.66} {w*.08} {h*.46} Q{w*.08} {h*.08} {w*.5} {h*.08} Z', f, s) + C(w * .34, h * .46, w * .1, INK, 0) + C(w * .66, h * .46, w * .1, INK, 0)
def ic_sound(w, h, f, s): return P(f'M{w*.08} {h*.36} L{w*.26} {h*.36} L{w*.5} {h*.12} L{w*.5} {h*.88} L{w*.26} {h*.64} L{w*.08} {h*.64} Z', f, s) + L(f'M{w*.64} {h*.32} Q{w*.74} {h*.5} {w*.64} {h*.68} M{w*.76} {h*.18} Q{w*.94} {h*.5} {w*.76} {h*.82}', s * 1.4)
def ic_star(w, h, f, s):
    pts = []
    for i in range(10):
        a = -math.pi / 2 + i * math.pi / 5; r = (.46 if i % 2 == 0 else .2) * min(w, h)
        pts.append(f'{w * .5 + r * math.cos(a):.1f} {h * .54 + r * math.sin(a):.1f}')
    return P('M' + ' L'.join(pts) + ' Z', f, s)
def ic_sword(w, h, f, s): return P(f'M{w*.86} {h*.1} L{w*.9} {h*.14} L{w*.4} {h*.64} L{w*.34} {h*.58} Z', '#e6e9ee', s) + L(f'M{w*.24} {h*.5} L{w*.5} {h*.76}', s * 1.6) + L(f'M{w*.24} {h*.5} L{w*.5} {h*.76}', s * .7, f) + L(f'M{w*.36} {h*.64} L{w*.12} {h*.88}', s * 2.2) + L(f'M{w*.36} {h*.64} L{w*.12} {h*.88}', s * 1.1, f)
def ic_trophy(w, h, f, s): return P(f'M{w*.24} {h*.1} L{w*.76} {h*.1} L{w*.72} {h*.42} Q{w*.5} {h*.6} {w*.28} {h*.42} Z', f, s) + L(f'M{w*.24} {h*.18} Q{w*.06} {h*.18} {w*.12} {h*.34} Q{w*.18} {h*.42} {w*.28} {h*.4} M{w*.76} {h*.18} Q{w*.94} {h*.18} {w*.88} {h*.34} Q{w*.82} {h*.42} {w*.72} {h*.4}', s) + rr(w * .42, h * .56, w * .16, h * .2, 2, f, s) + rr(w * .26, h * .76, w * .48, h * .14, 4, f, s)
def ic_music(w, h, f, s): return L(f'M{w*.34} {h*.76} L{w*.34} {h*.16} L{w*.84} {h*.08} L{w*.84} {h*.68}', s * 1.6) + E(w * .24, h * .78, w * .16, h * .12, f, s) + E(w * .74, h * .7, w * .16, h * .12, f, s) + L(f'M{w*.34} {h*.16} L{w*.84} {h*.08}', s * 1.0, f)
def ic_gear(w, h, f, s):
    out = []
    for i in range(8):
        a = i * 45
        out.append(f'<rect x="{w*.42}" y="{h*.04}" width="{w*.16}" height="{h*.2}" rx="3" fill="{f}" stroke="{INK}" stroke-width="{s}" transform="rotate({a} {w*.5} {h*.5})"/>')
    return ''.join(out) + C(w * .5, h * .5, w * .32, f, s) + C(w * .5, h * .5, w * .12, CREAM if f != CREAM else ICON_B, s)

ICONS = dict(check=ic_check, x=ic_x, chest=ic_chest, diamond=ic_diamond, down=ic_down, play=ic_play, pause=ic_pause, heart=ic_heart, info=ic_info,
             lock=ic_lock, person=ic_person, question=ic_question, shield=ic_shield, skull=ic_skull, sound=ic_sound, star=ic_star,
             sword=ic_sword, trophy=ic_trophy, music=ic_music, gear=ic_gear)


def burst(cx, cy, r, n, fill, sw, k=.55, rot=0):
    pts = []
    for i in range(n * 2):
        a = math.radians(rot) + i * math.pi / n; rr_ = r if i % 2 == 0 else r * k
        pts.append(f'{cx + rr_ * math.cos(a):.1f} {cy + rr_ * math.sin(a):.1f}')
    return P('M' + ' L'.join(pts) + ' Z', fill, sw)


def puff(cx, cy, r, fill, sw, n=7, seed=1):
    import random
    rnd = random.Random(seed); out = []
    for i in range(n):
        a = i * 2 * math.pi / n; d = r * .55
        out.append(f'<circle cx="{cx + d * math.cos(a):.1f}" cy="{cy + d * math.sin(a):.1f}" r="{r * rnd.uniform(.42, .55):.1f}" fill="{fill}" stroke="{INK}" stroke-width="{sw}"/>')
    out.append(f'<circle cx="{cx}" cy="{cy}" r="{r * .6}" fill="{fill}"/>')
    return ''.join(out)


def capy_head(w, h):
    s = 6
    return (P(f'M{w*.1} {h*.62} Q{w*.06} {h*.2} {w*.42} {h*.14} Q{w*.78} {h*.1} {w*.92} {h*.4} Q{w*.98} {h*.62} {w*.86} {h*.8} Q{w*.6} {h*.96} {w*.3} {h*.92} Q{w*.12} {h*.86} {w*.1} {h*.62} Z', '#c49a6e', s)
            + F(f'M{w*.78} {h*.36} Q{w*.96} {h*.4} {w*.94} {h*.6} Q{w*.9} {h*.78} {w*.76} {h*.76} Q{w*.7} {h*.56} {w*.78} {h*.36} Z', '#a97f58')
            + P(f'M{w*.26} {h*.2} Q{w*.24} {h*.04} {w*.36} {h*.06} Q{w*.4} {h*.1} {w*.38} {h*.18}', '#a97f58', s)
            + L(f'M{w*.42} {h*.36} h{w*.12} M{w*.62} {h*.33} h{w*.1}', s * .8)
            + f'<ellipse cx="{w*.44}" cy="{h*.56}" rx="{w*.06}" ry="{h*.04}" fill="#e08f7c" opacity=".6"/>'
            + L(f'M{w*.9} {h*.44} q{w*.02} {h*.02} {w*.02} {h*.06} M{w*.86} {h*.7} q-{w*.06} {h*.04} -{w*.1} 0', s * .6))


def assets():
    A = {}   # 이름 → (폴더, w, h, svg, wob)
    def kit(n, w, h, svg, wob=2.0): A[n] = ('Kit', w, h, svg, wob)

    # ---------- 판 · 버튼 (테두리 = kitBorder x5 안에 모서리가 들어가게) ----------
    kit('window', 490, 745, rr(4, 4, 482, 737, 26, CREAM) + f'<path d="M7 30 Q7 7 30 7 L460 7 Q483 7 483 30 L483 74 L7 74 Z" fill="{ORANGE}"/>' + L('M7 74 L483 74', 6)
        + C(450, 40, 22, CREAM, 5) + L('M441 31 L459 49 M459 31 L441 49', 6), 2.0)
    kit('panel_tan', 240, 200, rr(4, 4, 232, 192, 20, CREAM) + rrf(14, 14, 212, 172, 14, '#fffaf0', .7))
    kit('panel_brown', 130, 130, rr(4, 4, 122, 122, 20, '#e7c793') + rrf(12, 12, 106, 106, 14, '#f0d6a8'))
    kit('slot', 70, 70, rr(3, 3, 64, 64, 8, '#e3c894', 4))
    kit('slot_light', 70, 70, rr(3, 3, 64, 64, 8, '#f5e6c4', 4))
    kit('wood', 140, 150, button(140, 150, ORANGE, ORANGE2, 22) + rrf(26, 26, 88, 86, 12, '#e89c45', .55))
    kit('wood_dark', 140, 135, button(140, 135, '#d6964f', '#b8773a', 22) + rrf(26, 24, 88, 72, 12, '#c4843f', .55))
    kit('tile', 90, 100, button(90, 100, ORANGE, ORANGE2, 18))
    kit('plank', 150, 70, button(150, 70, ORANGE, ORANGE2, 18, lipH=7))
    kit('plank_thin', 150, 55, rr(4, 4, 142, 47, 18, CREAM) + rrf(12, 10, 126, 6, 3, '#ffffff', .6))
    kit('btn_green', 210, 65, button(210, 65, GREEN, GREEN2, 18, lipH=7))
    kit('btn_green_hi', 210, 65, button(210, 65, '#a3db86', '#7fbd62', 18, lipH=7))
    kit('sq_green', 110, 115, button(110, 115, GREEN, GREEN2, 22))
    kit('outline', 55, 60, rr(5, 5, 45, 50, 14, 'none', 7, 'stroke-opacity="1"').replace(f'stroke="{INK}"', 'stroke="#ffffff"'))
    for n, c in (('bar_green', GREEN), ('bar_red', RED), ('bar_teal', TEAL), ('bar_track', TRACK)):
        kit(n, 150, 20, rrf(0, 0, 150, 20, 9, c) + (rrf(6, 3, 138, 5, 2.5, '#ffffff', .35) if n != 'bar_track' else ''), 0.5)
    kit('check_off', 55, 60, rr(4, 4, 47, 50, 12, CREAM))
    kit('check_on', 55, 60, rr(4, 4, 47, 50, 12, GREEN) + ic_check(55, 60, CREAM, 3.5))
    kit('btn_down', 65, 70, button(65, 70, GREEN, GREEN2, 16) + ic_down(65, 70, CREAM, 3.5).replace('M', 'M', 1))
    kit('btn_play', 80, 80, button(80, 80, ORANGE, ORANGE2, 18) + f'<g transform="translate(22,18) scale(.6)">{ic_play(60, 60, CREAM, 6)}</g>')
    kit('btn_x', 80, 80, button(80, 80, ORANGE, ORANGE2, 18) + f'<g transform="translate(16,14) scale(.8)">{ic_x(60, 60, CREAM, 5)}</g>')
    kit('badge', 80, 80, button(80, 80, RED, '#c95544', 20) + rr(34, 14, 12, 32, 6, CREAM, 4) + C(40, 56, 7, CREAM, 4))
    kit('node', 150, 155, C(75, 77, 66, CREAM, 8))
    kit('node_dark', 150, 155, C(75, 77, 66, '#b9926a', 8))
    kit('lock', 45, 65, f'<g transform="translate(0,4)">{ic_lock(45, 58, "#9fb3c8", 4)}</g>')
    kit('lose', 125, 125, ic_x(125, 125, '#6f86b8', 7))
    kit('star', 145, 140, ic_star(145, 140, GOLD, 8))
    kit('star_off', 145, 140, ic_star(145, 140, '#d9c9ad', 8))
    kit('star_s', 85, 85, ic_star(85, 85, GOLD, 6))
    kit('star_s_off', 85, 85, ic_star(85, 85, '#d9c9ad', 6))
    kit('music', 65, 65, ic_music(65, 65, GREEN, 4.5))
    kit('music_off', 65, 65, ic_music(65, 65, '#b9926a', 4.5))
    kit('sound', 80, 60, ic_sound(80, 60, GREEN, 4.5))
    kit('sound_off', 80, 60, ic_sound(80, 60, '#b9926a', 4.5) + L('M50 12 L76 50', 5, RED))
    # 아이콘 (갈색 · 흰색)
    sizes = dict(check=(55, 55), chest=(60, 55), diamond=(55, 65), down=(55, 65), gear=(70, 70), heart=(55, 55), info=(25, 70), lock=(45, 60),
                 music=(65, 60), pause=(45, 55), person=(45, 55), play=(45, 55), question=(50, 70), shield=(50, 60), skull=(55, 55),
                 sound=(65, 55), star=(65, 60), sword=(60, 60), trophy=(65, 65), x=(55, 55))
    for n, (w, h) in sizes.items():
        kit('i_' + n, w, h, ICONS[n](w, h, ICON_B, 4), 1.2)
    for n in ('check', 'chest', 'gear', 'lock', 'pause', 'trophy'):
        w, h = sizes[n]; kit('i_' + n + '_w', w, h, ICONS[n](w, h, ICON_W, 4), 1.2)

    # ---------- Gen ----------
    def gen(n, w, h, svg, wob=2.0): A[n] = ('Gen', w, h, svg, wob)
    gen('arrow', 192, 192, P('M70 12 L122 12 L122 104 L168 104 L96 182 L24 104 L70 104 Z', '#ffd84d', 8))
    for i, (r, col) in enumerate(((70, '#ffcf5a'), (104, '#ffb04a'), (96, '#d9d0c4'))):
        if i < 2:
            gen(f'boom_{i}', 256, 256, burst(128, 128, r + 14, 9, '#ff9a4a', 7, .62, 10) + puff(128, 128, r * .8, col, 7, 7, i + 1) + C(128, 128, r * .35, '#fff3b0', 0))
            gen(f'boom_g_{i}', 256, 256, burst(128, 128, r + 14, 9, '#d0d0d0', 7, .62, 10) + puff(128, 128, r * .8, '#eeeeee', 7, 7, i + 1) + C(128, 128, r * .35, '#ffffff', 0))
        else:
            gen('boom_2', 256, 256, f'<g opacity=".85">{puff(128, 128, r, col, 7, 8, 3)}</g>')
            gen('boom_g_2', 256, 256, f'<g opacity=".85">{puff(128, 128, r, "#bdbdbd", 7, 8, 3)}</g>')
    gen('capy_head', 167, 131, capy_head(167, 131))
    gen('gem_gold', 132, 132, P('M26 70 L48 22 L92 18 L114 60 L96 108 L40 112 Z', GOLD, 7) + L('M48 22 L64 64 L92 18 M26 70 L64 64 L114 60 M64 64 L68 110', 4, GOLD2))
    gen('ore_rock', 132, 132, P('M20 80 L36 30 L82 16 L114 46 L110 98 L58 116 Z', '#b9b4ad', 7) + L('M36 30 L62 62 L114 46 M62 62 L58 116', 4, '#8f8a83'))
    gen('spark', 128, 128, burst(64, 64, 56, 4, '#fff1a8', 6, .28, 45) + burst(64, 64, 34, 4, '#ffffff', 4, .3, 0))
    # ---------- UI ----------
    def ui(n, w, h, svg, wob=2.0): A[n] = ('UI', w, h, svg, wob)
    def card(icon):
        return (L('M346 30 Q360 6 384 12 Q376 34 350 34 Z M380 40 Q402 20 420 32 Q404 52 380 44 Z', 5) + P('M346 30 Q360 6 384 12 Q376 34 350 34 Z', '#8fc06c', 5) + P('M380 40 Q402 20 420 32 Q404 52 380 44 Z', '#8fc06c', 5)
                + rr(12, 46, 448, 284, 26, CREAM, 7) + rr(26, 204, 420, 116, 22, '#bfe3a3', 6)
                + rr(100, 236, 282, 54, 27, GREEN_D, 6) + coin(132, 262, 30, 6)
                + rr(6, 28, 116, 116, 22, icon[0], 7) + icon[1]
                + P('M452 250 Q476 232 490 248 Q474 266 452 258 Z', '#8fc06c', 5) + P('M100 340 Q96 362 112 368 Q122 350 108 336 Z', '#8fc06c', 5))
    fire = ('#ff9a6a', burst(64, 86, 40, 7, '#ffd84d', 5, .5, -90) + C(64, 90, 14, '#fff3b0', 0))
    swd = ('#8fb8e8', f'<g transform="translate(16,40) scale(1.6)">{ic_sword(60, 60, "#8a5e3c", 3)}</g>')
    cn = ('#ffe08a', coin(64, 86, 34, 6))
    ui('card_atk', 497, 372, card(fire)); ui('card_spd', 497, 372, card(swd)); ui('card_gold', 497, 372, card(cn))
    ui('icon_gem', 132, 132, P('M24 50 L46 18 L86 18 L108 50 L66 116 Z', '#8fd6ff', 7) + L('M24 50 L108 50 M46 18 L56 50 L66 116 L76 50 L86 18', 4, '#5aa8d8'))
    ui('icon_money', 79, 74, coin(39.5, 37, 31, 6))
    ui('icon_pistol', 144, 126, P('M14 30 L126 30 L126 58 L64 58 L56 108 L24 108 L32 58 L14 58 Z', '#8f97a3', 7) + rr(36, 56, 28, 50, 8, '#9a6a44', 6) + L('M62 58 Q74 76 60 82', 5))
    ui('icon_shop', 143, 143, rr(20, 60, 103, 70, 8, CREAM, 7) + P('M12 60 L28 18 L115 18 L131 60 Z', '#f08a72', 7) + L('M40 18 L34 60 M64 18 L62 60 M88 18 L90 60 M110 18 L118 60', 5) + rr(32, 80, 30, 50, 4, '#c99260', 6) + rr(76, 78, 34, 28, 4, '#9fd6ff', 6))
    ui('icon_swords', 153, 139, f'<g transform="translate(6,4) scale(2.2)">{ic_sword(60, 60, "#8a5e3c", 3)}</g><g transform="translate(147,4) scale(-2.2,2.2)">{ic_sword(60, 60, "#8a5e3c", 3)}</g>')
    ui('stat_atk', 80, 81, rr(4, 4, 72, 73, 16, '#ff9a6a', 6) + burst(40, 44, 26, 7, '#ffd84d', 4, .5, -90))
    ui('stat_gold', 80, 81, rr(4, 4, 72, 73, 16, '#ffe08a', 6) + coin(40, 41, 22, 5))
    # ---------- Bullet ----------
    def bl(n, w, h, svg, wob=1.5): A[n] = ('Bullet', w, h, svg, wob)
    def trail(x0, x1, y, h, c): return f'<path d="M{x0} {y} L{x1} {y - h / 2} L{x1} {y + h / 2} Z" fill="{c}" opacity=".75"/>'
    bl('bullet_basic', 255, 145, trail(10, 160, 72, 40, '#ffd27a') + trail(40, 160, 72, 22, '#fff1b8') + rr(150, 52, 92, 40, 20, '#ffcf3a', 7) + rrf(160, 58, 60, 8, 4, '#ffffff', .5))
    bl('bullet_plain', 200, 69, rr(10, 10, 178, 49, 24, '#ffcf3a', 7) + rrf(24, 16, 120, 9, 4, '#ffffff', .5))
    bl('crosshair', 156, 160, C(78, 80, 50, 'none', 8).replace(f'stroke="{INK}"', 'stroke="#e8735f"') + L('M78 10 L78 44 M78 116 L78 150 M8 80 L42 80 M114 80 L148 80', 8, '#e8735f') + C(78, 80, 6, '#e8735f', 0))
    bl('drill', 256, 112, rr(8, 30, 70, 52, 12, '#8f97a3', 7) + P('M76 18 L246 56 L76 94 Z', '#c9d1da', 7) + L('M110 26 L130 86 M150 34 L166 78 M190 44 L200 68', 5))
    bl('laser_beam', 32, 24, rrf(0, 2, 32, 20, 0, '#8fd6ff') + rrf(0, 8, 32, 8, 0, '#ffffff'), 0)
    bl('missile', 320, 272, puff(70, 136, 56, '#ffb04a', 6, 7, 4) + rr(96, 106, 170, 60, 30, '#e8735f', 7) + P('M256 108 Q312 136 256 164 Z', '#fff4dc', 7) + P('M110 106 L84 70 L140 106 Z M110 166 L84 202 L140 166 Z', '#7fb8e8', 6) + C(200, 136, 14, '#9fd6ff', 5))
    bl('pellet', 64, 63, C(32, 31.5, 22, '#ffcf3a', 6))
    def icon_tile(inner): return inner
    bl('icon_single', 255, 132, trail(10, 110, 66, 30, '#ffd27a') + rr(100, 44, 140, 44, 22, '#ffcf3a', 7))
    bl('icon_fast', 255, 145, trail(4, 150, 72, 60, '#ffb04a') + trail(30, 150, 72, 30, '#fff1b8') + rr(140, 50, 104, 44, 22, '#ffcf3a', 7) + L('M30 30 L110 30 M40 114 L120 114', 7, '#ffd27a'))
    bl('icon_laser', 256, 236, P('M20 200 Q90 120 140 150 Q190 180 236 40', 'none', 22).replace(f'stroke="{INK}"', 'stroke="#8fd6ff"') + L('M20 200 Q90 120 140 150 Q190 180 236 40', 8, '#ffffff') + C(206, 70, 40, 'none', 7).replace(f'stroke="{INK}"', 'stroke="#e8735f"'))
    bl('icon_missile', 252, 256, f'<g transform="rotate(-45 126 128) translate(-34,-8)">{puff(70, 136, 50, "#ffb04a", 6, 7, 4) + rr(96, 108, 160, 56, 28, "#e8735f", 7) + P("M248 110 Q298 136 248 162 Z", "#fff4dc", 7) + C(196, 136, 13, "#9fd6ff", 5)}</g>')
    bl('icon_pierce', 249, 256, C(150, 150, 90, '#fff4dc', 7) + C(150, 150, 60, '#e8735f', 7) + C(150, 150, 28, '#fff4dc', 7) + f'<g transform="rotate(40 110 110)">{P("M20 90 L170 110 L20 130 Z", "#c9d1da", 7)}</g>')
    bl('icon_shotgun', 256, 161, f'<g transform="translate(6,58) scale(1.3)">{P("M4 30 L40 22 L46 46 L10 52 Z", "#9a6a44", 6) + rr(40, 18, 60, 20, 4, "#6c7480", 6) + rr(96, 22, 60, 12, 4, "#8f97a3", 6)}</g>' + ''.join(C(x, y, 9, '#ffcf3a', 5) for x, y in ((214, 40), (236, 70), (212, 98), (240, 120), (196, 70))))
    return A


def main(keys):
    A = assets()
    bak = os.path.join(ROOT, 'tools', '_build', 'pixel_orig')
    tiles = []
    for n, (folder, w, h, svg, wob) in A.items():
        if keys and not any(n.startswith(k) or folder.lower() == k for k in keys):
            continue
        dst = os.path.join(ART, folder, n + '.png')
        b = os.path.join(bak, folder, n + '.png')
        if os.path.exists(dst) and not os.path.exists(b):
            os.makedirs(os.path.dirname(b), exist_ok=True); shutil.copy2(dst, b)
        if os.path.exists(dst):
            ow, oh = Image.open(dst).size
            if (ow, oh) != (w, h):
                print('크기 다름', n, (ow, oh), (w, h))
        im = render(svg, w, h, wob=wob, freq=0.04)
        Image.fromarray(im).save(dst)
        tiles.append((n, im))
        print(folder, n)
    if tiles:
        T = 150; cols = 10; rows = (len(tiles) + cols - 1) // cols
        pv = Image.new('RGBA', (cols * T, rows * T), (120, 150, 120, 255))
        for i, (n, im) in enumerate(tiles):
            t = Image.fromarray(im); k = min((T - 8) / t.width, (T - 8) / t.height, 2)
            t = t.resize((max(1, int(t.width * k)), max(1, int(t.height * k))), Image.BICUBIC)
            pv.alpha_composite(t, ((i % cols) * T + (T - t.width) // 2, (i // cols) * T + (T - t.height) // 2))
        pv.save(os.path.join(PREV, 'doodle_ui.png'))


if __name__ == '__main__':
    main([a.lower() for a in sys.argv[1:]])
