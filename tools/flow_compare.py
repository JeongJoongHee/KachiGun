# -*- coding: utf-8 -*-
"""11-15 연출 바꾸기 전·뒤 비교: Logs/PlayTest/flow/before · after 의 장면(초당 15장 jpg)을 나란히 붙인 영상 → 연출_비교_그림/*.mp4
  (GIF 는 jpg 잡티 때문에 한 장면 8~15MB 라 반복 재생 mp4 로 — 문서는 <video autoplay loop muted>)
  촬영: KACHI_FLOW_LABEL=before|after bash tools/unity_batch.sh KachiGun.EditorTools.PlayTest.RunFlow --play
  사용: PYTHONUTF8=1 python tools/flow_compare.py   (문서 연출_비교.html 은 그림 이름만 씀 — 글은 손으로 고침)
"""
import glob
import os
import sys

from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, 'Kachi_Gun', 'Logs', 'PlayTest', 'flow')
OUT = os.path.join(ROOT, '연출_비교_그림')
CLIPS = ['a_battle', 'b_boss', 'c_next']
Y0, Y1 = 0.0, 0.80        # 화면 위 0 ~ 80% (아래 강화 카드·버튼 줄은 뺌)
W = 360                   # 한쪽 폭(px)
FPS = 15
FONT = os.path.join(os.environ.get('WINDIR', r'C:\Windows'), 'Fonts', 'malgunbd.ttf')


def frames(label, clip):
    return sorted(glob.glob(os.path.join(SRC, label, clip, '*.jpg')))


def crop(path):
    im = Image.open(path).convert('RGB')
    w, h = im.size
    im = im.crop((0, int(h * Y0), w, int(h * Y1)))
    return im.resize((W, int(im.height * W / w)), Image.LANCZOS)


def label_bar(text, w):
    bar = Image.new('RGB', (w, 34), (59, 40, 28))
    d = ImageDraw.Draw(bar)
    try:
        f = ImageFont.truetype(FONT, 18)
    except OSError:
        f = ImageFont.load_default()
    tw = d.textlength(text, font=f)
    d.text(((w - tw) / 2, 6), text, fill=(255, 246, 225), font=f)
    return bar


def pair(clip):
    a, b = frames('before', clip), frames('after', clip)
    n = max(len(a), len(b))
    if n == 0:
        return None
    out = []
    la, lb = label_bar('고치기 전', W), label_bar('고친 뒤', W)
    for i in range(n):
        ia = crop(a[min(i, len(a) - 1)]) if a else None
        ib = crop(b[min(i, len(b) - 1)])
        h = ib.height
        im = Image.new('RGB', (W * 2 + 8, h + 34), (251, 247, 239))
        im.paste(la, (0, 0)); im.paste(lb, (W + 8, 0))
        if ia:
            im.paste(ia, (0, 34))
        im.paste(ib, (W + 8, 34))
        out.append(im)
    return out


def main():
    import shutil, subprocess, tempfile
    import imageio_ffmpeg
    ff = imageio_ffmpeg.get_ffmpeg_exe()
    os.makedirs(OUT, exist_ok=True)
    for c in CLIPS:
        fr = pair(c)
        if not fr:
            print('없음', c); continue
        tmp = tempfile.mkdtemp()
        for i, f in enumerate(fr):
            w, h = f.size
            f.crop((0, 0, w - w % 2, h - h % 2)).save(os.path.join(tmp, f'{i:04d}.png'))
        p = os.path.join(OUT, c + '.mp4')
        subprocess.run([ff, '-y', '-loglevel', 'error', '-framerate', str(FPS), '-i', os.path.join(tmp, '%04d.png'),
                        '-c:v', 'libx264', '-pix_fmt', 'yuv420p', '-crf', '26', '-movflags', '+faststart', p], check=True)
        shutil.rmtree(tmp, ignore_errors=True)
        fr[len(fr) // 2].save(os.path.join(OUT, c + '.jpg'), quality=85)   # 영상이 안 나올 때 보이는 그림
        print(c, len(fr), '장', os.path.getsize(p) // 1024, 'KB')
    for g in glob.glob(os.path.join(OUT, '*.gif')):
        os.remove(g)


if __name__ == '__main__':
    sys.exit(main())
