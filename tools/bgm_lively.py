# -*- coding: utf-8 -*-
"""11-15 사용자 "스테이지마다 조금씩 다른 배경음 · 지금보다 활발하고 경쾌하게 · 지금 멜로디는 맘에 듦"
→ C "낮잠 경쾌" 멜로디·화음 그대로, 편곡만 다른 변주 4곡 — 스테이지가 바뀔 때마다 돌아가며 (스테이지 번호 % 4).
AudioManager.cs 의 LivelyData(변주) 와 같은 식 — 숫자를 바꾸면 둘 다 바꿀 것.
  공통: 조용한 도입 없이 처음부터 드럼 · 킥 1·3박 + 손뼉 2·4박 + 엇박 쉐이커 · 8분음 통통 베이스 · 구간 끝 손뼉 채우기
  변주: 1 햇살(F장조 138 · 오르골 + 높은 종) · 2 통통(G장조 144 · 짧은 뜯는 소리 · 마림바 8분) ·
        3 산들(D장조 132 · 마림바 멜로디 + 높은 종 · 4분 베이스) · 4 신나(A장조 150 · 부드러운 8비트 소리 · 킥 네 박)
사용: PYTHONUTF8=1 python tools/bgm_lively.py → 사운드_검토_wav/bgm_v1~4.m4a + tools/_bgm_lively.json (배율 = C# GAIN)
"""
import json
import os
import subprocess

import numpy as np

from bgm_cute import hz, ring_add, noise, marimba, bass, pad, kick, shaker, Dm, Bb
from sound_review import stage_bgm, measure, write_wav, BGM_VOL, SR

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')
OUT = os.path.join(ROOT, '사운드_검토_wav')
TARGET = -42.5      # 체감 크기: 예전 곡(-41.9)보다 조금 작게, 잔잔한 후보(-43.9)보다 크게


def lead(kind, f, gain, decay):
    sec = min(1.2, 6.0 / decay)
    n = int(SR * sec); t = np.arange(n) / SR; w = 2 * np.pi * f * t
    if kind == 'bell':
        v = np.sin(w) + 0.35 * np.sin(2 * w) * np.exp(-2 * t) + 0.12 * np.sin(3 * w) * np.exp(-6 * t)
    elif kind == 'pluck':
        v = np.sin(w) + 0.5 * np.sin(2 * w) * np.exp(-8 * t) + 0.25 * np.sin(3 * w) * np.exp(-12 * t)
    elif kind == 'chip':
        v = np.sin(w) + 0.33 * np.sin(3 * w) + 0.2 * np.sin(5 * w)
    else:   # mari
        v = np.sin(w) + 0.25 * np.sin(4 * w) * np.exp(-30 * t)
    return v * np.minimum(1, t / 0.003) * np.exp(-decay * t) * gain


def glock(f, gain):
    n = int(SR * 0.8); t = np.arange(n) / SR
    return np.sin(2 * np.pi * f * t) * np.minimum(1, t / 0.002) * np.exp(-6 * t) * gain


def clap(gain=0.06, seed=79):
    n = int(SR * 0.11); t = np.arange(n) / SR
    x = noise(n, seed); hp = x - np.concatenate([[0], x[:-1]])
    flutter = np.where(t < 0.024, 0.6 + 0.4 * np.cos(2 * np.pi * t / 0.008), 1.0)
    return (hp * 0.7 + x * 0.3) * np.exp(-28 * t) * flutter * gain


BASS8 = [(0, 0, 1, .20), (1, 2, 1, .10), (2, 0, 1, .18), (3, 2, 1, .10), (4, 1, 1, .18), (5, 2, 1, .10), (6, 1, 1, .16), (7, 2, 1, .10)]
BASS4 = [(0, 0, 2, .20), (3, 2, 1, .10), (4, 1, 2, .16), (6, 2, 1, .10)]

# 변주: 이름, 올림(반음), BPM, 멜로디 소리, 소리 줄어듦, 높은 종 세기, 드럼(한 마디 8칸), 베이스, 마림바 8분 전부?, 킥 세기
VARIANTS = [
    ('햇살', 0, 138, 'bell', 5.0, 0.30, 'khchkhch', BASS8, False, 0.24),
    ('통통', 2, 144, 'pluck', 7.0, 0.00, 'kkchkhch', BASS8, True, 0.24),
    ('산들', -3, 132, 'mari', 6.0, 0.35, 'khthkhch', BASS4, False, 0.22),
    ('신나', 4, 150, 'chip', 4.0, 0.25, 'khKhkhKh', BASS8, True, 0.26),
]
FILL = 'khchcccc'
INTRO = 'kh.hkh.h'

