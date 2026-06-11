# -*- coding: utf-8 -*-
"""
程序化合成 Survivor 项目使用的若干 SFX / 短 BGM。

产出（全部写到 Assets/Resources/Audio/，与现有 mp3 同目录，被 AudioManager 直接 Resources.Load）：
  1) 亡者复活.wav       —— 无罪复活 Boss 演出压迫感音效（3.2s，与 ReviveBossEffect 1.6s 龙眼 + 反向死亡对齐）
  2) 亡者回血.wav       —— 光电融入回 0.5% 血（0.4s 上行琶音叮）
  3) 升级.wav          —— 玩家升级（0.6s 明亮上行琶音）
  4) 经验拾取.wav       —— 经验石拾取（0.15s 短促清脆，避免批量叠加刺耳）
  5) 按键悬停.wav       —— UI 悬停（0.08s 极短软嘀）
  6) Boss出现.wav      —— 普通 Boss 入场预警（1.2s 低频号角 + 心跳）

输出统一参数：
  sample_rate = 44100 Hz, 单声道 int16，归一化到 ~-3 dBFS。
  写 .wav 是因为 Unity 对 wav 解码最稳，无需额外依赖；mp3 编码需要 ffmpeg/lame，所以 wav 即可。
"""

import os
import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, lfilter

SR = 44100  # 采样率
OUT_DIR = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "Audio"))


# ────────────────────────────────────────────────────────────
# 基础工具
# ────────────────────────────────────────────────────────────

def silence(seconds: float) -> np.ndarray:
    return np.zeros(int(SR * seconds), dtype=np.float32)


def tone(freq: float, seconds: float, wave: str = "sine", phase: float = 0.0) -> np.ndarray:
    """生成一段单音。wave: sine/square/saw/triangle。"""
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


def sweep_tone(f0: float, f1: float, seconds: float, wave: str = "sine") -> np.ndarray:
    """频率线性扫描的音色（pitch sweep），f0→f1。"""
    n = int(SR * seconds)
    t = np.arange(n, dtype=np.float32) / SR
    # 瞬时频率线性插值，相位 = 2π ∫ f(τ) dτ
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


def noise(seconds: float, kind: str = "white") -> np.ndarray:
    n = int(SR * seconds)
    if kind == "white":
        return np.random.uniform(-1.0, 1.0, n).astype(np.float32)
    if kind == "pink":
        # 简易 pink noise：白噪叠 1/f 滤波
        white = np.random.uniform(-1.0, 1.0, n).astype(np.float32)
        # FFT 整形
        spec = np.fft.rfft(white)
        freqs = np.fft.rfftfreq(n, 1 / SR)
        freqs[0] = 1.0
        spec = spec / np.sqrt(freqs)
        out = np.fft.irfft(spec, n).astype(np.float32)
        out = out / (np.max(np.abs(out)) + 1e-9)
        return out
    raise ValueError(kind)


def adsr(seconds: float, a: float, d: float, s_level: float, s_dur: float, r: float) -> np.ndarray:
    """ADSR 包络。a/d/s_dur/r 单位是秒；s_level∈[0,1]。"""
    n = int(SR * seconds)
    env = np.zeros(n, dtype=np.float32)
    na = max(1, int(a * SR))
    nd = max(1, int(d * SR))
    ns = max(0, int(s_dur * SR))
    nr = max(1, int(r * SR))
    total = na + nd + ns + nr
    if total > n:
        # 等比压缩
        scale = n / total
        na = max(1, int(na * scale))
        nd = max(1, int(nd * scale))
        ns = max(0, int(ns * scale))
        nr = max(1, n - na - nd - ns)
    idx = 0
    env[idx:idx + na] = np.linspace(0, 1, na, dtype=np.float32)
    idx += na
    env[idx:idx + nd] = np.linspace(1, s_level, nd, dtype=np.float32)
    idx += nd
    env[idx:idx + ns] = s_level
    idx += ns
    env[idx:idx + nr] = np.linspace(s_level, 0, nr, dtype=np.float32)
    return env


