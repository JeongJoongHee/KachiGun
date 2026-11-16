# -*- coding: utf-8 -*-
"""11-17 상점 탭 (사용자 "상점에서 탭을 추가해서 상품을 배치") — Shop.xlsx 에 Tab(순서 번호) · Tab_Name(탭 글자) 칸. 여러 번 돌려도 같은 결과.
사용: python tools/shop_tabs_1117.py   (그 뒤 python tools/strings_collect.py 로 영어 글자 + csv)
 - 게임은 Tab 번호 순서로 탭을 늘어놓고, 고른 탭의 줄만 보여 줌 (탭 안 순서 = Index). 표에 없는 새 줄은 Tab 0 → 맨 앞 탭이 됨
 - gem_bomb_1117.py 뒤에 돌릴 것 (그 도구는 앞 10칸만 씀 — 이 칸은 끝 두 칸이라 안 건드림)
"""
import os
import openpyxl

P = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Kachi_Gun', 'Table', 'Shop.xlsx')

TABS = {   # 탭 번호: (탭 글자, 상품 ID)
    1: ('다이아', ['gem_bomb_s', 'gem_bomb_m', 'gem_bomb_l', 'gem_pack_small', 'gem_pack_mid', 'gem_pack_large']),
    2: ('패키지', ['starter_ss_missile', 'remove_all_ads', 'auto_upgrade']),
    3: ('무료', ['free_gems_ad', 'buff_atk', 'buff_gold']),
}


def main():
    wb = openpyxl.load_workbook(P)
    ws = wb['Shop_Products']
    head = [c.value for c in ws[1]]
    for name in ('Tab', 'Tab_Name'):
        if name not in head:
            ws.cell(1, len(head) + 1).value = name
            head.append(name)
    col = {h: i + 1 for i, h in enumerate(head)}
    of = {pid: (t, nm) for t, (nm, ids) in TABS.items() for pid in ids}
    miss = []
    for r in range(2, ws.max_row + 1):
        pid = ws.cell(r, col['Product_Id']).value
        if not pid: continue
        if pid not in of: miss.append(pid); continue
        ws.cell(r, col['Tab']).value, ws.cell(r, col['Tab_Name']).value = of[pid]
    wb.save(P)
    for t, (nm, ids) in TABS.items(): print(t, nm, ids)
    if miss: print('탭이 없는 상품 (Tab 0 = 맨 앞 탭):', miss)


if __name__ == '__main__':
    main()
