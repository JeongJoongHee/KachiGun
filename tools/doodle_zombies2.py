# -*- coding: utf-8 -*-
"""11-11 좀비 추가 제안(좀비_추가_제안.html) 에서 사용자가 체크한 새 좀비 20종 (일반 17 · 보스 3).

doodle_art.py 의 도우미(walker · P · L · C · E · limb …)로 그림 — 칸 400x380, 오른쪽을 봄, 발바닥 y 376.
규칙: 초록은 피부에만 (색 변형 셰이더가 초록 피부만 돌림) — 독물·연기·지폐는 청록·금색으로.
뼈대 자리 = rig_gen.py ZOMBIE_RIG (같은 이름).

사용: PowerShell 에서  PYTHONUTF8=1 python tools/doodle_art.py znew2 [이름...]
"""
from doodle_art import (P, L, C, E, F, limb, zig, walker, shade, zface, zhead, rr_, sleeve_b, sleeve_f, fist,
                        INK, SKIN, CLOTH_W)

NEW_ZOMBIES2 = ['padding', 'ski', 'scientist', 'patient', 'hazmat', 'tube', 'cowboy', 'biker', 'camper', 'delivery',
                'jiangshi', 'chef', 'student', 'firefighter', 'money', 'drum', 'shield', 'yeti', 'captain', 'gasgiant']
TEAL = '#5fc2b4'        # 독물·연기 (초록 대신)
GOLD = '#e8c05a'


def _dots(pts, r=2.8, fill=INK):
    return ''.join(f'<circle cx="{x}" cy="{y}" r="{r}" fill="{fill}"/>' for x, y in pts)


def padding():
    """롱패딩: 무릎까지 오는 빵빵한 패딩(가로 누빔) · 털모자(방울) · 귀마개"""
    sk, cw = '#90c46e', '#4f6290'
    coat = (P('M150 250 Q160 228 204 226 Q250 228 260 250 Q272 300 262 346 Q204 358 146 346 Q136 300 150 250 Z', cw)
            + L('M146 278 Q204 290 264 278 M144 312 Q204 324 266 312', 4, shade(cw, .75))
            + L('M204 232 L204 350', 3.5, shade(cw, .75))
            + P('M176 232 Q204 252 232 232 Q232 222 204 220 Q176 222 176 232 Z', '#d9d2c0', 4.5))
    hat = (P('M152 150 Q150 100 204 96 Q258 100 256 150 Z', '#c0564a') + P('M148 152 L260 146 L262 162 L146 168 Z', '#e9dfc6', 5)
           + C(204, 90, 13, '#e9dfc6', 5) + L('M170 118 L172 146 M204 104 L204 146 M236 116 L236 146', 3, shade('#c0564a', .8)))
    muff = E(158, 196, 13, 17, '#e9dfc6', 5) + L('M156 180 Q150 160 156 150', 4)
    return walker(skin=sk, shirt=cw, pants='#3e4252', shoes='#5d4030', wide=10,
                  torso_extra=coat, head_extra=hat + muff,
                  hand_item_back=sleeve_b(cw) + E(260, 245, 12, 12, cw, 5),
                  hand_item=limb('M218 266 Q246 264 274 264', 28, cw) + L('M240 254 L242 276 M258 254 L260 276', 3, shade(cw, .75)))


