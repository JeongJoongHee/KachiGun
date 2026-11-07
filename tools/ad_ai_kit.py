# -*- coding: utf-8 -*-
"""다른 AI(영상·이미지 생성)에 광고 제작을 맡길 때 붙일 자료 (11-07)

  PYTHONUTF8=1 python tools/ad_ai_kit.py     (먼저 PlayTest.RunAd 촬영 · store 촬영이 있어야 함)
  결과 = 광고 소재/AI 제작 요청/자료/
    캐릭터/      카피바라 코스튬 18 · 좀비 12 — 투명 PNG (게임 그림 그대로)
    브랜드/      앱 아이콘 512 · 제목 로고(투명) · 색 견본 · 대표 캐릭터 한 장
    게임_화면/   스토어용 실제 화면 8장(1080x2340) · 광고 장면 대표 프레임(1080x1920)
    실제_플레이_클립/  장면별 실제 플레이 mp4 (문구 없음, 1080x1920 30fps) — AI 편집기에 그대로 넣을 수 있게
  요청서·프롬프트 = 광고 소재/AI 제작 요청/AI_제작_요청서.html (손으로 씀)
"""
import os, shutil, subprocess
from PIL import Image, ImageDraw
import imageio_ffmpeg
import ad_video as V

ROOT = V.ROOT
KIT = os.path.join(ROOT, '광고 소재', 'AI 제작 요청', '자료')
ART = V.ART
SKINS = ['기본 카피', '밀짚모자', '천사 날개', '새싹', '꿀꿀 돼지', '악어 잠옷', '원시인', '검은 두건', '그림자 망토', '공룡 잠옷',
         '모험가 배낭', '박쥐 날개', '멜빵바지', '꼬꼬 잠옷', '빨간 망토', '상어 잠옷', '줄무늬 고양이', '해적 선장']
ZOMBIES = [('crawler', '기어오는 좀비'), ('knife', '칼 좀비'), ('basic', '좀비'), ('hat', '모자 좀비'), ('bucket', '양동이 좀비'), ('armor', '갑옷 좀비'),
           ('bat', '방망이 좀비'), ('farmer', '농부 좀비'), ('mummy', '미라'), ('fat', '뚱보 좀비'), ('reaper', '사신 좀비'), ('ogre', '변이 오우거(보스)')]
PALETTE = [('선, 글자 테두리', '#1C110A'), ('진한 갈색(UI 글자)', '#281A12'), ('제목 금색', '#FFCD50'), ('크림', '#FFF4D6'),
           ('좀비 녹색', '#96E678'), ('버튼 녹색', '#6EBE46'), ('배경 살구', '#FFE8C0'), ('배경 주황', '#E1A452'), ('흙길', '#E8D2A8'), ('숲 녹색', '#7FB45A')]


def trim(im):
    return im.crop(im.getbbox()) if im.getbbox() else im


