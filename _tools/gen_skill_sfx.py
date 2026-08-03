# -*- coding: utf-8 -*-
"""
程序化合成 Survivor 项目的「技能音效」（玩家技能 + Boss 技能）。

设计原则：每个音效贴合技能的元素/主题——
  · 风系  → 高频 whoosh 气流噪声扫频
  · 冰系  → 高频晶体叮 + 碎裂噪声
  · 暗影  → 低频压迫 drone + 金属摩擦
  · 血族  → 湿润液体 splat + 低频脉动
  · 寄生  → 有机蠕动 + 黏腻低频
  · 自然  → 柔和木质/孢子噗
  · 物理  → 打击/挥砍 transient
  · 地震  → 极低频 rumble
  · 吐息  → 持续气流 + 液体喷射

复用 gen_audio_sfx.py 的合成基元（tone/sweep_tone/noise/adsr/滤波/mix）。
输出到 Assets/Resources/Audio/，AudioManager 通过 Resources.Load 自动识别。
"""

import os
import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, lfilter

SR = 44100
OUT_DIR = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "Audio"))


# ──────────────────────────────────────────────────────────
# 合成基元（与 gen_audio_sfx.py 保持一致）
# ──────────────────────────────────────────────────────────

def silence(seconds):
    return np.zeros(int(SR * seconds), dtype=np.float32)


def tone(freq, seconds, wave="sine", phase=0.0):
    n = int(SR * seconds)
    t = np.arange(n, dtype=np.float32) / SR
    x = 2.0 * np.pi * freq * t + phase
    if wave == "sine":
        return np.sin(x).astype(np.float32)
    if wave == "square":
        return np.sign(np.sin(x)).astype(np.float32)
    if wave == "saw":
        return (2.0 * (t * freq - np.floor(0.5 + t * freq))).astype(np.float32)
    if wave == "triangle":
        return (2.0 / np.pi * np.arcsin(np.sin(x))).astype(np.float32)
    raise ValueError(wave)


def sweep_tone(f0, f1, seconds, wave="sine"):
    n = int(SR * seconds)
    t = np.arange(n, dtype=np.float32) / SR
    f_t = f0 + (f1 - f0) * (t / seconds)
    phase = 2.0 * np.pi * np.cumsum(f_t) / SR
    if wave == "sine":
        return np.sin(phase).astype(np.float32)
    if wave == "square":
        return np.sign(np.sin(phase)).astype(np.float32)
    if wave == "saw":
        return ((phase / (2 * np.pi)) % 1.0 * 2.0 - 1.0).astype(np.float32)
    if wave == "triangle":
        return (2.0 / np.pi * np.arcsin(np.sin(phase))).astype(np.float32)
    raise ValueError(wave)


def noise(seconds, kind="white"):
    n = int(SR * seconds)
    if kind == "white":
        return np.random.uniform(-1.0, 1.0, n).astype(np.float32)
    if kind == "pink":
        white = np.random.uniform(-1.0, 1.0, n).astype(np.float32)
        spec = np.fft.rfft(white)
        freqs = np.fft.rfftfreq(n, 1 / SR)
        freqs[0] = 1.0
        spec = spec / np.sqrt(freqs)
        out = np.fft.irfft(spec, n).astype(np.float32)
        return (out / (np.max(np.abs(out)) + 1e-9)).astype(np.float32)
    raise ValueError(kind)


def adsr(seconds, a, d, s_level, s_dur, r):
    n = int(SR * seconds)
    env = np.zeros(n, dtype=np.float32)
    na, nd = max(1, int(a * SR)), max(1, int(d * SR))
    ns, nr = max(0, int(s_dur * SR)), max(1, int(r * SR))
    total = na + nd + ns + nr
    if total > n:
        scale = n / total
        na, nd = max(1, int(na * scale)), max(1, int(nd * scale))
        ns = max(0, int(ns * scale))
        nr = max(1, n - na - nd - ns)
    i = 0
    env[i:i + na] = np.linspace(0, 1, na, dtype=np.float32); i += na
    env[i:i + nd] = np.linspace(1, s_level, nd, dtype=np.float32); i += nd
    env[i:i + ns] = s_level; i += ns
    env[i:i + nr] = np.linspace(s_level, 0, nr, dtype=np.float32)
    return env


