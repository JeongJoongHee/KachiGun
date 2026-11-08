# -*- coding: utf-8 -*-
"""모바일 광고 영상 (11-07) — 실제 게임 화면(Unity 촬영)으로 15초 · 25초 광고 mp4

  1) 촬영:  bash tools/unity_batch.sh KachiGun.EditorTools.PlayTest.RunAd --play   (Unity 닫고, 약 1분)
           → Kachi_Gun/Logs/PlayTest/ad/<장면>/0000.jpg (1080x1920, 초당 30장, 게임 시간 고정)
  2) 편집:  PYTHONUTF8=1 python tools/ad_video.py
           → 광고 소재/동영상/  세로 15초 · 세로 25초 (1080x1920) · 정사각 15초 (1080x1080) · 가로 15초 (1920x1080) + 썸네일
  소리 = 게임 소리를 파이썬으로 따라 만든 wav (사운드_검토_wav/ — 10-26 소리 검토 때 만든 것)
  영상 인코더 = imageio-ffmpeg (pip install imageio-ffmpeg) 안의 ffmpeg
  글꼴 = 주아(Jua) — 가운뎃점(·)이 없으니 문구에 쓰지 말 것
"""
import math, os, random, subprocess, wave
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter
import imageio_ffmpeg

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GAME = os.path.join(ROOT, 'Kachi_Gun')
AD = os.path.join(GAME, 'Logs', 'PlayTest', 'ad')
SHOTS = os.path.join(GAME, 'Logs', 'PlayTest')
ART = os.path.join(GAME, 'Assets', 'Resources', 'Art')
FONT = os.path.join(GAME, 'Assets', 'Resources', 'Font', 'Jua-Regular.ttf')
ICON = os.path.join(GAME, 'Assets', 'Art', 'AppIcon', 'app_icon.png')
WAV = os.path.join(ROOT, '사운드_검토_wav')
OUT = os.path.join(ROOT, '광고 소재', '동영상')
FPS = 30
SR = 44100

GOLD = (255, 205, 80)
CREAM = (255, 244, 214)
INK = (40, 26, 18)
GREEN = (150, 230, 120)
_fonts = {}


def font(size):
    if size not in _fonts: _fonts[size] = ImageFont.truetype(FONT, size)
    return _fonts[size]


def text_outline(d, xy, s, f, fill, outline=INK, w=6, anchor='mm'):
    x, y = xy
    for dx in range(-w, w + 1, 2):
        for dy in range(-w, w + 1, 2):
            if dx * dx + dy * dy <= w * w: d.text((x + dx, y + dy), s, font=f, fill=outline, anchor=anchor)
    d.text((x, y), s, font=f, fill=fill, anchor=anchor)


# ---------------------------------------------------------------- 장면 (촬영 장면 이름, 장면 안 시작 초, 길이 초, 문구 1, 문구 2, 효과음)
# 효과음 = (장면 안 초, wav 이름, 크기) · 'gun' = 총소리 계속 · 'bgm' = 배경음 종류
def seg(clip, start, dur, l1, l2, sfx=(), gun=0.0, bgm='stage', taps=None):
    return dict(clip=clip, start=start, dur=dur, l1=l1, l2=l2, sfx=list(sfx), gun=gun, bgm=bgm, taps=taps or [])


# 강화 카드 누른 자리 (1080x1920 화면, 장면 b_upgrade 의 0.13초 간격 — PlayTest.AdTour 와 같은 규칙)
def upgrade_taps(start, dur):
    out, t = [], 0.0
    while t < 3.5:
        x = 540 - 330 if t < 1.6 else 540 if t < 2.6 else 540 + 330
        if start <= t < start + dur: out.append((t - start, x, 1660))
        t += 0.13
    return out


