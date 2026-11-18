# -*- coding: utf-8 -*-
"""11-18 카피친구 · 친구 던전 밸런스 시뮬레이터 (balance_sim.py 위에 친구를 더함).
사용: PYTHONUTF8=1 python tools/friend_balance.py            → 지금 표(Friend_List · Friend_Level · Config)로 잼 + friend_balance.html
      PYTHONUTF8=1 python tools/friend_balance.py --tune     → "광고 다 봄 · 하루 4시간"에 15마리 모두 Lv.10 이 TARGET 일 근처가 되는 필요 카드 배율을 찾아 보여 줌

가정 (게임 코드와 같은 식):
 - 2-1(stage 5)부터 하루 무료 FriendDgn_Tickets + 광고 FriendDgn_Ad_Daily(광고 볼 때만) 번 입장
 - 던전 L 이김 = 보스 FriendDgn_Bosses 마리 체력 합 ≤ 분대 보스 DPS x (FriendDgn_Time - 보스 사이 0.7초) x EFF
   보스 체력 = Tables.FriendDgnHp: 챕터 2L 첫 스테이지 좀비 체력 x 그 챕터 보스 배율 x FriendDgn_Hp_Mul
 - 매번 이길 수 있는 가장 높은 단계 (깬 단계 + 1 까지) — 못 이기면 깬 단계를 다시 (보상 받음)
 - 카드 = FriendDgnCards(L) 장을 그 단계에서 나오는 친구에게 Weight 비중으로 "기댓값"만큼 나눔 (운 없음)
 - 카드가 모이면 바로 레벨 업 · 효과는 게임처럼 공격·속도·금전은 곱 (1 + 합%), 보스 피해는 보급품 보스와 더함
"""
import io, json, math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import balance_sim as B

EFF = 0.85          # 보스가 나오며 걸어오는 시간 · 빗나감 등 (보스 던전 시뮬레이터와 비슷하게)
TARGET = 400        # 사용자 11-18: "모든 광고를 다 보았을 경우 400일 정도 기준"
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')


def friends_table():
    F = B.rd('Friend_List'); LV = B.rd('Friend_Level')
    return F, [int(r['Cards_Need']) for r in sorted(LV, key=lambda r: r['Lv'])]


