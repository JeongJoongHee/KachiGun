# 카치건 (좀비 키우기) — 세로 방치형 분대 슈팅, Unity 6000.0.84f1 · 2D URP

## ▶ 재개 메모 (2026-10-20 — 남은 작업 의견 23개 반영)

**10-20 2차 결정**: 광산 = 보스 던전 1회의 **절반**(`MINE_RATIO` 0.5) → `--retune` 으로 다시 맞춤: M 0.880, 광고 최대 150챕터 149일(2h 175·10h 119), **광고 안 봄 538일 = 그대로**(사용자: 광고를 봐야 하는 구조), 무료 다이아 31%(광산 54%·보스 40%) · "무료3:결제7" 해석 맞음 · 슬롯 골드·미션 골드 그대로 · **사냥 안 만듦**(스테이지 늘리는 방향, 표 끝 너머 자동 계산) · 앱 이름 확정. 남은 결정 = 우선순위(g2-priority)

**재개하면**: `PYTHONUTF8=1 python tools/todo_checks.py` (키 `kachigun-todo-1020`) → 체크·의견 반영. Unity 열려 있으면 `bash tools/compile_check.sh` 만, 닫혀 있으면 자동 시험(10-5~10-7 새로: 광고 다이아·미션 다이아+골드·영어 글자).
**10-20 한 것** (커밋 0b56019, git 저장소 새로 — push 는 사용자 몫: `git push -u origin main`)
- 밸런스: `tools/balance_apply_1020.py` — 항상 `Table/_backup_1020/`(10-20 전 표)에서 다시 씀. 맞춘 값은 `tools/balance_1020_params.json`, `--apply` = 맞추기 없이 표만 다시. 결과 `tools/balance_1020.json` → 밸런스_점검.html 맨 위
  - 광고 최대·하루 4시간 = 50챕터 21일·100챕터 63일·150챕터 146일 / 2시간 168일 / 10시간 121일 / **광고 안 봄 452일**(결정 대기)
  - 골드 1/2 · 무료 다이아 = 살 수 있는 것 전부(214만)의 30% (보상 배율 M 0.577: 도전·보스 던전·광산·미션) · 광산 티켓 1장 = 같은 챕터 보스 던전 1회 보상(무료 다이아 69% 가 광산 — 결정 대기) · 슬롯 강화 골드 = 그 챕터 수입 10분 · 처치 미션 1단계 ≈ 1.5챕터 · 스테이지 미션 Max_Tier 150 · 합성 1·2·4·8
  - 시뮬레이터 `balance_sim.py` 에 광고 다이아·미션 골드·광산 실제 보석·슬롯 강화 골드 모으기 추가. xlsx → csv 는 `python tools/xlsx2csv.py` (Unity 열려 있어도 됨)
- 게임: 상점 [무료 다이아] 광고 40 x 하루 3(`AdGems_Daily/Amount`, 상점 빨간 점) · 미션 보상 다이아 30%+골드 70%(`Mission_Gem_Ratio`, `Gem_Gold_Kills` 6) · 스킨 17개(Skin_17 해적 선장 그림·뼈대, 18~30 삭제) · 펫/탈것·패스 폐기 · 앱 이름 `Editor/ProjectInfo.cs`
- **영어**: `Core/Loc.cs` (`L.T("한국어")`, `L.F`), 표 `Table/String.xlsx` 285줄 (Key 한국어·EN), 새 글자는 `python tools/strings_collect.py`. 설정 → 언어 버튼(씬 다시 불러옴). 긴 글자 칸은 `UIKit.Fit`
- 확인 대기: 남은_작업_정리.html ①(광고 안 봄 452일·무료3:결제7 해석·광산 비중·슬롯 골드·미션 골드 환산) ②(사냥 3안·우선순위·push) ③ 재생 확인(영어 화면 아직 캡처 못 함)

## ▶ 재개 메모 (2026-10-19 밤 마무리 — 사용자 "다음에 이어할 수 있게")

**재개하면 이 순서로**
1. `Get-Process Unity` — Kachi_Gun 이 열려 있으면 배치 시험 불가(컴파일 검사 `bash tools/compile_check.sh` 만)
2. 아래 "확인 대기" 를 사용자에게 물어 다음 작업 정하기
3. 수치를 고칠 땐 **xlsx(`Kachi_Gun/Table/`)가 원본** — csv 는 직접 고치지 말 것. 바꾼 뒤 `bash tools/unity_batch.sh KachiGun.EditorTools.TableConverter.RunBatch` → 자동 시험 → `python tools/balance_sim.py` 로 기간 확인

