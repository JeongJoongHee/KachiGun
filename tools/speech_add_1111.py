# -*- coding: utf-8 -*-
"""11-11 요청_04: 카피바라 말풍선 대사 표 + Config + 영어 글자 (다시 돌려도 같음)
  Kachi_Gun/Table/Speech.xlsx (시트 Speech_Lines → Resources/Tables/Speech_Lines.csv)
    Trigger: Clear = 스테이지 클리어 · Boss = 보스 등장 · Random = 싸우는 중 가끔(Speech_Random_Min~Max 초마다)
    Who: Leader = 가운데 리더 · Any = 아무나
    Weight: 같은 Trigger 안에서 나올 비중
사용: PYTHONUTF8=1 python tools/speech_add_1111.py → python tools/xlsx2csv.py
"""
import os
import openpyxl

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Kachi_Gun', 'Table')

LINES = [
    # id, trigger, who, weight, 한국어, 영어
    ('clear_1', 'Clear', 'Leader', 1, '스테이지 클리어!', 'Stage clear!'),
    ('boss_1', 'Boss', 'Leader', 3, '보스다!! 조심해!!', "It's the boss!! Watch out!!"),
    ('boss_2', 'Boss', 'Leader', 1, '큰 놈이 온다!', 'Here comes a big one!'),
    ('rand_1', 'Random', 'Any', 2, '좀비가 몰려온다!', 'Zombies incoming!'),
    ('rand_2', 'Random', 'Any', 2, '다 쓸어버리자!', "Let's wipe them out!"),
    ('rand_3', 'Random', 'Any', 2, '모두 힘내!', 'Hang in there, everyone!'),
    ('rand_4', 'Random', 'Any', 1, '한 마리도 놓치지 마!', "Don't let a single one through!"),
    ('rand_5', 'Random', 'Any', 1, '끝까지 버티자!', 'Hold the line!'),
    ('rand_6', 'Random', 'Any', 1, '조준 똑바로!', 'Aim steady!'),
    ('rand_7', 'Random', 'Any', 1, '숲은 우리가 지킨다!', "We'll protect the forest!"),
    ('rand_8', 'Random', 'Any', 1, '아직 멀었어!', "We're just getting started!"),
]

CONFIG = [
    ('Speech_Random_Min', 15, '초', '말풍선', 'Speech.xlsx', '11-11 싸우는 중 아무 카피바라가 Random 대사를 말하는 간격 최소 (스테이지 화면에서만, 보스전 중엔 안 함)'),
    ('Speech_Random_Max', 30, '초', '말풍선', 'Speech.xlsx', '11-11 위 간격 최대 (0 이면 Random 대사 끔)'),
    ('Speech_Show', 2.2, '초', '말풍선', 'Speech.xlsx', '11-11 말풍선이 떠 있는 시간 (끝 0.25초 동안 흐려짐)'),
    ('Unlock_Toast_Time', 3.5, '초', 'UI', 'Unlock.xlsx', '11-11 콘텐츠가 열렸을 때 알림 글이 떠 있는 시간 (보통 알림은 1.8초)'),
    ('Unlock_Glow_Time', 6, '초', 'UI', 'Unlock.xlsx', '11-11 열린 버튼 뒤에서 빛이 깜빡이며 통통 튀는 시간'),
]


def speech():
    p = os.path.join(ROOT, 'Speech.xlsx')
    wb = openpyxl.Workbook(); ws = wb.active; ws.title = 'Speech_Lines'
    ws.append(['Id', 'Trigger', 'Who', 'Weight', 'Text', 'Note'])
    for i, tr, who, w, ko, en in LINES:
        ws.append([i, tr, who, w, ko, ''])
    ws['F2'] = 'Trigger: Clear 스테이지 클리어 · Boss 보스 등장 · Random 싸우는 중 가끔 / Who: Leader 리더 · Any 아무나 / 영어는 String 표'
    for col, wd in zip('ABCDEF', (10, 10, 9, 8, 26, 60)):
        ws.column_dimensions[col].width = wd
    wb.save(p)


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
    for _, _, _, _, ko, en in LINES:
        r = rows.get(ko) or ws.max_row + 1
        rows[ko] = r
        ws.cell(row=r, column=1, value=ko); ws.cell(row=r, column=2, value=en); ws.cell(row=r, column=3, value='Speech_Lines.Text')
    wb.save(p)


if __name__ == '__main__':
    speech(); config(); strings()
    print('ok', len(LINES))
