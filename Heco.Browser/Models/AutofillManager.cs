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
        public static AutofillData Data { get; private set; } = new AutofillData();

        private static string FilePath => Heco.Browser.Infrastructure.UserDataPaths.AutofillFile(
            Heco.Browser.Models.AppSettings.Current.CurrentProfile);

        static AutofillManager()
        {
            Load();
        }

        public static void Load()
        {
            var file = FilePath;
            if (File.Exists(file))
            {
                try
                {
                    var json = File.ReadAllText(file);
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
                var file = FilePath;
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
                var json = JsonSerializer.Serialize(Data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(file, json);
            }
            catch { }
        }
    }
}
