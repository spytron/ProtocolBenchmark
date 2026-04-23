# Protocol Benchmark

> **REST, SSE, WebSocket ve MQTT Protokollerinde İstek/Yanıt Yaklaşımının Performans Maliyeti: .NET 8 Ortamında Deneysel Karşılaştırma**


---

## 📋 Proje Hakkında

Bu çalışma; REST, SSE, WebSocket ve MQTT protokollerini **istek/yanıt modeline adapte ederek** aynı .NET 8 ortamında sistematik biçimde karşılaştırmaktadır. Gecikme, throughput, hata oranı ve bellek tüketimi açısından 9 farklı senaryo üzerinde deneysel analiz yapılmıştır.

### Mimarı Seçimler

| Protokol | Altyapı | Port |
|---|---|---|
| REST | ASP.NET Core Kestrel | 5000 |
| SSE | ASP.NET Core Kestrel | 5001 |
| WebSocket | ASP.NET Core Kestrel | 5002 |
| MQTT | Eclipse Mosquitto 2.x + MqttResponder | 1883 |

> **Not:** MQTT ve SSE doğal modellerinin dışında istek/yanıt modeline adapte edilmiştir. Bu tasarım kararı tüm protokolleri aynı ölçüm metodolojisiyle karşılaştırmak için bilinçli olarak alınmıştır.

---

## 🏗️ Proje Yapısı

```
ProtocolBenchmark/
├── ProtocolBenchmark.Server/      # REST, SSE, WebSocket sunucusu + MqttResponder
├── ProtocolBenchmark.Client/      # Test istemcisi + CSV kayıt
├── ProtocolBenchmark.Analysis/    # İstatistiksel analiz + rapor üretimi
├── ProtocolBenchmark.Models/      # Ortak veri modelleri
└── ProtocolBenchmark.Benchmarks/  # BenchmarkDotNet entegrasyonu
```

---

## 📊 Test Senaryoları

| # | Senaryo | Tür | İstemci | Süre | Amaç |
|---|---|---|---|---|---|
| S1 | Smoke | Doğrulama | 1 | 30 sn | Temel doğrulama — JMeter çapraz doğrulamada kullanıldı |
| S2 | LightLoad | Yük | 10 | 2 dk | Düşük yük gecikme ve throughput |
| S3 | NormalLoad | Yük | 50 | 5 dk | Tipik kullanım — JMeter çapraz doğrulamada kullanıldı |
| S4 | HeavyLoad | Yük | 200 | 5 dk | Yüksek eşzamanlı istemci |
| S5 | Stress | Stres | 500→∞ | Çökene dek | Kırılma noktası |
| S6 | Spike | Ani Yük | 10→500→10 | 3 dk | Ani yük tepkisi |
| S7 | Endurance | Dayanıklılık | 50 | 30 dk | Gecikme kayması ve bellek artışı |
| S8 | SmallPayload | Payload | 50 | 3 dk | 100 Byte mesaj etkisi |
| S9 | LargePayload | Payload | 50 | 3 dk | 100 KB — HTTP batching etkisi |

**Toplam:** 36 CSV veri seti (4 protokol × 9 senaryo) + 8 JMeter çapraz doğrulama CSV'si

---

## 📈 Sonuçlar

### Genel Protokol Sıralaması

| Sıra | Protokol | Ort. Gecikme | Throughput | Hata % | RAM |
|---|---|---|---|---|---|
| 🥇 1 | WebSocket | 7,72 ms | 2184,8 msg/s | 0,00% | 178,2 MB |
| 🥈 2 | SSE | 725,24 ms | 196,2 msg/s | 0,02% | 47,3 MB |
| 🥉 3 | REST | 857,68 ms | 163,8 msg/s | 0,01% | 29,4 MB |
| 4 | MQTT | 1073,54 ms | 107,5 msg/s | 0,03% | 33,2 MB |

---

### S1 — Smoke Test (1 İstemci, 30 sn)

