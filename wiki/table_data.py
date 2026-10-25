"""Read every lazy table page for data audits, without requiring a large browser DOM."""
import json
import re


def expand_tables(page, root):
    def expand(match):
        fragment=match[0]
        key=re.search(r'data-chunk-table="(\d+)"',fragment)[1]
        total=int(re.search(r'data-total="(\d+)"',fragment)[1])
        rows=[]
        for i in range((total+23)//24):
            chunk=json.loads((root/f'data/tables/{key}/{i}.json').read_text('utf-8'))
            assert len(chunk)==min(24,total-i*24)
            rows.extend(chunk)
        assert len(rows)==total
        first=re.search(r'<tbody>(.*?)</tbody>',fragment,re.S)[1]
        assert first==''.join(rows[:24])
        return re.sub(r'<tbody>.*?</tbody>',lambda _: '<tbody>'+''.join(rows)+'</tbody>',fragment,flags=re.S)
    return re.sub(r'<div[^>]*data-chunk-table="\d+"[^>]*>.*?</table></div>',expand,page,flags=re.S)
