# -*- coding: utf-8 -*-
"""좀비 움직임 고치기 전·뒤 비교 (11-04)
PlayTest.RunZombie 가 찍은 Logs/PlayTest/zshow(뒤) · zshow_before(전) 에서 좀비 12종을 칸마다 잘라
  - tools/_preview/zombie_<장면>.png : 위 = 전, 아래 = 뒤 (프레임 몇 장을 옆으로)
  - 좀비_움직임_비교.html 용 GIF : tools/_preview/zgif/<장면>_<전|뒤>.gif (12종 4x3 격자, 게임 시간 0.04초 간격)
사용: python tools/zombie_compare.py
"""
import json, os
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
PT = os.path.join(ROOT, 'Kachi_Gun', 'Logs', 'PlayTest')
OUT = os.path.join(ROOT, 'tools', '_preview')
GIF = os.path.join(OUT, 'zgif')
CW, CH = 300, 330          # 좀비 한 칸 (발 기준 위로)
SCENES = {'walk': 30, 'hit': 30, 'stop': 40}


def cells(folder, name, i, pos):
    im = Image.open(os.path.join(PT, folder, f'{name}_{i:02d}.png')).convert('RGB')
    out = []
    for p in pos:
        x, y = int(p['x']), int(p['y'])
        out.append(im.crop((x - CW // 2, y - CH + 40, x + CW // 2, y + 40)))
    return out


def grid(cs, scale=0.5):
    w, h = int(CW * scale), int(CH * scale)
    g = Image.new('RGB', (w * 4, h * 3))
    for k, c in enumerate(cs[:12]):
        g.paste(c.resize((w, h), Image.LANCZOS), ((k % 4) * w, (k // 4) * h))
    return g


def main():
    os.makedirs(GIF, exist_ok=True)
    pos = json.load(open(os.path.join(PT, 'zshow', 'pos.json'), encoding='utf-8'))
    for name, n in SCENES.items():
        for tag, folder in (('전', 'zshow_before'), ('뒤', 'zshow')):
            frames = [grid(cells(folder, name, i, pos)) for i in range(n)]
            frames[0].save(os.path.join(GIF, f'{name}_{tag}.gif'), save_all=True, append_images=frames[1:], duration=40, loop=0)
        # 정지 비교 띠: 좀비 3종(기본·기어 오는·사신) x 6프레임
        pick = [2, 0, 10]
        idx = list(range(0, n, max(1, n // 6)))[:6]
        sw, sh = CW // 2, CH // 2
        sheet = Image.new('RGB', (sw * len(idx), sh * len(pick) * 2 + 10), (30, 30, 30))
        for r, (tag, folder) in enumerate((('전', 'zshow_before'), ('뒤', 'zshow'))):
            for j, i in enumerate(idx):
                cs = cells(folder, name, i, pos)
                for q, z in enumerate(pick):
                    sheet.paste(cs[z].resize((sw, sh), Image.LANCZOS), (j * sw, (r * len(pick) + q) * sh + r * 10))
        sheet.save(os.path.join(OUT, f'zombie_{name}.png'))
    print('비교 그림', OUT)


if __name__ == '__main__':
    main()