| Protokol | Ort(ms) | P95(ms) | P99(ms) | TTFB(ms) | Bağ(ms) | msg/s | Hata% | RAM(MB) |
|---|---|---|---|---|---|---|---|---|
| REST | 32,48 | 248,90 | 248,90 | 32,48 | 0,00 | 2,5 | 0,00 | 0,79 |
| SSE | 5,02 | 16,55 | 16,55 | 4,27 | 0,00 | 2,0 | 0,00 | 27,61 |
| WebSocket | 1,68 | 7,64 | 7,64 | 1,67 | 16,76 | 2,5 | 0,00 | 35,27 |
| MQTT | 13,01 | 27,99 | 27,99 | 13,01 | 32,97 | 2,0 | 0,00 | 38,34 |

---

### S2 — Light Load (10 İstemci, 2 dk)

| Protokol | Ort(ms) | P95(ms) | P99(ms) | TTFB(ms) | Bağ(ms) | msg/s | Hata% | RAM(MB) |
|---|---|---|---|---|---|---|---|---|
| REST | 14,14 | 39,86 | 56,87 | 14,14 | 0,00 | 50,0 | 0,00 | 2,87 |
| SSE | 14,24 | 25,36 | 28,31 | 14,23 | 0,00 | 45,5 | 0,00 | 28,82 |
| WebSocket | 0,60 | 1,41 | 3,94 | 0,60 | 19,47 | 50,0 | 0,00 | 35,28 |
| MQTT | 15,35 | 27,04 | 97,73 | 15,35 | 35,73 | 45,5 | 0,00 | 40,79 |

---

### S3 — Normal Load (50 İstemci, 5 dk)

| Protokol | Ort(ms) | P95(ms) | P99(ms) | TTFB(ms) | Bağ(ms) | msg/s | Hata% | RAM(MB) |
|---|---|---|---|---|---|---|---|---|
| REST | 132,52 | 246,60 | 312,40 | 132,52 | 0,00 | 208,3 | 0,00 | 4,68 |
| SSE | 92,10 | 141,42 | 164,75 | 92,10 | 0,00 | 250,0 | 0,00 | 30,35 |
| WebSocket | 1,37 | 3,57 | 4,86 | 1,37 | 78,01 | 454,5 | 0,00 | 23,27 |
| MQTT | 246,22 | 413,20 | 557,27 | 246,22 | 160,27 | 138,9 | 0,00 | 42,13 |

---

### S4 — Heavy Load (200 İstemci, 5 dk)

| Protokol | Ort(ms) | P95(ms) | P99(ms) | TTFB(ms) | Bağ(ms) | msg/s | Hata% | RAM(MB) |
|---|---|---|---|---|---|---|---|---|
| REST | 866,36 | 1120,30 | 1273,52 | 866,36 | 0,00 | 212,8 | 0,00 | 9,60 |
| SSE | 722,59 | 887,93 | 969,88 | 722,59 | 0,00 | 256,4 | 0,00 | 23,33 |
| WebSocket | 4,21 | 5,23 | 89,86 | 4,21 | 255,45 | 2500,0 | 0,00 | 11,60 |
| MQTT | 1248,92 | 1387,89 | 1578,76 | 1248,92 | 755,61 | 149,3 | 0,00 | 33,23 |

---

### S5 — Stress Test (500+ İstemci, Çökene Dek)

| Protokol | Ort(ms) | P95(ms) | P99(ms) | msg/s | Hata% | Kayıp% | RAM(MB) |
|---|---|---|---|---|---|---|---|
| REST | 4777,31 | 10175,98 | 11459,36 | 132,6 | 0,08 | 0,08 | 46,58 |
| SSE | 3999,84 | 9231,15 | 10702,88 | 241,5 | 0,14 | 0,12 | 48,74 |
| **WebSocket** | **48,52** | **130,33** | **563,59** | **12893,1** | **0,00** | **0,00** | 443,16 |
| MQTT | 4997,46 | 8326,84 | 10184,71 | 103,0 | 0,24 | 0,24 | 30,80 |

