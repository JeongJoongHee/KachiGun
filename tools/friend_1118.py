# -*- coding: utf-8 -*-
"""11-18 1차 업데이트 (업데이트/1차 업데이트/261118.pptx) 표 만들기 — 여러 번 돌려도 같은 결과.
사용: PYTHONUTF8=1 python tools/friend_1118.py   (그 뒤 python tools/strings_collect.py 로 영어 글자 + csv)

 1) Table/Friend.xlsx 새로 만듦
    - Friend_List : 카피친구 15마리 (Friend_Id · Name · Stat(Atk/Spd/Gold/Boss) · Bonus_Per_Lv(%) · Unlock_Dgn_Lv(이 던전 단계부터 나옴) · Weight · Icon)
    - Friend_Level: 레벨마다 필요한 카드 (Lv1 = 처음 얻음 1장 · 최대 10레벨) — 효과 = Bonus_Per_Lv x 레벨
    ※ 숫자는 [임시] (사용자 규칙: 시스템 먼저, 밸런스는 마지막). 이미 있는 파일은 덮어씀 → 직접 고친 값이 있으면 이 도구를 먼저 고칠 것
 2) Config.xlsx Global_Config 에 친구 던전 · 무료 총알 줄 (없으면 더함, 있으면 값 그대로)
 3) Unlock.xlsx: friend 줄 더함 (보급품 자리 = 6-1) · 코스튬/보급품 줄 위치 글 = 컬렉션
 4) Shop.xlsx: free_bullets_ad (무료 탭, 무료 다이아 아래)
"""
import os
import openpyxl
from openpyxl.styles import Font, PatternFill

T = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Kachi_Gun', 'Table')

# 이름 · 능력 · 레벨당 % · 나오기 시작하는 던전 단계
FRIENDS = [
    ('토끼', 'Atk', 4, 1), ('다람쥐', 'Spd', 3, 1), ('햄스터', 'Gold', 4, 1),
    ('양', 'Boss', 4, 5), ('염소', 'Atk', 5, 10), ('사슴', 'Spd', 4, 15),
    ('나무늘보', 'Gold', 5, 20), ('코알라', 'Boss', 5, 30), ('웜뱃', 'Atk', 6, 40),
    ('기니피그', 'Spd', 5, 50), ('너구리', 'Gold', 6, 60), ('고양이', 'Boss', 6, 80),
    ('강아지', 'Atk', 8, 100), ('젖소', 'Gold', 8, 120), ('당나귀', 'Spd', 6, 150),
]
# 그 레벨이 되는 데 드는 카드 (Lv1 = 처음 얻을 때 1장) — 사용자 예시 "토끼 5/10 · 12/100" 처럼 뒤로 갈수록 많이
LEVELS = [1, 2, 4, 6, 10, 15, 25, 40, 60, 100]

CONFIG = [
    ('FriendDgn_Tickets', 2, '회/일', '친구던전', 'Friend.xlsx', '11-18 친구 던전 하루 무료 입장 수 (매일 0시 다시 채움)'),
    ('FriendDgn_Ad_Daily', 3, '회/일', '친구던전', 'Friend.xlsx', '11-18 무료 입장을 다 쓴 뒤 광고 보고 더 들어갈 수 있는 수 (하루)'),
    ('FriendDgn_Stage_Mul', 2, '배', '친구던전', 'Friend.xlsx', '11-18 던전 단계 ↔ 스테이지 1:2 — 던전 N 단계 = 스테이지 N x 이 값 번째(던전 1 = 1-2) 의 좀비 체력'),
    ('FriendDgn_Bosses', 3, '마리', '친구던전', 'Friend.xlsx', '11-18 한 번에 상대하는 보스 수 (한 마리씩 차례로 나옴, 모두 잡으면 이김)'),
    ('FriendDgn_Time', 60, '초', '친구던전', 'Friend.xlsx', '11-18 제한 시간 — 넘기면 실패 (티켓은 돌아오지 않음)'),
    ('FriendDgn_Hp_Mul', 1, '배', '친구던전', 'Friend.xlsx', '11-18 보스 한 마리 체력 = 그 스테이지 챕터 보스와 같은 배율 x 이 값 [임시]'),
    ('FriendDgn_Cards', 3, '장', '친구던전', 'Friend.xlsx', '11-18 이기면 받는 카피친구 카드 기본 수 [임시]'),
    ('FriendDgn_Cards_Step', 20, '단계', '친구던전', 'Friend.xlsx', '11-18 던전 단계가 이만큼 오를 때마다 카드 +1 (0 = 늘지 않음) [임시]'),
    ('AdBullets_Daily', 3, '회/일', '광고', 'Shop.xlsx', '11-18 상점 무료 탭 [무료 총알] 광고 하루 횟수'),
    ('AdBullets_Amount', 5, '개', '광고', 'Shop.xlsx', '11-18 [무료 총알] 광고 1번에 받는 D 등급 총알 (뽑기 5번과 같음) — 인벤토리가 차 있으면 못 받은 만큼 사라지고 횟수는 줄어듦(기획 그대로)'),
]

