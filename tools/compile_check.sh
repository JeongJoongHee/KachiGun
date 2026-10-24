#!/usr/bin/env bash
# Unity 를 열어 둔 상태에서도 쓸 수 있는 컴파일 검사 (Unity 에 포함된 Roslyn 사용)
# 사용: bash "D:/00 Final_Game/tools/compile_check.sh"   → 오류가 없으면 "OK"
U="/c/Program Files/Unity Hub/6000.0.84f1/Editor/Data"
P="/d/00 Final_Game/Kachi_Gun"
OUT="${TMPDIR:-/tmp}/kachigun_compile"; mkdir -p "$OUT"
DOTNET="$U/NetCoreRuntime/dotnet.exe"; CSC="$U/DotNetSdkRoslyn/csc.dll"
REFS=()
for f in "$U"/Managed/UnityEngine/*.dll; do REFS+=("-r:$f"); done
REFS+=("-r:$U/NetStandard/ref/2.1.0/netstandard.dll")
REFS+=("-r:$U/PlaybackEngines/AndroidPlayer/UnityEditor.Android.Extensions.dll")   # 10-24 앱 아이콘(AndroidPlatformIconKind)
for f in UnityEngine.UI Unity.2D.Animation.Runtime Unity.2D.Common.Runtime Unity.Collections Unity.Mathematics; do REFS+=("-r:$P/Library/ScriptAssemblies/$f.dll"); done
mapfile -t RT < <(find "$P/Assets/Scripts" -name "*.cs" -not -path "*/Editor/*")
mapfile -t ED < <(find "$P/Assets/Scripts" -path "*/Editor/*" -name "*.cs")
FAIL=0
"$DOTNET" "$CSC" -nologo -preferreduilang:en -noconfig -nostdlib -target:library -langversion:9 -nowarn:CS0168,CS0219 -out:"$OUT/Runtime.dll" "${REFS[@]}" "${RT[@]}" || FAIL=1
"$DOTNET" "$CSC" -nologo -preferreduilang:en -noconfig -nostdlib -target:library -langversion:9 -nowarn:CS0168,CS0219 -out:"$OUT/Editor.dll" "${REFS[@]}" -r:"$OUT/Runtime.dll" "${ED[@]}" || FAIL=1
[ $FAIL = 0 ] && echo "OK" || { echo "컴파일 오류 있음"; exit 1; }
