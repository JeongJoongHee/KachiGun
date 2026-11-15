# -*- coding: utf-8 -*-
"""11-15 사용자 요청 판타지 몬스터 좀비 15종(일반 12 · 보스 3)을 게임 표에 넣기 — 다시 돌려도 같은 결과(있는 줄은 덮어씀).
그림 = tools/doodle_zombies3.py (doodle_art.py znew3) · 뼈대 = tools/rig_gen.py.

배치 (11-11 사용자 규칙 "1~150 고르게 · 간단한 디자인은 초반 · 퀄 높은 디자인은 중후반"을 이어서):
  · 11-11 새 좀비는 챕터 6~56 에 2챕터마다 들어가 있음 → 이번 12종은 그 뒤 비어 있는 58~150 에 8챕터마다 (60 · 68 · … · 148)
    배경 좀비(설인 = 얼어붙은 설원 · 크라켄 = 오염된 해안 · 골렘 = 사막 도로)는 그 배경 챕터 중 제자리에서 가장 가까운 곳
  · 보스 = 챕터마다 돌아가며 9종: ogre → jason → pharaoh → yeti → captain → gasgiant → minotaur → wyvern → dragon
    (전엔 6종 — 챕터 7부터 보스 순서가 바뀜) · 보스 던전도 레벨마다 같은 순서
  · '설인 좀비'는 이미 보스(yeti)에 있어서 → 새 일반 좀비 id yetiz = '설인 좀비', 원래 보스 yeti 이름 = '거대 설인'
  · 숫자(체력·속도·비중)는 임시값 — 밸런스는 마지막에
  · 11-15 사용자 확인: 비홀더 → '눈알 좀비'(id eyeball) · 용 = 날지 않는 티라노 모양(그림·뼈대만, 표는 그대로)
사용: PYTHONUTF8=1 python tools/zombie_add_1115.py  →  python tools/xlsx2csv.py (바뀐 csv 만 남기기)
"""
import os
import openpyxl
from zombie_add_1111 import ROOT, biome_of, header, col

BOSS_CYCLE = ['ogre', 'jason', 'pharaoh', 'yeti', 'captain', 'gasgiant', 'minotaur', 'wyvern', 'dragon']

# id, 이름, 영어, 역할, 체력x, 골드x, 속도, 비중, 크기, 배경, 움직임, 효과, 그림자 폭 배   — 순서 = 간단 → 퀄 높음 (처음 나오는 챕터 순)
NEW = [
    ('skeleton', '해골 좀비', 'Skeleton Zombie', '일반', 0.8, 0.9, 1.1, 8, 1.0, '', '', '', 1.0),
    ('dog', '개 좀비', 'Zombie Dog', '고속', 0.7, 0.9, 1.5, 7, 0.9, '', '', '', 1.7),
    ('vampire', '뱀파이어 좀비', 'Vampire Zombie', '강습', 1.3, 1.3, 1.1, 7, 1.0, '', '', '', 1.0),
    ('werewolf', '늑대인간 좀비', 'Werewolf Zombie', '강습', 1.5, 1.4, 1.25, 6, 1.0, '', '', '', 1.0),
    ('spider', '거대 거미 좀비', 'Giant Spider Zombie', '고속', 1.1, 1.2, 1.35, 6, 1.05, '', '', '', 1.9),
    ('mimic', '미믹 좀비', 'Mimic Zombie', '특수', 1.4, 2.0, 0.9, 4, 0.9, '', 'Hop', '', 1.2),
    ('golem', '골렘 좀비', 'Golem Zombie', '탱커', 2.4, 2.0, 0.7, 5, 1.15, '사막 도로', '', '', 1.2),
    ('centaur', '켄타우로스 좀비', 'Centaur Zombie', '강습', 1.6, 1.5, 1.3, 5, 1.1, '', '', '', 1.8),
    ('yetiz', '설인 좀비', 'Yeti Zombie', '중갑', 1.8, 1.6, 0.85, 6, 1.0, '얼어붙은 설원', '', '', 1.1),
    ('cyclops', '사이클롭스 좀비', 'Cyclops Zombie', '엘리트', 2.2, 2.0, 0.85, 5, 1.2, '', '', '', 1.2),
    ('kraken', '크라켄 좀비', 'Kraken Zombie', '탱커', 1.9, 1.7, 0.8, 5, 1.05, '오염된 해안', '', '', 1.5),
    ('eyeball', '눈알 좀비', 'Eyeball Zombie', '특수', 1.4, 1.4, 0.9, 5, 0.95, '', '', '', 1.2),   # 11-15 사용자: '비홀더 좀비' → '눈알 좀비' (id beholder → eyeball)
]
BOSSES = [('minotaur', '미노타우르스 좀비', 'Minotaur Zombie', 1.0), ('wyvern', '와이번 좀비', 'Wyvern Zombie', 1.6),
          ('dragon', '용 좀비', 'Dragon Zombie', 1.7)]
RENAME = {'yeti': ('거대 설인', 'Giant Yeti')}      # 원래 보스 '설인 좀비' — 새 일반 설인과 이름이 겹쳐서
OLD_IDS = {'beholder': 'eyeball'}                 # 11-15 바뀐 id (표 줄을 새로 만들지 않고 그 줄의 Id 를 바꿈)
OLD_NAMES = ['비홀더 좀비']                      # String 표에서 지울 옛 이름


