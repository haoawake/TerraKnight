"""Writes src/TerraKnight/Resources/pinyin.txt (UI/Pinyin.cs: the book's search by pinyin) from pypinyin.

Every character of the CJK Unified Ideographs block (U+4E00-U+9FFF) with its toneless readings, all of them for a
character read more than one way, ü as v. One line a syllable: the syllable, a space, its characters.

usage: python tools/make_pinyin.py   (needs pypinyin, e.g. pip install --target <dir> pypinyin==0.53.0 and PYTHONPATH=<dir>)
"""
import collections
import os

from pypinyin import Style, pinyin

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "TerraKnight", "Resources", "pinyin.txt")

by = collections.defaultdict(list)
for cp in range(0x4E00, 0xA000):
    ch = chr(cp)
    r = pinyin(ch, style=Style.NORMAL, heteronym=True, errors="ignore")
    if not r or not r[0]:
        continue
    for s in dict.fromkeys(r[0]):
        if s.isascii() and s.isalpha():
            by[s].append(ch)
with open(OUT, "w", encoding="utf-8", newline="\n") as f:
    for s, cs in sorted(by.items()):
        f.write(s + " " + "".join(cs) + "\n")
print(sum(len(v) for v in by.values()), "readings,", len(by), "syllables ->", OUT)
