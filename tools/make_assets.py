# -*- coding: utf-8 -*-
"""Generate the tomato app icon and a soft completion chime."""
import math
import os
import struct
import wave

from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, "assets")
os.makedirs(ASSETS, exist_ok=True)


def lerp(a, b, t):
    return tuple(round(a[i] + (b[i] - a[i]) * t) for i in range(len(a)))


def make_icon():
    size = 1024
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))

    # Soft warm halo behind the tomato.
    halo = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    hd = ImageDraw.Draw(halo)
    hd.ellipse((112, 150, 912, 910), fill=(231, 104, 80, 44))
    halo = halo.filter(ImageFilter.GaussianBlur(46))
    img.alpha_composite(halo)

    # Tomato body: layered radial shading.
    body = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    bd = ImageDraw.Draw(body)
    cx, cy, rx, ry = 512, 566, 348, 326
    steps = 90
    for i in range(steps, 0, -1):
        t = i / steps
        rr_x = rx * t
        rr_y = ry * t
        # light source at upper-left
        color = lerp((255, 139, 108), (205, 65, 49), t ** 0.72)
        bd.ellipse((cx - rr_x, cy - rr_y, cx + rr_x, cy + rr_y), fill=color + (255,))
    img.alpha_composite(body)

    # Upper-left specular highlight.
    gloss = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    gd = ImageDraw.Draw(gloss)
    gd.ellipse((286, 344, 612, 600), fill=(255, 244, 230, 78))
    gloss = gloss.filter(ImageFilter.GaussianBlur(40))
    img.alpha_composite(gloss)

    # Little cheek blush.
    blush = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    bld = ImageDraw.Draw(blush)
    bld.ellipse((636, 590, 790, 700), fill=(255, 122, 96, 58))
    blush = blush.filter(ImageFilter.GaussianBlur(34))
    img.alpha_composite(blush)

    # Leaves: five tapered points, drawn as polygons.
    leaf_layer = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    ld = ImageDraw.Draw(leaf_layer)

    def leaf(angle_deg, length, width, color):
        a = math.radians(angle_deg)
        tip = (cx + math.cos(a) * length, 326 + math.sin(a) * length * 0.72)
        left = (
            cx + math.cos(a - math.pi / 2) * width,
            326 + math.sin(a - math.pi / 2) * width * 0.55,
        )
        right = (
            cx + math.cos(a + math.pi / 2) * width,
            326 + math.sin(a + math.pi / 2) * width * 0.55,
        )
        base = (cx, 330)
        ld.polygon([base, left, tip, right], fill=color)

    leaf(-166, 250, 70, (67, 112, 72, 255))
    leaf(-118, 292, 82, (82, 130, 84, 255))
    leaf(-62, 292, 82, (73, 119, 77, 255))
    leaf(-14, 250, 70, (60, 101, 66, 255))
    leaf(-90, 200, 68, (104, 151, 101, 255))
    img.alpha_composite(leaf_layer)

    # Stem, small curved capsule.
    stem = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    sd = ImageDraw.Draw(stem)
    sd.rounded_rectangle((484, 246, 540, 362), radius=28, fill=(73, 105, 66, 255))
    sd.rounded_rectangle((494, 252, 518, 352), radius=12, fill=(103, 139, 90, 255))
    img.alpha_composite(stem)

    # Tiny green leaf highlight.
    hl = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    hld = ImageDraw.Draw(hl)
    hld.ellipse((460, 300, 552, 342), fill=(171, 200, 148, 74))
    hl = hl.filter(ImageFilter.GaussianBlur(16))
    img.alpha_composite(hl)

    # Outline to keep the silhouette crisp at 16 px.
    outline = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    od = ImageDraw.Draw(outline)
    od.ellipse((cx - rx - 10, cy - ry - 10, cx + rx + 10, cy + ry + 10), outline=(150, 45, 35, 150), width=10)
    img.alpha_composite(outline)

    icon_path = os.path.join(ASSETS, "app.ico")
    sizes = [(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (24, 24), (16, 16)]
    img.save(icon_path, format="ICO", sizes=sizes)
    img.resize((512, 512), Image.LANCZOS).save(os.path.join(ROOT, "预览", "app-icon.png"))
    print("icon:", icon_path)


def make_chime():
    sample_rate = 44100
    duration = 2.65
    n = int(sample_rate * duration)
    samples = [0.0] * n

    # Gentle C-major arpeggio with bell-like partials.
    notes = [
        (0.00, 523.25, 0.20),
        (0.16, 659.25, 0.18),
        (0.32, 783.99, 0.17),
        (0.50, 1046.50, 0.13),
        (0.50, 261.63, 0.10),
    ]
    for start, freq, amp in notes:
        begin = int(start * sample_rate)
        for i in range(begin, n):
            t = (i - begin) / sample_rate
            if t < 0:
                continue
            attack = min(1.0, t / 0.006)
            decay = math.exp(-t / 0.66)
            env = attack * decay
            if env < 1e-5:
                break
            value = (
                math.sin(2 * math.pi * freq * t)
                + 0.30 * math.sin(2 * math.pi * freq * 2.01 * t)
                + 0.10 * math.sin(2 * math.pi * freq * 3.98 * t)
            )
            samples[i] += amp * env * value

    # Global fade-out and normalization.
    peak = max(abs(v) for v in samples) or 1.0
    gain = 0.38 / peak
    fade = int(0.45 * sample_rate)
    for i in range(n):
        g = gain
        if i > n - fade:
            g *= (n - i) / fade
        samples[i] *= g

    path = os.path.join(ASSETS, "chime.wav")
    with wave.open(path, "wb") as wf:
        wf.setnchannels(1)
        wf.setsampwidth(2)
        wf.setframerate(sample_rate)
        frames = bytearray()
        for v in samples:
            frames += struct.pack("<h", int(max(-1.0, min(1.0, v)) * 32767))
        wf.writeframes(bytes(frames))
    print("chime:", path)


if __name__ == "__main__":
    make_icon()
    make_chime()
