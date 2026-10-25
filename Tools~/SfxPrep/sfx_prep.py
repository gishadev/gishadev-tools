#!/usr/bin/env python3
"""
SFX Prep: the audio side of the SFX Prep window in Unity (Tools > gishadev > SFX Prep), shipped in
com.gishadev.tools under Tools~/SfxPrep. The window runs it; by hand:

  python sfx_prep.py process --project-root <unity project> [--config Tools/SfxPrep/sfx_config.json]
                             [--library-dir <sound libraries>] [--sound pig-idle ...]

For every sound in sound_list.json (the game's sound list, edited in the window; candidates come from sfx-scout
and from files you drop on the window, yours first), reads the candidate files and turns them into game-ready
clips: multi-take files are split into single events (one-shots) or cut into a crossfaded seamless loop (Loop
sounds), then trimmed, faded, converted to mono/stereo and normalized to a loudness per category. Clips go to
<cache_dir>/clips/ with <cache_dir>/prep_manifest.json describing them (scores, default picks, import settings).
The window does the rest: preview, keep/drop, copy into Assets and set import settings.

Processing settings live in the project's sfx_config.json (--config, relative to the project root), category
and per-sound settings in its sound_list.json. Paths in the config are relative to the project root;
--library-dir overrides its library_dir (the folder is per machine).
Needs Python 3.9+ and numpy. Library files are never changed.
"""
import argparse
import json
import os
import re
import shutil
import struct
import sys
import time
import zlib
from pathlib import Path

import numpy as np

# The Unity project; set from --project-root in main(). Config paths are relative to it.
REPO = Path.cwd()
FRAME = 0.01  # analysis hop in seconds
OVERRIDES = ("channels", "lufs", "max_len", "loop_seconds", "crossfade")  # per sound; 0 = default


def long_path(p):
    """Sound library paths nest deep; on Windows go past the 260-character limit."""
    s = os.path.abspath(str(p))
    if os.name == "nt" and not s.startswith("\\\\?\\"):
        s = "\\\\?\\" + s
    return s


# --------------------------------------------------------------------------- config

def load_config(path):
    path = REPO / path
    if not path.exists():
        sys.exit(f"No config at {path}.")
    return json.loads(path.read_text(encoding="utf-8"))


def sound_settings(categories, sound):
    """The category's settings with the sound's own overrides (non-zero ones) on top."""
    if sound["category"] not in categories:
        sys.exit(f"Sound '{sound['id']}' uses category '{sound['category']}', which isn't in the sound list.")
    s = dict(categories[sound["category"]])
    s.update({k: sound[k] for k in OVERRIDES if sound.get(k)})
    s["loop"] = sound.get("type", "").lower().startswith("loop")
    s["count"] = parse_count(sound.get("variations", "1"), s["loop"])
    return s


def parse_count(text, is_loop):
    """'4–6' -> 6, '2' -> 2, '1, pitch varies' -> 1. Loops keep one."""
    if is_loop:
        return 1
    nums = [int(n) for n in re.findall(r"\d+", str(text).split(",")[0])]
    return max(nums) if nums else 1


def load_sound_list(cfg):
    """(categories by id in list order, sounds) with each sound's candidates: yours first, removed ones left out."""
    path = REPO / cfg["sound_list"]
    if not path.exists():
        sys.exit(f"No sound list at {path}.")
    data = json.loads(path.read_text(encoding="utf-8"))
    categories = {c["id"]: c for c in data["categories"]}
    sounds = []
    for s in data["sounds"]:
        excluded = {norm(p) for p in s.get("excluded", [])}
        cands = [c for c in s.get("candidates", []) if norm(c["path"]) not in excluded]
        s["candidates"] = [c for c in cands if c.get("user")] + [c for c in cands if not c.get("user")]
        sounds.append(s)
    return categories, sounds


def norm(path):
    return str(path).replace("\\", "/").lower()


def load_type(seconds, loop, category):
    if category == "ambience" and seconds > 20:
        return "Streaming"
    if seconds <= 2.5 and not loop:
        return "DecompressOnLoad"
    return "CompressedInMemory"


# --------------------------------------------------------------------------- wav io

