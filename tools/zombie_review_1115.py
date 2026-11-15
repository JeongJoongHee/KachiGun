# -*- coding: utf-8 -*-
"""11-15 판타지 몬스터 좀비 15종 확인 문서: 좀비_추가_1115.html (+ 좀비_추가_1115_그림/)
  그림 = 게임 시트 걷기 칸 + 색 변형 3가지(doodle_art.tint_skin = 게임 셰이더와 같은 식) · 뼈대 움직임 GIF(tools/_preview/rig_z_*.png 8장)
  배치 = Zombie_Master.csv · 보스 순환 = Stage_Table.csv  /  체크·의견 키 kachigun-zombie-1115 (python tools/todo_checks.py 좀비_추가_1115.html)
사용: PYTHONUTF8=1 python tools/zombie_review_1115.py
"""
import csv
import html
import os

import numpy as np
from PIL import Image

from doodle_art import tint_skin
from zombie_add_1111 import biome_of
from zombie_add_1115 import NEW, BOSSES

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(ROOT, 'Kachi_Gun', 'Assets', 'Resources', 'Art', 'Zombie')
PREV = os.path.join(ROOT, 'tools', '_preview')
OUT = os.path.join(ROOT, '좀비_추가_1115_그림')
DOC = os.path.join(ROOT, '좀비_추가_1115.html')
TAB = os.path.join(ROOT, 'Kachi_Gun', 'Assets', 'Resources', 'Tables')

KIND = {'skeleton': '걷기', 'vampire': '걷기', 'werewolf': '걷기', 'golem': '걷기', 'cyclops': '걷기', 'yetiz': '걷기', 'minotaur': '걷기',
        'dog': '네발', 'centaur': '네발', 'spider': '떠서 다리 흔들림', 'mimic': '깡총', 'kraken': '떠서 다리 흔들림',
        'beholder': '떠서 촉수 흔들림', 'wyvern': '날개 퍼덕임', 'dragon': '날개 퍼덕임'}
NOTE = {
    'skeleton': '뼈만 남은 몸 · 빈 눈구멍에 작은 빛 · 피부가 없어 색 변형이 안 붙음',
    'dog': '찢어진 귀 · 빨간 목걸이 · 혀 · 갈비뼈',
    'vampire': '높은 깃 망토(안 붉은색) · 넘긴 머리 · 송곳니',
    'werewolf': '늑대 머리(초록 주둥이) · 체크 셔츠 · 맨발 발톱',
    'spider': '보라 몸에 초록 꿰맨 무늬 · 눈 여섯 · 다리 여덟',
    'mimic': '보물 상자 · 이빨 · 긴 초록 혀 · 깡총깡총 · 골드 2배(임시)',
    'golem': '돌덩이 몸 · 초록 이끼 · 빛나는 눈 · 체력 2.4배(임시)',
    'centaur': '말 몸 + 사람 몸통 · 활',
    'yetiz': '하늘빛 흰 털 · 고드름. 원래 보스 "설인 좀비"는 "거대 설인"으로 이름만 바꿈',
    'cyclops': '이마에 큰 외눈 · 몽둥이 · 크기 1.2배',
    'kraken': '큰 머리 · 다리 여섯(빨판) · 앞으로 뻗은 다리',
    'beholder': '몸 자체가 큰 눈알 + 초록 살 눈꺼풀 + 촉수. ⚠ 이름 확인 필요 (아래)',
    'minotaur': '소 머리 · 코뚜레 · 양날 도끼',
    'wyvern': '보라 날개 · 두 다리 · 긴 꼬리 · 날개 퍼덕임',
    'dragon': '붉은 날개 · 뿔 두 쌍 · 등 가시 · 앞발 · 입가 연기',
}


def cell(zid):
    return Image.open(os.path.join(ART, f'z_{zid}.png')).convert('RGBA').crop((0, 0, 400, 380))


