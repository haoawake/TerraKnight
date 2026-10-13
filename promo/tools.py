"""What the promo scripts share: where ffmpeg is."""
import os, shutil

HERE = os.path.dirname(os.path.abspath(__file__))


def ffmpeg():
    """ffmpeg: the path in ffmpeg_path.txt (this folder) if there is one, else imageio-ffmpeg's, else the one on PATH."""
    local = os.path.join(HERE, 'ffmpeg_path.txt')
    if os.path.exists(local):
        return open(local).read().strip()
    try:
        import imageio_ffmpeg
        return imageio_ffmpeg.get_ffmpeg_exe()
    except ImportError:
        return shutil.which('ffmpeg') or 'ffmpeg'
