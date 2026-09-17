"""Original procedural graybox audio. Python standard library only; deterministic.

Canonical WAVs live beside this script; stage.py copies them to Unity authoring.
Room export supplies the portable bundle used by both hosts.
No third-party recordings or synthesis libraries are used.
"""
import math
import random
import struct
import wave
from pathlib import Path

RATE = 11025
ROOT = Path(__file__).resolve().parent


def write(name, seconds, synth, periodic=False):
    rng = random.Random(741)
    count = int(RATE * seconds)
    samples = []
    for i in range(count):
        t = i / RATE
        # Short fade edges prevent hard waveform discontinuities.
        edge = 1.0 if periodic else min(1.0, i / 80, (count - 1 - i) / 160)
        value = synth(t, rng) * edge
        assert abs(value) < 0.95, (name, value)
        samples.append(round(value * 32767))
    with wave.open(str(ROOT / (name + '.wav')), 'wb') as output:
        output.setparams((1, 2, RATE, count, 'NONE', 'not compressed'))
        output.writeframes(struct.pack('<' + 'h' * count, *samples))


def ring(t, start, frequency, decay, gain):
    t -= start
    return 0 if t < 0 else gain * math.exp(-t * decay) * math.sin(math.tau * frequency * t)


write('footstep', .24, lambda t, r:
      .28 * math.exp(-t * 24) * math.sin(math.tau * 85 * t)
      + .10 * r.uniform(-1, 1) * math.exp(-t * 19))
write('key', .48, lambda t, r:
      ring(t, 0, 1800, 15, .12) + ring(t, .07, 2600, 18, .10)
      + ring(t, .13, 2100, 14, .08))
write('locked', .42, lambda t, r:
      ring(t, 0, 870, 32, .13) + ring(t, .025, 1530, 45, .09)
      + ring(t, .12, 1060, 34, .14) + ring(t, .145, 1940, 48, .08)
      + ring(t, .23, 780, 38, .08))
write('unlock', .65, lambda t, r:
      ring(t, 0, 1300, 25, .14) + ring(t, .16, 180, 15, .30)
      + ring(t, .24, 95, 10, .12))
# Integral cycles across four seconds: exact periodic waveform, no envelope dip.
write('ambient', 4, lambda t, r:
      .035 * math.sin(math.tau * 55 * t)
      + .018 * math.sin(math.tau * 82.5 * t)
      + .010 * math.sin(math.tau * 110 * t) * (1 + .3 * math.sin(math.tau * .25 * t)), periodic=True)
print('Generated five original mono PCM16 WAVs at 11025 Hz.')
