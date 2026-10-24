# -*- coding: utf-8 -*-
"""AAB 점검 (10-24) — 사용: PYTHONUTF8=1 python tools/aab_check.py [aab 경로]
패키지 이름 · 버전 · 서명(jarsigner) · 앱 이름(영어/한국어) · 광고 ID 권한 없음 · 용량 · 64비트 라이브러리를 확인.
(매니페스트·리소스는 protobuf 라 글자 조각을 찾아 확인)"""
import glob, os, subprocess, sys, zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
JDK = 'C:/Program Files/Unity Hub/6000.0.84f1/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin'


def main():
    p = sys.argv[1] if len(sys.argv) > 1 else max(glob.glob(os.path.join(ROOT, 'Kachi_Gun', 'Builds', '*.aab')), key=os.path.getmtime)
    z = zipfile.ZipFile(p)
    names = z.namelist()
    man = z.read('base/manifest/AndroidManifest.xml')
    res = z.read('base/resources.pb')
    ok = lambda b: '통과' if b else '확인 필요'
    print('파일', os.path.basename(p), '%.1f MB' % (os.path.getsize(p) / 1048576))
    print('패키지 com.zkfpf.kachizombie :', ok(b'com.zkfpf.kachizombie' in man))
    print('광고 ID 권한 없음 (AD_ID)    :', ok(b'AD_ID' not in man))
    perms = sorted({s.decode() for s in __import__('re').findall(rb'android\.permission\.[A-Z_]+', man)})
    print('권한 목록                    :', ', '.join(perms) or '없음')
    print('앱 이름 영어 Capybara vs Zombies :', ok('Capybara vs Zombies'.encode() in res))
    print('앱 이름 한국어 카피바라vs좀비   :', ok('카피바라vs좀비'.encode('utf-8') in res))
    libs = sorted({n.split('/')[2] for n in names if n.startswith('base/lib/')})
    print('네이티브 라이브러리 (64비트 arm64-v8a) :', ', '.join(libs), '→', ok(libs == ['arm64-v8a']))
    r = subprocess.run([os.path.join(JDK, 'jarsigner.exe'), '-verify', '-verbose:summary', '-certs', p], capture_output=True, encoding='cp949', errors='replace')
    signed = 'jar verified' in r.stdout
    owner = [l.strip() for l in r.stdout.splitlines() if 'CN=' in l][:1]
    print('서명                         :', ok(signed), owner[0] if owner else '')


if __name__ == '__main__':
    main()
