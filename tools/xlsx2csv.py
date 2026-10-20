# -*- coding: utf-8 -*-
"""xlsx → csv (Unity 의 [카치건 → 표 변환] 과 같은 결과). Unity 가 열려 있어 배치 변환을 못 할 때 씀.
사용: python tools/xlsx2csv.py          (바뀐 csv 만 씀)
      python tools/xlsx2csv.py --check  (쓰지 않고 다른 것만 보여 줌)
Unity 는 재생할 때 xlsx 가 더 새로우면 다시 변환하므로, 같은 내용이면 아무것도 바뀌지 않음."""
import glob, io, os, re, sys
import openpyxl

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Kachi_Gun')
SRC = os.path.join(ROOT, 'Table')
OUT = os.path.join(ROOT, 'Assets', 'Resources', 'Tables')


def num(v):
    if isinstance(v, bool): return 'TRUE' if v else 'FALSE'
    if isinstance(v, int): v = float(v)
    s = '%.15G' % v
    if float(s) != v: s = '%.17G' % v
    return s


def cell(v):
    if v is None: return ''
    if isinstance(v, (int, float)): return num(v)
    return str(v)


def esc(s):
    if any(c in s for c in ',"\n\r'): return '"' + s.replace('"', '""') + '"'
    return s


def sheet_csv(ws):
    rows = []
    for r in ws.iter_rows(values_only=True):
        cells = [cell(v) for v in r]
        while cells and cells[-1] == '': cells.pop()
        if cells: rows.append(cells)
    width = len(rows[0]) if rows else 0
    return ''.join(','.join(esc(r[i] if i < len(r) else '') for i in range(width)) + '\n' for r in rows)


def csv_name(sheet): return re.sub(r'_\d+(Ch)?$', '', sheet.strip())


def main(check=False):
    changed = []
    for f in sorted(glob.glob(os.path.join(SRC, '*.xlsx'))):
        if os.path.basename(f).startswith('~$'): continue
        wb = openpyxl.load_workbook(f, data_only=True)
        for ws in wb.worksheets:
            name = csv_name(ws.title)
            text = sheet_csv(ws)
            p = os.path.join(OUT, name + '.csv')
            old = io.open(p, encoding='utf-8-sig', newline='').read() if os.path.exists(p) else None
            if old == text: continue
            changed.append(name)
            if not check:
                with io.open(p, 'w', encoding='utf-8-sig', newline='') as fp: fp.write(text)
    print(('다름: ' if check else '바뀐 csv: ') + (', '.join(changed) if changed else '없음'))
    return changed


if __name__ == '__main__':
    main('--check' in sys.argv)