**지금 상태 (10-19 끝)**
- 기획 표 xlsx 15개 → csv 자동 변환(재생할 때·메뉴), 게임 전부 표로 동작. 보급품·총알 슬롯 강화·분해·미션 5종·골드 상자 있음
- 밸런스: **하루 4시간·무과금·광고 안 봄 = 150챕터 182일(6달)**. 강화 표는 `tools/balance_apply_1019.py`(K = 구간별 [0.47,0.47,0.47,0.47,0.45]) 가 `Player.xlsx`·`Config.xlsx`·`Buff.xlsx`·`Shop.xlsx`·`Unlock.xlsx` 를 백업(`Table/_backup_1019/`, 사용자 원본)에서 다시 씀 — **다시 돌리기 전 그 5개를 백업에서 되돌린 뒤 실행**
- 공속 상한(0.06초) 넘침 → 공격력 1:1 (`Stats.SpeedOverflow`), 공속 강화도 곱으로 성장, 광고 배율 x2
- 자동 시험 오류 0 (10-19 밤 마지막)

**확인 대기 (사용자 답 필요)**
- **광고를 계속 켜면 32일**에 끝남(무과금 182일) — 광고 배율·대상(골드만 등) 다시 정할지
- 100→150챕터가 14일로 빠름 — 후반만 더 느리게 다듬을지 (`balance_tune.candidate(k=[…])` 구간별)
- 밸런스 남은 것(`밸런스_점검.html` 2장): 다이아 수입 ≪ 쓸 곳(총알 SSU·슬롯 강화) · 미션 목표가 실제 진행과 안 맞음(처치 14단계에서 멈춤, 스테이지 137단계부터 불가) · 광산 보상 작음 · 슬롯 강화 골드 비용 의미 없음
- 총알 슬롯 강화를 "장착 칸마다"로 해석함 — 총알마다로 바꿀지
- 스킨 17~30 그림 없음(기본 그림) · 보급품 아이콘(상자+등급 색) 임시
- 게임 이름 · 스킨 이름 · 다음 우선순위(펫/탈것 · 사운드 · 광고/결제 SDK) · git 저장소 만들지

**보여 줄 문서**: `구현_정리.html`(전체 정리) · `밸런스_점검.html`(밸런스, 맨 위 = 최신 6달 결과)
**남은 작업 체크리스트 (10-19 작성)**: `남은_작업_정리.html` — 위 "확인 대기" + 재생 확인·리소스·없는 시스템·출시 전 정리 32항목. 체크·의견은 Chrome localStorage(키 `kachigun-todo-1019`) → `PYTHONUTF8=1 python tools/todo_checks.py` 로 읽음. 사용자가 "남은 작업 반영해 줘" 하면 이걸 먼저 읽을 것

## ▶ 10-19 밤 8 — 목표 6달로 다시 (사용자 "하루 4시간 무과금 기준 6달")
- `tools/balance_apply_1019.py` K = 구간별 [0.47, 0.47, 0.47, 0.47, 0.45] (백업에서 되돌린 뒤 다시 씀). 비용 x1.1509·1.0819·1.0537·1.0362·1.0268 / 성장 x1.0294·1.0155·1.0102·1.0069·1.0050
- 결과(광고 안 봄): 10챕터 4일 · 50챕터 73일 · 100챕터 168일 · **150챕터 182일**. 끝에 총알 SU·SU·U, 스킨 30, 보급품 25, 보스 던전 Lv 137. 자동 시험 오류 0
- **광고를 계속 켜면 32일** (x2 가 공격력·골드 둘 다라 강화 성장이 느릴수록 영향이 커짐) — 사용자에게 알림
- 남은 모양 문제: 100→150챕터가 14일로 빠름 (후반은 다이아·총알 등급이 끌고 감). 구간별 K 로 고를 수 있음 (`balance_tune.candidate(k=[…])`)
- 시뮬레이터 반복 사냥 계산 단위 30분(balance_sim.py)

