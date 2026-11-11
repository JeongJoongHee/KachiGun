# -*- coding: utf-8 -*-
"""11-11 새 좀비 32종 (요청_03 시안 12 + 좀비 추가 제안 20) 을 게임 표에 넣기 — 다시 돌려도 같은 결과(있는 줄은 덮어씀).

사용자 결정 (좀비_추가_시안.html · 좀비_추가_제안.html 의견):
  · 색 변형 = 셰이더 · 챕터마다 한 색(Chapter_Summary Zombie_Tint) + 배경도 그 색에 맞춘 판(Map_Sprite)
  · 1~150 고르게 섞기, 간단한 디자인은 초반 · 퀄 높은 디자인은 중후반 → MinChapter 6~56 에 2챕터마다 한 종,
    배경 좀비는 그 배경 챕터에 처음 나옴 + 그 배경에서 비중 x1.5(Config Zombie_Biome_Mul) + 처음 나온 뒤 5챕터 x3(Zombie_New_Mul)
  · 보스 = 챕터마다 돌아가며 ogre → jason(하키 좀비) → pharaoh → yeti → captain → gasgiant, 보스 던전도 레벨마다
  · 규칙 좀비 = 돈가방(표 숫자만) · 드럼통(죽으면 펑, Effect=Blast). 방패는 그림만(규칙은 나중) · 산타는 12월에
  · 숫자(체력·속도·비중)는 임시값 — 밸런스는 마지막에
사용: PYTHONUTF8=1 python tools/zombie_add_1111.py  →  python tools/xlsx2csv.py (바뀐 csv)
"""
import os
import openpyxl

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Kachi_Gun', 'Table')
BIOMES = ['폐허 숲길', '오염된 해안', '사막 도로', '얼어붙은 설원', '지하 연구소', '오염 구역']   # 챕터 1,2,3… 차례 (6챕터마다 돌아감)
def biome_of(ch): return BIOMES[(ch - 1) % 6]

BOSS_CYCLE = ['ogre', 'jason', 'pharaoh', 'yeti', 'captain', 'gasgiant']
TINT_CYCLE = ['green', 'red', 'blue', 'purple']      # 챕터 1 = 초록(원래 색) → 붉은 → 푸르딩딩 → 보라 → …
MAP_OF_TINT = {'green': 'side_map', 'red': 'side_map_waste', 'blue': 'side_map_pine', 'purple': 'side_map_dead'}