def read_wav(path):
    """Returns (float32 array [frames, channels], sample rate). PCM 8/16/24/32, float 32/64, RF64."""
    with open(long_path(path), "rb") as f:
        data = f.read()
    if data[8:12] != b"WAVE" or data[:4] not in (b"RIFF", b"RF64", b"BW64"):
        raise ValueError("not a WAV file")
    pos, fmt, raw, ds64_size = 12, None, None, None
    while pos + 8 <= len(data):
        cid, size = data[pos:pos + 4], struct.unpack("<I", data[pos + 4:pos + 8])[0]
        body = pos + 8
        if cid == b"ds64":
            ds64_size = struct.unpack("<Q", data[body + 8:body + 16])[0]
        elif cid == b"fmt ":
            fmt = struct.unpack("<HHIIHH", data[body:body + 16])
            if fmt[0] == 0xFFFE and size >= 26:
                fmt = (struct.unpack("<H", data[body + 24:body + 26])[0],) + fmt[1:]
        elif cid == b"data":
            if size == 0xFFFFFFFF and ds64_size is not None:
                size = ds64_size
            raw = data[body:body + size]
            break
        pos = body + size + (size & 1)
    if fmt is None or raw is None:
        raise ValueError("missing fmt or data chunk")
    tag, channels, rate, _, _, bits = fmt
    width = bits // 8
    raw = raw[:len(raw) - len(raw) % (width * channels)]
    if tag == 3:
        x = np.frombuffer(raw, dtype="<f4" if bits == 32 else "<f8").astype(np.float32)
    elif bits == 8:
        x = (np.frombuffer(raw, dtype=np.uint8).astype(np.float32) - 128) / 128
    elif bits == 16:
        x = np.frombuffer(raw, dtype="<i2").astype(np.float32) / 32768
    elif bits == 24:
        b = np.frombuffer(raw, dtype=np.uint8).reshape(-1, 3).astype(np.int32)
        v = b[:, 0] | (b[:, 1] << 8) | (b[:, 2] << 16)
        v = np.where(v >= 1 << 23, v - (1 << 24), v)
        x = v.astype(np.float32) / (1 << 23)
    elif bits == 32:
        x = np.frombuffer(raw, dtype="<i4").astype(np.float32) / 2147483648
    else:
        raise ValueError(f"unsupported bit depth {bits}")
    return x.reshape(-1, channels), rate


def write_wav(path, x, rate, seed=0):
    """16-bit PCM with TPDF dither."""
    rng = np.random.default_rng(seed)
    d = (rng.random(x.shape) - rng.random(x.shape)) / 32768
    pcm = np.clip(np.round((x + d) * 32767), -32768, 32767).astype("<i2")
    ch = x.shape[1]
    body = pcm.tobytes()
    head = struct.pack("<4sI4s4sIHHIIHH4sI", b"RIFF", 36 + len(body), b"WAVE", b"fmt ", 16, 1, ch, rate,
                       rate * ch * 2, ch * 2, 16, b"data", len(body))
    Path(path).parent.mkdir(parents=True, exist_ok=True)
    with open(long_path(path), "wb") as f:
        f.write(head + body)


# --------------------------------------------------------------------------- dsp

def to_channels(x, n):
    c = x.shape[1]
    if c == n:
        return x
    if n == 1:
        # 4 channels = ambisonic A-format in these bundles: the sum is an omni.
        src = x if c in (2, 4) else x[:, :2]
        return src.mean(axis=1, keepdims=True)
    if c == 1:
        return np.repeat(x, 2, axis=1)
    if c == 4:
        # A-format FLU, FRD, BLD, BRU -> left pair and right pair.
        return np.stack([(x[:, 0] + x[:, 2]) / 2, (x[:, 1] + x[:, 3]) / 2], axis=1)
    return x[:, :2]


def downsample(x, rate, max_rate):
    """Halve the rate (with an anti-alias FIR) while it is above max_rate and stays >= 44.1 kHz."""
    while rate > max_rate and rate % 2 == 0 and rate // 2 >= 44100:
        n = np.arange(-48, 49)
        h = np.sinc(0.45 * n) * 0.45 * np.blackman(len(n))
        h /= h.sum()
        x = np.stack([np.convolve(x[:, c], h, mode="same") for c in range(x.shape[1])], axis=1)[::2]
        rate //= 2
    return x.astype(np.float32), rate


def frame_db(x, rate):
    hop = max(1, int(rate * FRAME))
    m = np.abs(x).max(axis=1)
    n = len(m) // hop
    if n == 0:
        return np.array([-120.0]), hop
    e = np.sqrt((m[:n * hop].reshape(n, hop) ** 2).mean(axis=1))
    return 20 * np.log10(e + 1e-9), hop


