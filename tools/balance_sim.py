# -*- coding: utf-8 -*-
"""카치건 밸런스 시뮬레이터 — 사용: python tools/balance_sim.py [최대 날 수] [결과 json]
 — Resources/Tables/*.csv 를 게임 코드와 같은 식으로 계산 (10-19 처음, 10-20 규칙 추가).
가정: 하루 H 시간 켜 둠(오프라인 보상 없음), 플레이어는 '가장 효율 좋은 것부터' 산다.
ads=True = 광고를 최대로 봄: 공격·골드 버프 늘 켜짐 · 던전 결과 광고 x2 · 상점 광고 다이아 하루 AdGems_Daily 번.
10-20: 미션 보상 = 다이아 Mission_Gem_Ratio + 골드(나머지, 1다이아 = 좀비 Gem_Gold_Kills 마리 골드) · Max_Tier 에서 끝
       광산 = Mine_Ore_Spec 보석 전부 x Mine_Scaling 배율 · 슬롯 강화 골드는 모아서 삼(30분 수입 이하일 때)
"""
import csv, io, math, json, sys, re, os

T = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Kachi_Gun', 'Assets', 'Resources', 'Tables') + os.sep


def rd(n):
    rows = list(csv.DictReader(io.open(T + n + '.csv', encoding='utf-8-sig')))
    out = []
    for r in rows:
        d = {}
        for k, v in r.items():
            k = re.sub(r'\s*\(.*\)\s*$', '', k.strip()).strip()
            try: d[k] = float(v)
            except: d[k] = v
        out.append(d)
    return out


def load():
    """csv 다시 읽기 (표를 바꾼 뒤)"""
    global CFG, STAGE, UPG, WEAP, GRADE, GENCH, ENCH, SKIN, SUP, ZOM, CHAL, BOSS, MINE, ORE, MM, MT, BUFF, AD, ADD, MAXG, TYPES
    CFG = {r['Key']: r['Value'] for r in rd('Global_Config')}
    STAGE = rd('Stage_Table')
    UPG = rd('Gold_Upgrade')
    WEAP = rd('Weapon_Master')
    GRADE = rd('Weapon_Grade_Growth')
    GENCH = {int(r['Grade_Idx']): r for r in rd('Grade_Enchant_Multiplier')}
    ENCH = rd('Weapon_Enchant')
    SKIN = rd('Skin_Table')
    SUP = rd('Supply_Collection')
    ZOM = rd('Zombie_Master')
    CHAL = {int(r['Challenge_Chapter']): r for r in rd('Challenge')}
    BOSS = rd('Boss_Dungeon')
    MINE = rd('Mine_Scaling')
    ORE = rd('Mine_Ore_Spec')
    MM = rd('Mission_Master')
    MT = rd('Mission_Tier')
    BUFF = {r['Buff_Id']: r for r in rd('Buff_Table')}
    AD = BUFF['buff_atk']['Multiplier'] if 'buff_atk' in BUFF else 3      # 광고 버프 배율 (표)
    ADD = BUFF['dungeon_ad_x3']['Multiplier'] if 'dungeon_ad_x3' in BUFF else 3
    MAXG = len(GRADE) - 1
    TYPES = {}
    for w in WEAP: TYPES.setdefault(int(w['Grade_Idx']), []).append(w)


load()
MIN_IV = 0.06


def cfg(k, d):
    v = CFG.get(k, d)
    return v if isinstance(v, float) else d


def upg(lv):
    if lv < len(UPG): return UPG[lv]
    L = UPG[-1]; n = lv - int(L['Lv'])
    return {'Upgrade_Cost': L['Upgrade_Cost'] * L['Cost_Growth_Rate'] ** n, 'Atk_Power': L['Atk_Power'] * L['Stat_Growth_Rate'] ** n,
            'Atk_Speed': L['Atk_Speed'] * L['Spd_Growth_Rate'] ** n if 'Spd_Growth_Rate' in L else L['Atk_Speed'] + L['Spd_Add_Per_Lv'] * n, 'Money_Gain': L['Money_Gain'] * L['Gold_Growth_Rate'] ** n}


def mix(ch):
    ws = [z for z in ZOM if z['MinChapter'] <= ch and z['Base_Weight'] > 0]
    s = sum(z['Base_Weight'] for z in ws)
    return sum(z['Base_Weight'] * z['HpMul'] for z in ws) / s, sum(z['Base_Weight'] * z['GoldMul'] for z in ws) / s


