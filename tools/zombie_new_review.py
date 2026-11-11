# -*- coding: utf-8 -*-
"""11-11 요청_03 새 좀비 시안 문서 — 좀비_추가_시안.html (그림은 문서 안에 들어감, 파일 하나)

보여 주는 것: 새 좀비 12종(걷기 GIF = 게임과 같은 뼈대 계산 · 맞기 · 죽기) · 색 변형 3가지(초록 피부만, doodle_art.tint_skin)
            · 섞여 몰려오는 장면 · 결정 항목(추천안) — 항목마다 체크·의견 칸(localStorage, 키 KEY)
읽는 것:   Art/Zombie/z_<id>.png · Art/Rig/z_<id>.txt 를 만드는 rig_gen.py 의 뼈 자리(ZOMBIE_RIG) · Art/Char/skin_00 · gun_00
사용:      PYTHONUTF8=1 python tools/zombie_new_review.py          → 좀비_추가_시안.html
의견 읽기: PYTHONUTF8=1 python tools/todo_checks.py 좀비_추가_시안.html
"""
import base64
import html
import io
import os
import random
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import doodle_art as D          # noqa: E402
import rig_gen as R             # noqa: E402

ROOT = D.ROOT
ART = D.ART
OUT = os.path.join(ROOT, '좀비_추가_시안.html')
KEY = 'kachigun-zombie-1111'
DIRT = (238, 222, 184, 255)
E = html.escape

# id: (이름, 영어 이름, 생김새, 움직임)
NEW = {
    'hatchet': ('손도끼 좀비', 'Hatchet Zombie', '나무꾼 — 빨간 체크 셔츠 · 털모자 · 손도끼', '걸어 옴'),
    'chainsaw': ('자동톱 좀비', 'Chainsaw Zombie', '앞치마 · 두 손으로 든 전기톱', '걸어 옴'),
    'bucketw': ('양동이 좀비 (걷는)', 'Bucket Walker', '지금의 기어오는 양동이 좀비와 같은 옷 · 걸어 옴', '걸어 옴'),
    'helmet': ('헬멧 좀비', 'Knight Helmet Zombie', '중세 기사 투구만 (몸은 갑옷 없이 낡은 웃옷 + 허리띠) · 눈 틈으로 눈만 보임', '걸어 옴'),
    'suit': ('양복 좀비', 'Suit Zombie', '정장 · 빨간 넥타이 · 넘긴 머리', '걸어 옴'),
    'sport': ('운동복 좀비', 'Tracksuit Zombie', '파란 운동복 · 흰 줄 · 흰 운동화 · 걸음이 조금 큼', '걸어 옴'),
    'flag': ('깃발 좀비', 'Flag Zombie', '찢어진 빨간 깃발(뼈 X 무늬) — 걸을 때 깃발이 흔들림', '걸어 옴'),
    'ghost': ('유령 좀비', 'Ghost Zombie', '다리 없이 떠다니는 흰 몸 · 죽을 땐 아래로 꺼짐 · 초록 피부가 없어 색 변형 없음', '떠서 옴'),
    'miner': ('광부 좀비', 'Miner Zombie', '주황 안전모 + 이마 등 · 멜빵 · 곡괭이', '걸어 옴'),
    'fisher': ('어부 좀비', 'Fisher Zombie', '노란 비옷 · 비 모자 · 낚싯대 끝에 물고기', '걸어 옴'),
    'jason': ('하키 가면 좀비 (보스)', 'Hockey Mask Zombie', '요청의 "제이슨 좀비" — 큰 몸 · 흰 하키 가면 · 큰 칼. 원작의 빨간 삼각 무늬는 뺐음(이름은 결정 ⑤)', '걸어 옴 · 보스'),
    'pharaoh': ('파라오 좀비 (보스)', 'Pharaoh Zombie', '금·파랑 줄무늬 두건 · 금 목걸이 · 가슴 붕대 · 흰 치마 · 금 지팡이', '걸어 옴 · 보스'),
}
BOSSES = {'jason', 'pharaoh', 'ogre'}
OLD_GREEN = ['basic', 'crawler', 'knife', 'hat', 'bucket', 'armor', 'bat', 'farmer', 'fat', 'ogre']
OLD_NAMES = {'basic': '좀비', 'crawler': '기어오는 좀비', 'knife': '칼 좀비', 'hat': '모자 좀비', 'bucket': '양동이 좀비', 'armor': '갑옷 좀비',
             'bat': '방망이 좀비', 'farmer': '농부 좀비', 'fat': '뚱보 좀비', 'ogre': '변이 오우거 (보스)', 'mummy': '미라', 'reaper': '사신 좀비'}