def lowpass(x, cutoff, order=4):
    b, a = butter(order, min(cutoff / (SR / 2), 0.99), btype="low")
    return lfilter(b, a, x).astype(np.float32)


def highpass(x, cutoff, order=4):
    b, a = butter(order, max(cutoff / (SR / 2), 1e-4), btype="high")
    return lfilter(b, a, x).astype(np.float32)


def bandpass(x, lo, hi, order=4):
    b, a = butter(order, [max(lo / (SR / 2), 1e-4), min(hi / (SR / 2), 0.99)], btype="band")
    return lfilter(b, a, x).astype(np.float32)


def overlay_at(base, clip, start_sec, gain=1.0):
    start = int(start_sec * SR)
    end = start + len(clip)
    if end > len(base):
        base = np.concatenate([base, np.zeros(end - len(base), dtype=np.float32)])
    base[start:end] += clip * gain
    return base


def envelope(length_samples, points):
    """按 (相对位置0~1, 幅度) 列表线性插值出包络。"""
    xs = np.array([p[0] for p in points], dtype=np.float32)
    ys = np.array([p[1] for p in points], dtype=np.float32)
    t = np.linspace(0.0, 1.0, length_samples, dtype=np.float32)
    return np.interp(t, xs, ys).astype(np.float32)


def normalize(x, target_dbfs=-3.0):
    peak = float(np.max(np.abs(x)))
    if peak < 1e-9:
        return x
    return (x / peak * (10.0 ** (target_dbfs / 20.0))).astype(np.float32)


def soft_clip(x, drive=1.5):
    return np.tanh(x * drive).astype(np.float32)


def write_wav(path, x):
    x = np.clip(x, -1.0, 1.0)
    wavfile.write(path, SR, (x * 32767.0).astype(np.int16))
    print(f"  ->  {os.path.basename(path)}   ({len(x)/SR:.2f}s, peak={np.max(np.abs(x)):.3f})")


# ══════════════════════════════════════════════════════════
# 玩家技能音效
# ══════════════════════════════════════════════════════════

def synth_wind_cast():
    """风箭发射：短促上扬 whoosh（0.22s）。高频气流为主，轻盈。"""
    dur = 0.22
    out = silence(dur)
    # 主体：带通噪声 + 频段随时间上移（气流破空）
    air = noise(dur, "pink")
    air = bandpass(air, 900, 6500)
    air *= envelope(len(air), [(0, 0.0), (0.15, 1.0), (0.5, 0.5), (1.0, 0.0)])
    out = overlay_at(out, air, 0.0, 0.85)
    # 一层细锐的"咻"扫频，给箭矢的方向感
    whistle = sweep_tone(1400, 3200, dur * 0.7, "sine") * adsr(dur * 0.7, 0.01, 0.05, 0.35, 0.03, 0.06)
    out = overlay_at(out, whistle, 0.0, 0.18)
    return normalize(out, -8.0)   # 风箭 CD 短，故意做轻


def synth_wind_hit():
    """风箭命中：干脆的切割声（0.16s）。"""
    dur = 0.16
    out = silence(dur)
    cut = noise(dur, "white")
    cut = bandpass(cut, 2500, 9000)
    cut *= adsr(dur, 0.002, 0.04, 0.25, 0.02, 0.08)
    out = overlay_at(out, cut, 0.0, 0.7)
    thud = sweep_tone(320, 140, 0.10, "sine") * adsr(0.10, 0.002, 0.03, 0.3, 0.01, 0.05)
    out = overlay_at(out, thud, 0.0, 0.5)
    return normalize(out, -9.0)


