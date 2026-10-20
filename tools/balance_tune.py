# -*- coding: utf-8 -*-
"""골드 강화 구간 후보를 만들어 시뮬레이션 (원래 표는 안 건드림). 사용: python tools/balance_tune.py"""
import math, json, sys, io
import balance_sim as B

HP = [1.28, 1.14, 1.09, 1.06, 1.045]       # Stage_Table 구간 체력 성장
GG = [1.25, 1.135, 1.088, 1.059, 1.044]    # 골드 성장
SPD = [10, 6, 4, 2, 1]


def candidate(k=0.9, r=2.0, bounds=(80, 250, 500, 1000), base_cost=50, spd_mul=False):
    """구간마다: 강화 레벨/스테이지 = r 이 되도록 비용 성장, 공격력(=돈) 성장 = 체력 성장^k 를 r 레벨로 나눔.
    spd_mul = 공격속도도 공격력과 같은 비율로 곱해서 오름(공격력+공속 합쳐 체력 성장^k)"""
    pars = []
    ks = k if isinstance(k, (list, tuple)) else [k] * 5        # 구간마다 다르게 줄 수 있음
    for hp, gg, kk in zip(HP, GG, ks):
        la = kk * math.log(hp) / r / (2 if spd_mul else 1)
        lc = la + math.log(gg) / r
        pars.append((math.exp(lc), math.exp(la), math.exp(la)))
    rows = [{'Lv': 0, 'Upgrade_Cost': base_cost, 'Atk_Power': 100, 'Atk_Speed': 100, 'Money_Gain': 100,
             'Cost_Growth_Rate': 1, 'Stat_Growth_Rate': 1, 'Gold_Growth_Rate': 1, 'Spd_Add_Per_Lv': 0}]
    for lv in range(1, 1501):
        b = sum(lv > x for x in bounds)
        c, a, m = pars[b]
        p = rows[-1]
        rows.append({'Lv': lv, 'Upgrade_Cost': p['Upgrade_Cost'] * c, 'Atk_Power': p['Atk_Power'] * a, 'Atk_Speed': p['Atk_Speed'] * a if spd_mul else p['Atk_Speed'] + SPD[b],
                     'Money_Gain': p['Money_Gain'] * m, 'Cost_Growth_Rate': c, 'Stat_Growth_Rate': a, 'Gold_Growth_Rate': m, 'Spd_Add_Per_Lv': SPD[b]})
        if spd_mul: rows[-1]['Spd_Growth_Rate'] = a; rows[-1].pop('Spd_Add_Per_Lv')
    return pars, rows


def run(rows, **kw):
    old = B.UPG
    B.UPG = rows
    try:
        p = B.P(**kw)
        log = p.run(max_days=600)
    finally:
        B.UPG = old
    return p, log


if __name__ == '__main__':
    res = {}
    for k in [0.85, 0.9, 0.95]:
        pars, rows = candidate(k)
        p, log = run(rows)
        res[str(k)] = dict(pars=pars, log=log, stage=p.stage, day=p.day)
        print('k', k, 'pars', [tuple(round(x, 4) for x in t) for t in pars], '-> stage', p.stage, 'day', p.day, 'hours %.0f' % (p.t / 3600), 'farm %.0f' % (p.farm / 3600), 'lv', p.lv, 'slot', p.slot)
        for c in ['5', '10', '25', '50', '75', '100', '125', '150']:
            if c in log: print('   ch', c, 'day', log[c]['day'], 'farm_h %.1f' % log[c]['farm_h'], 'lv', log[c]['lv'][0], 'slot', log[c]['slot'])
    json.dump(res, io.open('tune.json', 'w'), default=float)
