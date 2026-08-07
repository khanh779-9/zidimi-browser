using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Heco.Browser.Infrastructure
{
    public class LanguageInfo
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
    }

    public class LanguageManager : INotifyPropertyChanged
    {
        private static LanguageManager? _instance;
        public static LanguageManager Instance => _instance ??= new LanguageManager();

        private readonly Dictionary<string, string> _currentStrings = new();
        
        public List<LanguageInfo> AvailableLanguages { get; } = new();

        private LanguageInfo? _currentLanguage;
        public LanguageInfo? CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                if (_currentLanguage != value && value != null)
                {
                    _currentLanguage = value;
                    LoadLanguageFile(value.FilePath);
                    SaveConfig(value.Code);
                    OnPropertyChanged();
                    // WPF uses Item[] to bind to indexers
                    OnPropertyChanged("Item[]");
                }
            }
        }

        public string this[string key]
        {
            get
            {
                if (_currentStrings.TryGetValue(key, out var val))
                {
                    return val;
                }
                return key; // return key as fallback
            }
        }

        private string ConfigPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
        private string LangDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "language");

        private LanguageManager()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (!Directory.Exists(LangDirectory))
            {
                Directory.CreateDirectory(LangDirectory);
            }

            // Read available languages
            string[] files = Directory.GetFiles(LangDirectory, "*.lng");
            foreach (var file in files)
            {
                var info = ParseLanguageInfo(file);
                if (info != null)
                {
                    AvailableLanguages.Add(info);
                }
            }

// Load last used language: prefer AppSettings.DisplayLanguage (the primary source),
            // fall back to the old config.ini, then to the default.
            string lastLangCode = Heco.Browser.Models.AppSettings.Global.DisplayLanguage;
            if (string.IsNullOrEmpty(lastLangCode) || lastLangCode.Length > 10)
                lastLangCode = "vi-VN";
            if (File.Exists(ConfigPath) && AvailableLanguages.All(l => l.Code != lastLangCode))
            {
                var configLines = File.ReadAllLines(ConfigPath);
                foreach (var line in configLines)
                {
                    if (line.StartsWith("Language="))
                    {
                        lastLangCode = line.Substring("Language=".Length).Trim();
                        break;
                    }
                }
            }

            var langToSet = AvailableLanguages.FirstOrDefault(l => l.Code == lastLangCode) 
                            ?? AvailableLanguages.FirstOrDefault()
                            ?? new LanguageInfo { Code = "en-US", Name = "English", FilePath = "" };
                            
            if (!string.IsNullOrEmpty(langToSet.FilePath))
            {
                CurrentLanguage = langToSet;
            }
        }

        private LanguageInfo ParseLanguageInfo(string filePath)
        {
            string code = Path.GetFileNameWithoutExtension(filePath);
            string name = code;
            
            try
            {
                var lines = File.ReadAllLines(filePath);
                string currentSection = "";
                foreach (var line in lines)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";") || trimmed.StartsWith("#")) continue;

                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        currentSection = trimmed.Substring(1, trimmed.Length - 2);
                        continue;
                    }

                    if (currentSection == "Info" && trimmed.StartsWith("LanguageName="))
                    {
                        name = trimmed.Substring("LanguageName=".Length).Trim();
                        break;
                    }
                }
            }
            catch { }

            return new LanguageInfo { Code = code, Name = name, FilePath = filePath };
        }

        private void LoadLanguageFile(string filePath)
        {
            _currentStrings.Clear();
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

            try
            {
                var lines = File.ReadAllLines(filePath);
                string currentSection = "";
                
                foreach (var line in lines)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";") || trimmed.StartsWith("#")) continue;

                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        currentSection = trimmed.Substring(1, trimmed.Length - 2);
                        continue;
                    }

                    int equalsIndex = trimmed.IndexOf('=');
                    if (equalsIndex > 0)
                    {
                        string key = trimmed.Substring(0, equalsIndex).Trim();
                        string value = trimmed.Substring(equalsIndex + 1).Trim();
                        
                        string fullKey = key.Contains("_") ? key : $"{currentSection}_{key}";
                        _currentStrings[fullKey] = value;
                    }
                }
            }
            catch { }
        }

        private void SaveConfig(string langCode)
        {
// The primary source is AppSettings.DisplayLanguage (stored in settings.json);
            // config.ini is kept only for backward compatibility.
            try
            {
                Heco.Browser.Models.AppSettings.Global.DisplayLanguage = langCode;
                Heco.Browser.Models.AppSettings.SaveAll();
            }
            catch { }

            try
            {
                var lines = new List<string>();
                if (File.Exists(ConfigPath))
                {
                    lines = File.ReadAllLines(ConfigPath).ToList();
                }

                int langIndex = lines.FindIndex(l => l.StartsWith("Language="));
                if (langIndex >= 0)
                {
                    lines[langIndex] = $"Language={langCode}";
                }
                else
                {
                    lines.Add($"Language={langCode}");
                }

                File.WriteAllLines(ConfigPath, lines);
            }
            catch { }
        }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
}

