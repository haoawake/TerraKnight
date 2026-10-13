"""Encodes recorded clips (the game's dev/rec/<clip>/00000.jpg...) into clips/<clip>.mp4 (1920 wide)
and a contact sheet each into sheets/<clip>.jpg (a frame a second).

    python encode_clips.py tunnel_kingspass town_walk ...    those clips
    python encode_clips.py --fps 30 old_clip                 clips recorded at 30 fps (the first set)
"""
import argparse, os, re, subprocess
from tools import ffmpeg

HERE = os.path.dirname(os.path.abspath(__file__))
FF = ffmpeg()


def rec_dir():
    """Where the autotest records: <Hollow Knight>/BepInEx/TerraKnight/dev/rec (the game folder from ../LocalPaths.props)."""
    if os.environ.get('TERRAKNIGHT_REC'):
        return os.environ['TERRAKNIGHT_REC']
    props = os.path.join(HERE, '..', 'LocalPaths.props')
    m = re.search(r'<HollowKnightDir>(.*?)</HollowKnightDir>', open(props, encoding='utf-8').read()) if os.path.exists(props) else None
    if not m:
        raise SystemExit('set TERRAKNIGHT_REC, or HollowKnightDir in LocalPaths.props')
    return os.path.join(m.group(1).strip(), 'BepInEx', 'TerraKnight', 'dev', 'rec')


REC = rec_dir()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('clips', nargs='+')
    ap.add_argument('--fps', type=int, default=50)
    a = ap.parse_args()
    for name in a.clips:
        src = os.path.join(REC, name)
        n = len([f for f in os.listdir(src) if f.endswith('.jpg')])
        out = os.path.join(HERE, 'clips', name + '.mp4')
        subprocess.run([FF, '-y', '-loglevel', 'error', '-framerate', str(a.fps), '-i', os.path.join(src, '%05d.jpg'),
                        '-vf', 'scale=1920:-2', '-c:v', 'libx264', '-preset', 'medium', '-crf', '18', '-pix_fmt', 'yuv420p', out], check=True)
        rows = max(1, min(6, (n // a.fps + 5) // 5))
        subprocess.run([FF, '-y', '-loglevel', 'error', '-i', out, '-vf', f'fps=1,scale=384:-2,tile=5x{rows}', '-frames:v', '1',
                        os.path.join(HERE, 'sheets', name + '.jpg')], check=True)
        print(name, n, 'frames', f'{n / a.fps:.1f}s')


if __name__ == '__main__':
    main()