def ski():
    """스키: 고글 · 털모자 · 목도리(뒤로 날림) · 스키 두 짝 · 폴. 다리는 붙임(뼈대 robe = 미끄러져 옴)"""
    sk, jk, pn = '#8cc56a', '#7fa8d0', '#3e4a6e'
    out = [
        L('M300 244 L246 372', 6, INK), L('M300 244 L246 372', 3, '#9aa3ad'), E(250, 362, 9, 3, INK, 3),   # 뒷폴
        limb('M226 247 Q262 244 296 244', 21, shade(jk, .9)), C(300, 244, 10, shade(SKIN, .9)),
        P('M180 296 L178 360 L230 360 L228 296 Z', pn), L('M204 300 L204 358', 3),                    # 붙은 다리
        P('M168 356 Q166 372 180 372 L238 372 Q244 362 234 356 Z', '#c0564a'),                         # 스키 신발
        P('M96 370 L330 370 Q352 370 356 356 Q362 352 362 362 Q358 380 330 380 L96 380 Z', GOLD, 5),     # 스키
        P(f'M168 246 Q202 232 238 246 L242 306 {zig(166, 242, 306, 8, 6)} Z', jk),
        L('M204 244 L204 304', 3.5),
        P('M160 236 Q204 252 246 236 L246 252 Q204 266 160 252 Z', '#c0564a', 5),                       # 목도리
        P('M164 246 Q130 252 104 236 L110 256 Q136 268 168 258 Z', '#c0564a', 5),
        L('M112 242 l-8 -6 M116 252 l-10 2', 3),
        zhead(204, 188, 54, sk),
        P('M150 158 Q148 108 204 102 Q260 108 258 158 Z', '#e9dfc6') + L('M152 140 Q204 128 256 140', 6, '#7fa8d0'),
        C(204, 96, 12, '#7fa8d0', 5),
        P('M160 166 Q204 156 262 162 L262 184 Q204 178 160 188 Z', '#5d5f6e', 5),                       # 고글 띠
        rr_(206, 158, 58, 30, 13, '#f2a65a', 5), L('M216 166 L232 166', 3, '#fff6d8'),
        limb('M218 266 Q262 262 300 264', 23, jk), C(306, 265, 11, sk),
        L('M306 264 L270 374', 6, INK), L('M306 264 L270 374', 3, '#9aa3ad'), E(272, 364, 9, 3, INK, 3),   # 앞폴
        fist(sk),
    ]
    return ''.join(out)


def scientist():
    """연구원: 흰 가운(무릎까지) · 동그란 안경 · 헝클어진 회색 머리 · 청록 플라스크"""
    sk, coat = '#9ccc74', CLOTH_W
    gown = (P('M160 244 Q204 230 248 244 L256 336 Q204 346 152 336 Z', coat)
            + P('M190 240 L204 274 L218 240 Z', '#7fa8d0', 4) + L('M204 274 L204 340', 3.5)
            + rr_(222, 280, 18, 14, 3, coat, 3.5) + L('M226 280 L226 270', 3, '#c0564a'))
    hair = P('M150 168 Q140 130 168 118 Q178 96 204 106 Q226 92 242 114 Q262 116 258 140 Q236 128 210 132 Q178 128 162 170 Z', '#b9b6c4')
    glasses = (C(224, 186, 17, 'none', 4.5) + C(256, 182, 15, 'none', 4.5) + L('M240 184 L242 183 M207 188 L186 180', 4))
    flask = (P('M318 226 L332 226 L332 246 L350 286 Q352 296 340 296 L310 296 Q298 296 300 286 L318 246 Z', '#e8f4f2', 5)
             + P('M306 276 L344 276 L350 286 Q352 294 340 294 L310 294 Q300 294 302 286 Z', TEAL, 0)
             + C(320, 256, 4, TEAL, 2.5) + C(330, 240, 5, TEAL, 2.5) + C(326, 214, 6, TEAL, 2.5) + fist(sk))
    return walker(skin=sk, shirt=coat, pants='#59606b', shoes='#3f3a36', torso_extra=gown, head_extra=hair + glasses,
                  hand_item_back=sleeve_b(coat), hand_item=sleeve_f(coat) + flask)


def patient():
    """실험체: 줄무늬 환자복 · 머리 붕대 · 바퀴 달린 링거대를 끌고 옴"""
    sk, gw = '#86bf74', '#a9c4dc'
    stripes = L('M174 256 L176 304 M190 250 L190 306 M206 248 L206 306 M222 250 L222 306', 4, '#7f9fbf')
    band = (P('M150 166 Q204 146 258 160 L258 180 Q204 166 150 186 Z', CLOTH_W, 5)
            + L('M170 158 L182 178 M182 156 L170 178', 3, '#c0564a') + E(216, 238, 6, 5, '#c0564a', 0))
    pole = (L('M330 372 L330 92', 7, INK) + L('M330 372 L330 92', 3.5, '#c9d1da')
            + L('M306 372 L354 372', 6, INK) + C(306, 374, 6, '#5d5f6e', 3.5) + C(354, 374, 6, '#5d5f6e', 3.5)
            + L('M318 92 L344 92', 5, INK)
            + P('M334 100 L360 100 L362 150 Q348 162 334 150 Z', '#e8f4f2', 4.5) + P('M336 126 L360 126 L360 148 Q348 158 336 148 Z', TEAL, 0)
            + L('M348 158 Q348 210 318 232', 2.5) + fist(sk))
    return walker(skin=sk, shirt=gw, pants=gw, shoes='#e9dfc6', torso_extra=stripes, head_extra=band,
                  hand_item_back=sleeve_b(gw), hand_item=pole)


