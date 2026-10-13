"""Synthesises the trailer's music: an original 140 BPM electronic track (no samples, nothing borrowed),
about a minute and a quarter: a 4-bar intro, a 4-bar groove (half-time drums, a riser), a 12-bar drop,
an 8-bar breakdown whose drums come back in its second half, a bigger 12-bar drop and a 3-bar outro
with a final hit. Writes music.wav and beats.json (each beat's time, and the bar starts and section
starts) for cutting on the beat."""
import json, os
import numpy as np
from scipy import signal
from scipy.io import wavfile

SR = 44100
BPM = 140
BEAT = 60.0 / BPM
BAR = BEAT * 4
SECTIONS = [('intro', 4), ('build', 4), ('drop1', 12), ('break', 8), ('drop2', 12), ('outro', 3)]
TOTAL_BARS = sum(n for _, n in SECTIONS)
LENGTH = TOTAL_BARS * BAR + 2.5  # the outro's tail rings on
N = int(LENGTH * SR)
rng = np.random.default_rng(7)
HERE = os.path.dirname(os.path.abspath(__file__))

L = np.zeros(N)   # drums
R = np.zeros(N)
ML = np.zeros(N)  # music (ducked by the kick)
MR = np.zeros(N)


def add(buf, start, x, gain=1.0):
    i = int(start * SR)
    if i >= N:
        return
    j = min(N, i + len(x))
    buf[i:j] += x[:j - i] * gain


def stereo(start, x, gain=1.0, pan=0.0, music=False):
    add(ML if music else L, start, x, gain * (1 - max(0, pan)))
    add(MR if music else R, start, x, gain * (1 + min(0, pan)))


def env(n, a=0.002, d=0.2, s=0.0, total=None):
    t = np.arange(n) / SR
    e = np.minimum(1, t / max(a, 1e-4)) * np.exp(-t / max(d, 1e-4))
    return e * (1 - s) + s * np.minimum(1, t / max(a, 1e-4))


def kick():
    n = int(0.45 * SR)
    t = np.arange(n) / SR
    f = 45 + 110 * np.exp(-t * 28)
    ph = 2 * np.pi * np.cumsum(f) / SR
    x = np.sin(ph) * np.exp(-t * 7.5)
    click = rng.standard_normal(n) * np.exp(-t * 400) * 0.3
    return np.tanh((x + click) * 2.2) * 0.9


def snare():
    n = int(0.28 * SR)
    t = np.arange(n) / SR
    noise = rng.standard_normal(n)
    b, a = signal.butter(2, [1500 / (SR / 2), 9000 / (SR / 2)], 'band')
    noise = signal.lfilter(b, a, noise) * np.exp(-t * 16)
    tone = np.sin(2 * np.pi * 190 * t) * np.exp(-t * 30) * 0.6
    return (noise * 0.9 + tone) * 0.7


def hat(open_=False):
    n = int((0.22 if open_ else 0.05) * SR)
    t = np.arange(n) / SR
    noise = rng.standard_normal(n)
    b, a = signal.butter(2, 7000 / (SR / 2), 'high')
    return signal.lfilter(b, a, noise) * np.exp(-t * (12 if open_ else 70)) * 0.35


def saw(freq, n, detune=0.0):
    t = np.arange(n) / SR
    return signal.sawtooth(2 * np.pi * freq * (1 + detune) * t + rng.uniform(0, 6.28))


def lowpass(x, cutoff, order=2):
    b, a = signal.butter(order, min(0.99, cutoff / (SR / 2)), 'low')
    return signal.lfilter(b, a, x)


def note(m):
    return 440.0 * 2 ** ((m - 69) / 12)


# A minor, i–VI–III–VII: Am, F, C, G.
CHORDS = [[57, 60, 64], [53, 57, 60], [48, 52, 55], [55, 59, 62]]
BASS = [45, 41, 48, 43]

sidechain = np.ones(N)
section_starts = {}
bar0 = 0
for name, bars in SECTIONS:
    section_starts[name] = bar0 * BAR
    bar0 += bars

