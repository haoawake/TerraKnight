"""Refresh the offline changelog from public, published GitHub Releases."""
import json
from pathlib import Path
import urllib.request


def main():
    request = urllib.request.Request(
        'https://api.github.com/repos/haoawake/TerraKnight/releases?per_page=100',
        headers={'User-Agent': 'TerraKnightWiki', 'Accept': 'application/vnd.github+json'})
    with urllib.request.urlopen(request, timeout=45) as response:
        data = json.load(response)
    assert isinstance(data, list)
    releases = [dict(tag=row['tag_name'], name=row['name'], published_at=row['published_at'],
                     url=row['html_url'], body=row['body'] or '', prerelease=row['prerelease'])
                for row in data if not row['draft']]
    assert releases, 'No published release notes returned; keep the existing cache'
    target = Path(__file__).resolve().parent/'releases.json'
    target.write_text(json.dumps(releases, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({'published_releases': len(releases), 'cache': str(target)}))


if __name__ == '__main__':
    main()
