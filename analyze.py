"""
Protocol Benchmark V3 – Python Görselleştirme & İstatistik
===========================================================
Kurulum  : pip install pandas matplotlib seaborn scipy numpy
Kullanım : python analyze.py
           python analyze.py --dir path/to/results
"""
import argparse, glob, os, warnings
import numpy as np
import pandas as pd
import matplotlib.pyplot as plt
import matplotlib.ticker as mticker
import seaborn as sns
from scipy import stats

warnings.filterwarnings("ignore")
sns.set_theme(style="whitegrid", palette="muted", font_scale=1.1)

COLORS = {
    "REST":      "#4C72B0",
    "SSE":       "#C44E52",
    "WebSocket": "#DD8452",
    "MQTT":      "#55A868",
}
OUT = "output/plots"
os.makedirs(OUT, exist_ok=True)

parser = argparse.ArgumentParser()
parser.add_argument("--dir", default="../ProtocolBenchmark.Client/results")
args = parser.parse_args()

# ── Veri yükle ────────────────────────────────────────────────────────────────
files = glob.glob(os.path.join(args.dir, "*.csv"))
if not files:
    print(f"CSV bulunamadı: {args.dir}")
    raise SystemExit(1)

df = pd.concat([pd.read_csv(f) for f in files], ignore_index=True)
df = df[df["IsSuccess"] == True].copy()
df["LatencyMs"] = pd.to_numeric(df["LatencyMs"], errors="coerce")
df["TtfbMs"]    = pd.to_numeric(df["TtfbMs"],    errors="coerce")
df["MemoryBytes"]= pd.to_numeric(df["MemoryBytes"], errors="coerce")
df["MemoryMB"]  = df["MemoryBytes"] / 1024 / 1024
df.dropna(subset=["LatencyMs"], inplace=True)
print(f"✅ {len(df):,} başarılı ölçüm yüklendi.\n")

protocols = [p for p in ["REST","SSE","WebSocket","MQTT"] if p in df["Protocol"].unique()]
scenarios = sorted(df["Scenario"].unique())


# ── 1. Boxplot: Gecikme Dağılımı ─────────────────────────────────────────────
def plot_boxplot():
    n = len(scenarios)
    cols = 3; rows = (n + cols - 1) // cols
    fig, axes = plt.subplots(rows, cols, figsize=(16, 5 * rows))
    axes = axes.flatten()

    for i, sc in enumerate(scenarios):
        ax = axes[i]
        sub  = df[df["Scenario"] == sc]
        data = [sub[sub["Protocol"] == p]["LatencyMs"].dropna().values for p in protocols]
        bp = ax.boxplot(data, labels=protocols, patch_artist=True,
                        showfliers=False, widths=0.5)
        for patch, proto in zip(bp["boxes"], protocols):
            patch.set_facecolor(COLORS.get(proto, "#aaa"))
            patch.set_alpha(0.75)
        ax.set_title(sc, fontweight="bold", fontsize=10)
        ax.set_ylabel("Gecikme (ms)")

    for j in range(i + 1, len(axes)):
        axes[j].set_visible(False)

    plt.suptitle("Gecikme Dağılımı – Senaryo Bazlı (Aykırı Değerler Hariç)",
                 fontsize=13, fontweight="bold")
    plt.tight_layout()
    p = f"{OUT}/01_latency_boxplot.png"
    plt.savefig(p, dpi=150, bbox_inches="tight"); plt.close()
    print(f"  📊 {p}")


# ── 2. Bar: Ortalama Gecikme ──────────────────────────────────────────────────
def plot_avg_bar():
    summary = (df.groupby(["Scenario","Protocol"])["LatencyMs"]
                 .mean().reset_index()
                 .rename(columns={"LatencyMs":"Avg"}))
    pivot = summary.pivot(index="Scenario", columns="Protocol", values="Avg")

    ax = pivot.plot(kind="bar", figsize=(14, 6),
                    color=[COLORS.get(c,"#aaa") for c in pivot.columns],
                    edgecolor="white", width=0.65)
    ax.set_title("Senaryo Bazlı Ortalama Gecikme Karşılaştırması",
                 fontsize=13, fontweight="bold")
    ax.set_xlabel("Senaryo"); ax.set_ylabel("Ort. Gecikme (ms)")
    ax.legend(title="Protokol"); plt.xticks(rotation=30, ha="right")
    plt.tight_layout()
    p = f"{OUT}/02_avg_latency_bar.png"
    plt.savefig(p, dpi=150, bbox_inches="tight"); plt.close()
    print(f"  📊 {p}")