# id: (이름, 영어, 역할, 체력x, 골드x, 속도, 비중, 크기, 배경, 움직임, 효과)  — 순서 = 간단 → 퀄 높음 (처음 나오는 챕터 순)
NEW = [
    ('bucketw', '양동이 좀비 (걷는)', 'Bucket Walker', '중갑', 1.5, 1.4, 0.85, 7, 1, '', '', ''),
    ('sport', '운동복 좀비', 'Tracksuit Zombie', '일반', 0.9, 1.0, 1.25, 8, 1, '', '', ''),
    ('suit', '양복 좀비', 'Suit Zombie', '일반', 1.1, 1.2, 1.0, 8, 1, '', '', ''),
    ('student', '학생 좀비', 'Student Zombie', '일반', 0.6, 0.7, 1.3, 8, 0.78, '', '', ''),
    ('chef', '요리사 좀비', 'Chef Zombie', '일반', 1.2, 1.2, 1.0, 8, 1, '', '', ''),
    ('hatchet', '손도끼 좀비', 'Hatchet Zombie', '강습', 1.2, 1.2, 1.05, 8, 1, '폐허 숲길', '', ''),
    ('tube', '튜브 좀비', 'Pool Float Zombie', '일반', 1.1, 1.1, 0.9, 8, 1, '오염된 해안', '', ''),
    ('fisher', '어부 좀비', 'Fisher Zombie', '일반', 1.2, 1.2, 0.95, 8, 1, '오염된 해안', '', ''),
    ('flag', '깃발 좀비', 'Flag Zombie', '일반', 1.1, 1.1, 1.0, 8, 1, '', '', ''),
    ('miner', '광부 좀비', 'Miner Zombie', '일반', 1.3, 1.3, 0.95, 8, 1, '', '', ''),
    ('cowboy', '카우보이 좀비', 'Cowboy Zombie', '강습', 1.2, 1.2, 1.2, 8, 1, '사막 도로', '', ''),
    ('padding', '롱패딩 좀비', 'Puffer Zombie', '중갑', 1.8, 1.6, 0.8, 6, 1.05, '얼어붙은 설원', '', ''),
    ('scientist', '연구원 좀비', 'Scientist Zombie', '일반', 1.1, 1.2, 1.0, 8, 1, '지하 연구소', '', ''),
    ('camper', '캠핑 좀비', 'Camper Zombie', '탱커', 1.4, 1.3, 0.85, 7, 1, '폐허 숲길', '', ''),
    ('delivery', '배달 좀비', 'Delivery Zombie', '고속', 1.0, 1.2, 1.4, 7, 1, '', '', ''),
    ('biker', '폭주족 좀비', 'Biker Zombie', '강습', 1.4, 1.3, 1.15, 7, 1, '', '', ''),
    ('helmet', '헬멧 좀비', 'Knight Helmet Zombie', '중갑', 1.8, 1.7, 0.85, 6, 1, '', '', ''),
    ('chainsaw', '자동톱 좀비', 'Chainsaw Zombie', '강습', 1.6, 1.6, 1.15, 5, 1, '', '', ''),
    ('ghost', '유령 좀비', 'Ghost Zombie', '특수', 1.0, 1.1, 1.1, 7, 1, '', '', ''),
    ('patient', '실험체 좀비', 'Test Subject Zombie', '특수', 1.3, 1.2, 0.8, 7, 1, '지하 연구소', '', ''),
    ('hazmat', '방호복 좀비', 'Hazmat Zombie', '중갑', 1.9, 1.7, 0.85, 6, 1, '오염 구역', '', ''),
    ('ski', '스키 좀비', 'Skier Zombie', '고속', 1.0, 1.2, 1.45, 7, 1, '얼어붙은 설원', 'Slide', ''),
    ('jiangshi', '강시 좀비', 'Hopping Zombie', '특수', 1.3, 1.3, 1.0, 7, 1, '', 'Hop', ''),
    ('shield', '방패 경찰 좀비', 'Riot Shield Zombie', '엘리트', 2.2, 2.0, 0.8, 5, 1.05, '', '', ''),
    ('firefighter', '소방관 좀비', 'Firefighter Zombie', '중갑', 1.7, 1.6, 0.9, 6, 1, '', '', ''),
    ('drum', '드럼통 좀비', 'Barrel Zombie', '특수', 1.3, 1.2, 0.85, 5, 1, '오염 구역', '', 'Blast'),
]
RARE = [('money', '돈가방 좀비', 'Treasure Zombie', '보물', 1.2, 8.0, 1.5, 1, 1, '', '', '', 3)]   # 마지막 = 처음 나오는 챕터
BOSSES = [('jason', '하키 좀비', 'Hockey Zombie'), ('pharaoh', '파라오 좀비', 'Pharaoh Zombie'), ('yeti', '설인 좀비', 'Yeti Zombie'),
          ('captain', '닻 선장 좀비', 'Anchor Captain Zombie'), ('gasgiant', '방독면 거인', 'Gas Giant')]
OLD_BIOME = {'farmer': '폐허 숲길', 'mummy': '사막 도로'}