TINT_KO = [(None, '기본 (초록)'), ('red', '붉은'), ('blue', '푸르딩딩'), ('purple', '보라')]


def b64(im, fmt='PNG', **kw):
    buf = io.BytesIO()
    im.save(buf, fmt, **kw)
    mime = {'PNG': 'image/png', 'JPEG': 'image/jpeg', 'GIF': 'image/gif'}[fmt]
    return 'data:%s;base64,%s' % (mime, base64.b64encode(buf.getvalue()).decode())


def cell(zid, i=0):
    return Image.open(os.path.join(ART, 'Zombie', 'z_%s.png' % zid)).convert('RGBA').crop((i * 400, 0, i * 400 + 400, 380))


def trim(im, pad=6):
    a = np.array(im)[..., 3]
    ys, xs = np.nonzero(a > 8)
    return im.crop((max(0, xs.min() - pad), max(0, ys.min() - pad), min(im.width, xs.max() + 1 + pad), min(im.height, ys.max() + 1 + pad)))


def on_dirt(im):
    bg = Image.new('RGBA', im.size, DIRT)
    bg.alpha_composite(im)
    return bg.convert('RGB')


def walk_gif(zid, frames=12, w=220):
    """게임과 같은 뼈대 걷기(rig_gen 의 자세 계산) — 흙색 바탕 GIF. 넓게 그려 칸 밖으로 나간 칼·낚싯대도 보이게"""
    rig, _ = (R.crawl_rig if zid in R.CRAWL_RIG else R.zombie_rig)(zid)
    W, H, pad = rig.W + 80, rig.H + 20, 40
    imgs = []
    for k in range(frames):
        pts = R.deform(rig, R.pose_angles(rig.kind, rig.anim, k / frames))
        pts = [(x + pad, y) for x, y in pts]
        im = Image.fromarray(R.render(rig, pts, W, H))
        im = on_dirt(im).resize((w, round(H * w / W)), Image.LANCZOS)
        imgs.append(im.convert('P', palette=Image.ADAPTIVE, colors=96))
    buf = io.BytesIO()
    imgs[0].save(buf, 'GIF', save_all=True, append_images=imgs[1:], duration=90, loop=0, optimize=True)
    return 'data:image/gif;base64,' + base64.b64encode(buf.getvalue()).decode()


def tinted(zid, tint, i=0):
    im = cell(zid, i)
    return im if tint is None else Image.fromarray(D.tint_skin(np.array(im), tint))


def horde_scene(seed=7):
    """카피바라 + 섞여 몰려오는 좀비 (게임처럼 왼쪽을 봄 · 아래가 앞)"""
    rnd = random.Random(seed)
    Wd, Hd = 1000, 470
    sc = Image.new('RGBA', (Wd, Hd), (232, 214, 172, 255))
    pool = [z for z in OLD_GREEN + list(NEW) + ['mummy', 'reaper'] if z not in BOSSES]
    items = []
    for i in range(30):
        zid = pool[i % len(pool)] if i < len(pool) else rnd.choice(pool)
        t = rnd.choice(['red', 'blue', 'purple']) if rnd.random() < 0.4 else None
        x = rnd.uniform(330, Wd - 30); y = rnd.uniform(150, Hd - 6)
        items.append((y, x, zid, t))
    k = 0.30
    for y, x, zid, t in sorted(items):
        im = tinted(zid, t).transpose(Image.FLIP_LEFT_RIGHT)
        im = im.resize((round(400 * k), round(380 * k)), Image.LANCZOS)
        sc.alpha_composite(im, (int(x - 200 * k), int(y - 376 * k)))
    capy = Image.open(os.path.join(ART, 'Char', 'skin_00.png')).convert('RGBA').crop((0, 0, 360, 360))
    gun = Image.open(os.path.join(ART, 'Char', 'gun_00.png')).convert('RGBA')
    px, py = D.CX0 + D.GPX * D.CS, D.CY0 + D.GPY * D.CS
    capy.alpha_composite(gun, (int(round(px - 90)), int(round(py - 55))))
    capy = capy.resize((216, 216), Image.LANCZOS)
    sc.alpha_composite(capy, (40, 170))
    return sc.convert('RGB')


