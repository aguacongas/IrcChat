namespace IrcChat.Client.Extensions;

public static class DateTimeExtension
{
    public static string ToMessageTimeString(this DateTime dateTime)
    {
        var now = DateTime.Now;
        if (dateTime.Year == now.Year && dateTime.Month == now.Month && dateTime.Day == now.Day)
        {
            return dateTime.ToString("HH:mm");
        }

        var yesterday = now.AddDays(-1);
        if (dateTime.Year == yesterday.Year && dateTime.Month == yesterday.Month && dateTime.Day == yesterday.Day)
        {
            return $"Hier {dateTime:HH:mm}";
        }

        return dateTime.ToShortDateString();
    }
}