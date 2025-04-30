using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Remoting.Messaging;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;
using System.Xml.Linq;
using GameTranslator;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GameTranslator
{
    public partial class MainWindow : Window
    {
        private string selectedFilePath;
        private string detectedFormat;
        private List<TranslationItem> translationItems = new List<TranslationItem>();
        private Dictionary<string, string> languageCodes = new Dictionary<string, string>
        {
            { "Otomatik Algıla", "auto" },
            { "Türkçe", "tr" },
            { "İngilizce", "en" },
            { "Almanca", "de" },
            { "Fransızca", "fr" },
            { "İspanyolca", "es" },
            { "İtalyanca", "it" },
            { "Rusça", "ru" },
            { "Japonca", "ja" },
            { "Çince", "zh" },
            { "Korece", "ko" }
        };

        // API Anahtarınız
        private string apiKey = "YOUR-OPENAI-API";

        public MainWindow()
        {
            InitializeComponent();
            InitializeUI();
        }
        public int ProgressValue
        {
            get { return (int)progressBar.Value; }
            set { progressBar.Value = value; }  // ProgressBar'ı güncellemek için
        }

        private void InitializeUI()
        {
            // Dil seçeneklerini doldur
            cmbSourceLang.ItemsSource = languageCodes.Keys;
            cmbSourceLang.SelectedItem = "Otomatik Algıla";

            cmbTargetLang.ItemsSource = languageCodes.Keys.Skip(1); // "Otomatik Algıla" dışındaki diller
            cmbTargetLang.SelectedItem = "Türkçe";

            // Format seçeneklerini doldur
            List<string> formats = new List<string> { "Otomatik Algıla", "XML", "JSON", "CSV", "Text" };
            cmbFormat.ItemsSource = formats;
            cmbFormat.SelectedItem = "Otomatik Algıla";
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "Çevrilecek Oyun Dosyasını Seçin",
                Filter = "Tüm Dosyalar|*.*|XML Dosyaları|*.xml|JSON Dosyaları|*.json|CSV Dosyaları|*.csv|Metin Dosyaları|*.txt"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                selectedFilePath = openFileDialog.FileName;
                txtFilePath.Text = selectedFilePath;
                LogMessage($"Dosya seçildi: {selectedFilePath}");
                DetectFormat(selectedFilePath);
            }
        }

        private void DetectFormat(string filePath)
        {
            try
            {
                string extension = System.IO.Path.GetExtension(filePath).ToLower();
                string content = File.ReadAllText(filePath);

                // 7.2 için if-else kullanarak format algılamayı yapıyoruz
                if (extension == ".xml")
                {
                    detectedFormat = "XML";
                }
                else if (extension == ".json")
                {
                    detectedFormat = "JSON";
                }
                else if (extension == ".csv")
                {
                    detectedFormat = "CSV";
                }
                else if (extension == ".txt")
                {
                    detectedFormat = "Text";
                }
                else
                {
                    detectedFormat = "Unknown";
                }

                // İçeriğe göre format algılama
                if (detectedFormat == "Unknown")
                {
                    if (content.TrimStart().StartsWith("<") && content.TrimEnd().EndsWith(">"))
                    {
                        try
                        {
                            XDocument.Parse(content);
                            detectedFormat = "XML";
                        }
                        catch { }
                    }
                    else if (content.TrimStart().StartsWith("{") && content.TrimEnd().EndsWith("}"))
                    {
                        try
                        {
                            JObject.Parse(content);
                            detectedFormat = "JSON";
                        }
                        catch { }
                    }
                    else if (content.Contains(",") && content.Split('\n').Any(line => line.Count(c => c == ',') > 0))
                    {
                        detectedFormat = "CSV";
                    }
                    else
                    {
                        detectedFormat = "Text";
                    }
                }

                cmbFormat.SelectedItem = detectedFormat;
                LogMessage($"Format algılandı: {detectedFormat}");
            }
            catch (Exception ex)
            {
                LogMessage($"Format algılama hatası: {ex.Message}");
            }
        }

        private void BtnAnalyze_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedFilePath) || !File.Exists(selectedFilePath))
            {
                MessageBox.Show("Lütfen geçerli bir dosya seçin!", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            AnalyzeFile();
        }

        private void AnalyzeFile()
        {
            LogMessage($"Dosya analiz ediliyor: {selectedFilePath}");
            statusBar.Content = "Analiz ediliyor...";
            translationItems.Clear();

            try
            {
                string content = File.ReadAllText(selectedFilePath);
                string format = cmbFormat.SelectedItem.ToString();

                if (format == "Otomatik Algıla")
                {
                    DetectFormat(selectedFilePath);
                    format = detectedFormat;
                }

                switch (format)
                {
                    case "XML":
                        AnalyzeXml(content);
                        break;
                    case "JSON":
                        AnalyzeJson(content);
                        break;
                    case "CSV":
                        AnalyzeCsv(content);
                        break;
                    case "Text":
                        AnalyzeText(content);
                        break;
                    default:
                        LogMessage("Desteklenmeyen dosya formatı.");
                        break;
                }

                // Önizleme için veri grid'ini doldur
                dataGridPreview.ItemsSource = null; // Resetle
                dataGridPreview.ItemsSource = translationItems;

                statusBar.Content = "Analiz tamamlandı";
                LogMessage($"Toplam {translationItems.Count} çevrilebilir metin bulundu.");
            }
            catch (Exception ex)
            {
                LogMessage($"Analiz hatası: {ex.Message}");
                statusBar.Content = "Hata oluştu";
            }
        }

        private void AnalyzeXml(string content)
        {
            try
            {
                XDocument doc = XDocument.Parse(content);

                // XML elementlerini dolaş ve metin içerenleri bul
                ParseXmlElement(doc.Root, "");

                LogMessage($"XML analizi: {translationItems.Count} metin elemanı bulundu");
            }
            catch (Exception ex)
            {
                LogMessage($"XML analiz hatası: {ex.Message}");
            }
        }

        private void ParseXmlElement(XElement element, string path)
        {
            string currentPath = string.IsNullOrEmpty(path) ? element.Name.LocalName : $"{path}/{element.Name.LocalName}";

            // Element içindeki metni kontrol et
            if (!string.IsNullOrWhiteSpace(element.Value) && !HasOnlyChildElements(element))
            {
                translationItems.Add(new TranslationItem
                {
                    Path = currentPath,
                    OriginalText = element.Value.Trim(),
                    TranslatedText = ""
                });
            }

            // Nitelikler (attributes) içindeki metinleri kontrol et
            foreach (var attribute in element.Attributes())
            {
                if (!string.IsNullOrWhiteSpace(attribute.Value))
                {
                    translationItems.Add(new TranslationItem
                    {
                        Path = $"{currentPath}/@{attribute.Name.LocalName}",
                        OriginalText = attribute.Value,
                        TranslatedText = ""
                    });
                }
            }

            // Alt elementleri dolaş
            foreach (var child in element.Elements())
            {
                ParseXmlElement(child, currentPath);
            }
        }

        private bool HasOnlyChildElements(XElement element)
        {
            return element.Elements().Any() && element.Nodes().All(n => n is XElement);
        }

        private void AnalyzeJson(string content)
        {
            try
            {
                JToken token = JToken.Parse(content);
                ParseJsonToken(token, "");

                LogMessage($"JSON analizi: {translationItems.Count} metin değeri bulundu");
            }
            catch (Exception ex)
            {
                LogMessage($"JSON analiz hatası: {ex.Message}");
            }
        }

        private void ParseJsonToken(JToken token, string path)
        {
            if (token is JValue value && value.Type == JTokenType.String)
            {
                string textValue = value.Value<string>();
                if (!string.IsNullOrWhiteSpace(textValue))
                {
                    translationItems.Add(new TranslationItem
                    {
                        Path = path,
                        OriginalText = textValue,
                        TranslatedText = ""
                    });
                }
            }
            else if (token is JObject obj)
            {
                foreach (var property in obj.Properties())
                {
                    string currentPath = string.IsNullOrEmpty(path) ? property.Name : $"{path}.{property.Name}";
                    ParseJsonToken(property.Value, currentPath);
                }
            }
            else if (token is JArray array)
            {
                for (int i = 0; i < array.Count; i++)
                {
                    string currentPath = $"{path}[{i}]";
                    ParseJsonToken(array[i], currentPath);
                }
            }
        }

        private void AnalyzeCsv(string content)
        {
            try
            {
                string[] lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                for (int i = 0; i < lines.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(lines[i]))
                    {
                        string[] fields = lines[i].Split(',');

                        for (int j = 0; j < fields.Length; j++)
                        {
                            if (!string.IsNullOrWhiteSpace(fields[j]))
                            {
                                translationItems.Add(new TranslationItem
                                {
                                    Path = $"Satır:{i + 1},Sütun:{j + 1}",
                                    OriginalText = fields[j].Trim(),
                                    TranslatedText = ""
                                });
                            }
                        }
                    }
                }

                LogMessage($"CSV analizi: {translationItems.Count} metin değeri bulundu");
            }
            catch (Exception ex)
            {
                LogMessage($"CSV analiz hatası: {ex.Message}");
            }
        }

        private void AnalyzeText(string content)
        {
            try
            {
                string[] lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                for (int i = 0; i < lines.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(lines[i]))
                    {
                        translationItems.Add(new TranslationItem
                        {
                            Path = $"Satır:{i + 1}",
                            OriginalText = lines[i].Trim(),
                            TranslatedText = ""
                        });
                    }
                }

                LogMessage($"Metin analizi: {translationItems.Count} satır bulundu");
            }
            catch (Exception ex)
            {
                LogMessage($"Metin analiz hatası: {ex.Message}");
            }
        }

        private async void BtnTranslate_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedFilePath) || !File.Exists(selectedFilePath))
            {
                MessageBox.Show("Lütfen geçerli bir dosya seçin!", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (translationItems.Count == 0)
            {
                AnalyzeFile();
            }

            if (translationItems.Count == 0)
            {
                MessageBox.Show("Çevrilecek metin bulunamadı!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string sourceLang = languageCodes[cmbSourceLang.SelectedItem.ToString()];
            string targetLang = languageCodes[cmbTargetLang.SelectedItem.ToString()];

            LogMessage($"Çeviri başlatılıyor: {sourceLang} -> {targetLang}");
            statusBar.Content = "Çeviri yapılıyor...";
            progressBar.Value = 0;
            progressBar.Maximum = translationItems.Count;

            try
            {
                // 1. Bütün metinleri birleştir
                string combinedText = string.Join(" ||| ", translationItems.Select(x => x.OriginalText));
                // 2. Token ve Maliyet Hesabı
                int inputTokens = EstimateTokenCount(combinedText);
                int outputTokens = (int)(inputTokens * 1.5); // Çıkış tahmini input * 1.5
                int totalTokens = inputTokens + outputTokens;

                // 3. Tahmini Ücret Hesabı
                // GPT-4o: input token 0.0005$, output token 0.0015$
                double estimatedCost = (inputTokens * 0.0005 + outputTokens * 0.0015) / 1000.0;

                // 4. Kullanıcıya gösterim
                string message = $"Tahmini Input Token: {inputTokens}\n" +
                                 $"Tahmini Output Token: {outputTokens}\n" +
                                 $"Toplam Token: {totalTokens}\n" +
                                 $"Tahmini Ücret: ${estimatedCost:F6}\n\n" +
                                 $"Devam etmek istiyor musunuz?";

                var result = MessageBox.Show(message, "Maliyet Bilgisi", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                {
                    LogMessage("Kullanıcı işlemi iptal etti.");
                    statusBar.Content = "İşlem iptal edildi.";
                    return; // Kullanıcı hayır dediyse API isteği yapılmayacak
                }


                // 2. Tek API çağrısı
                var translatedCombinedText = await TranslateTextAsync(combinedText, sourceLang, targetLang);

                // 3. Gelen cevabı parçalara böl
                var translatedParts = translatedCombinedText.Split(new[] { " ||| " }, StringSplitOptions.None);

                // 4. Her parçayı sırayla göster
                for (int i = 0; i < translationItems.Count && i < translatedParts.Length; i++)
                {
                    translationItems[i].TranslatedText = translatedParts[i].Trim();
                    ProgressValue = i + 1;

                    // EKRANA ANLIK OLARAK YANSIT
                    dataGridPreview.Items.Refresh();
                }

                // 5. Dosyayı kaydet
                SaveTranslatedFile();

                statusBar.Content = "Çeviri tamamlandı";
                MessageBox.Show("Çeviri tamamlandı ve dosya kaydedildi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogMessage($"Çeviri hatası: {ex.Message}");
                statusBar.Content = "Hata oluştu";
            }
        }
        public async Task TranslateAllAsync()
        {
            if (translationItems.Count == 0)
            {
                LogMessage("Çevrilecek metin bulunamadı!");
                return;
            }

            // Tüm metinleri birleştiriyoruz
            string combinedText = string.Join(" ||| ", translationItems.Select(x => x.OriginalText));

            try
            {
                LogMessage("Çeviri başlatıldı...");

                // Tek bir istekle tüm metinleri çeviriyoruz
                var translatedCombinedText = await TranslateTextAsync(combinedText, "auto", "tr");

                // Gelen çeviri yanıtını ayırıyoruz
                var translatedParts = translatedCombinedText.Split(new[] { " ||| " }, StringSplitOptions.None);

                // Her metni ilgili TranslationItem'a yerleştiriyoruz
                for (int i = 0; i < translationItems.Count && i < translatedParts.Length; i++)
                {
                    translationItems[i].TranslatedText = translatedParts[i].Trim();
                    ProgressValue = i + 1;
                }

                // Çevirilen dosyayı kaydediyoruz
                SaveTranslatedFile();

                LogMessage("Çeviri tamamlandı ve dosya kaydedildi.");
            }
            catch (Exception ex)
            {
                LogMessage($"Çeviri hatası: {ex.Message}");
            }
        }

        private async Task<string> TranslateTextAsync(string text, string sourceLang, string targetLang)
        {
            using (var client = new HttpClient())
            {
                try
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                    // Çeviri isteği
                    var prompt = $"Translate the following {(sourceLang == "auto" ? "" : $"{sourceLang} ")}text into {targetLang}:\n\n{text}";

                    var requestBody = new
                    {
                        model = "gpt-4o",
                        messages = new[] { new { role = "user", content = prompt } },
                        temperature = 0.2
                    };

                    var jsonContent = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

                    var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", jsonContent);

                    if ((int)response.StatusCode == 429)
                    {
                        LogMessage("🔴 429 - API limit aşılmış.");
                        return "API Limit Aşıldı";
                    }
                    else if (response.IsSuccessStatusCode)
                    {
                        var responseBody = await response.Content.ReadAsStringAsync();
                        var parsed = JObject.Parse(responseBody);
                        return parsed["choices"]?[0]?["message"]?["content"]?.ToString().Trim() ?? text;
                    }
                    else
                    {
                        LogMessage($"🟠 OpenAI API hatası: {response.StatusCode}");
                        return text;
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"🚫 İstisna oluştu: {ex.Message}");
                    return text;
                }
            }
        }

        private void SaveTranslatedFile()
        {
            try
            {
                string format = detectedFormat;

                string originalContent = File.ReadAllText(selectedFilePath);
                string newContent = "";

                if (format == "XML")
                {
                    newContent = TranslateXmlContent(originalContent);  // Çevirilmiş XML oluştur
                }
                else if (format == "JSON")
                {
                    newContent = TranslateJsonContent(originalContent);  // Çevirilmiş JSON oluştur
                }
                else if (format == "CSV")
                {
                    newContent = TranslateCsvContent(originalContent);   // Çevirilmiş CSV oluştur
                }
                else if (format == "Text")
                {
                    newContent = TranslateTextContent(originalContent);  // Çevirilmiş Text oluştur
                }
                else
                {
                    LogMessage("Desteklenmeyen dosya formatı, düz metin olarak kaydediliyor.");
                    newContent = string.Join("\n", translationItems.Select(item => item.TranslatedText));
                }

                string outputPath = GetOutputPath(selectedFilePath);
                File.WriteAllText(outputPath, newContent, Encoding.UTF8);

                LogMessage($"✅ Çeviri tamamlandı ve kaydedildi: {outputPath}");
            }
            catch (Exception ex)
            {
                LogMessage($"🚫 Dosya kaydetme hatası: {ex.Message}");
            }
        }


        private XElement FindXmlElement(XElement root, string path)
        {
            if (root == null || string.IsNullOrEmpty(path))
                return null;

            var parts = path.Split('/');

            XElement current = root;
            int startIndex = (current.Name.LocalName == parts[0]) ? 1 : 0; // Eğer kök zaten ilk parça ise atla

            for (int i = startIndex; i < parts.Length; i++)
            {
                current = current.Elements().FirstOrDefault(e => e.Name.LocalName == parts[i]);
                if (current == null)
                    return null; // O path bulunamazsa hemen çık
            }

            return current;
        }


        private string GetOutputPath(string inputPath)
        {
            string directory = System.IO.Path.GetDirectoryName(inputPath);
            string filename = System.IO.Path.GetFileNameWithoutExtension(inputPath);
            string extension = System.IO.Path.GetExtension(inputPath);

            return System.IO.Path.Combine(directory, filename + "_translated" + extension);
        }
        private string TranslateXmlContent(string originalContent)
        {
            XDocument doc = XDocument.Parse(originalContent);

            foreach (var item in translationItems)
            {
                if (string.IsNullOrWhiteSpace(item.TranslatedText))
                    continue;

                string path = item.Path;

                if (path.Contains("/@"))
                {
                    string[] parts = path.Split(new[] { "/@" }, StringSplitOptions.None);
                    string elementPath = parts[0];
                    string attributeName = parts[1];

                    var element = FindXmlElement(doc.Root, elementPath);
                    if (element != null && element.Attribute(attributeName) != null)
                    {
                        element.Attribute(attributeName).Value = item.TranslatedText;
                    }
                }
                else
                {
                    var element = FindXmlElement(doc.Root, path);
                    if (element != null)
                    {
                        element.Value = item.TranslatedText;
                    }
                }
            }

            return doc.ToString();
        }

        private string TranslateJsonContent(string originalContent)
        {
            JToken token = JToken.Parse(originalContent);

            foreach (var item in translationItems)
            {
                if (string.IsNullOrWhiteSpace(item.TranslatedText))
                    continue;

                UpdateJsonValue(token, item.Path, item.TranslatedText);
            }

            return token.ToString(Newtonsoft.Json.Formatting.Indented);
        }

        private string TranslateCsvContent(string originalContent)
        {
            string[] lines = originalContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            foreach (var item in translationItems)
            {
                if (string.IsNullOrWhiteSpace(item.TranslatedText))
                    continue;

                string path = item.Path;
                if (path.StartsWith("Satır:") && path.Contains(",Sütun:"))
                {
                    string[] parts = path.Split(',');
                    string rowPart = parts[0].Replace("Satır:", "");
                    string colPart = parts[1].Replace("Sütun:", "");

                    if (int.TryParse(rowPart, out int row) && int.TryParse(colPart, out int col))
                    {
                        row--; // 0-based index
                        col--;

                        if (row < lines.Length)
                        {
                            string[] fields = lines[row].Split(',');
                            if (col < fields.Length)
                            {
                                fields[col] = item.TranslatedText;
                                lines[row] = string.Join(",", fields);
                            }
                        }
                    }
                }
            }

            return string.Join(Environment.NewLine, lines);
        }

        private string TranslateTextContent(string originalContent)
        {
            string[] lines = originalContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            foreach (var item in translationItems)
            {
                if (string.IsNullOrWhiteSpace(item.TranslatedText))
                    continue;

                if (item.Path.StartsWith("Satır:"))
                {
                    string rowStr = item.Path.Replace("Satır:", "");
                    if (int.TryParse(rowStr, out int row))
                    {
                        row--; // 0-based
                        if (row < lines.Length)
                        {
                            lines[row] = item.TranslatedText;
                        }
                    }
                }
            }

            return string.Join(Environment.NewLine, lines);
        }

        private void UpdateJsonValue(JToken token, string path, string translatedText)
        {
            if (string.IsNullOrEmpty(path))
                return;

            if (path.Contains("."))
            {
                // Object property
                string propName = path.Substring(0, path.IndexOf('.'));
                string remainingPath = path.Substring(path.IndexOf('.') + 1);

                if (token is JObject obj && obj[propName] != null)
                {
                    UpdateJsonValue(obj[propName], remainingPath, translatedText);
                }
            }
            else if (path.Contains("[") && path.EndsWith("]"))
            {
                // Array index
                string arrayPath = path.Substring(0, path.IndexOf('['));
                string indexStr = path.Substring(path.IndexOf('[') + 1, path.Length - path.IndexOf('[') - 2);

                if (int.TryParse(indexStr, out int index) && token is JArray array && index < array.Count)
                {
                    if (string.IsNullOrEmpty(arrayPath))
                    {
                        if (array[index] is JValue)
                            array[index] = translatedText;
                    }
                    else
                    {
                        if (token is JObject obj && obj[arrayPath] is JArray targetArray && index < targetArray.Count)
                        {
                            UpdateJsonValue(targetArray[index], "", translatedText);
                        }
                    }
                }
            }
            else
            {
                // Simple property
                if (token is JObject obj && obj[path] != null && obj[path] is JValue)
                {
                    obj[path] = translatedText;
                }
            }
        }

        private int EstimateTokenCount(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            int tokenCount = 0;

            // Split by boşluklar ve noktalama işaretleri
            var words = text.Split(new char[] { ' ', '\n', '\r', '\t', '.', ',', '!', '?', ';', ':', '-', '(', ')', '[', ']', '{', '}', '"', '\'' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var word in words)
            {
                if (word.Length <= 4)
                    tokenCount += 1; // Kısa kelimeler genelde 1 token
                else
                    tokenCount += (int)Math.Ceiling(word.Length / 4.0); // Uzun kelimeleri 4 karaktere 1 token gibi düşün
            }

            return tokenCount;
        }

        private void LogMessage(string message)
        {
            txtLog.AppendText(message + Environment.NewLine);
            txtLog.ScrollToEnd();
            Console.WriteLine(message);
        }

        private string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
        }
    }

    public class TranslationItem
    {
        public string Path { get; set; }
        public string OriginalText { get; set; }
        public string TranslatedText { get; set; }
    }
}

