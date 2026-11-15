# -*- coding: utf-8 -*-
"""11-15 사용자 "배경음악을 조금 더 힐링되고 귀여운 음악으로" — 평소 전투 배경음 후보를 코드 합성으로 만들어 듣고 재 보기.
AudioManager.cs 의 SoftSong(새 엔진)과 같은 식 — 숫자를 바꾸면 둘 다 바꿀 것.
  악기: 오르골(사인 + 2·3배음, 길게 울림) · 화음 패드(사인 셋, 천천히 켜짐) · 통통 베이스(사인 + 2배음, 1·3박 근음/5음)
        · 마림바 분산화음(엇박) · 아주 약한 킥/쉐이커/틱
  곡: A "봄 산책" C장조 92 BPM · B "낮잠" F장조 84 BPM · C "낮잠 경쾌" F장조 126 BPM (11-15 사용자가 B + "경쾌하게 · 1.5배") — 4구간(처음 조용히 → 본 멜로디 → 새 진행 → 마무리), 반복 이음새 없음
사용: PYTHONUTF8=1 python tools/bgm_cute.py  → 사운드_검토_wav/bgm_*.wav + 배경음_후보.html (들어 보기 · 체크·의견)
"""
import json
import os

import numpy as np

from sound_review import stage_bgm, measure, write_wav, BGM_VOL, SR, env_points

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')
OUT = os.path.join(ROOT, '사운드_검토_wav')


def hz(m): return 440.0 * 2 ** ((m - 69) / 12.0)


def ring_add(data, src, at):
    idx = (at + np.arange(len(src))) % len(data)
    np.add.at(data, idx, src)


def noise(n, seed):
    return np.random.RandomState(seed).uniform(-1, 1, n)


# ---------------- 악기 (AudioManager.cs SoftSong 과 같은 식) ----------------
def bell(f, sec=1.3, gain=0.17):
    n = int(SR * sec); t = np.arange(n) / SR
    att = np.minimum(1, t / 0.004)
    v = np.sin(2 * np.pi * f * t) + 0.35 * np.sin(4 * np.pi * f * t) * np.exp(-2 * t) + 0.12 * np.sin(6 * np.pi * f * t) * np.exp(-6 * t)
    return v * att * np.exp(-3.2 * t) * gain


def marimba(f, sec=0.45, gain=0.05):
    n = int(SR * sec); t = np.arange(n) / SR
    v = np.sin(2 * np.pi * f * t) + 0.25 * np.sin(8 * np.pi * f * t) * np.exp(-30 * t)
    return v * np.minimum(1, t / 0.003) * np.exp(-9 * t) * gain


def bass(f, sec, gain=0.20):
    n = int(SR * sec); t = np.arange(n) / SR
    v = np.sin(2 * np.pi * f * t) + 0.35 * np.sin(4 * np.pi * f * t)
    rel = np.minimum(1, (sec - t) / 0.03)
    return v * np.minimum(1, t / 0.005) * np.exp(-3.0 * t / sec) * rel * gain


def pad(fs, sec, gain=0.035):
    n = int(SR * sec); t = np.arange(n) / SR
    env = np.minimum(1, t / 0.25) * np.minimum(1, (sec - t) / 0.2)
    return sum(np.sin(2 * np.pi * f * t) for f in fs) * env * gain


def kick(gain=0.18):
    n = int(SR * 0.12); t = np.arange(n) / n
    ph = np.cumsum((110 + (50 - 110) * t) / SR)
    return np.sin(2 * np.pi * ph) * np.exp(-5 * t) * gain


def shaker(gain=0.035, seed=71):
    n = int(SR * 0.05); t = np.arange(n) / n
    x = noise(n, seed); x = x - np.concatenate([[0], x[:-1]])         # 높은 소리만 (차분)
    return x * np.exp(-7 * t) * gain


def tick(gain=0.05, seed=73):
    n = int(SR * 0.03); t = np.arange(n) / n
    x = noise(n, seed); x = x - np.concatenate([[0], x[:-1]])
    return (x * 0.6 + np.sin(2 * np.pi * 1800 * np.arange(n) / SR)) * np.exp(-12 * t) * gain


