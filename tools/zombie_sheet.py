# -*- coding: utf-8 -*-
"""지금 게임에 나오는 좀비 그림을 한 장 문서로 — 좀비_정리.html (그림은 문서 안에 들어감, 파일 하나)

읽는 것: Kachi_Gun/Assets/Resources/Tables/Zombie_Master.csv (이름·역할·처음 나오는 챕터·나올 비중·배수)
        Kachi_Gun/Assets/Resources/Tables/String.csv (영어 이름) · Global_Config.csv (BossScale · Horde_Mul)
        Kachi_Gun/Assets/Resources/Art/Zombie/z_<id>.png (7칸 400x380: 걷기 0~3 · 맞기 4 · 죽기 5 · 흰 실루엣 6)
        Kachi_Gun/Logs/PlayTest/01b_horde.png · 15_boss_raid.png (실제 게임 화면, 없으면 그 절은 뺌)

사용: PYTHONUTF8=1 python tools/zombie_sheet.py      → 좀비_정리.html
      좀비 그림(doodle_art.py zombie)이나 Zombie 표를 바꾼 뒤 다시 돌리면 문서가 새로 만들어짐.
11-11: 좀비 44종(새 32) · 챕터 색 · 배경 판 · 보스 6마리 순환 — 나올 확률은 게임과 같은 식(GameManager.ZombieWeight: 배경 x · 새 얼굴 x)
       으로 대표 챕터만 · 그림은 WebP(문서 크기)
"""
import base64
import csv
import datetime
import html
import io
import os

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RES = os.path.join(ROOT, 'Kachi_Gun', 'Assets', 'Resources')
TAB = os.path.join(RES, 'Tables')
ART = os.path.join(RES, 'Art', 'Zombie')
SHOT = os.path.join(ROOT, 'Kachi_Gun', 'Logs', 'PlayTest')
OUT = os.path.join(ROOT, '좀비_정리.html')

CW, CH = 400, 380            # 시트 한 칸
DIRT = (238, 222, 184)       # 게임 흙길 색 (그림이 실제로 놓이는 바탕)

