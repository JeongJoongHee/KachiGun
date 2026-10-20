# -*- coding: utf-8 -*-
"""10-20 밸런스·표 적용 (남은_작업_정리 의견 반영). 사용: python tools/balance_apply_1020.py
항상 Table/_backup_1020/ (10-20 시작 전 xlsx) 에서 다시 만들어 씀 → 여러 번 돌려도 같은 결과.
  · 스킨 18~30 삭제 (17개만)                         · 골드 획득 1/2 (Stage_Table 좀비·보스 골드)
  · 상점 광고 무료 다이아 (하루 AdGems_Daily 번 x AdGems_Amount)
  · 미션 보상 = 다이아 30% + 골드 70% (Mission_Gem_Ratio) · 처치 1단계 ≈ 1.5챕터 · 스테이지 750 에서 끝 · 합성 1·2·4·8…
  · 광산 보상 = 보스 던전 수준 (같은 챕터의 보스 던전 1회 보상 = 광산 티켓 1장)
  · 슬롯 강화 골드 = 그 챕터 골드 수입 ENCH_MIN 분
  · 무료 다이아 = 다이아로 살 수 있는 것 전부의 FREE_SHARE (도전·보스 던전·광산·미션 보상에 같은 배율 M)
  · 골드 강화 표 구간별 K → 광고 최대·하루 4시간 = 50챕터 21일 · 100챕터 63일 · 150챕터 147일
결과: tools/balance_1020.json (밸런스_점검.html 에 씀)
"""
import glob, io, json, math, os, re, shutil, sys
import openpyxl

TOOLS = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, TOOLS)
import balance_tune as TU
import xlsx2csv as XC
import balance_sim as B

TB = os.path.join(TOOLS, '..', 'Kachi_Gun', 'Table') + os.sep
BK = TB + '_backup_1020' + os.sep

HOURS = 4.0
TARGET = {50: 21, 100: 63, 150: 147}     # 광고 최대 · 무과금 · 하루 4시간 (3주 · +6주 · +12주)
FREE_SHARE = 0.30                         # 무료 3 : 결제 7
GOLD_MUL = 0.5                            # 골드 획득량 1/2
ENCH_MIN = 10                             # 슬롯 강화 1번 골드 = 그 챕터 골드 수입 10분
SKIN_MAX = 17
AD_GEMS_DAILY, AD_GEMS_AMOUNT = 3, 40
GEM_RATIO, GEM_KILLS = 0.3, 6
MINE_RATIO = 0.5                          # 광산 티켓 1장 = 보스 던전 1회 보상의 절반 (10-20 의견)
MINE_FULL = None                          # 광산 보석 전부 기본 다이아 (Mine_Ore_Spec 에서 계산)