def synth_hurricane():
    """飓风：环形爆发气旋（0.85s）。低频卷动 + 高频旋转气流。"""
    dur = 0.85
    out = silence(dur)
    # 旋转气流：噪声 + LFO 幅度调制（模拟环绕）
    swirl = noise(dur, "pink")
    swirl = bandpass(swirl, 300, 5000)
    t = np.arange(len(swirl), dtype=np.float32) / SR
    swirl *= (0.65 + 0.35 * np.sin(2 * np.pi * 7.0 * t))      # 7Hz 旋转感
    swirl *= envelope(len(swirl), [(0, 0.0), (0.1, 1.0), (0.55, 0.7), (1.0, 0.0)])
    out = overlay_at(out, swirl, 0.0, 0.75)
    # 低频卷动
    low = sweep_tone(110, 55, dur * 0.8, "triangle") * envelope(int(SR * dur * 0.8),
        [(0, 0.0), (0.15, 1.0), (0.6, 0.6), (1.0, 0.0)])
    low = lowpass(low, 420)
    out = overlay_at(out, low, 0.0, 0.45)
    # 起手一声"轰"
    burst = noise(0.18, "white")
    burst = bandpass(burst, 150, 2200)
    burst *= adsr(0.18, 0.004, 0.05, 0.3, 0.03, 0.09)
    out = overlay_at(out, burst, 0.0, 0.35)
    return normalize(out, -4.5)


def synth_wind_blade():
    """风之形风刃：锐利挥砍（0.28s）。"""
    dur = 0.28
    out = silence(dur)
    # 挥砍 whoosh：频段快速下移（刀锋掠过）
    slash = noise(dur, "pink")
    slash = bandpass(slash, 1200, 8000)
    slash *= envelope(len(slash), [(0, 0.0), (0.08, 1.0), (0.35, 0.45), (1.0, 0.0)])
    out = overlay_at(out, slash, 0.0, 0.8)
    # 金属般的高频谐振，体现"刃"
    ring = (tone(2600, dur * 0.5, "sine") * 0.6 + tone(3900, dur * 0.5, "sine") * 0.3)
    ring *= adsr(dur * 0.5, 0.003, 0.06, 0.25, 0.02, 0.06)
    out = overlay_at(out, ring, 0.01, 0.2)
    return normalize(out, -6.0)


def synth_ice_cast():
    """冰系发射：结晶凝聚（0.35s）。清冷晶体音 + 霜冻嘶声。"""
    dur = 0.35
    out = silence(dur)
    # 晶体上行三音（冷色调，用纯五度堆叠）
    for f, at in [(1046.5, 0.0), (1568.0, 0.07), (2093.0, 0.14)]:
        n_len = 0.20
        cry = tone(f, n_len, "sine") + tone(f * 2.0, n_len, "sine") * 0.3
        cry *= adsr(n_len, 0.003, 0.05, 0.3, 0.03, 0.11)
        out = overlay_at(out, cry, at, 0.42)
    # 霜冻嘶声（高频窄带噪声）
    frost = noise(dur, "white")
    frost = bandpass(frost, 6000, 12000)
    frost *= envelope(len(frost), [(0, 0.0), (0.2, 0.8), (1.0, 0.0)])
    out = overlay_at(out, frost, 0.0, 0.12)
    return normalize(out, -6.5)


def synth_dark_cast():
    """暗影系发射（黑暗齿轮）：低频压迫 + 金属摩擦（0.4s）。"""
    dur = 0.40
    out = silence(dur)
    # 低频暗涌
    dark = (tone(58.0, dur, "sine") * 0.9 + tone(87.0, dur, "triangle") * 0.5
            + tone(116.0, dur, "sine") * 0.3)
    dark *= adsr(dur, 0.01, 0.10, 0.55, 0.12, 0.17)
    dark = soft_clip(dark, 1.4)
    dark = lowpass(dark, 380)
    out = overlay_at(out, dark, 0.0, 0.7)
    # 金属齿轮摩擦（中频带通噪声 + 快速颗粒调制）
    grind = noise(dur, "white")
    grind = bandpass(grind, 800, 3600)
    tg = np.arange(len(grind), dtype=np.float32) / SR
    grind *= (0.5 + 0.5 * np.sign(np.sin(2 * np.pi * 42.0 * tg)))   # 42Hz 齿列颗粒感
    grind *= envelope(len(grind), [(0, 0.0), (0.12, 0.9), (0.6, 0.5), (1.0, 0.0)])
    out = overlay_at(out, grind, 0.0, 0.22)
    return normalize(out, -5.5)