def tint_strip(zid):
    a = cell(zid)
    arr = np.array(a)
    tiles = [a] + [Image.fromarray(tint_skin(arr, t)) for t in ('red', 'blue', 'purple')]
    sh = Image.new('RGBA', (400 + 3 * 200, 380), (246, 238, 222, 255))
    sh.alpha_composite(tiles[0], (0, 0))
    for i, t in enumerate(tiles[1:]):
        sh.alpha_composite(t.resize((200, 190), Image.LANCZOS), (400 + i * 200, 95))
    return sh.convert('RGB')


def walk_gif(zid):
    p = os.path.join(PREV, f'rig_z_{zid}.png')
    if not os.path.exists(p):
        return None
    im = Image.open(p).convert('RGB')
    fr = [im.crop((400 * k, 0, 400 * (k + 1), 380)).resize((240, 228), Image.LANCZOS) for k in range(1, 9)]
    bg = (89, 86, 120)
    fr = [f.quantize(colors=128) for f in fr]
    out = os.path.join(OUT, f'{zid}_walk.gif')
    fr[0].save(out, save_all=True, append_images=fr[1:], duration=110, loop=0)
    return out


def main():
    os.makedirs(OUT, exist_ok=True)
    zm = {r['Id']: r for r in csv.DictReader(open(os.path.join(TAB, 'Zombie_Master.csv'), encoding='utf-8-sig'))}
    bossCh = {}
    for r in csv.DictReader(open(os.path.join(TAB, 'Stage_Table.csv'), encoding='utf-8-sig')):
        if r['Is_Boss_Stage'] == 'Y':
            bossCh.setdefault(r['Boss_Id'], []).append(int(r['Chapter']))
    items = []
    for z in NEW:
        items.append((z[0], z[1], False))
    for b in BOSSES:
        items.append((b[0], b[1], True))
    cards = []
    for zid, name, boss in items:
        tint_strip(zid).save(os.path.join(OUT, f'{zid}.jpg'), quality=86)
        g = walk_gif(zid)
        r = zm[zid]
        if boss:
            chs = bossCh.get(zid, [])
            where = f'보스 · 챕터 {", ".join(map(str, chs[:4]))} … (9챕터마다)'
        else:
            ch = int(r['MinChapter'])
            where = f'챕터 <b>{ch}</b>부터 ({biome_of(ch)}{" · 이 배경에서 더 자주" if r["Biome"] else ""})'
        stats = '' if boss else f' · {r["Role"]} · 체력 x{r["HpMul"]} · 골드 x{r["GoldMul"]} · 빠르기 {r["Speed"]} · 크기 {r["Scale"]}'
        gif = f'<img class="walk" src="좀비_추가_1115_그림/{zid}_walk.gif" alt="{html.escape(name)} 움직임">' if g else ''
        cards.append(f'''<li data-id="z-{zid}"><label><input type="checkbox"><span class="t">{html.escape(name)} <span class="id">{zid}</span></span></label>
<div class="pics"><img src="좀비_추가_1115_그림/{zid}.jpg" alt="{html.escape(name)} (원래 · 붉은 · 푸르딩딩 · 보라)">{gif}</div>
<div class="meta">{where}{stats} · 움직임: {KIND[zid]}</div>
<div class="meta">{html.escape(NOTE[zid])}</div></li>''')
    page = TEMPLATE.replace('__CARDS__', '\n'.join(cards))
    open(DOC, 'w', encoding='utf-8').write(page)
    print('ok', len(cards), DOC)