## ▶ 10-19 밤 7 — 밸런스 적용 (사용자 결정: 목표 2달 · 강화표 수정 OK · 공속 넘침 → 공격력 1:1 · 광고 x2)
- `tools/balance_apply_1019.py`(K=0.505) 가 `Player.xlsx` Gold_Upgrade 를 다시 만듦: 구간 Lv 80/250/500/1000, 레벨/스테이지 2, 공격력·공속·돈 셋 다 같은 비율로 곱 성장 (구간별 비용 x1.1534·1.0831·1.0545·1.0367·1.0275 / 성장 x1.0317·1.0167·1.0109·1.0074·1.0056). 칸 이름 `Spd_Add_Per_Lv` → `Spd_Growth_Rate`(Tables 가 있으면 곱으로 이어 계산). Config Bracket_Growth_Rules 도 같은 값. 원본 백업 `Kachi_Gun/Table/_backup_1019/`
- 광고 x2: Buff.xlsx(buff_atk·buff_gold·dungeon_ad_x3 = 2, 이름/설명) · Config BuffMul·AdMultiplier 2 · Shop·Unlock 글자. 화면 글자는 `HUD.BuffX()`·결과 창이 표에서 읽음. 영구 3배 패키지는 그대로
- `Stats.SpeedOverflow()`: 상한 없는 간격 < Min_Fire_Interval 이면 그 비율을 `Damage` 에 곱함. 무기 창에 "넘친 공속 → 공격 xN"
- 결과(게임 csv 로 시뮬레이션): 4시간·광고 안 봄 61일 · 광고 계속 26일 · 2시간 78일 · 10시간 45일. 자동 시험 오류 0 (공속 넘침 1:1 시험 추가)
- 남은 밸런스: 다이아 수입 vs 쓸 곳 · 미션 목표 · 광산 보상 (밸런스_점검.html 2장)

## ▶ 10-19 밤 6 — 밸런스 점검 (사용자 "지금 테이블로 전체 밸런스 점검") → `밸런스_점검.html`
- 시뮬레이터 `tools/balance_sim.py`(csv 를 게임 식 그대로, 하루 4시간·최적 구매 가정) · 후보 `tools/balance_tune.py`
- 결과: 지금 표 = 150챕터 58시간(광고 계속 8시간). 6~25챕터만 벽(약 50시간), 25챕터 뒤 폭주(골드 강화 후반 구간 돈 성장 ≈ 비용 성장). 공속 상한 0.06초에 13챕터 도달(속도 강화 헛돈, 느린 총 압도) · 무료 다이아 ≈ 쓸 곳의 1% · 처치 미션 14단계에서 멈춤·스테이지 미션 137단계부터 불가
- 후보: Gold_Upgrade 구간을 레벨 80/250/500/1000, 레벨/스테이지 2, 공격력 성장 = 체력 성장^0.6 → 121시간(30일), 고르게. **xlsx 수정·공속 넘침→공격력 등은 사용자 허락·전체 길이 결정 대기**

## ▶ 10-19 밤 5 — 기획 표(xlsx) 적용 (사용자가 `Kachi_Gun/Table/` 에 xlsx 15개)
- **변환**: `Editor/XlsxReader.cs`(xlsx 직접 읽음, 설치 없음, 수식 = 저장된 계산값) + `Editor/TableConverter.cs` — 메뉴 `카치건 > 표 변환 (xlsx → csv)`, **재생 누를 때·에디터 열 때 xlsx 가 바뀌었으면 자동**(시각 = `Library/KachiGunTables.stamp`), 배치 `TableConverter.RunBatch`. 시트 하나 = csv 하나, 이름 = 시트 이름에서 `_150`·`_1500`·`_150Ch` 를 뗌. 바뀐 csv 만 씀. 엑셀로 열어 둬도 읽음
  - 검증: 변환 결과를 openpyxl 값과 6만 칸 대조 → 차이 0
