using System;
using System.Globalization;

namespace net.timka.mcache.Utils
{
	public static class TimeUtils
	{
		public static DateTime ParseYYYYMMDDHHMM(string dateString, string separator)
		{
			string format = string.Format("yyyy{0}MM{0}dd{0}HH{0}mm", separator);
			DateTime dt = DateTime.ParseExact(dateString, format, CultureInfo.InvariantCulture);
			return dt;
		}

		public static string FormatYYYYMMDDHHMM(DateTime dt, string separator)
		{
			return string.Format("{1:0000}{0}{2:00}{0}{3:00}{0}{4:00}{0}{5:00}", separator, dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute);
		}

		public static DateTime RoundUpTo(DateTime dt, byte minutes)
		{
			int min = ((dt.Minute + minutes) / minutes) * minutes;
			return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour + (min / 60), min % 60, 0);
		}

		public static DateTime RoundDownTo(DateTime dt, byte minutes)
		{
			return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, (dt.Minute / minutes) * minutes, 0);
		}

		public static double Elapsed(DateTime startTime)
		{
			return (DateTime.Now - startTime).TotalMilliseconds / 1000;
		}

		public static bool IsSameDate(DateTime date1, DateTime date2)
		{
			return (date1.Year == date2.Year &&
					date1.Month == date2.Month &&
					date1.Day == date2.Day);
		}
	}
}