K, S, H, HO = kick(), snare(), hat(), hat(True)
beats = []
for bar in range(TOTAL_BARS):
    t_bar = bar * BAR
    sec = next(nm for nm, _ in SECTIONS if section_starts[nm] <= t_bar + 1e-6 and (t_bar < section_starts[nm] + dict(SECTIONS)[nm] * BAR - 1e-6))
    chord = CHORDS[bar % 4]
    root = BASS[bar % 4]
    for b in range(4):
        tb = t_bar + b * BEAT
        beats.append(round(tb, 4))
        if sec in ('drop1', 'drop2') or (sec == 'outro' and bar == TOTAL_BARS - 3 and b == 0):
            stereo(tb, K, 1.0)
            i = int(tb * SR)
            duck = 1 - 0.75 * np.exp(-np.arange(int(BEAT * SR)) / SR * 9)
            j = min(N, i + len(duck))
            sidechain[i:j] = np.minimum(sidechain[i:j], duck[:j - i])
        if sec in ('drop1', 'drop2') and b in (1, 3):
            stereo(tb, S, 0.8)
        if sec in ('drop1', 'drop2'):
            for h in range(2 if sec == 'drop1' else 4):
                th = tb + h * BEAT / (2 if sec == 'drop1' else 4)
                stereo(th, HO if (sec == 'drop2' and h == 2) else H, 0.6, pan=0.3 if h % 2 else -0.3)
        if sec == 'break' and b % 2 == 0:
            stereo(tb, H, 0.35)
        # The groove: half-time kick and snare, eighth hats.
        if sec == 'build':
            if b in (0, 2) or (b == 3 and bar % 2 == 1):
                stereo(tb, K, 0.75)
            if b == 2:
                stereo(tb, S, 0.55)
            for hh in range(2):
                stereo(tb + hh * BEAT / 2, H, 0.45, pan=0.25 if hh else -0.25)
        # The breakdown's second half: the kick back on every beat, a snare roll in its last bar.
        brk = int(section_starts['break'] / BAR)
        if sec == 'break' and bar >= brk + 4:
            stereo(tb, K, 0.6 if bar < brk + 7 else 0.8)
            if bar == brk + 7:
                for r in range(4):
                    stereo(tb + r * BEAT / 4, S, 0.25 + 0.12 * b + 0.03 * r)
    # Bass: eighths on the root (an octave jump on the off-beats in the second drop).
    if sec in ('drop1', 'drop2'):
        for e in range(8):
            m = root + (12 if (sec == 'drop2' and e % 2) else 0)
            n = int(BEAT / 2 * SR * 0.95)
            x = saw(note(m - 12), n) * 0.5 + saw(note(m - 12), n, 0.004) * 0.5
            x = lowpass(x, 900 if sec == 'drop1' else 1400) * env(n, 0.003, 0.18)
            add(ML, t_bar + e * BEAT / 2, x, 0.55)
            add(MR, t_bar + e * BEAT / 2, x, 0.55)
    # Lead: a sixteenth arpeggio of the chord, a supersaw opened up in the second drop.
    if sec in ('drop1', 'drop2', 'break', 'build'):
        pattern = [0, 1, 2, 1, 2, 0, 1, 2] * 2
        for s16 in range(16):
            if sec == 'break' and s16 % 4:
                continue
            if sec == 'build' and s16 % 2:
                continue
            m = chord[pattern[s16]] + 12 + (12 if sec == 'drop2' and s16 % 8 >= 6 else 0)
            n = int(BEAT / 4 * SR * (0.9 if sec != 'break' else 3.5))
            x = sum(saw(note(m), n, d) for d in (-0.008, -0.003, 0.0, 0.003, 0.008)) / 5
            cut = 2500 if sec == 'drop1' else 5200 if sec == 'drop2' else 900 + 250 * (bar - int(section_starts['build'] / BAR)) * 2 if sec == 'build' else 1200
            x = lowpass(x, cut) * env(n, 0.004, 0.12 if sec != 'break' else 0.5)
            pan = -0.35 if s16 % 2 else 0.35
            stereo(t_bar + s16 * BEAT / 4, x, 0.32 if sec != 'break' else 0.3, pan, music=True)
    # Pads under the intro, the breakdown and the outro.
    if sec in ('intro', 'break', 'outro', 'build'):
        n = int(BAR * SR)
        x = sum(saw(note(m), n, d) for m in chord for d in (-0.006, 0.006)) / 6
        t = np.arange(n) / SR
        prog = (bar - int(section_starts[sec] / BAR) + t / BAR) / dict(SECTIONS)[sec]
        cut = 400 + 3000 * prog.mean() if sec != 'outro' else 1800
        x = lowpass(x, cut) * np.minimum(1, t / 0.3) * np.minimum(1, (BAR - t) / 0.2)
        stereo(t_bar, x, 0.35, music=True)