# ---------------- 곡 ----------------
def soft_song(bpm, sections, target_loud):
    step = 60.0 / bpm / 2
    m = int(np.ceil(SR * step))
    steps = sum(len(s['mel']) for s in sections)
    data = np.zeros(m * steps)
    K, SH, TK = kick(), shaker(), tick()
    st = 0
    for s in sections:
        for b, ch in enumerate(s['chords']):
            o = (st + b * 8) * m
            fs = [hz(x) for x in ch]
            if s.get('pad', True):
                ring_add(data, pad(fs, 8 * m / SR), o)
            # 베이스 무늬: (칸, 음 0 = 근음 · 1 = 낮은 5음 · 2 = 한 옥타브 위 근음, 길이 칸, 세기)
            for k, w, ln, g in s.get('bass', BASS_SLOW):
                n = ch[0] - 12 if w == 0 else ch[2] - 24 if w == 1 else ch[0]
                ring_add(data, bass(hz(n), ln * m / SR, g), o + k * m)
            if s.get('arp', True):
                ar = [ch[0] + 12, ch[1] + 12, ch[2] + 12, ch[1] + 12]
                for j, k in enumerate((1, 3, 5, 7)):
                    ring_add(data, marimba(hz(ar[j])), o + k * m)
        for k, n in enumerate(s['mel']):
            if n > 0:
                ring_add(data, bell(hz(n)), (st + k) * m)
            d = s['drums'][k % len(s['drums'])]
            if d == 'k': ring_add(data, K, (st + k) * m)
            elif d == 's': ring_add(data, SH, (st + k) * m)
            elif d == 't': ring_add(data, TK, (st + k) * m)
        st += len(s['mel'])
    # 크기 맞추기: 고정 배율(GAIN) — C# 도 같은 숫자를 곱함
    return data


# MIDI: C4 60 · D 62 · E 64 · F 65 · G 67 · A 69 · B 71 · C5 72 …
C, Am, F, G, Em, Dm, Bb = [60, 64, 67], [57, 60, 64], [53, 57, 60], [55, 59, 62], [52, 55, 59], [50, 53, 57], [58, 62, 65]
DR_QUIET, DR_MAIN, DR_B = '........', 'k.s.t.s.', 'k.s.tss.'


def song_a():
    """A 봄 산책: C장조 92 BPM · C-Am-F-G / F-G-Em-Am / F-G-C-C"""
    m1 = [76, 0, 79, 0, 84, 0, 79, 0, 81, 0, 79, 76, 0, 0, 72, 0, 77, 0, 81, 0, 84, 0, 81, 79, 79, 0, 74, 0, 76, 74, 0, 0]
    m0 = [x if i % 4 == 0 else 0 for i, x in enumerate(m1)]
    m3 = [84, 0, 81, 0, 77, 0, 81, 0, 83, 0, 79, 0, 74, 0, 79, 0, 79, 0, 76, 0, 71, 0, 76, 79, 81, 0, 0, 0, 76, 0, 72, 0]
    m4 = [81, 79, 77, 0, 81, 0, 84, 0, 83, 81, 79, 0, 86, 0, 83, 0, 84, 0, 79, 0, 76, 0, 79, 0, 84, 0, 0, 0, 0, 0, 0, 0]
    secs = [dict(chords=[C, Am, F, G], mel=m0, drums=DR_QUIET, arp=False),
            dict(chords=[C, Am, F, G], mel=m1, drums=DR_MAIN),
            dict(chords=[F, G, Em, Am], mel=m3, drums=DR_B),
            dict(chords=[F, G, C, C], mel=m4, drums=DR_MAIN)]
    return 92, secs


BASS_SLOW = [(0, 0, 4, 0.20), (4, 1, 4, 0.16)]                                    # A·B: 1·3박
BASS_BOUNCE = [(0, 0, 2, 0.20), (3, 2, 1, 0.10), (4, 1, 2, 0.16), (6, 2, 1, 0.10)]   # C: 통통 (엇박에 옥타브 위)


def song_c():
    """C 낮잠 경쾌 (11-15 사용자: B 를 골라 "조금 더 경쾌하게 · 속도 1.5배"): F장조 126 BPM · B 진행 그대로 + 구간 하나 더(20마디 38초)
    드럼 1·3박 킥 + 2·4박 틱 + 엇박 쉐이커 · 베이스 통통 · 멜로디 빈 곳을 채움"""
    Fh, Ch, Amh = [53, 57, 60], [48, 52, 55], [57, 60, 64]
    m1 = [72, 0, 74, 69, 72, 0, 77, 0, 74, 0, 76, 72, 69, 0, 72, 0, 70, 0, 74, 0, 77, 0, 74, 72, 72, 0, 67, 69, 67, 0, 0, 0]
    m0 = [x if i % 4 == 0 else 0 for i, x in enumerate(m1)]
    m3 = [74, 0, 72, 0, 70, 0, 72, 74, 72, 0, 74, 0, 76, 0, 79, 0, 76, 0, 72, 0, 69, 0, 72, 0, 74, 0, 72, 0, 69, 0, 0, 0]
    m4 = [70, 0, 74, 0, 77, 0, 74, 0, 79, 0, 76, 0, 72, 0, 74, 76, 77, 0, 72, 0, 69, 0, 72, 0, 77, 0, 0, 0, 0, 0, 0, 0]
    D = 'kstsksts'
    secs = [dict(chords=[Fh, Dm, Bb, Ch], mel=m0, drums='k...t...', arp=False, bass=BASS_SLOW),
            dict(chords=[Fh, Dm, Bb, Ch], mel=m1, drums=D, bass=BASS_BOUNCE),
            dict(chords=[Bb, Ch, Amh, Dm], mel=m3, drums=D, bass=BASS_BOUNCE),
            dict(chords=[Fh, Dm, Bb, Ch], mel=m1, drums='kstskstt', bass=BASS_BOUNCE),
            dict(chords=[Bb, Ch, Fh, Fh], mel=m4, drums=D, bass=BASS_BOUNCE)]
    return 126, secs


