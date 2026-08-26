#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""產生八個 mod 的 Thunderstore 圖示（`mods/<專案>/icon.png`）。

    python .github/tools/make_icons.py

Thunderstore 只收 **剛好 256x256 的 PNG**，而清單上真正被看到的尺寸更小
（r2modman 大約 48px）。所以規則只有一條：**一個字符，白色，佔畫面一半，
其餘留白**。塞第二個元素、或把字符放大到填滿，縮到 48px 就都糊掉了。

第二條規則是實際看過草稿才補上的：**畫遊戲裡的那個東西，不要畫類別的通用符號**。
RadioMusic 第一版畫音符，那只說得出「這是音樂 mod」；畫成收音機才說得出
「這是改那台收音機的 mod」。HudNumbers 第一版畫愛心，同樣的毛病——愛心跟
「把數值顯示出來」沒有關係，改成畫它自己那塊面板才對得上。
同理 AmmoCounter 畫子彈、Economy 畫硬幣上的魚。只有本來就沒有對應實體的
（Guardian 的保護、ConfigMenu 的設定）才用通用符號。

設計取自 Modrinth 上那批效能 mod 的圖示：飽和的中間調底色 + 純白剪影，
底色由上到下**微微變亮**（不是全平，也不是深色漸層）。字符佔 48-55%。

顏色帶功能，不只是好看：**冷色 = 只有你自己要裝，暖色 = 只有房主要裝**。
八張排在一起時，一眼就分得出哪些是給房主的。

