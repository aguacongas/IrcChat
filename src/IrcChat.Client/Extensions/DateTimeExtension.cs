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
        if (dateTime.Year == now.Year && dateTime.Month == now.Month && dateTime.Day == now.Day - 1)
        {
            return $"Hier {dateTime:HH:mm}";
        }
        return dateTime.ToShortDateString();
    }
}
