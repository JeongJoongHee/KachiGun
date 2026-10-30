# -*- coding: utf-8 -*-
"""AAB · APK 점검 (10-24, 10-30 광고·결제 기준으로 바꿈) — 사용: PYTHONUTF8=1 python tools/aab_check.py [aab·apk 경로]
패키지 이름 · 서명(jarsigner) · 앱 이름(영어/한국어) · 64비트 라이브러리 ·
2번 빌드부터: 광고 ID 권한(AD_ID) · 결제 권한(BILLING) · AdMob 앱 ID 가 "있어야" 통과.
(AAB 매니페스트·리소스는 protobuf, APK 매니페스트는 바이너리 XML(UTF-16) 이라 글자 조각을 찾아 확인)"""
import glob, os, re, subprocess, sys, zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
JDK = 'C:/Program Files/Unity Hub/6000.0.84f1/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin'
ADMOB_APP_ID = 'ca-app-pub-5299482082775448~5156919833'   # 키 보관/admob.txt


def main():
    p = sys.argv[1] if len(sys.argv) > 1 else max(glob.glob(os.path.join(ROOT, 'Kachi_Gun', 'Builds', '*.aab')), key=os.path.getmtime)
    apk = p.lower().endswith('.apk')
    z = zipfile.ZipFile(p)
    names = z.namelist()
    man = z.read('AndroidManifest.xml' if apk else 'base/manifest/AndroidManifest.xml')
    res = z.read('resources.arsc' if apk else 'base/resources.pb')
    enc = 'utf-16-le' if apk else 'utf-8'
    has = lambda s, blob=man: s.encode(enc) in blob or s.encode('utf-8') in blob
    ok = lambda b: '통과' if b else '확인 필요'
    print('파일', os.path.basename(p), '%.1f MB' % (os.path.getsize(p) / 1048576))
    print('패키지 com.zkfpf.kachizombie :', ok(has('com.zkfpf.kachizombie')))
    print('광고 ID 권한 있음 (AD_ID)     :', ok(has('com.google.android.gms.permission.AD_ID')), '— Play Console 광고 ID 선언 = 예')
    print('결제 권한 있음 (BILLING)      :', ok(has('com.android.vending.BILLING')))
    print('AdMob 앱 ID                   :', ok(has(ADMOB_APP_ID)), ADMOB_APP_ID)
    text = man.decode(enc, errors='ignore') if apk else man.decode('latin-1')
    perms = sorted(set(re.findall(r'[a-z.]+\.permission\.[A-Z_]+', text)))
    print('권한 목록                     :', ', '.join(perms) or '없음')
    print('앱 이름 영어 Capybara vs Zombies :', ok(has('Capybara vs Zombies', res)))
    print('앱 이름 한국어 카피바라vs좀비   :', ok(has('카피바라vs좀비', res)))
    pre = 'lib/' if apk else 'base/lib/'
    libs = sorted({n[len(pre):].split('/')[0] for n in names if n.startswith(pre)})
    print('네이티브 라이브러리 (64비트 arm64-v8a) :', ', '.join(libs), '→', ok(libs == ['arm64-v8a']))
    r = subprocess.run([os.path.join(JDK, 'jarsigner.exe'), '-verify', '-verbose:summary', '-certs', p], capture_output=True, encoding='cp949', errors='replace')
    signed = 'jar verified' in r.stdout
    owner = [l.strip() for l in r.stdout.splitlines() if 'CN=' in l][:1]
    print('서명 (jarsigner)              :', ok(signed) if not apk else ('v1 서명 있음' if signed else 'v2 서명만(APK 정상) — apksigner 로 확인'), owner[0] if owner else '')


if __name__ == '__main__':
    main()