def synth_blood_cast():
    """血族血统：湿润液体涌动 + 低频脉动（0.45s）。"""
    dur = 0.45
    out = silence(dur)
    # 液体涌动（低中频噪声 + 随机化包络）
    liquid = noise(dur, "pink")
    liquid = bandpass(liquid, 180, 2400)
    liquid *= envelope(len(liquid), [(0, 0.0), (0.1, 1.0), (0.3, 0.55), (0.6, 0.7), (1.0, 0.0)])
    out = overlay_at(out, liquid, 0.0, 0.6)
    # 心跳般的低频脉动（血族主题）
    for at, g in [(0.0, 1.0), (0.20, 0.75)]:
        beat = sweep_tone(95, 42, 0.20, "sine") * adsr(0.20, 0.004, 0.05, 0.35, 0.03, 0.10)
        beat = lowpass(soft_clip(beat, 1.25), 260)
        out = overlay_at(out, beat, at, g * 0.75)
    return normalize(out, -5.0)


def synth_blood_hit():
    """血族命中：湿润 splat（0.18s）。"""
    dur = 0.18
    out = silence(dur)
    splat = noise(dur, "pink")
    splat = bandpass(splat, 220, 3000)
    splat *= adsr(dur, 0.003, 0.05, 0.25, 0.02, 0.09)
    out = overlay_at(out, splat, 0.0, 0.75)
    low = sweep_tone(260, 90, 0.12, "sine") * adsr(0.12, 0.003, 0.03, 0.3, 0.02, 0.06)
    out = overlay_at(out, low, 0.0, 0.5)
    return normalize(out, -8.5)


def synth_parasite_cast():
    """命途·寄生：有机蠕动 + 黏腻低频（0.5s）。触手破土而出。"""
    dur = 0.50
    out = silence(dur)
    # 黏腻低频上行（触手钻出）
    squirm = sweep_tone(45, 150, dur * 0.7, "triangle")
    squirm *= envelope(int(SR * dur * 0.7), [(0, 0.0), (0.2, 1.0), (0.7, 0.6), (1.0, 0.0)])
    squirm = soft_clip(squirm, 1.5)
    squirm = lowpass(squirm, 500)
    out = overlay_at(out, squirm, 0.0, 0.7)
    # 有机质摩擦（中低频噪声 + 不规则调制）
    organic = noise(dur, "pink")
    organic = bandpass(organic, 300, 2800)
    to = np.arange(len(organic), dtype=np.float32) / SR
    organic *= (0.55 + 0.45 * np.sin(2 * np.pi * 13.0 * to + np.sin(2 * np.pi * 3.1 * to)))
    organic *= envelope(len(organic), [(0, 0.0), (0.15, 0.9), (0.55, 0.6), (1.0, 0.0)])
    out = overlay_at(out, organic, 0.0, 0.30)
    return normalize(out, -5.0)


def synth_spore():
    """孢子领域：柔和的孢子噗散（0.4s）。自然、不刺耳。"""
    dur = 0.40
    out = silence(dur)
    # 噗——柔和的宽带噪声，快速起音慢衰减
    puff = noise(dur, "pink")
    puff = lowpass(puff, 3000)
    puff = highpass(puff, 200)
    puff *= envelope(len(puff), [(0, 0.0), (0.06, 1.0), (0.4, 0.35), (1.0, 0.0)])
    out = overlay_at(out, puff, 0.0, 0.7)
    # 一点温和的木质共鸣（自然主题）
    woody = tone(220, 0.25, "triangle") * 0.5 + tone(330, 0.25, "sine") * 0.25
    woody *= adsr(0.25, 0.01, 0.08, 0.3, 0.05, 0.11)
    out = overlay_at(out, woody, 0.0, 0.25)
    return normalize(out, -7.5)


