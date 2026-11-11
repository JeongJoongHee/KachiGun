# -*- coding: utf-8 -*-
"""11-11 요청_04: 카피바라 말풍선 대사 표 + Config + 영어 글자 (다시 돌려도 같음)
  Kachi_Gun/Table/Speech.xlsx (시트 Speech_Lines → Resources/Tables/Speech_Lines.csv)
    Trigger: Clear = 스테이지 클리어 · Boss = 보스 등장 · Random = 싸우는 중 가끔(Speech_Random_Min~Max 초마다)
    Who: Leader = 가운데 리더 · Any = 아무나
    Weight: 같은 Trigger 안에서 나올 비중
사용: PYTHONUTF8=1 python tools/speech_add_1111.py → python tools/xlsx2csv.py
  ※ speech() 는 Speech.xlsx 를 새로 씀 — 사용자가 표에 직접 넣은 줄은 먼저 LINES 에 옮길 것
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
    # 11-11 사용자가 Speech.xlsx 에 직접 넣은 3줄
    ('rand_9', 'Random', 'Any', 1, '조금 더 힘을 내!', 'Just a little more!'),
    ('rand_10', 'Random', 'Any', 1, '껄껄껄!', 'Hahaha!'),
    ('rand_11', 'Random', 'Any', 1, '카피바라는 귀엽다!!', 'Capybaras are cute!!'),
    # 11-12 사용자가 Speech.xlsx 에 직접 넣은 3줄
    ('rand_12', 'Random', 'Any', 1, '고되다. 고돼!', 'So tiring. So tiring!'),
    ('rand_13', 'Random', 'Any', 1, '우닫다다다닫', 'Ratatatatat!'),
    ('rand_14', 'Random', 'Any', 1, '배고프당!', "I'm hungry~!"),
]

# 11-12 받아치기: Trigger=Reply 줄은 Reply_To 의 대사가 나온 뒤 Speech_Reply_Delay 초 뒤 다른 동료가 말함
#   Who: Other = 방금 말한 동료 말고 아무나 · Back = 처음 말을 꺼낸 동료가 다시 받아침
#   답 줄에도 답을 달 수 있음(3마디) · 답이 여럿이면 Weight 대로 하나 · 동료가 1마리면 답 없음
#   답이 없는 대사는 혼잣말로 끝 (rand_2·4·5·7·9)
REPLIES = [
    # id, reply_to, who, weight, 한국어, 영어
    ('re_14a', 'rand_14', 'Other', 2, '밥이라도 먹고 할까?', 'Wanna grab a bite first?'),
    ('re_14a1', 're_14a', 'Back', 1, '좋아! 수박 먹자!', 'Yes! Watermelon time!'),
    ('re_14b', 'rand_14', 'Other', 1, '이것만 끝내고 먹자!', "Let's eat after this!"),
    ('re_12a', 'rand_12', 'Other', 1, '조금만 더 버텨!', 'Hang in a bit longer!'),
    ('re_12b', 'rand_12', 'Other', 1, '나도 힘들어...', 'Me too...'),
    ('re_10a', 'rand_10', 'Other', 1, '뭐가 그렇게 웃겨?', "What's so funny?"),
    ('re_10a1', 're_10a', 'Back', 1, '그냥~', 'Nothing~'),
    ('re_11a', 'rand_11', 'Other', 1, '당연하지!', 'Obviously!'),
    ('re_11b', 'rand_11', 'Other', 1, '지금 그럴 때야?!', 'Is this really the time?!'),
    ('re_13a', 'rand_13', 'Other', 1, '입으로 쏘는 거야?', 'Are you shooting with your mouth?'),
    ('re_13a1', 're_13a', 'Back', 1, '기분이야!', "It's for the vibe!"),
    ('re_3a', 'rand_3', 'Other', 1, '오케이!', 'Okay!'),
    ('re_3b', 'rand_3', 'Other', 1, '너나 잘해~', 'Worry about yourself~'),
    ('re_8a', 'rand_8', 'Other', 1, '언제 끝나...', 'When does it end...'),
    ('re_1a', 'rand_1', 'Other', 1, '다 덤벼라!', 'Bring it on!'),
    ('re_6a', 'rand_6', 'Other', 1, '하고 있거든!', 'I am!'),
    ('re_b1a', 'boss_1', 'Other', 1, '맡겨 둬!', 'Leave it to me!'),
    ('re_b2a', 'boss_2', 'Other', 1, '세게 한 방 먹이자!', "Let's hit it hard!"),
    ('re_c1a', 'clear_1', 'Other', 1, '수고했어!', 'Good work!'),
    ('re_c1b', 'clear_1', 'Other', 1, '다음 가자!', 'On to the next!'),
]

CONFIG = [
    ('Speech_Random_Min', 15, '초', '말풍선', 'Speech.xlsx', '11-11 싸우는 중 아무 카피바라가 Random 대사를 말하는 간격 최소 (스테이지 화면에서만, 보스전 중엔 안 함)'),
    ('Speech_Random_Max', 30, '초', '말풍선', 'Speech.xlsx', '11-11 위 간격 최대 (0 이면 Random 대사 끔)'),
    ('Speech_Show', 2.2, '초', '말풍선', 'Speech.xlsx', '11-11 말풍선이 떠 있는 시간 (끝 0.25초 동안 흐려짐)'),
    ('Speech_Reply_Delay', 1.0, '초', '말풍선', 'Speech.xlsx', '11-12 대사가 나온 뒤 다른 동료가 받아치기까지 (Speech_Show 보다 짧아야 두 풍선이 같이 보임)'),
    ('Speech_Reply_Chance', 0.8, '비율', '말풍선', 'Speech.xlsx', '11-12 답(Reply 줄)이 달린 대사가 나왔을 때 실제로 받아칠 확률 (1 = 항상 · 0 = 받아치기 끔)'),
    ('Unlock_Toast_Time', 3.5, '초', 'UI', 'Unlock.xlsx', '11-11 콘텐츠가 열렸을 때 알림 글이 떠 있는 시간 (보통 알림은 1.8초)'),
    ('Unlock_Glow_Time', 6, '초', 'UI', 'Unlock.xlsx', '11-11 열린 버튼 뒤에서 빛이 깜빡이며 통통 튀는 시간'),
]


def speech():
    p = os.path.join(ROOT, 'Speech.xlsx')
    wb = openpyxl.Workbook(); ws = wb.active; ws.title = 'Speech_Lines'
    ws.append(['Id', 'Trigger', 'Reply_To', 'Who', 'Weight', 'Text', 'Note'])
    for i, tr, who, w, ko, en in LINES:
        ws.append([i, tr, '', who, w, ko, ''])
    for i, to, who, w, ko, en in REPLIES:
        ws.append([i, 'Reply', to, who, w, ko, ''])
    ws['G2'] = 'Trigger: Clear 스테이지 클리어 · Boss 보스 등장 · Random 싸우는 중 가끔 · Reply 받아치기 / Who: Leader 리더 · Any 아무나 / 영어는 String 표'
    ws['G3'] = 'Reply 줄: Reply_To = 받아칠 대사 Id (답 줄에 또 답 가능) / Who: Other 다른 동료 · Back 처음 말한 동료 / 같은 대사에 답이 여럿이면 Weight 대로 하나'
    for col, wd in zip('ABCDEFG', (10, 10, 10, 8, 8, 26, 60)):
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
    for _, _, _, _, ko, en in LINES + REPLIES:
        r = rows.get(ko) or ws.max_row + 1
        rows[ko] = r
        ws.cell(row=r, column=1, value=ko); ws.cell(row=r, column=2, value=en); ws.cell(row=r, column=3, value='Speech_Lines.Text')
    wb.save(p)


if __name__ == '__main__':
    speech(); config(); strings()
    print('ok', len(LINES))