def hazmat():
    """방호복: 노란 방호복이 머리까지 · 둥근 방독면(필터 두 개) · 검은 장갑·장화 · 등에 작은 통"""
    su = '#e8c05a'
    su2 = shade(su, .88)
    out = [
        rr_(130, 238, 34, 62, 10, '#9aa3ad', 5), L('M138 238 L138 226 L154 226', 4),                     # 등 통
        limb('M226 247 Q262 244 298 244', 22, su2), C(302, 244, 12, '#3a3440'),
        P('M174 296 L172 364 L198 364 L200 296 Z', su2), P('M204 296 L210 364 L234 364 L230 296 Z', su),
        P('M166 352 Q166 378 182 378 L202 378 Q204 366 198 352 Z', '#3a3440'),
        P('M206 352 Q206 378 222 378 L246 378 Q250 366 238 352 Z', '#3a3440'),
        P(f'M160 244 Q204 226 248 244 L250 310 {zig(160, 250, 310, 8, 6)} Z', su),
        L('M204 240 L204 306', 3.5, shade(su, .7)), L('M164 280 L248 278', 3, shade(su, .7)),
        P('M144 214 Q138 128 204 122 Q270 128 266 214 Q204 236 144 214 Z', su),                          # 두건
        P('M186 152 Q228 140 262 156 Q270 196 250 214 Q214 222 192 206 Q178 182 186 152 Z', '#3e4252', 5), # 방독면
        E(222, 172, 13, 11, '#bfe3ef', 4), E(250, 168, 11, 10, '#bfe3ef', 4),
        C(214, 210, 13, '#9aa3ad', 5), C(254, 204, 12, '#9aa3ad', 5), L('M210 210 l8 0 M250 204 l8 0', 3),
        limb('M218 266 Q262 262 304 264', 24, su), C(309, 265, 13, '#3a3440'),
    ]
    return ''.join(out)


def tube():
    """튜브: 줄무늬 수영복 · 허리에 오리 튜브 · 물안경(이마) · 오리발"""
    sk = '#94c070'
    duck = (E(206, 296, 74, 24, '#f2c14e', 6) + L('M150 288 Q206 278 262 288', 4, '#fff2a8')
            + E(272, 284, 15, 16, '#f2c14e', 5) + C(278, 262, 15, '#f2c14e', 5)
            + P('M290 260 L306 265 L291 271 Z', '#ef9a4a', 4) + f'<circle cx="282" cy="257" r="3" fill="{INK}"/>')
    suit = L('M170 260 L240 258 M170 276 L240 274', 6, CLOTH_W)
    gog = L('M152 152 Q204 136 258 150', 7, '#5b7fc4') + E(220, 144, 13, 9, '#bfe3ef', 4) + E(246, 142, 11, 8, '#bfe3ef', 4)
    out = walker(skin=sk, shirt='#c0564a', pants=sk, shoes='#ef9a4a', torso_extra=suit + duck, head_extra=gog)
    return out.replace('M168 360 Q168 378 182 378 L200 378 Q202 368 196 360 Z', 'M160 360 Q156 378 172 378 L206 378 Q210 368 196 360 Z')


def cowboy():
    """카우보이: 넓은 챙 모자 · 판초(지그재그 무늬) · 박차 장화 · 올가미"""
    sk = '#9ccc74'
    hat = (P('M118 152 Q130 168 204 152 Q278 136 292 148 Q300 132 280 136 Q204 150 132 140 Q114 140 118 152 Z', '#a8743e')
           + P('M160 146 Q158 98 186 96 Q204 108 222 94 Q252 96 252 140 Q204 150 160 146 Z', '#a8743e')
           + L('M160 136 Q204 128 252 130', 6, '#5d4030'))
    poncho = (P('M150 246 Q204 228 258 246 L270 300 L204 312 L140 300 Z', '#c46a5a')
              + L('M150 284 L162 274 L174 284 L186 274 L198 284 L210 274 L222 284 L234 274 L246 284 L258 274', 4, GOLD))
    rope = E(316, 286, 20, 24, 'none', 7) + f'<ellipse cx="316" cy="286" rx="20" ry="24" fill="none" stroke="#d2a86a" stroke-width="3.5"/>' + fist(sk)
    spur = C(178, 372, 6, GOLD, 3) + C(216, 372, 6, GOLD, 3)
    return walker(skin=sk, shirt='#d8c49a', pants='#5a6ea6', shoes='#7a5536', torso_extra=poncho + spur, head_extra=hat, hand_item=rope)