# =====================================================================
def fmt(v):
    """게임 Num.F 와 같은 표기 (내림)"""
    if v < 1000: return str(int(math.floor(v)))
    k = int(math.floor(math.log10(v) / 3)); m = v / 10 ** (k * 3)
    if m >= 1000: k += 1; m /= 1000
    named = ['', 'k', 'm', 'b', 't']
    unit = named[k] if k < 5 else chr(ord('a') + (k - 5) // 26 % 26) + chr(ord('a') + (k - 5) % 26)
    tr = lambda x, d: math.floor(x * 10 ** d) / 10 ** d
    s = ('%.2f' % tr(m, 2)) if m < 10 else ('%.1f' % tr(m, 1)) if m < 100 else str(int(math.floor(m)))
    if '.' in s: s = s.rstrip('0').rstrip('.')
    return s + unit


def strip(h): return re.sub(r'\s*\(.*\)\s*$', '', str(h or '').strip()).strip()


class Sheet:
    """칸 이름(단위 표시 뗀 것)으로 읽고 쓰기"""
    def __init__(s, ws):
        s.ws = ws
        s.head = [c.value for c in ws[1]]
        s.col = {strip(h): i + 1 for i, h in enumerate(s.head) if h is not None}

    def rows(s): return range(2, s.ws.max_row + 1)
    def get(s, r, k): return s.ws.cell(r, s.col[k]).value
    def set(s, r, k, v): s.ws.cell(r, s.col[k]).value = v

    def rename(s, old, new):
        c = s.col.pop(strip(old)); s.ws.cell(1, c).value = new; s.col[strip(new)] = c

    def add_col(s, name):
        c = s.ws.max_column + 1
        s.ws.cell(1, c).value = name; s.col[strip(name)] = c
        return c


def restore():
    """백업에서 xlsx 를 다시 가져옴 (처음이면 지금 xlsx 를 백업)"""
    if not os.path.isdir(BK):
        os.makedirs(BK)
        for f in glob.glob(TB + '*.xlsx'): shutil.copy2(f, BK)
        print('백업 만듦:', BK)
    return {os.path.basename(f): openpyxl.load_workbook(f) for f in glob.glob(BK + '*.xlsx')}


# =====================================================================
# 고정 변경 (매개변수 없음)
# =====================================================================
def edit_static(W):
    global MINE_FULL
    # ---------- 스킨 17개만 ----------
    ws = W['Skin.xlsx'].worksheets[0]
    sh = Sheet(ws)
    for r in range(ws.max_row, 1, -1):
        if sh.get(r, 'Id') is not None and int(sh.get(r, 'Id')) > SKIN_MAX: ws.delete_rows(r)
    for r in sh.rows():
        if sh.get(r, 'Id') == SKIN_MAX: sh.set(r, 'Impl_Status', '구현 완료 (10-20 Skin_17 그림)')
    ws.title = 'Skin_Table_17'

    # ---------- 골드 1/2 ----------
    st = Sheet(W['Stage.xlsx']['Stage_Table'])
    for r in st.rows():
        g = st.get(r, 'Base_Zombie_Gold') * GOLD_MUL
        st.set(r, 'Base_Zombie_Gold', round(g, 2)); st.set(r, 'Gold_표기', fmt(g))
        st.set(r, 'Stage_20Kills_Gold_표기', fmt(g * st.get(r, 'Kills_Quota')))
        bg = (st.get(r, 'Boss_Gold') or 0) * GOLD_MUL
        st.set(r, 'Boss_Gold', round(bg, 2)); st.set(r, 'Boss_Gold_표기', fmt(bg) if bg > 0 else '-')
    gold = {st.get(r, 'Stage_Idx'): (st.get(r, 'Base_Zombie_Gold'), st.get(r, 'Kills_Quota'), st.get(r, 'Boss_Gold') or 0) for r in st.rows()}
    cs = Sheet(W['Stage.xlsx']['Chapter_Summary_150'])
    for r in cs.rows():
        c = cs.get(r, 'Chapter')
        tot = sum(gold[i][0] * gold[i][1] + gold[i][2] for i in range((c - 1) * 5, c * 5) if i in gold)
        cs.set(r, 'Chapter_Total_Gold_표기', fmt(tot))
    zs = Sheet(W['Zombie.xlsx']['Chapter_Zombie_Stats_150'])
    for r in zs.rows():
        c = zs.get(r, 'Chapter'); i0, i4 = (c - 1) * 5, (c - 1) * 5 + 4
        if i0 in gold: zs.set(r, 'Base_Gold', fmt(gold[i0][0]))
        if i4 in gold and gold[i4][2] > 0: zs.set(r, 'Boss_Ogre_Gold', fmt(gold[i4][2]))

    # ---------- Global_Config 새 키 ----------
    gc = Sheet(W['Config.xlsx']['Global_Config'])
    keys = {gc.get(r, 'Key'): r for r in gc.rows()}
    gc.set(keys['GoldBase'], 'Value', 30 * GOLD_MUL)
    gc.set(keys['GoldBase'], 'Description', 'Stage 1-1 (idx=0) 기본 좀비 처치 골드 (10-20 골드 1/2)')
    new = [
        ('AdGems_Daily', AD_GEMS_DAILY, '회/일', '광고', 'Shop.xlsx', '상점 [무료 다이아] 광고 하루 횟수'),
        ('AdGems_Amount', AD_GEMS_AMOUNT, '다이아', '광고', 'Shop.xlsx', '상점 [무료 다이아] 광고 1번에 받는 다이아'),
        ('Mission_Gem_Ratio', GEM_RATIO, '비율', '미션', 'Mission.xlsx', '미션 보상(다이아로 친 값) 중 다이아로 주는 비율 — 나머지는 골드'),
        ('Gem_Gold_Kills', GEM_KILLS, '마리', '미션', 'Mission.xlsx', '골드 1다이아어치 = 지금 스테이지 좀비 N마리 골드 x 돈 배율 (상점 골드 상자 100다이아 = 600마리와 같은 비율)'),
        ('Free_Gem_Share', FREE_SHARE, '비율', '밸런스', '-', '참고값 (게임은 안 읽음): 150챕터까지 무료로 버는 다이아 ÷ 다이아로 살 수 있는 것 전부 = 무료 3 : 결제 7'),
    ]
    for k, v, u, cat, tbl, desc in new:
        r = keys.get(k) or gc.ws.max_row + 1
        for name, val in zip(['Key', 'Value', 'Unit', 'Category', 'Control_Table', 'Description'], [k, v, u, cat, tbl, desc]): gc.set(r, name, val)

    # ---------- 상점: 광고 무료 다이아 ----------
    sp = Sheet(W['Shop.xlsx']['Shop_Products'])
    for r in sp.rows():
        if sp.get(r, 'Index') is not None and sp.get(r, 'Index') >= 2: sp.set(r, 'Index', sp.get(r, 'Index') + 1)
    r = sp.ws.max_row + 1
    for k, v in [('Index', 2), ('Product_Id', 'free_gems_ad'), ('Category', 'Ad_Gems'), ('Name', '무료 다이아'),
                 ('Description', f'광고 1편 · 다이아 {AD_GEMS_AMOUNT} · 하루 {AD_GEMS_DAILY}번'), ('Price_Type', 'Ad'), ('Price_Value', '광고'),
                 ('Bonus_Gems', AD_GEMS_AMOUNT), ('Reward_Multiplier_Or_Item', f'Gems+{AD_GEMS_AMOUNT} (Global_Config AdGems_Daily·AdGems_Amount)'),
                 ('Impl_Status', '구현 완료 (10-20)')]:
        sp.set(r, k, v)

    # ---------- 미션 구조 ----------
    mm = Sheet(W['Mission.xlsx']['Mission_Master'])
    mm.rename('Base_Reward_Gems', 'Base_Reward_Value')
    mm.add_col('Max_Tier')
    for r in mm.rows():
        i = mm.get(r, 'Mission_Id')
        if i == 'stage': mm.set(r, 'Max_Tier', 150); mm.set(r, 'Impl_Status', '10-20: 5스테이지마다 1단계, 750(150단계)에서 끝')
        if i == 'merge': mm.set(r, 'Tier_1_7_Preset', '1|2|4|8|16|32|64'); mm.set(r, 'Default_Growth_Rate', 2); mm.set(r, 'Impl_Status', '10-20: 1·2·4·8… (두 배씩)')
        if i == 'kill': mm.set(r, 'Impl_Status', '10-20: 1단계 ≈ 1.5챕터 처치 수 (Mission_Tier Kill_Target)')

    # ---------- 광산: 보석 전부 기본 다이아 · 필요한 타수 ----------
    ore = Sheet(W['Mine.xlsx']['Mine_Ore_Spec'])
    MINE_FULL = sum((ore.get(r, 'Base_Reward_Gems') or 0) * (ore.get(r, 'Spawn_Count') or 0) for r in ore.rows())
    hits = sum((ore.get(r, 'Hit_HP') or 0) * (ore.get(r, 'Spawn_Count') or 0) for r in ore.rows())
    ms = Sheet(W['Mine.xlsx']['Mine_Scaling_150Ch'])
    ms.rename('Max_Gems_Ad_x3', 'Max_Gems_Ad_x2 (광고2배)')
    for r in ms.rows(): ms.set(r, 'Required_Total_Hits', hits)

    # 던전 결과 광고 배율 x2 (칸 이름만)
    Sheet(W['Challenge.xlsx'].worksheets[0]).rename('Reward_Ad_x3_Gems', 'Reward_Ad_x2_Gems')
    Sheet(W['Boss_Dungeon.xlsx'].worksheets[0]).rename('Reward_Ad_x3_Gems', 'Reward_Ad_x2_Gems')


# =====================================================================
# 매개변수 변경
# =====================================================================
def edit_params(W, P):
    K, bounds, M = P['K'], P['bounds'], P['M']
    # ---------- 골드 강화 표 ----------
    pars, rows = TU.candidate(K, spd_mul=True, bounds=bounds)
    ws = W['Player.xlsx']['Gold_Upgrade_1500']
    sh = Sheet(ws)
    if 'Spd_Add_Per_Lv' in sh.col: sh.rename('Spd_Add_Per_Lv', 'Spd_Growth_Rate')
    cum = 0
    for lv, r in enumerate(rows):
        R = lv + 2
        cum += r['Upgrade_Cost']
        vals = {
            'Lv': lv, 'Target_Chapter': min(150, lv // 10 + 1), 'Cost_Growth_Rate': round(r['Cost_Growth_Rate'], 5),
            'Upgrade_Cost': round(r['Upgrade_Cost'], 2), 'Cost_표기': fmt(r['Upgrade_Cost']), 'Cum_Cost_표기': fmt(cum),
            'Stat_Growth_Rate': round(r['Stat_Growth_Rate'], 5), 'Atk_Power': round(r['Atk_Power'], 4), 'Atk_Mul': round(r['Atk_Power'] / 100, 6),
            'Atk_표기': fmt(r['Atk_Power']), 'Spd_Growth_Rate': round(r['Stat_Growth_Rate'], 5), 'Atk_Speed': round(r['Atk_Speed'], 4),
            'Spd_Mul': round(r['Atk_Speed'] / 100, 6), 'Gold_Growth_Rate': round(r['Gold_Growth_Rate'], 5),
            'Money_Gain': round(r['Money_Gain'], 4), 'Gold_Mul': round(r['Money_Gain'] / 100, 6),
        }
        for k, v in vals.items(): sh.set(R, k, v)
    br = Sheet(W['Config.xlsx']['Bracket_Growth_Rules'])
    if 'Upgrade_Spd_Add' in br.col: br.rename('Upgrade_Spd_Add', 'Upgrade_Spd_Growth')
    b = [0] + list(bounds) + [1500]
    for i in range(5):
        R = i + 2
        cg, a, m = pars[i]
        br.set(R, 'Player_Lv_Range', f'Lv.{b[i] + (1 if i else 0)} ~ {b[i + 1]}')
        br.set(R, 'Upgrade_Cost_Growth', round(cg, 5)); br.set(R, 'Upgrade_Stat_Growth', round(a, 5)); br.set(R, 'Upgrade_Spd_Growth', round(a, 5))

    # ---------- 보상 배율 M: 도전 · 보스 던전 · 광산 · 미션 ----------
    ch = Sheet(W['Challenge.xlsx'].worksheets[0])
    cum = 0
    for r in ch.rows():
        g = round(ch.get(r, 'Reward_Gems') * M); cum += g
        ch.set(r, 'Reward_Gems', g); ch.set(r, 'Reward_Add_Gems', round((ch.get(r, 'Reward_Add_Gems') or 0) * M))
        ch.set(r, 'Reward_Ad_x2_Gems', g * 2); ch.set(r, 'Cum_Reward_Gems', cum)
    bd = Sheet(W['Boss_Dungeon.xlsx'].worksheets[0])
    boss_by_ch = []
    cum = 0
    for r in bd.rows():
        base = bd.get(r, 'Reward_Gems')
        boss_by_ch.append((bd.get(r, 'Recommended_Chapter'), base))
        g = round(base * M); cum += g
        bd.set(r, 'Reward_Gems', g); bd.set(r, 'Reward_Add_Gems', round((bd.get(r, 'Reward_Add_Gems') or 0) * M))
        bd.set(r, 'Reward_Ad_x2_Gems', g * 2); bd.set(r, 'Cum_Reward_Gems', cum)
    # 광산 티켓 1장 = 같은 챕터 보스 던전 1회 보상 x MINE_RATIO (배율 M 포함)
    ms = Sheet(W['Mine.xlsx']['Mine_Scaling_150Ch'])
    prev = None
    for r in ms.rows():
        c = ms.get(r, 'Unlock_Chapter')
        rew = [g for rc, g in boss_by_ch if rc <= c]
        per = (rew[-1] if rew else boss_by_ch[0][1]) * M * MINE_RATIO
        mul = round(per / MINE_FULL, 3)
        ms.set(r, 'Reward_Multiplier', mul); ms.set(r, 'Reward_Growth_Add', round(mul - prev, 3) if prev else 0); prev = mul
        ms.set(r, 'Small_Ore_Gems', round(0.1 * mul, 2)); ms.set(r, 'Big_Ore_Gems', round(mul, 2))
        ms.set(r, 'Max_Gems_Per_Ticket', round(MINE_FULL * mul, 1)); ms.set(r, 'Max_Gems_Ad_x2', round(MINE_FULL * mul * 2, 1))
        ms.set(r, 'Daily_2Tickets_Ad_Max', round(MINE_FULL * mul * 4, 1))
    mm = Sheet(W['Mission.xlsx']['Mission_Master'])
    base = {}
    for r in mm.rows():
        base[mm.get(r, 'Mission_Id')] = (mm.get(r, 'Base_Reward_Value'), mm.get(r, 'Reward_Add_Per_10Tier'))
        mm.set(r, 'Base_Reward_Value', round(mm.get(r, 'Base_Reward_Value') * M))
        mm.set(r, 'Reward_Add_Per_10Tier', round(mm.get(r, 'Reward_Add_Per_10Tier') * M))

    # ---------- 미션 단계 표 ----------
    mt = Sheet(W['Mission.xlsx']['Mission_Tier_150'])
    if 'Reward_Gems' in mt.col: mt.rename('Reward_Gems', 'Reward_Value (다이아30%+골드70%)')
    if 'Cum_Reward_Gems_Per_Mission' in mt.col: mt.rename('Cum_Reward_Gems_Per_Mission', 'Cum_Reward_Value_Per_Mission')
    kt = P['kill_targets']
    bv, ba = round(base['kill'][0] * M), round(base['kill'][1] * M)
    cum = 0
    for r in mt.rows():
        t = mt.get(r, 'Tier')
        k = kt[t - 1]
        mt.set(r, 'Expected_Chapter', round(1.5 * t, 1))
        mt.set(r, 'Kill_Target', k); mt.set(r, 'Kill_Target_표기', fmt(k))
        mt.set(r, 'Kill_Growth_Rate', max(1.01, round(k / kt[t - 2], 4)) if t > 1 else 1)   # 마지막 줄 = 표 너머 성장률 (게임도 최소 1.01)
        mt.set(r, 'Stage_Add_Step', 5); mt.set(r, 'Stage_Target', 5 * t)
        v = bv + ba * ((t - 1) // 10); cum += v
        mt.set(r, 'Reward_Value', v); mt.set(r, 'Cum_Reward_Value_Per_Mission', cum)

    # ---------- 슬롯 강화 골드 = 그 챕터 골드 수입 ENCH_MIN 분 ----------
    we = Sheet(W['Weapon_Enchant.xlsx']['Weapon_Enchant_150'])
    inc = P['income']          # 챕터 → 초당 골드
    prevg = None
    for r in we.rows():
        lv = we.get(r, 'Enchant_Lv')
        g = float('%.3g' % (inc(lv) * 60 * ENCH_MIN))
        we.set(r, 'Cost_Gold', g); we.set(r, 'Cost_Gold_표기', fmt(g))
        we.set(r, 'Gold_Cost_Growth', round(g / prevg, 4) if prevg else 1); prevg = g


def write(W):
    for name, wb in W.items(): wb.save(TB + name)
    XC.main()
    B.load()


# =====================================================================
def sinks():
    """다이아로 살 수 있는 것 전부 (스킨·보급품·총알 SSU 3칸·슬롯 강화 150 x 3, 실패 확률 포함)"""
    sk = sum(r['Price_Gems'] for r in B.SKIN)
    su = sum(r['Cost_Gems'] for r in B.SUP)
    wp = 3 * B.cfg('GachaCost', 100) * 2 ** B.MAXG
    en = 3 * sum(r['Cost_Gems'] / (r['Success_Rate'] / 100) for r in B.ENCH)
    return dict(skin=sk, supply=su, weapon=wp, enchant=en, total=sk + su + wp + en)


def run(**kw):
    p = B.P(**kw)
    log = {str(k): v for k, v in p.run(max_days=1500).items()}
    return p, log


def milestones(log):
    return {c: (log[str(c)]['day'] if str(c) in log else 9999) for c in TARGET}


def income_fn(log):
    pts = sorted((int(c), max(1e-9, v['grate'])) for c, v in log.items())
    def f(ch):
        if ch <= pts[0][0]: return pts[0][1]
        for (c0, g0), (c1, g1) in zip(pts, pts[1:]):
            if c0 <= ch <= c1: return math.exp(math.log(g0) + (math.log(g1) - math.log(g0)) * (ch - c0) / max(1, c1 - c0))
        return pts[-1][1]
    return f


def kill_targets(log):
    pts = sorted((int(c), v['kills']) for c, v in log.items())
    def at(ch):
        if ch <= pts[0][0]: return pts[0][1] * ch / max(1, pts[0][0])
        for (c0, k0), (c1, k1) in zip(pts, pts[1:]):
            if c0 <= ch <= c1: return k0 + (k1 - k0) * (ch - c0) / max(1, c1 - c0)
        # 150챕터 너머: 마지막 30챕터의 챕터당 처치 수로 이어서 (반복 사냥으로 계속 늘어남)
        c1, k1 = pts[-1]
        c0, k0 = min(pts, key=lambda x: abs(x[0] - (c1 - 30)))
        return k1 + (k1 - k0) / max(1, c1 - c0) * (ch - c1)
    out, last = [], 0
    for t in range(1, 151):
        v = at(1.5 * t)
        v = float('%.3g' % v)                 # 보기 좋게 세 자리
        v = max(v, last + 1); out.append(int(v)); last = v
    return out


PARAMS = os.path.join(TOOLS, 'balance_1020_params.json')


def write_all(P):
    W = restore(); edit_static(W); edit_params(W, P); write(W)


def sim_k(K, bounds, until=750, ads=True, hours=HOURS, max_days=1500):
    """골드 강화 표만 바꿔서(메모리) 시뮬레이션"""
    _, rows = TU.candidate(K, spd_mul=True, bounds=bounds)
    old = B.UPG
    B.UPG = rows
    try:
        p = B.P(ads=ads, hours=hours)
        log = {str(k): v for k, v in p.run(max_days=max_days, until=until).items()}
    finally:
        B.UPG = old
    return p, log


def day_at(log, ch): return log[str(ch)]['hours'] / HOURS if str(ch) in log else 9999


def bisect(f, target, lo=0.08, hi=2.0, n=14):
    """f(k) = 날 수 (k 가 클수록 빠름) → f(k) ≈ target 인 k"""
    for _ in range(n):
        mid = math.sqrt(lo * hi)
        if f(mid) > target: lo = mid
        else: hi = mid
    return math.sqrt(lo * hi)


def tune(P):
    P['income'] = P.get('income') or (lambda ch: 1.0)
    for outer in range(2):
        write_all(P)
        for pas in range(2):
            b = P['bounds']
            kA = bisect(lambda k: day_at(sim_k([k] * 5, b, until=250, max_days=TARGET[50] * 3)[1], 50), TARGET[50])
            kB = bisect(lambda k: day_at(sim_k([kA] * 3 + [k, k], b, until=500, max_days=TARGET[100] * 3)[1], 100), TARGET[100])
            kC = bisect(lambda k: day_at(sim_k([kA] * 3 + [kB, k], b, max_days=TARGET[150] * 3)[1], 150), TARGET[150])
            P['K'] = [kA, kA, kA, kB, kC]
            p, log = sim_k(P['K'], b)
            l50, l100 = log['50']['lv'][0] if '50' in log else 700, log['100']['lv'][0] if '100' in log else 1350
            P['bounds'] = [80, 250, int(min(max(300, l50), 1200)), int(min(max(l50 + 100, l100), 1450))]
            print(f'  {outer}.{pas} K', [round(k, 4) for k in P['K']], 'bounds', b, '→', P['bounds'],
                  'days', {c: round(day_at(log, c), 1) for c in TARGET}, flush=True)
        S = sinks()
        p, log = sim_k(P['K'], P['bounds'])
        free = sum(p.gem_src.values()); ad = p.gem_src['ad']
        share = free / S['total']
        print(outer, 'M %.3f' % P['M'], 'share %.3f' % share, 'free %.0f' % free, 'sinks %.0f' % S['total'], flush=True)
        P['income'] = income_fn(log); P['kill_targets'] = kill_targets(log)
        if abs(share - FREE_SHARE) < 0.01: break
        P['M'] = max(0.05, P['M'] * (FREE_SHARE * S['total'] - ad) / max(1, free - ad))
    return P


def main():
    P = dict(K=[0.35, 0.35, 0.35, 0.41, 0.415], bounds=[80, 250, 643, 1207], M=0.577, kill_targets=[100 * t for t in range(1, 151)])
    if '--retune' in sys.argv and os.path.exists(PARAMS):
        # 저장한 값에서 출발해 다시 맞추기 (규칙을 조금 바꿨을 때)
        P.update(json.load(io.open(PARAMS, encoding='utf-8')))
        inc = P.pop('income_pts')
        P['income'] = income_fn({str(c): dict(grate=g) for c, g in inc})
        for a in sys.argv:
            if a.startswith('--M='): P['M'] = float(a[4:])
        P = tune(P)
    elif '--apply' in sys.argv and os.path.exists(PARAMS):
        # 저장한 값으로 다시 쓰기만 (맞추기 없이)
        P.update(json.load(io.open(PARAMS, encoding='utf-8')))
        inc = P.pop('income_pts')
        P['income'] = income_fn({str(c): dict(grate=g) for c, g in inc})
    else:
        # 처음: 구조만 바꾼 표로 한 번 돌려서 수입·처치 곡선을 얻음
        P['income'] = lambda ch: 1.0
        write_all(P)
        p, log = sim_k(P['K'], P['bounds'])
        P['income'] = income_fn(log); P['kill_targets'] = kill_targets(log)
        P = tune(P)
    write_all(P)
    # 저장 (다음에 --apply 로 같은 표를 다시 만들 수 있게)
    json.dump(dict(K=P['K'], bounds=P['bounds'], M=P['M'], kill_targets=P['kill_targets'],
                   income_pts=[(c, P['income'](c)) for c in range(1, 151)]), io.open(PARAMS, 'w', encoding='utf-8'), indent=1)

    # 확인: csv 를 새로 읽어서 (게임과 같은 표) 광고 최대 · 2시간 · 10시간 · 광고 없음
    B.load()
    S = sinks()
    res = dict(params=dict(K=P['K'], bounds=P['bounds'], M=P['M'], ench_min=ENCH_MIN, gold_mul=GOLD_MUL, free_share=FREE_SHARE), sinks=S, runs={})
    for name, kw in [('ads', dict(ads=True, hours=4)), ('ads2h', dict(ads=True, hours=2)), ('ads10h', dict(ads=True, hours=10)), ('noad', dict(ads=False, hours=4))]:
        p = B.P(**kw)
        log = {str(k): v for k, v in p.run(max_days=3000).items()}
        res['runs'][name] = dict(log=log, end=p.snap(), stage=p.stage, day=p.day)
        print(name, 'day', p.day, 'stage', p.stage, {c: log[str(c)]['day'] if str(c) in log else None for c in (10, 25, 50, 75, 100, 125, 150)}, flush=True)
    json.dump(res, io.open(os.path.join(TOOLS, 'balance_1020.json'), 'w', encoding='utf-8'), default=float, ensure_ascii=False, indent=1)
    print('끝 — K', [round(k, 4) for k in P['K']], 'bounds', P['bounds'], 'M %.3f' % P['M'])


if __name__ == '__main__':
    main()
