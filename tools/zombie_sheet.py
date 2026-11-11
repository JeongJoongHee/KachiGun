# -*- coding: utf-8 -*-
"""지금 게임에 나오는 좀비 그림을 한 장 문서로 — 좀비_정리.html (그림은 문서 안에 들어감, 파일 하나)

읽는 것: Kachi_Gun/Assets/Resources/Tables/Zombie_Master.csv (이름·역할·처음 나오는 챕터·나올 비중·배수)
        Kachi_Gun/Assets/Resources/Tables/String.csv (영어 이름) · Global_Config.csv (BossScale · Horde_Mul)
        Kachi_Gun/Assets/Resources/Art/Zombie/z_<id>.png (7칸 400x380: 걷기 0~3 · 맞기 4 · 죽기 5 · 흰 실루엣 6)
        Kachi_Gun/Logs/PlayTest/01b_horde.png · 15_boss_raid.png (실제 게임 화면, 없으면 그 절은 뺌)

사용: PYTHONUTF8=1 python tools/zombie_sheet.py      → 좀비_정리.html
      좀비 그림(doodle_art.py zombie)이나 Zombie 표를 바꾼 뒤 다시 돌리면 문서가 새로 만들어짐.
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
ROBE = {'reaper'}
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
}


def rows(name):
    with open(os.path.join(TAB, name + '.csv'), encoding='utf-8-sig', newline='') as f:
        return list(csv.DictReader(f))


def raw_rows(name):
    with open(os.path.join(TAB, name + '.csv'), encoding='utf-8-sig', newline='') as f:
        return list(csv.reader(f))


def b64(im, fmt='PNG', **kw):
    buf = io.BytesIO()
    im.save(buf, fmt, **kw)
    mime = 'image/png' if fmt == 'PNG' else 'image/jpeg'
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
            move='기어 옴' if zid in CRAWL else '망토로 떠서 옴' if zid in ROBE else '걸어 옴',
            walk=b64(walk, optimize=True), walk_wh=(walk.width, walk.height),
            hit=b64(half(hit), optimize=True), die=b64(half(die), optimize=True),
            small=b64(small, optimize=True), small_wh=(round(walk.width * k), round(walk.height * k)),
        ))
    items.sort(key=lambda z: (z['boss'], z['minch']))
    normal = [z for z in items if not z['boss']]
    bosses = [z for z in items if z['boss']]

    # 챕터별 나올 확률 (GameManager.PickZombie: MinChapter <= 챕터 인 것들의 Base_Weight 비율)
    steps = sorted({z['minch'] for z in normal})
    def prob(ch):
        pool = [z for z in normal if z['minch'] <= ch]
        s = sum(z['weight'] for z in pool)
        return {z['id']: z['weight'] / s * 100 for z in pool}, len(pool)
    P = [(ch, *prob(ch)) for ch in steps]

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
    w('<p class="mut">%s 만듦 · 그림은 %s 판(밝은 낙서풍, 죽는 모습 고친 뒤) · 일반 %d종 + 보스 %d종 · 숫자는 <code>Zombie.xlsx</code> 표 그대로</p>'
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
    groups = [(ch, [z for z in normal if z['minch'] == ch]) for ch in steps] + [(None, bosses)]
    for ch, zs in groups:
        if not zs:
            continue
        w('<h3>%s (%d종)</h3>' % ('보스 전용' if ch is None else '처음부터 나옴 — 챕터 1' if ch == 1 else '챕터 %d 부터 (스테이지 %d-1)' % (ch, ch), len(zs)))
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
    for i, (ch, _, n) in enumerate(P):
        last = i == len(P) - 1
        w('<th>챕터 %s<br>%d종</th>' % ('%d~%d' % (ch, n_ch) if last else ('%d' % ch if P[i + 1][0] == ch + 1 else '%d~%d' % (ch, P[i + 1][0] - 1)), n))
    w('</tr>')
    for z in normal:
        w('<tr><td><img class="mini" src="%s" alt="">%s</td><td>%s</td>' % (z['small'], E(z['name']), num(z['weight'])))
        for ch, pr, _ in P:
            if z['id'] in pr:
                v = pr[z['id']]
                w('<td style="background:rgba(143,196,104,%.2f)">%.1f%%</td>' % (min(0.75, v / 50), v))
            else:
                w('<td class="no">–</td>')
        w('</tr>')
    w('</table></div>')
    w('<p class="mut">좀비가 나올 때마다 "지금 챕터에 나올 수 있는 좀비" 중에서 비중대로 하나를 뽑습니다(<code>GameManager.PickZombie</code>). 챕터 %d 부터는 일반 %d종이 모두 나오고 비율이 끝까지 같습니다.</p>'
      % (steps[-1], len(normal)))

    # ---------- 어디서
    w('<h2>4. 어디에 나오나</h2><div class="box"><ul>')
    w('<li><b>스테이지</b> — 지금 챕터 기준으로 위 표대로. 한 번에 6~10마리씩 무리로 몰려나옵니다(마릿수 x%s, 한 마리 체력·골드는 그만큼 나눔).</li>' % E(horde))
    w('<li><b>도전</b> — 그 도전 챕터 기준으로 같은 표대로 뽑아 제자리에 세워 둡니다.</li>')
    for z in bosses:
        w('<li><b>보스</b> — 챕터마다 N-5 스테이지에 <b>%s</b> (크기 x%s) · 보스 던전에도 같은 그림 (크기 x%s). 로비 [보스] 버튼 아이콘도 이 그림입니다.</li>'
          % (E(z['name']), num(boss_scale), num(raid_scale)))
    w('<li><b>비밀던전(광산)</b> — 좀비 없음 (광석만).</li>')
    w('</ul></div>')

    # ---------- 실제 화면
    shots = [('01b_horde.png', '스테이지 1-1 — 챕터 1 좀비 3종(기어오는 좀비 · 칼 좀비 · 좀비)이 몰려오는 모습'),
             ('15_boss_raid.png', '보스 던전 — 변이 오우거 (크기 x%s)' % num(raid_scale))]
    have = [(os.path.join(SHOT, f), c) for f, c in shots if os.path.exists(os.path.join(SHOT, f))]
    if have:
        w('<h2>5. 실제 게임 화면</h2><div class="shots">')
        for p, c in have:
            im = Image.open(p).convert('RGB')
            W, H = im.size
            im = im.crop((0, int(H * 0.25), W, int(H * 0.66)))
            im = im.resize((720, round(im.height * 720 / im.width)), Image.LANCZOS)
            w('<figure><img src="%s" alt=""><figcaption>%s · 자동 시험 촬영 %s</figcaption></figure>'
              % (b64(im, 'JPEG', quality=84), E(c), datetime.date.fromtimestamp(os.path.getmtime(p)).isoformat()))
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
