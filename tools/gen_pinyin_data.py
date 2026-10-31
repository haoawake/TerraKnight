#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""生成拼音检索数据（游戏插件与 wiki 网页共用一套生成逻辑）。

数据来源：pypinyin（MIT）的 pinyin_dict，取每个字的全部读音（多音字全收），
去掉声调，并把 ü 写成 v（lv / nv / lve / nve），方便全拼输入。

输出格式：gzip 压缩的 UTF-8 文本，每行
    拼音<TAB>汉字串
同一个字会出现在它每个读音的行里。

字集（game 模式，激进裁剪到实际会搜的文本）：
  * 百科全书里的物品/怪物/NPC/配方名 —— 也就是游戏里所有可搜名字的来源；
  * 插件源码与语言资源里出现的中文（界面文案、地区名、名字兜底）。
生成后会逐个检查：物品表里每个汉字是否都有读音，缺的字打出来（pypinyin 也没有读音的
罕见字才需要手工补进 EXTRA）。

用法：
    python tools/gen_pinyin_data.py --mode game
    python tools/gen_pinyin_data.py --mode wiki      # 全量常用汉字，给网页搜索用
"""
import argparse
import gzip
import io
import os
import re
import sys

from pypinyin.contrib.tone_convert import to_normal
from pypinyin.pinyin_dict import pinyin_dict

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)

CJK = re.compile(r'[\u3400-\u4dbf\u4e00-\u9fff\uf900-\ufaff\U00020000-\U0002ffff]')

# pypinyin 没收录的字（确有其字才写在这里）：'字': ['读音', ...]
EXTRA = {}


def readings_by_char():
    """码点 -> 去声调、去重后的读音列表。"""
    out = {}
    for code, toned in pinyin_dict.items():
        ch = chr(code)
        if not CJK.match(ch):
            continue
        seen = []
        for raw in toned.replace('ü', 'v').split(','):
            if not raw:
                continue
            py = to_normal(raw).replace('ü', 'v').lower().replace('ɑ', 'a').replace('ɡ', 'g')
            if py and py not in seen:
                seen.append(py)
        if seen:
            out[ch] = seen
        # pypinyin 的字典值里偶见多个字的组合，按单字取即可
    for ch, pys in EXTRA.items():
        out.setdefault(ch, pys)
    return out


def file_chars(paths):
    chars = set()
    for path in paths:
        try:
            with open(path, encoding='utf-8', errors='ignore') as f:
                chars.update(CJK.findall(f.read()))
        except OSError:
            pass
    return chars


def catalog_chars():
    """百科全书：所有物品、怪物、NPC、配方名的来源文本。"""
    source = os.environ.get('TK_WIKI_SOURCE_ROOT', ROOT)
    book = os.path.join(source, '泰拉圣巢本地百科全书.md')
    found = set()
    for root, _, files in os.walk(os.path.join(source, 'wiki')):
        for name in files:
            if name.endswith('.py'):
                found |= file_chars([os.path.join(root, name)])
    if os.path.exists(book):
        found |= file_chars([book])
    return found


def project_chars():
    """插件源码与语言资源里的中文：界面文案、地区名、名字兜底。"""
    paths = []
    for folder in ('src',):
        for base, _, files in os.walk(os.path.join(ROOT, folder)):
            for name in files:
                if name.endswith(('.cs', '.json', '.txt')):
                    paths.append(os.path.join(base, name))
    path = os.path.join(ROOT, 'wiki-source.json')
    if os.path.exists(path):
        paths.append(path)
    return file_chars(paths)


def chars_for(mode):
    table = readings_by_char()
    if mode == 'wiki':
        return set(table)
    wanted = catalog_chars() | project_chars()
    have = {c for c in wanted if c in table}
    missing = sorted(wanted - set(have))
    if missing:
        print('pypinyin 与 EXTRA 都没有读音的字（需要人工补）：%s' % ''.join(missing))
    print('物品表/源码用字 %d，其中有读音 %d' % (len(wanted), len(have)))
    return have


def build(mode):
    table = readings_by_char()
    chars = chars_for(mode)
    rows = {}
    for ch in chars:
        for py in table[ch]:
            rows.setdefault(py, []).append(ch)
    lines = []
    for py in sorted(rows):
        lines.append(py + '\t' + ''.join(sorted(rows[py])))
    return '\n'.join(lines) + '\n'


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--mode', choices=('game', 'wiki'), default='game')
    ap.add_argument('--out', default=None)
    ap.add_argument('--plain', action='store_true', help='不压缩，直接输出文本')
    args = ap.parse_args()
    text = build(args.mode)
    out = args.out or os.path.join(ROOT, 'src', 'TerraKnight', 'Resources', 'pinyin.dat')
    data = text.encode('utf-8')
    if args.plain:
        payload = data
    else:
        buf = io.BytesIO()
        with gzip.GzipFile(fileobj=buf, mode='wb', compresslevel=9, mtime=0) as gz:
            gz.write(data)
        payload = buf.getvalue()
    os.makedirs(os.path.dirname(out), exist_ok=True)
    with open(out, 'wb') as f:
        f.write(payload)
    print('%s: %d 行, 未压缩 %d 字节, 写入 %d 字节 -> %s' % (
        args.mode, text.count('\n'), len(data), len(payload), out))


if __name__ == '__main__':
    main()