def biker():
    """폭주족: 검은 가죽 재킷(징) · 선글라스 · 닭 볏 머리 · 쇠사슬"""
    sk, jk = '#8cc56a', '#3a3440'
    jacket = (L('M190 244 L200 300 M220 244 L212 300', 4, '#5d5f6e') + _dots([(176, 262), (176, 280), (232, 262), (232, 280)], 3, '#c9d1da'))
    hair = P('M176 140 Q186 96 196 132 Q204 86 216 130 Q228 92 236 136 Q222 146 204 144 Q186 146 176 140 Z', '#c46a5a')
    glasses = P('M206 176 L268 172 Q268 196 248 196 Q236 196 236 180 L232 180 Q228 196 214 196 Q204 194 206 176 Z', '#2c2836', 4.5)
    chain = ''.join(C(312 + 6 * i, 262 + 18 * i, 6, 'none', 3.5) for i in range(5)) + fist(sk)
    return walker(skin=sk, shirt=jk, pants='#3e4252', shoes='#2e2a28', torso_extra=jacket, head_extra=hair + glasses,
                  hand_item_back=sleeve_b(jk), hand_item=sleeve_f(jk) + _dots([(258, 264), (268, 264)], 3, '#c9d1da') + chain)


def camper():
    """캠핑: 등보다 큰 배낭(위에 침낭) · 매달린 냄비 · 앞손에 랜턴 · 모자"""
    sk = '#93c66c'
    pack = (rr_(108, 200, 74, 116, 16, '#ef9a4a', 6) + rr_(100, 176, 90, 32, 14, '#5b7fc4', 6) + L('M110 192 L180 192', 3)
            + rr_(116, 250, 56, 36, 8, shade('#ef9a4a', .85), 4.5)
            + L('M106 262 L98 276', 3) + E(98, 288, 14, 10, '#9aa3ad', 4.5) + L('M84 284 l-6 -4', 4))
    cap = P('M152 156 Q150 108 204 104 Q256 108 256 152 Z', '#c46a5a') + P('M250 150 Q290 146 298 156 Q276 164 250 162 Z', '#c46a5a', 5)
    strap = L('M180 246 Q186 276 176 304', 7, '#6b4a32')
    lantern = (L('M316 268 L316 286', 4) + rr_(300, 286, 32, 40, 8, '#fff2a8', 5) + L('M300 296 L332 296 M300 316 L332 316', 3)
               + P('M304 282 L328 282 L324 288 L308 288 Z', '#5d5f6e', 4) + fist(sk))
    return pack + walker(skin=sk, shirt='#a0784e', pants='#59606b', torso_extra=strap, head_extra=cap, hand_item=lantern)


def delivery():
    """배달: 오토바이 헬멧(앞 가리개 올림) · 등에 네모 배달통 · 주황 조끼(반사 띠). 바쁘게 뛰어 옴"""
    sk, vest = '#9ccc74', '#ef9a4a'
    box = rr_(98, 192, 76, 84, 8, '#c0564a', 6) + L('M106 214 L166 214', 5, CLOTH_W) + L('M136 192 L136 276', 3)
    torso = L('M172 268 L238 266 M172 286 L238 284', 5, '#d9dee6') + L('M184 246 L176 236', 5)
    helm = (P('M144 196 Q138 118 206 112 Q272 116 270 186 L240 192 Q238 170 214 166 Q190 168 186 196 Z', CLOTH_W)
            + L('M196 118 Q236 112 264 140', 8, '#c0564a')
            + P('M232 128 Q276 128 284 156 L272 160 Q262 138 228 140 Z', '#9fb6c8', 4.5))
    return box + walker(skin=sk, shirt=vest, pants='#3e4a6e', shoes=CLOTH_W, torso_extra=torso, head_extra=helm,
                        hand_item_back=sleeve_b('#3e4a6e'), hand_item=sleeve_f('#3e4a6e'))


