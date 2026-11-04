# -*- coding: utf-8 -*-
"""11-04 요청_02 스킨 재밸런스: "스킨 모두 구매 = 100만원 정도" + "무기와 스킨 효율을 어느 정도 맞춤"
사용: python tools/skin_balance.py            → 제안값 계산 + 시뮬 비교 (표는 안 바꿈, 결과 tools/_skin_balance.json)
      python tools/skin_balance.py --apply    → Kachi_Gun/Table/Skin.xlsx 에 적용 (Excel 로 — 누적 가격 칸이 수식이라서)

1) 가격: 지금 표의 가격 곡선(사용자 11-04 판, 500 → 12,000)을 비율 그대로 키워 합계 = 100만원어치 다이아
   (원화 → 다이아 = 가장 큰 다이아 상자 비율, Shop_Products gem_pack_large: 4,200개 = 11,900원)
2) 효과: 스킨 i 의 추천 챕터에 도착했을 때의 시뮬 상태(앞 스킨은 가졌다고 침)에서
   "스킨 i 를 산 효율"(다이아 1개당 log 초당 피해 + 0.3 log 금전 — balance_sim 의 gem_options 와 같은 식) =
   "총알 한 등급 올리기 효율" 이 되도록 스킨 i 의 공격·속도·금전 보너스를 같은 비율로 키우거나 줄임
   → 3번 되풀이(바뀐 스킨으로 시뮬을 다시 돌려 상태를 다시 잼) · 뒤 스킨이 앞 스킨보다 약하지 않게 · 정수 %
"""
import copy, io, json, math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import balance_sim as B

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
XLSX = os.path.join(ROOT, 'Kachi_Gun', 'Table', 'Skin.xlsx')
OUT = os.path.join(ROOT, 'tools', '_skin_balance.json')
TARGET_KRW = 1_000_000


def gem_rate():
    shop = {r['Product_Id']: r for r in B.rd('Shop_Products')}
    r = shop['gem_pack_large']
    return r['Bonus_Gems'] / r['Price_Value']          # 다이아 / 원


class Snap(B.P):
    """챕터가 바뀔 때마다 상태를 통째로 복사해 둠"""
    def snap(s):
        d = super().snap()
        if not getattr(s, '_copying', False):
            s._copying = True
            s.states = getattr(s, 'states', {})
            ch = s.stage // 5 + 1
            if ch not in s.states:
                c = copy.copy(s); c.__dict__ = copy.deepcopy({k: v for k, v in s.__dict__.items() if k != 'states'})
                s.states[ch] = c
            s._copying = False
        return d


def run_states(ads):
    p = Snap(ads=ads); p.run(max_days=1500)
    return p


def shares(ads):
    p = B.P(ads=ads); p.run(max_days=1500)
    u = p.gem_use
    return u.get('skin', 0), u.get('weapon', 0), p


def calibrate(ads=True):
    """스킨 효과 전체에 곱할 배율 m 하나를 찾음: 시뮬에서 스킨에 쓴 다이아 ≈ 총알 합성에 쓴 다이아 (각 스킨의 모양·비율은 그대로)"""
    base = {int(r['Id']): (r['Atk_Bonus'], r['Spd_Bonus'], r['Gold_Bonus']) for r in B.SKIN}

    def set_m(m):
        for r in B.SKIN[1:]:
            a0, s0, g0 = base[int(r['Id'])]
            r['Atk_Bonus'], r['Spd_Bonus'], r['Gold_Bonus'] = a0 * m, s0 * m, g0 * m

    def gap(m):
        set_m(m); sk, wp, _ = shares(ads)
        return math.log((sk + 1) / (wp + 1))
    lo, hi = 0.25, 8.0
    log = []
    for _ in range(14):
        mid = math.sqrt(lo * hi); g = gap(mid); log.append((round(mid, 3), round(g, 3)))
        if g < 0: lo = mid
        else: hi = mid
    m = math.sqrt(lo * hi); set_m(m)
    # 정수 % (뒤 스킨이 앞보다 약하지 않게)
    prev = (0, 0, 0)
    for r in B.SKIN[1:]:
        vals = [max(1, round(v)) for v in (r['Atk_Bonus'], r['Spd_Bonus'], r['Gold_Bonus'])]
        r['Atk_Bonus'], r['Spd_Bonus'], r['Gold_Bonus'] = (max(v, pv) for v, pv in zip(vals, prev))
        prev = (r['Atk_Bonus'], r['Spd_Bonus'], r['Gold_Bonus'])
    return m, log


def summary(tag, ads):
    p = B.P(ads=ads); log = p.run(max_days=1500)
    days = {c: (log[c]['day'] if c in log else None) for c in (10, 30, 60, 100, 150)}
    use = p.gem_use; tot = sum(use.values()) or 1
    return dict(tag=tag, ads=ads, day_end=p.day, chapter=p.stage // 5 + 1, days=days, skins=len(p.skins) - 1,
                gem_use={k: f"{v / tot * 100:.0f}%" for k, v in use.items()})


