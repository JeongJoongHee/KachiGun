# -*- coding: utf-8 -*-
"""11-15 연출 바꾸기 (사용자 "카피바라가 전진하고 좀비떼가 뭉쳐 있게 · 보스전엔 보스만 + 말풍선 · 보스를 잡으면 다음 맵으로 이동") Config 줄
  Kachi_Gun/Table/Config.xlsx (시트 Global_Config) 에 아래 줄만 넣거나 고침 — 다른 줄은 안 건드림
사용: PYTHONUTF8=1 python tools/flow_config_1115.py → python tools/xlsx2csv.py (고친 Global_Config.csv 만 남기기)
  대사는 tools/speech_add_1111.py (BossIntro · BossTaunt · BossHurt · BossDie · BossWin)
"""
import os
import openpyxl

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Kachi_Gun', 'Table')

CONFIG = [
    ('Advance_On', 1, '0/1', '연출', '-', '11-15 1 = 카피바라가 걸어 나가고(땅이 흐름) 좀비는 뭉친 무리로 앞에서 기다림 · 0 = 예전(분대 고정, 좀비가 걸어옴)'),
    ('Advance_Speed', 1.6, 'unit/s', '연출', '-', '11-15 분대가 걸어 나가는 빠르기 (땅이 흐르는 빠르기)'),
    ('Advance_Engage', 0.5, '비율', '연출', '-', '11-15 어느 카피바라든 (사정거리 x 이 값) 안에 좀비가 있으면 멈춰서 쏨, 없으면 걸어 나감 (걷는 동안도 사정거리 안이면 쏨) · 0.5 = 화난 얼굴 거리(Capy_Angry_Range)와 같아 멈출 때 화난 얼굴'),
    ('Horde_Clump_Radius', 0.5, 'unit', '연출', '-', '11-15 뭉친 무리 반지름 = 이 값 x √마릿수 (6마리 1.2칸 · 10마리 1.6칸, 세로는 0.8배)'),
    ('Horde_Pack_Gap', 3.5, 'unit', '연출', '-', '11-15 무리와 다음 무리 사이 거리 (앞 무리 맨 뒤 → 다음 무리 앞)'),
    ('Horde_Approach_Speed', 0.4, 'unit/s', '연출', '-', '11-15 무리가 다 같이 다가오는 빠르기 (좀비 표 빠르기보다 느리면 이 값 · 0 = 제자리에서 꿈틀)'),
    ('Boss_Stop_Dist', 4.0, 'unit', '연출', '-', '11-15 전진할 때 보스가 맨 앞 카피바라에서 이만큼 앞에서 멈춤 (바로 앞까지 오면 말풍선이 겹침)'),
    ('Boss_Only', 1, '0/1', '연출', '-', '11-15 1 = 보스전엔 졸개가 물러나고 보스만 · 0 = 예전(보스전에도 졸개가 한 마리씩)'),
    ('Boss_Speech_Min', 6, '초', '말풍선', 'Speech.xlsx', '11-15 싸우는 동안 보스가 BossTaunt 대사를 말하는 간격 최소 (다른 말풍선이 떠 있으면 쉼)'),
    ('Boss_Speech_Max', 9, '초', '말풍선', 'Speech.xlsx', '11-15 위 간격 최대'),
    ('Travel_On', 1, '0/1', '연출', '-', '11-15 1 = 보스를 잡으면 분대가 달려 나가며 땅이 어두워졌다 다음 맵으로 밝아짐 · 0 = 예전(그 자리에서 맵만 바뀜)'),
    ('Travel_Time', 3.2, '초', '연출', '-', '11-15 다음 맵 이동 연출 전체 시간 (처음 35%(최대 1.3초)는 서서 보스가 쓰러지며 한마디 → 달리기 · 가운데쯤 어두워졌다 새 맵)'),
    ('Travel_Speed', 7, 'unit/s', '연출', '-', '11-15 다음 맵으로 달려갈 때 최고 빠르기'),
]


def config():
    p = os.path.join(ROOT, 'Config.xlsx')
    wb = openpyxl.load_workbook(p); ws = wb['Global_Config']
    rows = {ws.cell(row=r, column=1).value: r for r in range(2, ws.max_row + 1)}
    for row in CONFIG:
        r = rows.get(row[0]) or ws.max_row + 1
        rows[row[0]] = r
        for i, v in enumerate(row):
            ws.cell(row=r, column=i + 1, value=v)
    wb.save(p)


if __name__ == '__main__':
    config()
    print('ok', len(CONFIG))