CRAWL = {'crawler', 'bucket'}
ROBE = {'reaper', 'ghost'}
LOOK = {                     # 생김새 한 줄 (doodle_art.py zombies() 의 색·소품 그대로)
    'crawler': '바닥을 기어 오는 좀비. 주황 셔츠 · 보라 바지.',
    'knife': '파란 셔츠, 앞으로 뻗은 손에 칼.',
    'basic': '기본 좀비. 주황 셔츠 · 파란 바지, 두 팔을 앞으로.',
    'hat': '갈색 중절모를 쓴 좀비.',
    'bucket': '양동이를 머리에 쓰고 기어 오는 좀비.',
    'armor': '회색 갑옷 · 어깨 보호대 · 쇠 장갑. 몸이 넓음.',
    'bat': '나무 방망이를 든 좀비.',
    'farmer': '밀짚모자 · 멜빵바지 · 쇠스랑.',
    'mummy': '온몸에 붕대를 감은 미라.',
    'fat': '배가 나온 뚱보. 웃통을 벗음.',
    'reaper': '후드 달린 긴 망토(다리 없음) · 해골 얼굴 · 낫.',
    'ogre': '뿔 두 개 · 큰 근육 몸 · 뒷손에 방망이.',
    # 11-11 요청_03
    'hatchet': '나무꾼 — 빨간 체크 셔츠 · 털모자 · 손도끼.', 'chainsaw': '앞치마 · 두 손으로 든 전기톱.',
    'bucketw': '기어 오는 양동이 좀비와 같은 옷 · 서서 걸어 옴.', 'helmet': '중세 기사 투구만 · 낡은 웃옷 + 허리띠.',
    'suit': '정장 · 빨간 넥타이 · 넘긴 머리.', 'sport': '파란 운동복 · 흰 줄 · 흰 운동화.',
    'flag': '찢어진 빨간 깃발(뼈 X 무늬) — 걸을 때 흔들림.', 'ghost': '다리 없이 떠다니는 흰 몸 · 죽을 땐 아래로 꺼짐.',
    'miner': '주황 안전모 + 이마 등 · 멜빵 · 곡괭이.', 'fisher': '노란 비옷 · 비 모자 · 낚싯대 끝에 물고기.',
    'jason': '큰 몸 · 흰 하키 가면 · 큰 칼 (요청의 "제이슨 좀비").', 'pharaoh': '금·파랑 줄무늬 두건 · 금 목걸이 · 붕대 · 금 지팡이.',
    # 11-11 좀비 추가 제안
    'padding': '무릎까지 오는 롱패딩 · 방울 털모자 · 귀마개.', 'ski': '고글 · 목도리 · 스키 두 짝 · 폴. 다리를 붙인 채 미끄러져 옴.',
    'scientist': '흰 가운 · 동그란 안경 · 회색 머리 · 청록 플라스크.', 'patient': '줄무늬 환자복 · 머리 붕대 · 바퀴 달린 링거대.',
    'hazmat': '노란 방호복이 머리까지 · 방독면. 얼굴이 안 보여 색 변형 없음.', 'tube': '줄무늬 수영복 · 허리에 오리 튜브 · 물안경 · 오리발.',
    'cowboy': '넓은 챙 모자 · 판초 · 박차 장화 · 올가미.', 'biker': '가죽 재킷 · 선글라스 · 닭 볏 머리 · 쇠사슬.',
    'camper': '등보다 큰 배낭 · 냄비 · 앞손에 랜턴.', 'delivery': '오토바이 헬멧 · 등에 배달통 · 주황 조끼. 빠름.',
    'jiangshi': '관모 · 이마에 노란 부적 · 긴 관복. 두 발 모아 깡총깡총.', 'chef': '높은 조리모 · 흰 조리복 · 프라이팬.',
    'student': '교복 · 빨간 넥타이 · 큰 책가방. 작고 빠름(크기 x0.78).', 'firefighter': '방화복(반사 띠) · 빨간 헬멧 · 소화기.',
    'money': '줄무늬 옷 · 눈가리개 · $ 자루. 드물게 나오고 골드 x8.', 'drum': '빨간 드럼통(경고 띠 · 청록 독물). 죽으면 펑 — 주변 좀비 피해.',
    'shield': '남색 제복 · 경찰 모자 · 투명 방패 (깨지는 규칙은 나중).', 'yeti': '흰 털 큰 몸 · 얼음 몽둥이.',
    'captain': '선장 모자 · 남색 긴 코트(금 단추) · 회색 수염 · 큰 닻.', 'gasgiant': '큰 몸 · 방독면 · 멜빵바지 · 등에 드럼통 둘 · 청록 연기.',
}
TINT_KO = {'green': '초록(원래)', 'red': '붉은', 'blue': '푸르딩딩', 'purple': '보라'}
MAP_KO = {'side_map': '기본 (둥근 나무)', 'side_map_waste': '황폐 (그루터기 · 마른 땅)', 'side_map_pine': '세모 나무', 'side_map_dead': '죽은 나무'}
SHOW_CH = [1, 2, 3, 5, 6, 13, 20, 28, 40, 60, 100]


def rows(name):
    with open(os.path.join(TAB, name + '.csv'), encoding='utf-8-sig', newline='') as f:
        return list(csv.DictReader(f))


def raw_rows(name):
    with open(os.path.join(TAB, name + '.csv'), encoding='utf-8-sig', newline='') as f:
        return list(csv.reader(f))


def b64(im, fmt='WEBP', **kw):
    buf = io.BytesIO()
    if fmt == 'WEBP':
        kw = {'quality': 88, 'method': 6}
    im.save(buf, fmt, **kw)
    mime = {'PNG': 'image/png', 'WEBP': 'image/webp'}.get(fmt, 'image/jpeg')
    return 'data:%s;base64,%s' % (mime, base64.b64encode(buf.getvalue()).decode())


def trim(im, pad=6):
    a = np.array(im)[..., 3]
    ys, xs = np.nonzero(a > 8)
    if len(xs) == 0:
        return im
    return im.crop((max(0, xs.min() - pad), max(0, ys.min() - pad), min(im.width, xs.max() + 1 + pad), min(im.height, ys.max() + 1 + pad)))


def num(v):
    f = float(v)
    return ('%g' % f)


def speed_word(v):
    return '빠름' if v >= 1.1 else '느림' if v <= 0.8 else '보통'