def synth_tomb_cast():
    """亡者领域：幽冥低语 + 阴森共鸣（0.6s）。"""
    dur = 0.60
    out = silence(dur)
    # 幽冥低频 drone（小二度不协和，制造阴森感）
    drone = (tone(73.4, dur, "sine") * 0.9 + tone(77.8, dur, "sine") * 0.6
             + tone(146.8, dur, "triangle") * 0.35)
    drone *= envelope(len(drone), [(0, 0.0), (0.15, 1.0), (0.6, 0.65), (1.0, 0.0)])
    drone = soft_clip(drone, 1.3)
    drone = lowpass(drone, 420)
    out = overlay_at(out, drone, 0.0, 0.65)
    # 阴风（高频带通噪声，缓慢起落）
    wail = noise(dur, "pink")
    wail = bandpass(wail, 1500, 5500)
    wail *= envelope(len(wail), [(0, 0.0), (0.3, 0.7), (0.7, 0.4), (1.0, 0.0)])
    out = overlay_at(out, wail, 0.0, 0.16)
    return normalize(out, -5.0)


def synth_hellfire_cast():
    """地狱火：天降三叉戟——沉重下坠 + 烈焰轰鸣（0.7s）。"""
    dur = 0.70
    out = silence(dur)
    # 下坠扫频（从高到低，戟体撕裂空气）
    fall = sweep_tone(900, 120, 0.35, "saw")
    fall *= envelope(int(SR * 0.35), [(0, 0.0), (0.1, 0.9), (1.0, 0.5)])
    fall = lowpass(fall, 2500)
    out = overlay_at(out, fall, 0.0, 0.35)
    # 落地烈焰爆轰
    blast = noise(0.42, "white")
    blast = lowpass(blast, 1800)
    blast *= adsr(0.42, 0.004, 0.09, 0.35, 0.08, 0.21)
    out = overlay_at(out, blast, 0.30, 0.55)
    # 低频冲击
    impact = sweep_tone(120, 40, 0.35, "sine") * adsr(0.35, 0.003, 0.08, 0.45, 0.06, 0.18)
    impact = soft_clip(impact, 1.5)
    impact = lowpass(impact, 300)
    out = overlay_at(out, impact, 0.30, 0.85)
    # 燃烧余烬（高频噼啪）
    ember = noise(0.30, "white")
    ember = bandpass(ember, 3500, 9000)
    te = np.arange(len(ember), dtype=np.float32) / SR
    ember *= (np.random.uniform(0, 1, len(ember)) > 0.93).astype(np.float32)  # 稀疏噼啪
    ember *= envelope(len(ember), [(0, 1.0), (1.0, 0.0)])
    out = overlay_at(out, ember, 0.35, 0.25)
    return normalize(out, -4.0)


# ══════════════════════════════════════════════════════════
# Boss 技能音效
# ══════════════════════════════════════════════════════════

def synth_boss_slash():
    """Boss 挥砍（狼爪/剑气）：厚重挥砍 + 金属锐鸣（0.35s）。"""
    dur = 0.35
    out = silence(dur)
    slash = noise(dur, "pink")
    slash = bandpass(slash, 700, 7000)
    slash *= envelope(len(slash), [(0, 0.0), (0.07, 1.0), (0.3, 0.4), (1.0, 0.0)])
    out = overlay_at(out, slash, 0.0, 0.8)
    # 厚重感：低频体
    body = sweep_tone(420, 130, 0.20, "triangle") * adsr(0.20, 0.004, 0.05, 0.35, 0.03, 0.10)
    body = lowpass(body, 900)
    out = overlay_at(out, body, 0.0, 0.45)
    # 金属锐鸣
    ring = tone(3100, 0.16, "sine") * adsr(0.16, 0.002, 0.04, 0.2, 0.02, 0.08)
    out = overlay_at(out, ring, 0.02, 0.15)
    return normalize(out, -4.0)