# ── 3. P95 Heatmap ────────────────────────────────────────────────────────────
def plot_heatmap():
    p95 = (df.groupby(["Protocol","Scenario"])["LatencyMs"]
             .quantile(0.95).unstack())
    plt.figure(figsize=(14, 4))
    sns.heatmap(p95, annot=True, fmt=".1f", cmap="YlOrRd",
                linewidths=0.5, cbar_kws={"label":"P95 Gecikme (ms)"})
    plt.title("P95 Gecikme Isı Haritası (ms)", fontsize=13, fontweight="bold")
    plt.tight_layout()
    p = f"{OUT}/03_p95_heatmap.png"
    plt.savefig(p, dpi=150, bbox_inches="tight"); plt.close()
    print(f"  📊 {p}")


# ── 4. Throughput bar ─────────────────────────────────────────────────────────
def plot_throughput():
    df["Timestamp"] = pd.to_datetime(df["Timestamp"], utc=True, errors="coerce")
    df_s = df.dropna(subset=["Timestamp"]).sort_values("Timestamp")

    fig, axes = plt.subplots(1, len(protocols), figsize=(16, 4), sharey=True)
    for ax, proto in zip(axes, protocols):
        sub = df_s[df_s["Protocol"] == proto].copy()
        if sub.empty: continue
        sub["Second"] = (sub["Timestamp"] - sub["Timestamp"].min()).dt.total_seconds().astype(int)
        tp = sub.groupby("Second").size().reset_index(name="MsgPerSec")
        ax.fill_between(tp["Second"], tp["MsgPerSec"],
                        color=COLORS.get(proto,"#aaa"), alpha=0.4)
        ax.plot(tp["Second"], tp["MsgPerSec"],
                color=COLORS.get(proto,"#aaa"), linewidth=1.5)
        ax.set_title(proto, fontweight="bold"); ax.set_xlabel("Süre (s)")
    axes[0].set_ylabel("Mesaj/saniye")
    plt.suptitle("Throughput Zaman Serisi", fontsize=13, fontweight="bold")
    plt.tight_layout()
    p = f"{OUT}/04_throughput_timeseries.png"
    plt.savefig(p, dpi=150, bbox_inches="tight"); plt.close()
    print(f"  📊 {p}")


# ── 5. Bellek kullanımı (Endurance testi) ─────────────────────────────────────
def plot_memory():
    end = df[df["Scenario"] == "S7_Endurance"].copy()
    if end.empty: return

    end["Timestamp"] = pd.to_datetime(end["Timestamp"], utc=True, errors="coerce")
    end = end.dropna(subset=["Timestamp"]).sort_values("Timestamp")

    fig, ax = plt.subplots(figsize=(12, 5))
    for proto in protocols:
        sub = end[end["Protocol"] == proto].copy()
        if sub.empty: continue
        sub["Second"] = (sub["Timestamp"] - sub["Timestamp"].min()).dt.total_seconds().astype(int)
        mem = sub.groupby("Second")["MemoryMB"].mean().reset_index()
        ax.plot(mem["Second"], mem["MemoryMB"],
                label=proto, color=COLORS.get(proto,"#aaa"), linewidth=2)

    ax.set_title("Dayanıklılık Testi – Bellek Kullanımı (30 dk)", fontsize=13, fontweight="bold")
    ax.set_xlabel("Süre (s)"); ax.set_ylabel("Bellek (MB)"); ax.legend()
    plt.tight_layout()
    p = f"{OUT}/05_endurance_memory.png"
    plt.savefig(p, dpi=150, bbox_inches="tight"); plt.close()
    print(f"  📊 {p}")