- 예전 csv 5개(Config·Weapon·Skin·Zombie·Mission) 삭제 → 새 csv 24개. `Tables.cs` 새로 씀: 칸 이름의 `(초)` 같은 단위는 떼고 읽음(`Head()`), 표 끝 너머(스테이지 750·강화 1500·도전/보스 던전/미션 150)는 마지막 성장률로 이어서
- 표 → 코드: Stage_Table(스테이지별 처치 수·동시 수·간격·체력·골드·보스) · Chapter_Summary(맵 색·이름 → 새 챕터 토스트) · Gold_Upgrade(`Stats.Cost/Power/Speed/Money`) · Player_Base(`Capy.Scale`·조준 각·이동 속도·최소 발사 간격·`Field.Home/Sub` 자리) · Weapon_Grade_Growth(등급 13개 이름·색, `Tables.GradeName/GradeColor`) · Weapon_Master · Bullet_Spec(`Bullet.Fire(spec…)`) · Skin_Table(31) · Zombie_Master(속도 범위·멈춤 거리·키·그림자) · Challenge · Boss_Dungeon · Mine_Ore_Spec(4종, 거대 황금 크리스탈 = 금빛) · Mine_Scaling(보상 배율) · Mission_Master/Tier · Buff_Table(최대 누적 1시간) · Shop_Products(상점 목록) · Unlock_Roadmap(`GM.Unlocked(id)`·`GM.LockMsg(id)`·열림 알림)
- **새 시스템**: 보급품 창 `SupplyPopup`(25종, 6챕터마다 열림, 보유 효과 공격·속도·돈·보스 피해 합산, `S.supplies`) · **총알 슬롯 강화**(무기 창 장착 칸 → [강화], `S.slotLv[3]`, 다이아+골드+성공 확률, 장착 총알 등급이 최대 레벨·효과 배율 — 해석: 표의 "총알 슬롯 강화" 문구대로 칸마다. 사용자 확인 대기) · 분해(인벤토리 → [분해] 다이아) · 미션 보스 처치·총알 합성(`statBossKills`·`statMerges`) · 상점 골드 상자(다이아 100 → 지금 스테이지 좀비 600마리 골드 x 돈 배율)
- 보스 피해 배율: `Bullet.bossMul` → `GM.Damage(t, dmg, from, bossMul)` (보스일 때만)
- 스킨 17~30 은 그림이 없어 `Art.ArtSkin()` = 기본 그림 + 스킨 창 "그림 준비 중"
- PlayTest 10-1~10-4 추가(표 값·보급품·슬롯 강화·D +12 제한·분해·골드 상자·미션 기록) → 자동 시험 오류 0. 배치 변환도 확인(Unity(Mono) 와 .NET 의 아주 큰 수 마지막 자리 표기만 달라 5개 csv 다시 씀, 값은 같음). 캡처 `캡처/기획표_적용_화면들.png`
- 거대 황금 크리스탈 그림 `Gen/gem_gold.png` (`python tools/icon_gen.py gem_gold`) — 표 Sprite_Path 가 UI/icon_gem 이면 Huge 는 자동으로 이 그림
- 구현_정리.html 의 시스템 표·"수치 고치는 곳"·변경 기록·남은 것 갱신

## ▶ 10-19 밤 4 — 보스 시간 제한 + 반복 사냥 (사용자 "추천 방식으로")
- Config `BossTime` 30초. 진행 막대 = 남은 시간(빨강, "보스! N초"). 시간 끝 → `GM.BossFail()`: 보스는 오른쪽으로 걸어 나가 사라짐(`Zombie.Retreat`, 보상 없음) · `S.bossFarm = true`(세이브) · 토스트 "보스 실패! 강화 후 다시 도전하세요"
- 반복 사냥: 좀비는 계속 나오고 골드만 들어옴(처치 수·스테이지 그대로), 막대 = "반복 사냥 중 — 강화 후 보스 도전!" · Stage 아래 빨간 **[보스 도전]** 버튼(콩닥, `GM.ChallengeBoss()`) · 공격력 강화할 돈이 모이면 안내 화살표 다시(`GM.upgradeHint`, 강화하면 꺼짐)
- 멈춘 좀비가 겹치지 않게 stopDist 2.0~4.6 으로 넓힘
- 자동 시험에 3-0 보스 실패→반복 사냥→보스 도전→3-1 추가, 오류 0. 캡처 `캡처/보스실패_반복사냥.png`·`03b_boss_farm.png`

## ▶ 10-19 밤 3 — 배치·톤 (사용자 요청)
- 배너(Stage N-M·보스 등장·던전 이름) → **토스트**로 통일(`HUD.Banner` = 색 글자 토스트, 큰 글자 배너 삭제), 토스트 판 800x100 + 뜰 때 살짝 튐
- 강화 카드 3장 + 이름을 **한 판(panel_brown, `upgradePanel`)** 으로 묶음
- 오른쪽 줄: 공격 x3 → 골드 x3 → **절전모드**(나무 받침 + i_pause_w, -554) → **자동강화**(맨 아래, -694). Stage 아래 절전모드 버튼은 없앰
- 배경: `tools/side_map.py` 에 TONE(감마 0.78·×1.06·+6) → 평균 밝기 64 → 95
- 강화 안내 화살표 = 처음 쓰던 노란 화살표 Gen/arrow 로 되돌림(사용자 요청, icon_gen.py RETIRED 에서 arrow 뺌). Unity 열려 있어 시험 못 함 → 이어서 "두껍지 않고 샤프하게": icon_gen.arrow() 를 4배 크게 그려 줄이는 매끈한 날렵한 화살표(192px, 도트 아님)로, ArtImport 에서 arrow.png 만 Bilinear
- 이어서: 토스트 판 투명(글자 44·외곽선 4만) · 카피바라 1.2배(`Capy.Scale`, 루트 크기라 총·그림자·총구 같이) — 자동 시험 오류 0, 캡처 `캡처/배치_톤_캐릭터1.2배.png`