def main():
    apply = '--apply' in sys.argv
    B.load()
    old = [dict(r) for r in B.SKIN]
    before = [summary('지금', False), summary('지금', True)]
    rate = gem_rate()
    total = TARGET_KRW * rate
    psum = sum(r['Price_Gems'] for r in B.SKIN)
    k = total / psum
    for r in B.SKIN[1:]:
        r['Price_Gems'] = max(100, round(r['Price_Gems'] * k / 100) * 100)
    m, log = calibrate(True)
    print(f'스킨 효과 배율 x{m:.2f} (시뮬 광고 최대: 스킨에 쓴 다이아 ≈ 총알에 쓴 다이아)')
    after = [summary('새 값', False), summary('새 값', True)]
    rows = []
    cum = [0, 0, 0, 0]
    for o, r in zip(old, B.SKIN):
        cum = [cum[0] + r['Price_Gems'], cum[1] + r['Atk_Bonus'], cum[2] + r['Spd_Bonus'], cum[3] + r['Gold_Bonus']]
        rows.append(dict(id=int(r['Id']), name=r['Name'], ch=int(r['Recommended_Chapter']),
                         price_old=o['Price_Gems'], price=r['Price_Gems'],
                         atk_old=o['Atk_Bonus'], atk=r['Atk_Bonus'], spd_old=o['Spd_Bonus'], spd=r['Spd_Bonus'],
                         gold_old=o['Gold_Bonus'], gold=r['Gold_Bonus'], cum=list(cum)))
    res = dict(rate_gems_per_krw=rate, total_gems=sum(r['price'] for r in rows), total_krw=sum(r['price'] for r in rows) / rate,
               old_total_gems=psum, old_total_krw=psum / rate,
               old_mul=[1 + sum(r['atk_old'] for r in rows) / 100, 1 + sum(r['spd_old'] for r in rows) / 100, 1 + sum(r['gold_old'] for r in rows) / 100],
               new_mul=[1 + cum[1] / 100, 1 + cum[2] / 100, 1 + cum[3] / 100],
               rows=rows, skin_mul=m, calib=log, before=before, after=after)
    json.dump(res, io.open(OUT, 'w', encoding='utf-8'), ensure_ascii=False, indent=1, default=float)
    print(f"원화 환율 {1 / rate:.2f}원/다이아 · 합계 {res['old_total_gems']:,.0f} → {res['total_gems']:,.0f} 다이아 ({res['old_total_krw']:,.0f} → {res['total_krw']:,.0f}원)")
    print('모두 산 배율 공격·속도·금전', [round(x, 2) for x in res['old_mul']], '→', [round(x, 2) for x in res['new_mul']])
    for r in rows[1:]:
        print(f"  {r['id']:2} {r['name']:8} ch{r['ch']:3}  가격 {r['price_old']:>6,.0f} → {r['price']:>7,.0f}  공격 {r['atk_old']:>4.0f} → {r['atk']:>4.0f}  속도 {r['spd_old']:>4.0f} → {r['spd']:>4.0f}  금전 {r['gold_old']:>3.0f} → {r['gold']:>3.0f}")
    for b, a in zip(before, after):
        print(('광고 최대' if b['ads'] else '광고 안 봄'), '150챕터', b['days'][150], '→', a['days'][150], '일 · 산 스킨', b['skins'], '→', a['skins'], '· 다이아 쓴 곳', b['gem_use'], '→', a['gem_use'])
    if apply: write_xlsx(rows)


def write_xlsx(rows):
    """Excel 로 값만 씀 (Cum_Price_Gems 수식은 그대로) — Price_Gems · Atk/Spd/Gold_Add_Pct · _Bonus · Cum_* · Total_*_Mul"""
    import win32com.client as w
    xl = w.DispatchEx('Excel.Application'); xl.Visible = False; xl.DisplayAlerts = False
    try:
        wb = xl.Workbooks.Open(XLSX); ws = wb.Worksheets(1)
        hdr = {ws.Cells(1, c).Value: c for c in range(1, 30) if ws.Cells(1, c).Value}
        prev = (0, 0, 0)
        for i, r in enumerate(rows):
            y = i + 2
            assert int(ws.Cells(y, hdr['Id']).Value) == r['id']
            ws.Cells(y, hdr['Price_Gems']).Value = r['price']
            ws.Cells(y, hdr['Atk_Bonus (%)']).Value = r['atk']; ws.Cells(y, hdr['Atk_Add_Pct']).Value = r['atk'] - prev[0]
            ws.Cells(y, hdr['Spd_Bonus (%)']).Value = r['spd']; ws.Cells(y, hdr['Spd_Add_Pct']).Value = r['spd'] - prev[1]
            ws.Cells(y, hdr['Gold_Bonus (%)']).Value = r['gold']; ws.Cells(y, hdr['Gold_Add_Pct']).Value = r['gold'] - prev[2]
            ws.Cells(y, hdr['Cum_Atk_Bonus (%)']).Value = r['cum'][1]; ws.Cells(y, hdr['Cum_Spd_Bonus (%)']).Value = r['cum'][2]; ws.Cells(y, hdr['Cum_Gold_Bonus (%)']).Value = r['cum'][3]
            ws.Cells(y, hdr['Total_Atk_Mul']).Value = round(1 + r['cum'][1] / 100, 2); ws.Cells(y, hdr['Total_Spd_Mul']).Value = round(1 + r['cum'][2] / 100, 2); ws.Cells(y, hdr['Total_Gold_Mul']).Value = round(1 + r['cum'][3] / 100, 2)
            prev = (r['atk'], r['spd'], r['gold'])
        wb.Save(); wb.Close()
        print('Skin.xlsx 에 적용')
    finally:
        xl.Quit()


if __name__ == '__main__':
    main()
