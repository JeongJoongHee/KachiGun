# -*- coding: utf-8 -*-
"""11-17 다이아 폭탄 (다이아_폭탄_추천.html 체크·의견 반영) — 표 고치기. 여러 번 돌려도 같은 결과.
사용: python tools/gem_bomb_1117.py   (그 뒤 python tools/strings_collect.py 로 영어 글자 + csv)
 - Shop.xlsx: 맨 위에 Gem_Bomb 3줄 (Index -3·-2·-1 = 상점 맨 위 — 상점은 Index 순서) (gem_bomb_s/m/l — Play Console 인앱 상품 ID 와 같아야 함)
              기존 다이아 상품 소폭 올림 (사용자 의견 "기존 상품의 다이아 개수를 소폭 상승"): 500→550 · 1,800→2,000 · 4,200→4,600 (약 +10%)
 - Config.xlsx: Gem_Bomb_Limit 3 (상품마다 한 달에 N번 · 매월 1일 0시(한국 시간)에 다시 · 0 = 폭탄 안 팖)
"""
import os
import openpyxl

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Kachi_Gun', 'Table')

NOTE = '11-17 다이아 폭탄 · 상품마다 한 달 Gem_Bomb_Limit 번 · 매월 1일 갱신 · 첫 구매 2배 안 붙음'
BOMBS = [
    [-3, 'gem_bomb_s', 'Gem_Bomb', '작은 다이아 폭탄 (3,500)', '다이아 3,500개 · 한 달에 3번', 'KRW', 4900, 3500, 'Gems+3500', NOTE],
    [-2, 'gem_bomb_m', 'Gem_Bomb', '다이아 폭탄 (8,000)', '다이아 8,000개 · 한 달에 3번', 'KRW', 9900, 8000, 'Gems+8000', NOTE],
    [-1, 'gem_bomb_l', 'Gem_Bomb', '초대형 다이아 폭탄 (27,000)', '다이아 27,000개 · 한 달에 3번', 'KRW', 29900, 27000, 'Gems+27000', NOTE],
]
# 기존 다이아 상품: id → (새 개수, 이름, 설명)
PACKS = {
    'gem_pack_small': (550, '다이아 한 줌 (550)', '다이아 550개 즉시 획득', '500 → 550'),
    'gem_pack_mid': (2000, '다이아 주머니 (2,000)', '다이아 2,000개 즉시 획득', '1,800 → 2,000'),
    'gem_pack_large': (4600, '다이아 상자 (4,600)', '다이아 4,600개 즉시 획득', '4,200 → 4,600'),
}


def shop():
    p = os.path.join(ROOT, 'Shop.xlsx')
    wb = openpyxl.load_workbook(p)
    ws = wb['Shop_Products']
    head = [c.value for c in ws[1]]
    col = {h: i + 1 for i, h in enumerate(head)}
    # 빈 줄 지움 (11-16 스타터팩 줄을 지우고 남은 빈 줄)
    for r in range(ws.max_row, 1, -1):
        if all(ws.cell(r, c).value in (None, '') for c in range(1, len(head) + 1)):
            ws.delete_rows(r)
    ids = {ws.cell(r, col['Product_Id']).value: r for r in range(2, ws.max_row + 1)}
    for pid, (n, name, desc, how) in PACKS.items():
        r = ids[pid]
        ws.cell(r, col['Name']).value = name
        ws.cell(r, col['Description']).value = desc
        ws.cell(r, col['Bonus_Gems']).value = n
        ws.cell(r, col['Reward_Multiplier_Or_Item']).value = f'Gems+{n}'
        st = str(ws.cell(r, col['Impl_Status']).value or '')
        if '11-17' not in st:
            ws.cell(r, col['Impl_Status']).value = (st + f' · 11-17 {how}').strip(' ·')
    # 폭탄 줄: 있으면 값만 맞추고, 없으면 맨 위(머리 줄 바로 아래)에 넣음
    for i, row in enumerate(BOMBS):
        r = ids.get(row[1])
        if r is None:
            ws.insert_rows(2 + i)
            r = 2 + i
            ids = {ws.cell(k, col['Product_Id']).value: k for k in range(2, ws.max_row + 1)}
        for c, v in enumerate(row, start=1):
            ws.cell(r, c).value = v
    wb.save(p)
    print('Shop.xlsx 줄:', [ws.cell(r, col['Product_Id']).value for r in range(2, ws.max_row + 1)])


def config():
    p = os.path.join(ROOT, 'Config.xlsx')
    wb = openpyxl.load_workbook(p)
    ws = wb['Global_Config']
    keys = {ws.cell(r, 1).value: r for r in range(2, ws.max_row + 1)}
    row = ['Gem_Bomb_Limit', 3, '회/월', '상점', 'Shop.xlsx',
           '11-17 다이아 폭탄(Gem_Bomb 줄) 상품마다 한 달에 살 수 있는 횟수 — 매월 1일 0시(한국 시간)에 다시 이만큼 · 0 = 폭탄을 안 팖 · 첫 구매 2배는 안 붙음']
    r = keys.get(row[0])
    if r is None:
        r = keys['Gem_First_Mul'] + 1
        ws.insert_rows(r)
    for c, v in enumerate(row, start=1):
        ws.cell(r, c).value = v
    wb.save(p)
    print('Config.xlsx', row[0], '=', row[1], '(줄', r, ')')


if __name__ == '__main__':
    shop()
    config()