def mission_value(m):
    return m['Base_Reward_Value'] if 'Base_Reward_Value' in m else m['Base_Reward_Gems']


class P:
    def __init__(s, ads=False, hours=4.0, spd_smart=True, overflow=True):
        s.ads, s.hours, s.smart, s.overflow = ads, hours, spd_smart, overflow   # overflow = 상한 넘는 공속 → 공격력 1:1
        s.t = 0.0; s.day = 0; s.stage = 0; s.gold = 0.0; s.gems = 0.0
        s.lv = [0, 0, 0]          # atk spd gold
        s.slot = [0, -1, -1]      # 등급 (-1 = 빈칸)
        s.ench = [0, 0, 0]
        s.skins = {0}; s.sup = set(); s.chal = set(); s.bossLv = 0
        s.stat = dict(kill=0, stage=0, mine=0, boss=0, merge=0)
        s.mtier = [0] * len(MM)
        s.gem_src = dict(mission=0, challenge=0, mine=0, raid=0, ad=0)
        s.gem_use = dict(weapon=0, skin=0, supply=0, enchant=0)
        s.gold_use = dict(atk=0, spd=0, gold=0, enchant=0)
        s.gold_src = dict(stage=0, mission=0)
        s.farm = 0.0; s.log = []; s.spd_cap_t = None
        s.reserve = 0.0           # 슬롯 강화 골드를 모으는 중 (강화에 안 씀)
        s.grate = 1.0             # 최근 골드 수입 (초당)

    # ---------- 능력치 ----------
    def skin_sum(s, k): return sum(SKIN[i][k] for i in s.skins if i < len(SKIN))
    def sup_sum(s, k): return sum(SUP[i - 1][k] for i in s.sup)

    def atkmul(s):
        return upg(s.lv[0])['Atk_Power'] / 100 * (1 + s.skin_sum('Atk_Bonus') / 100) * (1 + s.sup_sum('Atk_Bonus') / 100) * (AD if s.ads else 1)

    def spdmul(s): return upg(s.lv[1])['Atk_Speed'] / 100 * (1 + s.skin_sum('Spd_Bonus') / 100) * (1 + s.sup_sum('Spd_Bonus') / 100)

    def goldmul(s): return upg(s.lv[2])['Money_Gain'] / 100 * (1 + s.skin_sum('Gold_Bonus') / 100) * (1 + s.sup_sum('Gold_Bonus') / 100) * (AD if s.ads else 1)

    def ench_eff(s, i):
        g = s.slot[i]
        if g < 0 or s.ench[i] <= 0: return 0, 0, 0
        lv = min(s.ench[i], int(GENCH[g]['Max_Enchant_Lv']))
        e = ENCH[lv - 1]; m = GENCH[g]['Enchant_Effect_Mul']
        return e['Atk_Bonus_Pct'] * m, e['Spd_Bonus_Pct'] * m, e['Boss_Dmg_Bonus_Pct'] * m

    def slot_dps(s, i, boss=False, slot=None, ench=None):
        g = s.slot[i] if slot is None else slot
        if g < 0: return 0
        el = s.ench[i] if ench is None else ench
        old = s.slot[i], s.ench[i]
        s.slot[i], s.ench[i] = g, el
        a, sp, b = s.ench_eff(i)
        s.slot[i], s.ench[i] = old
        spd = s.spdmul() * (1 + sp / 100)
        am = s.atkmul() * (1 + a / 100) * ((1 + (s.sup_sum('Boss_Dmg_Bonus') + b) / 100) if boss else 1)
        ws = TYPES[min(g, MAXG)]
        iv = (lambda w: w['Interval'] / spd) if s.overflow else (lambda w: max(MIN_IV, w['Interval'] / spd))
        return sum(w['Atk'] * w['Count'] / iv(w) for w in ws) / len(ws) * am

    def dps(s, boss=False): return sum(s.slot_dps(i, boss) for i in range(3))

    def spd_capped(s):
        if s.overflow: return False
        sp = s.spdmul()
        return all(s.slot[i] < 0 or all(w['Interval'] / (sp * (1 + s.ench_eff(i)[1] / 100)) <= MIN_IV for w in TYPES[s.slot[i]]) for i in range(3))

    # ---------- 시간 ----------
    def advance(s, dt):
        s.t += dt
        while s.t >= (s.day + 1) * s.hours * 3600:
            s.day += 1; s.daily()

    def daily(s):
        ch = s.stage // 5 + 1
        # 광고 무료 다이아 (상점)
        if s.ads:
            g = cfg('AdGems_Daily', 0) * cfg('AdGems_Amount', 0)
            s.gems += g; s.gem_src['ad'] += g
        # 광산 MineTickets 장: 보석 전부 x 단계 배율, 60초 동안 때린 만큼
        tier = [r for r in MINE if r['Unlock_Chapter'] <= ch]
        if s.stage >= 15 and tier:
            full = sum(o['Base_Reward_Gems'] * o['Spawn_Count'] for o in ORE) * tier[-1]['Reward_Multiplier']
            need = sum(o['Hit_HP'] * o['Spawn_Count'] for o in ORE)
            hits = 60 * sum(len(TYPES[g]) and sum(w['Count'] / max(MIN_IV, w['Interval'] / s.spdmul()) for w in TYPES[g]) / len(TYPES[g]) for g in s.slot if g >= 0) * 0.7
            n = cfg('MineTickets', 2)
            g = full * min(1, hits / need) * n * (ADD if s.ads else 1)
            s.gems += g; s.gem_src['mine'] += g; s.stat['mine'] += n
        # 보스 던전 BossRaidTickets 장 (이길 때만 보상·다음 단계)
        if s.stage >= 45:
            for _ in range(int(cfg('BossRaidTickets', 3))):
                b = BOSS[min(s.bossLv, len(BOSS) - 1)]
                if s.dps(True) * b['Time_Limit'] >= b['Boss_HP']:
                    r = b['Reward_Gems'] * (ADD if s.ads else 1)
                    s.gems += r; s.gem_src['raid'] += r; s.bossLv += 1; s.stat['boss'] += 1

    # ---------- 쓰기 ----------
    def mission_target(s, i, tr):
        m = MM[i]
        k = m['Save_Stat_Key']
        col = {'statKills': 'Kill', 'statStages': 'Stage', 'statMines': 'Mine', 'statBossKills': 'Boss', 'statMerges': 'Merge'}[k] + '_Target'
        if col in MT[0]:
            if tr < len(MT): return MT[tr][col]
            L = MT[-1]; n = tr - len(MT) + 1
            pre = col.split('_')[0]
            if pre + '_Growth_Rate' in L and isinstance(L[pre + '_Growth_Rate'], float): return L[col] * max(1.01, L[pre + '_Growth_Rate']) ** n
            if pre + '_Add_Step' in L and isinstance(L[pre + '_Add_Step'], float): return L[col] + max(1, L[pre + '_Add_Step']) * n
            return L[col] * max(1.01, m['Default_Growth_Rate']) ** n
        pre = [float(x) for x in str(m['Tier_1_7_Preset']).split('|')]
        return pre[tr] if tr < len(pre) else pre[-1] * max(1.01, m['Default_Growth_Rate']) ** (tr - len(pre) + 1)

    def missions(s):
        keys = {'statKills': 'kill', 'statStages': 'stage', 'statMines': 'mine', 'statBossKills': 'boss', 'statMerges': 'merge'}
        ratio = cfg('Mission_Gem_Ratio', 1.0)
        kills = cfg('Gem_Gold_Kills', 6)
        for i, m in enumerate(MM):
            k = keys[m['Save_Stat_Key']]
            mx = m.get('Max_Tier', 0)
            mx = int(mx) if isinstance(mx, float) else 0
            for _ in range(500):
                tr = s.mtier[i]
                if mx and tr >= mx: break
                if s.stat[k] < s.mission_target(i, tr): break
                v = mission_value(m) + m['Reward_Add_Per_10Tier'] * (tr // 10)
                # 10-24: 단계마다 골드(짝수) → 다이아(홀수) 번갈아 한 종류, 두 단계 합 = 다이아 ratio + 골드 (1-ratio)
                if tr % 2 == 1:
                    s.gems += v * ratio * 2; s.gem_src['mission'] += v * ratio * 2
                else:
                    g = v * (1 - ratio) * 2 * kills * s.stage_row(s.stage)['Base_Zombie_Gold'] * s.goldmul()
                    s.gold += g; s.gold_src['mission'] += g
                s.mtier[i] += 1

    def gem_options(s):
        base_b, base_g = s.dps(True), s.goldmul()
        ch = s.stage // 5 + 1
        opts = []
        def val(nb, ng): return math.log(max(nb, 1e-9) / max(base_b, 1e-9)) + 0.3 * math.log(ng / base_g)
        low = min(range(3), key=lambda k: s.slot[k])      # 총알은 가장 낮은 칸부터 (분대 3마리 고르게)
        for i in range(3):
            g = s.slot[i]
            if g < MAXG and i == low:
                cost = 100 if g < 0 else 100 * 2 ** g
                old = s.slot[i]; s.slot[i] = g + 1; nb = s.dps(True); s.slot[i] = old
                opts.append((val(nb, base_g) / cost, cost, 0, 'weapon', i))
            if g >= 0 and s.ench[i] < min(int(GENCH[g]['Max_Enchant_Lv']), len(ENCH)):
                e = ENCH[s.ench[i]]
                rate = e['Success_Rate'] / 100
                if e['Cost_Gold'] / rate > s.grate * 1800: continue      # 골드 30분 수입보다 비싸면 아직 안 함
                old = s.ench[i]; s.ench[i] += 1; nb = s.dps(True); s.ench[i] = old
                opts.append((val(nb, base_g) / (e['Cost_Gems'] / rate), e['Cost_Gems'] / rate, e['Cost_Gold'] / rate, 'enchant', i))
        for sk in SKIN:
            if sk['Id'] in s.skins: continue
            s.skins.add(int(sk['Id'])); nb, ng = s.dps(True), s.goldmul(); s.skins.discard(int(sk['Id']))
            opts.append((val(nb, ng) / sk['Price_Gems'], sk['Price_Gems'], 0, 'skin', int(sk['Id'])))
            break   # 싼 순서로 하나만 (뒤 스킨이 더 비쌈)
        for su in SUP:
            if su['Supply_Id'] in s.sup or su['Unlock_Chapter'] > ch: continue
            s.sup.add(int(su['Supply_Id'])); nb, ng = s.dps(True), s.goldmul(); s.sup.discard(int(su['Supply_Id']))
            opts.append((val(nb, ng) / su['Cost_Gems'], su['Cost_Gems'], 0, 'supply', int(su['Supply_Id'])))
        return sorted(opts, reverse=True)

    def spend_gems(s):
        s.reserve = 0.0
        for _ in range(200):
            o = s.gem_options()
            if not o: return
            v, cost, gcost, kind, arg = o[0]
            if s.gems < cost: return                       # 가장 좋은 것을 위해 다이아를 모음
            if s.gold < gcost: s.reserve = gcost; return   # 슬롯 강화 골드를 모음 (골드 강화를 잠시 멈춤)
            s.gems -= cost; s.gold -= gcost; s.gem_use[kind] += cost
            if kind == 'weapon':
                if s.slot[arg] >= 0: s.stat['merge'] += 2 ** max(0, s.slot[arg])   # 한 등급 올리려면 아래 등급 합성이 2^g 번
                s.slot[arg] += 1
            elif kind == 'enchant': s.ench[arg] += 1; s.gold_use['enchant'] += gcost
            elif kind == 'skin': s.skins.add(arg)
            else: s.sup.add(arg)

    def next_upg(s):
        c = [(upg(s.lv[k])['Upgrade_Cost'], k) for k in range(3) if not (k == 1 and s.smart and s.spd_capped())]
        return min(c)

    def spend_gold(s):
        for _ in range(100000):
            cost, k = s.next_upg()
            if not (cost > 0 and math.isfinite(cost)) or not math.isfinite(s.gold): return   # 무한 반복 방지
            if s.gold - s.reserve < cost: return
            s.gold -= cost; s.lv[k] += 1; s.gold_use[['atk', 'spd', 'gold'][k]] += cost

    # ---------- 스테이지 ----------
    def stage_row(s, i):
        if i < len(STAGE): return STAGE[i]
        L = STAGE[-1]; b = [r for r in STAGE if r['Sub_Stage'] == i % 5 + 1][-1]; n = i - int(b['Stage_Idx'])
        r = dict(b); r['Base_Zombie_HP'] *= L['Hp_Growth_Rate'] ** n; r['Base_Zombie_Gold'] *= L['Gold_Growth_Rate'] ** n
        r['Boss_HP'] *= L['Hp_Growth_Rate'] ** n; r['Boss_Gold'] *= L['Gold_Growth_Rate'] ** n
        return r

    def rate(s, row):
        hm, gm = mix(s.stage // 5 + 1)
        hp = row['Base_Zombie_HP'] * hm
        return min(s.dps() / hp, 1 / row['Spawn_Interval']), row['Base_Zombie_Gold'] * gm

    def challenge(s):
        ch = s.stage // 5 + 1
        for c in range(1, ch + 1):
            if c in s.chal or c not in CHAL: continue
            r = CHAL[c]
            hm, _ = mix(c)
            if s.dps() * r['Time_Limit'] * 0.8 >= r['Base_Zombie_HP'] * hm * r['Zombie_Count']:
                s.chal.add(c); g = r['Reward_Gems'] * (ADD if s.ads else 1); s.gems += g; s.gem_src['challenge'] += g
            else: break

    def run(s, max_days=400, until=750):
        chap_log = {}
        while s.stage < until and s.day < max_days:
            s.missions(); s.spend_gems(); s.spend_gold(); s.challenge()
            if s.spd_cap_t is None and s.spd_capped() and s.slot[0] >= 0: s.spd_cap_t = s.stage
            row = s.stage_row(s.stage)
            kr, gpk = s.rate(row)
            s.grate = kr * gpk * s.goldmul()
            boss = row['Is_Boss_Stage'] == 'Y'
            if not boss:
                dt = row['Kills_Quota'] / kr + 3
                g = row['Kills_Quota'] * gpk * s.goldmul()
                s.advance(dt); s.gold += g; s.gold_src['stage'] += g; s.stat['kill'] += row['Kills_Quota']
                s.stage += 1; s.stat['stage'] += 1
                continue
            bd = s.dps(True)
            if bd * row['Boss_Time_Sec'] >= row['Boss_HP']:
                dt = row['Kills_Quota'] / kr + row['Boss_HP'] / bd + 3
                g = (row['Kills_Quota'] * gpk + row['Boss_Gold']) * s.goldmul()
                s.advance(dt); s.gold += g; s.gold_src['stage'] += g
                s.stat['kill'] += row['Kills_Quota'] + 1; s.stat['boss'] += 1
                s.stage += 1; s.stat['stage'] += 1
                ch = s.stage // 5
                chap_log[ch] = s.snap()
                continue
            # 보스 실패 → 반복 사냥 (다음 강화 살 때까지 또는 다음 날까지)
            cost, _ = s.next_upg()
            need = max(1.0, (cost + s.reserve - s.gold) / s.grate)
            to_day = (s.day + 1) * s.hours * 3600 - s.t + 1
            dt = min(max(need, 1800.0), to_day)     # 반복 사냥은 최소 30분 단위로 (계산 빠르게)
            s.advance(dt); s.farm += dt; s.gold += s.grate * dt; s.gold_src['stage'] += s.grate * dt; s.stat['kill'] += kr * dt
        return chap_log

    def snap(s):
        return dict(day=s.day, hours=s.t / 3600, farm_h=s.farm / 3600, lv=list(s.lv), slot=list(s.slot), ench=list(s.ench),
                    skins=len(s.skins) - 1, sup=len(s.sup), boss_lv=s.bossLv, gems=s.gems, dps=s.dps(True), kills=s.stat['kill'],
                    merges=s.stat['merge'], mines=s.stat['mine'], boss_kills=s.stat['boss'], grate=s.grate, goldmul=s.goldmul(),
                    gem_src=dict(s.gem_src), gem_use=dict(s.gem_use), gold_use=dict(s.gold_use), gold_src=dict(s.gold_src))


if __name__ == '__main__':
    out = {}
    for name, kw in [('base', dict()), ('ads', dict(ads=True)), ('heavy', dict(hours=10)), ('ads2h', dict(ads=True, hours=2))]:
        p = P(**kw)
        log = p.run(max_days=int(sys.argv[1]) if len(sys.argv) > 1 else 365)
        out[name] = dict(log=log, end=p.snap(), stage=p.stage, spd_cap=p.spd_cap_t)
        print(name, 'stage', p.stage, 'ch', p.stage // 5 + 1, 'day', p.day, 'lv', p.lv, 'slot', p.slot, 'ench', p.ench, 'skins', len(p.skins) - 1, 'sup', len(p.sup))
    json.dump(out, io.open(sys.argv[2] if len(sys.argv) > 2 else 'sim.json', 'w'), default=float)
