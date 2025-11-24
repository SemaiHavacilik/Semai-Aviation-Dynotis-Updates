using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Linq;

class Program
{
    // GitHub'daki Raw XML linkiniz
    const string XmlUrl = "https://raw.githubusercontent.com/SemaiHavacilik/Semai-Aviation/master/Dynotis-Updates/version.xml";

    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Dynotis Güncelleme Test Prosesi ===");
        Console.WriteLine($"XML Kontrol Ediliyor: {XmlUrl}");

        try
        {
            using (HttpClient client = new HttpClient())
            {
                // 1. XML Dosyasını Çek
                string xmlContent = await client.GetStringAsync(XmlUrl);
                Console.WriteLine("XML Başarıyla Okundu.");

                // 2. XML'i Parse Et (En yüksek versiyonu bul)
                XDocument doc = XDocument.Parse(xmlContent);

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
                    Console.WriteLine($"Zorunlu Güncelleme: {latestUpdate.Mandatory}");

                    // 3. İndirme Testi (Dosyayı gerçekten indirebiliyor muyuz?)
                    Console.WriteLine("\nİndirme testi başlatılıyor (SharePoint bağlantısı)...");

                    var response = await client.GetAsync(latestUpdate.Url);

                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"[BAŞARILI] Sunucu yanıt verdi: {response.StatusCode}");
                        Console.WriteLine($"Dosya Tipi: {response.Content.Headers.ContentType}");
                        long? size = response.Content.Headers.ContentLength;
                        Console.WriteLine($"Dosya Boyutu: {(size.HasValue ? (size / 1024 / 1024) + " MB" : "Bilinmiyor")}");

                        Console.WriteLine("Test tamamlandı. Link AutoUpdater.NET ile çalışmaya uygun.");
                    }
                    else
                    {
                        Console.WriteLine($"[HATA] Dosya indirilemedi. Hata Kodu: {response.StatusCode}");
                        Console.WriteLine("Not: SharePoint linkinin 'Anyone' (Herkes) erişimine açık olduğundan ve '?download=1' içerdiğinden emin olun.");
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