def card(zid, name, en, look, move, gif):
    hit = trim(cell(zid, 4)); die = trim(cell(zid, 5))
    hit.thumbnail((110, 110)); die.thumbnail((130, 90))
    return (f'<li data-id="z-{zid}"><label><input type="checkbox"><span>'
            f'<span class="pic"><img class="walk" src="{gif}" alt="{E(name)} 걷기">'
            f'<span class="side"><span class="mini"><img src="{b64(hit, optimize=True)}" alt=""><i>맞기</i></span>'
            f'<span class="mini"><img src="{b64(die, optimize=True)}" alt=""><i>죽기</i></span></span></span>'
            f'<span class="txt"><b>{E(name)}</b> <small>{E(en)} · {zid}</small><br>{E(look)}'
            f'<br><span class="tagz">{E(move)}</span> <span class="ok">✔ = 이대로 좋음</span></span>'
            f'</span></label></li>')


def decision(did, title, body, opts, rec):
    li = ''.join(f'<li>{"<span class=rec>추천 </span>" if i == rec else ""}{o}</li>' for i, o in enumerate(opts))
    return (f'<li data-id="{did}"><label><input type="checkbox"><span><span class="tag t-pick">결정</span><b>{title}</b>'
            f'<span class="how">{body}</span><ol class="opts">{li}</ol></span></label></li>')