TEMPLATE = '''<!doctype html>
<html lang="ko"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>판타지 좀비 15종</title><style>
:root{--bg:#fbf7ef;--fg:#3b281c;--mut:#7a6a5c;--line:#e3d8c6;--card:#fff;--acc:#a8642f}
*{box-sizing:border-box}
body{background:var(--bg);color:var(--fg);font-family:"Malgun Gothic",sans-serif;margin:0;padding:24px 16px;line-height:1.6}
main{max-width:980px;margin:auto} h1{font-size:24px;margin:0 0 4px} h2{font-size:19px;margin:30px 0 8px;color:var(--acc)}
.mut{color:var(--mut);font-size:14px} code{background:#f1e8d8;padding:1px 5px;border-radius:4px;font-size:13px}
.box{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:10px 16px;margin:12px 0;font-size:14.5px}
.box.warn{background:#fff6e6;border-color:#f0cf96}
ul.todo{list-style:none;padding:0;margin:0}
ul.todo>li{background:var(--card);border:1px solid var(--line);border-radius:14px;padding:12px 14px;margin:12px 0}
ul.todo>li.checked{border-color:#9cc47e;background:#f6fbef}
.row{display:flex;align-items:flex-start;gap:10px}
.row label{flex:1;display:flex;gap:10px;align-items:flex-start;cursor:pointer}
.row input{width:22px;height:22px;margin-top:4px;flex:none;accent-color:#6aa84f}
.t{font-weight:700;font-size:17px} .id{font-weight:400;font-size:12px;color:var(--mut);font-family:Consolas,monospace}
.pics{display:flex;gap:10px;flex-wrap:wrap;align-items:center;margin:8px 0}
.pics img{max-width:100%;border-radius:10px;border:1px solid var(--line)} .pics img.walk{width:180px}
.meta{font-size:14px;color:var(--fg)}
.pen{border:1px solid var(--line);background:#faf5ea;border-radius:8px;font-size:13px;padding:2px 8px;cursor:pointer;color:var(--fg);flex:none}
li.has-note .pen{background:#fde9c8}
.notebox{display:none;margin-top:8px} li.open .notebox{display:block}
.notebox textarea{width:100%;min-height:60px;border:1px solid var(--line);border-radius:8px;padding:6px 8px;font:inherit;font-size:14px}
.hint{font-size:12px;color:var(--mut)}
table{border-collapse:collapse;width:100%;font-size:13.5px;margin:6px 0}
th,td{border:1px solid var(--line);padding:4px 8px;text-align:left} th{background:#f4ecdf}
#bar{position:sticky;top:0;background:var(--bg);padding:8px 0;z-index:2;border-bottom:1px solid var(--line)}
</style></head><body><main>
<h1>판타지 몬스터 좀비 15종 (일반 12 · 보스 3)</h1>
<div class="mut">2026-11-15 · 그림 = 게임 그림 그대로(왼쪽 큰 그림 = 원래 초록, 오른쪽 작은 셋 = 챕터 색 붉은·푸르딩딩·보라) · 움직임 = 뼈대 미리보기 · 체크 = "이대로 좋음", ✎ 의견 = 고칠 점</div>
<div id="bar"><span id="stat" class="mut"></span></div>

<div class="box warn"><b>먼저 알려 드릴 것 / 제 해석</b>
<ol>
<li><b>배치</b>: 11-11 에 정하신 "1~150 고르게 · 간단한 디자인은 앞 · 퀄 높은 디자인은 뒤" 규칙을 이어서, 이미 새 좀비가 들어간 챕터 6~56 다음 비어 있는 <b>챕터 60~148 에 8챕터마다 한 종</b>씩 넣었습니다. 처음 나오는 챕터에선 꼭 나오고 그 뒤 5챕터 동안 자주 나옵니다(예전 규칙 그대로). 지금 플레이어들이 초반이라 <b>당분간 직접 보기 어렵습니다</b> — 더 일찍 보이게 하려면 의견 칸에 "앞으로" 라고 적어 주세요 (예: 비어 있는 홀수 챕터 7~29).</li>
<li><b>보스 3종</b>은 보스 순환에 넣었습니다: 오우거 → 하키 → 파라오 → 거대 설인 → 선장 → 방독면 → <b>미노타우르스(7) → 와이번(8) → 용(9)</b> → 다시 오우거. 그래서 <b>챕터 7부터 나오는 보스 순서가 바뀝니다</b>(보스 던전도 같은 순서).</li>
<li><b>설인 좀비</b>는 이미 보스로 있어서, 새 일반 좀비를 "설인 좀비"(털 덥수룩)로 하고 원래 보스 이름을 <b>"거대 설인"</b>으로 바꿨습니다.</li>
<li><b>⚠ 비홀더</b>: "비홀더(Beholder)"는 D&amp;D 회사가 자기 고유 몬스터로 지키는 이름·모양입니다(일반 판타지 몬스터와 달리 공개 규칙에서도 빠져 있음). 그래서 모양은 "몸 자체가 커다란 눈알 + 초록 살 눈꺼풀 + 촉수"로 따로 그렸고(눈 달린 촉수 없음), 표 이름은 말씀하신 "비홀더 좀비" 그대로 두었습니다. 스토어에 내는 게임이라 <b>"눈알 좀비" 같은 이름으로 바꾸시길 추천</b>합니다 — 의견 칸에 원하는 이름을 적어 주세요. (처음 그린 초록 둥근 몸 + 외눈 모양은 유명 애니메이션 캐릭터와 닮아서 바꿨습니다)</li>
<li>체력·골드·빠르기·나올 비중은 <b>임시값</b>입니다 (밸런스는 마지막에).</li>
</ol></div>

<ul class="todo">
__CARDS__
</ul>

<h2>도구</h2>
<div class="mut"><code>tools/doodle_zombies3.py</code> (그림, PowerShell 에서 <code>python tools/doodle_art.py znew3</code>) · <code>tools/rig_gen.py</code> (뼈대) · <code>tools/zombie_add_1115.py</code> (표: 챕터·보스 순환·이름) · <code>tools/zombie_review_1115.py</code> (이 문서)</div>

<script>
(function(){
  // 저장 형식: {"c":{항목id:true}, "n":{항목id:"의견"}} — tools/todo_checks.py 좀비_추가_1115.html 로 읽음
  var KEY='kachigun-zombie-1115', st={c:{},n:{}};
  try{ var v=JSON.parse(localStorage.getItem(KEY)||'{}'); st.c=v.c||{}; st.n=v.n||{}; }catch(e){}
  var items=document.querySelectorAll('ul.todo > li[data-id]');
  function save(){ try{ localStorage.setItem(KEY,JSON.stringify(st)); }catch(e){} stat(); }
  function stat(){ var c=0,n=0; items.forEach(function(li){ if(st.c[li.dataset.id]) c++; if((st.n[li.dataset.id]||'').trim()) n++; });
    document.getElementById('stat').textContent='체크 '+c+' / '+items.length+' · 의견 '+n+'  (이 브라우저에 저장 → "체크했어" 라고 알려 주세요)'; }
  items.forEach(function(li){
    var id=li.dataset.id, label=li.querySelector('label'), cb=li.querySelector('input');
    var row=document.createElement('div'); row.className='row'; li.insertBefore(row,label); row.appendChild(label);
    var pen=document.createElement('button'); pen.type='button'; pen.className='pen'; pen.textContent='✎ 의견'; row.appendChild(pen);
    var box=document.createElement('div'); box.className='notebox';
    var ta=document.createElement('textarea'); ta.placeholder='고칠 점 (예: 더 무섭게 · 색 · 이름 · 더 앞 챕터에)';
    var hint=document.createElement('div'); hint.className='hint'; hint.textContent='적는 대로 이 브라우저에 저장됩니다.';
    box.appendChild(ta); box.appendChild(hint); li.appendChild(box);
    cb.checked=!!st.c[id]; ta.value=st.n[id]||''; li.classList.toggle('checked',cb.checked);
    function mark(){ li.classList.toggle('has-note',!!ta.value.trim()); pen.textContent=ta.value.trim()?'✎ 의견 있음':'✎ 의견'; }
    mark(); if(ta.value.trim()) li.classList.add('open');
    cb.addEventListener('change',function(){ st.c[id]=cb.checked; li.classList.toggle('checked',cb.checked); save(); });
    ta.addEventListener('input',function(){ if(ta.value.trim()) st.n[id]=ta.value; else delete st.n[id]; mark(); save(); });
    pen.addEventListener('click',function(){ li.classList.toggle('open'); if(li.classList.contains('open')) ta.focus(); });
  });
  stat();
})();
</script>
</main></body></html>
'''

if __name__ == '__main__':
    main()
