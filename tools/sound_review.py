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
    f = min(len(d), int(np.ceil(SR * 0.003)))
    d[:f] *= np.arange(f) / f
    d[len(d) - f:] *= (np.arange(f) / f)[::-1]
    return d


def sweep(f0, f1, dur, kind, gain):
    n = int(np.ceil(SR * dur)); t = np.arange(n) / n
    ph = np.cumsum((f0 + (f1 - f0) * t) / SR)
    return make_clip(wave_(ph, kind) * np.exp(-4 * t) * gain)


def sweep_noise(f0, f1, dur, noise, gain, seed=12345):
    n = int(np.ceil(SR * dur)); t = np.arange(n) / n
    ph = np.cumsum((f0 + (f1 - f0) * t) / SR)
    nz = np.random.default_rng(seed).random(n) * 2 - 1
    return make_clip((wave_(ph, 'saw') * (1 - noise) + nz * noise) * np.exp(-5 * t) * gain)


def arp(notes, step, kind, gain):
    m = int(np.ceil(SR * step)); t = np.tile(np.arange(m) / m, len(notes))
    ph = np.cumsum(np.repeat(notes, m) / SR)
    return make_clip(wave_(ph, kind) * np.exp(-2.5 * t) * gain)


def loop(step_dur, bass, lead, lead_kind):
    m = int(np.ceil(SR * step_dur)); i = np.tile(np.arange(m), len(bass)); t = i / m
    bp = np.cumsum(np.repeat(bass, m) / SR); lp = np.cumsum(np.repeat(lead, m) / SR)
    ef = min(m // 4, int(np.ceil(SR * 0.002)))
    edge = np.where(i < ef, i / ef, np.where(i > m - ef, (m - i) / ef, 1.0))
    d = wave_(bp, 'saw') * np.exp(-3.5 * t) * 0.32 * edge + wave_(lp, lead_kind) * np.exp(-4.2 * t) * 0.22 * edge
    return np.clip(d, -1, 1)


SFX = {  # 이름: (한국어, 언제, 만들기)
    'Shot': ('일반 총 발사', '기본·속사·관통탄 한 발마다', lambda: sweep_noise(520, 120, 0.075, 0.45, 0.55)),
    'Shotgun': ('산탄 발사', '산탄 한 번 쏠 때', lambda: sweep_noise(340, 70, 0.13, 0.65, 0.70)),
    'Laser': ('레이저 발사', '레이저탄', lambda: sweep(1350, 260, 0.095, 'saw', 0.45)),
    'Missile': ('미사일 발사', '미사일탄', lambda: sweep_noise(280, 95, 0.16, 0.55, 0.55)),
    'Boom': ('미사일 폭발', '미사일이 터질 때', lambda: sweep_noise(160, 38, 0.28, 0.70, 0.85)),
    'Hit': ('좀비 맞음', '총알이 좀비에 맞을 때마다', lambda: sweep_noise(210, 65, 0.065, 0.38, 0.55)),
    'MineHit': ('광산 보석 때림', '광산에서 보석 맞을 때마다', lambda: arp([1174.66, 1567.98], 0.032, 'tri', 0.45)),
    'Dead': ('좀비 죽음', '좀비 한 마리마다', lambda: sweep(240, 480, 0.08, 'tri', 0.42)),
    'BossAppear': ('보스 등장 경고', '보스 나올 때 · 던전 시작', lambda: arp([220, 207.65, 196, 185, 220], 0.11, 'saw', 0.6)),
    'BossDead': ('보스 처치', '보스 잡았을 때', lambda: arp([392, 523.25, 659.25, 783.99, 1046.5], 0.11, 'square', 0.6)),
    'Upgrade': ('강화 성공', '공격·체력 강화 · 슬롯 강화 · 구매', lambda: arp([523.25, 659.25, 783.99], 0.045, 'tri', 0.48)),
    'Merge': ('합성 · 뽑기', '총알 합성 · 뽑기', lambda: arp([523.25, 659.25, 783.99, 1046.5], 0.075, 'square', 0.5)),
    'Coin': ('골드·다이아 들어옴', '날아간 돈이 위 칸에 닿을 때마다', lambda: arp([987.77, 1318.51], 0.042, 'sine', 0.45)),
    'UiClick': ('버튼 누름', '모든 버튼', lambda: arp([659.25, 880], 0.035, 'tri', 0.38)),
    'Victory': ('던전 승리', '던전 깼을 때', lambda: arp([523.25, 659.25, 783.99, 1046.5, 1318.51], 0.10, 'square', 0.52)),
    'Fail': ('실패', '보스·던전 실패 · 슬롯 강화 실패', lambda: arp([392, 349.23, 311.13, 261.63], 0.15, 'saw', 0.5)),
}
BGM = {
    'Stage_BGM': ('일반 전투 배경음', lambda: loop(0.25, [110] * 8 + [87.31] * 8 + [130.81] * 8 + [98] * 4 + [82.41] * 4,
        [440, 523.25, 659.25, 523.25, 440, 523.25, 659.25, 587.33, 349.23, 440, 523.25, 440, 349.23, 440, 523.25, 659.25,
         523.25, 659.25, 783.99, 659.25, 523.25, 659.25, 587.33, 523.25, 392, 493.88, 587.33, 493.88, 329.63, 493.88, 440, 329.63], 'tri')),
    'Boss_BGM': ('보스전·던전 배경음', lambda: loop(0.2, [110] * 6 + [130.81, 110] + [103.83] * 6 + [123.47, 103.83] + [98] * 6 + [116.54, 98] + [82.41, 82.41, 87.31, 87.31, 98, 98, 82.41, 82.41],
        [440, 659.25, 523.25, 659.25, 440, 659.25, 587.33, 523.25, 415.30, 659.25, 493.88, 659.25, 415.30, 659.25, 587.33, 493.88,
         392, 587.33, 466.16, 587.33, 392, 587.33, 523.25, 466.16, 329.63, 493.88, 349.23, 523.25, 392, 587.33, 493.88, 329.63], 'square')),
}
MIN_GAP = {'Shot': .045, 'Shotgun': .045, 'Laser': .045, 'Missile': .045, 'Hit': .045, 'MineHit': .045, 'Dead': .04, 'Coin': .04, 'Boom': .06}
RANDOM_PITCH = {'Shot', 'Shotgun', 'Laser', 'Hit', 'MineHit'}


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


# ---------------- 게임 속 상황 섞기 (Unity 와 같은 규칙: 종류별 최소 간격 · 16 채널 · 빈 채널 없으면 안 남) ----------------
class Mixer:
    def __init__(self, sec, clips, bgm=None):
        self.out = np.zeros(int(SR * sec)); self.clips = clips; self.busy = [0.0] * CHANNELS; self.ch = 0
        self.last = {}; self.coin_p, self.coin_t = 1.0, -9; self.rng = np.random.default_rng(7)
        self.dropped = {}; self.played = {}; self.max_voices = 0; self.voices = []
        if bgm is not None:
            rep = np.tile(bgm, int(np.ceil(len(self.out) / len(bgm))))[:len(self.out)]
            self.out += rep * BGM_VOL

    def play(self, name, t):
        if t - self.last.get(name, -9) < MIN_GAP.get(name, .02): return
        self.last[name] = t
        idx = None
        for i in range(CHANNELS):
            j = (i + self.ch) % CHANNELS
            if self.busy[j] <= t: idx = j; break
        if idx is None: self.dropped[name] = self.dropped.get(name, 0) + 1; return
        self.ch = idx
        pitch = 1.0
        if name == 'Coin':
            self.coin_p = min(self.coin_p + .04, 1.55) if t - self.coin_t < .45 else 1.0; self.coin_t = t; pitch = self.coin_p
        elif name in RANDOM_PITCH: pitch = self.rng.uniform(.92, 1.08)
        c = self.clips[name]
        if pitch != 1.0:
            c = np.interp(np.arange(0, len(c) - 1, pitch), np.arange(len(c)), c)
        self.busy[idx] = t + len(c) / SR
        s = int(t * SR); e = min(len(self.out), s + len(c))
        if e <= s: return
        self.out[s:e] += c[:e - s] * SFX_VOL
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
        write_wav(os.path.join(OUT, k + '_x4.wav'), np.tile(d, 4) * BGM_VOL)   # 4번 반복해서 이음새도 들리게
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
