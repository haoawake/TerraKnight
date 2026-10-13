"""A contact sheet with each frame's time written on it, for picking a shot's in-point.

    python timesheet.py clip [step seconds] [from] [to]   ->  sheets/<clip>_t.jpg
"""
import os, subprocess, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from tools import ffmpeg

HERE = os.path.dirname(os.path.abspath(__file__))
FF = ffmpeg()


def main():
    clip = sys.argv[1]
    step = float(sys.argv[2]) if len(sys.argv) > 2 else 0.5
    t0 = float(sys.argv[3]) if len(sys.argv) > 3 else 0.0
    path = os.path.join(HERE, 'clips', clip + '.mp4')
    out = subprocess.run([FF, '-i', path], stderr=subprocess.PIPE, text=True, errors='replace').stderr
    dur = 0.0
    for line in out.splitlines():
        if 'Duration:' in line:
            h, m, s = line.split('Duration:')[1].split(',')[0].strip().split(':')
            dur = int(h) * 3600 + int(m) * 60 + float(s)
    t1 = float(sys.argv[4]) if len(sys.argv) > 4 else dur
    font = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 22)
    tiles = []
    t = t0
    while t < t1 - 0.01:
        raw = subprocess.run([FF, '-loglevel', 'quiet', '-ss', f'{t:.3f}', '-i', path, '-frames:v', '1', '-vf', 'scale=384:240',
                              '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-'], stdout=subprocess.PIPE).stdout
        if len(raw) < 384 * 240 * 3:
            break
        im = Image.fromarray(np.frombuffer(raw, np.uint8).reshape(240, 384, 3).copy())
        d = ImageDraw.Draw(im)
        d.rectangle((0, 0, 70, 26), fill=(0, 0, 0))
        d.text((4, 2), f'{t:.1f}', font=font, fill=(255, 255, 0))
        tiles.append(im)
        t += step
    cols = 6
    rows = (len(tiles) + cols - 1) // cols
    sheet = Image.new('RGB', (384 * cols, 240 * rows))
    for i, im in enumerate(tiles):
        sheet.paste(im, ((i % cols) * 384, (i // cols) * 240))
    sheet.save(os.path.join(HERE, 'sheets', clip + '_t.jpg'), quality=85)
    print(clip, len(tiles), 'frames')


if __name__ == '__main__':
    main()