def lowpass(x: np.ndarray, cutoff: float, order: int = 4) -> np.ndarray:
    b, a = butter(order, cutoff / (SR / 2), btype="low")
    return lfilter(b, a, x).astype(np.float32)


def highpass(x: np.ndarray, cutoff: float, order: int = 4) -> np.ndarray:
    b, a = butter(order, cutoff / (SR / 2), btype="high")
    return lfilter(b, a, x).astype(np.float32)


def bandpass(x: np.ndarray, lo: float, hi: float, order: int = 4) -> np.ndarray:
    b, a = butter(order, [lo / (SR / 2), hi / (SR / 2)], btype="band")
    return lfilter(b, a, x).astype(np.float32)


def mix(*tracks: np.ndarray) -> np.ndarray:
    """等长不限混音。短的补零。"""
    n = max(len(t) for t in tracks)
    out = np.zeros(n, dtype=np.float32)
    for t in tracks:
        out[: len(t)] += t
    return out


def overlay_at(base: np.ndarray, clip: np.ndarray, start_sec: float, gain: float = 1.0) -> np.ndarray:
    """把 clip 叠加到 base 的 start_sec 位置（自动扩长）。"""
    start = int(start_sec * SR)
    end = start + len(clip)
    if end > len(base):
        base = np.concatenate([base, np.zeros(end - len(base), dtype=np.float32)])
    base[start:end] += clip * gain
    return base


def normalize(x: np.ndarray, target_dbfs: float = -3.0) -> np.ndarray:
    peak = float(np.max(np.abs(x)))
    if peak < 1e-9:
        return x
    target_lin = 10.0 ** (target_dbfs / 20.0)
    return (x / peak * target_lin).astype(np.float32)


def soft_clip(x: np.ndarray, drive: float = 1.5) -> np.ndarray:
    """tanh 软削波，给低频部分加一点"被推过头"的过载感（用于压迫感音效）。"""
    return np.tanh(x * drive).astype(np.float32)


def write_wav(path: str, x: np.ndarray) -> None:
    x = np.clip(x, -1.0, 1.0)
    pcm = (x * 32767.0).astype(np.int16)
    wavfile.write(path, SR, pcm)
    print(f"  ->  {path}   ({len(x) / SR:.2f}s, peak={np.max(np.abs(x)):.3f})")


# ────────────────────────────────────────────────────────────
# 1) 亡者复活（压迫感 Boss 复活演出音效）
#
# 结构（总长 3.2s，对齐 ReviveBossEffect：龙眼 1.6s + 反向死亡 ≈ 1.6s 平均）：
#   0.00 ~ 0.20s  低频脉冲 boom（sub 30Hz → 60Hz scoop） + 暴风噪声 rise
#   0.20 ~ 0.80s  心跳脉冲 × 3（70→55→40 BPM 的反常减速 = 死亡心跳）
#   0.30 ~ 1.30s  低频金属轰鸣 drone（叠 40Hz / 55Hz / 82Hz / 110Hz 谐波，缓慢拍频）
#   0.80 ~ 1.50s  龙吼共振扫频（80Hz→50Hz，软削波，伴随玻璃高频啸鸣 3000~5000Hz）
#   1.50 ~ 2.00s  尖锐"玻璃裂"高频噪声 burst（带通 4k~9k）+ 低频"砰"叠合（= 龙眼消失瞬间）
#   2.00 ~ 3.20s  低频残辉 drone（55Hz + 缓慢淡出），最后 0.8s 渐弱
# ────────────────────────────────────────────────────────────

