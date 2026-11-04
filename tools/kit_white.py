# -*- coding: utf-8 -*-
"""UI 팩 아이콘의 흰색판 만들기 (i_lock → i_lock_w 와 같은 모양): 진한 테두리는 그대로, 안쪽 갈색만 밝은 회백색
게임에서 Image.color 로 원하는 색을 입혀 씀 (11-04 출석 창의 녹색 체크 = i_check_w)
사용: python tools/kit_white.py i_check [i_x ...]   → Kachi_Gun/Assets/Resources/Art/Kit/<이름>_w.png
"""
import os, sys
import numpy as np
from PIL import Image

KIT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Kachi_Gun', 'Assets', 'Resources', 'Art', 'Kit')

for name in sys.argv[1:]:
    a = np.array(Image.open(os.path.join(KIT, name + '.png')).convert('RGBA')).astype(np.float32)
    lum = a[:, :, :3].mean(2)
    fill = (a[:, :, 3] > 0) & (lum > 55)                       # 테두리(≈40) 빼고 안쪽
    v = np.clip(190 + (lum - 60) * 0.6, 170, 230)              # 안쪽 명암은 살림
    for c in range(3): a[:, :, c] = np.where(fill, v, a[:, :, c])
    out = os.path.join(KIT, name + '_w.png')
    Image.fromarray(a.astype(np.uint8), 'RGBA').save(out)
    print(out)
