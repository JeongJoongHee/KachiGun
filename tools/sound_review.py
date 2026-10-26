# -*- coding: utf-8 -*-
"""AudioManager.cs 의 코드 합성 소리를 그대로 따라 만들어 wav 로 뽑고 측정한다 (소리 검토용).
사용: PYTHONUTF8=1 python tools/sound_review.py
 - 결과: 사운드_검토_wav/*.wav (효과음 16 · 배경음 2 · 상황 섞기 5) + tools/_sound_metrics.json
 - 노이즈는 .NET System.Random(12345) 대신 numpy 난수 (소리 느낌은 같음, 숫자 한 자리까지 같지는 않음)
 - AudioManager.cs 의 숫자를 바꾸면 아래 SFX·BGM 표도 같이 바꿀 것
"""
import json, os, wave
import numpy as np

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')
OUT = os.path.join(ROOT, '사운드_검토_wav')
SR = 44100
BGM_VOL, SFX_VOL, CHANNELS = 0.22, 0.48, 16


# ---------------- 합성 (AudioManager.cs 와 같은 식) ----------------
def wave_(phase, kind):
    p = phase - np.floor(phase)
    if kind == 'sine': return np.sin(p * np.pi * 2)
    if kind == 'square': return np.where(p < 0.5, 1.0, -1.0)
    if kind == 'saw': return 2 * p - 1
    if kind == 'tri': return 1 - 4 * np.abs(np.round(p - 0.25) - (p - 0.25))
    raise ValueError(kind)


def make_clip(d):
    d = d.copy()
    f = min(len(d), int(np.ceil(SR * 0.003)))
    d[:f] *= np.arange(f) / f
    d[len(d) - f:] *= (np.arange(f) / f)[::-1]
    return d


def sweep(f0, f1, dur, kind, gain):
    n = int(np.ceil(SR * dur)); t = np.arange(n) / n
    ph = np.cumsum((f0 + (f1 - f0) * t) / SR)
    return wave_(ph, kind) * np.exp(-4 * t) * gain


def sweep_noise(f0, f1, dur, noise, gain, seed=12345):
    n = int(np.ceil(SR * dur)); t = np.arange(n) / n
    ph = np.cumsum((f0 + (f1 - f0) * t) / SR)
    nz = np.random.default_rng(seed).random(n) * 2 - 1
    return (wave_(ph, 'saw') * (1 - noise) + nz * noise) * np.exp(-5 * t) * gain


def arp(notes, step, kind, gain, last_hold=1.0):
    m = int(np.ceil(SR * step)); lm = int(np.ceil(m * last_hold))
    out, phase = [], 0.0
    for k, f in enumerate(notes):
        last = k == len(notes) - 1
        n = lm if last else m; dec = 2.5 / last_hold * 1.6 if last else 2.5
        t = np.arange(n) / n
        ph = phase + np.cumsum(np.full(n, f / SR)); phase = ph[-1]
        out.append(wave_(ph, kind) * np.exp(-dec * t) * gain)
    return np.concatenate(out)


def mix(a, b):
    r = np.zeros(max(len(a), len(b))); r[:len(a)] += a; r[:len(b)] += b
    return np.clip(r, -1, 1)


def noise_burst(dur, decay, gain, high, seed=777):
    n = int(np.ceil(SR * dur)); x = np.random.default_rng(seed).random(n) * 2 - 1
    v = (x - np.concatenate([[0], x[:-1]])) * 0.5 if high else x
    return v * np.exp(-decay * np.arange(n) / SR) * gain


def thump(dur, f0, f1, gain):
    n = int(np.ceil(SR * dur)); t = np.arange(n) / n
    ph = np.cumsum((f0 + (f1 - f0) * np.sqrt(t)) / SR)
    return np.sin(ph * 2 * np.pi) * np.exp(-6 * t) * gain


def octv(notes, k=2): return [x * k for x in notes]


DRUM_LIGHT, DRUM_FULL, DRUM_HALF, DRUM_DRIVE = '.h.h.h.h', 'khshkhsh', 'k...h...', 'khshkkhs'


