using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using GameTranslator.Services;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using System.Xml.Linq;
using GameTranslator.Model;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;
using System;

namespace GameTranslator.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly ITranslationService _translationService;

        private string _selectedFilePath;
        public string SelectedFilePath
        {
            get { return _selectedFilePath; }
            set
            {
                _selectedFilePath = value;
                OnPropertyChanged(nameof(SelectedFilePath));
            }
        }

        private string _detectedFormat;
        public string DetectedFormat
        {
            get { return _detectedFormat; }
            set
            {
                _detectedFormat = value;
                OnPropertyChanged(nameof(DetectedFormat));
            }
        }

        private string _logText;
        public string LogText
        {
            get { return _logText; }
            set
            {
                _logText = value;
                OnPropertyChanged(nameof(LogText));
            }
        }

        private int _progressValue;
        public int ProgressValue
        {
            get { return _progressValue; }
            set
            {
                _progressValue = value;
                OnPropertyChanged(nameof(ProgressValue));
            }
        }

        private int _progressMaximum;
        public int ProgressMaximum
        {
            get { return _progressMaximum; }
            set
            {
                _progressMaximum = value;
                OnPropertyChanged(nameof(ProgressMaximum));
            }
        }

        // TranslationItem Listesi
        public ObservableCollection<TranslationItem> TranslationItems { get; set; } = new ObservableCollection<TranslationItem>();

        // Dil kodları sözlüğü
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

        // ComboBox'lar için Listeler
        public List<string> SourceLanguages { get; }
        public List<string> TargetLanguages { get; }
        public List<string> FormatList { get; }

        private string _selectedSourceLanguage;
        private string _selectedTargetLanguage;
        private string _selectedFormat;

        public string SelectedSourceLanguage
        {
            get { return _selectedSourceLanguage; }
            set { _selectedSourceLanguage = value;  }
        }

        public string SelectedTargetLanguage
        {
            get { return _selectedTargetLanguage; }
            set { _selectedTargetLanguage = value; }
        }

        public string SelectedFormat
        {
            get { return _selectedFormat; }
            set { _selectedFormat = value; }
        }

        // ViewModel constructor
        public MainViewModel(ITranslationService translationService)
        {
            _translationService = translationService;

            // Dil listelerini oluşturuyoruz
            SourceLanguages = languageCodes.Keys.ToList();
            TargetLanguages = languageCodes.Keys.ToList();
            FormatList = new List<string> { "XML", "JSON", "CSV", "Text" }; // Format listesi


            SelectedSourceLanguage = "Otomatik Algıla";
            SelectedTargetLanguage = "Türkçe";
            SelectedFormat = "XML";

            // Komutları başlatıyoruz
            BrowseCommand = new RelayCommand(BrowseFile);
            AnalyzeCommand = new RelayCommand(AnalyzeFile);
            TranslateCommand = new RelayCommand(async () => await TranslateAllAsync());
            CheckBlockCommand = new RelayCommand(async () => await CheckIfBlockedAsync());
        }

        public ICommand BrowseCommand { get; }
        public ICommand AnalyzeCommand { get; }
        public ICommand TranslateCommand { get; }
        public ICommand CheckBlockCommand { get; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        // Dosya seçme metodunu tanımlıyoruz
        public void BrowseFile()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Çevrilecek Oyun Dosyasını Seçin",
                Filter = "Tüm Dosyalar|*.*|XML Dosyaları|*.xml|JSON Dosyaları|*.json|CSV Dosyaları|*.csv|Metin Dosyaları|*.txt"
            };

            if (dialog.ShowDialog() == true)
            {
                SelectedFilePath = dialog.FileName;
                DetectFormat();
                Log($"Dosya seçildi: {SelectedFilePath}");
            }
        }
        private void AnalyzeXml(string content)
        {
            var doc = XDocument.Parse(content);
            ParseXml(doc.Root, "");
        }

        private void ParseXml(XElement element, string path)
        {
            var currentPath = string.IsNullOrEmpty(path) ? element.Name.LocalName : $"{path}/{element.Name.LocalName}";

            if (!string.IsNullOrWhiteSpace(element.Value) && !element.Elements().Any())
            {
                TranslationItems.Add(new TranslationItem { Path = currentPath, OriginalText = element.Value.Trim() });
            }

            foreach (var child in element.Elements())
            {
                ParseXml(child, currentPath);
            }
        }
        private void AnalyzeJson(string content)
        {
            var token = JToken.Parse(content);
            ParseJson(token, "");
        }

        private void ParseJson(JToken token, string path)
        {
            if (token is JValue value && value.Type == JTokenType.String)
            {
                TranslationItems.Add(new TranslationItem { Path = path, OriginalText = value.Value<string>() });
            }
            else if (token is JObject obj)
            {
                foreach (var prop in obj.Properties())
                {
                    ParseJson(prop.Value, string.IsNullOrEmpty(path) ? prop.Name : $"{path}.{prop.Name}");
                }
            }
            else if (token is JArray array)
            {
                for (int i = 0; i < array.Count; i++)
                {
                    ParseJson(array[i], $"{path}[{i}]");
                }
            }
        }

        private void AnalyzeCsv(string content)
        {
            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++)
            {
                var fields = lines[i].Split(',');
                for (int j = 0; j < fields.Length; j++)
                {
                    if (!string.IsNullOrWhiteSpace(fields[j]))
                    {
                        TranslationItems.Add(new TranslationItem { Path = $"Satır:{i + 1},Sütun:{j + 1}", OriginalText = fields[j].Trim() });
                    }
                }
            }
        }

        private void AnalyzeText(string content)
        {
            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(lines[i]))
                {
                    TranslationItems.Add(new TranslationItem { Path = $"Satır:{i + 1}", OriginalText = lines[i].Trim() });
                }
            }
        }



        // Dosya analiz metodunu tanımlıyoruz
        public void AnalyzeFile()
        {
            if (string.IsNullOrEmpty(SelectedFilePath) || !File.Exists(SelectedFilePath))
            {
                Log("Lütfen geçerli bir dosya seçin!");
                return;
            }

            TranslationItems.Clear();

            var content = File.ReadAllText(SelectedFilePath);

            switch (DetectedFormat)
            {
                case "XML": AnalyzeXml(content); break;
                case "JSON": AnalyzeJson(content); break;
                case "CSV": AnalyzeCsv(content); break;
                case "Text": AnalyzeText(content); break;
                default: Log("Desteklenmeyen dosya formatı."); break;
            }

            ProgressMaximum = TranslationItems.Count;
            ProgressValue = 0;

            Log($"Analiz tamamlandı: {TranslationItems.Count} çeviri bulunuyor.");
        }

        // Çeviriyi başlatan metod
        public async Task TranslateAllAsync()
        {
            if (TranslationItems.Count == 0)
            {
                Log("Çevrilecek metin bulunamadı!");
                return;
            }

            string combinedText = string.Join(" ||| ", TranslationItems.Select(x => x.OriginalText));

            try
            {
                Log("Çeviri başlatıldı...");
                var translatedCombinedText = await _translationService.TranslateTextAsync(combinedText, "auto", "tr");

                var translatedParts = translatedCombinedText.Split(new[] { "|||" }, StringSplitOptions.None);

                for (int i = 0; i < TranslationItems.Count && i < translatedParts.Length; i++)
                {
                    TranslationItems[i].TranslatedText = translatedParts[i].Trim();
                    ProgressValue = i + 1;
                }

                SaveTranslatedFile();
                Log("Çeviri tamamlandı ve dosya kaydedildi.");
            }
            catch (Exception ex)
            {
                Log($"Çeviri hatası: {ex.Message}");
            }
        }

        // Dil formatlarını tespit eden metod
        private void DetectFormat()
        {
            var extension = Path.GetExtension(SelectedFilePath)?.ToLower();
            var content = File.ReadAllText(SelectedFilePath);

            switch (extension)
            {
                case ".xml":
                    DetectedFormat = "XML";
                    break;
                case ".json":
                    DetectedFormat = "JSON";
                    break;
                case ".csv":
                    DetectedFormat = "CSV";
                    break;
                case ".txt":
                    DetectedFormat = "Text";
                    break;
                default:
                    DetectedFormat = "Unknown";
                    break;
            }

            if (DetectedFormat == "Unknown")
            {
                if (content.TrimStart().StartsWith("<") && content.TrimEnd().EndsWith(">"))
                    DetectedFormat = "XML";
                else if (content.TrimStart().StartsWith("{") && content.TrimEnd().EndsWith("}"))
                    DetectedFormat = "JSON";
                else if (content.Contains(",") && content.Split('\n').Any(line => line.Count(c => c == ',') > 0))
                    DetectedFormat = "CSV";
                else
                    DetectedFormat = "Text";
            }
        }

        // Çeviri işlemini kontrol eden metod
        public async Task CheckIfBlockedAsync()
        {
            try
            {
                bool isBlocked = await _translationService.CheckIfBlockedAsync();
                if (isBlocked)
                {
                    Log("API erişimi bloklanmış.");
                }
                else
                {
                    Log("API erişimi normal.");
                }
            }
            catch (Exception ex)
            {
                Log($"Hata: {ex.Message}");
            }
        }

        // Çevirilen dosyayı kaydeden metod
        private void SaveTranslatedFile()
        {
            var outputPath = Path.Combine(Path.GetDirectoryName(SelectedFilePath),
                                          Path.GetFileNameWithoutExtension(SelectedFilePath) + "_translated" +
                                          Path.GetExtension(SelectedFilePath));

            var outputLines = TranslationItems.Select(item => item.TranslatedText);

            File.WriteAllText(outputPath, string.Join(Environment.NewLine, outputLines));
            Log($"Çevirilen dosya kaydedildi: {outputPath}");
        }

        // Log yazma metodunu tanımlıyoruz
        private void Log(string message)
        {
            LogText += $"{DateTime.Now:HH:mm:ss} - {message}\n";
        }
    }
}