def synth_revive_boss() -> np.ndarray:
    total = 3.2
    out = silence(total)

    # —— 0.00s 起手低频 boom（sub-bass 暴击）——
    # 30Hz sine + 60Hz 谐波，0.2s 内陡升然后稍长 release
    boom_len = 0.65
    boom = (
        sweep_tone(20, 55, boom_len, wave="sine") * 1.0
        + sweep_tone(40, 110, boom_len, wave="sine") * 0.55
        + sweep_tone(60, 165, boom_len, wave="triangle") * 0.25
    )
    boom *= adsr(boom_len, a=0.005, d=0.10, s_level=0.55, s_dur=0.10, r=0.40)
    boom = soft_clip(boom, drive=1.3)
    boom = lowpass(boom, 350)
    out = overlay_at(out, boom, 0.00, gain=0.95)

    # —— 0.00s 起手暴风噪声 rise（pink noise，0~0.8s 从 0 起 ramp 到峰再回落）——
    wind_len = 1.0
    wind = noise(wind_len, kind="pink")
    wind = bandpass(wind, 200, 2500)
    wenv = np.concatenate([
        np.linspace(0.0, 1.0, int(0.5 * SR), dtype=np.float32),
        np.linspace(1.0, 0.0, int(0.5 * SR), dtype=np.float32),
    ])
    if len(wenv) < len(wind):
        wenv = np.pad(wenv, (0, len(wind) - len(wenv)))
    else:
        wenv = wenv[: len(wind)]
    wind *= wenv
    out = overlay_at(out, wind, 0.00, gain=0.28)

    # —— 0.20s 起 心跳脉冲 × 3（减速的死亡心跳）——
    # 每个心跳是"咚-咚"两连击。第一个 0.20s，第二个 0.50s，第三个 0.95s（间隔越来越长）
    def heartbeat(at: float, gain: float):
        nonlocal out
        # 一次心跳 = 0.05s 短 boom + 0.08s 后再来一次稍弱的
        beat1 = sweep_tone(80, 35, 0.18, wave="sine") * adsr(0.18, 0.003, 0.04, 0.4, 0.02, 0.10)
        beat2 = sweep_tone(70, 30, 0.16, wave="sine") * adsr(0.16, 0.003, 0.03, 0.3, 0.02, 0.10)
        beat1 = lowpass(soft_clip(beat1, 1.2), 240)
        beat2 = lowpass(soft_clip(beat2, 1.2), 240)
        out = overlay_at(out, beat1, at, gain)
        out = overlay_at(out, beat2, at + 0.10, gain * 0.75)

    heartbeat(0.20, 0.80)
    heartbeat(0.55, 0.75)
    heartbeat(1.05, 0.70)

    # —— 0.30 ~ 1.60s 低频金属轰鸣 drone（多谐波叠拍频）——
    drone_len = 1.30
    d1 = tone(41.20, drone_len, wave="sine") * 1.00         # E1
    d2 = tone(55.00, drone_len, wave="sine") * 0.70         # A1
    d3 = tone(82.41, drone_len, wave="triangle") * 0.50     # E2
    d4 = tone(110.00, drone_len, wave="triangle") * 0.35    # A2
    d5 = tone(164.81, drone_len, wave="sine") * 0.18        # E3 微高谐波，给"金属"感
    drone = d1 + d2 + d3 + d4 + d5
    # 慢拍频调制：1.5Hz LFO 让低频"喘息"
    t_d = np.arange(len(drone), dtype=np.float32) / SR
    lfo = 0.85 + 0.15 * np.sin(2 * np.pi * 1.3 * t_d)
    drone *= lfo
    drone = soft_clip(drone, drive=1.1)
    drone = lowpass(drone, 400)
    drone_env = np.concatenate([
        np.linspace(0.0, 1.0, int(0.30 * SR), dtype=np.float32),
        np.ones(int(0.55 * SR), dtype=np.float32),
        np.linspace(1.0, 0.0, int(0.45 * SR), dtype=np.float32),
    ])
    if len(drone_env) < len(drone):
        drone_env = np.pad(drone_env, (0, len(drone) - len(drone_env)))
    else:
        drone_env = drone_env[: len(drone)]
    drone *= drone_env
    out = overlay_at(out, drone, 0.30, gain=0.55)

    # —— 0.80 ~ 1.50s 龙吼共振扫频（从 85Hz 扫到 45Hz，带 saw 谐波 + 软削波）——
    roar_len = 0.70
    roar_core = sweep_tone(85, 45, roar_len, wave="saw")
    roar_harm = sweep_tone(170, 90, roar_len, wave="triangle") * 0.55
    roar = roar_core + roar_harm
    roar *= adsr(roar_len, a=0.10, d=0.15, s_level=0.85, s_dur=0.25, r=0.20)
    roar = soft_clip(roar, drive=1.8)
    roar = lowpass(roar, 600)
    out = overlay_at(out, roar, 0.80, gain=0.65)

    # 龙吼伴随玻璃高频啸鸣（3k~5k 带通的 pink noise）
    sizzle = noise(roar_len, kind="pink")
    sizzle = bandpass(sizzle, 3000, 5000)
    sizzle *= adsr(roar_len, a=0.10, d=0.10, s_level=0.5, s_dur=0.30, r=0.20)
    out = overlay_at(out, sizzle, 0.80, gain=0.10)

    # —— 1.50 ~ 2.00s "玻璃裂"高频 burst + 低频"砰"——
    # 玻璃裂：4k~9k 白噪 + 极短 attack
    crack_len = 0.45
    crack = noise(crack_len, kind="white")
    crack = bandpass(crack, 4000, 9000)
    crack *= adsr(crack_len, a=0.002, d=0.06, s_level=0.30, s_dur=0.08, r=0.30)
    out = overlay_at(out, crack, 1.50, gain=0.22)

    # 玻璃裂同步低频砰
    pang = sweep_tone(60, 25, 0.45, wave="sine") * adsr(0.45, 0.003, 0.10, 0.5, 0.10, 0.25)
    pang = soft_clip(pang, 1.3)
    pang = lowpass(pang, 300)
    out = overlay_at(out, pang, 1.50, gain=0.85)

    # —— 2.00 ~ 3.20s 低频残辉 drone（55Hz 单音渐弱）——
    tail_len = 1.20
    tail = tone(55.0, tail_len, wave="sine") * 0.7 + tone(82.41, tail_len, wave="sine") * 0.35
    tail *= np.linspace(1.0, 0.0, len(tail), dtype=np.float32) ** 1.8  # 偏快淡出
    tail = lowpass(tail, 280)
    out = overlay_at(out, tail, 2.00, gain=0.40)

    return normalize(out, -3.0)