def jiangshi():
    """강시: 관모(검은 둥근 모자 + 꼭지) · 이마에 노란 부적 · 긴 남색 관복(다리 안 보임) · 두 팔을 앞으로 쭉 (뼈대 robe = 깡총)"""
    sk, rb = '#a6cf72', '#3a4a78'
    out = [
        limb('M226 244 Q266 240 304 240', 22, shade(rb, .85)), C(310, 240, 10, shade(sk, .9)),                  # 뒷팔(소매)
        P('M180 364 Q178 378 196 378 L214 378 Q216 368 208 364 Z', '#2c2836'),
        P('M204 364 Q204 378 222 378 L240 378 Q244 368 232 364 Z', '#2c2836'),
        P('M162 242 Q204 226 246 242 Q256 300 252 366 L156 366 Q150 300 162 242 Z', rb),
        P('M184 238 L204 300 L224 238 Z', GOLD, 4), L('M204 300 L204 364', 3.5),
        P('M156 330 L252 330 L252 342 L156 342 Z', GOLD, 4),
        rr_(196, 284, 18, 18, 3, '#c0564a', 4),
        zhead(204, 188, 52, sk),
        P('M146 156 Q146 112 204 108 Q262 112 262 156 Q204 146 146 156 Z', '#2c2836'),
        P('M140 156 Q204 142 268 156 L268 166 Q204 154 140 168 Z', '#3a3440', 5), C(204, 102, 9, '#c0564a', 4.5),
        P('M226 150 L250 150 L248 214 L230 214 Z', '#f6dc6e', 4),                                               # 부적
        L('M234 160 Q244 168 236 176 Q246 184 236 194 M240 198 l4 10', 3, '#c0564a'),
        limb('M218 262 Q262 260 306 260', 24, rb), C(312, 261, 11, sk),
    ]
    return ''.join(out)


def chef():
    """요리사: 높은 하얀 조리모 · 흰 조리복(단추 두 줄) · 빨간 목수건 · 프라이팬"""
    sk = '#90c46e'
    toque = (P('M164 154 L170 118 Q150 98 168 80 Q178 58 204 70 Q226 54 244 76 Q262 96 240 118 L246 152 Z', CLOTH_W)
             + L('M182 120 L182 150 M204 116 L204 150 M226 120 L226 150', 3, '#c9c1b3'))
    jacket = (_dots([(194, 260), (194, 280), (194, 298), (216, 258), (216, 278), (216, 296)], 3.2)
              + P('M182 238 Q204 252 228 238 L222 252 Q204 262 186 252 Z', '#c0564a', 4))
    pan = (L('M300 266 L336 240', 10, INK) + L('M300 266 L336 240', 5, '#6b4a32')
           + E(358, 222, 32, 12, '#4a4f5e', 5, -32) + fist(sk))
    return walker(skin=sk, shirt=CLOTH_W, pants='#59606b', shoes='#3f3a36', torso_extra=jacket, head_extra=toque,
                  hand_item_back=sleeve_b(CLOTH_W), hand_item=sleeve_f(CLOTH_W) + pan)


def student():
    """학생(꼬마): 남색 교복 · 흰 깃 · 빨간 넥타이 · 큰 책가방. 머리를 조금 크게(표에서 크기 x0.78)"""
    sk, bz = '#9ccc74', '#3e4a6e'
    bag = rr_(112, 212, 64, 88, 18, '#c0564a', 6) + rr_(120, 252, 46, 32, 8, shade('#c0564a', .85), 4.5) + L('M178 246 Q190 272 182 296', 6, '#6b4a32')
    tie = P('M192 240 L204 256 L216 240 Z', CLOTH_W, 4) + P('M199 252 L209 252 L212 284 L204 292 L196 284 Z', '#c0564a', 3.5)
    hair = P('M146 176 Q140 120 204 116 Q262 118 262 166 Q236 140 214 148 Q176 140 160 182 Z', '#3a3440')
    return bag + walker(skin=sk, shirt=bz, pants='#8a8f99', shoes='#3f3a36', torso_extra=tie, head_extra=hair, hr=60,
                        hand_item_back=sleeve_b(bz), hand_item=sleeve_f(bz))