# ── 6. Payload hassasiyet analizi ─────────────────────────────────────────────
def plot_payload():
    small = df[df["Scenario"] == "S8_SmallPayload"]
    large = df[df["Scenario"] == "S9_LargePayload"]
    if small.empty or large.empty: return

    fig, axes = plt.subplots(1, 2, figsize=(14, 5))
    for ax, sub, title in zip(axes, [small, large], ["100B Payload", "100KB Payload"]):
        means = sub.groupby("Protocol")["LatencyMs"].mean().reindex(protocols)
        colors = [COLORS.get(p,"#aaa") for p in means.index]
        means.plot(kind="bar", ax=ax, color=colors, edgecolor="white")
        ax.set_title(title, fontweight="bold"); ax.set_ylabel("Ort. Gecikme (ms)")
        ax.set_xlabel(""); ax.tick_params(axis="x", rotation=0)

    plt.suptitle("Payload Boyutu Etkisi", fontsize=13, fontweight="bold")
    plt.tight_layout()
    p = f"{OUT}/06_payload_sensitivity.png"
    plt.savefig(p, dpi=150, bbox_inches="tight"); plt.close()
    print(f"  📊 {p}")


# ── 7. Welch t-testi matrisi ──────────────────────────────────────────────────
def print_welch():
    print("\n📐 Welch t-testi (α = 0.05) — Tüm Protokol Çiftleri")
    print(f"{'─'*72}")
    pairs = [(a,b) for i,a in enumerate(protocols) for b in protocols[i+1:]]
    for pa, pb in pairs:
        a = df[df["Protocol"]==pa]["LatencyMs"].dropna().values
        b = df[df["Protocol"]==pb]["LatencyMs"].dropna().values
        if len(a)<2 or len(b)<2: continue
        t,p = stats.ttest_ind(a, b, equal_var=False)
        sig = "✅" if p < 0.05 else "❌"
        pooled = np.sqrt((np.std(a,ddof=1)**2 + np.std(b,ddof=1)**2)/2)
        d = (np.mean(a)-np.mean(b))/pooled if pooled>0 else 0
        effect = ("Küçük" if abs(d)<0.2 else "Orta" if abs(d)<0.5
                  else "Büyük" if abs(d)<0.8 else "Çok Büyük")
        print(f"  {pa+' vs '+pb:<22} t={t:7.3f}  p={p:8.4f}  "
              f"{sig} {'Anlamlı   ' if p<0.05 else 'Anlamsız  '}"
              f"  d={d:6.3f} ({effect})")
    print()


# ── 8. Genel özet ─────────────────────────────────────────────────────────────
def print_summary():
    g = df.groupby("Protocol")["LatencyMs"]
    tbl = pd.DataFrame({
        "Ort(ms)"  : g.mean(),
        "Medyan(ms)": g.median(),
        "P95(ms)"  : g.quantile(0.95),
        "P99(ms)"  : g.quantile(0.99),
        "Std(ms)"  : g.std(),
        "Min(ms)"  : g.min(),
        "Max(ms)"  : g.max(),
    }).round(2).reindex(protocols)
    print("📋 Genel Özet İstatistikler")
    print(tbl.to_string())
    tbl.to_csv("output/genel_ozet.csv")
    print("\n✅ output/genel_ozet.csv kaydedildi.\n")


# ── Ana akış ──────────────────────────────────────────────────────────────────
if __name__ == "__main__":
    print("Grafikler oluşturuluyor...")
    plot_boxplot()
    plot_avg_bar()
    plot_heatmap()
    plot_payload()
    try: plot_throughput()
    except Exception as e: print(f"  ⚠ Throughput atlandı: {e}")
    try: plot_memory()
    except Exception as e: print(f"  ⚠ Bellek grafiği atlandı: {e}")

    print_summary()
    print_welch()

    print(f"\n✅ Tüm çıktılar → {OUT}/")
