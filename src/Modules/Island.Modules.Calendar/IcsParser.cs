using System.Globalization;
using System.Text;

namespace Island.Modules.Calendar;

/// <summary>一個行程(時間都是本地時間)。</summary>
public sealed record CalEvent(DateTime Start, DateTime End, string Title, string? Location, bool AllDay);

/// <summary>
/// iCalendar(.ics)解析:Google 日曆 / Outlook / Apple 日曆的「私人網址」都是這個格式。
/// 支援:時區(TZID、UTC、浮動時間)、全天行程、取消的行程、
/// 重複規則 RRULE 的 DAILY / WEEKLY(BYDAY)/ MONTHLY / YEARLY + INTERVAL / COUNT / UNTIL、EXDATE、
/// 以及單次修改(RECURRENCE-ID)。純函式,不碰網路與 UI。
/// </summary>
public static class IcsParser
{
    private const int MaxIterations = 5000;

    /// <summary>回傳 [from, to) 之間開始或還在進行的行程,依開始時間排序。</summary>
    public static List<CalEvent> Parse(string ics, DateTime from, DateTime to)
    {
        var raw = ReadEvents(ics);
        var overrides = raw.Where(e => e.RecurrenceId is not null).ToList();
        var result = new List<CalEvent>();

        foreach (var e in raw.Where(e => e.RecurrenceId is null))
        {
            if (e.Cancelled || e.Start is null) continue;
            var duration = (e.End ?? (e.AllDay ? e.Start.Value.AddDays(1) : e.Start.Value)) - e.Start.Value;
            if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;

            foreach (var start in Occurrences(e, to))
            {
                if (e.ExDates.Contains(start)) continue;
                // 這一次被單獨修改過(或取消)→ 用修改後的版本
                if (overrides.Any(o => o.Uid == e.Uid && o.RecurrenceId == start)) continue;
                var end = start + duration;
                if (end <= from && !(duration == TimeSpan.Zero && start >= from)) continue;
                result.Add(new CalEvent(start, end, e.Summary, e.Location, e.AllDay));
            }
        }
        foreach (var o in overrides)
        {
            if (o.Cancelled || o.Start is null) continue;
            var end = o.End ?? (o.AllDay ? o.Start.Value.AddDays(1) : o.Start.Value);
            if (o.Start.Value < to && (end > from || o.Start.Value >= from))
                result.Add(new CalEvent(o.Start.Value, end, o.Summary, o.Location, o.AllDay));
        }
        return result.OrderBy(e => e.Start).ThenBy(e => e.Title, StringComparer.CurrentCulture).ToList();
    }

    // ── 讀 VEVENT ───────────────────────────────────────

    private sealed class RawEvent
    {
        public string Uid = "";
        public string Summary = "(沒有標題)";
        public string? Location;
        public DateTime? Start, End;
        public bool AllDay, Cancelled;
        public string? RRule;
        public DateTime? RecurrenceId;
        public readonly HashSet<DateTime> ExDates = [];
    }

