# Telefonda deneme (Android APK)

## 1. Sunucuyu bilgisayarda çalıştır

Docker Desktop kurulu olmalı. Depo klasöründe:

```bash
docker compose --profile api up -d --build
```

- API `http://<bilgisayar-IP>:5080` adresinde açılır. Açıldığını kontrol etmek için tarayıcıda `http://localhost:5080/health/live` adresini aç.
- Bilgisayarın yerel IP'sini öğren: Windows'ta `ipconfig`, macOS/Linux'ta `ip a` veya `ifconfig` (ör. `192.168.1.20`).
- Telefon ve bilgisayar **aynı Wi-Fi ağında** olmalı. Windows Güvenlik Duvarı sorarsa 5080 portuna izin ver.
- Yönetici paneli: `http://localhost:5080/admin`.

## 2. APK'yı indir ve kur

1. GitHub → **Actions** → `claude/sahanet-platform-architecture-mkps4g` dalının son yeşil **CI** çalışması.
2. Sayfanın altındaki **Artifacts** bölümünden `android-apk` dosyasını indir ve zip'i aç.
   - `fishing/…-Signed.apk` → BalıkçıNet
   - `scrap/…-Signed.apk` → HurdaNet
3. APK'yı telefona gönder (USB, Drive, WhatsApp'ta kendine) ve aç. "Bilinmeyen kaynaklardan yükleme" izni istenirse ver.

## 3. Uygulamada

1. Açılış ekranının en üstündeki **🌐 Sunucu** alanına `192.168.1.20:5080` yaz (kendi IP'n).
2. Telefon numaranı gir → **Kod gönder**.
3. Geliştirme ortamında SMS gönderilmez; kod her zaman **123456**'dır (`Development:FixedOtpCode`).
4. Rızaları işaretle, bölge seç → **Başla**.

Sunucu adresini sonradan **⚙️ Ayarlar** ekranından değiştirebilirsin.

## Ekranlar

| BalıkçıNet | HurdaNet |
|---|---|
| Deniz durumu, Av günlüğü (çevrimdışı), Uyarılar, Noktalarım (konumdan ekle), Pazar (ilan ver / ilan sahibine yaz), Mesajlar, 🆘 Acil durum (158 / konum SMS), Ayarlar (verilerimi indir, hesabımı sil) | Toplama talepleri (talep oluştur / hurdacı olarak üstlen), Hurda fiyatları, Pazar, Mesajlar, Duyurular, Ayarlar |

## Bilinen sınırlamalar (test sürümü)

- Push bildirimleri telefona gelmez: Firebase projesi ve `google-services.json` gerekiyor. Mesajlar ekranı açıkken 5 saniyede bir yenilenir.
- Deniz durumu Open-Meteo'dan gelir; sunucunun internete çıkabilmesi gerekir.
- Ödeme yalnızca sandbox'tır.
- Bu APK Debug imzalıdır; mağazaya yüklenemez.