def main():
    Z = rows('Zombie_Master')
    en = {r[0]: r[1] for r in raw_rows('String') if len(r) >= 3 and r[2].strip() == 'Zombie_Master.Name'}
    cfg = {r[0]: r[1] for r in raw_rows('Global_Config') if len(r) >= 2}
    boss_scale = float(cfg.get('BossScale', 1.7))
    horde = cfg.get('Horde_Mul', '1')
    raid_scale = 2.3
    try:
        raid_scale = float(rows('Boss_Dungeon')[0]['Boss_Scale'])
    except Exception:
        pass
    n_ch = max(int(r['Chapter']) for r in rows('Chapter_Summary'))

    newest = 0
    items = []
    for r in Z:
        zid = r['Id']
        p = os.path.join(ART, 'z_%s.png' % zid)
        newest = max(newest, os.path.getmtime(p))
        sheet = Image.open(p).convert('RGBA')
        cell = lambda i: sheet.crop((i * CW, 0, (i + 1) * CW, CH))
        walk, hit, die = trim(cell(0)), trim(cell(4)), trim(cell(5))
        scale = float(r['Scale'])
        weight = float(r['Base_Weight'])
        boss = weight <= 0
        show = scale * (boss_scale if boss else 1)          # 한눈에 줄에서의 크기
        k = 0.35 * show
        small = walk.resize((max(1, round(walk.width * k * 2)), max(1, round(walk.height * k * 2))), Image.LANCZOS)
        half = lambda im: im.resize((im.width * 3 // 5, im.height * 3 // 5), Image.LANCZOS)
        items.append(dict(
            id=zid, name=r['Name'], en=en.get(r['Name'], ''), role=r['Role'], minch=int(r['MinChapter']), weight=weight, boss=boss,
            hp=float(r['HpMul']), gold=float(r['GoldMul']), speed=float(r['Speed']), scale=scale,
            move='기어 옴' if zid in CRAWL else '떠서 옴' if zid in ROBE else '깡총깡총' if r.get('Move') == 'Hop' else '미끄러져 옴' if r.get('Move') == 'Slide' else '걸어 옴',
            biome=r.get('Biome', ''), effect=r.get('Effect', ''),
            walk=b64(walk, optimize=True), walk_wh=(walk.width, walk.height),
            hit=b64(half(hit), optimize=True), die=b64(half(die), optimize=True),
            small=b64(small, optimize=True), small_wh=(round(walk.width * k), round(walk.height * k)),
        ))
    items.sort(key=lambda z: (z['boss'], z['minch']))
    normal = [z for z in items if not z['boss']]
    bosses = [z for z in items if z['boss']]

    # 챕터별 나올 확률 (GameManager.ZombieWeight 와 같은 식: Base_Weight x 배경이 맞으면 Zombie_Biome_Mul x 처음 나온 뒤 몇 챕터 Zombie_New_Mul)
    steps = sorted({z['minch'] for z in normal})
    chs = {int(r['Chapter']): r for r in rows('Chapter_Summary')}
    cf = lambda k, d: float(cfg.get(k, d))
    def zw(z, ch):
        if z['minch'] > ch or z['weight'] <= 0: return 0.0
        w = z['weight']
        if z['biome'] and z['biome'] == chs[(ch - 1) % n_ch + 1]['Biome_Name']: w *= cf('Zombie_Biome_Mul', 1)
        if z['minch'] >= cf('Zombie_New_From', 6) and ch - z['minch'] < cf('Zombie_New_Chapters', 0): w *= cf('Zombie_New_Mul', 1)
        return w
    def prob(ch):
        s = sum(zw(z, ch) for z in normal)
        return {z['id']: zw(z, ch) / s * 100 for z in normal if zw(z, ch) > 0}, sum(1 for z in normal if zw(z, ch) > 0)
    P = [(ch, *prob(ch)) for ch in SHOW_CH]
    # 1~150 동안 나온 몫 (모든 챕터 더함) — "고르게"를 보는 값
    tot = {z['id']: 0.0 for z in normal}
    for ch in range(1, n_ch + 1):
        for k, v in prob(ch)[0].items(): tot[k] += v / n_ch

    E = html.escape
    o = []
    w = o.append
    day = datetime.date.today().isoformat()
    art_day = datetime.date.fromtimestamp(newest).isoformat()
    w('''<!doctype html><html lang="ko"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>좀비 정리</title><style>
:root{--bg:#fbf7ef;--fg:#3b281c;--mut:#7a6a5c;--line:#e3d8c6;--card:#fff;--dirt:#eedeb8;--acc:#a8642f}
*{box-sizing:border-box}
body{background:var(--bg);color:var(--fg);font-family:"Malgun Gothic",sans-serif;margin:0;padding:24px 16px;line-height:1.6}
main{max-width:1080px;margin:auto} h1{font-size:24px;margin:0 0 4px} h2{font-size:18px;margin:34px 0 10px} h3{font-size:15px;margin:22px 0 8px;color:var(--acc)}
.mut{color:var(--mut);font-size:14px} code{background:#f1e8d8;padding:1px 5px;border-radius:4px;font-size:13px}
.box{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:12px 18px;margin:12px 0}
.box ul{margin:6px 0;padding-left:20px}
.line{background:var(--dirt);border:1px solid var(--line);border-radius:12px;padding:18px 14px 10px;display:flex;flex-wrap:wrap;align-items:flex-end;justify-content:center;gap:14px 6px}
.line figure{margin:0;text-align:center;font-size:12px;line-height:1.3} .line img{display:block;margin:0 auto 4px}
.line figcaption b{display:block;font-size:13px}
.cards{display:grid;grid-template-columns:repeat(auto-fill,minmax(min(320px,100%),1fr));gap:12px}
.card{background:var(--card);border:1px solid var(--line);border-radius:14px;overflow:hidden}
.pic{background:var(--dirt);display:grid;grid-template-columns:1fr 104px;gap:8px;padding:10px;align-items:end}
.pic .big{height:236px;display:flex;align-items:flex-end;justify-content:center}
.pic .big img{max-width:100%;max-height:100%}
.pic .side{display:grid;gap:8px}
.pic .side div{background:#f6ecd2;border-radius:8px;height:98px;padding:4px 4px 16px;position:relative;display:flex;align-items:flex-end;justify-content:center}
.pic .side img{max-width:100%;max-height:100%}
.pic .side span{position:absolute;left:0;right:0;bottom:1px;text-align:center;font-size:11px;color:var(--mut)}
.info{padding:10px 14px 12px}
.info .nm{font-size:17px;font-weight:700} .info .nm small{font-weight:400;color:var(--mut);font-size:13px;margin-left:6px}
.chip{display:inline-block;border-radius:999px;padding:0 9px;font-size:12px;background:#f1e8d8;color:#6b4a32;margin:2px 4px 2px 0}
.chip.ch{background:#e4efd6;color:#3f6a2c} .chip.boss{background:#f4d9d3;color:#9a3a2a}
.info p{margin:4px 0 6px;font-size:14px}
.st{display:grid;grid-template-columns:repeat(4,1fr);gap:6px;font-size:12px;color:var(--mut);text-align:center}
.st b{display:block;font-size:15px;color:var(--fg)}
.st div{background:#faf5ea;border-radius:8px;padding:4px 2px}
.file{font-size:12px;color:var(--mut);margin-top:8px}
.scroll{overflow-x:auto}
table{border-collapse:collapse;width:100%;background:var(--card);font-size:14px;border:1px solid var(--line);border-radius:12px}
th,td{border-bottom:1px solid var(--line);padding:6px 10px;text-align:right;white-space:nowrap}
th{color:var(--acc);font-size:13px} td:first-child,th:first-child{text-align:left}
td .mini{height:26px;vertical-align:middle;margin-right:6px}
td.no{color:#c9bca8}
.shots{display:grid;grid-template-columns:repeat(auto-fit,minmax(260px,1fr));gap:12px}
.shots figure{margin:0;background:var(--card);border:1px solid var(--line);border-radius:12px;padding:8px} .shots img{width:100%;border-radius:8px;display:block}
.shots figcaption{font-size:13px;color:var(--mut);margin-top:6px}
</style></head><body><main>''')
    w('<h1>좀비 정리 — 지금 게임에 나오는 좀비 %d종</h1>' % len(items))
    w('<p class="mut">%s 만듦 · 그림은 %s 판(밝은 낙서풍) · 일반 %d종 + 보스 %d종 · 숫자는 <code>Zombie.xlsx</code> 표 그대로 (새 좀비 숫자는 임시값 — 밸런스는 마지막에)</p>'
      % (day, art_day, len(normal), len(bosses)))

    # ---------- 한눈에
    w('<h2>1. 한눈에 — 게임 안 크기 비율 그대로</h2>')
    w('<div class="line">')
    for z in items:
        sw, sh = z['small_wh']
        cap = '보스 x%s' % num(boss_scale) if z['boss'] else '챕터 %d~' % z['minch']
        w('<figure><img src="%s" width="%d" height="%d" alt="%s"><figcaption><b>%s</b>%s</figcaption></figure>'
          % (z['small'], sw, sh, E(z['name']), E(z['name']), cap))
    w('</div>')
    w('<p class="mut">왼쪽부터 처음 나오는 챕터 순서. 게임에서는 분대(왼쪽)를 보도록 좌우를 뒤집어 그립니다 — 여기 그림은 원본 방향(오른쪽을 봄).</p>')

    # ---------- 카드
    w('<h2>2. 좀비별 그림 — 걷기 · 맞기 · 죽기</h2>')
    # 11-11: 챕터 6 부터는 한 종씩이라 구간으로 묶음 (카드 옆에 '챕터 N 부터' 표시)
    bands = [(lo, lo) for lo in steps if lo <= 5] + [(6, 20), (21, 40), (41, 80)]
    groups = [((lo, hi), [z for z in normal if lo <= z['minch'] <= hi]) for lo, hi in bands] + [(None, bosses)]
    for ch, zs in groups:
        if not zs:
            continue
        lo, hi = ch if ch else (0, 0)
        title = '보스 전용' if ch is None else '처음부터 나옴 — 챕터 1' if hi == 1 else '챕터 %d 부터 (스테이지 %d-1)' % (lo, lo) if lo == hi else '챕터 %d~%d 에 새로 나옴' % (lo, max(z['minch'] for z in zs))
        w('<h3>%s (%d종)</h3>' % (title, len(zs)))
        w('<div class="cards">')
        for z in zs:
            w('<div class="card"><div class="pic"><div class="big"><img src="%s" alt="%s 걷기"></div>' % (z['walk'], E(z['name'])))
            w('<div class="side"><div><img src="%s" alt="맞기"><span>맞기</span></div><div><img src="%s" alt="죽기"><span>죽기</span></div></div></div>' % (z['hit'], z['die']))
            w('<div class="info"><div class="nm">%s<small>%s · %s</small></div>' % (E(z['name']), E(z['en']), z['id']))
            chips = '<span class="chip">%s</span>' % E(z['role'])
            chips += '<span class="chip boss">보스로만</span>' if z['boss'] else '<span class="chip ch">챕터 %d 부터</span>' % z['minch']
            chips += '<span class="chip">%s</span>' % z['move']
            w('<div>%s</div><p>%s</p>' % (chips, E(LOOK.get(z['id'], ''))))
            size = 'x%s' % num(boss_scale if z['boss'] else z['scale'])
            size_lbl = '크기 · 던전 x%s' % num(raid_scale) if z['boss'] else '크기'
            hp = '보스 표' if z['boss'] else 'x%s' % num(z['hp'])
            gold = '보스 표' if z['boss'] else 'x%s' % num(z['gold'])
            w('<div class="st"><div><b>%s</b>체력</div><div><b>%s</b>골드</div><div><b>%s</b>속도 · %s</div><div><b>%s</b>%s</div></div>'
              % (hp, gold, num(z['speed']), speed_word(z['speed']), size, size_lbl))
            w('<div class="file">Art/Zombie/z_%s.png · 뼈대 Art/Rig/z_%s.txt</div></div></div>' % (z['id'], z['id']))
        w('</div>')
    w('<p class="mut">체력·골드 = 그 스테이지 기본값 x 이 배수 (기본 "좀비" 가 x1). 속도는 기본 좀비 = 1, 한 마리마다 0.935~1.265배로 조금씩 다릅니다. 보스 체력·골드는 <code>Stage.xlsx</code> 의 보스 칸(스테이지마다 따로)입니다.</p>')

    # ---------- 확률
    w('<h2>3. 챕터별로 누가 얼마나 나오나</h2>')
    w('<div class="scroll"><table><tr><th>좀비</th><th>나올 비중</th>')
    for ch, _, n in P:
        w('<th>챕터 %d<br><small>%s</small><br>%d종</th>' % (ch, E(chs[(ch - 1) % n_ch + 1]['Biome_Name']), n))
    w('<th>1~%d 전체</th></tr>' % n_ch)
    for z in normal:
        w('<tr><td><img class="mini" src="%s" alt="">%s%s</td><td>%s</td>' % (z['small'], E(z['name']), ' <small>(%s)</small>' % E(z['biome']) if z['biome'] else '', num(z['weight'])))
        for ch, pr, _ in P:
            if z['id'] in pr:
                v = pr[z['id']]
                w('<td style="background:rgba(143,196,104,%.2f)">%.1f%%</td>' % (min(0.75, v / 50), v))
            else:
                w('<td class="no">–</td>')
        w('<td><b>%.1f%%</b></td></tr>' % tot[z['id']])
    w('</table></div>')
    w('<p class="mut">좀비가 나올 때마다 "지금 챕터에 나올 수 있는 좀비" 중에서 비중대로 하나를 뽑습니다(<code>GameManager.PickZombie</code>). '
      '비중 = 표 Base_Weight x <b>배경이 맞으면 %s배</b>(괄호 안 배경) x <b>처음 나온 챕터부터 %s챕터 동안 %s배</b>(챕터 %s 이후에 나오는 좀비만). '
      '챕터 %d 부터는 일반 %d종이 모두 나옵니다. 맨 오른쪽 = 1~%d챕터를 다 더했을 때 그 좀비가 나온 몫(고르게 = 약 %.1f%%).</p>'
      % (num(cf('Zombie_Biome_Mul', 1)), num(cf('Zombie_New_Chapters', 0)), num(cf('Zombie_New_Mul', 1)), num(cf('Zombie_New_From', 6)), steps[-1], len(normal), n_ch, 100 / len(normal)))

    # ---------- 색 · 배경 판 · 보스 순환 (11-11)
    w('<h2>3-2. 챕터 색 · 배경 판 · 보스</h2>')
    try:
        import sys
        sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
        from doodle_art import tint_skin
        base = Image.open(os.path.join(ART, 'z_basic.png')).convert('RGBA').crop((0, 0, CW, CH))
        arr = np.array(base)
        w('<div class="line">')
        for tn in ['green', 'red', 'blue', 'purple']:
            im = base if tn == 'green' else Image.fromarray(tint_skin(arr, tn))
            im = trim(im); im = im.resize((im.width * 2 // 5, im.height * 2 // 5), Image.LANCZOS)
            w('<figure><img src="%s" alt=""><figcaption><b>%s</b></figcaption></figure>' % (b64(im), TINT_KO[tn]))
        w('</div>')
    except Exception as ex:
        w('<p class="mut">(색 견본 못 만듦: %s)</p>' % E(str(ex)))
    w('<div class="scroll"><table><tr><th>챕터</th><th>배경</th><th>좀비 색</th><th>배경 판</th><th>보스</th></tr>')
    st = rows('Stage_Table')
    boss_of = {int(r['Chapter']): r['Boss_Id'] for r in st if r['Is_Boss_Stage'] == 'Y'}
    names = {z['id']: z['name'] for z in items}
    for ch in range(1, 13):
        r = chs[ch]
        w('<tr><td>%d</td><td>%s</td><td>%s</td><td>%s</td><td>%s</td></tr>' % (ch, E(r['Biome_Name']), TINT_KO.get(r.get('Zombie_Tint', ''), '-'),
          E(MAP_KO.get(r.get('Map_Sprite', ''), r.get('Map_Sprite', ''))), E(names.get(boss_of.get(ch, ''), boss_of.get(ch, '')))))
    w('<tr><td colspan="5" class="no">… 같은 차례로 %d챕터까지 (배경 6 · 색 4 · 보스 6 이 각각 돌아감)</td></tr></table></div>' % n_ch)
    w('<p class="mut">좀비 색 = 그 챕터 일반 좀비의 %d%% (나머지 원래 초록, Config <code>Tint_Chance</code>) · 보스는 늘 그 색. 초록 피부만 바뀌고 옷·소품은 그대로(셰이더 <code>SpriteFlash</code>). '
      '배경 판은 흙길은 같고 위아래 숲 띠만 다름(<code>Art/Map/side_map*.png</code>, <code>doodle_art.py maps</code>). 보스 던전도 레벨마다 같은 6마리가 돌아감.</p>'
      % round(cf('Tint_Chance', 1) * 100))

    # ---------- 어디서
    w('<h2>4. 어디에 나오나</h2><div class="box"><ul>')
    w('<li><b>스테이지</b> — 지금 챕터 기준으로 위 표대로. 한 번에 6~10마리씩 무리로 몰려나옵니다(마릿수 x%s, 한 마리 체력·골드는 그만큼 나눔).</li>' % E(horde))
    w('<li><b>도전</b> — 그 도전 챕터 기준으로 같은 표대로 뽑아 제자리에 세워 둡니다.</li>')
    w('<li><b>보스</b> — 챕터마다 N-5 스테이지에 %s 차례로 (크기 x%s) · 보스 던전도 레벨마다 같은 차례 (크기 x%s). [보스 도전] · 아래 [보스] 버튼 아이콘 = 그 보스.</li>'
      % (' → '.join(E(names.get(b, b)) for b in [boss_of[c] for c in range(1, 7)]), num(boss_scale), num(raid_scale)))
    w('<li><b>드럼통 좀비</b> — 죽고 0.1초 뒤 펑: 반경 %s칸 안 좀비에게 드럼통 최대 체력 x%s 피해 (넉백 없음).</li>' % (num(cf('Drum_Blast_Radius', 0)), num(cf('Drum_Blast_Mul', 0))))
    w('<li><b>비밀던전(광산)</b> — 좀비 없음 (광석만).</li>')
    w('</ul></div>')

    # ---------- 실제 화면
    shots = [('01b_horde.png', '스테이지 1-1 — 챕터 1 좀비 3종(기어오는 좀비 · 칼 좀비 · 좀비)이 몰려오는 모습'),
             ('15_boss_raid.png', '보스 던전 — 변이 오우거 (크기 x%s)' % num(raid_scale)),
             ('19f_new_zombies.png', '새 좀비 32종 줄 세우기 (색 변형 돌아가며: 초록 · 붉은 · 푸르딩딩 · 보라)'),
             ('19e_drum_blast.png', '드럼통 좀비 펑 — 가까운 좀비가 같이 쓰러짐'),
             ('19g_map_ch1.png', '챕터 1 — 기본 판 · 초록'), ('19g_map_ch2.png', '챕터 2 — 황폐 판 · 붉은'),
             ('19g_map_ch3.png', '챕터 3 — 세모 나무 판 · 푸르딩딩'), ('19g_map_ch4.png', '챕터 4 — 죽은 나무 판 · 보라')]
    have = [(os.path.join(SHOT, f), c) for f, c in shots if os.path.exists(os.path.join(SHOT, f))]
    if have:
        w('<h2>5. 실제 게임 화면</h2><div class="shots">')
        for p, c in have:
            im = Image.open(p).convert('RGB')
            W, H = im.size
            im = im.crop((0, int(H * 0.25), W, int(H * 0.66)))
            im = im.resize((720, round(im.height * 720 / im.width)), Image.LANCZOS)
            w('<figure><img src="%s" alt=""><figcaption>%s · 자동 시험 촬영 %s</figcaption></figure>'
              % (b64(im, 'JPEG', quality=80), E(c), datetime.date.fromtimestamp(os.path.getmtime(p)).isoformat()))
        w('</div>')

    # ---------- 파일
    w('<h2>%d. 그림 파일 · 바꾸는 법</h2><div class="box"><ul>' % (6 if have else 5))
    w('<li>그림 = <code>Kachi_Gun/Assets/Resources/Art/Zombie/z_&lt;id&gt;.png</code> — 한 장에 7칸(칸 400x380): 걷기 4칸(같은 그림) · 맞기 · 죽기 · 흰 실루엣.</li>')
    w('<li>걷는 움직임은 그림을 바꿔 끼우는 게 아니라 <b>뼈대</b>(<code>Art/Rig/z_&lt;id&gt;.txt</code>)가 걷기 그림의 다리·팔·머리를 움직여 만듭니다. 맞기 = 뒤로 살짝 젖힘, 죽기 = 뒤로 벌러덩(기는 좀비는 그 자리에 납작).</li>')
    w('<li>그림 다시 그리기: PowerShell 에서 <code>python tools/doodle_art.py zombie</code> → <code>python tools/rig_gen.py</code> (뼈대 다시) → 배치 시험.</li>')
    w('<li>이름 · 처음 나오는 챕터 · 나올 비중 · 배수 = <code>Kachi_Gun/Table/Zombie.xlsx</code> (게임은 Zombie_Master.csv 를 읽음).</li>')
    w('<li>이 문서 다시 만들기: <code>PYTHONUTF8=1 python tools/zombie_sheet.py</code></li>')
    w('</ul></div>')
    w('</main></body></html>')

    with open(OUT, 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(o))
    print('saved', OUT, '%.2f MB' % (os.path.getsize(OUT) / 1e6), '· zombies', len(items))


if __name__ == '__main__':
    main()