def firefighter():
    """소방관: 황갈색 방화복(반사 띠) · 빨간 헬멧(앞 방패) · 소화기"""
    sk, su = '#8cc56a', '#c9a25a'
    stripes = L('M168 270 L240 268', 7, '#d9dee6') + L('M168 270 L240 268', 3, GOLD) + L('M176 340 L198 340 M206 340 L230 340', 6, '#d9dee6')
    helm = (P('M144 162 Q140 102 206 98 Q268 102 266 156 Q300 156 296 170 Q206 160 132 174 Q126 162 144 162 Z', '#c0564a')
            + L('M206 100 L206 156', 6, shade('#c0564a', .8)) + P('M190 124 L222 124 L218 156 L194 156 Z', GOLD, 4))
    ext = (rr_(304, 236, 34, 74, 12, '#c0564a', 5) + rr_(312, 222, 18, 16, 4, '#4a4f5e', 4)
           + L('M330 228 Q356 224 352 262 L346 290', 6) + L('M318 250 L326 250', 3, CLOTH_W) + fist(sk))
    return walker(skin=sk, shirt=su, pants=su, shoes='#3f3a36', torso_extra=stripes, head_extra=helm,
                  hand_item_back=sleeve_b(su), hand_item=sleeve_f(su) + L('M254 256 L254 274', 5, '#d9dee6') + ext)


def money():
    """돈가방(보물): 검정·흰 줄무늬 옷 · 검은 눈가리개 · 털모자 · 앞손에 $ 자루 · 금화가 튐"""
    sk = '#94c070'
    stripes = L('M170 258 L240 256 M170 278 L240 276 M170 298 L240 296', 6, CLOTH_W)
    mask = P('M200 168 Q236 158 270 166 L268 188 Q236 180 202 192 Z', '#2c2836', 4)
    mask += '<circle cx="225" cy="180" r="5" fill="#fffbe8"/><circle cx="252" cy="176" r="4.5" fill="#fffbe8"/>'
    hat = P('M152 152 Q150 104 204 100 Q258 104 256 150 Z', '#3a3440') + P('M148 154 L260 148 L260 162 L148 168 Z', '#3a3440', 5)
    sack = (P('M300 268 Q286 300 300 330 Q330 350 360 330 Q374 300 356 268 Q330 256 300 268 Z', '#d8c49a')
            + P('M310 270 L346 270 L352 252 L304 252 Z', '#c4a874', 4.5) + L('M308 262 L350 262', 4)
            + f'<text x="329" y="320" font-family="Arial Black,Arial" font-weight="900" font-size="40" fill="{GOLD}" stroke="{INK}" stroke-width="3" text-anchor="middle">$</text>'
            + C(370, 236, 9, GOLD, 4) + C(286, 226, 8, GOLD, 4) + C(352, 212, 7, GOLD, 3.5) + fist(sk))
    return walker(skin=sk, shirt='#3a3440', pants='#3e4252', shoes='#2e2a28', torso_extra=stripes, head_extra=hat + mask, hand_item=sack)


def drum():
    """드럼통: 빨간 드럼통(노랑·검정 경고 띠)을 안고 옴 · 위로 청록 독물이 넘침 (죽으면 펑)"""
    sk = '#86bf74'
    barrel = (P('M246 214 Q290 206 334 214 L338 334 Q290 344 242 334 Z', '#c0564a')
              + E(290, 214, 44, 9, shade('#c0564a', .8), 5)
              + P('M244 262 L336 262 L337 284 L243 284 Z', GOLD, 4.5)
              + ''.join(f'<path d="M{x} 262 l10 0 l-8 22 l-10 0 Z" fill="{INK}"/>' for x in range(252, 334, 20))
              + L('M244 240 Q290 248 336 240 M242 310 Q290 318 338 310', 4, shade('#c0564a', .75))
              + P('M262 214 Q264 196 276 204 Q282 186 296 198 Q306 188 314 206 L318 214 Q290 222 262 214 Z', TEAL, 4.5)
              + P('M316 216 Q324 236 318 250 Q312 236 316 216 Z', TEAL, 3.5)
              + C(328, 266, 11, sk) + C(252, 300, 11, shade(sk, .9)))
    return walker(skin=sk, shirt='#7f8a99', pants='#4f5262', shoes='#3f3a36', hand_item=barrel)


