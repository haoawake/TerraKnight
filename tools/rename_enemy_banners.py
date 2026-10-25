"""Rename existing banner assets to exact roster IDs without changing image bytes."""
import csv
import hashlib
import json
import re
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PACKAGE = ROOT / '空洞骑士怪物旗帜素材包'


def write_mapping(package, rows):
    mapping = {row['id']: row['file'] for row in rows if row['status'] == 'generated'}
    (package / 'banner-map.json').write_text(json.dumps(mapping, ensure_ascii=False, indent=2), encoding='utf-8')
    with (package / '旗帜名称对照.csv').open('w', encoding='utf-8-sig', newline='') as output:
        writer = csv.writer(output)
        writer.writerow(['怪物内部ID', '中文名', '英文名', '分类', '阶段', '旗帜文件名', '旗帜路径', '怪物参考图路径'])
        for row in rows:
            if row['status'] == 'generated':
                writer.writerow([row['id'], row['name'], row['en'], row['category'], row['stage'], Path(row['file']).name, row['file'], row['reference_file']])
    (package / '直接映射说明.md').write_text('''# 按怪物 ID 直接映射

四个图片目录中的文件均使用怪物图鉴内部 ID，保持大小写和 @ 后缀，不加编号或中文前缀。

- 阴影爬虫：`旗帜_16x48/AbyssCrawler.png`
- 躯壳矿工：`旗帜_16x48/ZombieMiner.png`
- 阶段 0 的躯壳矿工：`旗帜_16x48/ZombieMiner@0.png`

原尺寸旗帜、8 倍预览、绘制原图及怪物参考图的文件名相同，目录分别为 `旗帜_16x48`、`预览_8倍`、`绘制原图`、`怪物参考`。可直接按文件名配对。

`banner-map.json` 是「图鉴内部 ID → 旗帜相对路径」字典；`旗帜名称对照.csv` 可用 Excel 打开；`预览.html` 可逐个对照怪物与旗帜。

程序已有图鉴 ID 时直接使用 `旗帜_16x48/<ID>.png`。游戏内按敌人日志 record 与场景阶段获取变体时，沿用 `EnemyDrops.KeyFor(record, EnemyDrops.VariantLevel(sceneName))`，然后以返回的完整 ID 查找图片；不要使用 GameObject 实例名，也不要把 `@0` 改成 `-0` 或删除，否则会丢失变体区分。加载器若不支持 @，应使用自定义映射表，保持图鉴 ID 不变。

此包只有美术素材，文件重命名本身不添加游戏中的旗帜逻辑。
''', encoding='utf-8')


def main():
    rows = json.loads((PACKAGE / 'manifest.json').read_text(encoding='utf-8'))
    source_ids = set(re.findall(r'\["([^"]+)"\] = new Entry', (ROOT / 'src/TerraKnight/Items/EnemyDropData.cs').read_text(encoding='utf-8-sig')))
    replacements = {}
    changes = []
    before = {}
    for row in rows:
        assert row['id'] in source_ids, row['id']
        for field in ['file', 'preview', 'original', 'reference_file']:
            old = PACKAGE / row[field]
            new = old.with_name(row['id'] + '.png')
            assert old.resolve().is_relative_to(PACKAGE.resolve()) and new.resolve().is_relative_to(PACKAGE.resolve())
            digest = hashlib.sha256(old.read_bytes()).hexdigest()
            if field == 'file':
                assert digest == row['sha256'], row['id']
            if old != new:
                assert not new.exists(), new
                changes.append((old, new, digest))
                replacements[row[field]] = new.relative_to(PACKAGE).as_posix()
            before[(row['id'], field)] = digest
            row[field] = new.relative_to(PACKAGE).as_posix()
    assert len({str(new).lower() for _, new, _ in changes}) == len(changes)
    for old, new, digest in changes:
        old.rename(new)
        assert hashlib.sha256(new.read_bytes()).hexdigest() == digest
    for filename in ['预览.html']:
        path = PACKAGE / filename
        text = path.read_text(encoding='utf-8')
        for old, new in replacements.items():
            text = text.replace(old, new)
        path.write_text(text, encoding='utf-8')
    (PACKAGE / 'manifest.json').write_text(json.dumps(rows, ensure_ascii=False, indent=2), encoding='utf-8')
    write_mapping(PACKAGE, rows)
    readme = PACKAGE / 'README.md'
    text = readme.read_text(encoding='utf-8')
    if '直接映射说明.md' not in text:
        readme.write_text(text + '\n图片已按完整内部 ID 命名，可直接映射；详见 `直接映射说明.md`、`banner-map.json` 和 `旗帜名称对照.csv`。\n', encoding='utf-8')
    for field, directory in [('file', '旗帜_16x48'), ('preview', '预览_8倍'), ('original', '绘制原图'), ('reference_file', '怪物参考')]:
        actual = {p.name for p in (PACKAGE / directory).glob('*.png')}
        assert actual == {row['id'] + '.png' for row in rows}, directory
        for row in rows:
            assert hashlib.sha256((PACKAGE / row[field]).read_bytes()).hexdigest() == before[(row['id'], field)]
    validation = json.loads((PACKAGE / 'validation.json').read_text(encoding='utf-8'))
    validation.update(naming='exact-roster-id', mapped_ids=len(rows), renamed_files=len(changes), image_bytes_unchanged=True, source_ids_verified=True)
    (PACKAGE / 'validation.json').write_text(json.dumps(validation, ensure_ascii=False, indent=2), encoding='utf-8')
    archive = ROOT / 'artifacts/空洞骑士怪物旗帜素材包-ID映射版.zip'
    with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as bundle:
        for path in sorted(PACKAGE.rglob('*')):
            if path.is_file():
                bundle.write(path, path.relative_to(PACKAGE.parent))
    with zipfile.ZipFile(archive) as bundle:
        assert bundle.testzip() is None
        assert sum('/旗帜_16x48/' in name and name.endswith('.png') for name in bundle.namelist()) == len(rows)
    print(json.dumps({'mapped_ids': len(rows), 'renamed_files': len(changes), 'image_bytes_unchanged': True, 'archive': str(archive)}, ensure_ascii=False))


if __name__ == '__main__':
    main()
