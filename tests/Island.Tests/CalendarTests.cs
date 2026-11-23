using Island.Modules.Calendar;

namespace Island.Tests;

public class CalendarTests
{
    private static string Cal(params string[] events) =>
        "BEGIN:VCALENDAR\r\nVERSION:2.0\r\n" + string.Join("", events) + "END:VCALENDAR\r\n";

    private static string Ev(string body) => "BEGIN:VEVENT\r\n" + body.Trim().Replace("\n", "\r\n") + "\r\nEND:VEVENT\r\n";

    private static readonly DateTime Mon = new(2026, 11, 23);   // 週一

    [Fact]
    public void Utc_And_TimeZone_AreConvertedToLocal()
    {
        var ics = Cal(
            Ev("UID:a\nSUMMARY:UTC 會議\nDTSTART:20261123T020000Z\nDTEND:20261123T030000Z"),
            Ev("UID:b\nSUMMARY:台北時間\nDTSTART;TZID=Asia/Taipei:20261123T140000\nDTEND;TZID=Asia/Taipei:20261123T150000"),
            Ev("UID:c\nSUMMARY:浮動\nDTSTART:20261123T090000\nDTEND:20261123T093000"));
        var list = IcsParser.Parse(ics, Mon, Mon.AddDays(1));
        Assert.Equal(3, list.Count);
        var utc = list.Single(e => e.Title == "UTC 會議");
        Assert.Equal(new DateTime(2026, 11, 23, 2, 0, 0, DateTimeKind.Utc).ToLocalTime(), utc.Start);
        var tw = list.Single(e => e.Title == "台北時間");
        var expectedTw = TimeZoneInfo.ConvertTime(new DateTime(2026, 11, 23, 14, 0, 0), TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei"), TimeZoneInfo.Local);
        Assert.Equal(expectedTw, tw.Start);
        Assert.Equal(TimeSpan.FromHours(1), tw.End - tw.Start);
        Assert.Equal(new DateTime(2026, 11, 23, 9, 0, 0), list.Single(e => e.Title == "浮動").Start);
    }

    [Fact]
    public void AllDay_FoldedLines_Escapes_Cancelled_Alarm()
    {
        var ics = Cal(
            Ev("UID:a\nSUMMARY:生日\nDTSTART;VALUE=DATE:20261123\nDTEND;VALUE=DATE:20261124"),
            Ev("UID:b\nSUMMARY:很長的標題\n  接下一行\nLOCATION:台北\\, 101\nDTSTART:20261123T100000\nDTEND:20261123T110000\nBEGIN:VALARM\nSUMMARY:不是行程\nEND:VALARM"),
            Ev("UID:c\nSUMMARY:取消了\nSTATUS:CANCELLED\nDTSTART:20261123T120000"));
        var list = IcsParser.Parse(ics, Mon, Mon.AddDays(1));
        Assert.Equal(2, list.Count);
        Assert.True(list[0].AllDay);
        Assert.Equal("生日", list[0].Title);
        Assert.Equal("很長的標題 接下一行", list[1].Title);
        Assert.Equal("台北, 101", list[1].Location);
    }

    [Fact]
    public void Weekly_ByDay_WithCount_And_ExDate()
    {
        // 從 11/23(一)開始,每週一三五,共 5 次,11/27(五)那次取消
        var ics = Cal(Ev("UID:w\nSUMMARY:健身\nDTSTART:20261123T190000\nDTEND:20261123T200000\nRRULE:FREQ=WEEKLY;BYDAY=MO,WE,FR;COUNT=5\nEXDATE:20261127T190000"));
        var list = IcsParser.Parse(ics, Mon, Mon.AddDays(30));
        Assert.Equal(new[] { 23, 25, 30, 2 }, list.Select(e => e.Start.Day));   // 23 25 (27 取消) 30 12/2 = COUNT 5 含被取消的那次
        Assert.All(list, e => Assert.Equal(19, e.Start.Hour));
    }

    [Fact]
    public void Daily_Interval_Until_AndWindow()
    {
        var ics = Cal(Ev("UID:d\nSUMMARY:吃藥\nDTSTART:20261101T080000\nDTEND:20261101T081000\nRRULE:FREQ=DAILY;INTERVAL=2;UNTIL=20261129"));
        var list = IcsParser.Parse(ics, Mon, Mon.AddDays(14));
        // 11/1 起每 2 天:…21, 23, 25, 27, 29(UNTIL 只有日期 = 包含那天)
        Assert.Equal(new[] { 23, 25, 27, 29 }, list.Select(e => e.Start.Day));
    }

    [Fact]
    public void Monthly_And_Yearly()
    {
        var ics = Cal(
            Ev("UID:m\nSUMMARY:繳房租\nDTSTART:20260105T090000\nRRULE:FREQ=MONTHLY"),
            Ev("UID:y\nSUMMARY:紀念日\nDTSTART;VALUE=DATE:20201201\nRRULE:FREQ=YEARLY"));
        var list = IcsParser.Parse(ics, Mon, Mon.AddDays(45));
        Assert.Contains(list, e => e.Title == "繳房租" && e.Start == new DateTime(2026, 12, 5, 9, 0, 0));
        Assert.Contains(list, e => e.Title == "繳房租" && e.Start == new DateTime(2027, 1, 5, 9, 0, 0));
        Assert.Contains(list, e => e.Title == "紀念日" && e.Start == new DateTime(2026, 12, 1) && e.AllDay);
    }

    [Fact]
    public void RecurrenceId_OverridesOneOccurrence()
    {
        var ics = Cal(
            Ev("UID:r\nSUMMARY:週會\nDTSTART:20261123T100000\nDTEND:20261123T110000\nRRULE:FREQ=WEEKLY"),
            Ev("UID:r\nRECURRENCE-ID:20261130T100000\nSUMMARY:週會(改到下午)\nDTSTART:20261130T150000\nDTEND:20261130T160000"));
        var list = IcsParser.Parse(ics, Mon, Mon.AddDays(14));
        Assert.Equal(new[] { "週會", "週會(改到下午)" }, list.Select(e => e.Title));
        Assert.Equal(15, list[1].Start.Hour);
    }

    [Fact]
    public void Garbage_DoesNotThrow()
    {
        Assert.Empty(IcsParser.Parse("", Mon, Mon.AddDays(1)));
        Assert.Empty(IcsParser.Parse("<html>not a calendar</html>", Mon, Mon.AddDays(1)));
        Assert.Empty(IcsParser.Parse(Cal(Ev("UID:x\nSUMMARY:壞時間\nDTSTART:tomorrow")), Mon, Mon.AddDays(1)));
    }

    [Fact]
    public void Reminder_Timing()
    {
        var now = new DateTime(2026, 11, 23, 13, 45, 0);
        var events = new List<CalEvent>
        {
            new(new DateTime(2026, 11, 23, 9, 0, 0), new DateTime(2026, 11, 23, 10, 0, 0), "早上", null, false),
            new(new DateTime(2026, 11, 23), new DateTime(2026, 11, 24), "全天", null, true),
            new(new DateTime(2026, 11, 23, 14, 0, 0), new DateTime(2026, 11, 23, 15, 0, 0), "下午會議", null, false),
            new(new DateTime(2026, 11, 23, 16, 0, 0), new DateTime(2026, 11, 23, 17, 0, 0), "晚一點", null, false),
        };
        var remind = TimeSpan.FromMinutes(10);
        Assert.Equal("下午會議", CalendarLogic.NextReminder(events, now, remind)?.Title);
        // 13:45 還沒到 13:50 的提醒時刻 → 醒來時間 13:50
        Assert.Equal(new DateTime(2026, 11, 23, 13, 50, 0), CalendarLogic.NextWake(events, now, remind));
        // 13:55 已經在提醒中 → 下一次醒來是 14:00(行程開始,結束提醒)
        Assert.Equal(new DateTime(2026, 11, 23, 14, 0, 0), CalendarLogic.NextWake(events, now.AddMinutes(10), remind));
        Assert.Equal("5 分鐘後", CalendarLogic.Until(new DateTime(2026, 11, 23, 14, 0, 0), now.AddMinutes(10)));
        Assert.Equal("1 分鐘後", CalendarLogic.Until(new DateTime(2026, 11, 23, 14, 0, 0), new DateTime(2026, 11, 23, 13, 59, 30)));
        Assert.Equal("即將開始", CalendarLogic.Until(now, now));
        Assert.Equal("全天", CalendarLogic.TimeRange(events[1]));
        Assert.Equal("14:00–15:00", CalendarLogic.TimeRange(events[2]));
    }
}