def schedule():
    """58~150 에 8챕터마다 (60, 68, … 148). 배경 좀비는 그 배경 챕터 중 제자리에서 가장 가까운 빈 챕터"""
    used, out = set(), {}
    for i, z in enumerate(NEW):
        want, bio = 60 + 8 * i, z[9]
        if bio:
            c = min((c for c in range(58, 151) if biome_of(c) == bio and c not in used), key=lambda c: abs(c - want))
        else:
            c = want
            while c in used: c += 1
        used.add(c); out[z[0]] = c
    return out


def zombies():
    p = os.path.join(ROOT, 'Zombie.xlsx')
    wb = openpyxl.load_workbook(p); ws = wb['Zombie_Master']
    h = header(ws)
    for r in range(2, ws.max_row + 1):
        old = ws.cell(row=r, column=1).value
        if old in OLD_IDS:
            ws.cell(row=r, column=1, value=OLD_IDS[old])
    rows = {ws.cell(row=r, column=1).value: r for r in range(2, ws.max_row + 1)}
    sch = schedule()

    def put(zid, name, role, minch, w, hp, gold, sp, sc, bio, move, fx, shw, boss=False):
        r = rows.get(zid) or ws.max_row + 1
        rows[zid] = r
        v = {'Id': zid, 'Name': name, 'Role': role, 'MinChapter': minch, 'Base_Weight': w, 'HpMul': hp, 'GoldMul': gold,
             'Speed': sp, 'Actual_Speed_Min': round(sp * 0.935, 3), 'Actual_Speed_Max': round(sp * 1.265, 3), 'Scale': sc,
             'StopDist_Min': 3.38 if boss else 2, 'StopDist_Max': 4.16 if boss else 4.6,
             'Height': round(1.35 * sc, 3), 'Radius': round(0.5 * sc * (1.3 if shw >= 1.5 else 1), 3),
             'Shadow_W': round(0.8 * sc * shw, 3), 'Shadow_H': round(0.22 * sc, 3),
             'Sprite_Path': 'Zombie/z_' + zid, 'Rig_File': 'Art/Rig/z_' + zid, 'Biome': bio, 'Move': move, 'Effect': fx}
        for k, val in v.items():
            ws.cell(row=r, column=h.index(k) + 1, value=val if val != '' else None)

    for zid, name, en, role, hp, gold, sp, w, sc, bio, move, fx, shw in NEW:
        put(zid, name, role, sch[zid], w, hp, gold, sp, sc, bio, move, fx, shw)
    for zid, name, en, shw in BOSSES:
        put(zid, name, '보스전용', 99, 0, 1, 1, 0.55, 1, '', '', '', shw, boss=True)
    cName = h.index('Name') + 1
    for zid, (ko, en) in RENAME.items():
        ws.cell(row=rows[zid], column=cName, value=ko)
    wb.save(p)
    return sch


def stages():
    p = os.path.join(ROOT, 'Stage.xlsx')
    wb = openpyxl.load_workbook(p)
    ws = wb['Stage_Table']; h = header(ws)
    cB, cId, cCh = h.index('Is_Boss_Stage') + 1, h.index('Boss_Id') + 1, h.index('Chapter') + 1
    for r in range(2, ws.max_row + 1):
        if ws.cell(row=r, column=cB).value == 'Y':
            ws.cell(row=r, column=cId, value=BOSS_CYCLE[(ws.cell(row=r, column=cCh).value - 1) % len(BOSS_CYCLE)])
    wb.save(p)


def boss_dungeon():
    p = os.path.join(ROOT, 'Boss_Dungeon.xlsx')
    wb = openpyxl.load_workbook(p); ws = wb.active; h = header(ws)
    cId, cLv = h.index('Boss_Id') + 1, h.index('Save_bossLv') + 1     # 0 부터
    for r in range(2, ws.max_row + 1):
        ws.cell(row=r, column=cId, value=BOSS_CYCLE[ws.cell(row=r, column=cLv).value % len(BOSS_CYCLE)])
    wb.save(p)


def strings():
    p = os.path.join(ROOT, 'String.xlsx')
    wb = openpyxl.load_workbook(p); ws = wb.active
    rows = {ws.cell(row=r, column=1).value: r for r in range(2, ws.max_row + 1)}
    for r in range(ws.max_row, 1, -1):
        if ws.cell(row=r, column=1).value in OLD_NAMES:
            ws.delete_rows(r)
    rows = {ws.cell(row=r, column=1).value: r for r in range(2, ws.max_row + 1)}
    names = [(z[1], z[2]) for z in NEW] + [(b[1], b[2]) for b in BOSSES] + list(RENAME.values())
    for ko, en in names:
        r = rows.get(ko) or ws.max_row + 1
        rows[ko] = r
        ws.cell(row=r, column=1, value=ko); ws.cell(row=r, column=2, value=en); ws.cell(row=r, column=3, value='Zombie_Master.Name')
    wb.save(p)


if __name__ == '__main__':
    sch = zombies()
    stages(); boss_dungeon(); strings()
    for zid, ch in sorted(sch.items(), key=lambda kv: kv[1]):
        print(ch, biome_of(ch), zid)
