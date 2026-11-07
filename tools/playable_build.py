# -*- coding: utf-8 -*-
"""플레이어블 광고(HTML5) 한 파일로 만들기 (11-08)

  PYTHONUTF8=1 python tools/playable_build.py
  틀 = tools/playable_template.html (게임 코드) · 그림 = 게임 그림(Resources/Art)을 줄여 WebP 로 · 글꼴 = 주아에서 쓰는 글자만
  결과 = 광고 소재/플레이어블/
    카피바라vs좀비_플레이어블.html        기본 (메타 · 브라우저 미리보기 — 메타는 FbPlayableAd 를 알아서 씀)
    카피바라vs좀비_플레이어블_MRAID.html  mraid.js 를 부르는 판 (유니티 애즈 · 앱러빈 · 아이언소스 · 민티그럴 등)
    카피바라vs좀비_플레이어블_구글광고.zip  index.html + 구글 ExitApi (구글 애즈 HTML5 — 올리기 전 구글 검사 도구로 확인)
  시험: 브라우저로 열기 · 주소 끝에 ?jump=초 를 붙이면 그만큼 자동 진행한 화면부터
"""
import base64, io, os, zipfile
from PIL import Image

TOOLS = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(TOOLS)
ART = os.path.join(ROOT, 'Kachi_Gun', 'Assets', 'Resources', 'Art')
FONT = os.path.join(ROOT, 'Kachi_Gun', 'Assets', 'Resources', 'Font', 'Jua-Regular.ttf')
ICON = os.path.join(ROOT, 'Kachi_Gun', 'Assets', 'Art', 'AppIcon', 'app_icon.png')
KIT = os.path.join(ROOT, '광고 소재', 'AI 제작 요청', '자료', '브랜드')
OUT = os.path.join(ROOT, '광고 소재', '플레이어블')
SKIN = 17   # 해적 선장


def webp(im, q=86):
    b = io.BytesIO()
    im.save(b, 'WEBP', quality=q, method=6)
    return 'data:image/webp;base64,' + base64.b64encode(b.getvalue()).decode()


def fit(im, w=None, h=None):
    if w and not h: h = round(im.height * w / im.width)
    if h and not w: w = round(im.width * h / im.height)
    return im.resize((w, h), Image.LANCZOS)


