"""글꼴 비교 HTML (11-05).

촬영: 글꼴마다 KACHI_FONT=<이름> KACHI_FONT_SCALE=<배율> bash tools/unity_batch.sh KachiGun.EditorTools.PlayTest.RunFont --play
      → Kachi_Gun/Logs/PlayTest/font/*.png 를 <src>/<이름>/ 로 옮겨 둠
실행: PYTHONUTF8=1 python tools/font_compare.py <src 폴더>
결과: 글꼴_비교.html + 글꼴_비교_img/ (부분 확대 jpg)
"""
import sys, os, html
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FONTS = [  # (폴더 이름, 보이는 이름, 설명)
    ("MonaS12TextKR", "0. 지금 (모나 12)", "지금 쓰는 픽셀 글꼴 — 획이 1칸이라 얇아 보임"),
    ("Galmuri11-Bold", "1. 갈무리11 굵게", "픽셀 글꼴 그대로 · 획 두 칸 — 도트 그림과 가장 잘 어울림"),
    ("Jua-Regular", "2. 주아", "둥글고 두꺼운 손글씨 — 귀여운 캐주얼 게임 느낌"),
    ("DoHyeon-Regular", "3. 도현", "반듯한 굵은 고딕 — 간판 글씨처럼 깔끔"),
    ("Pretendard-ExtraBold", "4. 프리텐다드 아주 굵게", "앱에서 많이 쓰는 고딕 — 가장 또렷, 게임 느낌은 덜함"),
    ("BlackHanSans-Regular", "5. 검은고딕", "아주 두꺼운 제목용 고딕 — 숫자·제목이 힘 있음"),
]
CROPS = [  # (이름, 파일, 상자 x0,y0,x1,y1 — 1080x2340 기준)
    ("아래 강화 카드 · 메뉴 (실제 크기)", "01_main", (0, 1790, 1080, 2340)),
    ("위 정보 줄 (실제 크기)", "01_main", (0, 0, 1080, 360)),
    ("총알 창 일부", "02_weapon", (0, 300, 1080, 1100)),
    ("미션 창 일부", "03_mission", (0, 400, 1080, 1200)),
    ("상점 창 일부", "04_shop", (0, 400, 1080, 1200)),
    ("설정 창 일부", "05_settings", (0, 500, 1080, 1500)),
]


def main(src):
    out = os.path.join(ROOT, "글꼴_비교_img")
    os.makedirs(out, exist_ok=True)
    have = [f for f in FONTS if os.path.isdir(os.path.join(src, f[0]))]
    rows = []
    for ci, (title, shot, box) in enumerate(CROPS):
        cells = []
        for fi, (key, name, _) in enumerate(have):
            p = os.path.join(src, key, shot + ".png")
            if not os.path.exists(p):
                cells.append(f"<td><div class='miss'>없음</div></td>"); continue
            im = Image.open(p).convert("RGB").crop(box)
            fn = f"c{ci}_{fi}.jpg"
            im.save(os.path.join(out, fn), quality=88)
            cells.append(f"<td><img src='글꼴_비교_img/{fn}' loading='lazy'></td>")
        rows.append(f"<tr><th colspan='{len(have)}' class='sec'>{html.escape(title)}</th></tr><tr>{''.join(cells)}</tr>")
    full = []
    for fi, (key, name, _) in enumerate(have):
        p = os.path.join(src, key, "01_main.png")
        if os.path.exists(p):
            im = Image.open(p).convert("RGB"); im = im.resize((im.width // 2, im.height // 2), Image.LANCZOS)
            fn = f"full_{fi}.jpg"; im.save(os.path.join(out, fn), quality=85)
            full.append(f"<td><img src='글꼴_비교_img/{fn}' loading='lazy'></td>")
    head = "".join(f"<th><b>{html.escape(n)}</b><br><span>{html.escape(d)}</span></th>" for _, n, d in have)
    doc = f"""<!doctype html><html lang="ko"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>글꼴 비교</title><style>
body{{margin:0;padding:16px;background:#2b2520;color:#eee;font-family:'Malgun Gothic',sans-serif}}
h1{{font-size:22px;margin:4px 0 8px}} p.note{{color:#cbbfae;margin:4px 0 14px;line-height:1.6}}
.wrap{{overflow-x:auto}} table{{border-collapse:collapse}}
th,td{{padding:6px;vertical-align:top;border:1px solid #4a3f35}} thead th{{position:sticky;top:0;background:#3a312a;text-align:left;min-width:300px;font-size:15px}}
thead th span{{font-weight:normal;color:#cbbfae;font-size:13px}}
th.sec{{background:#463a30;text-align:left;font-size:14px;color:#ffd99a}}
img{{display:block;width:100%;max-width:540px;min-width:300px}} .miss{{color:#a88;padding:20px}}
</style></head><body>
<h1>글꼴 비교 — 어느 번호로 할까요?</h1>
<p class="note">모두 상업용 무료(OFL) 글꼴입니다. 같은 장면(5챕터 · 같은 세이브)을 글꼴만 바꿔 찍었습니다. 옆으로 밀어서 비교하세요.<br>
고른 번호를 알려 주시면 그 글꼴로 바꾸고 나머지는 지웁니다. 크기를 조금 키우거나 줄이고 싶으면 "2번, 조금 크게"처럼 적어 주세요.</p>
<div class="wrap"><table><thead><tr>{head}</tr></thead><tbody>
{''.join(rows)}
<tr><th colspan='{len(have)}' class='sec'>메인 화면 전체 (반 크기)</th></tr><tr>{''.join(full)}</tr>
</tbody></table></div></body></html>"""
    with open(os.path.join(ROOT, "글꼴_비교.html"), "w", encoding="utf-8") as f:
        f.write(doc)
    print("글꼴_비교.html", len(have), "개 글꼴")


if __name__ == "__main__":
    main(sys.argv[1])
