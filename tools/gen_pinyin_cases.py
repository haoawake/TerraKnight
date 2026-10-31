# -*- coding: utf-8 -*-
"""从 pinyin.dat 与百科词条生成离线测试用例（供 tools/pinyin-test 使用）。

每个用例：名字、该名字的全拼、首字母。全拼与首字母都按「每个字第一个读音」生成，
只要运行时的 PinyinSearch 正确，这两个查询都必须命中。
"""
import gzip
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CJK = re.compile(r'[\u4e00-\u9fff]')


def load_table():
    path = os.path.join(ROOT, 'src', 'TerraKnight', 'Resources', 'pinyin.dat')
    with gzip.open(path, 'rt', encoding='utf-8') as f:
        table = {}
        for line in f:
            py, _, chars = line.rstrip('\n').partition('\t')
            for ch in chars:
                table.setdefault(ch, []).append(py)
    return table


def spell(name, table, initials=False):
    out = []
    for ch in name:
        if CJK.match(ch):
            pys = table.get(ch)
            if not pys:
                return None
            out.append(pys[0][0] if initials else pys[0])
        elif ch.isascii() and ch.isalnum():
            out.append(ch.lower())
    return ''.join(out)


def main():
    table = load_table()
    source = os.path.join(ROOT, '泰拉圣巢本地百科全书.md')
    text = open(source, encoding='utf-8').read()
    marks = list(re.finditer(r'<a id="(item|enemy)-[^"]+"></a>\s*\n', text))
    names = []
    for i, m in enumerate(marks):
        body = text[m.end(): marks[i + 1].start() if i + 1 < len(marks) else len(text)]
        h = re.match(r'#{2,4} (.+)', body)
        if h:
            names.append(h.group(1).strip())
    cases, skipped = [], []
    for name in names:
        full = spell(name, table)
        init = spell(name, table, initials=True)
        if not full or not init:
            skipped.append(name)
            continue
        cases.append({'name': name, 'full': full, 'initials': init})
    out = os.path.join(ROOT, 'tools', 'pinyin-test', 'cases.json')
    os.makedirs(os.path.dirname(out), exist_ok=True)
    json.dump(cases, open(out, 'w', encoding='utf-8'), ensure_ascii=False, separators=(',', ':'))
    print('cases %d, skipped %d -> %s' % (len(cases), len(skipped), out))
    if skipped:
        print('skipped:', skipped[:20])


if __name__ == '__main__':
    main()