def shield():
    """방패 경찰: 남색 제복 · 경찰 모자(배지) · 투명 방패 (깨지는 규칙은 나중)"""
    sk, uni = '#8cc56a', '#3e4a6e'
    cap = (P('M152 150 Q150 108 206 104 Q258 108 258 146 Z', uni) + P('M146 152 L262 146 L262 158 L146 164 Z', '#2c2836', 5)
           + P('M240 150 Q278 150 284 160 Q262 166 240 162 Z', '#2c2836', 4.5) + P('M196 116 L212 116 L214 132 L204 140 L194 132 Z', GOLD, 3.5))
    badge = P('M222 260 L232 260 L234 272 L227 278 L220 272 Z', GOLD, 3) + L('M166 296 L242 294', 7, '#2c2836')
    sh = (f'<path d="M262 166 L332 160 Q340 160 340 170 L344 362 Q344 372 334 372 L268 374 Q258 374 258 364 Z" fill="#cfe6f2" fill-opacity=".72" stroke="{INK}" stroke-width="6" stroke-linejoin="round"/>'
          + L('M274 186 L292 178 M274 206 L318 188', 4, CLOTH_W) + rr_(266, 300, 72, 16, 4, uni, 4) + fist(sk))
    return walker(skin=sk, shirt=uni, pants=uni, shoes='#2e2a28', torso_extra=badge, head_extra=cap,
                  hand_item_back=sleeve_b(uni), hand_item=sleeve_f(uni) + sh)


# ------------------------------------------------------------------ 보스 3종 (하키 가면·파라오와 같은 큰 몸 틀)
#   뼈대: neck 172 · hip 298 · crotch 306 · knee 334 · split 206 · 뒷팔 어깨 (150,196) · 앞팔 어깨 (262,200)
def _boss_body(sk, coat, pants, boots, head_fn, back_item='', front_item='', coat_extra='', coat_path=None):
    coat_path = coat_path or ('M138 182 Q208 160 282 182 Q300 240 288 306 ' + zig(140, 288, 306, 10, 8) + ' Q126 240 138 182 Z')
    return ''.join([
        back_item,
        limb('M150 196 Q118 236 112 282', 28, shade(coat, .85)), C(110, 292, 16, shade(sk, .9)),
        P('M158 298 L154 360 L198 360 L202 298 Z', shade(pants, .9)), P('M214 298 L218 360 L260 360 L254 298 Z', pants),
        P('M144 354 Q144 378 160 378 L204 378 Q206 366 198 354 Z', boots), P('M214 354 Q214 378 230 378 L272 378 Q276 366 262 354 Z', boots),
        P(coat_path, coat), coat_extra,
        head_fn(),
        limb('M262 200 Q298 214 314 248', 30, coat),
        front_item,
        C(318, 258, 19, sk),
    ])


def yeti():
    """설인(보스): 흰 털 큰 몸(털 가장자리) · 얼굴·손은 좀비 피부 · 얼음 몽둥이"""
    sk, fur, fur2 = '#94c070', '#eef1f4', '#c9d6e2'

    def head():
        return (P('M150 120 Q146 52 214 46 Q284 52 280 122 Q286 160 260 176 L172 176 Q144 160 150 120 Z', fur)
                + L('M156 90 l-10 -6 M272 86 l10 -8 M168 64 l-6 -10 M258 60 l8 -10', 4)
                + E(222, 122, 44, 40, sk) + zface(226, 118, 40, stitches=False)
                )
    coat = ('M130 186 Q210 150 290 186 Q306 246 292 310 L278 300 L266 312 L252 300 L238 312 L224 300 L210 312 L196 300 '
            'L182 312 L168 300 L154 312 L140 300 Q120 246 130 186 Z')
    belly = E(212, 250, 44, 40, fur2, 0) + L('M178 214 l8 8 M240 226 l8 -6 M196 280 l10 4', 3.5)
    club = (P('M300 268 L330 134 Q336 104 364 112 Q384 124 372 150 L322 276 Z', '#bfe3ef')
            + L('M334 160 L350 132 M342 192 L356 170', 3.5, CLOTH_W) + P('M304 256 L324 264 L318 284 L300 276 Z', '#9fb6c8', 5))
    return _boss_body(sk, fur, fur2, '#9fb6c8', head, front_item=club, coat_extra=belly, coat_path=coat).replace(
        limb('M262 200 Q298 214 314 248', 30, fur), limb('M262 200 Q298 214 314 248', 34, fur))