def capy(cell, gun):
    sk = Image.open(os.path.join(ART, 'Char', f'skin_{SKIN:02d}.png')).convert('RGBA')
    c = sk.crop(((cell % 4) * 360, (cell // 4) * 360, (cell % 4 + 1) * 360, (cell // 4 + 1) * 360))
    if gun:
        g = Image.open(os.path.join(ART, 'Char', f'gun_{SKIN:02d}.png')).convert('RGBA')
        c.alpha_composite(g, (171 - 90, 253 - 55))   # 총 회전 중심(171,253) = 총 그림 (90,55)
    return fit(c, 180)


def zsheet(name, sc):
    z = Image.open(os.path.join(ART, 'Zombie', f'{name}.png')).convert('RGBA')
    fr = [z.crop((i * 400, 0, i * 400 + 400, z.height)) for i in (0, 1, 2, 3, 5)]   # 걷기 4 + 쓰러짐
    w, h = int(400 * sc), int(z.height * sc)
    out = Image.new('RGBA', (w * 5, h), (0, 0, 0, 0))
    for i, f in enumerate(fr): out.alpha_composite(f.resize((w, h), Image.LANCZOS), (i * w, 0))
    return out


def assets():
    A = {}
    m = Image.open(os.path.join(ART, 'Map', 'side_map.png')).convert('RGB')
    A['map'] = webp(m.resize((678, 960), Image.LANCZOS), 80)
    A['capy_idle'] = webp(capy(0, True), 90)
    A['capy_shoot'] = webp(capy(4, True), 90)
    A['muzzle'] = webp(fit(Image.open(os.path.join(ART, 'Char', 'muzzle_flash.png')).convert('RGBA'), 108), 90)
    for z in ('z_basic', 'z_hat', 'z_bucket', 'z_fat'): A[z] = webp(zsheet(z, 0.5), 86)
    A['z_ogre'] = webp(zsheet('z_ogre', 0.95), 86)
    for i in range(3): A[f'boom{i}'] = webp(fit(Image.open(os.path.join(ART, 'Gen', f'boom_{i}.png')).convert('RGBA'), 200), 86)
    A['bullet'] = webp(fit(Image.open(os.path.join(ART, 'Bullet', 'bullet_basic.png')).convert('RGBA'), 80), 90)
    A['coin'] = webp(fit(Image.open(os.path.join(ART, 'UI', 'icon_money.png')).convert('RGBA'), 80), 90)
    A['icon_single'] = webp(fit(Image.open(os.path.join(ART, 'Bullet', 'icon_single.png')).convert('RGBA'), 184), 90)
    A['icon_laser'] = webp(fit(Image.open(os.path.join(ART, 'Bullet', 'icon_laser.png')).convert('RGBA'), 184), 90)
    for k in ('atk', 'spd', 'gold'): A[f'card_{k}'] = webp(fit(Image.open(os.path.join(ART, 'UI', f'card_{k}.png')).convert('RGBA'), 340), 88)
    A['arrow'] = webp(fit(Image.open(os.path.join(ART, 'Gen', 'arrow.png')).convert('RGBA'), 136), 90)
    A['icon'] = webp(fit(Image.open(ICON).convert('RGBA'), 320), 90)
    A['logo'] = webp(fit(Image.open(os.path.join(KIT, '제목_로고_투명.png')).convert('RGBA'), 800), 88)
    A['kv'] = webp(fit(Image.open(os.path.join(KIT, '대표_캐릭터_투명.png')).convert('RGBA'), 840), 86)
    return A


def font_subset(text):
    from fontTools import subset
    from fontTools.ttLib import TTFont
    chars = set(text) | set('0123456789.,!?%+-/:()kmbtLvSTAGECLR abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ')
    f = TTFont(FONT)
    opt = subset.Options(); opt.flavor = 'woff2'; opt.layout_features = ['*']
    s = subset.Subsetter(opt); s.populate(text=''.join(sorted(c for c in chars if c.strip() or c == ' '))); s.subset(f)
    b = io.BytesIO(); f.flavor = 'woff2'; f.save(b)
    return base64.b64encode(b.getvalue()).decode()


def main():
    os.makedirs(OUT, exist_ok=True)
    tpl = open(os.path.join(TOOLS, 'playable_template.html'), encoding='utf-8').read()
    A = assets()
    js_assets = '{' + ','.join(f'"{k}":"{v}"' for k, v in A.items()) + '}'
    korean = ''.join(ch for ch in tpl if ord(ch) > 127)
    fnt = font_subset(korean)
    html = tpl.replace('/*ASSETS*/{}', js_assets).replace('/*FONT*/', fnt)
    outs = {
        '카피바라vs좀비_플레이어블.html': html,
        '카피바라vs좀비_플레이어블_MRAID.html': html.replace('<!--HEAD_EXTRA-->', '<script src="mraid.js"></script>'),
    }
    for name, h in outs.items():
        open(os.path.join(OUT, name), 'w', encoding='utf-8', newline='\n').write(h)
        print(f'{name}  {os.path.getsize(os.path.join(OUT, name)) / 1e6:.2f}MB')
    g = html.replace('<!--HEAD_EXTRA-->', '<meta name="ad.size" content="width=320,height=480">\n'
                     '<script type="text/javascript" src="https://tpc.googlesyndication.com/pagead/gadgets/html5/api/exitapi.js"></script>')
    zp = os.path.join(OUT, '카피바라vs좀비_플레이어블_구글광고.zip')
    with zipfile.ZipFile(zp, 'w', zipfile.ZIP_DEFLATED) as z: z.writestr('index.html', g)
    print(f'{os.path.basename(zp)}  {os.path.getsize(zp) / 1e6:.2f}MB')
    print('그림', len(A), '· 글꼴', round(len(fnt) * 3 / 4 / 1024), 'KB')


if __name__ == '__main__':
    main()