def schedule():
    """2챕터마다 한 종 (6, 8, … 56). 배경 좀비는 그 배경 챕터 중 제자리에서 가장 가까운 빈 챕터"""
    used, out = set(), {}
    for i, z in enumerate(NEW):
        want, bio = 6 + 2 * i, z[9]
        if bio:
            c = min((c for c in range(max(6, want - 3), 80) if biome_of(c) == bio and c not in used), key=lambda c: abs(c - want))
        else:
            c = want
            while c in used: c += 1
        used.add(c); out[z[0]] = c
    return out


def header(ws):
    return [c.value for c in ws[1]]


def col(ws, name):
    h = header(ws)
    if name not in h:
        ws.cell(row=1, column=len(h) + 1, value=name)
        h.append(name)
    return h.index(name) + 1


def zombies():
    p = os.path.join(ROOT, 'Zombie.xlsx')
    wb = openpyxl.load_workbook(p); ws = wb['Zombie_Master']
    cBio, cMove, cFx = col(ws, 'Biome'), col(ws, 'Move'), col(ws, 'Effect')
    h = header(ws)
    rows = {ws.cell(row=r, column=1).value: r for r in range(2, ws.max_row + 1)}
    for zid, b in OLD_BIOME.items():
        ws.cell(row=rows[zid], column=cBio, value=b)
    sch = schedule()

    def put(zid, name, role, minch, w, hp, gold, sp, sc, bio, move, fx, boss=False):
        r = rows.get(zid) or ws.max_row + 1
        rows[zid] = r
        sz = sc
        v = {'Id': zid, 'Name': name, 'Role': role, 'MinChapter': minch, 'Base_Weight': w, 'HpMul': hp, 'GoldMul': gold,
             'Speed': sp, 'Actual_Speed_Min': round(sp * 0.935, 3), 'Actual_Speed_Max': round(sp * 1.265, 3), 'Scale': sc,
             'StopDist_Min': 3.38 if boss else 2, 'StopDist_Max': 4.16 if boss else 4.6,
             'Height': round(1.35 * sz, 3), 'Radius': round(0.5 * sz, 3),
             'Shadow_W': 1.15 if zid == 'ski' else round(0.8 * sz, 3), 'Shadow_H': round(0.22 * sz, 3),
             'Sprite_Path': 'Zombie/z_' + zid, 'Rig_File': 'Art/Rig/z_' + zid, 'Biome': bio, 'Move': move, 'Effect': fx}
        for k, val in v.items():
            ws.cell(row=r, column=h.index(k) + 1, value=val if val != '' else None)

    for z in NEW:
        zid, name, en, role, hp, gold, sp, w, sc, bio, move, fx = z
        put(zid, name, role, sch[zid], w, hp, gold, sp, sc, bio, move, fx)
    for zid, name, en, role, hp, gold, sp, w, sc, bio, move, fx, ch in RARE:
        put(zid, name, role, ch, w, hp, gold, sp, sc, bio, move, fx)
    for zid, name, en in BOSSES:
        put(zid, name, '보스전용', 99, 0, 1, 1, 0.55, 1, '', '', '', boss=True)
    wb.save(p)
    return sch


def stages():
    p = os.path.join(ROOT, 'Stage.xlsx')
    wb = openpyxl.load_workbook(p)
    ws = wb['Stage_Table']; h = header(ws)
    cB, cId, cCh = h.index('Is_Boss_Stage') + 1, h.index('Boss_Id') + 1, h.index('Chapter') + 1
    for r in range(2, ws.max_row + 1):
        if ws.cell(row=r, column=cB).value == 'Y':
            ws.cell(row=r, column=cId, value=BOSS_CYCLE[(ws.cell(row=r, column=cCh).value - 1) % 6])
    ws = wb['Chapter_Summary_150']
    cT, cM = col(ws, 'Zombie_Tint'), col(ws, 'Map_Sprite')
    for r in range(2, ws.max_row + 1):
        ch = ws.cell(row=r, column=1).value
        t = TINT_CYCLE[(ch - 1) % 4]
        ws.cell(row=r, column=cT, value=t); ws.cell(row=r, column=cM, value=MAP_OF_TINT[t])
    wb.save(p)


