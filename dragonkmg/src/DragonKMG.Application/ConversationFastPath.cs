using System.Text.RegularExpressions;

namespace DragonKMG.Application;

public static class ConversationFastPath
{
    public const string SourceMarker = "[Response source: local greeting; no provider call]";
    public static string? Reply(string input)
    {
        if (input.Length > 64) return null;
        var text = Regex.Replace(input.Trim().ToLowerInvariant(), @"\s+", " ").Trim('!', '.', '؟', '?', '،', ' ');
        return text switch
        {
            "مرحبا" or "مرحباً" or "أهلا" or "اهلا" or "أهلاً" or "اهلاً" or "السلام عليكم" or "مرحبا اختبار" or "مرحبا اختبر" => "[رد محلي سريع — بدون استدعاء الموديل] أهلًا! اكتب مهمتك لنبدأ.",
            "hi" or "hello" or "hey" or "hello test" => "[Local instant reply — no model call] Hello! Send your task when ready.",
            _ => null
        };
    }
}