    private static List<RawEvent> ReadEvents(string ics)
    {
        var events = new List<RawEvent>();
        RawEvent? cur = null;
        int depth = 0;   // VEVENT 裡面的 VALARM 等子區塊不要混進來
        foreach (var line in Unfold(ics))
        {
            var (name, prms, value) = Split(line);
            if (name == "BEGIN")
            {
                if (value.Equals("VEVENT", StringComparison.OrdinalIgnoreCase) && cur is null) { cur = new RawEvent(); depth = 0; }
                else if (cur is not null) depth++;
                continue;
            }
            if (name == "END")
            {
                if (cur is not null && depth > 0) depth--;
                else if (cur is not null && value.Equals("VEVENT", StringComparison.OrdinalIgnoreCase)) { events.Add(cur); cur = null; }
                continue;
            }
            if (cur is null || depth > 0) continue;
            switch (name)
            {
                case "UID": cur.Uid = value; break;
                case "SUMMARY": cur.Summary = Unescape(value) is { Length: > 0 } s ? s : "(沒有標題)"; break;
                case "LOCATION": cur.Location = Unescape(value) is { Length: > 0 } l ? l : null; break;
                case "STATUS": cur.Cancelled = value.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase); break;
                case "RRULE": cur.RRule = value; break;
                case "DTSTART":
                    cur.Start = ParseTime(value, prms, out var allDay);
                    cur.AllDay = allDay;
                    break;
                case "DTEND": cur.End = ParseTime(value, prms, out _); break;
                case "RECURRENCE-ID": cur.RecurrenceId = ParseTime(value, prms, out _); break;
                case "EXDATE":
                    foreach (var part in value.Split(','))
                        if (ParseTime(part, prms, out _) is { } ex) cur.ExDates.Add(ex);
                    break;
            }
        }
        return events;
    }

    /// <summary>RFC 5545:以空白或 tab 開頭的行接在上一行後面。</summary>
    private static IEnumerable<string> Unfold(string ics)
    {
        var sb = new StringBuilder();
        foreach (var rawLine in ics.Replace("\r\n", "\n").Split('\n'))
        {
            if (rawLine.Length > 0 && (rawLine[0] == ' ' || rawLine[0] == '\t'))
            {
                sb.Append(rawLine, 1, rawLine.Length - 1);
                continue;
            }
            if (sb.Length > 0) yield return sb.ToString();
            sb.Clear().Append(rawLine.TrimEnd('\r'));
        }
        if (sb.Length > 0) yield return sb.ToString();
    }

    /// <summary>"DTSTART;TZID=Asia/Taipei:20261123T100000" → ("DTSTART", {TZID: Asia/Taipei}, "20261123T100000")。</summary>
    private static (string Name, Dictionary<string, string> Params, string Value) Split(string line)
    {
        int colon = -1;
        bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '"') quoted = !quoted;
            else if (line[i] == ':' && !quoted) { colon = i; break; }
        }
        if (colon < 0) return ("", [], "");
        var head = line[..colon].Split(';');
        var prms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in head.Skip(1))
        {
            int eq = p.IndexOf('=');
            if (eq > 0) prms[p[..eq]] = p[(eq + 1)..].Trim('"');
        }
        return (head[0].ToUpperInvariant(), prms, line[(colon + 1)..]);
    }

    private static string Unescape(string v) =>
        v.Replace("\\n", "\n").Replace("\\N", "\n").Replace("\\,", ",").Replace("\\;", ";").Replace("\\\\", "\\").Trim();

    /// <summary>轉成本地時間。"…Z" = UTC;有 TZID = 那個時區;都沒有 = 浮動(視為本地);VALUE=DATE = 全天。</summary>
    internal static DateTime? ParseTime(string value, IReadOnlyDictionary<string, string> prms, out bool allDay)
    {
        value = value.Trim();
        allDay = false;
        if (value.Length == 8 || (prms.TryGetValue("VALUE", out var v) && v.Equals("DATE", StringComparison.OrdinalIgnoreCase)))
        {
            allDay = true;
            return DateTime.TryParseExact(value[..Math.Min(8, value.Length)], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
        }
        bool utc = value.EndsWith('Z');
        if (!DateTime.TryParseExact(value.TrimEnd('Z'), "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)) return null;
        if (utc) return DateTime.SpecifyKind(t, DateTimeKind.Utc).ToLocalTime();
        if (prms.TryGetValue("TZID", out var tzid) && FindZone(tzid) is { } zone)
        {
            try { return TimeZoneInfo.ConvertTime(DateTime.SpecifyKind(t, DateTimeKind.Unspecified), zone, TimeZoneInfo.Local); }
            catch (ArgumentException) { return t; }   // 夏令時間切換的不存在時刻
        }
        return t;
    }

    private static TimeZoneInfo? FindZone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch { }
        // 有些日曆用 "(UTC+08:00) Taipei" 或 "/Asia/Taipei" 這種寫法
        var trimmed = id.Trim('/');
        try { return TimeZoneInfo.FindSystemTimeZoneById(trimmed); }
        catch { return null; }
    }

    // ── 重複規則 ─────────────────────────────────────────

    private static IEnumerable<DateTime> Occurrences(RawEvent e, DateTime to)
    {
        var start = e.Start!.Value;
        if (string.IsNullOrEmpty(e.RRule))
        {
            if (start < to) yield return start;
            yield break;
        }

        var rule = e.RRule.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0].ToUpperInvariant(), p => p[1], StringComparer.OrdinalIgnoreCase);
        string freq = rule.GetValueOrDefault("FREQ", "").ToUpperInvariant();
        int interval = int.TryParse(rule.GetValueOrDefault("INTERVAL"), out var iv) && iv > 0 ? iv : 1;
        int? count = int.TryParse(rule.GetValueOrDefault("COUNT"), out var c) && c > 0 ? c : null;
        DateTime? until = rule.TryGetValue("UNTIL", out var u) ? ParseTime(u, new Dictionary<string, string>(), out _) : null;
        if (until is { } un && u.Length == 8) until = un.AddDays(1).AddTicks(-1);   // 只有日期:包含那一天
        var byDay = rule.TryGetValue("BYDAY", out var bd)
            ? bd.Split(',').Select(ParseDay).Where(d => d is not null).Select(d => d!.Value).ToHashSet()
            : [];

        int produced = 0;
        for (int i = 0; i < MaxIterations; i++)
        {
            IEnumerable<DateTime> batch;
            switch (freq)
            {
                case "DAILY": batch = [start.AddDays((long)i * interval)]; break;
                case "WEEKLY":
                    if (byDay.Count == 0) { batch = [start.AddDays(7L * i * interval)]; break; }
                    var weekStart = start.Date.AddDays(-(((int)start.DayOfWeek + 6) % 7)).AddDays(7L * i * interval);   // 週一
                    batch = Enumerable.Range(0, 7).Select(d => weekStart.AddDays(d))
                        .Where(d => byDay.Contains(d.DayOfWeek))
                        .Select(d => d + start.TimeOfDay)
                        .Where(d => d >= start);
                    break;
                case "MONTHLY":
                    var m = start.AddMonths(i * interval);
                    batch = m.Day == start.Day ? [m] : [];   // 31 號在小月份跳過(和 Google 一樣)
                    break;
                case "YEARLY":
                    var y = start.AddYears(i * interval);
                    batch = y.Day == start.Day ? [y] : [];
                    break;
                default:
                    if (start < to) yield return start;
                    yield break;
            }
            foreach (var occ in batch)
            {
                if (until is { } limit && occ > limit) yield break;
                if (count is { } max && produced >= max) yield break;
                if (occ >= to) yield break;
                produced++;
                yield return occ;
            }
        }
    }

    private static DayOfWeek? ParseDay(string s)
    {
        var code = s.Trim().ToUpperInvariant();
        if (code.Length > 2) code = code[^2..];   // "1MO"、"-1FR" 只取星期(月重複的序數不支援)
        return code switch
        {
            "MO" => DayOfWeek.Monday,
            "TU" => DayOfWeek.Tuesday,
            "WE" => DayOfWeek.Wednesday,
            "TH" => DayOfWeek.Thursday,
            "FR" => DayOfWeek.Friday,
            "SA" => DayOfWeek.Saturday,
            "SU" => DayOfWeek.Sunday,
            _ => null,
        };
    }
}
