using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;

class Program
{
    // GitHub'daki XML dosyanızın adresi
    const string XmlUrl = "https://raw.githubusercontent.com/SemaiHavacilik/Semai-Aviation-Dynotis-Updates/master/version.xml";

    static async Task Main(string[] args)
    {
        // Konsol başlığı ve giriş mesajı
        Console.Title = "Dynotis Update Simulation v3.0";
        WriteColor("=== Dynotis Güncelleme Simülasyonu ===", ConsoleColor.Cyan);
        WriteColor($"Hedef XML: {XmlUrl}\n", ConsoleColor.Gray);

        using (HttpClient client = new HttpClient())
        {
            // GitHub istekleri için User-Agent ekliyoruz
            client.DefaultRequestHeaders.Add("User-Agent", "DynotisUpdateTester");

            try
            {
                // ---------------------------------------------------------
                // ADIM 1: XML İNDİRME VE PARSE ETME
                // ---------------------------------------------------------
                WriteColor("1. XML Dosyası İndiriliyor...", ConsoleColor.Yellow);

                // XML içeriğini çek
                string xmlContent = await client.GetStringAsync(XmlUrl);

                // BOM (Byte Order Mark) Temizliği - Görünmez karakterleri siler
                xmlContent = xmlContent.Trim().Replace("\uFEFF", "");

                // 404 Kontrolü
                if (xmlContent.Contains("404: Not Found"))
                {
                    WriteColor("[KRİTİK HATA] XML dosyası bulunamadı (404). Linki kontrol edin.", ConsoleColor.Red);
                    return;
                }

                // XML'i Parse et
                XDocument doc = XDocument.Parse(xmlContent);
                WriteColor("[BAŞARILI] XML Parse Edildi.", ConsoleColor.Green);

                // En güncel sürümü bul
                var latestUpdate = doc.Descendants("item")
                    .Select(x => new
                    {
                        Version = new Version(x.Element("version")?.Value ?? "0.0.0.0"),
                        Url = x.Element("url")?.Value,
                        Changelog = x.Element("changelog")?.Value,
                        Mandatory = x.Element("mandatory")?.Value
                    })
                    .OrderByDescending(x => x.Version)
                    .FirstOrDefault();

                if (latestUpdate == null)
                {
                    WriteColor("[HATA] XML içinde geçerli bir <item> bulunamadı.", ConsoleColor.Red);
                    return;
                }

                // Bilgileri Ekrana Yaz
                Console.WriteLine($"\n------------------------------------------------");
                Console.WriteLine($" Tespit Edilen Sürüm: {latestUpdate.Version}");
                Console.WriteLine($" MSI Linki: {latestUpdate.Url}");
                Console.WriteLine($" Changelog: {latestUpdate.Changelog}");
                Console.WriteLine($"------------------------------------------------\n");

                // ---------------------------------------------------------
                // ADIM 2: CHANGELOG KONTROLÜ
                // ---------------------------------------------------------
                WriteColor("2. Changelog Linki Kontrol Ediliyor...", ConsoleColor.Yellow);
                if (!string.IsNullOrEmpty(latestUpdate.Changelog))
                {
                    var logResponse = await client.GetAsync(latestUpdate.Changelog);
                    if (logResponse.IsSuccessStatusCode)
                        WriteColor("[BAŞARILI] Changelog erişilebilir.", ConsoleColor.Green);
                    else
                        WriteColor($"[UYARI] Changelog linki çalışmıyor! Kod: {logResponse.StatusCode}", ConsoleColor.Red);
                }

                // ---------------------------------------------------------
                // ADIM 3: İNDİRME KLASÖRÜ HAZIRLIĞI
                // ---------------------------------------------------------
                WriteColor("\n3. İndirme Klasörü Hazırlanıyor...", ConsoleColor.Yellow);

                // Gerçek projedeki gibi Temp klasörünü kullanıyoruz
                string downloadFolder = Path.Combine(Path.GetTempPath(), "DynotisUpdate");

                if (!Directory.Exists(downloadFolder))
                {
                    Directory.CreateDirectory(downloadFolder);
                    WriteColor($"[BİLGİ] Klasör oluşturuldu: {downloadFolder}", ConsoleColor.Gray);
                }
                else
                {
                    WriteColor($"[BİLGİ] Klasör zaten mevcut: {downloadFolder}", ConsoleColor.Gray);
                }

                // Dosya ismini URL'den al ve decode et (boşluklar için)
                string fileName = Path.GetFileName(latestUpdate.Url);
                fileName = Uri.UnescapeDataString(fileName);
                string localFilePath = Path.Combine(downloadFolder, fileName);

                // ---------------------------------------------------------
                // ADIM 4: İNDİRME TESTİ
                // ---------------------------------------------------------
                WriteColor($"\n4. MSI İndiriliyor ({fileName})...", ConsoleColor.Yellow);

                Stopwatch sw = Stopwatch.StartNew();
                // Sadece headerları değil, içeriği de okumaya başla
                var response = await client.GetAsync(latestUpdate.Url, HttpCompletionOption.ResponseHeadersRead);

                if (!response.IsSuccessStatusCode)
                {
                    WriteColor($"[HATA] Dosya indirilemedi. HTTP Kodu: {response.StatusCode}", ConsoleColor.Red);
                }
                else
                {
                    long? totalBytes = response.Content.Headers.ContentLength;

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var buffer = new byte[8192];
                        long totalRead = 0;
                        int bytesRead;

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            totalRead += bytesRead;

                            if (totalBytes.HasValue)
                            {
                                double progress = (double)totalRead / totalBytes.Value * 100;
                                Console.Write($"\rİlerleme: %{progress:F1}  ({totalRead / 1024 / 1024} MB)");
                            }
                        }
                    }

                    sw.Stop();
                    Console.WriteLine(); // Satır başı
                    WriteColor($"[BAŞARILI] İndirme Tamamlandı.", ConsoleColor.Green);
                    WriteColor($"Kaydedilen Yer: {localFilePath}", ConsoleColor.Gray);

                    // ---------------------------------------------------------
                    // ADIM 5: KOMUT SİMÜLASYONU
                    // ---------------------------------------------------------
                    WriteColor("\n5. Kurulum Komutu Simülasyonu", ConsoleColor.Yellow);

                    // Projenizdeki kodun aynısı: /i "%1" /passive /norestart
                    string productionArgsFormat = "/i \"{0}\" /passive /norestart";
                    string finalCommandArgs = string.Format(productionArgsFormat, localFilePath);

                    Console.WriteLine("AutoUpdater.NET şu komutu çalıştıracak:");
                    Console.WriteLine("--------------------------------------------------");
                    WriteColor($"msiexec {finalCommandArgs}", ConsoleColor.Cyan);
                    Console.WriteLine("--------------------------------------------------");

                    WriteColor("\n[ANALİZ]:", ConsoleColor.White);
                    if (localFilePath.Contains(" "))
                    {
                        WriteColor(" -> Dosya yolunda boşluk var.", ConsoleColor.Magenta);
                        WriteColor(" -> Ancak komutta dosya yolu tırnak (\") içinde olduğu için SORUNSUZ çalışacaktır.", ConsoleColor.Green);
                    }
                    else
                    {
                        WriteColor(" -> Dosya yolunda boşluk yok, tırnaklar yine de güvenlik sağlar.", ConsoleColor.Green);
                    }

                    // İsteğe bağlı: İndirilen test dosyasını silmek isterseniz yorumu kaldırın
                    // try { File.Delete(localFilePath); } catch { }
                }
            }
            catch (Exception ex)
            {
                WriteColor($"\n[BEKLENMEYEN HATA]: {ex.Message}", ConsoleColor.Red);
            }
        }

        WriteColor("\nSimülasyon tamamlandı. Çıkmak için bir tuşa basın...", ConsoleColor.White);
        Console.ReadKey();
    }

    // Renkli yazı yazmak için yardımcı metot
    static void WriteColor(string text, ConsoleColor color)
    {
        var oldColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ForegroundColor = oldColor;
    }
}
