using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Heco.Browser.Infrastructure;
using Microsoft.Data.Sqlite;

namespace Heco.Browser.Models
{
    public class PasswordEntry
    {
        public long Id { get; set; }
        public string Url { get; set; } = "";
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
    }

    public class CardEntry
    {
        public string Guid { get; set; } = System.Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public string CardNumber { get; set; } = "";
        public string Expiry { get; set; } = "";
    }

    public class AddressEntry
    {
        public string Guid { get; set; } = System.Guid.NewGuid().ToString();
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

/// <summary>
    /// Manages autofill following Chrome's schema, storing one SQLite file per profile directory:
    ///   - "Web Data"   → addresses (autofill_profiles) + cards (credit_cards)
    ///   - "Login Data" → passwords (logins)
    /// Data is loaded into POCOs for DataManagerWindow to share; every change calls Save().
    /// </summary>
    public static class AutofillManager
    {
        public static AutofillData Data { get; private set; } = new AutofillData();

        private static string CurrentProfile => Heco.Browser.Models.AppSettings.Global.CurrentProfile;

        static AutofillManager()
        {
            Load();
        }

        public static void Load()
        {
            Data = new AutofillData();
            var profile = CurrentProfile;

            try
            {
                using (var wb = SqliteHelper.Open(UserDataPaths.WebDataFile(profile)))
                {
                    ReadAddresses(wb, Data.Addresses);
                    ReadCards(wb, Data.Cards);
                }

                using (var ld = SqliteHelper.Open(UserDataPaths.LoginDataFile(profile)))
                {
                    ReadPasswords(ld, Data.Passwords);
                }
            }
            catch { }
        }

        public static void Save()
        {
            // CEF handles Autofill and Login Data natively. We shouldn't write manually.
        }

        // ---------- Read ----------