def captain():
    """닻 선장(보스): 선장 모자(흰 윗면·금 닻 배지) · 남색 긴 코트(금 단추) · 회색 수염 · 큰 닻"""
    sk, coat = '#8cbf6c', '#3e4a6e'

    def head():
        return (E(214, 112, 58, 60, sk) + zface(222, 110, 46, stitches=False)
                + P('M170 128 Q190 182 236 178 Q262 172 268 142 Q246 160 230 152 Q200 156 180 126 Z', '#c9c6cf', 5)
                + P('M150 76 Q214 40 280 70 L276 88 Q214 70 154 92 Z', CLOTH_W)
                + P('M150 90 Q214 70 278 86 L280 100 Q214 84 150 104 Z', '#2c2836', 5)
                + P('M236 92 Q286 90 296 104 Q262 110 236 104 Z', '#2c2836', 4.5)
                + C(212, 82, 8, GOLD, 3.5))
    coat_extra = (_dots([(196, 210), (196, 240), (196, 270), (226, 208), (226, 238), (226, 268)], 4.5, GOLD)
                  + L('M150 186 L172 196 M270 186 L250 196', 8, GOLD))
    anchor = (L('M332 376 L344 150', 12, INK) + L('M332 376 L344 150', 6, '#9aa3ad')
              + C(345, 136, 14, 'none', 6) + L('M318 186 L370 190', 9, INK) + L('M318 186 L370 190', 4.5, '#9aa3ad')
              + P('M290 350 Q300 384 332 380 Q364 380 374 350 L360 352 Q350 368 332 368 Q312 366 304 352 Z', '#9aa3ad')
              + P('M286 340 L300 356 L284 360 Z', '#9aa3ad', 4) + P('M378 340 L362 356 L380 360 Z', '#9aa3ad', 4))
    return _boss_body(sk, coat, '#2c2836', '#2e2a28', head, front_item=anchor, coat_extra=coat_extra)


def gasgiant():
    """방독면 거인(보스): 큰 초록 몸 · 방독면(둥근 눈 · 앞 필터) · 갈색 멜빵바지 · 등에 주황 드럼통 둘 · 청록 연기"""
    sk = '#94b86a'

    def head():
        return (E(214, 112, 60, 60, sk)
                + P('M196 70 Q262 60 280 110 Q284 156 252 170 Q212 176 198 150 Q182 110 196 70 Z', '#5d5f6e')
                + C(226, 104, 15, '#bfe3ef', 5) + C(260, 100, 13, '#bfe3ef', 5)
                + f'<circle cx="230" cy="104" r="4" fill="{INK}"/><circle cx="262" cy="100" r="3.6" fill="{INK}"/>'
                + rr_(236, 132, 34, 34, 8, '#9aa3ad', 5) + L('M244 142 L262 142 M244 152 L262 152', 3)
                + L('M164 92 Q186 80 200 80', 8))
    back = (rr_(76, 140, 56, 86, 10, '#ef9a4a', 6) + rr_(100, 200, 56, 86, 10, '#ef9a4a', 6)
            + L('M76 170 L132 170 M100 230 L156 230', 4) + L('M80 196 L128 196 M104 256 L152 256', 4)
            + C(96, 116, 16, TEAL, 4) + C(80, 92, 12, TEAL, 4) + C(104, 72, 9, TEAL, 3.5) + C(124, 104, 10, TEAL, 4))
    coat_extra = (P('M150 236 L278 230 L288 306 L144 306 Z', '#8a5a36') + L('M168 236 L160 186 M262 232 L270 186', 8, '#8a5a36')
                  + C(170, 244, 5, GOLD, 3) + C(258, 240, 5, GOLD, 3) + L('M184 210 Q200 220 214 210 M220 206 Q236 216 248 204', 4))
    fist_big = P('M300 236 Q326 226 340 248 Q348 274 326 284 Q300 286 296 262 Z', sk)
    return _boss_body(sk, sk, '#8a5a36', '#3f3a36', head, back_item=back, front_item=fist_big, coat_extra=coat_extra)


def zombies_new2():
    return {
        'padding': padding(), 'ski': ski(), 'scientist': scientist(), 'patient': patient(), 'hazmat': hazmat(),
        'tube': tube(), 'cowboy': cowboy(), 'biker': biker(), 'camper': camper(), 'delivery': delivery(),
        'jiangshi': jiangshi(), 'chef': chef(), 'student': student(), 'firefighter': firefighter(), 'money': money(),
        'drum': drum(), 'shield': shield(), 'yeti': yeti(), 'captain': captain(), 'gasgiant': gasgiant(),
    }