class FP(B.P):
    def __init__(s, F, need, mul=1.0, unlock=None, bmul=1.0, **kw):
        super().__init__(**kw)
        s.F = F; s.need = need; s.mul = mul; s.bmul = bmul
        s.unlock = unlock or [int(f['Unlock_Dgn_Lv']) for f in F]
        s.flv = [0] * len(F); s.fcard = [0.0] * len(F)
        s.fbest = 0; s.fruns = 0; s.fwins = 0
        s.flog = []          # (날, 깬 단계, 친구 레벨 합, 공격 %, 챕터)
        s.done_day = None; s.first_max = None

    # ---------- 친구 효과 ----------
    def fsum(s, stat):
        return sum(round(s.F[i]['Bonus_Per_Lv'] * s.bmul, 1) * s.flv[i] for i in range(len(s.F)) if s.F[i]['Stat'] == stat)

    def atkmul(s): return super().atkmul() * (1 + s.fsum('Atk') / 100)
    def spdmul(s): return super().spdmul() * (1 + s.fsum('Spd') / 100)
    def goldmul(s): return super().goldmul() * (1 + s.fsum('Gold') / 100)

    def sup_sum(s, k):
        v = super().sup_sum(k)
        return v + s.fsum('Boss') if k == 'Boss_Dmg_Bonus' else v

    # ---------- 표 계산 (Tables.cs 와 같음) ----------
    def need_of(s, i, lv):
        if lv <= 1: return 1
        return max(1, round(s.need[min(lv, len(s.need)) - 1] * s.F[i]['Card_Mul'] * s.mul))

    def dgn_hp(s, L):
        idx = (L * int(B.cfg('FriendDgn_Stage_Mul', 2)) - 1) * 5
        br = s.stage_row(idx // 5 * 5 + 4)
        r = s.stage_row(idx)
        ratio = br['Boss_HP'] / br['Base_Zombie_HP'] if br['Boss_HP'] > 0 else B.cfg('BossHpMul', 15)
        return r['Base_Zombie_HP'] * ratio * B.cfg('FriendDgn_Hp_Mul', 1)

    def dgn_cards(s, L):
        step, add = int(B.cfg('FriendDgn_Cards_Step', 5)), int(B.cfg('FriendDgn_Cards_Add', 1))
        return max(1, int(B.cfg('FriendDgn_Cards', 3)) + ((max(1, L) - 1) // step * add if step > 0 else 0))

    def can_win(s, L):
        n = B.cfg('FriendDgn_Bosses', 3)
        t = B.cfg('FriendDgn_Time', 60) - 0.7 * (n - 1)
        return s.dps(True) * t * EFF >= n * s.dgn_hp(L)

    # ---------- 하루 ----------
    def daily(s):
        super().daily()
        if s.stage < 5: return                                    # 2-1 에서 열림
        runs = int(B.cfg('FriendDgn_Tickets', 2)) + (int(B.cfg('FriendDgn_Ad_Daily', 3)) if s.ads else 0)
        for _ in range(runs):
            L = s.fbest + 1
            if s.can_win(L): s.fbest = L
            else: L = s.fbest
            s.fruns += 1
            if L <= 0: continue
            s.fwins += 1
            pool = [i for i in range(len(s.F)) if s.unlock[i] <= L]
            w = sum(s.F[i]['Weight'] for i in pool)
            c = s.dgn_cards(L)
            for i in pool:
                s.fcard[i] += c * s.F[i]['Weight'] / w
        # 레벨 업
        for i in range(len(s.F)):
            while True:
                lv = s.flv[i]
                if lv >= len(s.need): break
                nd = s.need_of(i, lv + 1)
                if s.fcard[i] + 1e-9 < nd: break
                s.fcard[i] -= nd; s.flv[i] = lv + 1
        full = all(l >= len(s.need) for l in s.flv)
        if any(l >= len(s.need) for l in s.flv) and s.first_max is None: s.first_max = s.day
        if full and s.done_day is None: s.done_day = s.day
        s.flog.append((s.day, s.fbest, sum(s.flv), round(s.fsum('Atk'), 1), s.stage // 5 + 1))


def run(F, need, mul=1.0, unlock=None, ads=True, days=900, bmul=1.0):
    p = FP(F, need, mul, unlock, bmul, ads=ads)
    log = p.run(max_days=days, until=10 ** 9)          # 150챕터를 넘어도 표 끝 너머로 이어서 (친구가 다 찰 때까지)
    while p.done_day is None and p.day < days:        # 스테이지 계산이 끝났으면 날만 넘기며 던전
        p.advance(p.hours * 3600)
    return p, log


def ch150(log):
    return next((v['day'] for c, v in sorted(log.items()) if c >= 150), 10 ** 6)


def tune2(F, need):
    """두 손잡이: 효과 배율 bmul(150챕터 날) x 필요 카드 배율 m(15마리 만렙 날) — 둘 다 TARGET 근처"""
    bm, m = 1.0, 1.0
    for it in range(8):
        m, d = tune(F, need, None, bmul=bm)
        lo, hi = 0.05, 1.5
        for _ in range(22):
            b = (lo + hi) / 2
            p, log = run(F, need, m, None, days=1200, bmul=b)
            if ch150(log) > TARGET: lo = b
            else: hi = b
        bm = (lo + hi) / 2
        p, log = run(F, need, m, None, days=1200, bmul=bm)
        print(f'  {it}: 효과 x{bm:.3f} · 필요 카드 x{m:.3f} → 150챕터 {ch150(log)}일 · 15마리 만렙 {p.done_day}일')
        if abs(ch150(log) - TARGET) <= 8 and p.done_day and abs(p.done_day - TARGET) <= 8: break
    return bm, m


def tune(F, need, unlock, lo=0.05, hi=8.0, bmul=1.0):
    """15마리 만렙 날이 TARGET 에 가장 가까운 필요 카드 배율"""
    best = None
    for _ in range(30):
        m = (lo + hi) / 2
        p, _ = run(F, need, m, unlock, bmul=bmul)
        d = p.done_day or 10 ** 6
        if best is None or abs(d - TARGET) < abs(best[1] - TARGET): best = (m, d)
        if d > TARGET: hi = m
        else: lo = m
    return best


def main():
    F, need = friends_table()
    args = sys.argv[1:]
    if '--tune2' in args:
        bm, m = tune2(F, need)
        print(f'효과 x{bm:.3f} · 필요 카드 x{m:.3f}')
        return
    if '--tune' in args:
        m, d = tune(F, need, None)
        print(f'필요 카드 x{m:.3f} → 광고 다 봄 15마리 만렙 {d}일 (목표 {TARGET})')
        return
    rows = []
    for name, kw in [('광고 다 봄 · 하루 4시간', dict(ads=True)), ('광고 안 봄 · 하루 4시간', dict(ads=False))]:
        p, log = run(F, need, 1.0, None, days=2000, **kw) if kw['ads'] else run(F, need, 1.0, None, ads=False, days=2000)
        ch150 = next((v['day'] for c, v in sorted(log.items()) if c >= 150), None)
        rows.append(dict(name=name, done=p.done_day, first=p.first_max, ch150=ch150, flog=p.flog, wins=p.fwins, best=p.fbest,
                         atk=p.fsum('Atk'), spd=p.fsum('Spd'), gold=p.fsum('Gold'), boss=p.fsum('Boss')))
        print(name, '· 15마리 만렙', p.done_day, '일 · 첫 만렙', p.first_max, '일 · 150챕터', ch150, '일 · 깬 단계', p.fbest)
    json.dump(rows, io.open(os.path.join(ROOT, 'tools', '_friend_balance.json'), 'w', encoding='utf-8'), ensure_ascii=False, default=float)


if __name__ == '__main__':
    main()