HOOK = seg('a_horde', 0.0, 3.0, '좀비 떼가 몰려온다!', '카피바라 분대, 발사!',
           [(0.0, 'Boom', 0.9), (0.05, 'Laser', 0.7), (0.35, 'Missile', 0.6), (0.6, 'Boom', 0.7), (1.2, 'Boom', 0.6), (1.9, 'Boom', 0.6)], gun=1.0)
HOOK['hook'] = True   # 첫 장면(썸네일)부터 문구가 또렷하게
UPG = seg('b_upgrade', 0.0, 2.8, '톡톡 눌러서 강화', '공격력, 공격속도, 금전', [], gun=0.35, taps=upgrade_taps(0.0, 2.8))
MERGE = seg('c_merge', 0.0, 3.6, '같은 탄알을 겹치면', '한 단계 위로 합성!',
            [(0.5, 'Merge', 1.0), (1.5, 'Merge', 1.0), (2.5, 'Merge', 1.0), (3.2, 'Merge', 1.0), (3.25, 'Coin', 0.6)])
BOSS = seg('d_boss', 0.2, 3.8, '챕터마다 보스 등장!', '쓰러뜨리고 다음 챕터로',
           [(0.0, 'BossAppear', 1.0), (0.4, 'Boom', 0.7), (1.4, 'Boom', 0.7), (3.0, 'BossDead', 1.0), (3.05, 'Victory', 0.8)], gun=0.9, bgm='boss')
HIGH = seg('e_high', 0.0, 2.2, 'D부터 SSU까지 13등급', '레이저와 미사일의 화끈한 화력',
           [(0.0, 'Laser', 0.8), (0.1, 'Boom', 0.8), (0.5, 'Missile', 0.7), (0.8, 'Boom', 0.7)], gun=1.0)
MINE = seg('f_mine', 0.0, 2.0, '비밀던전에서', '다이아를 캐자!', [(0.2 + 0.11 * i, 'MineHit', 0.5) for i in range(14)], gun=0.5)
SKIN = seg('g_skin', 0.0, 2.0, '18가지 카피바라 코스튬', '모을수록 강해지는 보유 효과', [(0.1, 'UiClick', 0.6)])
OFFLINE = dict(still='18d_offline.png', dur=2.6, l1='꺼 둬도 쌓이는', l2='방치 보상', sfx=[(0.3, 'Coin', 0.8), (0.45, 'Coin', 0.8)], gun=0, bgm='stage', taps=[])
END = dict(end=True, dur=2.5, l1='', l2='', sfx=[(0.0, 'Victory', 0.9)], gun=0, bgm='stage', taps=[])

EDITS = {
    '15초': [HOOK, UPG, MERGE, BOSS, END],                                   # 3 + 2.8 + 3.6 + 3.8 + 2.5 = 15.7
    '25초': [HOOK, UPG, MERGE, HIGH, BOSS, MINE, SKIN, OFFLINE, dict(END, dur=3.0)],   # 3+2.8+3.6+2.2+3.8+2+2+2.6+3 = 25.0
}
EDITS['15초'][4] = dict(END, dur=1.8)                                         # 15초 맞춤 (3+2.8+3.6+3.8+1.8 = 15.0)


# ---------------------------------------------------------------- 그림
def clip_frames(clip):
    d = os.path.join(AD, clip)
    return [os.path.join(d, f) for f in sorted(os.listdir(d)) if f.endswith('.jpg')]


_cache = {}