# ────────────────────────────────────────────────────────────
# 2) 亡者回血（光电融入"叮"）—— 0.4s 上行三音琶音，清亮
# ────────────────────────────────────────────────────────────

def synth_tomb_heal() -> np.ndarray:
    total = 0.42
    out = silence(total)
    # E5(659) → B5(987) → E6(1318)，每音 0.08s，叠 + 共振高谐波
    notes = [(659.25, 0.00), (987.77, 0.08), (1318.51, 0.16)]
    for f, at in notes:
        n_len = 0.18
        tn = tone(f, n_len, wave="sine") + tone(f * 2, n_len, wave="sine") * 0.35 + tone(f * 3, n_len, wave="sine") * 0.12
        tn *= adsr(n_len, a=0.003, d=0.05, s_level=0.4, s_dur=0.04, r=0.09)
        out = overlay_at(out, tn, at, gain=0.55)
    # 尾部添加一点空气感
    air = noise(0.10, kind="pink")
    air = bandpass(air, 5000, 9000)
    air *= adsr(0.10, 0.005, 0.03, 0.3, 0.02, 0.04)
    out = overlay_at(out, air, 0.18, gain=0.08)
    return normalize(out, -3.5)


# ────────────────────────────────────────────────────────────
# 3) 升级（明亮大调四音上行）
# ────────────────────────────────────────────────────────────

def synth_levelup() -> np.ndarray:
    total = 0.62
    out = silence(total)
    # C5 E5 G5 C6（大三和弦琶音 + 顶音强调）
    seq = [(523.25, 0.00, 0.16, 0.55),
           (659.25, 0.10, 0.16, 0.60),
           (783.99, 0.20, 0.18, 0.70),
           (1046.50, 0.32, 0.30, 1.00)]
    for f, at, dur, g in seq:
        body = tone(f, dur, "sine") + tone(f * 2, dur, "sine") * 0.4 + tone(f * 3, dur, "triangle") * 0.18
        body *= adsr(dur, 0.005, 0.06, 0.5, dur * 0.4, 0.10)
        out = overlay_at(out, body, at, gain=g * 0.55)
    return normalize(out, -3.0)


