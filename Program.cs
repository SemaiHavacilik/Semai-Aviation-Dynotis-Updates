using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Linq;

class Program
{
    // GitHub Raw Linkiniz
    const string XmlUrl = "https://raw.githubusercontent.com/SemaiHavacilik/Semai-Aviation-Dynotis-Updates/master/version.xml";

    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Dynotis Güncelleme Test Prosesi ===");
        Console.WriteLine($"XML Kontrol Ediliyor: {XmlUrl}");

        try
        {
            using (HttpClient client = new HttpClient())
            {
                // User-Agent ekleyelim (Bazen GitHub tarayıcı gibi davranmayan istekleri reddedebilir)
                client.DefaultRequestHeaders.Add("User-Agent", "DynotisUpdater");

                // 1. XML Dosyasını Çek
                string xmlContent = await client.GetStringAsync(XmlUrl);

                // --- HATA AYIKLAMA: İndirilen veriyi görelim ---
                Console.WriteLine("--- İndirilen İçerik Başlangıcı ---");
                Console.WriteLine(xmlContent);
                Console.WriteLine("--- İndirilen İçerik Sonu ---");

                // 2. BOM (Byte Order Mark) Temizliği [KRİTİK NOKTA BURASI]
                // Dosya başındaki görünmez karakteri ve boşlukları siler.
                xmlContent = xmlContent.Trim().Replace("\uFEFF", "");

                // Eğer içerik "404: Not Found" ise hata fırlat
                if (xmlContent.Contains("404: Not Found"))
                {
                    throw new Exception("GitHub dosyayı bulamadı (404). Linki veya Repo gizliliğini kontrol edin.");
                }

                // 3. XML'i Parse Et
                XDocument doc = XDocument.Parse(xmlContent);
                Console.WriteLine("XML Başarıyla Parse Edildi.");

                var latestUpdate = doc.Descendants("item")
                    .Select(x => new
                    {
                        Version = new Version(x.Element("version")?.Value ?? "0.0.0.0"),
                        Url = x.Element("url")?.Value,
                        Mandatory = x.Element("mandatory")?.Value
                    })
                    .OrderByDescending(x => x.Version)
                    .FirstOrDefault();

                if (latestUpdate != null)
                {
                    Console.WriteLine($"\nEn Güncel Sürüm: {latestUpdate.Version}");
                    Console.WriteLine($"İndirme Linki: {latestUpdate.Url}");

                    // 4. İndirme Testi
                    Console.WriteLine("\nİndirme testi başlatılıyor (Github)...");

                    // Github bazen yönlendirme yapar, HttpClient bunu otomatik takip eder ama kontrol edelim.
                    var response = await client.GetAsync(latestUpdate.Url, HttpCompletionOption.ResponseHeadersRead);

                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"[BAŞARILI] Sunucu yanıt verdi: {response.StatusCode}");
                        Console.WriteLine($"Dosya Tipi: {response.Content.Headers.ContentType}");
                        long? size = response.Content.Headers.ContentLength;

                        // Github bazen boyutu header'da vermez, sorun değil.
                        Console.WriteLine($"Dosya Boyutu: {(size.HasValue ? (size / 1024 / 1024) + " MB" : "Bilinmiyor (Chunked Transfer)")}");
                        Console.WriteLine("Test tamamlandı.");
                    }
                    else
                    {
                        Console.WriteLine($"[HATA] Dosya indirilemedi. Hata Kodu: {response.StatusCode}");
                    }
                }
                else
                {
                    Console.WriteLine("XML içinde geçerli bir <item> bulunamadı.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n[KRİTİK HATA]: {ex.Message}");
        }

        Console.WriteLine("\nÇıkmak için bir tuşa basın...");
        Console.ReadKey();
    }
}
