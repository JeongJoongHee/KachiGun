# 남은 작업 정리.html 의 체크 상태 읽기 — 체크는 html 파일이 아니라 Chrome localStorage 에 저장됨.
# Chrome 의 Local Storage(leveldb) 를 직접 풀어 (.ldb = 스내피 압축 표, .log = 쓰기 기록) 키 값을 찾고 항목 이름과 맞춰 보여 줌.
# 10-05 부터 의견(결정 항목 글자를 누르면 열리는 칸)도 같이 저장·출력. 한글 출력이라 PYTHONUTF8=1 로 실행
# 사용: python tools/todo_checks.py [키]   (기본 키 = 문서 안 script 의 KEY)
import glob, html, json, os, re, struct, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DOC = os.path.join(ROOT, "남은_작업_정리.html")
DIR = os.path.expandvars(r"%LOCALAPPDATA%\Google\Chrome\User Data\Default\Local Storage\leveldb")


def varint(b, i):
    r = s = 0
    while True:
        c = b[i]; i += 1
        r |= (c & 0x7F) << s; s += 7
        if c < 0x80: return r, i


def snappy(b):
    _, i = varint(b, 0)
    out = bytearray()
    while i < len(b):
        t = b[i]; i += 1
        k = t & 3
        if k == 0:
            ln = t >> 2
            if ln >= 60:
                nb = ln - 59; ln = int.from_bytes(b[i:i + nb], "little"); i += nb
            ln += 1
            out += b[i:i + ln]; i += ln
            continue
        if k == 1:
            ln = ((t >> 2) & 7) + 4; off = ((t >> 5) << 8) | b[i]; i += 1
        elif k == 2:
            ln = (t >> 2) + 1; off = int.from_bytes(b[i:i + 2], "little"); i += 2
        else:
            ln = (t >> 2) + 1; off = int.from_bytes(b[i:i + 4], "little"); i += 4
        for _ in range(ln): out.append(out[-off])
    return bytes(out)


def block(data, off, size):
    raw = data[off:off + size]
    return snappy(raw) if data[off + size] == 1 else raw


def entries(blk):
    nrest = struct.unpack("<I", blk[-4:])[0]
    end = len(blk) - 4 - 4 * nrest
    i = 0; key = b""
    while i < end:
        shared, i = varint(blk, i); nonshared, i = varint(blk, i); vlen, i = varint(blk, i)
        key = key[:shared] + blk[i:i + nonshared]; i += nonshared
        yield key, blk[i:i + vlen]; i += vlen


def latest(keyname):
    key = keyname.encode()
    found = []   # (순서, 값)
    for f in glob.glob(os.path.join(DIR, "*.ldb")):
        data = open(f, "rb").read()
        foot = data[-48:]
        _, j = varint(foot, 0); _, j = varint(foot, j)
        io, j = varint(foot, j); isz, j = varint(foot, j)
        for _, h in entries(block(data, io, isz)):
            bo, k = varint(h, 0); bs, k = varint(h, k)
            for k2, val in entries(block(data, bo, bs)):
                if k2[:-8].endswith(key):
                    found.append((struct.unpack("<Q", k2[-8:])[0] >> 8, val))
    logs = sorted(glob.glob(os.path.join(DIR, "*.log")), key=lambda f: int(re.sub(r"\D", "", os.path.basename(f)) or 0))
    for n, f in enumerate(logs):   # 아직 표로 안 옮겨진 최신 기록 — 같은 파일 안에선 뒤에 쓴 것이 최신
        data = open(f, "rb").read()
        p = 0
        while (p := data.find(key, p)) >= 0:
            ln, q = varint(data, p + len(key))
            found.append(((1 << 62) + (n << 32) + p, data[q:q + ln])); p += 1
    if not found: return None
    val = max(found, key=lambda x: x[0])[1]
    return val[1:].decode("latin-1") if val[:1] == b"\x01" else val[1:].decode("utf-16-le")


doc = open(DOC, encoding="utf-8").read()
keyname = sys.argv[1] if len(sys.argv) > 1 else re.search(r"KEY='([^']+)'", doc).group(1)
raw = latest(keyname)
print(f"키 {keyname}: {raw}")
data = json.loads(raw) if raw else {}
if "c" in data or "n" in data or (not data and "data-id=\"" in doc):
    # 10-05 형식: {"c":{항목id:true}, "n":{항목id:"의견"}} — 항목id = li data-id
    checks, notes = data.get("c", {}), data.get("n", {})
    # 10-12: 단계 목록(ol.steps)이 들어 있어 여러 줄인 항목도 읽게 re.S · 공백 정리
    items = re.findall(r'<li data-id="([^"]+)"><label><input type="checkbox"><span>(.*?)</span></label></li>', doc, re.S)
    for id_, t in items:
        t = " ".join(html.unescape(re.sub("<[^>]+>", " ", t)).split())
        note = (notes.get(id_) or "").strip()
        print(("V " if checks.get(id_) else "  ") + f"{id_:18} " + t[:100])
        if note: print("     의견: " + note.replace(chr(10), chr(10) + " " * 11))
    known = {i for i, _ in items}
    for id_ in sorted(set(checks) | set(notes)):
        if id_ not in known: print(f"(문서에 없는 항목 {id_}: 체크 {checks.get(id_)} 의견 {notes.get(id_)})")
    print(f"체크 {sum(1 for i, _ in items if checks.get(i))} / {len(items)} · 의견 {sum(1 for i, _ in items if (notes.get(i) or '').strip())}")
else:
    # 10-04 까지 형식: {"번호":true}, 번호 = ul.todo input 순서
    on = {int(k) for k, v in data.items() if v is True}
    items = re.findall(r'<input type="checkbox"><span>(.*?)</span></label>', doc)
    for i, t in enumerate(items):
        t = html.unescape(re.sub("<[^>]+>", "", t))
        print(("V " if i in on else "  ") + f"{i:2} " + t[:110])
    print(f"체크 {len(on)} / {len(items)}")
