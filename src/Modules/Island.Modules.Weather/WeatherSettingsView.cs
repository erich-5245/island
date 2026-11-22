using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Island.Core;

namespace Island.Modules.Weather;

/// <summary>
/// 設定視窗裡的「天氣」頁:顯示目前位置,搜尋城市(Open-Meteo Geocoding,只送出你輸入的地名)並選擇。
/// </summary>
internal sealed class WeatherSettingsView : StackPanel
{
    private static readonly HttpClient Http = CreateHttp();

    private readonly WeatherModule _module;
    private readonly TextBlock _current;
    private readonly TextBox _query;
    private readonly Button _search;
    private readonly StackPanel _results;
    private readonly TextBlock _status;

    public WeatherSettingsView(WeatherModule module)
    {
        _module = module;

        // 目前位置
        var currentCard = Card();
        var currentPanel = new StackPanel();
        currentPanel.Children.Add(Title("目前位置"));
        _current = Caption("");
        currentPanel.Children.Add(_current);
        currentCard.Child = currentPanel;
        Children.Add(currentCard);

        // 搜尋
        var searchCard = Card();
        var searchPanel = new StackPanel();
        searchPanel.Children.Add(Title("換城市"));
        searchPanel.Children.Add(Caption("輸入城市或區名後按搜尋,從結果中選一個。中文找不到時可以試試英文,例如 Hsinchu、Taipei。"));
        var row = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };
        _search = new Button { Content = "搜尋", Margin = new Thickness(8, 0, 0, 0) };
        _search.SetResourceReference(StyleProperty, IslandTheme.SettingsAccentButton);
        _search.Click += async (_, _) => await SearchAsync();
        DockPanel.SetDock(_search, Dock.Right);
        row.Children.Add(_search);
        _query = new TextBox();
        _query.SetResourceReference(StyleProperty, IslandTheme.SettingsTextBox);
        _query.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await SearchAsync(); };
        row.Children.Add(_query);
        searchPanel.Children.Add(row);
        _status = Caption("");
        _status.Margin = new Thickness(0, 8, 0, 0);
        searchPanel.Children.Add(_status);
        _results = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
        searchPanel.Children.Add(_results);
        searchCard.Child = searchPanel;
        Children.Add(searchCard);

        ShowCurrent();
    }

    private void ShowCurrent()
    {
        var l = _module.Location;
        _current.Text = string.Create(CultureInfo.InvariantCulture, $"{l.Name}(緯度 {l.Latitude:0.###}、經度 {l.Longitude:0.###})");
    }

    private async Task SearchAsync()
    {
        var q = _query.Text.Trim();
        if (q.Length == 0) return;
        _search.IsEnabled = false;
        _status.Text = "搜尋中…";
        _results.Children.Clear();
        try
        {
            var url = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(q)}&count=8&language=zh&format=json";
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));
            if (!doc.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
            {
                _status.Text = "找不到這個地方,換個寫法或改用英文試試。";
                return;
            }
            _status.Text = "選一個:";
            foreach (var r in results.EnumerateArray())
            {
                string name = Str(r, "name");
                string detail = string.Join(" · ", new[] { Str(r, "admin2"), Str(r, "admin1"), Str(r, "country") }
                    .Where(s => s.Length > 0).Distinct());
                double lat = r.GetProperty("latitude").GetDouble();
                double lon = r.GetProperty("longitude").GetDouble();
                _results.Children.Add(ResultButton(name, detail, lat, lon));
            }
        }
        catch (Exception ex)
        {
            _status.Text = $"搜尋失敗:{ex.Message}";
        }
        finally
        {
            _search.IsEnabled = true;
        }
    }

    private Button ResultButton(string name, string detail, double lat, double lon)
    {
        var content = new StackPanel();
        content.Children.Add(new TextBlock { Text = name, FontSize = 13, Foreground = Brushes.White });
        if (detail.Length > 0)
            content.Children.Add(new TextBlock { Text = detail, FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A)) });
        var b = new Button
        {
            Content = content,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 6, 0, 0),
            Padding = new Thickness(12, 7, 12, 7),
        };
        b.SetResourceReference(StyleProperty, IslandTheme.SettingsButton);
        b.Click += (_, _) =>
        {
            // 顯示用的名稱:區名太短(例如「東區」)時加上上一層行政區
            string display = name.Length <= 2 && detail.Length > 0 ? $"{detail.Split(" · ")[0]}{name}" : name;
            _module.SetLocation(new WeatherLocation(display, Math.Round(lat, 4), Math.Round(lon, 4)));
            ShowCurrent();
            _results.Children.Clear();
            _status.Text = $"已改成 {display},天氣會在幾秒內更新。";
        };
        return b;
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static Border Card()
    {
        var b = new Border();
        b.SetResourceReference(StyleProperty, IslandTheme.SettingsCard);
        return b;
    }

    private static TextBlock Title(string text)
    {
        var t = new TextBlock { Text = text };
        t.SetResourceReference(StyleProperty, IslandTheme.SettingsTitleText);
        return t;
    }

    private static TextBlock Caption(string text)
    {
        var t = new TextBlock { Text = text };
        t.SetResourceReference(StyleProperty, IslandTheme.SettingsCaptionText);
        return t;
    }

    private static HttpClient CreateHttp()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Island/0.1");
        return http;
    }
}
