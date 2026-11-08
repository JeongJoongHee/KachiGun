# -*- coding: utf-8 -*-
"""성능 측정 전·뒤 비교 HTML (11-08).

PlayTest.RunPerf 가 남긴 Kachi_Gun/Logs/PlayTest/perf/<이름표>.json 두 개를 구간별로 나란히.
사용: PYTHONUTF8=1 python tools/perf_report.py [before] [after] [출력.html]   (기본 before · after · 성능_점검.html)
"""
import json, os, sys, html

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
PERF = os.path.join(ROOT, 'Kachi_Gun', 'Logs', 'PlayTest', 'perf')

SEG = {'stage': '스테이지 사냥', 'menus': '창 4개 열고 닫기', 'challenge': '도전 던전', 'stage2': '스테이지 (도전 뒤)',
       'mine': '비밀던전', 'stage3': '스테이지 (비밀던전 뒤)', 'bossraid': '보스 던전'}


def load(name):
    with open(os.path.join(PERF, name + '.json'), encoding='utf-8') as f:
        return json.load(f)


def agg(rows, seg, first):
    rs = [r for r in rows if r['seg'] == seg and ((r['cycle'] == 1) == first)]
    if not rs:
        return None
    n = sum(r['frames'] for r in rs)
    return dict(
        code=sum(r['avgMs'] * r['frames'] for r in rs) / n,
        codeMax=max(r['maxMs'] for r in rs),
        loopMax=max(r['loopMax'] for r in rs),
        kb=sum(r['scriptKB'] * r['frames'] for r in rs) / n,
        ui=sum(r['uiMs'] * r['frames'] for r in rs) / n,
        objs=max(r['objects'] for r in rs),
        mono=max(r['monoMB'] for r in rs),
    )


def cell(b, a, key, fmt, lower=True):
    if b is None or a is None:
        return '<td>-</td><td>-</td><td></td>'
    vb, va = b[key], a[key]
    if vb > 0:
        pct = (va - vb) / vb * 100
        good = pct < -5 if lower else pct > 5
        bad = pct > 15 if lower else pct < -15
        cls = 'good' if good else 'bad' if bad else ''
        ch = f'<span class="{cls}">{pct:+.0f}%</span>'
    else:
        ch = ''
    return f'<td>{fmt.format(vb)}</td><td><b>{fmt.format(va)}</b></td><td>{ch}</td>'


def main():
    bn = sys.argv[1] if len(sys.argv) > 1 else 'before'
    an = sys.argv[2] if len(sys.argv) > 2 else 'after'
    out = sys.argv[3] if len(sys.argv) > 3 else os.path.join(ROOT, '성능_점검.html')
    B, A = load(bn), load(an)
    rows = []
    for first in (True, False):
        title = '처음 들어갈 때 (1바퀴)' if first else '두 번째부터 (2·3바퀴 평균)'
        rows.append(f'<tr class="grp"><td colspan="16">{title}</td></tr>')
        for seg, label in SEG.items():
            b, a = agg(B, seg, first), agg(A, seg, first)
            if b is None and a is None:
                continue
            rows.append('<tr><td class="l">' + html.escape(label) + '</td>'
                        + cell(b, a, 'loopMax', '{:.0f}')
                        + cell(b, a, 'codeMax', '{:.1f}')
                        + cell(b, a, 'code', '{:.2f}')
                        + cell(b, a, 'kb', '{:.2f}')
                        + cell(b, a, 'objs', '{:.0f}')
                        + '</tr>')
    body = '\n'.join(rows)
    doc = f'''<!doctype html><html lang="ko"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>성능 점검</title><style>
:root{{--bg:#fbf7ef;--fg:#3b281c;--mut:#7a6a5c;--line:#e3d8c6;--good:#3f7a3a;--bad:#a8432f;--card:#fff}}
body{{background:var(--bg);color:var(--fg);font-family:"Malgun Gothic",sans-serif;margin:0;padding:24px 16px;line-height:1.55}}
main{{max-width:1100px;margin:auto}} h1{{font-size:24px;margin:0 0 4px}} h2{{font-size:18px;margin:28px 0 8px}}
.mut{{color:var(--mut);font-size:14px}} .box{{background:var(--card);border:1px solid var(--line);border-radius:10px;padding:14px 18px;margin:12px 0}}
.wrap{{overflow-x:auto}} table{{border-collapse:collapse;width:100%;font-size:14px;background:var(--card)}}
th,td{{border-bottom:1px solid var(--line);padding:6px 8px;text-align:right;white-space:nowrap}} th{{background:#f1e8d8;font-weight:600}}
td.l{{text-align:left}} tr.grp td{{text-align:left;font-weight:700;background:#f7f0e3}}
.good{{color:var(--good);font-weight:700}} .bad{{color:var(--bad);font-weight:700}}
li{{margin:4px 0}} code{{background:#f1e8d8;padding:1px 5px;border-radius:4px}}
</style></head><body><main>
<h1>성능 점검 — 화면이 밀리는 느낌 (11-08)</h1>
<p class="mut">자동 측정(PlayTest.RunPerf): 51스테이지 · 레이저/미사일/산탄총 · 한 바퀴 = 스테이지 사냥 → 창 4개 → 도전 → 스테이지 → 비밀던전 → 스테이지 → 보스 던전, 3바퀴.
PC 에서 잰 값이라 폰은 몇 배 느립니다 — <b>전·뒤 차이</b>와 <b>멈칫하는 순간(최악)</b>을 보세요.</p>
<div class="box"><b>표 읽는 법</b><ul>
<li><b>최악 프레임 ms</b> = 그 구간에서 가장 오래 걸린 한 프레임(화면 그리기 포함). 60프레임이면 한 프레임 16.7ms — 이걸 넘으면 그만큼 멈칫. PC 에디터는 화면 그리기 때문에 평소에도 20ms 안팎.</li>
<li><b>게임 코드 최악/평균 ms</b> = 우리 스크립트만 쓴 시간.</li>
<li><b>쓰레기 메모리 KB/프레임</b> = 게임 코드가 한 프레임에 만들고 버리는 메모리. 쌓이면 몇 초마다 메모리 청소로 멈칫.</li>
<li><b>물체 수</b> = 바퀴가 갈수록 늘면 누수. 전·뒤 모두 바퀴마다 늘지 않았습니다.</li></ul></div>
<div class="wrap"><table>
<tr><th class="l" rowspan="2" style="text-align:left">구간</th><th colspan="3">최악 프레임 ms</th><th colspan="3">게임 코드 최악 ms</th><th colspan="3">게임 코드 평균 ms</th><th colspan="3">쓰레기 메모리 KB/프레임</th><th colspan="3">물체 수(최대)</th></tr>
<tr>{"<th>전</th><th>뒤</th><th></th>" * 5}</tr>
{body}
</table></div>
<p class="mut">원본: <code>Kachi_Gun/Logs/PlayTest/perf/{bn}.json</code> · <code>{an}.json</code> · 다시 만들기: <code>PYTHONUTF8=1 python tools/perf_report.py {bn} {an}</code></p>
<!--NOTES-->
</main></body></html>'''
    with open(out, 'w', encoding='utf-8') as f:
        f.write(doc)
    print('→', out)


if __name__ == '__main__':
    main()
