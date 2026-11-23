using System.Globalization;
using System.Text.Json;

namespace Island.Modules.Weather;

/// <summary>某個時刻的天氣預報(逐時)。</summary>
public sealed record HourForecast(DateTime Time, double Temperature, int Code, int RainChance);

/// <summary>一次從 Open-Meteo 取回的資料。</summary>
public sealed record WeatherSnapshot
{
    public DateTime FetchedAt { get; init; }
    public double Temperature { get; init; }
    public double FeelsLike { get; init; }
    public int Code { get; init; }
    public bool IsDay { get; init; }
    /// <summary>現在的降水量(mm/15 分)。</summary>
    public double PrecipitationNow { get; init; }
    public double TodayMax { get; init; }
    public double TodayMin { get; init; }
    public int TodayRainChance { get; init; }
    /// <summary>接下來的逐時預報(從下一個整點起)。</summary>
    public IReadOnlyList<HourForecast> Hours { get; init; } = [];
    /// <summary>接下來每 15 分鐘的降水量(從現在這一格起)。</summary>
    public IReadOnlyList<double> Next15Min { get; init; } = [];

    /// <summary>解析 Open-Meteo forecast API 的 JSON(時區為當地時間)。</summary>
    public static WeatherSnapshot Parse(string json, DateTime now)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var cur = root.GetProperty("current");
        var daily = root.GetProperty("daily");
        var hourly = root.GetProperty("hourly");

        var hourTimes = hourly.GetProperty("time").EnumerateArray().Select(t => ParseTime(t.GetString())).ToArray();
        var hourTemps = hourly.GetProperty("temperature_2m").EnumerateArray().Select(Num).ToArray();
        var hourCodes = hourly.GetProperty("weather_code").EnumerateArray().Select(e => (int)Num(e)).ToArray();
        var hourRain = hourly.GetProperty("precipitation_probability").EnumerateArray().Select(e => (int)Num(e)).ToArray();

        var hours = new List<HourForecast>();
        for (int i = 0; i < hourTimes.Length && hours.Count < 6; i++)
        {
            if (hourTimes[i] <= now) continue;
            hours.Add(new HourForecast(hourTimes[i], hourTemps[i], hourCodes[i], hourRain[i]));
        }

        var q = root.GetProperty("minutely_15");
        var qTimes = q.GetProperty("time").EnumerateArray().Select(t => ParseTime(t.GetString())).ToArray();
        var qRain = q.GetProperty("precipitation").EnumerateArray().Select(Num).ToArray();
        var next = new List<double>();
        for (int i = 0; i < qTimes.Length && next.Count < 4; i++)
        {
            if (qTimes[i].AddMinutes(15) <= now) continue;   // 已經過去的格子
            next.Add(qRain[i]);
        }

        return new WeatherSnapshot
        {
            FetchedAt = now,
            Temperature = Num(cur.GetProperty("temperature_2m")),
            FeelsLike = Num(cur.GetProperty("apparent_temperature")),
            Code = (int)Num(cur.GetProperty("weather_code")),
            IsDay = Num(cur.GetProperty("is_day")) > 0,
            PrecipitationNow = Num(cur.GetProperty("precipitation")),
            TodayMax = Num(daily.GetProperty("temperature_2m_max")[0]),
            TodayMin = Num(daily.GetProperty("temperature_2m_min")[0]),
            TodayRainChance = (int)Num(daily.GetProperty("precipitation_probability_max")[0]),
            Hours = hours,
            Next15Min = next,
        };
    }

    private static double Num(JsonElement e) => e.ValueKind == JsonValueKind.Number ? e.GetDouble() : 0;

    private static DateTime ParseTime(string? s) =>
        DateTime.ParseExact(s ?? "", "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);
}

/// <summary>WMO 天氣代碼 → 文字與單色符號(Segoe UI Symbol 有的字元,WPF 可以正常顯示)。</summary>
/// <summary>下雨提醒的判斷(純邏輯,方便測試)。</summary>
internal static class RainAlert
{
    /// <summary>
    /// 現在沒在下、接下來 30 分鐘(兩格 15 分)有降水 → 提醒一次。
    /// 正在下雨時視為已提醒;預報轉晴後重設,下一場雨可以再提醒。
    /// </summary>
    public static bool ShouldAlert(WeatherSnapshot s, ref bool alerted, double threshold = 0.1)
    {
        bool rainingNow = s.PrecipitationNow >= threshold || WeatherCodes.IsWet(s.Code);
        bool rainSoon = s.Next15Min.Skip(1).Take(2).Any(v => v >= threshold);

        if (rainingNow)
        {
            alerted = true;    // 已經在下了,不用再提醒
            return false;
        }
        if (!rainSoon)
        {
            alerted = false;   // 預報轉晴,下次再有雨可以重新提醒
            return false;
        }
        if (alerted) return false;
        alerted = true;
        return true;
    }
}

public static class WeatherCodes
{
    public static string Describe(int code) => code switch
    {
        0 => "晴",
        1 => "大致晴朗",
        2 => "局部多雲",
        3 => "陰",
        45 or 48 => "霧",
        51 or 53 or 55 => "毛毛雨",
        56 or 57 => "凍毛毛雨",
        61 => "小雨",
        63 => "雨",
        65 => "大雨",
        66 or 67 => "凍雨",
        71 or 73 or 75 or 77 => "雪",
        80 or 81 => "陣雨",
        82 => "大陣雨",
        85 or 86 => "陣雪",
        95 => "雷雨",
        96 or 99 => "雷雨伴冰雹",
        _ => "—",
    };

    public static string Symbol(int code, bool isDay) => code switch
    {
        0 or 1 => isDay ? "☀" : "☾",   // ☀ / ☾
        2 => isDay ? "⛅" : "☁",        // ⛅ / ☁
        3 or 45 or 48 => "☁",               // ☁
        >= 51 and <= 67 => "☂",             // ☂
        >= 80 and <= 82 => "☂",
        >= 71 and <= 77 => "❄",             // ❄
        85 or 86 => "❄",
        >= 95 => "⛈",                       // ⛈
        _ => "☁",
    };

    /// <summary>該代碼是否代表正在下雨/雪(用來判斷「已經在下雨」不再提醒)。</summary>
    public static bool IsWet(int code) => code >= 51;
}