def song_b():
    """B 낮잠: F장조 84 BPM · F-Dm-Bb-C / Bb-C-Am-Dm / Bb-C-F-F (더 느리고 낮게)"""
    Fh, Ch, Amh = [53, 57, 60], [48, 52, 55], [57, 60, 64]
    m1 = [72, 0, 0, 69, 72, 0, 77, 0, 74, 0, 0, 72, 69, 0, 0, 0, 70, 0, 74, 0, 77, 0, 74, 0, 72, 0, 0, 0, 67, 0, 0, 0]
    m0 = [x if i % 8 in (0, 4) else 0 for i, x in enumerate(m1)]
    m3 = [74, 0, 72, 0, 70, 0, 0, 0, 72, 0, 74, 0, 76, 0, 0, 0, 76, 0, 72, 0, 69, 0, 0, 0, 74, 0, 72, 0, 69, 0, 0, 0]
    m4 = [70, 0, 74, 0, 77, 0, 74, 0, 79, 0, 76, 0, 72, 0, 0, 0, 77, 0, 72, 0, 69, 0, 72, 0, 77, 0, 0, 0, 0, 0, 0, 0]
    secs = [dict(chords=[Fh, Dm, Bb, Ch], mel=m0, drums=DR_QUIET, arp=False),
            dict(chords=[Fh, Dm, Bb, Ch], mel=m1, drums='k...t...'),
            dict(chords=[Bb, Ch, Amh, Dm], mel=m3, drums='k.s.t.s.'),
            dict(chords=[Bb, Ch, Fh, Fh], mel=m4, drums='k...t...')]
    return 84, secs


def main():
    os.makedirs(OUT, exist_ok=True)
    old = stage_bgm()
    ref = measure(old, BGM_VOL)
    res = {'old': dict(ref, loop_sec=round(len(old) / SR, 1))}
    write_wav(os.path.join(OUT, 'bgm_0_old.wav'), np.tile(old, 2) * BGM_VOL)
    for key, fn in (('a', song_a), ('b', song_b), ('c', song_c)):
        bpm, secs = fn()
        d = soft_song(bpm, secs, None)
        # 예전 곡보다 체감 크기 2dB 작게 (힐링 · 효과음이 더 잘 들리게) → 배율을 정해 C# 에 그대로 적음
        cur = measure(d, BGM_VOL)['loud_db']
        gain = 10 ** ((ref['loud_db'] - 2.0 - cur) / 20)
        d = np.clip(d * gain, -1, 1)
        mm = measure(d, BGM_VOL)
        res[key] = dict(mm, gain=round(float(gain), 3), bpm=bpm, loop_sec=round(len(d) / SR, 1), seam=round(abs(float(d[-1] - d[0])), 4), env=env_points(d, 200))
        write_wav(os.path.join(OUT, f'bgm_{key}.wav'), np.tile(d, 2) * BGM_VOL)
    json.dump(res, open(os.path.join(ROOT, 'tools', '_bgm_cute.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    for k, v in res.items():
        print(k, {x: v[x] for x in ('loop_sec', 'peak_db', 'rms_db', 'loud_db', 'centroid', 'phone') if x in v}, v.get('gain'), v.get('seam'))
    # 듣기 문서(배경음_후보.html)용: wav(7MB) → m4a(1.3MB)
    import subprocess, imageio_ffmpeg
    ff = imageio_ffmpeg.get_ffmpeg_exe()
    for k in ('0_old', 'a', 'b', 'c'):
        w = os.path.join(OUT, f'bgm_{k}.wav')
        subprocess.run([ff, '-y', '-loglevel', 'error', '-i', w, '-c:a', 'aac', '-b:a', '128k', w[:-4] + '.m4a'], check=True)
        os.remove(w)


if __name__ == '__main__':
    main()