def frame_at(s, t):
    """장면 s 의 t 초(장면 안) 그림 1080x1920"""
    if 'still' in s:
        key = s['still']
        if key not in _cache:
            im = Image.open(os.path.join(SHOTS, key)).convert('RGB')
            y0 = (im.height - 1920) // 2
            _cache[key] = im.crop((0, y0, 1080, y0 + 1920))
        im = _cache[key]
        k = 1.0 + 0.05 * (t / s['dur'])                       # 천천히 다가감
        w, h = int(1080 * k), int(1920 * k)
        return im.resize((w, h), Image.BILINEAR).crop(((w - 1080) // 2, (h - 1920) // 2, (w - 1080) // 2 + 1080, (h - 1920) // 2 + 1920))
    fs = _cache.setdefault(s['clip'], clip_frames(s['clip']))
    i = min(len(fs) - 1, int(round((s['start'] + t) * FPS)))
    return Image.open(fs[i]).convert('RGB')


def capy_and_zombies(h):
    sk = Image.open(os.path.join(ART, 'Char', 'skin_17.png')).convert('RGBA')   # 해적 선장 (칸 8)
    c = 8
    cap = sk.crop(((c % 4) * 360, (c // 4) * 360, (c % 4 + 1) * 360, (c // 4 + 1) * 360))
    cap = cap.crop(cap.getbbox())
    zs = []
    for zn in ('z_basic', 'z_fat', 'z_hat'):
        z = Image.open(os.path.join(ART, 'Zombie', zn + '.png')).convert('RGBA').crop((0, 0, 400, 380))
        zs.append(z.crop(z.getbbox()).transpose(Image.FLIP_LEFT_RIGHT))
    sc = lambda im, hh: im.resize((int(im.width * hh / im.height), hh), Image.LANCZOS)
    return sc(cap, h), [sc(z, int(h * 0.82)) for z in zs]


def rounded(im, r):
    m = Image.new('L', im.size, 0)
    ImageDraw.Draw(m).rounded_rectangle([0, 0, im.width - 1, im.height - 1], r, fill=255)
    out = im.convert('RGBA'); out.putalpha(m)
    return out


def cta_button(w, h, text, size, pulse=1.0):
    b = Image.new('RGBA', (int(w * pulse) + 20, int(h * pulse) + 20), (0, 0, 0, 0))
    d = ImageDraw.Draw(b)
    bw, bh = int(w * pulse), int(h * pulse)
    d.rounded_rectangle([10, 16, 10 + bw, 16 + bh - 6], bh // 2, fill=(60, 110, 40))
    d.rounded_rectangle([10, 10, 10 + bw, 10 + bh - 6], bh // 2, fill=(110, 190, 70), outline=INK, width=6)
    text_outline(d, (10 + bw // 2, 10 + bh // 2 - 6), text, font(int(size * pulse)), (255, 255, 255), w=5)
    return b


def end_card(W, H, t):
    """마지막 화면: 앱 아이콘 · 이름 · 캐릭터 · [지금 무료 플레이]"""
    bg = Image.new('RGB', (W, H), (255, 236, 196))
    d = ImageDraw.Draw(bg)
    for y in range(H):   # 위 → 아래 따뜻한 그라데이션
        k = y / H
        d.line([(0, y), (W, y)], fill=(int(255 - 30 * k), int(232 - 70 * k), int(190 - 110 * k)))
    canvas = bg.convert('RGBA')
    portrait = H > W
    s = min(W, H) / 1080
    icon = Image.open(ICON).convert('RGBA').resize((int(300 * s), int(300 * s)), Image.LANCZOS)
    icon = rounded(icon, int(64 * s))
    cap, zs = capy_and_zombies(int((520 if portrait else 430) * s))
    pulse = 1.0 + 0.06 * math.sin(t * 2 * math.pi * 1.6)
    if portrait:
        canvas.alpha_composite(icon, ((W - icon.width) // 2, int(230 * s)))
        d = ImageDraw.Draw(canvas)
        text_outline(d, (W // 2, int(680 * s)), '카피바라', font(int(130 * s)), GOLD, w=9)
        text_outline(d, (W // 2, int(820 * s)), 'vs 좀비', font(int(130 * s)), GREEN, w=9)
        text_outline(d, (W // 2, int(940 * s)), '방치형 분대 슈팅', font(int(58 * s)), CREAM, w=6)
        base = H - int(360 * s)
        for i, z in enumerate(zs):
            canvas.alpha_composite(z, (W // 2 + int((60 + i * 120) * s), base - z.height + int((0 if i != 1 else -30) * s)))
        canvas.alpha_composite(cap, (W // 2 - cap.width + int(40 * s), base - cap.height))
        b = cta_button(int(640 * s), int(150 * s), '지금 무료 플레이', int(70 * s), pulse)
        canvas.alpha_composite(b, ((W - b.width) // 2, H - int(300 * s) + int(150 * s * (1 - pulse) / 2)))
        text_outline(ImageDraw.Draw(canvas), (W // 2, H - int(90 * s)), 'Google Play 에서 다운로드', font(int(44 * s)), CREAM, w=4)
    elif W == H:
        # 정사각: 위에서 아래로 — 아이콘 · 이름 · 캐릭터 · 버튼 (11-07: 옆으로 놓으면 캐릭터가 버튼을 가렸음)
        icon2 = rounded(Image.open(ICON).convert('RGBA').resize((int(190 * s), int(190 * s)), Image.LANCZOS), int(42 * s))
        canvas.alpha_composite(icon2, ((W - icon2.width) // 2, int(50 * s)))
        d = ImageDraw.Draw(canvas)
        text_outline(d, (W // 2, int(320 * s)), '카피바라 vs 좀비', font(int(104 * s)), GOLD, w=8)
        text_outline(d, (W // 2, int(410 * s)), '방치형 분대 슈팅', font(int(48 * s)), CREAM, w=5)
        cap2, zs2 = capy_and_zombies(int(330 * s))
        base = int(790 * s)
        for i, z in enumerate(zs2):
            canvas.alpha_composite(z, (W // 2 + int((30 + i * 95) * s), base - z.height + int((0 if i != 1 else -22) * s)))
        canvas.alpha_composite(cap2, (W // 2 - cap2.width + int(30 * s), base - cap2.height))
        b = cta_button(int(560 * s), int(124 * s), '지금 무료 플레이', int(58 * s), pulse)
        canvas.alpha_composite(b, ((W - b.width) // 2, int(820 * s)))
        text_outline(ImageDraw.Draw(canvas), (W // 2, H - int(40 * s)), 'Google Play 에서 다운로드', font(int(34 * s)), INK, outline=CREAM, w=3)
    else:
        # 가로: 왼쪽 글자, 오른쪽 캐릭터
        left = W * 0.30 if W > H * 1.2 else W * 0.30
        canvas.alpha_composite(icon, (int(left - icon.width / 2), int(H * 0.10)))
        d = ImageDraw.Draw(canvas)
        text_outline(d, (int(left), int(H * 0.10) + icon.height + int(90 * s)), '카피바라', font(int(104 * s)), GOLD, w=8)
        text_outline(d, (int(left), int(H * 0.10) + icon.height + int(200 * s)), 'vs 좀비', font(int(104 * s)), GREEN, w=8)
        b = cta_button(int(520 * s), int(130 * s), '지금 무료 플레이', int(60 * s), pulse)
        canvas.alpha_composite(b, (int(left - b.width / 2), H - int(250 * s)))
        text_outline(ImageDraw.Draw(canvas), (int(left), H - int(70 * s)), 'Google Play 에서 다운로드', font(int(36 * s)), INK, outline=CREAM, w=3)
        base = H - int(80 * s)
        cx = int(W * 0.70)
        for i, z in enumerate(zs):
            canvas.alpha_composite(z, (cx + int((40 + i * 105) * s), base - z.height + int((0 if i != 1 else -26) * s)))
        canvas.alpha_composite(cap, (cx - cap.width + int(60 * s), base - cap.height))
    return canvas.convert('RGB')


def caption_layer(W, l1, l2, t, size1, size2, y1, y2, instant=False):
    """문구 2줄 (처음 0.18초 동안 위에서 톡 내려오며 나타남 · instant = 첫 장면처럼 바로 또렷하게)"""
    lay = Image.new('RGBA', (W, y2 + size2 + 40), (0, 0, 0, 0))
    a = 1.0 if instant else min(1.0, t / 0.18)
    off = int((1 - a) * -30)
    d = ImageDraw.Draw(lay)
    if l1: text_outline(d, (W // 2, y1 + off), l1, font(size1), GOLD, w=8)
    if l2: text_outline(d, (W // 2, y2 + off), l2, font(size2), CREAM, w=6)
    if a < 1:
        lay.putalpha(lay.getchannel('A').point(lambda v: int(v * a)))
    return lay


_grad = {}


def top_shade(W, h):
    if (W, h) not in _grad:
        g = Image.new('L', (W, h))
        for y in range(h):
            k = 1.0 if y < h * 0.62 else max(0.0, 1 - (y - h * 0.62) / (h * 0.38))
            ImageDraw.Draw(g).line([(0, y), (W, y)], fill=int(236 * k))
        _grad[(W, h)] = Image.merge('RGBA', (*Image.new('RGB', (W, h), (20, 12, 8)).split(), g))
    return _grad[(W, h)]


def draw_taps(im, s, t, scale=1.0, ox=0, oy=0):
    """강화 카드를 누른 자리에 하얀 물결 (0.35초)"""
    d = ImageDraw.Draw(im, 'RGBA')
    for (tt, x, y) in s['taps']:
        u = t - tt
        if 0 <= u < 0.35:
            r = (30 + 110 * u / 0.35) * scale
            al = int(220 * (1 - u / 0.35))
            cx, cy = ox + x * scale, oy + y * scale
            d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=(255, 255, 255, al), width=max(3, int(9 * scale)))
            if u < 0.12: d.ellipse([cx - 26 * scale, cy - 26 * scale, cx + 26 * scale, cy + 26 * scale], fill=(255, 255, 255, 150))


def vertical(s, t):
    if s.get('end'): return end_card(1080, 1920, t)
    im = frame_at(s, t).convert('RGBA')
    draw_taps(im, s, t)
    im.alpha_composite(top_shade(1080, 470), (0, 0))
    im.alpha_composite(caption_layer(1080, s['l1'], s['l2'], t, 96, 60, 150, 268, s.get('hook', False)), (0, 0))
    return im.convert('RGB')


def blurred_bg(src, W, H):
    small = src.resize((max(1, src.width // 8), max(1, src.height // 8)), Image.BILINEAR).filter(ImageFilter.GaussianBlur(4))
    k = max(W / small.width, H / small.height)
    bg = small.resize((int(small.width * k) + 2, int(small.height * k) + 2), Image.BILINEAR)
    bg = bg.crop(((bg.width - W) // 2, (bg.height - H) // 2, (bg.width - W) // 2 + W, (bg.height - H) // 2 + H))
    return Image.blend(bg, Image.new('RGB', (W, H), (30, 20, 16)), 0.5)


def side_layout(s, t, W, H):
    """정사각 · 가로: 게임 화면(세로)을 오른쪽에 · 왼쪽에 문구 · 뒤는 같은 화면을 흐리게"""
    if s.get('end'): return end_card(W, H, t)
    src = frame_at(s, t)
    canvas = blurred_bg(src, W, H).convert('RGBA')
    gh = H - 40
    gw = int(1080 * gh / 1920)
    g = src.convert('RGBA')
    draw_taps(g, s, t)
    g = rounded(g.resize((gw, gh), Image.BILINEAR), 22)
    gx = W - gw - (60 if W > H * 1.2 else 40)
    canvas.alpha_composite(g, (gx, 20))
    lw = gx - 20
    a = 1.0 if s.get('hook') else min(1.0, t / 0.18)
    d = ImageDraw.Draw(canvas)
    sc = 1.0 if W > H * 1.2 else 0.82
    off = int((1 - a) * -24)
    # 줄이 칸보다 길면 글자 줄임
    f1 = int(92 * sc); f2 = int(56 * sc)
    while font(f1).getlength(s['l1']) > lw - 60 and f1 > 40: f1 -= 2
    while font(f2).getlength(s['l2']) > lw - 60 and f2 > 30: f2 -= 2
    if a > 0:
        text_outline(d, (lw // 2 + 10, H // 2 - int(60 * sc) + off), s['l1'], font(f1), GOLD, w=7)
        text_outline(d, (lw // 2 + 10, H // 2 + int(50 * sc) + off), s['l2'], font(f2), CREAM, w=5)
    icon = rounded(Image.open(ICON).convert('RGBA').resize((int(120 * sc), int(120 * sc)), Image.LANCZOS), int(26 * sc))
    canvas.alpha_composite(icon, (40, H - icon.height - 36))
    text_outline(d, (60 + icon.width, H - icon.height // 2 - 36), '카피바라 vs 좀비', font(int(48 * sc)), CREAM, w=4, anchor='lm')
    return canvas.convert('RGB')


# ---------------------------------------------------------------- 소리
_wav = {}


def load_wav(name):
    if name not in _wav:
        w = wave.open(os.path.join(WAV, name + '.wav'))
        a = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(np.float32) / 32768
        if w.getnchannels() == 2: a = a.reshape(-1, 2).mean(1)
        _wav[name] = a
    return _wav[name]


def mix_audio(segs, total, path):
    n = int(total * SR) + SR
    out = np.zeros(n, np.float32)
    rng = random.Random(7)
    t0 = 0.0
    for s in segs:
        a, b = int(t0 * SR), int((t0 + s['dur']) * SR)
        # 배경음 (장면 길이만큼, 시작·끝 0.08초 이어 붙이기)
        bg = load_wav('Boss_BGM_x4' if s['bgm'] == 'boss' else 'Stage_BGM_x4')
        off = int(t0 * SR) % len(bg)
        seg_bg = np.resize(np.roll(bg, -off), b - a) * 0.42
        out[a:b] += seg_bg
        # 총소리 계속
        if s['gun'] > 0:
            tt = 0.0
            while tt < s['dur']:
                nm = rng.choice(['Laser', 'Shot', 'Shotgun', 'Laser'])
                w = load_wav(nm) * 0.22 * s['gun'] * rng.uniform(0.6, 1.0)
                i = a + int(tt * SR)
                out[i:i + len(w)] += w[:max(0, n - i)]
                if rng.random() < 0.35:
                    c = load_wav('Coin') * 0.18 * s['gun']; j = i + int(0.03 * SR); out[j:j + len(c)] += c[:max(0, n - j)]
                tt += rng.uniform(0.07, 0.13)
        for (st, nm, g) in s['sfx']:
            w = load_wav(nm) * g * 0.8
            i = a + int(st * SR)
            out[i:i + len(w)] += w[:max(0, n - i)]
        for (st, x, y) in s['taps']:
            w = load_wav('Upgrade') * 0.55
            i = a + int(st * SR)
            out[i:i + len(w)] += w[:max(0, n - i)]
        t0 += s['dur']
    out = out[:int(total * SR)]
    fade = int(0.4 * SR)
    out[-fade:] *= np.linspace(1, 0, fade)
    peak = np.max(np.abs(out)) or 1
    out = np.tanh(out / peak * 1.3) / math.tanh(1.3) * 0.89   # 살짝 눌러 크게
    pcm = (out * 32767).astype(np.int16)
    with wave.open(path, 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes(pcm.tobytes())


# ---------------------------------------------------------------- 확률형 아이템 표시 (11-08)
# 게임산업법(2024-03 개정): 광고·선전물에 "확률형 아이템 포함" 표시. 게임 안 [총알 뽑기] = 보석(원화로 살 수 있음)으로 무작위 총알.
PROB_TEXT = '확률형 아이템 포함'
_badge = {}


def prob_badge(scale=1.0):
    """어두운 반투명 둥근 띠 + 크림 글자 (영상·이미지·끝 화면 모두 같은 모양)"""
    if scale not in _badge:
        f = font(int(40 * scale))
        tw = int(f.getlength(PROB_TEXT))
        pw, ph = tw + int(44 * scale), int(64 * scale)
        b = Image.new('RGBA', (pw + 8, ph + 8), (0, 0, 0, 0))
        d = ImageDraw.Draw(b)
        d.rounded_rectangle([4, 4, pw + 3, ph + 3], int(22 * scale), fill=(28, 17, 10, 200), outline=CREAM, width=max(2, int(3 * scale)))
        text_outline(d, (4 + pw // 2, 4 + ph // 2 - int(2 * scale)), PROB_TEXT, f, CREAM, w=max(2, int(3 * scale)))
        _badge[scale] = b
    return _badge[scale]


def badge_pos(W, H, b):
    """세로 = 오른쪽 위 (문구 띠 위) · 정사각/가로 = 왼쪽 위 (문구 쪽, 게임 화면 안 가림)"""
    m = int(26 * min(W, H) / 1080)
    return (W - b.width - m, m) if H > W else (m, m)


def with_badge(im):
    W, H = im.size
    b = prob_badge(round(min(W, H) / 1080, 3))
    im = im.convert('RGBA')
    im.alpha_composite(b, badge_pos(W, H, b))
    return im.convert('RGB')


# ---------------------------------------------------------------- 영상
def render(name, segs, W, H, maker):
    total = sum(s['dur'] for s in segs)
    wav = os.path.join(OUT, f'_{name}.wav')
    mix_audio(segs, total, wav)
    mp4 = os.path.join(OUT, f'{name}.mp4')
    ff = imageio_ffmpeg.get_ffmpeg_exe()
    cmd = [ff, '-y', '-loglevel', 'error', '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-s', f'{W}x{H}', '-r', str(FPS), '-i', '-',
           '-i', wav, '-c:v', 'libx264', '-preset', 'medium', '-crf', '18', '-pix_fmt', 'yuv420p', '-profile:v', 'high',
           '-c:a', 'aac', '-b:a', '160k', '-shortest', '-movflags', '+faststart', mp4]
    p = subprocess.Popen(cmd, stdin=subprocess.PIPE)
    thumb = None
    for s in segs:
        nf = int(round(s['dur'] * FPS))
        for i in range(nf):
            im = maker(s, i / FPS)
            if im.size != (W, H): im = im.resize((W, H))
            im = with_badge(im)
            p.stdin.write(im.tobytes())
            if thumb is None and i == int(0.6 * FPS): thumb = im
    p.stdin.close(); p.wait()
    os.remove(wav)
    if thumb: thumb.save(os.path.join(OUT, f'{name}_썸네일.jpg'), quality=92)
    print(f'{name}.mp4  {W}x{H}  {total:.1f}초  {os.path.getsize(mp4) / 1e6:.1f}MB')


def main():
    os.makedirs(OUT, exist_ok=True)
    for c in ('a_horde', 'b_upgrade', 'c_merge', 'd_boss', 'e_high', 'f_mine', 'g_skin'):
        if not os.path.isdir(os.path.join(AD, c)): raise SystemExit('촬영 장면 없음: ' + c + ' — 먼저 PlayTest.RunAd')
    render('광고_세로_15초_1080x1920', EDITS['15초'], 1080, 1920, vertical)
    render('광고_세로_25초_1080x1920', EDITS['25초'], 1080, 1920, vertical)
    render('광고_정사각_15초_1080x1080', EDITS['15초'], 1080, 1080, lambda s, t: side_layout(s, t, 1080, 1080))
    render('광고_가로_15초_1920x1080', EDITS['15초'], 1920, 1080, lambda s, t: side_layout(s, t, 1920, 1080))


if __name__ == '__main__':
    main()