def main():
    o = []
    w = o.append
    w('''<!doctype html><html lang="ko"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>새 좀비 시안</title><style>
:root{--bg:#fbf7ef;--fg:#3b281c;--mut:#7a6a5c;--line:#e3d8c6;--card:#fff;--dirt:#eedeb8;--acc:#a8642f;--green:#3f6a2c;--green-bg:#e4efd6;--orange:#b5652a;--orange-bg:#fbe9d6}
*{box-sizing:border-box}
body{background:var(--bg);color:var(--fg);font-family:"Malgun Gothic",sans-serif;margin:0;padding:24px 16px 60px;line-height:1.6}
main{max-width:1100px;margin:auto} h1{font-size:24px;margin:0 0 4px} h2{font-size:18px;margin:36px 0 10px}
.mut{color:var(--mut);font-size:14px} code{background:#f1e8d8;padding:1px 5px;border-radius:4px;font-size:13px}
.box{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:12px 18px;margin:12px 0} .box ul{margin:6px 0;padding-left:20px}
.bar{position:sticky;top:0;background:var(--bg);padding:8px 0;display:flex;gap:10px;align-items:center;z-index:2;border-bottom:1px solid var(--line);flex-wrap:wrap;font-size:14px}
#copyNotes{border:1px solid var(--orange);background:var(--orange-bg);color:var(--orange);border-radius:8px;padding:2px 10px;cursor:pointer;font-size:13px}
.scene img{width:100%;border-radius:12px;border:1px solid var(--line);display:block}
ul.todo{list-style:none;padding:0;margin:0}
ul.todo > li{background:var(--card);border:1px solid var(--line);border-radius:12px;margin:8px 0}
ul.todo .row{display:flex;gap:8px;align-items:flex-start;padding:10px 12px}
ul.todo label{flex:1;display:flex;gap:10px;cursor:pointer;min-width:0} ul.todo input{margin-top:6px;transform:scale(1.25);flex:none}
ul.cards{display:grid;grid-template-columns:repeat(auto-fill,minmax(min(330px,100%),1fr));gap:10px}
ul.cards > li{margin:0}
.pic{display:flex;gap:6px;align-items:flex-end;background:var(--dirt);border-radius:10px;padding:6px}
.pic img.walk{width:200px;max-width:62%;height:auto;display:block;border-radius:6px}
.side{display:flex;flex-direction:column;gap:6px;flex:1}
.mini{display:flex;flex-direction:column;align-items:center;background:#f6ecd2;border-radius:8px;padding:4px}
.mini img{max-width:100%;height:auto} .mini i{font-style:normal;font-size:11px;color:var(--mut)}
.txt{display:block;font-size:14px;margin-top:6px} .txt b{font-size:16px} .txt small{color:var(--mut)}
.tagz{display:inline-block;border-radius:999px;padding:0 8px;font-size:12px;background:#f1e8d8;color:#6b4a32}
.ok{font-size:12px;color:var(--green)}
.tag{display:inline-block;font-size:12px;border-radius:6px;padding:0 6px;margin-right:6px;font-weight:bold} .t-pick{background:var(--orange-bg);color:var(--orange)}
.how{display:block;margin:6px 0 2px;font-size:14.5px}
ol.opts{margin:6px 0 0;padding-left:20px;font-size:14px} ol.opts li{margin:3px 0} .rec{color:var(--green);font-weight:bold}
.pen{border:1px solid var(--line);background:var(--bg);border-radius:8px;padding:2px 10px;font-size:13px;cursor:pointer;white-space:nowrap;color:var(--mut)}
li.has-note .pen{background:var(--orange-bg);color:var(--orange);border-color:var(--orange);font-weight:bold}
.notebox{display:none;padding:0 14px 12px 41px} li.open .notebox{display:block}
.notebox textarea{width:100%;min-height:64px;border:1px solid var(--orange);border-radius:8px;padding:8px 10px;background:var(--bg);color:var(--fg);font:15px/1.55 inherit}
ul.todo > li.checked .txt b,ul.todo > li.checked > .row b{color:var(--green)}
.scroll{overflow-x:auto}
table.tint{border-collapse:collapse;background:var(--card);border:1px solid var(--line);border-radius:12px;font-size:13px;min-width:560px;width:100%}
table.tint th,table.tint td{border-bottom:1px solid var(--line);padding:4px 6px;text-align:center}
table.tint th{color:var(--acc)} table.tint td:first-child{text-align:left;white-space:nowrap;font-size:14px}
table.tint td.c{background:var(--dirt)} table.tint img{height:96px;width:auto;display:block;margin:auto}
table.tint tr.new td:first-child b::after{content:" 새";color:var(--orange);font-size:11px}
table.plan{border-collapse:collapse;font-size:13.5px;margin:6px 0;background:#fffdf8}
table.plan th,table.plan td{border:1px solid var(--line);padding:3px 8px;text-align:center} table.plan td:first-child{text-align:left}
</style></head><body><main>''')
    w('<h1>새 좀비 시안 — 요청_03 (12종 + 색 변형 3가지)</h1>')
    w('<p class="mut">2026-11-11 · <code>추가 요청/요청_03.docx</code> · 그림은 지금 화풍(낙서풍) 그대로 · <b>게임에는 아직 안 넣었습니다</b> — 그림을 보고 고칠 곳 + 맨 아래 결정 5개를 정해 주시면 넣습니다.</p>')
    w('<div class="bar"><span id="stat"></span><button id="copyNotes" type="button">의견 모아 복사</button>'
      '<span class="mut">✔ = 이대로 좋음 · ✎ 의견 = 고칠 곳 (이 브라우저에 저장, 제가 <code>todo_checks.py</code> 로 읽음)</span></div>')
    w('<div class="box"><b>요청을 이렇게 읽었습니다</b><ul>'
      '<li><b>색상 추가</b> = 좀비 종류는 그대로 두고 <b>초록 피부만</b> 붉은 · 푸르딩딩 · 보라로 바꾼 판. 옷·모자·무기 색은 그대로라 같은 좀비도 달라 보입니다. 미라 · 사신 · 유령은 초록 피부가 없어 바뀌지 않습니다.</li>'
      '<li><b>제이슨 좀비 – 보스</b> = 흰 하키 가면 + 큰 칼을 든 큰 좀비. 영화 원작의 상징인 빨간 삼각 무늬는 넣지 않았고, 이름은 결정 ⑤.</li>'
      '<li><b>양동이 걷는 좀비</b> = 지금 기어 오는 양동이 좀비와 같은 옷·양동이로 서서 걷는 좀비 (따로 한 종).</li>'
      '<li><b>헬멧 좀비 (중세 기사 헬멧만)</b> = 갑옷 없이 투구만 쓴 좀비. 몸은 낡은 웃옷 + 허리띠 (지금의 갑옷 좀비와 겹치지 않게).</li>'
      '<li><b>유령 좀비</b> = 다리 없이 바닥에서 조금 떠서 옴 (사신과 같은 망토 움직임), 죽을 땐 아래로 꺼짐.</li>'
      '<li>그 밖에 손도끼 · 자동톱 · 양복 · 운동복 · 깃발 · 광부 · 어부 · 파라오(보스)는 이름 그대로.</li></ul></div>')

    # ---------- 섞인 장면
    w('<h2>1. 섞여서 몰려오면 — 지금 좀비 + 새 좀비 + 색 변형 40%</h2>')
    w(f'<div class="scene"><img src="{b64(horde_scene(), "JPEG", quality=86)}" alt="섞인 장면"></div>')
    w('<p class="mut">게임처럼 왼쪽(분대 쪽)을 보게 뒤집었고, 크기는 실제 게임 화면과 비슷한 비율입니다. 색 변형 비율 40% 는 보여 주려고 넣은 값이고 실제 비율은 결정 ②.</p>')

    # ---------- 카드
    w('<h2>2. 새 좀비 12종 — 걷기(게임과 같은 뼈대 움직임) · 맞기 · 죽기</h2>')
    w('<ul class="todo cards">')
    for zid, (name, en, look, move) in NEW.items():
        w(card(zid, name, en, look, move, walk_gif(zid)))
        print('card', zid)
    w('</ul>')
    w('<p class="mut">걷기 GIF 는 <code>tools/rig_gen.py</code> 의 자세 계산(게임 <code>Core/Rig.cs</code> 와 같은 식)으로 만들었습니다. 게임에선 걸으면서 앞으로 움직이고, 한 마리마다 걸음 폭·몸 기울기가 조금씩 다릅니다.</p>')

    # ---------- 색 변형
    w('<h2>3. 색 변형 — 초록 피부만 붉은 · 푸르딩딩 · 보라</h2>')
    w('<div class="scroll"><table class="tint"><tr><th>좀비</th>' + ''.join(f'<th>{t}</th>' for _, t in TINT_KO) + '</tr>')
    for zid in OLD_GREEN + [z for z in NEW if z != 'ghost']:
        nm = NEW[zid][0] if zid in NEW else OLD_NAMES[zid]
        w(f'<tr class="{"new" if zid in NEW else ""}"><td><b>{E(nm)}</b></td>')
        for t, _ in TINT_KO:
            im = trim(tinted(zid, t), 4)
            im.thumbnail((140, 120), Image.LANCZOS)
            w(f'<td class="c"><img src="{b64(im, optimize=True)}" alt=""></td>')
        w('</tr>')
        print('tint', zid)
    w('</table></div>')
    w('<ul class="todo">')
    w('<li data-id="t-colors"><label><input type="checkbox"><span><b>색 세 가지 느낌</b> — 붉은(채도 낮은 벽돌색) · 푸르딩딩(회색 섞인 하늘색) · 보라(연보라). '
      '쨍하지 않게 지금 초록과 같은 밝기·채도로 맞췄습니다. 더 진하게·옅게, 다른 색 추가는 의견에 (예: "보라 더 진하게"). <span class="ok">✔ = 이대로 좋음</span></span></label></li>')
    w('</ul>')

    # ---------- 결정
    w('<h2>4. 게임에 넣는 방법 — 결정 5개</h2>')
    w('<p class="mut">글자를 누르면 의견 칸이 열립니다. "추천대로" 라고만 적어도 됩니다. 체력·속도 같은 숫자는 임시값으로 넣고 밸런스는 가장 마지막에 (지금까지처럼).</p>')
    w('<ul class="todo">')
    w(decision('d-tintway', '① 색 변형을 넣는 방법',
               '색마다 그림을 따로 만들지, 게임이 그릴 때 피부색만 바꿀지.',
               ['<b>그릴 때 바꾸기(셰이더)</b> — 지금 맞을 때 하얗게 번쩍이는 셰이더에 "초록 → 다른 색" 몇 줄. 그림 파일이 늘지 않고 지금 좀비·새 좀비·보스 모두 바로 됨. 이 문서 그림과 같은 식',
                '<b>색마다 그림 파일</b> — 21종 x 3색 = 63장 + 뼈대 63개. 그림 한 장이 게임 안에서 압축 없이 4MB 라 앱이 크게 늘어남'], 0))
    w(decision('d-tintuse', '② 색 좀비가 나오는 방식',
               '보기만 다르게 할지, 색마다 성질을 줄지.',
               ['<b>무작위로 섞기, 성질은 같음</b> — 한 마리마다 정해진 확률로 색이 바뀜(챕터 3부터 30% 추천). 체력·골드 그대로. Config 값 2개(<code>Tint_Start_Chapter</code> · <code>Tint_Chance</code>)로 바꿀 수 있게',
                '<b>챕터마다 한 색</b> — 그 챕터 좀비는 대부분 그 색 (배경 색과 맞춤)',
                '<b>색마다 성질</b> — 예: 붉은 = 빠름 · 푸르딩딩 = 단단함 · 보라 = 골드 많이. 새 규칙이라 시험 · 밸런스가 더 필요'], 0))
    plan = ('<table class="plan"><tr><th>안</th><th>챕터 1</th><th>2</th><th>3</th><th>4</th><th>5</th><th>6</th><th>7</th><th>8</th><th>9</th><th>10</th></tr>'
            '<tr><td>① 1~10</td><td>운동복</td><td>양동이(걷는)</td><td>손도끼</td><td>깃발</td><td>양복</td><td>광부</td><td>어부</td><td>헬멧</td><td>자동톱</td><td>유령</td></tr></table>')
    w(decision('d-newch', '③ 새 일반 좀비 10종이 처음 나오는 챕터',
               '지금은 챕터 1 에 3종 → 5 까지 11종이 다 나오고 그 뒤로는 같은 좀비만 나옵니다.' + plan,
               ['<b>챕터 1~10 에 한 종씩</b> (위 표) — 처음부터 종류가 늘고, 10챕터까지 계속 새 얼굴. 나올 비중은 지금 일반 좀비와 비슷하게(8 안팎), 강한 것(헬멧 = 단단, 자동톱 = 빠름)만 낮게',
                '<b>챕터 6~15 에 한 종씩</b> — 지금 1~5 는 그대로, 그 뒤부터 새 얼굴',
                '<b>처음부터 모두 섞기</b> — 챕터 1 부터 21종'], 0))
    w(decision('d-boss', '④ 보스 3종(오우거 · 하키 가면 · 파라오)을 쓰는 방식',
               '지금은 모든 챕터 보스 · 보스 던전이 변이 오우거 하나입니다. 보스 체력·골드는 지금 표 그대로(보스 그림만 바뀜).',
               ['<b>챕터마다 돌아가며</b> — 1 오우거 → 2 하키 가면 → 3 파라오 → 4 오우거 … · 보스 던전도 레벨마다 돌아가며 (Stage 표 <code>Boss_Id</code> 칸)',
                '<b>오우거는 그대로, 새 보스 둘은 보스 던전에만</b>',
                '<b>10챕터마다 바뀜</b> — 1~10 오우거 · 11~20 하키 가면 · 21~30 파라오 …'], 0))
    w(decision('d-jason', '⑤ "제이슨 좀비" 이름',
               '"제이슨"은 영화 13일의 금요일의 캐릭터 이름입니다. 스토어에 올린 게임에서 그대로 쓰면 상표·저작권 문제가 될 수 있어 다른 이름을 추천합니다. 그림도 원작 상징(빨간 삼각 무늬)은 뺐습니다.',
               ['<b>하키 가면 좀비</b> / Hockey Mask Zombie',
                '<b>제이슨 좀비</b> 그대로',
                '<b>직접 정하기</b> — 의견에 이름'], 0))
    w('</ul>')

    # ---------- 파일
    w('<h2>5. 그림 파일 · 도구</h2><div class="box"><ul>'
      '<li>그림 = <code>Kachi_Gun/Assets/Resources/Art/Zombie/z_&lt;id&gt;.png</code> 12장 (지금 좀비와 같은 7칸 시트) · 뼈대 = <code>Art/Rig/z_&lt;id&gt;.txt</code> 12개 — 게임 표에 줄이 없어 아직 안 나옴</li>'
      '<li>id: ' + ' · '.join(f'{NEW[z][0]} <code>{z}</code>' for z in NEW) + '</li>'
      '<li>다시 그리기: PowerShell 에서 <code>python tools/doodle_art.py znew</code> (이름을 붙이면 그것만: <code>znew jason ghost</code>) → <code>python tools/rig_gen.py hatchet …</code> → 이 문서 <code>PYTHONUTF8=1 python tools/zombie_new_review.py</code></li>'
      '<li>색 변형 식 = <code>doodle_art.py</code> 의 <code>tint_skin()</code> · <code>TINTS</code> (게임에 넣을 땐 셰이더가 같은 식)</li>'
      '</ul></div>')

    # ---------- 스크립트 (남은_작업_정리 와 같은 저장 형식 — todo_checks.py 가 읽음)
    w('''<script>
(function(){
  var KEY='%s', st={c:{},n:{}};
  try{ var v=JSON.parse(localStorage.getItem(KEY)||'{}'); st.c=v.c||{}; st.n=v.n||{}; }catch(e){}
  function save(){ try{ localStorage.setItem(KEY,JSON.stringify(st)); }catch(e){} stat(); }
  var items=document.querySelectorAll('ul.todo > li[data-id]');
  function stat(){ var c=0,n=0; items.forEach(function(li){ var id=li.dataset.id; if(st.c[id]) c++; if((st.n[id]||'').trim()) n++; });
    document.getElementById('stat').textContent='체크 '+c+' / '+items.length+' · 의견 '+n; }
  items.forEach(function(li){
    var id=li.dataset.id, label=li.querySelector('label'), cb=li.querySelector('input');
    var pick=!!li.querySelector('.t-pick');
    var row=document.createElement('div'); row.className='row'; li.insertBefore(row,label); row.appendChild(label);
    var pen=document.createElement('button'); pen.type='button'; pen.className='pen'; pen.textContent='✎ 의견'; row.appendChild(pen);
    var box=document.createElement('div'); box.className='notebox';
    var ta=document.createElement('textarea'); ta.placeholder=pick?'어떻게 할지 적어 주세요 (예: 1번 / 추천대로)':'고칠 곳이 있으면 적어 주세요';
    box.appendChild(ta); li.appendChild(box);
    cb.checked=!!st.c[id]; ta.value=st.n[id]||'';
    function mark(){ li.classList.toggle('has-note',!!ta.value.trim()); pen.textContent=ta.value.trim()?'✎ 의견 있음':'✎ 의견'; li.classList.toggle('checked',cb.checked); }
    mark(); if(ta.value.trim()) li.classList.add('open');
    cb.addEventListener('change',function(){ st.c[id]=cb.checked; save(); mark(); });
    label.addEventListener('click',function(e){ if(e.target!==cb && e.target.closest && e.target.closest('img,table,code')) e.preventDefault(); });
    ta.addEventListener('input',function(){ if(ta.value.trim()) st.n[id]=ta.value; else delete st.n[id]; mark(); save(); });
    function toggle(){ li.classList.toggle('open'); if(li.classList.contains('open')) ta.focus(); }
    pen.addEventListener('click',toggle);
    if(pick) label.addEventListener('click',function(e){ if(e.target===cb) return; e.preventDefault(); toggle(); });
  });
  document.getElementById('copyNotes').addEventListener('click',function(){
    var out=[], btn=this;
    items.forEach(function(li){ var id=li.dataset.id, note=(st.n[id]||'').trim(), on=!!st.c[id]; if(!note && !on) return;
      var t=(li.querySelector('b')||li).textContent.replace(/\\s+/g,' ').trim();
      out.push((on?'[V] ':'[ ] ')+t+(note?'\\n    → '+note.replace(/\\n/g,'\\n      '):'')); });
    var text=out.length?out.join('\\n'):'(체크·의견 없음)';
    function done(){ btn.textContent='복사했어요'; setTimeout(function(){ btn.textContent='의견 모아 복사'; },1200); }
    if(navigator.clipboard&&navigator.clipboard.writeText) navigator.clipboard.writeText(text).then(done,done); else done();
  });
  stat();
})();
</script>''' % KEY)
    w('</main></body></html>')
    with open(OUT, 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(o))
    print('saved', OUT, '%.2f MB' % (os.path.getsize(OUT) / 1e6))


if __name__ == '__main__':
    main()