---

### S6 — Spike Test (10→500→10 İstemci, 3 dk)

| Protokol | Ort(ms) | P95(ms) | P99(ms) | msg/s | Hata% | RAM(MB) |
|---|---|---|---|---|---|---|
| REST | 1603,82 | 2538,14 | 3000,32 | 215,2 | 0,00 | 57,32 |
| SSE | 1497,07 | 2339,92 | 2713,68 | 228,7 | 0,00 | 124,61 |
| **WebSocket** | **9,10** | **19,25** | **117,94** | **2600,0** | **0,00** | 764,27 |
| MQTT | 2458,67 | 3970,45 | 4938,83 | 123,0 | 0,00 | 7,88 |

---

### S7 — Endurance Test (50 İstemci, 30 dk)

| Protokol | Ort(ms) | P95(ms) | P99(ms) | msg/s | Hata% | RAM(MB) |
|---|---|---|---|---|---|---|
| REST | 110,62 | 177,26 | 252,90 | 235,9 | 0,00 | 30,25 |
| SSE | 87,15 | 129,63 | 158,33 | 264,9 | 0,00 | 37,60 |
| **WebSocket** | **0,72** | **1,88** | **3,33** | **458,5** | **0,00** | 70,65 |
| MQTT | 258,18 | 385,96 | 478,93 | 138,8 | 0,00 | 22,09 |

---

### S8 — Small Payload (50 İstemci, 100 Byte, 3 dk)

| Protokol | Ort(ms) | P95(ms) | P99(ms) | msg/s | Hata% | RAM(MB) |
|---|---|---|---|---|---|---|
| REST | 132,03 | 250,26 | 327,54 | 208,3 | 0,00 | 74,20 |
| SSE | 87,81 | 138,58 | 164,60 | 250,0 | 0,00 | 59,16 |
| **WebSocket** | **0,50** | **0,99** | **1,73** | **454,5** | **0,00** | 139,12 |
| MQTT | 242,02 | 387,33 | 481,77 | 135,1 | 0,00 | 47,36 |

---

### S9 — Large Payload (50 İstemci, 100 KB, 3 dk)

| Protokol | Ort(ms) | P95(ms) | P99(ms) | msg/s | Hata% | RAM(MB) |
|---|---|---|---|---|---|---|
| REST | 49,82 | 85,68 | 161,72 | 208,3 | 0,00 | 38,16 |
| SSE | 21,33 | 45,07 | 99,08 | 227,3 | 0,00 | 45,81 |
| **WebSocket** | **2,78** | **5,43** | **10,49** | **250,0** | **0,00** | 81,07 |
| MQTT | 182,03 | 266,06 | 372,00 | 131,6 | 0,00 | 36,31 |

> **H6 Notu:** REST S8→S9 geçişinde -%62, SSE -%76 gecikme iyileşmesi gösterdi (HTTP batching etkisi). WebSocket +456% artış gösterse de 2,78ms ile mutlak en düşük değeri korudu.

---

## 🔬 Apache JMeter Çapraz Doğrulama

S1 ve S3 senaryolarında Apache JMeter 5.6.3 ile bağımsız doğrulama yapılmıştır. PowerShell betikleriyle 8 CSV'den ortalama, P95 ve hata metrikleri hesaplanmıştır.

### Karşılaştırmalı Sonuç Tablosu