def synth_boss_quake():
    """Boss 震地：极低频 rumble + 碎石（0.9s）。"""
    dur = 0.90
    out = silence(dur)
    # 极低频地鸣
    rumble = (tone(32.0, dur, "sine") * 1.0 + tone(48.0, dur, "sine") * 0.6
              + tone(64.0, dur, "triangle") * 0.35)
    tr = np.arange(len(rumble), dtype=np.float32) / SR
    rumble *= (0.75 + 0.25 * np.sin(2 * np.pi * 5.5 * tr))   # 震动颤抖
    rumble *= envelope(len(rumble), [(0, 0.0), (0.05, 1.0), (0.45, 0.6), (1.0, 0.0)])
    rumble = soft_clip(rumble, 1.6)
    rumble = lowpass(rumble, 260)
    out = overlay_at(out, rumble, 0.0, 0.9)
    # 碎石飞溅（稀疏中高频颗粒）
    debris = noise(0.55, "white")
    debris = bandpass(debris, 900, 6000)
    debris *= (np.random.uniform(0, 1, len(debris)) > 0.9).astype(np.float32)
    debris *= envelope(len(debris), [(0, 1.0), (0.4, 0.5), (1.0, 0.0)])
    out = overlay_at(out, debris, 0.06, 0.28)
    return normalize(out, -3.5)


def synth_boss_breath():
    """Boss 吐息（龙息/史莱姆吐息）：持续气流喷射（1.0s）。"""
    dur = 1.00
    out = silence(dur)
    # 喷射气流主体
    jet = noise(dur, "pink")
    jet = bandpass(jet, 250, 5500)
    tj = np.arange(len(jet), dtype=np.float32) / SR
    jet *= (0.8 + 0.2 * np.sin(2 * np.pi * 17.0 * tj))     # 喷流不稳定感
    jet *= envelope(len(jet), [(0, 0.0), (0.12, 1.0), (0.7, 0.8), (1.0, 0.0)])
    out = overlay_at(out, jet, 0.0, 0.8)
    # 低频推力
    push = tone(72.0, dur * 0.85, "triangle") * 0.7 + tone(108.0, dur * 0.85, "sine") * 0.4
    push *= envelope(int(SR * dur * 0.85), [(0, 0.0), (0.15, 1.0), (0.75, 0.6), (1.0, 0.0)])
    push = lowpass(push, 400)
    out = overlay_at(out, push, 0.0, 0.4)
    return normalize(out, -4.0)


def synth_boss_spit():
    """Boss 吐射弹丸（史莱姆/蘑菇喷吐）：黏腻喷射（0.3s）。"""
    dur = 0.30
    out = silence(dur)
    # 黏液喷出
    spit = noise(dur, "pink")
    spit = bandpass(spit, 300, 4000)
    spit *= adsr(dur, 0.004, 0.07, 0.3, 0.05, 0.14)
    out = overlay_at(out, spit, 0.0, 0.75)
    # 上扬的"啵"
    pop = sweep_tone(180, 420, 0.14, "triangle") * adsr(0.14, 0.003, 0.04, 0.3, 0.02, 0.07)
    pop = lowpass(pop, 1600)
    out = overlay_at(out, pop, 0.0, 0.45)
    return normalize(out, -6.0)


def synth_boss_dive():
    """Boss 俯冲（蝙蝠扑击）：快速下压 whoosh（0.4s）。"""
    dur = 0.40
    out = silence(dur)
    # 俯冲气流：频段快速下移
    dive = noise(dur, "pink")
    dive = bandpass(dive, 500, 6000)
    dive *= envelope(len(dive), [(0, 0.0), (0.15, 0.7), (0.55, 1.0), (1.0, 0.0)])
    out = overlay_at(out, dive, 0.0, 0.7)
    # 下压扫频
    swoop = sweep_tone(700, 160, dur * 0.8, "triangle")
    swoop *= envelope(int(SR * dur * 0.8), [(0, 0.0), (0.2, 0.8), (0.7, 1.0), (1.0, 0.0)])
    swoop = lowpass(swoop, 1800)
    out = overlay_at(out, swoop, 0.0, 0.4)
    return normalize(out, -5.5)