def _biquad_response(b, a, w):
    z = np.exp(-1j * w)
    return (b[0] + b[1] * z + b[2] * z * z) / (a[0] + a[1] * z + a[2] * z * z)


def _k_weighting(rate, nfreq):
    """BS.1770 K-weighting magnitude on an rfft grid (phase doesn't matter for loudness)."""
    w = np.linspace(0, np.pi, nfreq)
    # Stage 1: high shelf.
    f0, g, q = 1681.974450955533, 3.999843853973347, 0.7071752369554196
    k = np.tan(np.pi * f0 / rate)
    vh = 10 ** (g / 20)
    vb = vh ** 0.4996667741545416
    a0 = 1 + k / q + k * k
    b1 = np.array([(vh + vb * k / q + k * k) / a0, 2 * (k * k - vh) / a0, (vh - vb * k / q + k * k) / a0])
    a1 = np.array([1, 2 * (k * k - 1) / a0, (1 - k / q + k * k) / a0])
    # Stage 2: high pass.
    f0, q = 38.13547087602444, 0.5003270373238773
    k = np.tan(np.pi * f0 / rate)
    a0 = 1 + k / q + k * k
    b2 = np.array([1, -2, 1])
    a2 = np.array([1, 2 * (k * k - 1) / a0, (1 - k / q + k * k) / a0])
    return np.abs(_biquad_response(b1, a1, w) * _biquad_response(b2, a2, w))