# ────────────────────────────────────────────────────────────
# 4) 经验拾取（短促清脆"嘀"，避免批量叠加刺耳）
# ────────────────────────────────────────────────────────────

def synth_xp_pickup() -> np.ndarray:
    total = 0.16
    out = silence(total)
    # 1600Hz → 2400Hz 极短 sweep
    tn = sweep_tone(1600, 2400, 0.10, wave="sine")
    tn *= adsr(0.10, 0.002, 0.02, 0.4, 0.02, 0.05)
    out = overlay_at(out, tn, 0.0, gain=0.7)
    return normalize(out, -6.0)  # 拾取声音故意更轻，避免吵


# ────────────────────────────────────────────────────────────
# 5) 按键悬停（极短软"嘀"）
# ────────────────────────────────────────────────────────────

def synth_ui_hover() -> np.ndarray:
    total = 0.09
    out = silence(total)
    tn = tone(1800, 0.05, "sine") + tone(2700, 0.05, "sine") * 0.35
    tn *= adsr(0.05, 0.002, 0.015, 0.3, 0.01, 0.025)
    out = overlay_at(out, tn, 0.0, gain=0.5)
    return normalize(out, -10.0)  # UI 悬停极轻，避免操作时刺耳


# ────────────────────────────────────────────────────────────
# 6) Boss 出现（低频号角 + 心跳）—— 1.2s
# ────────────────────────────────────────────────────────────

def synth_boss_appear() -> np.ndarray:
    total = 1.30
    out = silence(total)
    # 0.00s 起 低频号角（A2 110Hz 主音 + A1 55Hz 八度下）
    horn_len = 0.90
    horn = (
        tone(55.0, horn_len, "saw") * 0.8
        + tone(110.0, horn_len, "saw") * 0.6
        + tone(165.0, horn_len, "triangle") * 0.25
    )
    horn *= adsr(horn_len, a=0.08, d=0.15, s_level=0.7, s_dur=0.45, r=0.22)
    horn = soft_clip(horn, drive=1.4)
    horn = lowpass(horn, 500)
    out = overlay_at(out, horn, 0.0, gain=0.6)

    # 0.20s, 0.55s 两声心跳
    def beat(at, gain):
        nonlocal out
        b = sweep_tone(80, 30, 0.20, "sine") * adsr(0.20, 0.003, 0.05, 0.4, 0.03, 0.10)
        b = lowpass(soft_clip(b, 1.2), 240)
        out = overlay_at(out, b, at, gain)

    beat(0.20, 0.85)
    beat(0.55, 0.85)

    # 1.00s 尾部一个高频"嘶"残辉（让玩家知道紧张感落地）
    hiss = noise(0.25, "pink")
    hiss = bandpass(hiss, 2000, 4500)
    hiss *= adsr(0.25, 0.02, 0.08, 0.4, 0.05, 0.10)
    out = overlay_at(out, hiss, 1.00, gain=0.08)

    return normalize(out, -3.5)


# ────────────────────────────────────────────────────────────
# 主入口
# ────────────────────────────────────────────────────────────

def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    print(f"输出目录: {OUT_DIR}")
    targets = [
        ("亡者复活.wav", synth_revive_boss),
        ("亡者回血.wav", synth_tomb_heal),
        ("升级.wav", synth_levelup),
        ("经验拾取.wav", synth_xp_pickup),
        ("按键悬停.wav", synth_ui_hover),
        ("Boss出现.wav", synth_boss_appear),
    ]
    for fname, fn in targets:
        path = os.path.join(OUT_DIR, fname)
        x = fn()
        write_wav(path, x)
    print("完成")


if __name__ == "__main__":
    # 固定随机种子让噪声可复现
    np.random.seed(20260610)
    main()