# Risers into each drop: noise swept up, and a rising tone.
for into in ('drop1', 'drop2'):
    t_end = section_starts[into]
    d = BAR * 2
    n = int(d * SR)
    t = np.arange(n) / SR
    noise = rng.standard_normal(n)
    out = np.zeros(n)
    for k in range(16):
        a, b_ = int(k * n / 16), int((k + 1) * n / 16)
        f = 500 * (16 ** (k / 15))
        bb, aa = signal.butter(2, min(0.99, f / (SR / 2)), 'high')
        out[a:b_] = signal.lfilter(bb, aa, noise[a:b_])
    tone = np.sin(2 * np.pi * np.cumsum(200 * (8 ** (t / d))) / SR) * 0.25
    x = (out * 0.35 + tone) * (t / d) ** 2
    stereo(t_end - d, x, 0.6)
    # The impact at the drop.
    n2 = int(1.8 * SR)
    t2 = np.arange(n2) / SR
    boom = np.sin(2 * np.pi * np.cumsum(30 + 60 * np.exp(-t2 * 6)) / SR) * np.exp(-t2 * 2.2)
    crash = signal.lfilter(*signal.butter(2, 5000 / (SR / 2), 'high'), rng.standard_normal(n2)) * np.exp(-t2 * 2.5) * 0.3
    stereo(t_end, boom * 0.9 + crash, 0.8)

# The final hit at the outro's start, ringing out.
t_hit = section_starts['outro']
n3 = int(4 * SR)
t3 = np.arange(n3) / SR
hit = np.sin(2 * np.pi * np.cumsum(32 + 70 * np.exp(-t3 * 5)) / SR) * np.exp(-t3 * 1.3)
hit += signal.lfilter(*signal.butter(2, 4000 / (SR / 2), 'high'), rng.standard_normal(n3)) * np.exp(-t3 * 1.6) * 0.35
chord_hit = sum(saw(note(m), n3, d) for m in (45, 57, 60, 64, 69) for d in (-0.005, 0.005)) / 10
hit += lowpass(chord_hit, 2200) * np.exp(-t3 * 0.9) * 0.6
stereo(t_hit, hit, 0.9)

mix = np.stack([L + ML * sidechain, R + MR * sidechain], axis=1)
mix = np.tanh(mix * 1.6) / np.tanh(1.6)
mix /= max(1e-6, np.abs(mix).max()) / 0.95
fade = int(0.8 * SR)
mix[-fade:] *= np.linspace(1, 0, fade)[:, None]
wavfile.write(os.path.join(HERE, 'music.wav'), SR, (mix * 32767).astype(np.int16))
bars = [round(i * BAR, 4) for i in range(TOTAL_BARS + 1)]
json.dump({'bpm': BPM, 'beat': BEAT, 'bar': BAR, 'beats': beats, 'bars': bars,
           'sections': {k: round(v, 4) for k, v in section_starts.items()}, 'length': round(LENGTH, 3)},
          open(os.path.join(HERE, 'beats.json'), 'w'), indent=1)
print('music', round(LENGTH, 2), 's; sections', {k: round(v, 2) for k, v in section_starts.items()})
