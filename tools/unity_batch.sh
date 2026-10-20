#!/usr/bin/env bash
# Unity 배치 모드로 에디터 메서드 실행. Unity 가 이 프로젝트(Kachi_Gun)를 열고 있으면 실패하므로 먼저 확인한다.
# 사용: bash "D:/00 Final_Game/tools/unity_batch.sh" <메서드> [--play]
#   씬 다시 만들기 : KachiGun.EditorTools.SceneBuilder.BuildAll
#   자동 시험      : KachiGun.EditorTools.PlayTest.Run --play   (화면은 Kachi_Gun/Logs/PlayTest/*.png)
METHOD="$1"; [ -z "$METHOD" ] && { echo "메서드 이름을 넣어 주세요"; exit 2; }
UE="/c/Program Files/Unity Hub/6000.0.84f1/Editor/Unity.exe"
P="D:/00 Final_Game/Kachi_Gun"
LOG="${TMPDIR:-/tmp}/kachigun_batch_$(date +%H%M%S).log"
if powershell -NoProfile -c "if (Get-Process Unity -ErrorAction SilentlyContinue | ? { \$_.MainWindowTitle -like '*Kachi_Gun*' }) { exit 0 } else { exit 1 }"; then
  echo "Unity 에 Kachi_Gun 이 열려 있습니다. 닫아 주세요."; exit 3
fi
QUIT="-quit"; [ "$2" = "--play" ] && QUIT=""
timeout 580 "$UE" -batchmode $QUIT -projectPath "$P" -executeMethod "$METHOD" -logFile "$LOG"
CODE=$?
echo "exit=$CODE  log=$LOG"
grep -n "error CS\|Exception\|\[TEST\]\|\[SceneBuilder\]" "$LOG" | grep -v "Licensing" | head -80
exit $CODE
