# -*- coding: utf-8 -*-
"""11-05 밤: 슬롯 강화 제거(Stats.SlotEnchantOn = false) 뒤 밸런스 — 총알 등급 공격력에 등급별 배율을 곱함.
사용: PYTHONUTF8=1 python tools/balance_noench_1105.py        (적용 → xlsx2csv 는 따로)
      PYTHONUTF8=1 python tools/balance_noench_1105.py --check (쓰지 않고 바뀔 값만)

 - 배율 MUL = 강화가 있던 진행(시뮬 balance_sim, 광고 안 봄·광고 최대, 방치 포함)에서 등급마다 실제로 받던 강화 효과(공격·속도·보스 피해)를
   옮기되(다이아·골드가 강화에 안 들어가 남는 만큼 덜 줌), 챕터 10·25·50·75·100·125·150 날짜가 강화 있을 때와 같아지게 맞춘 매끈한 곡선.
   결과(시뮬, 하루 4시간 + 방치): 150챕터 광고 안 봄 1307 → 1409일 · 광고 최대 486 → 492일 (강화 없이 그대로면 3000일에 99/117챕터)
 - 원본 = Table/_backup_1105/Weapon.xlsx (처음 실행 때 만듦). **늘 원본에서 다시 계산** → 두 번 돌려도 같은 값.
   그 대신 이 도구 뒤에 Weapon.xlsx 를 직접 고쳤다면, 다시 돌리기 전에 그 변경을 원본에도 옮길 것.
 - 바꾸는 칸: Weapon_Master 의 Atk · Atk_표기 · Atk_Growth_Rate · Single/Multi_Target_DPS · DPS_표기,
   Weapon_Grade_Growth 의 Grade_Atk_Growth_Rate · Cum_Atk_Multiplier_vs_D (게임 코드는 Atk 만 씀, 나머지는 보기용)
"""
import math, os, shutil, sys
import openpyxl

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Kachi_Gun', 'Table')
SRC = os.path.join(ROOT, 'Weapon.xlsx')
BAK = os.path.join(ROOT, '_backup_1105', 'Weapon.xlsx')

# 매끈한 곡선: MUL_g = exp(0.02 g + 0.044 g^2) — 등급 한 단계 공격력 = 원래 성장률 x (1.07 ~ 2.81), 실제 C x2.1 … SSU x8.4
# (등급마다 따로 맞춘 울퉁불퉁한 값은 오차 0.40 이지만 S→SS x5 · SR x2.9 · SSR x9 처럼 들쭉날쭉해서 안 씀, 이 곡선 오차 0.65)
MUL = {g: math.exp(0.02 * g + 0.044 * g * g) for g in range(13)}


def sig3(v):
    """세 자리 유효 숫자로 반올림 (100 아래는 정수)"""
    if v < 1000: return int(round(v))
    p = 10 ** (int(math.floor(math.log10(v))) - 2)
    return int(round(v / p) * p)


def fmt(v):
    """Num.F 와 같은 표기(내림): 1.01m · 152k"""
    if v < 1000: return str(int(math.floor(v)))
    k = int(math.floor(math.log10(v) / 3)); m = v / 10 ** (k * 3)
    if m >= 1000: k += 1; m /= 1000
    units = ['', 'k', 'm', 'b', 't']
    u = units[k] if k < len(units) else chr(97 + (k - 5) // 26 % 26) + chr(97 + (k - 5) % 26)
    tr = lambda x, d: math.floor(x * 10 ** d) / 10 ** d
    s = f'{tr(m, 2):.2f}' if m < 10 else f'{tr(m, 1):.1f}' if m < 100 else f'{math.floor(m):.0f}'
    if '.' in s: s = s.rstrip('0').rstrip('.')
    return s + u


def main(check):
    if not os.path.exists(BAK):
        os.makedirs(os.path.dirname(BAK), exist_ok=True); shutil.copy2(SRC, BAK)
        print('원본 백업', BAK)
    wb = openpyxl.load_workbook(BAK)
    ws = wb['Weapon_Master']
    hd = {c.value: i for i, c in enumerate(ws[1])}
    col = lambda *names: next(hd[n] for n in names if n in hd)
    cG, cT, cA, cAs, cAg = col('Grade_Idx'), col('Type'), col('Atk'), col('Atk_표기'), col('Atk_Growth_Rate')
    cS, cM, cDs = col('Single_Target_DPS'), col('Multi_Target_Max_DPS'), col('DPS_표기')
    rows = list(ws.iter_rows(min_row=2))
    new_atk = {}
    for r in rows:
        if r[cG].value is None: continue
        g = int(r[cG].value); old = float(r[cA].value)
        na = sig3(old * MUL[g]); k = na / old
        new_atk[(g, r[cT].value)] = na
        if check and g in (0, 5, 9, 12): print(r[col('Name')].value, old, '→', na)
        r[cA].value = na; r[cAs].value = fmt(na)
        for c in (cS, cM):
            if isinstance(r[c].value, (int, float)): r[c].value = round(r[c].value * k, 1)
        if isinstance(r[cS].value, (int, float)): r[cDs].value = fmt(r[cS].value)
    for r in rows:   # 등급 성장률 = 같은 종류 한 등급 아래와의 비율
        if r[cG].value is None: continue
        g = int(r[cG].value); prev = new_atk.get((g - 1, r[cT].value))
        r[cAg].value = 1 if g == 0 or not prev else round(r[cA].value / prev, 3)
    gs = wb['Weapon_Grade_Growth']
    gh = {c.value: i for i, c in enumerate(gs[1])}
    cum_prev = None
    for r in gs.iter_rows(min_row=2):
        if r[gh['Grade_Idx']].value is None: continue
        g = int(r[gh['Grade_Idx']].value)
        cum = float(r[gh['Cum_Atk_Multiplier_vs_D']].value) * MUL[g] / MUL[0]
        r[gh['Cum_Atk_Multiplier_vs_D']].value = round(cum, 2)
        r[gh['Grade_Atk_Growth_Rate']].value = 1 if cum_prev is None else round(cum / cum_prev, 3)
        cum_prev = cum
        if check: print('등급', r[gh['Grade_Name']].value, '누적 x', round(cum, 1))
    if check: print('(--check: 안 씀)'); return
    wb.save(SRC)
    print('Weapon.xlsx 씀 → 다음: python tools/xlsx2csv.py (Weapon_Master · Weapon_Grade_Growth 만 남기고 나머지 csv 는 되돌리기)')


if __name__ == '__main__':
    main('--check' in sys.argv)