def characters():
    d = os.path.join(KIT, '캐릭터'); os.makedirs(d, exist_ok=True)
    for i, nm in enumerate(SKINS):
        sk = Image.open(os.path.join(ART, 'Char', f'skin_{i:02d}.png')).convert('RGBA')
        c = 8
        cell = trim(sk.crop(((c % 4) * 360, (c // 4) * 360, (c % 4 + 1) * 360, (c // 4 + 1) * 360)))
        cell = cell.resize((cell.width * 2, cell.height * 2), Image.LANCZOS)
        cell.save(os.path.join(d, f'카피바라_{i:02d}_{nm}.png'))
    for zid, nm in ZOMBIES:
        z = Image.open(os.path.join(ART, 'Zombie', f'z_{zid}.png')).convert('RGBA')
        f = trim(z.crop((0, 0, 400, z.height))).transpose(Image.FLIP_LEFT_RIGHT)   # 왼쪽(카피바라 쪽)을 보게
        f.save(os.path.join(d, f'좀비_{zid}_{nm}.png'))
    print('캐릭터', len(SKINS) + len(ZOMBIES))


def brand():
    d = os.path.join(KIT, '브랜드'); os.makedirs(d, exist_ok=True)
    shutil.copy(V.ICON, os.path.join(d, '앱아이콘_512.png'))
    # 제목 로고 (투명)
    W, H = 1400, 560
    lg = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    dr = ImageDraw.Draw(lg)
    V.text_outline(dr, (W // 2, 170), '카피바라', V.font(190), V.GOLD, w=12)
    V.text_outline(dr, (W // 2, 380), 'vs 좀비', V.font(190), V.GREEN, w=12)
    V.text_outline(dr, (W // 2, 510), '방치형 분대 슈팅', V.font(70), V.CREAM, w=7)
    trim(lg).save(os.path.join(d, '제목_로고_투명.png'))
    # 색 견본
    cw, ch = 220, 200
    pal = Image.new('RGB', (cw * 5, ch * 2 + 10), (255, 255, 255))
    dr = ImageDraw.Draw(pal)
    for i, (nm, hx) in enumerate(PALETTE):
        x, y = (i % 5) * cw, (i // 5) * (ch + 10)
        dr.rectangle([x + 6, y + 6, x + cw - 6, y + ch - 66], fill=hx, outline='#1C110A', width=3)
        dr.text((x + cw // 2, y + ch - 46), hx, font=V.font(30), fill='#1C110A', anchor='mm')
        dr.text((x + cw // 2, y + ch - 16), nm, font=V.font(22), fill='#555555', anchor='mm')
    pal.save(os.path.join(d, '색_견본.png'))
    # 대표 캐릭터 한 장 (해적 선장 + 좀비 3)
    cap, zs = V.capy_and_zombies(700)
    kv = Image.new('RGBA', (1500, 820), (0, 0, 0, 0))
    for i, z in enumerate(zs):
        kv.alpha_composite(z, (720 + i * 200, 800 - z.height - (40 if i == 1 else 0)))
    kv.alpha_composite(cap, (680 - cap.width + 80, 800 - cap.height))
    trim(kv).save(os.path.join(d, '대표_캐릭터_투명.png'))
    print('브랜드 4')


def screens():
    d = os.path.join(KIT, '게임_화면'); os.makedirs(d, exist_ok=True)
    store = os.path.join(V.SHOTS, 'store')
    names = {'01_battle': '전투', '02_boss': '보스', '03_weapon': '총알관리_합성', '04_high': '상위총알', '05_supply': '보급품',
             '06_skin': '코스튬', '07_mine': '비밀던전', '08_challenge': '도전던전'}
    for k, nm in names.items():
        p = os.path.join(store, k + '.png')
        if os.path.exists(p): shutil.copy(p, os.path.join(d, f'스토어화면_{k[:2]}_{nm}_1080x2340.png'))
    keys = [('a_horde', 0.25, '좀비떼_훅'), ('b_upgrade', 1.0, '강화'), ('c_merge', 1.62, '합성'), ('d_boss', 1.2, '보스'),
            ('d_boss', 3.4, '보스처치'), ('e_high', 0.4, '레이저미사일'), ('f_mine', 0.8, '비밀던전'), ('g_skin', 1.0, '코스튬')]
    for clip, t, nm in keys:
        V.frame_at(dict(clip=clip, start=0.0, dur=99), t).save(os.path.join(d, f'광고장면_{nm}_1080x1920.jpg'), quality=92)
    print('게임 화면', len(names) + len(keys))


def clips():
    d = os.path.join(KIT, '실제_플레이_클립'); os.makedirs(d, exist_ok=True)
    ff = imageio_ffmpeg.get_ffmpeg_exe()
    names = {'a_horde': '1_좀비떼_쓸어버리기_5초', 'b_upgrade': '2_강화카드_연타_3.5초', 'c_merge': '3_총알합성_4초', 'd_boss': '4_보스_등장_처치_5초',
             'e_high': '5_상위총알_레이저미사일_4.5초', 'f_mine': '6_비밀던전_3.5초', 'g_skin': '7_코스튬창_2초'}
    for clip, nm in names.items():
        src = os.path.join(V.AD, clip, '%04d.jpg')
        out = os.path.join(d, nm + '.mp4')
        subprocess.run([ff, '-y', '-loglevel', 'error', '-framerate', '30', '-i', src, '-c:v', 'libx264', '-crf', '18', '-pix_fmt', 'yuv420p',
                        '-movflags', '+faststart', out], check=True)
    print('클립', len(names))


if __name__ == '__main__':
    characters(); brand(); screens(); clips()