def boss_dungeon():
    p = os.path.join(ROOT, 'Boss_Dungeon.xlsx')
    wb = openpyxl.load_workbook(p); ws = wb.active; h = header(ws)
    cId, cLv = h.index('Boss_Id') + 1, h.index('Save_bossLv') + 1     # 0 부터
    for r in range(2, ws.max_row + 1):
        ws.cell(row=r, column=cId, value=BOSS_CYCLE[ws.cell(row=r, column=cLv).value % 6])
    wb.save(p)


CONFIG = [
    ('Zombie_Biome_Mul', 1.5, '배', '좀비', 'Zombie.xlsx', '11-11 좀비 Biome 칸이 지금 챕터 배경(Chapter_Summary Biome_Name)과 같으면 나올 비중 x 이 값 (1 = 배경 상관없음)'),
    ('Zombie_New_Mul', 3, '배', '좀비', 'Zombie.xlsx', '11-11 처음 나오는 챕터(MinChapter)부터 Zombie_New_Chapters 챕터 동안 나올 비중 x 이 값 — 늦게 나오는 좀비도 고르게 보이게 (1 = 끔)'),
    ('Zombie_New_From', 6, '챕터', '좀비', 'Zombie.xlsx', '11-11 새 얼굴 비중은 MinChapter 가 이 값 이상인 좀비만 (처음 5챕터 원래 좀비 섞임은 그대로)'),
    ('Zombie_New_Chapters', 5, '챕터', '좀비', 'Zombie.xlsx', '11-11 위 "새 얼굴" 비중이 붙는 챕터 수'),
    ('Tint_Chance', 0.8, '비율', '좀비', 'Chapter_Summary', '11-11 챕터 좀비 색(Zombie_Tint)이 붙을 확률 — 나머지는 원래 초록 (1 = 모두 그 색). 보스는 늘 그 색'),
    ('Drum_Blast_Mul', 3, '배', '좀비', 'Zombie.xlsx', '11-11 드럼통 좀비(Effect=Blast)가 죽으면 터지는 피해 = 그 드럼통 최대 체력 x 이 값 (주변 좀비만)'),
    ('Drum_Blast_Radius', 1.8, '칸', '좀비', 'Zombie.xlsx', '11-11 드럼통 폭발 반경'),
]


def config():
    p = os.path.join(ROOT, 'Config.xlsx')
    wb = openpyxl.load_workbook(p); ws = wb['Global_Config']
    rows = {ws.cell(row=r, column=1).value: r for r in range(2, ws.max_row + 1)}
    for row in CONFIG:
        r = rows.get(row[0]) or ws.max_row + 1
        for i, v in enumerate(row):
            ws.cell(row=r, column=i + 1, value=v)
    wb.save(p)


def strings():
    p = os.path.join(ROOT, 'String.xlsx')
    wb = openpyxl.load_workbook(p); ws = wb.active
    rows = {ws.cell(row=r, column=1).value: r for r in range(2, ws.max_row + 1)}
    names = [(z[1], z[2]) for z in NEW] + [(z[1], z[2]) for z in RARE] + [(b[1], b[2]) for b in BOSSES]
    # 마지막 좀비 이름 줄 다음에 끼우지 않고 맨 아래에 붙임 (줄 순서는 상관없음)
    for ko, en in names:
        r = rows.get(ko) or ws.max_row + 1
        rows[ko] = r
        ws.cell(row=r, column=1, value=ko); ws.cell(row=r, column=2, value=en); ws.cell(row=r, column=3, value='Zombie_Master.Name')
    wb.save(p)


if __name__ == '__main__':
    sch = zombies()
    stages(); boss_dungeon(); config(); strings()
    for zid, ch in sorted(sch.items(), key=lambda kv: kv[1]):
        print(ch, biome_of(ch), zid)
