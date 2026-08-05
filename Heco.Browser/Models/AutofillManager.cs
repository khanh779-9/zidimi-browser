using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Heco.Browser.Models
{
    public class PasswordEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Url { get; set; } = "";
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
    }

    public class CardEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public string CardNumber { get; set; } = "";
        public string Expiry { get; set; } = "";
    }

    public class AddressEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Address { get; set; } = "";
    }

    public class AutofillData
    {
        public List<PasswordEntry> Passwords { get; set; } = new();
        public List<CardEntry> Cards { get; set; } = new();
        public List<AddressEntry> Addresses { get; set; } = new();
    }

    public static class AutofillManager
    {
        private static readonly string FilePath;
        public static AutofillData Data { get; private set; } = new AutofillData();

        static AutofillManager()
        {
            var appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HecoBrowser");
            Directory.CreateDirectory(appData);
            FilePath = Path.Combine(appData, "autofill.json");
            Load();
        }

        public static void Load()
        {
            if (File.Exists(FilePath))
            {
                try
                {
                    var json = File.ReadAllText(FilePath);
                    Data = JsonSerializer.Deserialize<AutofillData>(json) ?? new AutofillData();
                }
                catch
                {
                    Data = new AutofillData();
                }
            }
        }

        public static void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(Data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, json);
            }
            catch { }
        }
    }
}
