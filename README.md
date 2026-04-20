# Protocol Benchmark V3
## REST • SSE • WebSocket • MQTT

> **Yüksek Lisans Tezi** | Ahmet Yesevi Üniversitesi  
> Danışman: Dr. Öğr. Üyesi Bilgin Avenoğlu  
> Saf .NET 8.0 Console — ASP.NET Core bağımlılığı yok

---

## 📁 Proje Yapısı

```
ProtocolBenchmarkV3/
├── ProtocolBenchmark.Models/      # Ortak modeller, ScenarioRegistry
├── ProtocolBenchmark.Server/      # Console sunucu (4 protokol)
├── ProtocolBenchmark.Client/      # Console istemci + Checkpoint
├── ProtocolBenchmark.Benchmarks/  # BenchmarkDotNet mikro testler
├── ProtocolBenchmark.Analysis/    # İstatistiksel analiz + hipotez
├── jmeter/                        # Apache JMeter .jmx planları
├── analyze.py                     # Python görselleştirme
└── ProtocolBenchmark.sln
```

---

## ⚙️ Gereksinimler

| Araç | Sürüm | Zorunlu |
|------|-------|---------|
| .NET SDK | 8.0+ | ✅ |
| Python | 3.10+ | Grafikler için |
| Apache JMeter | 5.6+ | Çapraz doğrulama için |
| JMeter WS Plugin | Son sürüm | WebSocket testi için |
| JMeter MQTT Plugin | Son sürüm | MQTT testi için |

### Python kurulum
```bash
pip install pandas matplotlib seaborn scipy numpy
```

### JMeter Plugin Kurulum
1. WebSocket: https://bitbucket.org/pjtr/jmeter-websocket-samplers
2. MQTT: https://github.com/emqx/mqtt-jmeter
3. İndirilen .jar dosyaları → `JMeter/lib/ext/` klasörüne kopyala

---

## 🚀 Adım Adım Çalıştırma

### 1 — Restore
```bash
dotnet restore
```

### 2 — Sunucuyu başlat (Terminal 1)
```bash
cd ProtocolBenchmark.Server
dotnet run
```

Çıktı:
```
REST      → http://localhost:5000
SSE       → http://localhost:5001
WebSocket → ws://localhost:5002
MQTT      → localhost:1883
```

### 3 — Test istemcisini çalıştır (Terminal 2)
```bash
cd ProtocolBenchmark.Client
dotnet run
```

Menüden seçim yapın:
- Protokol: REST / SSE / WebSocket / MQTT / Tümü
- Senaryo: S1–S9 veya tüm senaryolar (0)

**Checkpoint:** Yazılım çökerse yeniden başlatın — kaldığı yerden devam eder.

### 4 — JMeter çapraz doğrulama
```bash
# Sunucu çalışırken:
jmeter -n -t jmeter/REST_Benchmark.jmx -l jmeter-results/REST.jtl
jmeter -n -t jmeter/SSE_Benchmark.jmx  -l jmeter-results/SSE.jtl
jmeter -n -t jmeter/WebSocket_Benchmark.jmx -l jmeter-results/WS.jtl
jmeter -n -t jmeter/MQTT_Benchmark.jmx -l jmeter-results/MQTT.jtl
```

### 5 — İstatistiksel analiz (.NET)
```bash
cd ProtocolBenchmark.Analysis
dotnet run
# veya farklı klasör:
dotnet run -- /path/to/results
```

### 6 — BenchmarkDotNet (Release zorunlu)
```bash
cd ProtocolBenchmark.Benchmarks
dotnet run -c Release
```

### 7 — Python grafikleri
```bash
python analyze.py
python analyze.py --dir ProtocolBenchmark.Client/results
```

---

## 🧪 Test Senaryoları

| # | Senaryo | Tür | İstemci | Süre | Amaç |
|---|---------|-----|---------|------|------|
| S1 | Smoke | Doğrulama | 1 | 30 sn | Sistem çalışıyor mu? |
| S2 | LightLoad | Yük | 10 | 2 dk | Düşük yük |
| S3 | NormalLoad | Yük | 50 | 5 dk | Tipik yük |
| S4 | HeavyLoad | Yük | 200 | 5 dk | Yüksek yük |
| S5 | Stress | Stres | 500→∞ | Çökene dek | Kırılma noktası |
| S6 | Spike | Ani yük | 10→500→10 | 3 dk | Ani yük tepkisi |
| S7 | Endurance | Dayanıklılık | 50 | 30 dk | Gecikme kayması + bellek |
| S8 | SmallPayload | Payload | 50 | 3 dk | 100B mesaj |
| S9 | LargePayload | Payload | 50 | 3 dk | 100KB mesaj |

---

## 📊 Ölçülen Metrikler

**Performans:** Gecikme (ort/min/P95/P99/std), TTFB, Bağlantı kurulum süresi, Throughput  
**Güvenilirlik:** Hata oranı, Mesaj kaybı oranı, Bağlantı kopma sayısı  
**Kaynak:** Bellek (ort/peak), Payload boyutu etkisi

---

## 🔄 Checkpoint Sistemi

Test ilerlemesi `results/checkpoint.json` dosyasına kaydedilir.

```json
{
  "sessionId": "2026-03-19_14-32",
  "startedAt": "2026-03-19T14:32:00",
  "completed": ["REST_S1_Smoke", "REST_S2_LightLoad", ...],
  "remaining": ["SSE_S3_NormalLoad", ...]
}
```

Yazılım yeniden başlatıldığında otomatik olarak kaldığı yerden devam eder.

---

## 📤 Üretilen Çıktılar

```
results/
  checkpoint.json          ← İlerleme durumu
  REST_S1_Smoke.csv        ← Ham ölçüm verileri (36 dosya)
  ...

output/
  summary.csv              ← Senaryo özetleri (tez tabloları)
  hypotheses.csv           ← Hipotez değerlendirme sonuçları
  report.txt               ← Tam metin raporu
  genel_ozet.csv           ← Python genel istatistik
  plots/
    01_latency_boxplot.png
    02_avg_latency_bar.png
    03_p95_heatmap.png
    04_throughput_timeseries.png
    05_endurance_memory.png
    06_payload_sensitivity.png

jmeter-results/
  REST_ALL.csv             ← JMeter çapraz doğrulama
  SSE_ALL.csv
  WebSocket_ALL.csv
  MQTT_ALL.csv

BenchmarkDotNet.Artifacts/
  *.html / *.csv / *.md    ← Mikro benchmark raporları
```

---

## 🏗️ Mimari Notlar

- **ASP.NET Core yok** → REST=HttpListener, SSE=HttpListener+stream, WS=System.Net.WebSockets
- **MQTT Broker gömülü** → MQTTnet embedded broker, harici kurulum gerekmez
- **Ayrı portlar** → REST:5000, SSE:5001, WS:5002, MQTT:1883
- **Adil karşılaştırma** → Tüm protokoller aynı `BenchmarkPayload` modelini taşır
- **Tekrarlanabilir** → Random(seed:42) ile sabit test verisi

---


