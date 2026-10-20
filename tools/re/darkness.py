"""Exact fixed-point emulation of the 1.19.3 province darkness (findings/S-NIGHT-darkness.md).
province_vectors(x, y) once per province, then darkness(vectors, day_of_year, hour_of_day)."""
import struct, math
def f32fx(v): return int(round(struct.unpack('f', struct.pack('f', v))[0] * 100000))
def tdiv(a, b):  # C division, truncation toward zero
    q = abs(a) // abs(b)
    return q if (a >= 0) == (b >= 0) else -q
def mul(a, b): return tdiv(a * b, 100000)
def div(a, b): return tdiv(a * 100000, b)
PI, TWO_PI, HALF_PI = 314159, 628319, 157080
def sin_q(x):  # 0x24ef700: x - x^3/6 + x^5/120
    acc, p, fac, k, add = 0, x, 1, 2, True
    for _ in range(3):
        t = div(p, fac * 100000) if False else tdiv(p * 100000, fac * 100000)
        acc = acc + t if add else acc - t
        add = not add
        p = mul(mul(p, x), x)
        fac *= (k + 1) * k; k += 2
    return acc
def _red(x):
    r = x - tdiv(x, TWO_PI) * TWO_PI
    if r < 0: r += TWO_PI
    neg = r > PI
    if r > PI: r -= PI
    a = r if r <= HALF_PI else 314160 - r
    s = sin_q(a)
    return -s if neg else s
def fsin(x): return _red(x)
def fcos(x): return _red(x + HALF_PI)
def isqrt_game(n):
    if n <= 1: return n
    x = n // 2
    while True:
        prev = x
        x = (tdiv(n, x) + x) // 2
        if not x < prev: return prev
def normalize(v):
    n = sum(c * c for c in v)
    l = isqrt_game(n)
    l = ((l + 2**31) % 2**32) - 2**31
    if l == 0: return [-1, -1, -1]
    return [tdiv(c * 100000, l) for c in v]
GMT, W, H = 2793, 5632, 2048
S, N, FEATHER = f32fx(0.17), f32fx(0.93), f32fx(0.024)
def province_vectors(x, y):
    tz = tdiv((x - GMT) * 10**10, W * 100000) + 100000
    yf = tdiv(y * 10**10, H * 100000)
    lat = mul(N - S, yf) + S
    c = -fcos(mul(lat, PI))
    r = 100000 - abs(c)
    out = []
    for h in range(24):
        hf = tdiv(h * 10**10, 2400000)
        t = tz + hf
        t = t - tdiv(t, 100000) * 100000
        a = tdiv(t * 628318, 100000)
        out.append(normalize([mul(r, fsin(a)), c, mul(fcos(a), r)]))
    return out
MONTH = [31,28,31,30,31,30,31,31,30,31,30,31]
def doy(m, d): return sum(MONTH[:m-1]) + d - 1
J, D = doy(6, 22), doy(12, 22)
def sun(d):
    if J <= d <= D:
        t = div((d - J) * 100000, (D - J) * 100000); s = 10 * t - 500000
    elif d > D:
        t = div((d - D) * 100000, (J + 365 - D) * 100000); s = 10 * (50000 - t)
    else:
        t = div((d - (D - 365)) * 100000, (J - (D - 365)) * 100000); s = 10 * (50000 - t)
    deg = tdiv(s * 100000, 18000000)
    ang = mul(deg, PI)
    cs = max(-100000, min(100000, fcos(ang))); sn = max(-100000, min(100000, fsin(ang)))
    # rotate (0, 0, 1): y = sin*z + y*cos, z = z*cos - y*sin
    return (0, mul(sn, 100000), mul(100000, cs) - 0)
def darkness(vecs, d, h):
    sv = sun(d)
    v = vecs[h % 24]
    dot = mul(sv[1], v[1]) + mul(sv[2], v[2]) + mul(sv[0], v[0])
    A = mul(FEATHER, 50000); B = -A
    inv = -1 if A == B else tdiv(10**10, A - B)
    val = mul(dot - B, inv)
    return max(0, min(100000, val)) / 100000