# C "낮잠 경쾌" 멜로디 (bgm_cute.song_c 와 같음)
FH, CH, AMH = [53, 57, 60], [48, 52, 55], [57, 60, 64]
M1 = [72, 0, 74, 69, 72, 0, 77, 0, 74, 0, 76, 72, 69, 0, 72, 0, 70, 0, 74, 0, 77, 0, 74, 72, 72, 0, 67, 69, 67, 0, 0, 0]
M3 = [74, 0, 72, 0, 70, 0, 72, 74, 72, 0, 74, 0, 76, 0, 79, 0, 76, 0, 72, 0, 69, 0, 72, 0, 74, 0, 72, 0, 69, 0, 0, 0]
M4 = [70, 0, 74, 0, 77, 0, 74, 0, 79, 0, 76, 0, 72, 0, 74, 76, 77, 0, 72, 0, 69, 0, 72, 0, 77, 0, 0, 0, 0, 0, 0, 0]
M0 = [x if i % 4 == 0 else 0 for i, x in enumerate(M1)]
SECS = [([FH, Dm, Bb, CH], M0, False), ([FH, Dm, Bb, CH], M1, False), ([Bb, CH, AMH, Dm], M3, False),
        ([FH, Dm, Bb, CH], M1, True), ([Bb, CH, FH, FH], M4, True)]     # (화음, 멜로디, 마지막 마디 손뼉 채우기)


def lively(v):
    name, tr, bpm, lk, ld, octg, drum, bpat, arp8, kg = VARIANTS[v]
    m = int(np.ceil(SR * 60.0 / bpm / 2))
    steps = sum(len(s[1]) for s in SECS)
    data = np.zeros(m * steps)
    K, CL, SH, TK = kick(kg), clap(), shaker(0.04), None
    from bgm_cute import tick
    TK = tick()
    st = 0
    for si, (chords, mel, fill) in enumerate(SECS):
        for b, ch0 in enumerate(chords):
            ch = [x + tr for x in ch0]
            o = (st + b * 8) * m
            ring_add(data, pad([hz(x) for x in ch], 8 * m / SR, 0.03), o)
            for k, w, ln, g in bpat:
                n = ch[0] - 12 if w == 0 else ch[2] - 24 if w == 1 else ch[0]
                ring_add(data, bass(hz(n), ln * m / SR, g), o + k * m)
            ar = [ch[0] + 12, ch[1] + 12, ch[2] + 12, ch[1] + 12]
            for j in range(8 if arp8 else 4):
                k = j if arp8 else 1 + 2 * j
                ring_add(data, marimba(hz(ar[j % 4])), o + k * m)
        for k, n in enumerate(mel):
            at = (st + k) * m
            if n > 0:
                ring_add(data, lead(lk, hz(n + tr), 0.16, ld), at)
                if octg > 0:
                    ring_add(data, glock(hz(n + tr + 12), 0.16 * octg), at)
            last = k >= len(mel) - 8
            pat = INTRO if si == 0 else (FILL if fill and last else drum)
            d = pat[k % 8]
            if d in 'kK': ring_add(data, K, at)
            if d in 'cK': ring_add(data, CL, at)
            if d == 'h': ring_add(data, SH, at)
            if d == 't': ring_add(data, TK, at)
        st += len(mel)
    return data


def main():
    os.makedirs(OUT, exist_ok=True)
    ref = measure(stage_bgm(), BGM_VOL)
    res = {}
    ff = None
    try:
        import imageio_ffmpeg
        ff = imageio_ffmpeg.get_ffmpeg_exe()
    except Exception:
        pass
    for v in range(len(VARIANTS)):
        d = lively(v)
        gain = 10 ** ((TARGET - measure(d, BGM_VOL)['loud_db']) / 20)
        d = np.clip(d * gain, -1, 1)
        mm = measure(d, BGM_VOL)
        res[f'v{v + 1}'] = dict(name=VARIANTS[v][0], bpm=VARIANTS[v][2], gain=round(float(gain), 3), loop_sec=round(len(d) / SR, 1),
                               loud_db=float(mm['loud_db']), phone=mm['phone'], peak_db=float(mm['peak_db']), seam=round(abs(float(d[-1] - d[0])), 4))
        w = os.path.join(OUT, f'bgm_v{v + 1}.wav')
        write_wav(w, np.tile(d, 2) * BGM_VOL)
        if ff:
            subprocess.run([ff, '-y', '-loglevel', 'error', '-i', w, '-c:a', 'aac', '-b:a', '128k', w[:-4] + '.m4a'], check=True)
            os.remove(w)
        print(f'v{v + 1}', res[f'v{v + 1}'])
    json.dump(res, open(os.path.join(ROOT, 'tools', '_bgm_lively.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)


if __name__ == '__main__':
    main()