def song(step_dur, sections, lead_kind):
    m = int(np.ceil(SR * step_dur)); steps = sum(len(s[0]) for s in sections); total = m * steps
    data = np.zeros(total); bp = lp = 0.0
    ef = min(m // 4, int(np.ceil(SR * 0.002))); i = np.arange(m); t = i / m
    edge = np.where(i < ef, i / ef, np.where(i > m - ef, (m - i) / ef, 1.0))
    kick = thump(0.14, 150, 45, 0.32)
    snare = mix(noise_burst(0.12, 28, 0.16, False, 31), sweep(200, 160, 0.06, 'tri', 0.08))
    hat = noise_burst(0.04, 90, 0.10, True, 57)
    step = 0
    for bass, lead, drums, ld in sections:
        for k in range(len(bass)):
            o = step * m; v = np.zeros(m)
            if bass[k] > 0:
                ph = bp + np.cumsum(np.full(m, bass[k] / SR)); bp = ph[-1]
                v += wave_(ph, 'saw') * np.exp(-3.5 * t) * 0.30 * edge
            if lead[k] > 0:
                ph = lp + np.cumsum(np.full(m, lead[k] / SR)); lp = ph[-1]
                v += wave_(ph, lead_kind) * np.exp(-ld * t) * 0.20 * edge
            data[o:o + m] += v
            d = drums[k % len(drums)]
            src = {'k': kick, 's': snare, 'h': hat}.get(d)
            if src is not None:
                idx = (o + np.arange(len(src))) % total
                np.add.at(data, idx, src)
            step += 1
    return np.clip(data, -1, 1)


def R(f, n): return [f] * n


def stage_bgm():
    bassA = R(110, 8) + R(87.31, 8) + R(130.81, 8) + R(98, 4) + R(82.41, 4)
    leadA = [440, 523.25, 659.25, 523.25, 440, 523.25, 659.25, 587.33, 349.23, 440, 523.25, 440, 349.23, 440, 523.25, 659.25,
             523.25, 659.25, 783.99, 659.25, 523.25, 659.25, 587.33, 523.25, 392, 493.88, 587.33, 493.88, 329.63, 493.88, 440, 329.63]
    bassB = R(73.42, 8) + R(110, 8) + R(87.31, 8) + R(82.41, 8)
    leadB = [587.33, 0, 698.46, 0, 880, 698.46, 587.33, 0, 659.25, 0, 523.25, 0, 440, 523.25, 659.25, 0,
             698.46, 0, 880, 0, 1046.5, 880, 698.46, 0, 659.25, 0, 830.61, 0, 987.77, 830.61, 659.25, 0]
    leadA2 = [leadA[i] * 2 if i % 2 == 0 else 0 for i in range(32)]
    bassC = [bassA[i] if i % 2 == 0 else 0 for i in range(32)]
    leadC = [leadA[i] if i % 8 == 0 else 0 for i in range(32)]
    return song(0.25, [(bassA, leadA, DRUM_LIGHT, 4.2), (bassA, leadA, DRUM_FULL, 4.2), (bassB, leadB, DRUM_FULL, 4.2),
                       (bassA, leadA2, DRUM_DRIVE, 4.2), (bassC, leadC, DRUM_HALF, 1.2)], 'tri')


def boss_bgm():
    bassA = [110] * 6 + [130.81, 110] + [103.83] * 6 + [123.47, 103.83] + [98] * 6 + [116.54, 98] + [82.41, 82.41, 87.31, 87.31, 98, 98, 82.41, 82.41]
    leadA = [440, 659.25, 523.25, 659.25, 440, 659.25, 587.33, 523.25, 415.30, 659.25, 493.88, 659.25, 415.30, 659.25, 587.33, 493.88,
             392, 587.33, 466.16, 587.33, 392, 587.33, 523.25, 466.16, 329.63, 493.88, 349.23, 523.25, 392, 587.33, 493.88, 329.63]
    leadA2 = [leadA[i] * 2 if i % 2 == 1 else 0 for i in range(32)]
    bassB = R(110, 8) + R(116.54, 8) + R(110, 8) + R(82.41, 8)
    leadB = [880, 830.61, 880, 0, 659.25, 0, 880, 0, 932.33, 880, 932.33, 0, 698.46, 0, 932.33, 0,
             880, 830.61, 880, 0, 1046.5, 0, 880, 0, 830.61, 0, 659.25, 0, 493.88, 0, 415.30, 0]
    run = [440, 493.88, 523.25, 587.33, 659.25, 698.46, 830.61, 880]
    bassC = [bassA[i] if i % 4 == 0 else 0 for i in range(32)]
    leadC = [run[i - 24] if i >= 24 else 0 for i in range(32)]
    return song(0.2, [(bassA, leadA, DRUM_FULL, 4.2), (bassA, leadA2, DRUM_DRIVE, 4.2), (bassB, leadB, DRUM_FULL, 4.2),
                      (bassA, leadA, DRUM_DRIVE, 4.2), (bassC, leadC, DRUM_HALF, 4.2), (bassA, leadA, DRUM_FULL, 4.2)], 'square')


BOSS_N = [220, 207.65, 196, 185, 220]
BD_N = [196, 261.63, 329.63, 392, 523.25]
FAIL_N = [392, 349.23, 311.13, 261.63]
SFX = {  # 이름: (한국어, 언제, 만들기) — AudioManager.cs GenerateAllProceduralSounds 와 같게 (10-26 다듬은 값)
    'Shot': ('일반 총 발사', '기본·속사·관통탄 한 발마다', lambda: make_clip(sweep_noise(520, 120, 0.075, 0.45, 0.55))),
    'Shotgun': ('산탄 발사', '산탄 한 번 쏠 때', lambda: make_clip(sweep_noise(340, 70, 0.13, 0.65, 0.70))),
    'Laser': ('레이저 발사', '레이저탄', lambda: make_clip(sweep(1350, 260, 0.095, 'saw', 0.45))),
    'Missile': ('미사일 발사', '미사일탄', lambda: make_clip(sweep_noise(280, 95, 0.16, 0.55, 0.55))),
    'Boom': ('미사일 폭발', '미사일이 터질 때', lambda: make_clip(sweep_noise(160, 38, 0.28, 0.70, 0.85))),
    'Hit': ('좀비 맞음', '총알이 좀비에 맞을 때마다', lambda: make_clip(sweep_noise(420, 130, 0.065, 0.5, 0.5))),
    'MineHit': ('광산 보석 때림', '광산에서 보석 맞을 때마다', lambda: make_clip(arp([1174.66, 1567.98], 0.032, 'tri', 0.28))),
    'Dead': ('좀비 죽음', '좀비 한 마리마다', lambda: make_clip(mix(sweep(480, 960, 0.08, 'tri', 0.36), noise_burst(0.05, 40, 0.3, True)))),
    'BossAppear': ('보스 등장 경고', '보스 나올 때 · 던전 시작', lambda: make_clip(mix(mix(arp(octv(BOSS_N), 0.11, 'saw', 0.55), arp(octv(BOSS_N), 0.11, 'square', 0.28)), arp(BOSS_N, 0.11, 'saw', 0.15)))),
    'BossDead': ('보스 처치', '보스 잡았을 때', lambda: make_clip(mix(mix(arp(BD_N, 0.11, 'saw', 0.45), arp(octv(BD_N), 0.11, 'square', 0.32)), thump(0.25, 140, 42, 0.7)))),
    'Upgrade': ('강화 성공', '공격·체력 강화 · 슬롯 강화 · 구매', lambda: make_clip(arp([523.25, 659.25, 783.99], 0.045, 'tri', 0.48))),
    'Merge': ('합성 · 뽑기', '총알 합성 · 뽑기', lambda: make_clip(arp([523.25, 659.25, 783.99, 1046.5], 0.075, 'square', 0.5))),
    'Coin': ('골드·다이아 들어옴', '날아간 돈이 위 칸에 닿을 때마다', lambda: make_clip(arp([987.77, 1318.51], 0.042, 'sine', 0.225))),
    'UiClick': ('버튼 누름', '모든 버튼', lambda: make_clip(arp([659.25, 880], 0.035, 'tri', 0.38))),
    'Victory': ('던전 승리', '던전 깼을 때', lambda: make_clip(arp([523.25, 659.25, 783.99, 1046.5, 1318.51], 0.10, 'square', 0.52, 3.0))),
    'Fail': ('실패', '보스·던전 실패 · 슬롯 강화 실패', lambda: make_clip(mix(mix(arp(FAIL_N, 0.15, 'saw', 0.25), arp(octv(FAIL_N), 0.15, 'square', 0.25)), arp(octv(FAIL_N, 4), 0.15, 'tri', 0.15)))),
}
BGM = {
    'Stage_BGM': ('일반 전투 배경음', stage_bgm),
    'Boss_BGM': ('보스전·던전 배경음', boss_bgm),
}
MIN_GAP = {'Shot': .125, 'Shotgun': .125, 'Laser': .125, 'Missile': .125, 'Hit': .1, 'MineHit': .08, 'Coin': .1, 'Dead': .06, 'Boom': .12}
RANDOM_PITCH = {'Shot', 'Shotgun', 'Laser', 'Hit', 'MineHit'}
COMBAT = {'Shot', 'Shotgun', 'Laser', 'Missile', 'Boom', 'Hit', 'MineHit', 'Dead', 'Coin'}
COMBAT_CH = 10


# ---------------- 측정 ----------------
def a_weight(f):
    f2 = f * f
    ra = (12194 ** 2 * f2 * f2) / ((f2 + 20.6 ** 2) * np.sqrt((f2 + 107.7 ** 2) * (f2 + 737.9 ** 2)) * (f2 + 12194 ** 2) + 1e-30)
    return ra * 10 ** (2.0 / 20)


def db(x): return 20 * np.log10(max(x, 1e-9))


def measure(d, vol):
    x = d * vol
    sp = np.fft.rfft(x * np.hanning(len(x))); f = np.fft.rfftfreq(len(x), 1 / SR); p = np.abs(sp) ** 2
    cen = float((f * p).sum() / (p.sum() + 1e-30))
    rms_a = np.sqrt(np.mean(np.fft.irfft(np.fft.rfft(x) * a_weight(f), len(x)) ** 2))
    phone = float(p[f >= 400].sum() / (p.sum() + 1e-30))
    return {'ms': round(len(d) / SR * 1000), 'peak_db': round(db(np.abs(x).max()), 1), 'rms_db': round(db(np.sqrt(np.mean(x ** 2))), 1),
            'loud_db': round(db(rms_a), 1), 'centroid': round(cen), 'phone': round(phone * 100)}


def env_points(d, n=160):
    k = max(1, len(d) // n)
    return [round(float(np.abs(d[i:i + k]).max()), 3) for i in range(0, len(d), k)][:n]


# ---------------- 게임 속 상황 섞기 (Unity 와 같은 규칙: 종류별 최소 간격 · 전투 10채널 + 중요한 소리 6채널 · 빈 채널 없으면 안 남) ----------------
class Mixer:
    def __init__(self, sec, clips, bgm=None):
        self.out = np.zeros(int(SR * sec)); self.clips = clips; self.busy = [0.0] * CHANNELS; self.ch = 0
        self.last = {}; self.coin_p, self.coin_t = 1.0, -9; self.rng = np.random.default_rng(7)
        self.dropped = {}; self.played = {}; self.max_voices = 0; self.voices = []
        if bgm is not None:
            rep = np.tile(bgm, int(np.ceil(len(self.out) / len(bgm))))[:len(self.out)]
            self.out += rep * BGM_VOL

    def free(self, t, lo, hi):
        n = hi - lo; start = min(max(self.ch, lo), hi - 1) - lo
        for i in range(n):
            j = lo + (start + i) % n
            if self.busy[j] <= t: return j
        return None

    def play(self, name, t):
        gap = t - self.last.get(name, -99)
        if gap < MIN_GAP.get(name, .02): return
        self.last[name] = t
        combat = name in COMBAT
        idx = self.free(t, 0, COMBAT_CH) if combat else self.free(t, COMBAT_CH, CHANNELS)
        if idx is None and not combat: idx = self.free(t, 0, CHANNELS)
        if idx is None: self.dropped[name] = self.dropped.get(name, 0) + 1; return
        self.ch = idx
        vol = SFX_VOL
        if name in ('Shot', 'Shotgun', 'Laser', 'Missile'):
            vol *= 0.65 + 0.35 * min(1, max(0, (gap - .125) / .125))
        pitch = 1.0
        if name == 'Coin':
            if t - self.coin_t < .45:
                self.coin_p += .04
                if self.coin_p > 1.55: self.coin_p = 1.0
            else: self.coin_p = 1.0
            self.coin_t = t; pitch = self.coin_p
        elif name in RANDOM_PITCH: pitch = self.rng.uniform(.92, 1.08)
        c = self.clips[name]
        if pitch != 1.0:
            c = np.interp(np.arange(0, len(c) - 1, pitch), np.arange(len(c)), c)
        self.busy[idx] = t + len(c) / SR
        s = int(t * SR); e = min(len(self.out), s + len(c))
        if e <= s: return
        self.out[s:e] += c[:e - s] * vol
        self.played[name] = self.played.get(name, 0) + 1
        self.max_voices = max(self.max_voices, sum(1 for b in self.busy if b > t))

    def result(self):
        x = self.out; over = int((np.abs(x) > 1.0).sum())
        return {'peak_db': round(db(np.abs(x).max()), 1), 'clip_ms': round(over / SR * 1000, 1), 'max_voices': self.max_voices,
                'dropped': self.dropped, 'played': self.played, 'coin_pitch_end': round(self.coin_p, 2)}


def scenario_battle(clips, bgm, sec, capys, interval, kinds, kill_every, boss_at=None):
    m = Mixer(sec, clips, bgm)
    ev = []
    for c in range(capys):
        t = 0.1 + c * interval / capys
        while t < sec - .3:
            k = kinds[c % len(kinds)]
            ev.append((t, k)); ev.append((t + .12, 'Hit'))
            if k == 'Missile': ev.append((t + .25, 'Boom'))
            t += interval
    t = .5
    while t < sec - .8:
        ev.append((t, 'Dead')); ev.append((t + .6, 'Coin')); t += kill_every
    if boss_at: ev.append((boss_at, 'BossAppear'))
    for t, k in sorted(ev): m.play(k, t)
    return m


def write_wav(path, d):
    d = np.clip(d, -1, 1)
    with wave.open(path, 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((d * 32767).astype('<i2').tobytes())


def main():
    os.makedirs(OUT, exist_ok=True)
    res = {'sfx': {}, 'bgm': {}, 'mix': {}}
    clips = {k: v[2]() for k, v in SFX.items()}
    for k, (ko, when, _) in SFX.items():
        write_wav(os.path.join(OUT, k + '.wav'), clips[k] * SFX_VOL)
        res['sfx'][k] = dict(ko=ko, when=when, **measure(clips[k], SFX_VOL), env=env_points(clips[k]))
    bgms = {k: v[1]() for k, v in BGM.items()}
    for k, (ko, _) in BGM.items():
        d = bgms[k]
        write_wav(os.path.join(OUT, k + '_x4.wav'), np.tile(d, 4 if len(d) < SR * 15 else 2) * BGM_VOL)   # 반복해서 이음새도 들리게 (긴 곡은 2번)
        seam = abs(float(d[-1] - d[0]))
        res['bgm'][k] = dict(ko=ko, **measure(d, BGM_VOL), loop_sec=round(len(d) / SR, 2), seam=round(seam, 4), env=env_points(d, 320))

    mixes = {
        'mix1_early': ('초반 전투 10초', '카피바라 2마리 · 기본탄 0.6초마다 · 좀비 1.2초에 1마리', scenario_battle(clips, bgms['Stage_BGM'], 10, 2, 0.6, ['Shot'], 1.2)),
        'mix2_mid': ('중반 전투 10초', '분대 3 · 기본·산탄·레이저 0.25초마다 · 좀비 0.4초에 1마리', scenario_battle(clips, bgms['Stage_BGM'], 10, 3, 0.25, ['Shot', 'Shotgun', 'Laser'], 0.4)),
        'mix3_late': ('후반 전투 + 보스 등장 10초', '분대 3 · 공속 넘침 0.06초 · 미사일 포함 · 5초에 보스 등장 (보스 배경음)', scenario_battle(clips, bgms['Boss_BGM'], 10, 3, 0.06, ['Shot', 'Laser', 'Missile'], 0.15, boss_at=5.0)),
    }
    m = Mixer(6, clips, bgms['Stage_BGM'])
    for i in range(int(6 / .05)): m.play('MineHit', .1 + i * .05)
    mixes['mix4_mine'] = ('광산 6초', '보석을 초당 20번 때릴 때', m)
    m = Mixer(8, clips, bgms['Stage_BGM'])
    for i in range(30): m.play('Coin', .2 + i * .22)
    mixes['mix5_coin'] = ('돈 연속 8초', '좀비를 계속 잡아 돈이 0.22초마다 들어올 때 (높낮이 올라감)', m)
    for k, (ko, desc, mm) in mixes.items():
        write_wav(os.path.join(OUT, k + '.wav'), mm.out)
        res['mix'][k] = dict(ko=ko, desc=desc, **mm.result())
    with open(os.path.join(ROOT, 'tools', '_sound_metrics.json'), 'w', encoding='utf-8') as f:
        json.dump(res, f, ensure_ascii=False, indent=1)
    for k, v in res['sfx'].items():
        print(f"{k:11s} {v['ms']:4d}ms 최대{v['peak_db']:6.1f} 평균{v['rms_db']:6.1f} 체감{v['loud_db']:6.1f} 밝기{v['centroid']:5d}Hz 폰{v['phone']:3d}%")
    for k, v in res['bgm'].items():
        print(f"{k:11s} 반복 {v['loop_sec']}초 최대{v['peak_db']} 평균{v['rms_db']} 체감{v['loud_db']} 이음새 {v['seam']} 폰{v['phone']}%")
    for k, v in res['mix'].items():
        print(k, {x: v[x] for x in ['peak_db', 'clip_ms', 'max_voices', 'dropped', 'played', 'coin_pitch_end']})


if __name__ == '__main__':
    main()
