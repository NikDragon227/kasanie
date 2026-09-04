namespace Kasanie.Api.Infrastructure;

/// <summary>Простые брендированные шаблоны транзакционных писем (HTML + текстовый fallback).</summary>
public static class EmailTemplates
{
    private const string Brand = "#086cf5";

    private static string Encode(string value) => System.Net.WebUtility.HtmlEncode(value);

    private static (string Html, string Text) Action(string preheader, string heading, string intro, string buttonLabel, string url, string note)
    {
        var h = Encode(heading);
        var i = Encode(intro);
        var b = Encode(buttonLabel);
        var n = Encode(note);
        var u = Encode(url);
        var pre = Encode(preheader);

        var html = $$"""
<!doctype html>
<html lang="ru">
<head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
<body style="margin:0;padding:0;background:#eef1f5;">
<span style="display:none!important;visibility:hidden;opacity:0;height:0;width:0;overflow:hidden;mso-hide:all;">{{pre}}</span>
<table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#eef1f5;">
<tr><td align="center" style="padding:32px 16px;">
<table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:480px;background:#ffffff;border-radius:14px;overflow:hidden;font-family:-apple-system,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;">
<tr><td style="background:#0b1220;padding:22px 32px;">
<span style="color:#ffffff;font-size:13px;font-weight:800;letter-spacing:.32em;">КАСАНИЕ</span>
</td></tr>
<tr><td style="padding:28px 32px 4px;">
<h1 style="margin:0 0 12px;font-size:20px;line-height:1.3;color:#0b1220;">{{h}}</h1>
<p style="margin:0 0 24px;font-size:15px;line-height:1.6;color:#4b5568;">{{i}}</p>
<table role="presentation" cellpadding="0" cellspacing="0"><tr><td style="border-radius:10px;background:{{Brand}};">
<a href="{{u}}" style="display:inline-block;padding:13px 26px;font-size:15px;font-weight:700;color:#ffffff;text-decoration:none;">{{b}}</a>
</td></tr></table>
<p style="margin:22px 0 0;font-size:13px;line-height:1.6;color:#8a95a5;">Если кнопка не открывается, скопируйте ссылку в браузер:<br>
<a href="{{u}}" style="color:{{Brand}};word-break:break-all;">{{u}}</a></p>
</td></tr>
<tr><td style="padding:22px 32px 28px;">
<hr style="border:none;border-top:1px solid #e9edf2;margin:0 0 14px;">
<p style="margin:0;font-size:12px;line-height:1.6;color:#9aa5b4;">{{n}}<br>© Касание · <a href="https://prokasanie.ru" style="color:#9aa5b4;">prokasanie.ru</a></p>
</td></tr>
</table>
</td></tr>
</table>
</body>
</html>
""";

        var text = $"{heading}\n\n{intro}\n\n{url}\n\n{note}\n\n— Касание · prokasanie.ru";
        return (html, text);
    }

    public static (string Subject, string Html, string Text) PasswordReset(string url)
    {
        var (html, text) = Action(
            "Ссылка для смены пароля в Касании",
            "Смена пароля",
            "Вы запросили восстановление доступа к аккаунту Касание. Нажмите кнопку, чтобы задать новый пароль — ссылка действует ограниченное время.",
            "Задать новый пароль", url,
            "Если вы не запрашивали смену пароля, просто проигнорируйте это письмо — пароль останется прежним.");
        return ("Восстановление пароля — Касание", html, text);
    }

    public static (string Subject, string Html, string Text) ConfirmEmail(string url)
    {
        var (html, text) = Action(
            "Подтвердите адрес почты в Касании",
            "Подтвердите email",
            "Спасибо за регистрацию в Касании. Остался один шаг — подтвердите, что это ваш адрес.",
            "Подтвердить email", url,
            "Если вы не создавали аккаунт, просто проигнорируйте это письмо.");
        return ("Подтвердите email — Касание", html, text);
    }

    public static (string Subject, string Html, string Text) PublicActivityCancelled(string activityTitle, string whenText, string url)
    {
        var (html, text) = Action(
            $"«{activityTitle}» отменена организатором",
            "Активность отменена",
            $"Организатор отменил «{activityTitle}» ({whenText}). Ваше место больше не понадобится — приносим извинения за неудобство.",
            "Найти другое событие", url,
            "Если вы не ожидали этого письма, просто проигнорируйте его.");
        return ("Активность отменена — Касание", html, text);
    }
}