def loudness(x, rate):
    """Integrated loudness (LUFS) with the BS.1770 gates; clips under 400 ms use their mean square."""
    n = len(x)
    if n < 16:
        return -120.0
    h = _k_weighting(rate, n // 2 + 1)
    y = np.fft.irfft(np.fft.rfft(x, axis=0) * h[:, None], n=n, axis=0)
    block, step = int(0.4 * rate), int(0.1 * rate)
    if n < block:
        ms = (y ** 2).mean(axis=0).sum()
        return float(-0.691 + 10 * np.log10(ms + 1e-12))
    starts = np.arange(0, n - block + 1, step)
    cs = np.concatenate([np.zeros((1, y.shape[1])), np.cumsum(y.astype(np.float64) ** 2, axis=0)])
    ms = ((cs[starts + block] - cs[starts]) / block).sum(axis=1)
    ms = ms[-0.691 + 10 * np.log10(ms + 1e-12) > -70]
    if len(ms) == 0:
        return -120.0
    rel = -0.691 + 10 * np.log10(ms.mean()) - 10
    ms = ms[-0.691 + 10 * np.log10(ms) > rel]
    return float(-0.691 + 10 * np.log10(ms.mean() + 1e-12))


def fade(x, rate, fin, fout):
    x = x.copy()
    a, b = min(len(x), int(fin * rate)), min(len(x), int(fout * rate))
    if a:
        x[:a] *= np.linspace(0, 1, a, dtype=np.float32)[:, None]
    if b:
        x[-b:] *= (np.linspace(1, 0, b, dtype=np.float32) ** 2)[:, None]
    return x


# --------------------------------------------------------------------------- one-shots and loops

def find_events(x, rate, sp, max_len):
    """Regions of the file that hold one event each: [(start, end)] in samples, plus the noise floor in dB."""
    db, hop = frame_db(x, rate)
    peak, floor = db.max(), float(np.percentile(db, 10))
    thr = max(floor + 12, peak - sp["threshold_db"])
    if peak - floor < 12:  # one steady sound with no real gaps
        thr = peak - 40
    active = db > thr
    runs, i = [], 0
    while i < len(active):
        if active[i]:
            j = i
            while j < len(active) and active[j]:
                j += 1
            runs.append([i, j])
            i = j
        else:
            i += 1
    gap = int(sp["min_gap"] / FRAME)
    merged = []
    for r in runs:
        if merged and r[0] - merged[-1][1] < gap:
            merged[-1][1] = r[1]
        else:
            merged.append(r)
    tail_frames = int(sp["max_tail"] / FRAME)
    events = []
    for k, (a, b) in enumerate(merged):
        if (b - a) * FRAME < sp["min_len"] or db[a:b].max() < peak - sp["quietest_event_db"]:
            continue
        limit = min(len(db), b + tail_frames, merged[k + 1][0] if k + 1 < len(merged) else len(db))
        while b < limit and db[b] > floor + 6:  # let the release ring out
            b += 1
        s = max(0, a * hop - int(sp["pre_roll"] * rate))
        e = min(len(x), b * hop)
        # Long continuous regions (rain, a fountain) become several fixed-length chunks.
        step = int(max_len * rate)
        while e - s > step * 1.25:
            events.append((s, s + step))
            s += step
        events.append((s, e))
    events.sort(key=lambda r: -float(np.abs(x[r[0]:r[1]]).max()))
    return sorted(events[:sp["max_clips_per_file"]]), floor


def make_loop(x, rate, seconds, crossfade):
    """Cuts the steadiest stretch of the file and crossfades its end into its start."""
    total = len(x) / rate
    xf = min(crossfade, max(0.05, total * 0.15))
    length = min(seconds, total - xf)
    if length < 0.5:
        return None, 0.0, 0.0
    n, nx = int(length * rate), int(xf * rate)
    db, hop = frame_db(x, rate)
    w = max(1, (n + nx) // hop)
    if len(db) > w:
        cs, cs2 = np.cumsum(np.r_[0, db]), np.cumsum(np.r_[0, db ** 2])
        mean = (cs[w:] - cs[:-w]) / w
        std = np.sqrt(np.maximum((cs2[w:] - cs2[:-w]) / w - mean ** 2, 0))
        ok = mean >= np.median(mean) - 3  # skip quiet stretches
        best = int(np.argmin(np.where(ok, std, np.inf)))
        start, steadiness = best * hop, float(std[best])
    else:
        start, steadiness = 0, float(db.std())
    start = max(0, min(start, len(x) - n - nx))
    a = x[start:start + n + nx]
    out = a[nx:nx + n].copy()
    t = np.linspace(0, np.pi / 2, nx, dtype=np.float32)[:, None]
    out[-nx:] = a[n:n + nx] * np.cos(t) + a[:nx] * np.sin(t)  # equal power: the end flows into out[0]
    return out, start / rate, steadiness


# --------------------------------------------------------------------------- ranking

def score(c, st):
    """Higher is better: clean (high SNR), not clipped, a sensible length, early in the scout's ranking."""
    s = min(c["snr_db"], 40) + max(0, 8 - 1.5 * (c["rank"] - 1))
    s -= min(20, c["clipped_src"] * 2e4)
    if st["loop"]:
        s -= 3 * c["steadiness"]
    else:
        if c["seconds"] < 0.08:
            s -= 15
        if c["seconds"] > st["max_len"] * 1.1:
            s -= 5
    if c["peak_limited"]:
        s -= 2
    return s


def default_keep(clips, count):
    """Best clip of each source file first, then the second best of each, and so on, so the variations
    don't all come from one recording."""
    by_source = {}
    for c in sorted(clips, key=lambda c: -c["score"]):
        by_source.setdefault(c["rank"], []).append(c)
    queues = sorted(by_source.values(), key=lambda q: (not q[0]["user"], -q[0]["score"]))  # your picks first
    picked = []
    while len(picked) < count and any(queues):
        for q in queues:
            if q and len(picked) < count:
                picked.append(q.pop(0))
    return picked


# --------------------------------------------------------------------------- process

def process_sound(cfg, categories, sound, cache):
    sid = sound["id"]
    st = sound_settings(categories, sound)
    clip_dir = cache / "clips" / sid
    shutil.rmtree(long_path(clip_dir), ignore_errors=True)
    clips, missing = [], []
    for rank, cand in enumerate(sound.get("candidates", []), 1):
        src = Path(cfg["library_dir"]) / cand["path"]
        try:
            x, rate = read_wav(src)
        except FileNotFoundError:
            missing.append(cand["path"])
            continue
        except Exception as e:
            print(f"  skip {cand['file']}: {e}", file=sys.stderr)
            continue
        clipped_src = float((np.abs(x) >= 0.999).mean())
        x = to_channels(x, st["channels"])
        if st["loop"]:
            loop, at, steady = make_loop(x, rate, st.get("loop_seconds", cfg["loop"]["seconds"]),
                                         st.get("crossfade", cfg["loop"]["crossfade"]))
            pieces, floor = ([(loop, at, steady)] if loop is not None else []), None
        else:
            events, floor = find_events(x, rate, cfg["split"], st["max_len"])
            pieces = [(x[a:b], a / rate, 0.0) for a, b in events]
        for k, (y, at, steady) in enumerate(pieces, 1):
            y, r = downsample(y - y.mean(axis=0), rate, cfg["max_sample_rate"])
            if not st["loop"]:
                y = fade(y, r, cfg["fade_in"], cfg["fade_out"])
            lufs = loudness(y, r)
            gain = float(st["lufs"] - lufs)
            peak = float(20 * np.log10(np.abs(y).max() + 1e-9))
            limited = peak + gain > cfg["peak_ceiling_db"]
            if limited:
                gain = cfg["peak_ceiling_db"] - peak
            y = y * np.float32(10 ** (gain / 20))
            # Named after the source file, not its rank, so ticks survive adding or removing other picks.
            cid = f"{zlib.crc32(norm(cand['path']).encode()):08x}_{k:02d}"
            rel = f"clips/{sid}/{sid}__{cid}.wav"
            write_wav(cache / rel, y, r, seed=rank * 1000 + k)
            db, _ = frame_db(y, r)
            snr = float(db.max() - (floor if floor is not None else np.percentile(db, 10)))
            seconds = round(len(y) / r, 3)
            clips.append({
                "id": cid, "file": rel, "source": cand["file"], "library": cand.get("library", ""),
                "path": cand["path"], "user": bool(cand.get("user")), "rank": rank, "at": round(at, 2),
                "seconds": seconds, "rate": r, "channels": int(y.shape[1]),
                "lufs_in": round(lufs, 1), "gain_db": round(gain, 1), "peak_limited": bool(limited),
                "snr_db": round(snr, 1), "clipped_src": clipped_src, "steadiness": round(steady, 2),
                "load_type": load_type(seconds, st["loop"], sound["category"]),
                "quality": 0.6 if sound["category"] == "ambience" else 0.7,
            })
    for c in clips:
        c["score"] = round(score(c, st), 1)
    chosen = {c["id"] for c in default_keep(clips, st["count"])}
    for c in clips:
        c["keep"] = c["id"] in chosen
    return {"id": sid, "count": st["count"], "loop": st["loop"], "processed": time.strftime("%Y-%m-%d %H:%M:%S"),
            "missing": missing, "clips": clips}


def process(args):
    cfg = load_config(args.config)
    if args.library_dir:
        cfg["library_dir"] = args.library_dir
    if not cfg.get("library_dir"):
        sys.exit("No SFX library folder: set it in the SFX Prep window's settings (or library_dir in the config).")
    cache = REPO / cfg["cache_dir"]
    categories, all_sounds = load_sound_list(cfg)
    sounds = [s for s in all_sounds if not args.sound or s["id"] in args.sound]
    manifest_path = cache / "prep_manifest.json"
    entries = {}
    if args.sound and manifest_path.exists():
        entries = {e["id"]: e for e in json.loads(manifest_path.read_text(encoding="utf-8"))["sounds"]}
    t0 = time.time()
    for i, sound in enumerate(sounds, 1):
        e = process_sound(cfg, categories, sound, cache)
        entries[e["id"]] = e
        note = f", {len(e['missing'])} missing" if e["missing"] else ""
        print(f"[{i}/{len(sounds)}] {e['id']}: {len(e['clips'])} clips{note} ({time.time() - t0:.0f}s)", flush=True)
    # Sounds removed from the list drop out of the manifest (and their clips from the cache).
    listed = [s["id"] for s in all_sounds]
    for sid in set(entries) - set(listed):
        shutil.rmtree(long_path(cache / "clips" / sid), ignore_errors=True)
    cache.mkdir(parents=True, exist_ok=True)
    manifest = {"created": time.strftime("%Y-%m-%d %H:%M:%S"), "library_dir": cfg["library_dir"],
                "sounds": [entries[sid] for sid in listed if sid in entries]}
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=1), encoding="utf-8")
    print(f"Done: {manifest_path}", flush=True)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    p = sub.add_parser("process")
    p.add_argument("--sound", action="append", help="only this sound id (repeatable)")
    p.add_argument("--project-root", default=".", help="the Unity project (default: current folder)")
    p.add_argument("--config", default="Tools/SfxPrep/sfx_config.json", help="relative to the project root")
    p.add_argument("--library-dir", help="folder the candidates' paths are relative to; overrides the config")
    args = ap.parse_args()
    global REPO
    REPO = Path(args.project_root).resolve()
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")
    process(args)


if __name__ == "__main__":
    main()