| Protokol | Sn | PB Ort(ms) | JM Ort(ms) | PB P95(ms) | JM P95(ms) | JM Hata | Yorum |
|---|---|---|---|---|---|---|---|
| REST | S1 | 32,48 | 5,74 | 248,90 | 8 | 0 | Kısmi uyum |
| REST | S3 | 132,52 | 277,68 | 246,60 | 406 | 0 | Kısmi uyum |
| SSE | S1 | 5,02 | 3,83 | 16,55 | 6 | 0 | Uyumlu ✓ |
| SSE | S3 | 92,10 | 221,92 | 141,42 | 301 | 0 | Kısmi uyum |
| WebSocket | S1 | 1,68 | 9,23 | 7,64 | 12 | 0 | ⚠️ Metodoloji farkı |
| WebSocket | S3 | 1,37 | 488,71 | 3,57 | 623 | 0 | ⚠️ Metodoloji farkı |
| MQTT | S1 | 13,01 | 1,28 | 27,99 | 3 | 11617 | ⚠️ Farklı ölçüm modeli |
| MQTT | S3 | 246,22 | 54,86 | 413,20 | 115 | 136441 | ⚠️ Farklı ölçüm modeli |

### JMeter Metodoloji Farkları

**WebSocket:** JMeter her ölçümde yeni TCP+WebSocket bağlantısı kurmaktadır. ProtocolBenchmark kalıcı bağlantı kullanmaktadır. S3'te 1,37ms vs 488ms farkı bu nedenle oluşmuştur.

**MQTT:** JMeter yalnızca PUBLISH işlemini ölçmektedir. ProtocolBenchmark 4 hop tam döngüyü (client→broker→MqttResponder→broker→client) ölçmektedir. JMeter'ın 1,28ms değeri H7 hipotezini desteklemektedir: MQTT saf PUBLISH modunda çok düşük gecikme sağlamaktadır.

**MQTT Hata Sayısı:** JMeter'ın raporladığı hatalar gerçek iletişim hataları değil, subscribe mekanizmasının JMeter tarafından tam desteklenmemesinden kaynaklanan metodoloji uyumsuzluğudur.

---

## 🧪 Hipotez Değerlendirme Özeti

| H# | Hipotez | Sonuç | Bulgu |
|---|---|---|---|
| H1 | WS < SSE < REST < MQTT gecikme sıralaması | ✅ DESTEKLENDI | WS(7,72ms) < SSE(725ms) < REST(857ms) < MQTT(1073ms) |
| H2 | Sürekli bağlantılı protokoller REST'ten daha düşük gecikme | ✅ DESTEKLENDI | WS ve SSE her yük seviyesinde REST'ten iyi |
| H3 | WS en yüksek throughput; MQTT en düşük | ✅ DESTEKLENDI | WS S5'te 12.893 msg/s; MQTT 103-149 msg/s |
| H4 | Stres testinde MQTT en yüksek hata oranı | ✅ DESTEKLENDI | MQTT %0,24; REST %0,08; SSE %0,14; WS %0,00 |
| H5 | WS yüksek eşzamanlı bağlantıda en fazla bellek | ✅ DESTEKLENDI | WS stres/spike'ta 443-764 MB; dayanıklılıkta stabil |
| H6 | Büyük payload'da REST/SSE iyileşir; WS artar; WS mutlak en düşük | ✅ DESTEKLENDI | REST -%62, SSE -%76; WS +456% ama 2,78ms ile en hızlı |
| H7 | MQTT pub/sub'da üstün (teorik) | ✅ TEORİK DESTEKLENDİ | Literatür + JMeter MQTT PUBLISH: 1,28ms |

---

## 🛠️ Teknoloji Stack

```
Sunucu:    ASP.NET Core (.NET 8) + Kestrel
Broker:    Eclipse Mosquitto 2.x
MQTT Lib:  MQTTnet 4.x
Analiz:    .NET Analysis + Python (pandas, matplotlib)
İstatistik: Welch t-testi + Cohen's d + IQR (MathNet.Numerics)
Çapraz:    Apache JMeter 5.6.3
CSV:       CsvHelper
Log:       Serilog
```

---

## 🚀 Kurulum ve Çalıştırma

### Gereksinimler

```
.NET 8 SDK
Eclipse Mosquitto 2.x
Python 3.x (analiz için)
```

### Sunucuyu Başlat

```bash
cd ProtocolBenchmark.Server
dotnet run
```