def synth_boss_summon():
    """Boss 召唤小怪：诡异召唤共鸣（0.7s）。"""
    dur = 0.70
    out = silence(dur)
    # 上行不协和音簇（召唤仪式感）
    for f, at in [(110.0, 0.0), (155.6, 0.10), (207.7, 0.20), (277.2, 0.30)]:
        n_len = 0.32
        cry = tone(f, n_len, "saw") * 0.55 + tone(f * 2, n_len, "triangle") * 0.3
        cry *= adsr(n_len, 0.02, 0.08, 0.4, 0.10, 0.12)
        cry = lowpass(cry, 2200)
        out = overlay_at(out, cry, at, 0.38)
    # 低频仪式底噪
    ritual = tone(55.0, dur, "sine") * 0.7
    ritual *= envelope(len(ritual), [(0, 0.0), (0.2, 1.0), (0.7, 0.6), (1.0, 0.0)])
    ritual = lowpass(ritual, 220)
    out = overlay_at(out, ritual, 0.0, 0.5)
    return normalize(out, -5.0)


def synth_boss_charge():
    """Boss 蓄力/技能前摇：紧张上扬（0.6s）。"""
    dur = 0.60
    out = silence(dur)
    # 上行扫频（蓄力）
    charge = sweep_tone(90, 480, dur, "saw")
    charge *= envelope(len(charge), [(0, 0.0), (0.3, 0.6), (0.85, 1.0), (1.0, 0.3)])
    charge = lowpass(charge, 2600)
    charge = soft_clip(charge, 1.2)
    out = overlay_at(out, charge, 0.0, 0.55)
    # 能量嘶鸣
    hiss = noise(dur, "pink")
    hiss = bandpass(hiss, 2000, 7000)
    hiss *= envelope(len(hiss), [(0, 0.0), (0.5, 0.5), (0.9, 1.0), (1.0, 0.2)])
    out = overlay_at(out, hiss, 0.0, 0.18)
    return normalize(out, -5.5)


# ══════════════════════════════════════════════════════════

def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    print(f"输出目录: {OUT_DIR}")
    targets = [
        # ── 玩家技能 ──
        ("风箭发射.wav",   synth_wind_cast),
        ("风箭命中.wav",   synth_wind_hit),
        ("飓风.wav",       synth_hurricane),
        ("风刃.wav",       synth_wind_blade),
        ("冰霜发射.wav",   synth_ice_cast),
        ("暗影发射.wav",   synth_dark_cast),
        ("血族发射.wav",   synth_blood_cast),
        ("血族命中.wav",   synth_blood_hit),
        ("寄生发射.wav",   synth_parasite_cast),
        ("孢子扩散.wav",   synth_spore),
        ("亡者领域.wav",   synth_tomb_cast),
        ("地狱火发射.wav", synth_hellfire_cast),
        # ── Boss 技能 ──
        ("Boss挥砍.wav",   synth_boss_slash),
        ("Boss震地.wav",   synth_boss_quake),
        ("Boss吐息.wav",   synth_boss_breath),
        ("Boss吐射.wav",   synth_boss_spit),
        ("Boss俯冲.wav",   synth_boss_dive),
        ("Boss召唤.wav",   synth_boss_summon),
        ("Boss蓄力.wav",   synth_boss_charge),
    ]
    for fname, fn in targets:
        write_wav(os.path.join(OUT_DIR, fname), fn())
    print(f"完成，共 {len(targets)} 个音效")


if __name__ == "__main__":
    np.random.seed(20260803)
    main()