        private static void ReadPasswords(SqliteConnection conn, List<PasswordEntry> list)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, origin_url, username_value, password_value FROM logins WHERE blocklisted_by_user = 0 ORDER BY id;";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new PasswordEntry
                {
                    Id = r.IsDBNull(0) ? 0 : r.GetInt64(0),
                    Url = r.GetString(1),
                    Username = r.GetString(2),
                    Password = r.IsDBNull(3) ? "" : r.GetString(3),
                });
            }
        }

        private static void ReadCards(SqliteConnection conn, List<CardEntry> list)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT guid, name_on_card, expiration_month, expiration_year, card_number_encrypted FROM credit_cards ORDER BY date_modified DESC;";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var guid = r.GetString(0);
                var month = r.IsDBNull(2) ? "" : r.GetInt32(2).ToString("00");
                var year = r.IsDBNull(3) ? "" : (r.GetInt32(3) > 99 ? r.GetInt32(3).ToString().Substring(2) : r.GetInt32(3).ToString("00"));
                list.Add(new CardEntry
                {
                    Guid = guid,
                    Name = r.GetString(1),
                    CardNumber = r.IsDBNull(4) ? "" : System.Text.Encoding.UTF8.GetString((byte[])r.GetValue(4)),
                    Expiry = year.Length == 0 ? month : $"{month}/{year}",
                });
            }
        }

        private static void ReadAddresses(SqliteConnection conn, List<AddressEntry> list)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT guid, company_name, street_address, city FROM autofill_profiles ORDER BY date_modified DESC;";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var name = Trim(r, 1);
                var street = Trim(r, 2);
                var city = Trim(r, 3);
                list.Add(new AddressEntry
                {
                    Guid = r.GetString(0),
                    Name = name,
                    Address = string.IsNullOrEmpty(city) ? street : string.IsNullOrEmpty(street) ? city : $"{street}, {city}",
                    Phone = ReadFirstPhone(conn, r.GetString(0)),
                });
            }
        }

        private static string ReadFirstPhone(SqliteConnection conn, string guid)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT number FROM autofill_profile_phones WHERE guid=$g ORDER BY rowid LIMIT 1;";
            cmd.Parameters.AddWithValue("$g", guid);
            var v = cmd.ExecuteScalar();
            return v == null || v == DBNull.Value ? "" : v.ToString() ?? "";
        }

        private static string Trim(SqliteDataReader r, int i) => (r.IsDBNull(i) ? "" : r.GetString(i)).Trim();

        // ---------- Write ----------

        private static void SavePasswords(SqliteConnection conn, List<PasswordEntry> list)
        {
            SqliteHelper.Exec(conn, "DELETE FROM logins;");
            foreach (var p in list)
            {
                SqliteHelper.Exec(conn, """
                    INSERT INTO logins(origin_url, username_element, username_value, password_value, signon_realm,
                        scheme, password_type, times_used, date_created) VALUES($url,'',$usr,$pwd,$url,0,0,1,strftime('%s','now'));
                    """, ("$url", p.Url), ("$usr", p.Username), ("$p", p.Password));
            }
        }

        private static void SaveCards(SqliteConnection conn, List<CardEntry> list)
        {
            SqliteHelper.Exec(conn, "DELETE FROM credit_cards;");
            foreach (var c in list)
            {
                string? month = null, year = null;
                var parts = c.Expiry.Split('/');
                if (parts.Length == 2)
                {
                    if (int.TryParse(parts[0].Trim(), out var m)) month = m.ToString();
                    if (int.TryParse(parts[1].Trim(), out var y))
                    {
                        year = (y > 99 ? y : 2000 + y).ToString();
                        if (year.Length == 4) year = year.Substring(2);
                    }
                }
                SqliteHelper.Exec(conn, """
                    INSERT INTO credit_cards (guid, name_on_card, expiration_month, expiration_year,
                        card_number_encrypted, date_modified) VALUES ($g,$n,$m,$y,$c,strftime('%s','now'));
                    """, ("$g", c.Guid), ("$n", c.Name),
                    ("$m", month), ("$y", year), ("$c", System.Text.Encoding.UTF8.GetBytes(c.CardNumber)));
            }
        }

        private static void SaveAddresses(SqliteConnection conn, List<AddressEntry> list)
        {
            SqliteHelper.Exec(conn, "DELETE FROM autofill_profiles; DELETE FROM autofill_profile_phones;");
            foreach (var a in list)
            {
                SqliteHelper.Exec(conn, """
                    INSERT INTO autofill_profiles (guid, company_name, street_address, city, date_modified)
                    VALUES ($guid,$name,$addr,$city,strftime('%s','now'));
                    """, ("$guid", a.Guid), ("$name", a.Name), ("$addr", a.Address), ("$city", ""));
                SqliteHelper.Exec(conn,
                    "INSERT INTO autofill_profile_phones(guid, number) VALUES ($guid,$ph);",
                    ("$guid", a.Guid), ("$ph", a.Phone));
            }
        }

        // ---------- Schema ----------

        private static void EnsureWebDataSchema(SqliteConnection conn)
        {
            EnsureMeta(conn);

            SqliteHelper.Exec(conn, """
                CREATE TABLE IF NOT EXISTS autofill (
                    name VARCHAR, value VARCHAR, value_lower VARCHAR,
                    date_created INTEGER NOT NULL DEFAULT 0, date_last_used INTEGER NOT NULL DEFAULT 0,
                    count INTEGER NOT NULL DEFAULT 1);
                """);

            SqliteHelper.Exec(conn, """
                CREATE TABLE IF NOT EXISTS autofill_profiles (
                    guid VARCHAR(128) PRIMARY KEY, company_name VARCHAR(255), street_address VARCHAR(255),
                    dependent_locality VARCHAR(255), city VARCHAR(255), state VARCHAR(255),
                    zipcode VARCHAR(255), sorting_code VARCHAR(255), country_code VARCHAR(255),
                    use_count INTEGER NOT NULL DEFAULT 0, use_date INTEGER NOT NULL DEFAULT 0,
                    date_modified INTEGER NOT NULL DEFAULT 0, language_code VARCHAR(50),
                    label VARCHAR(255), disallow_settings_visible_updates INTEGER);
                """);
            SqliteHelper.Exec(conn, """
                CREATE TABLE IF NOT EXISTS autofill_profile_names (
                    guid VARCHAR(128), first_name VARCHAR(255), middle_name VARCHAR(255),
                    last_name VARCHAR(255), first_last_name VARCHAR(255), conjunction_last_name VARCHAR(255),
                    second_last_name VARCHAR(255), full_name VARCHAR(255));
                """);
            SqliteHelper.Exec(conn, """
                CREATE TABLE IF NOT EXISTS autofill_profile_emails (guid VARCHAR(128), email VARCHAR(255));
                """);
            SqliteHelper.Exec(conn, """
                CREATE TABLE IF NOT EXISTS autofill_profile_phones (guid VARCHAR(128), number VARCHAR(255));
                """);
            SqliteHelper.Exec(conn, """
                CREATE TABLE IF NOT EXISTS autofill_profile_birthdates (guid VARCHAR(128), day INTEGER, month INTEGER, year INTEGER);
                """);
            SqliteHelper.Exec(conn, """
                CREATE TABLE IF NOT EXISTS autofill_profile_addresses (
                    guid VARCHAR(128), street_address VARCHAR(255), street_name VARCHAR(255),
                    dependent_street_name VARCHAR(255), house_number VARCHAR(255), subpremise VARCHAR(255),
                    dependent_locality VARCHAR(255), city VARCHAR(255), state VARCHAR(255),
                    zip_code VARCHAR(255), country_code VARCHAR(255), sorting_code VARCHAR(255),
                    apartment_number VARCHAR(255), floor VARCHAR(255));
                """);

            SqliteHelper.Exec(conn, """
                CREATE TABLE IF NOT EXISTS credit_cards (
                    guid VARCHAR(64) PRIMARY KEY, name_on_card VARCHAR(255),
                    expiration_month INTEGER DEFAULT 0, expiration_year INTEGER DEFAULT 0,
                    card_number_encrypted BLOB, use_count INTEGER NOT NULL DEFAULT 0,
                    use_date INTEGER NOT NULL DEFAULT 0, date_modified INTEGER NOT NULL DEFAULT 0,
                    is_user_confirmed INTEGER NOT NULL DEFAULT 0, billing_address_id VARCHAR(64),
                    nickname VARCHAR(255));
                """);
            SqliteHelper.Exec(conn, "CREATE TABLE IF NOT EXISTS local_stored_cvc(guid VARCHAR(64) PRIMARY KEY, value_encrypted BLOB NOT NULL, last_updated_timestamp INTEGER NOT NULL);");

            SqliteHelper.Exec(conn, """
                CREATE TABLE IF NOT EXISTS keywords (
                    id INTEGER PRIMARY KEY, short_name VARCHAR NOT NULL, keyword VARCHAR NOT NULL,
                    favicon_url VARCHAR(512) NOT NULL, url VARCHAR(512) NOT NULL,
                    safe_for_autoreplace INTEGER NOT NULL, originating_url VARCHAR(512),
                    date_created INTEGER NOT NULL DEFAULT 0, usage_count INTEGER NOT NULL DEFAULT 0,
                    input_encodings VARCHAR(255), suggest_url VARCHAR(512),
                    prepopulate_id INTEGER DEFAULT 0 NOT NULL, created_by_policy INTEGER DEFAULT 0 NOT NULL,
                    last_modified INTEGER NOT NULL DEFAULT 0, sync_guid VARCHAR, alternate_urls VARCHAR(1024),
                    image_url VARCHAR(512), search_url_post_params VARCHAR(1024), suggest_url_post_params VARCHAR(1024),
                    image_url_post_params VARCHAR(1024), new_tab_url VARCHAR(512), last_visited INTEGER NOT NULL DEFAULT 0,
                    starter_pack_id INTEGER DEFAULT 0 NOT NULL, enforced_by_policy INTEGER DEFAULT 0 NOT NULL,
                    featured_by_policy INTEGER DEFAULT 0 NOT NULL);
                """);
            SqliteHelper.Exec(conn, "CREATE TABLE IF NOT EXISTS token_service(service VARCHAR PRIMARY KEY NOT NULL, encrypted_token BLOB);");
            SqliteHelper.Exec(conn, "CREATE TABLE IF NOT EXISTS autofill_sync_metadata(model_type INTEGER NOT NULL, storage_key VARCHAR NOT NULL, value BLOB);");
            SqliteHelper.Exec(conn, "CREATE TABLE IF NOT EXISTS autofill_model_type_state(model_type INTEGER PRIMARY KEY NOT NULL, value BLOB);");
        }

        private static void EnsureLoginDataSchema(SqliteConnection conn)
        {
            EnsureMeta(conn);

            // Migration from the old schema: rename column blacklisted_by_user → blocklisted_by_user
            try { SqliteHelper.Exec(conn, "ALTER TABLE logins RENAME COLUMN blacklisted_by_user TO blocklisted_by_user;"); } catch { }

            SqliteHelper.Exec(conn, """
                CREATE TABLE IF NOT EXISTS logins (
                    origin_url VARCHAR NOT NULL, action_url VARCHAR, username_element VARCHAR,
                    username_value VARCHAR, password_element VARCHAR, password_value BLOB,
                    submit_element VARCHAR, signon_realm VARCHAR NOT NULL, date_created INTEGER NOT NULL DEFAULT 0,
                    blocklisted_by_user INTEGER NOT NULL DEFAULT 0, scheme INTEGER NOT NULL DEFAULT 0,
                    password_type INTEGER NOT NULL DEFAULT 0, times_used INTEGER NOT NULL DEFAULT 0,
                    form_data BLOB, display_name VARCHAR, icon_url VARCHAR, federation_url VARCHAR,
                    skip_zero_click INTEGER NOT NULL DEFAULT 0, generation_upload_status INTEGER NOT NULL DEFAULT 0,
                    possible_username_pairs BLOB, id INTEGER PRIMARY KEY AUTOINCREMENT,
                    date_last_used INTEGER NOT NULL DEFAULT 0, moving_blocked_for BLOB,
                    date_password_modified INTEGER NOT NULL DEFAULT 0, sender_email VARCHAR, sender_name VARCHAR,
                    date_received INTEGER, sharing_notification_displayed INTEGER,
                    sender_profile_image_url VARCHAR, date_last_filled INTEGER, actor_login_approved INTEGER);
                """);
            SqliteHelper.Exec(conn, """
                CREATE TABLE IF NOT EXISTS stats (
                    origin_domain VARCHAR NOT NULL, username_value VARCHAR,
                    dismissal_count INTEGER DEFAULT 0 NOT NULL, update_time INTEGER NOT NULL);
                """);
            SqliteHelper.Exec(conn, """
                CREATE TABLE IF NOT EXISTS insecure_credentials (
                    parent_id INTEGER NOT NULL REFERENCES logins(id) ON UPDATE CASCADE ON DELETE CASCADE,
                    insecurity_type INTEGER NOT NULL, create_time INTEGER NOT NULL,
                    is_muted INTEGER DEFAULT 0 NOT NULL, trigger_notification_from_backend INTEGER DEFAULT 0 NOT NULL);
                """);
            SqliteHelper.Exec(conn, """
                CREATE TABLE IF NOT EXISTS password_notes (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    parent_id INTEGER NOT NULL REFERENCES logins(id) ON UPDATE CASCADE ON DELETE CASCADE,
                    key VARCHAR NOT NULL, value BLOB NOT NULL, date_created INTEGER NOT NULL, confidential INTEGER);
                """);
            SqliteHelper.Exec(conn, "CREATE TABLE IF NOT EXISTS passwords_sync_entities_metadata(storage_key VARCHAR PRIMARY KEY NOT NULL, metadata BLOB);");
            SqliteHelper.Exec(conn, "CREATE TABLE IF NOT EXISTS passwords_sync_model_metadata(id INTEGER PRIMARY KEY NOT NULL, model_metadata BLOB);");
        }

        private static void EnsureMeta(SqliteConnection conn)
        {
            SqliteHelper.Exec(conn, "CREATE TABLE IF NOT EXISTS meta(key LONGVARCHAR NOT NULL UNIQUE PRIMARY KEY, value LONGVARCHAR);");
            try { SqliteHelper.SetMeta(conn, "version", "102"); } catch { }
        }

        // ---------- Legacy JSON migration ----------

        private static void MigrateLegacyJson(string profile)
        {
            try
            {
                var dir = UserDataPaths.ProfileDir(profile);
                var migrateFile = Path.Combine(dir, "Autofill.migrate");
                if (!File.Exists(migrateFile)) return;

                AutofillData? legacy = null;
                try
                {
                    legacy = JsonSerializer.Deserialize<AutofillData>(File.ReadAllText(migrateFile));
                }
                catch { }

                if (legacy != null)
                {
                    foreach (var c in legacy.Cards)
                        if (!Data.Cards.Any(x => x.CardNumber == c.CardNumber && x.Name == c.Name))
                            Data.Cards.Add(c);
                    foreach (var a in legacy.Addresses)
                        if (!Data.Addresses.Any(x => x.Address == a.Address && x.Phone == a.Phone))
                            Data.Addresses.Add(a);
                    foreach (var p in legacy.Passwords)
                        if (!Data.Passwords.Any(x => x.Username == p.Username && x.Url == p.Url))
                            Data.Passwords.Add(p);
                }

                File.Delete(migrateFile);
                Save(); // persist merged data after deleting the migrate file (avoid recursion)
            }
            catch { }
        }
    }
}