FILL = PatternFill('solid', fgColor='FFE9C46A')


def head(ws, cols):
    ws.append(cols)
    for c in ws[1]: c.font = Font(bold=True); c.fill = FILL


def friend_xlsx():
    wb = openpyxl.Workbook()
    ws = wb.active; ws.title = 'Friend_List'
    head(ws, ['Friend_Id', 'Name', 'Stat', 'Bonus_Per_Lv', 'Max_Bonus (%)', 'Unlock_Dgn_Lv', 'Unlock_Stage', 'Weight', 'Icon', 'Note'])
    for i, (nm, st, b, u) in enumerate(FRIENDS, 1):
        idx = u * 2 - 1
        ws.append([i, nm, st, b, b * len(LEVELS), u, f'{idx // 5 + 1}-{idx % 5 + 1}', 1, 'friend_%02d' % i,
                   'Stat: Atk 공격력 · Spd 공격속도 · Gold 금전 · Boss 보스 피해 / Unlock_Stage = 그 던전 단계와 맞먹는 스테이지(참고)'])
    for c, w in zip('ABCDEFGHIJ', (10, 12, 8, 13, 13, 14, 13, 8, 12, 60)): ws.column_dimensions[c].width = w
    wl = wb.create_sheet('Friend_Level')
    head(wl, ['Lv', 'Cards_Need', 'Cum_Cards', 'Note'])
    cum = 0
    for lv, n in enumerate(LEVELS, 1):
        cum += n
        wl.append([lv, n, cum, '처음 얻을 때 1장' if lv == 1 else f'Lv.{lv - 1} → Lv.{lv} 에 카드 {n}장'])
    wb.save(os.path.join(T, 'Friend.xlsx'))
    print('Friend.xlsx', len(FRIENDS), '마리 ·', len(LEVELS), '레벨 · 한 마리 끝까지 카드', cum)


def config():
    p = os.path.join(T, 'Config.xlsx')
    wb = openpyxl.load_workbook(p); ws = wb['Global_Config']
    have = {ws.cell(r, 1).value for r in range(2, ws.max_row + 1)}
    for row in CONFIG:
        if row[0] not in have: ws.append(list(row)); print('Config +', row[0])
    wb.save(p)


def unlock():
    p = os.path.join(T, 'Unlock.xlsx')
    wb = openpyxl.load_workbook(p); ws = wb.active
    hd = [c.value for c in ws[1]]; col = {h: i + 1 for i, h in enumerate(hd)}
    rows = {ws.cell(r, 1).value: r for r in range(2, ws.max_row + 1)}
    if 'skin' in rows:
        ws.cell(rows['skin'], col['UI_Location']).value = '하단 탭 2 [컬렉션] (코스튬 탭)'
        ws.cell(rows['skin'], col['Unlock_Banner_Message']).value = '컬렉션이 열렸습니다!'
    if 'supply' in rows:
        ws.cell(rows['supply'], col['UI_Location']).value = '컬렉션 안 [보급품] 탭'
        ws.cell(rows['supply'], col['Unlock_Banner_Message']).value = '-'      # 같은 때 열리는 친구 던전 알림이 나옴
    if 'friend' not in rows:
        ws.append(['friend', '친구 던전 (카피친구)', '하단 탭 3 [친구] (보급품 자리)', 'Stage', 25, '6-1', 25, 'Stage 6-1 에서 열립니다', '친구 던전이 열렸습니다!'])
        print('Unlock + friend')
    wb.save(p)


def shop():
    p = os.path.join(T, 'Shop.xlsx')
    wb = openpyxl.load_workbook(p); ws = wb['Shop_Products']
    ids = {ws.cell(r, 2).value for r in range(2, ws.max_row + 1)}
    if 'free_bullets_ad' not in ids:
        ws.append([6, 'free_bullets_ad', 'Ad_Bullets', '무료 총알', '광고 1편 · D 등급 총알 5개 · 하루 3번', 'Ad', '광고', 0,
                   'Bullet D x5 (Global_Config AdBullets_Daily·AdBullets_Amount)', '11-18 1차 업데이트 (인벤토리가 차 있으면 못 받은 만큼 사라지고 횟수는 줄어듦)', 3, '무료'])
        print('Shop + free_bullets_ad')
    wb.save(p)


if __name__ == '__main__':
    friend_xlsx(); config(); unlock(); shop()