## ▶ 10-19 밤 2 — UI 팩 적용 (사용자가 리소스/ 에 도트 UI 팩 추가: Woods Journey·Buttons·Settings·Shop·Win_loose·Levels·Main_tiles·Icons 등)
- `tools/ui_kit.py`: 팩에서 부품 60개를 잘라 5배(NEAREST) → `Art/Kit/*.png` (원본 안 건드림). 9-slice 테두리 = BORDER(파이썬)·`Art.Kit()` 의 kitBorder(C#) 같은 값. 미리보기 `tools/_preview/ui_kit.png`, 부품 찾기용 번호 그림 `tools/_preview/kitidx/*.png`
  - 밝은 아이콘 `*_w`(갈색 받침 위용) · `badge`(빨간 ! 알림) · `outline`(흰 테두리 → 등급색 곱함)은 스크립트가 색을 바꿔 만듦
- `UIKit`: Panel = 양피지 창(초록 제목줄+X), ColorBtn = 색에 따라 초록 버튼/나무 버튼(회색 = 꺼짐 색), KitBtn·KitImg·TileBtn(나무 받침+아이콘)·Row(창 안 줄 = 둥근 나무판)·SetOn·Ink(양피지 위 갈색 글자)
- 바뀐 곳: 모든 창(제목줄·X 는 투명 버튼) · 확인 창(초록 [시작]/[확인]) · 결과 창(성공 별 3개 / 실패 파란 X) · 스킨·상점·미션·설정 줄 · 무기 창 칸(황토 칸 + 등급색 테두리) · 미션·진행·체력 막대(UI 팩 막대) · HUD 설정(톱니)·상점·미션(트로피)·버프(나무 받침)·자동강화(나무 ▶)·절전모드·골드/다이아 바탕·도전·챕터 마디(동그라미)·총알 칸·아래 버튼 줄·자물쇠·알림 ! · 안내 화살표(초록 ▼) · 토스트·던전 바탕
- **안 쓰는 그림 40장 삭제(사용자 요청)**: Gen 9(gear·mission·auto·arrow·lock·crate·bolt·moon·round_line) · UI 30(예전 UI.png 조각: btn_*·plank_*·panel_*·card_1~3·card_base·edge_*·statbox_*·stat_spd·icon_sword) · Map/main_map. 도구가 다시 만들지 않게 icon_gen.py RETIRED, slice_assets.py RETIRED → 중간 재료는 `tools/_build/` 에 저장(카드·아이콘 만들 때 읽음). 남은 생성 그림 = 효과(폭발·불꽃·그림자)·광산 회색 바위·round(빈 칸 바탕)·capy_head
- 삭제 뒤 Unity 가 열려 있어 자동 시험 못 함 → 코드가 부르는 그림 경로 30개가 모두 있는 것만 확인. (뒤에 자동 시험 통과)
- 캡처 `캡처/UI팩_적용_화면들.png`, 자동 시험 오류 0
- 테두리 매끈하게(사용자 "삐쭉삐쭉"): ui_kit.py big() = Scale2x 두 번(대각선 메움) → 5배 BICUBIC(알파 곱해서) + Kit 필터 Bilinear · clean_window() 로 창의 찢긴 자국(제목줄 홈·양피지 옆 들쭉날쭉)을 줄/열마다 흔한 색으로 폄. 캡처 `캡처/UI테두리_매끈.png`, 자동 시험 오류 0 (삭제 뒤 시험도 이걸로 통과)

## ▶ 10-19 밤 작업 — 뼈대(리깅) 애니메이션 (사용자 요청: PixelFantasy 로 좀비·카피바라 움직임 개선)
- PixelFantasy 는 뼈대가 아니라 자체 그림 한 장씩 바꾸는 방식이고 좀비(ZombieA·B)도 64px 작은 도트라 그대로 못 씀 → 사용자 결정: **Unity 2D Animation(이미 설치)으로 우리 그림에 뼈대 직접 심기**
- `tools/rig_gen.py`: 그림에 8px 그물 + 뼈(골반·허리·머리·허벅지/정강이·팔+든 무기) + 무게 → `Resources/Art/Rig/*.txt`(좀비 z_<id> 12, 카피바라 c_00~16). 뼈 자리 숫자 = ZOMBIE_RIG·CRAWL_RIG·CAPY. 미리보기(게임과 같은 식) `tools/_preview/rig_<이름>.png`(첫 칸 = 구역 색: 파랑 뒷다리·초록 앞다리·주황 뒷팔·분홍 앞팔)
- `Core/Rig.cs`: RigData(파일 → 그물·뼈·무게 든 Sprite, 실행 중 생성) · RigView(SpriteRenderer+SpriteSkin+뼈 Transform, Walk·Idle·Capy 자세 = rig_gen.pose_angles 와 같은 식, Flip = 통째 뒤집기)
  - 좀비: 3/4 앞모습이라 다리를 맞바꾸지 않고 **번갈아 발 들어 내딛는 걸음**(디딘 발이 땅에 붙게 골반 높이 계산 FootLift), 팔·무기 흔들림, 멈추면 꿈틀·허우적. 기는 좀비 = 두 팔 번갈아 뻗기. 사신 = 옷자락. 맞기·죽기는 원래 그림
  - 카피바라: 사격 자세 1장 + 숨쉬기·쏠 때 뒤로 젖힘·위를 겨누면 살짝 젖힘·걷기. **총은 몸통(spine) 뼈 자식**
- 시험: `bash tools/unity_batch.sh KachiGun.EditorTools.PlayTest.RunRig --play` → `Logs/PlayTest/rig/*.png` (좀비 12종 세워 위상 돌림). 캡처 `캡처/뼈대_좀비걷기.gif`·`뼈대_좀비멈춤.gif`·`뼈대_좀비걷기_8장.png`
- **PixelFantasy 의 PixelTileEngine 이 Newtonsoft 를 필요로 해 프로젝트 전체 컴파일이 깨져 있었음** → Packages/manifest.json 에 `com.unity.nuget.newtonsoft-json` 3.2.1 추가(에셋은 안 지움)
- compile_check.sh 에 Unity.2D.Animation.Runtime 등 참조 추가
- UI: 총알 칸 등급 두 번 표시 고침 + 긴 이름 글자 줄임, 아래 버튼 판 = panel_small (저녁 UI 작업 자동 시험·캡처 확인 끝)

## ▶ 10-19 저녁 작업 — UI 배치 변경 (사용자 그림 `UI 수정.png`)
- 왼쪽 위: 설정 → **상점** → 미션(그림에 없어 상점 아래로) · 가운데: Stage → **[도전]** 버튼(빨간 점 = 할 수 있는 도전) → 절전모드
- 오른쪽: 골드/다이아 → **공격 x3 → 골드 x3 → 자동강화**
- **[메뉴] 드롭다운 삭제** → 맨 아래 버튼 줄 `Nav_*`: 무기·스킨·보급품·보스·광산(티켓 수·빨간 점)
- 아래: **총알 1·2·3 칸**(`AmmoSlot*`, 장착 무기 등급·이름, 누르면 무기 창) → 강화 카드(0.88배로 줄임) → 버튼 줄. 높이 합 `HUD.BottomUiHeight` 540
- 땅: `Field.FieldTop/Bottom` = 맵 px 820~1760 (아래 UI 에 안 가리게), 분대 y = 화면 0.385·0.512·0.640
- (밤에 Unity 닫고 자동 시험·캡처 확인 끝)

## ▶ 10-19 오후 작업 — 가로 전장으로 변경 (사용자 그림 `배경 수정.png` = PvZ 참고)
- 사용자 결정: **세로 화면 유지 · 줄(레인) 없음 · 배경은 지금 맵 무늬로 새로 · 총은 `총 이미지.png` 로 교체**
- 배경: `tools/side_map.py` — Main_Map 조각을 이어 붙여(1/4 크기에서 고르고 실제 크기 그대로 붙임, 좌우 뒤집기만) 위·아래 숲, 가운데 흙+밝은 길 → `Art/Map/side_map.png`. 숲 경계 숫자 = LAYOUT(top_edge 330·bottom_edge 2140), 다른 모양은 `python tools/side_map.py <seed>`. (옛 main_map.png 는 안 씀)
- `Field`: 흙 띠 = 맵 px y 760~1930 (`FieldTop/FieldBottom`), 분대 = 왼쪽 세로 한 줄(x 화면 13%, 리더 가운데 0.549 · 동료1 위 0.402 · 동료2 아래 0.696). 좀비는 오른쪽 화면 밖(`SpawnPoint`)에서 나와 `Zombie` 가 왼쪽으로 직진, 맨 앞 카피바라 + stopDist 에서 멈춤. 던전 `RandomRoad(near, far)` = 분대 앞 ~ 오른쪽 끝(옛 '아래~위' 뜻 대신).
  → 오전의 `캐릭터 위치.png` 자리(왼쪽 아래 + 빨간 점)는 이 배치로 대체됨.
- 총: `gun_split.py` 가 `총 이미지.png` 를 1.6배(NEAREST) + 스킨별 앞발을 펌프 위에 얹어 `gun_XX.png`(260x130, 회전 중심 90,55). 총구 = 중심에서 +137.6·+17.6px (`Art.GunMuzzle`).
- 캡처 `캡처/가로전장_전투.png`·`가로전장_화면들.png`, 자동 시험 오류 0

## ▶ 10-19 오전 작업 (사용자 요청: 캐릭터 자리 고정·동료 자리·총 분리)
- **분대 자리 고정**(스테이지): `캐릭터 위치.png` 대로 리더 = 왼쪽 아래, 동료 2 = 빨간 점 두 곳. 화면 비율 좌표 `Field.ScreenPt(fx, fy)`(폰 폭이 달라도 화면상 같은 자리) — Home (0.180, 0.734) · 동료1 (0.1025, 0.612) · 동료2 (0.442, 0.720), 값은 발 위치.
  스테이지에선 터치 이동 없음. **던전(도전·광산·보스)은 그대로 이동**(좀비·보스가 제자리에 서 있어 고정하면 못 깸) → 던전도 고정할지 사용자 답 대기.
- **총 분리**: `tools/gun_split.py`(slice_assets do_chars 가 부름) — 몸 시트 0~7칸에서 총+손잡이 앞발을 지우고 메움, 스킨별 총 `Char/gun_XX.png`(200x90, 회전 중심 34,40), 칸별 총 자리 `Char/gun_pos.txt`, 총구 불꽃 `Char/muzzle_flash.png`. 6번(반동) 칸은 7번 몸으로 대체, 반동은 코드(총이 뒤로 밀리며 위로 9°).
  `Capy`: 총은 Body 자식, 대상 쪽으로 회전(위 80°·아래 45° 까지), 총알은 돌아간 총구에서. (캡처는 오후 작업으로 대체)
- 원본 그림이 `리소스/` 로 옮겨져 slice_assets 에 `src()` 추가(맨 위 → 없으면 리소스 폴더)
- **치트 창**(에디터 전용, 빌드엔 안 들어감): 메뉴 `카치건 > 치트 창` — 골드 +1k~+1t·x10·직접 입력·0으로 / 다이아 +100~+100,000·직접 입력 / 광산·보스 티켓. `Editor/CheatWindow.cs`
- 알림: 스킨 16 원본은 칸마다 주황/회색 고양이가 섞여 있음(깜빡여 보임) — 원본 문제

## 원칙
- 원본 PNG 는 건드리지 않는다. 게임용 그림은 `tools/slice_assets.py` 가 잘라 `Kachi_Gun/Assets/Resources/Art` 에 만든다(다시 돌려도 같은 결과).
- 원본에 없는 작은 아이콘·효과(설정 톱니·미션·자물쇠·화살표·폭발 등)는 `tools/icon_gen.py` 로 직접 그린 것 → `Art/Gen`.
- 생성 도구 표시(✦)가 겹친 스킨 마지막 쓰러짐 칸은 앞 칸으로 대체(캐릭터가 쓰러질 일이 없어 안 보임).
- 씬은 카메라 + `Boot` 만. 맵·분대·UI 는 전부 코드가 만든다(인스펙터로 배치 안 고침 → 숫자는 코드에서).

## 구조 (`Kachi_Gun/Assets/Scripts`)
- `Core` — `Tables`(Resources/Tables/*.csv 24개 읽기 — 원본은 `Kachi_Gun/Table/*.xlsx`, 표 끝 너머는 마지막 성장률) · `SaveData`(PlayerPrefs JSON `KachiGun.Save.v1`, 보급품·슬롯 강화 레벨 포함) · `Stats`(강화·스킨·보급품·슬롯 강화·버프·공속 넘침) · `Num`(1k·1m·1b·1t 다음 1aa·1ab … 표기, 10-19 변경) · `Art`(시트 → 스프라이트) · `AdService`/`Iap`([임시] 광고 = 바로 지급, 결제 = 준비 중)
- `Game` — `Boot`(전부 생성) · `GameManager`(스테이지·보스·도전·광산·보스 던전·뽑기/합성/장착·스킨·버프·미션) · `Field`(맵·카메라·가로 흙 띠, 분대 자리·좀비 나오는 곳) · `Squad`(리더+동료2, 스테이지 = 자리 고정 · 던전 = AI 이동/터치 이동) · `Capy`(자동 사격, 뼈대 몸 + 몸통 뼈에 붙은 총) · `Zombie`(뼈대 걷기, 맞기·죽기는 그림)/`Ore`(`Target` 공통) · `Bullet`(단발·속사·산탄·드릴 관통·미사일, 레이저는 즉시 판정) · `Fx`(폭발·불꽃·레이저 줄, 피해 숫자·날아가는 지폐)
- `UI` — `HUD`(UI 수정.png 배치: 아래 버튼 줄·총알 칸, 드롭다운 없음) · `UIKit`(코드로 UI 만들기, 길게 누르기 `HoldButton`) · `Popup`(창 공통 + 확인/결과) · `WeaponPopup`(끌어서 합성) · `MenuPopups`(스킨·상점(표)·미션 5종·보급품·설정)
- `Editor` — `SceneBuilder` · `CheatWindow`(재화 치트) · `ArtImport`(압축 없음·최대 4096) · `PlayTest`(자동 시험 + 캡처) · `XlsxReader`·`TableConverter`(xlsx → csv, 메뉴 `카치건 > 표 변환`, 재생할 때 자동)

## 명령 (Git Bash)
- 컴파일 검사(Unity 열려 있어도 됨): `bash tools/compile_check.sh`
- 씬 다시 만들기: `bash tools/unity_batch.sh KachiGun.EditorTools.SceneBuilder.BuildAll`
- 움직임 연속 촬영: `bash tools/unity_batch.sh KachiGun.EditorTools.PlayTest.RunAnim --play` → `Logs/PlayTest/anim·walk/*.png` (배치 모드는 시간이 빨리 흘러 속도감은 실제와 다름)
- 자동 시험: `bash tools/unity_batch.sh KachiGun.EditorTools.PlayTest.Run --play` → `Kachi_Gun/Logs/PlayTest/*.png` (보여 줄 땐 `캡처/` 로 복사)
- 그림 다시 자르기: `PYTHONIOENCODING=utf-8 python tools/slice_assets.py [chars zombies ui bullets map]` · 아이콘: `python tools/icon_gen.py`
- 표 변환: `bash tools/unity_batch.sh KachiGun.EditorTools.TableConverter.RunBatch`
- 밸런스: `PYTHONIOENCODING=utf-8 python tools/balance_sim.py` (지금 csv 로 150챕터까지 며칠) · 후보 `tools/balance_tune.py` · 적용 `tools/balance_apply_1019.py`
- Unity 에 Kachi_Gun 이 열려 있으면 배치가 안 됨(스크립트가 알려 줌)

## 좌표 메모
- 맵 1728x2448px, 100px = 1칸, 원점 가운데. `Field.P(px,py)` = 맵 픽셀(위가 0) → 월드. 분대 자리 = 화면 비율 `Field.ScreenPt` (10-19 작업 참고).
- 카메라는 맵 높이 전체가 보이게(폰 세로). 위 15%·아래 19% 는 UI 가 덮는 것으로 계산(`Field.TopUi/BottomUi`).
- 캐릭터 시트 4x4 칸 360px, **발 기준 정렬**(발 가운데 x=160, 바닥 8px 위 — 원본 칸 간격이 고르지 않아 칸 기준으로 맞추면 프레임마다 미끄러짐). 총은 따로 그림(gun_split.py) — 총구 = 총 회전 중심에서 +142px·+3px (`Art.GunMuzzle`).
- 좀비 시트 7칸 400x380: 걷기 0~3(걷기 1장에서 엉덩이 아래 다리를 앞뒤로 벌려 만듦, 기어가는 좀비는 늘였다 줄임, 사신은 옷자락) · 맞기 · 죽기 · 맞기 흰 실루엣. 걸은 거리 0.15칸마다 다음 걷기 그림.

## 기획서 대비 빠진 것 (다음 후보)
펫·탈것(12) · 사냥(35) · 시즌/VIP 패스 · 실제 광고/결제/클라우드 저장 · 사운드(리소스 없음) · 다국어 · 스킨 30종 중 그림 16종 · 보급품 그림
