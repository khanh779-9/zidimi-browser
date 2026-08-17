using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CefSharp;
using Zidimi.Browser.Controls;
using Zidimi.Browser.Infrastructure;
using Zidimi.Browser.Models;

namespace Zidimi.Browser.Views
{
    public class CookieItem
    {
        public string Name { get; set; } = "";
        public string Value { get; set; } = "";
        public string Domain { get; set; } = "";
        public string Path { get; set; } = "";
        public bool Secure { get; set; }
        public bool HttpOnly { get; set; }
        public string Expires { get; set; } = "";
    }

    public partial class CookieManagerWindow : Window
    {
        private readonly string _url;
        private readonly ICookieManager? _manager;
        private List<CookieItem> _items = new();

        public CookieManagerWindow(string url)
        {
            InitializeComponent();
            _url = url;

            try
            {
                var ctx = App.RequestContexts.GetProfileContext(AppSettings.Global.CurrentProfile)
                          ?? Cef.GetGlobalRequestContext();
                _manager = ctx.GetCookieManager(null);
            }
            catch (Exception ex)
            {
                AppLogger.Log("Cookies", ex, "Getting the profile cookie manager.");
                _manager = null;
            }

            Loaded += async (_, _) => await LoadCookies();
        }

        private async Task LoadCookies()
        {
            if (_manager == null)
            {
                CookieList.ItemsSource = new List<CookieItem>();
                CookieCount.Text = LanguageManager.Instance["Cookie_Unavailable"];
                return;
            }

            var baseHost = Uri.TryCreate(_url, UriKind.Absolute, out var pageUri)
                ? pageUri.Host
                : string.Empty;

            // Get cookies for the current URL (host cookies) plus all of them to filter by domain.
            var items = new List<CookieItem>();
            var visitor = new TaskCookieVisitor();
            try
            {
                if (Uri.IsWellFormedUriString(_url, UriKind.Absolute))
                {
                    _manager.VisitUrlCookies(_url, true, visitor);
                    var cookies = await visitor.Task;
                    foreach (var c in cookies)
                        items.Add(ToItem(c));
                }
            }
            catch (Exception ex)
            {
                AppLogger.Log("Cookies", ex, $"Visiting cookies for '{_url}'.");
            }

            // fallback: list all cookies if URL filtering is not available
            if (items.Count == 0 && !string.IsNullOrEmpty(baseHost))
            {
                var allVisitor = new TaskCookieVisitor();
                try
                {
                    _manager.VisitAllCookies(allVisitor);
                    var all = await allVisitor.Task;
                    items = all
                        .Where(c => DomainMatchesHost(c.Domain ?? string.Empty, baseHost))
                        .Select(ToItem)
                        .ToList();
                }
                catch (Exception ex)
                {
                    AppLogger.Log("Cookies", ex, "Visiting all cookies as fallback.");
                }
            }

            _items = items.OrderBy(i => i.Domain).ThenBy(i => i.Name).ToList();
            CookieList.ItemsSource = _items;
            CookieCount.Text = string.Format(
                LanguageManager.Instance[_items.Count == 0 ? "Cookie_None" : "Cookie_Count"], _items.Count);
        }

        private static CookieItem ToItem(Cookie c) => new()
        {
            Name = c.Name ?? "",
            Value = c.Value ?? "",
            Domain = c.Domain ?? "",
            Path = c.Path ?? "",
            Secure = c.Secure,
            HttpOnly = c.HttpOnly,
            Expires = c.Expires.HasValue ? c.Expires.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "Session",
        };

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (_manager == null) return;
            var selected = CookieList.SelectedItems?.Cast<CookieItem>().ToList();
            if (selected == null || selected.Count == 0) return;

            foreach (var item in selected)
            {
                try
                {
                    var cookieUrl = BuildCookieUrl(item);
                    await _manager.DeleteCookiesAsync(cookieUrl, item.Name);
                }
                catch (Exception ex)
                {
                    AppLogger.Log("Cookies", ex, $"Deleting cookie '{item.Name}' for '{item.Domain}'.");
                }
            }

            await LoadCookies();
        }


        private static bool DomainMatchesHost(string domain, string host)
        {
            domain = domain.Trim().TrimStart('.');
            host = host.Trim().TrimEnd('.');
            return host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
                   host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildCookieUrl(CookieItem item)
        {
            var host = item.Domain.Trim().TrimStart('.');
            var path = string.IsNullOrWhiteSpace(item.Path) ? "/" : item.Path;
            if (!path.StartsWith('/')) path = "/" + path;
            return $"{(item.Secure ? "https" : "http")}://{host}{path}";
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}