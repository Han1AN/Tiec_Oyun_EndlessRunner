# Grove Run — bisiklet challenge

Oyun sahnesi: `Assets/Scenes/GroveRunner_Game.unity`.

Unity'de bu sahneyi açıp Play'e basın. Herhangi bir tuş, fare tıklaması veya ekran dokunuşu koşuyu başlatır. Bilgisayarda **A / D** ya da **sol / sağ ok**; tablette ekrandaki **SOL / SAĞ** alanlarını basılı tutarak yön verin. Bisiklet kendiliğinden ilerler, rampalardan kendiliğinden sıçrar ve yolun dışına çıkamaz.

Tek çarpışma koşuyu bitirir. Sonuç ekranında geçen saniye, tamamlanan parkur yüzdesi ve en iyi beş kayıt gösterilir. İsminizi yazıp KAYDET'e basın. START GAME, klavyede bir tuş veya sonuç ekranının boş alanına dokunmak yeni koşuyu başlatır. İsim alanını düzenlerken tuşlar oyunu başlatmaz. Kayıtlar cihazda saklanır.

## Hız ve zorluk ayarı

Project penceresindeki `Assets/TIEC_Runner/RunnerSettings.asset` dosyasını seçin.

- **Target Seconds:** varsayılan 30 saniye.
- **Speed Multiplier:** güncel varsayılan 1,2; parkur 25 saniyede tamamlanır. 1 = 30 saniye; 0,75 = 40 saniye. Artırmak hem hızı hem zorluğu artırır.
- **Start Weight / End Weight:** başlangıç ve son hızın birbirine oranı.
- **Acceleration Power:** hızlanmanın parkurun hangi bölümünde yoğunlaştığı.
- **Lateral Speed:** direksiyonun sağa ve sola hareket hızı.
- **Road Half Width:** bisikletin yarı genişliği hesaba katılarak uygulanan yol sınırı.

İleri hızın integrali parkur mesafesine göre normalize edilir. Bu nedenle hızlanma eğrisi değiştiğinde hedef süre korunur; Speed Multiplier değiştiğinde süre onunla orantılı değişir.

Rampalardan sonraki uçuş mesafesi ve yayı kısaltıldı. Güncel hızda havada kalma süresi yaklaşık 0,48–1,55 saniyedir. İniş mesafesi ve yay yüksekliği, BicyclePlayer üzerindeki Bicycle Motor bileşeninin Jumps listesinden değiştirilebilir.

## Dosyaların sorumlulukları

| Dosya | Sorumluluk |
|---|---|
| RunnerConfig | Süre, hız profili, yol genişliği ve fizik katmanları |
| RunnerSession | Hazır → Oynanıyor → Sonuç akışı, süre, bitiş ve yeniden başlatma |
| RunnerInput | Klavye, fare ve dokunmatik girdiyi birleştirme |
| BicycleMotor | İleri hareket, direksiyon, yol sınırı, rampalar ve tüm hareket adımını tarayan çarpışma kontrolü |
| RunnerCamera | Bisiklet takibi ve hıza bağlı görüş açısı |
| RunnerHud | Menü, oyun göstergeleri, sonuç ve isim kaydı |
| RunnerTouchSteer | Birden fazla parmağın ayrı takibi |
| RunnerSafeArea | Ekran boyutu ve tablet güvenli alanına uyum |
| ScoreRepository | Cihazdaki kayıtları saklama, aynı koşunun ismini güncelleme, sıralama |
| RunnerHazard | Engel nesnesi işaretleyicisi |
| Editor/RunnerSceneSetup | Unity API'leriyle sahne kurma ve referansları atama |
| Editor/RunnerUiFactory | Canvas ve UI bileşenlerini oluşturup bağlama |
| Editor/RunnerValidation | Referans, fizik, hız profili ve kayıt kontrolleri |

Sahne bileşenlerinin bağlantıları Inspector'da açıkça atanmıştır. Oyun sırasında nesne adlarıyla sahne araması veya global singleton kullanılmaz. Karakterin animasyon hiyerarşisi korunur; direksiyonun görsel yatışı ayrı bir üst nesnede uygulanır.

## Harita ve font

Hazır haritanın bir kopyası oyun sahnesine dönüştürülür. Kaynak araçlarla uyuşmayan çarpışma alanları, görünür araçların boyutlarıyla düzeltilir. İçe aktarımda ters kalan harita işaretleri dünya yönüne çevrilir. İlk çukuru geçmek için ekrandaki soldaki rampayı kullanın; sonraki bölümlerde hız arttıkça daha erken yön değiştirmeniz gerekir.

Başlıklarda San Andreas tarzına benzer gotik **UnifrakturCook**, okunaklı metinlerde **Roboto Bold** kullanılır. [UnifrakturCook kaynağı](https://fonts.google.com/specimen/UnifrakturCook). Font lisansları `Assets/TIEC_Runner/Fonts` klasöründedir.

Tablet için yatay ekran ve ölçeklenen Canvas hazırlanmıştır. Android/iPadOS üzerinde fiziksel cihaz testi ve mağaza paketleri ayrıca yapılmalıdır.

## Doğrulama

İlk 30 saniyelik ayarda Unity Play Mode içinde 13 oynanış kontrolü geçti: çarpışma, aynı kaydın isim değişikliği, yeniden başlatma, iki yol sınırı, yüksek hızda taramalı çarpışma, dokunmatik yön girdisi, iki parmağın birleşimi ve bırakılması, geçerli rotanın bitişi, tam 30 saniye, sonuçta zamanın durması ve art arda 10 yeniden başlatma.

Güncel 1,2 hız çarpanı ve kısa uçuşlarla Play Mode içindeki motor, sabit zaman adımlarıyla tekrar çalıştırıldı. Sağ yönün kamera yönüyle eşleşmesi, iki yol sınırı, yedi rampanın çarpışmadan inişi, tüm rotanın geçilmesi, 25 saniyelik bitiş ve referans/hız profili kontrolü olmak üzere 13 kontrol geçti; hata sayısı 0.

Referans, hız profili ve skor kalıcılığı kontrollerinde hata sayısı **0**. 1280×800 bilgisayar ve 1024×768 tablet oranındaki UI görüntüleri incelendi. Dokunmatik testler Unity'de simüle edildi; fiziksel tablette henüz test yapılmadı.