畫法是 4 倍超取樣後 LANCZOS 縮到 256——Pillow 的多邊形沒有反鋸齒，
直接畫 256 會有階梯邊。
"""

import math
import os
import sys

try:
    from PIL import Image, ImageDraw
except ImportError:
    sys.exit("需要 Pillow：pip install Pillow")

try:                                # Pillow 10 拿掉了 Image.LANCZOS 這種舊名字
    RESAMPLE = Image.Resampling.LANCZOS
except AttributeError:              # Pillow < 9.1
    RESAMPLE = Image.LANCZOS

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
MODS = os.path.join(ROOT, "mods")

SIZE = 256
SS = 4                              # 超取樣倍率
N = SIZE * SS
WHITE = (255, 255, 255)

# 冷色 = 只有你自己要裝
GREEN  = (79, 180, 119)
BLUE   = (62, 142, 208)
PURPLE = (138, 95, 203)
TEAL   = (42, 166, 160)
SLATE  = (85, 104, 137)
# 暖色 = 只有房主要裝
ORANGE = (224, 138, 60)
RED    = (206, 90, 78)
GOLD   = (217, 165, 33)


def shade(c, k):
    """k > 1 變亮，k < 1 變暗。"""
    return tuple(max(0, min(255, int(v * k))) for v in c)


def canvas(base):
    """飽和底色，由上到下微微變亮——參考圖量出來就是這個方向。"""
    top, bottom = shade(base, 0.90), shade(base, 1.10)
    img = Image.new("RGB", (N, N), base)
    d = ImageDraw.Draw(img)
    for y in range(N):
        t = y / float(N - 1)
        d.line([(0, y), (N, y)],
               fill=tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3)))
    return img, ImageDraw.Draw(img), bottom


# --------------------------------------------------------------------------
# 字符。座標一律用 N 的比例寫，改 SIZE 不用重畫。
# --------------------------------------------------------------------------

def hud_panel(d, cx, cy, w, h, fill):
    """這個 mod 畫在畫面角落的那塊面板：左邊標籤、右邊數值，三行。

    走過兩條死路才到這裡，都記下來免得再走一次：

    愛心——只說得出「跟血量有關」，跟「把數值顯示出來」沒有關係。
    七段顯示器的數字——名字對了，但段與段之間的縫跟筆畫一樣粗，
    縮到 48px 整組糊成兩塊，兩位數也救不回來。

    面板同時說到了 HUD 和 Numbers，而且線條夠少，縮小之後仍然是
    「一塊有幾行讀數的框」。
    """
    t = h * 0.09
    left, top = cx - w / 2.0, cy - h / 2.0
    d.rounded_rectangle([left, top, left + w, top + h], radius=h * 0.16,
                        outline=fill, width=int(t))
    # 左短右更短＝「名稱：數值」。長度各行不同，才不會看起來像清單
    for i, (label, value) in enumerate(((0.30, 0.22), (0.24, 0.30), (0.34, 0.16))):
        y = top + h * (0.28 + 0.22 * i)
        d.rounded_rectangle([left + w * 0.13, y - t / 2.0,
                             left + w * (0.13 + label), y + t / 2.0],
                            radius=t / 2.0, fill=fill)
        d.rounded_rectangle([left + w * 0.87 - w * value, y - t / 2.0,
                             left + w * 0.87, y + t / 2.0],
                            radius=t / 2.0, fill=fill)


def shield(d, cx, cy, w, h, fill):
    """兩肩 -> 右側下到尖端 -> 左側倒回來。左右兩串接反會自交成一片葉子。"""
    top, half, knee = cy - h / 2.0, w / 2.0, 0.46
    right, left = [], []
    for i in range(61):
        t = i / 60.0
        y = top + h * (knee + (1 - knee) * t)
        x = half * math.cos(t * math.pi / 2.0) ** 0.62
        right.append((cx + x, y))
        left.append((cx - x, y))
    d.polygon([(cx - half, top), (cx + half, top)] + right + list(reversed(left)),
              fill=fill)


def bullet(d, cx, cy, w, h, fill, bg):
    """彈頭 + 彈殼。中間那道背景色的縫是彈殼口，少了它只是一根膠囊。"""
    half, top = w / 2.0, cy - h / 2.0
    ogive = h * 0.38
    pts = []
    for i in range(41):                       # 右半邊的彈頭曲線
        t = i / 40.0
        pts.append((cx + half * math.sin(t * math.pi / 2.0) ** 0.75, top + ogive * t))
    pts.append((cx + half, cy + h / 2.0))
    pts.append((cx - half, cy + h / 2.0))
    for i in range(41):
        t = 1 - i / 40.0
        pts.append((cx - half * math.sin(t * math.pi / 2.0) ** 0.75, top + ogive * t))
    d.polygon(pts, fill=fill)
    gap = h * 0.035
    d.rectangle([cx - half, top + h * 0.46, cx + half, top + h * 0.46 + gap], fill=bg)


def sliders(d, cx, cy, w, h, fill, bg):
    """三根滑桿。旋鈕中心挖一個背景色的洞，不然只是三條有腫塊的線。"""
    bar = h * 0.10
    knob = bar * 1.9
    for i, at in enumerate((0.32, 0.66, 0.46)):
        y = cy - h / 2.0 + h * (0.16 + 0.34 * i)
        d.rounded_rectangle([cx - w / 2.0, y - bar / 2.0, cx + w / 2.0, y + bar / 2.0],
                            radius=bar / 2.0, fill=fill)
        kx = cx - w / 2.0 + w * at
        d.ellipse([kx - knob, y - knob, kx + knob, y + knob], fill=fill)
        d.ellipse([kx - knob * 0.36, y - knob * 0.36,
                   kx + knob * 0.36, y + knob * 0.36], fill=bg)


def coin_fish(d, cx, cy, r, fill, bg):
    """硬幣上打一條魚。錢的來源就是魚，Economy 是同一條曲線的兩端。"""
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=fill)
    bl, bh = r * 1.06, r * 0.56           # 魚身
    fx = cx + r * 0.13
    d.ellipse([fx - bl / 2.0, cy - bh / 2.0, fx + bl / 2.0, cy + bh / 2.0], fill=bg)
    tail = fx - bl / 2.0                  # 魚尾
    d.polygon([(tail + r * 0.06, cy),
               (tail - r * 0.42, cy - r * 0.34),
               (tail - r * 0.42, cy + r * 0.34)], fill=bg)
    eye = r * 0.09                        # 眼睛用白色挖回來
    ex, ey = fx + bl * 0.26, cy - bh * 0.16
    d.ellipse([ex - eye, ey - eye, ex + eye, ey + eye], fill=fill)


def radio(d, cx, cy, w, h, fill, bg):
    """遊戲裡那台收音機。天線是關鍵——少了它，白色方塊在 48px 只是一個方塊。"""
    left, right = cx - w / 2.0, cx + w / 2.0
    bottom = cy + h / 2.0
    body_h = h * 0.72
    top = bottom - body_h
    mid = (top + bottom) / 2.0

    # 天線先畫，機身壓在上面，接點就藏起來了
    aw = h * 0.055
    d.line([(cx + w * 0.28, top + body_h * 0.30), (cx + w * 0.60, cy - h * 0.50)],
           fill=fill, width=int(aw), joint="curve")
    tip = aw * 1.15
    d.ellipse([cx + w * 0.60 - tip, cy - h * 0.50 - tip,
               cx + w * 0.60 + tip, cy - h * 0.50 + tip], fill=fill)

    d.rounded_rectangle([left, top, right, bottom], radius=h * 0.10, fill=fill)

    # 左邊喇叭
    sr = body_h * 0.31
    sx = left + w * 0.28
    d.ellipse([sx - sr, mid - sr, sx + sr, mid + sr], fill=bg)
    d.ellipse([sx - sr * 0.34, mid - sr * 0.34,
               sx + sr * 0.34, mid + sr * 0.34], fill=fill)

    # 右邊調頻窗 + 指針，下面一顆旋鈕
    wl, wr = cx + w * 0.04, right - w * 0.08
    wt, wb = mid - body_h * 0.33, mid - body_h * 0.02
    d.rounded_rectangle([wl, wt, wr, wb], radius=body_h * 0.07, fill=bg)
    nw = (wr - wl) * 0.09
    nx = wl + (wr - wl) * 0.62
    d.rectangle([nx - nw / 2.0, wt + body_h * 0.04, nx + nw / 2.0, wb - body_h * 0.04],
                fill=fill)
    kr = body_h * 0.13
    kx, ky = (wl + wr) / 2.0, mid + body_h * 0.24
    d.ellipse([kx - kr, ky - kr, kx + kr, ky + kr], fill=bg)


def gear(d, cx, cy, r, fill, bg, teeth=8):
    tw, tl = r * 0.30, r * 0.34
    for i in range(teeth):
        a = 2 * math.pi * i / teeth
        ca, sa = math.cos(a), math.sin(a)
        px, py = cx + ca * (r + tl * 0.35), cy + sa * (r + tl * 0.35)
        corners = []
        for dx, dy in ((-tl, -tw), (tl, -tw), (tl, tw), (-tl, tw)):
            corners.append((px + dx * ca - dy * sa, py + dx * sa + dy * ca))
        d.polygon(corners, fill=fill)
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=fill)
    d.ellipse([cx - r * 0.38, cy - r * 0.38, cx + r * 0.38, cy + r * 0.38], fill=bg)


def prompt(d, cx, cy, w, h, fill):
    """終端機的 >_ 。"""
    lw = w * 0.155
    d.line([(cx - w * 0.44, cy - h * 0.40), (cx - w * 0.02, cy),
            (cx - w * 0.44, cy + h * 0.40)], fill=fill, width=int(lw), joint="curve")
    d.rounded_rectangle([cx + w * 0.08, cy + h * 0.25,
                         cx + w * 0.48, cy + h * 0.25 + lw],
                        radius=lw / 2.0, fill=fill)


# --------------------------------------------------------------------------

def build(base, draw_glyph):
    """字符畫在透明圖層上，**依實際畫出來的範圍置中**後才貼上去。

    不能靠各個字符函式自己把座標算準：收音機的天線伸出機身之外，照機身的
    幾何中心擺，整張看起來就偏右上。量 bbox 是唯一不會算錯的方法，
    順便讓其他字符（愛心那種上下不對稱的曲線）也一併正過來。
    """
    img, _, bg = canvas(base)
    layer = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    draw_glyph(ImageDraw.Draw(layer), bg)

    box = layer.getbbox()
    if box:
        dx = (N - (box[0] + box[2])) // 2
        dy = (N - (box[1] + box[3])) // 2
        if dx or dy:
            moved = Image.new("RGBA", (N, N), (0, 0, 0, 0))
            moved.paste(layer.crop(box), (box[0] + dx, box[1] + dy))
            layer = moved

    return Image.alpha_composite(img.convert("RGBA"), layer) \
                .convert("RGB").resize((SIZE, SIZE), RESAMPLE)


ICONS = {
    # 冷色：只有你自己要裝
    "HtF.HudNumbers":  (GREEN,  lambda d, bg: hud_panel(d, N * .5, N * .5, N * .50, N * .40, WHITE)),
    "HtF.AmmoCounter": (BLUE,   lambda d, bg: bullet(d, N * .5, N * .5, N * .26, N * .56, WHITE, bg)),
    "HtF.RadioMusic":  (PURPLE, lambda d, bg: radio(d, N * .5, N * .52, N * .50, N * .46, WHITE, bg)),
    "HtF.ConfigMenu":  (TEAL,   lambda d, bg: gear(d, N * .5, N * .5, N * .195, WHITE, bg)),
    "HtF.DazedTools":  (SLATE,  lambda d, bg: prompt(d, N * .5, N * .5, N * .52, N * .5, WHITE)),
    # 暖色：只有房主要裝
    "HtF.Guardian":    (ORANGE, lambda d, bg: shield(d, N * .5, N * .5, N * .44, N * .54, WHITE)),
    "HtF.HostRules":   (RED,    lambda d, bg: sliders(d, N * .5, N * .5, N * .50, N * .46, WHITE, bg)),
    "HtF.Economy":     (GOLD,   lambda d, bg: coin_fish(d, N * .5, N * .5, N * .26, WHITE, bg)),
}


def main():
    if not os.path.isdir(MODS):
        sys.exit("找不到 mods/：%s" % MODS)
    for mod in sorted(ICONS):
        base, glyph = ICONS[mod]
        folder = os.path.join(MODS, mod)
        if not os.path.isdir(folder):
            sys.exit("找不到 mods/%s" % mod)
        path = os.path.join(folder, "icon.png")
        build(base, glyph).save(path, "PNG")
        print("mods/%s/icon.png  256x256" % mod)
    print("%d 張。" % len(ICONS))
    return 0


if __name__ == "__main__":
    sys.exit(main())
