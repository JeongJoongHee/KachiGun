# -*- coding: utf-8 -*-
"""11-04 강화 카드(공격력·공격속도·금전) 아래 녹색 금액 칸이 너무 쨍함 → 녹색만 채도·밝기를 낮춤
사용: python tools/card_green.py
 - 원본은 처음 한 번 tools/_build/card_green_orig/ 에 복사해 두고, 늘 원본에서 다시 만듦 (여러 번 돌려도 같은 결과)
 - coin_icon.py 를 다시 돌리면(카드 금화 다시 그림) 이 스크립트도 다시 돌릴 것 (원본 복사본을 지우고)
 - 밝은 녹색(금액 칸)은 크게, 어두운 녹색(잎)은 조금만 낮춤
 - 미리보기: tools/_preview/card_green.png (위 = 전, 아래 = 뒤)
"""
import os, shutil
import cv2, numpy as np

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
UI = os.path.join(ROOT, 'Kachi_Gun', 'Assets', 'Resources', 'Art', 'UI')
ORIG = os.path.join(ROOT, 'tools', '_build', 'card_green_orig')
PREV = os.path.join(ROOT, 'tools', '_preview', 'card_green.png')
FILES = ['card_atk.png', 'card_spd.png', 'card_gold.png']

SAT_BRIGHT, VAL_BRIGHT = 0.72, 0.78   # 금액 칸 (밝은 녹색)
SAT_DARK, VAL_DARK = 0.85, 0.97       # 잎 (어두운 녹색)


def mute(im):
    bgr, a = im[:, :, :3], im[:, :, 3]
    hsv = cv2.cvtColor(bgr, cv2.COLOR_BGR2HSV).astype(np.float32)
    h, s, v = hsv[:, :, 0], hsv[:, :, 1], hsv[:, :, 2]
    green = (h >= 30) & (h <= 85) & (s > 50) & (a > 0)
    t = np.clip((v - 120) / 80, 0, 1)                      # 0 = 어두운 녹색 · 1 = 밝은 녹색
    sk = SAT_DARK + (SAT_BRIGHT - SAT_DARK) * t
    vk = VAL_DARK + (VAL_BRIGHT - VAL_DARK) * t
    hsv[:, :, 1] = np.where(green, s * sk, s)
    hsv[:, :, 2] = np.where(green, v * vk, v)
    out = im.copy()
    out[:, :, :3] = cv2.cvtColor(np.clip(hsv, 0, 255).astype(np.uint8), cv2.COLOR_HSV2BGR)
    return out


def main():
    os.makedirs(ORIG, exist_ok=True)
    before, after = [], []
    for f in FILES:
        src = os.path.join(ORIG, f)
        if not os.path.exists(src): shutil.copy2(os.path.join(UI, f), src)
        im = cv2.imread(src, cv2.IMREAD_UNCHANGED)
        out = mute(im)
        cv2.imwrite(os.path.join(UI, f), out)
        before.append(im); after.append(out)
        print(f, '완료')
    def row(ims):
        bg = np.full((ims[0].shape[0], sum(i.shape[1] for i in ims), 3), (40, 70, 100), np.uint8)
        x = 0
        for i in ims:
            al = i[:, :, 3:4] / 255.0
            bg[:, x:x + i.shape[1]] = (i[:, :, :3] * al + bg[:, x:x + i.shape[1]] * (1 - al)).astype(np.uint8); x += i.shape[1]
        return bg
    os.makedirs(os.path.dirname(PREV), exist_ok=True)
    cv2.imwrite(PREV, np.vstack([row(before), row(after)]))
    print('미리보기', PREV)


if __name__ == '__main__':
    main()