Sunucu şu portları dinler:
- `http://localhost:5000/message` — REST
- `http://localhost:5001/stream` — SSE  
- `ws://localhost:5002/ws` — WebSocket
- `localhost:1883` — MQTT (Mosquitto)

### Mosquitto'yu Başlat

```bash
mosquitto -c mosquitto.conf
```

### Testleri Çalıştır

```bash
cd ProtocolBenchmark.Client
dotnet run
```

İnteraktif menüden protokol ve senaryo seçilir. Sonuçlar `results/` klasörüne CSV olarak kaydedilir.

### Analiz Üret

```bash
cd ProtocolBenchmark.Analysis
dotnet run -- ../ProtocolBenchmark.Client/results
```

`output/` klasöründe üretilir:
- `summary.csv` — Senaryo özetleri
- `hypotheses.csv` — Hipotez sonuçları
- `report.txt` — Tam metin raporu

### Python Grafikleri

```bash
python analyze.py --dir path/to/results
```

---

## 📁 Veri Seti

```
36 CSV  → ProtocolBenchmark ana testler (4 protokol × 9 senaryo)
 8 CSV  → Apache JMeter çapraz doğrulama (4 protokol × 2 senaryo)
─────────────────────────────────────────
44 CSV  → Toplam (~658 MB, ~3,8 milyon ölçüm kaydı)
```

> CSV dosyaları zip olarak projenin Data folder ında dır

---

## 📌 Kullanım Senaryosuna Göre Protokol Seçim Rehberi

| Kullanım Senaryosu | Önerilen Protokol | Gerekçe |
|---|---|---|
| Düşük gecikme gerektiren real-time | WebSocket | Kalıcı bağlantı, minimal overhead |
| Yüksek eşzamanlı istemci | WebSocket | S5'te 12.893 msg/s, sıfır hata |
| Basit API / CRUD | REST | Kolay entegrasyon, geniş ekosistem |
| Sunucudan canlı bildirim | SSE | HTTP tabanlı, güvenlik duvarı uyumlu |
| IoT pub/sub mesajlaşma | MQTT | Doğal pub/sub modeli için üstün |
| Mikroservis req/response | WebSocket veya REST | MQTT bu modelde 4 hop maliyeti taşır |

---

## ⚠️ Sınırlılıklar

- Testler localhost üzerinde gerçekleştirilmiştir; gerçek ağ gecikmesi simüle edilmemiştir
- SSE ve MQTT doğal modellerinin dışında request/response modeline adapte edilmiştir; saf pub/sub ve streaming kullanımı kapsam dışıdır
- Eclipse Mosquitto tek örnekli çalıştırılmıştır; cluster performansı kapsam dışıdır
- TLS/SSL güvenlik karşılaştırması yapılmamıştır
- JMeter çapraz doğrulaması S1 ve S3 senaryolarıyla sınırlıdır

---

## 📚 Kaynakça

1. Fielding, R. T. (2000). *Architectural Styles and the Design of Network-based Software Architectures*. UC Irvine.
2. Fette, I., & Melnikov, A. (2011). *The WebSocket Protocol*. RFC 6455.
3. Welch, B. L. (1947). The generalization of Student's problem. *Biometrika*, 34(1-2), 28-35.
4. Cohen, J. (1988). *Statistical Power Analysis for the Behavioral Sciences* (2nd ed.).
5. Atmoko, R. A., et al. (2019). IoT real time data acquisition using MQTT protocol. *Journal of Physics*.
6. Banks, A., et al. (2019). *MQTT Version 5.0*. OASIS Standard.
7. Mishra, B., & Kertesz, A. (2020). The use of MQTT in M2M and IoT systems. *IEEE Access*.
8. Naik, N. (2017). Choice of effective messaging protocols for IoT systems. *IEEE SYSMICS*.
9. Jain, R. (1991). *The Art of Computer Systems Performance Analysis*. John Wiley and Sons.
10. W3C. (2015). *Server-Sent Events*. W3C Recommendation.
11. Apache Software Foundation. (2024). *Apache JMeter User Manual* (Version 5.6).

---


