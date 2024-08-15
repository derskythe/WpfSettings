using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using NLog;

namespace WpfSettings
{
    public class Utilities
    {
        // ReSharper disable FieldCanBeMadeReadOnly.Local
        // ReSharper disable InconsistentNaming
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        // ReSharper restore InconsistentNaming
        // ReSharper restore FieldCanBeMadeReadOnly.Local

        //private static TimeZoneInfo _Tz = TimeZoneInfo.FindSystemTimeZoneById("Azerbaijan Standard Time");
        private static readonly Regex _AlphaNumber = new(@"[^A-Z0-9.$ \;\.\-]");

        [DllImport("user32.dll")]
        static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [DllImport("kernel32.dll", EntryPoint = "GetSystemTime", SetLastError = true)]
        public extern static void Win32GetSystemTime(ref SystemTime sysTime);

        [DllImport("kernel32.dll", EntryPoint = "SetSystemTime", SetLastError = true)]
        public extern static bool Win32SetSystemTime(ref SystemTime sysTime);

        private static readonly CultureInfo _Info = new(0x042C);

        public static string FormatDate(DateTime value)
        {
            return value.ToString("HH:mm:ss dd.MM.yyyy");
        }

        public static bool AlphaNumber(string value)
        {
            if (string.IsNullOrEmpty(value) || _AlphaNumber.IsMatch(value))
            {
                return false;
            }

            return true;
        }

        public static BitmapImage ImageFromBase64(string imageString)
        {
            BitmapImage image = null;
            try
            {
                var imagesBytes = Convert.FromBase64String(imageString);
                image = new BitmapImage();
                image.BeginInit();
                image.StreamSource = new MemoryStream(imagesBytes);
                image.EndInit();
            }
            catch (Exception ex)
            {
                Log.Error(ex, ex.Message);
            }

            return image;
        }

        public static uint GetLastInputTime()
        {
            uint idleTime = 0;
            var lastInputInfo = new LASTINPUTINFO();
            lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);
            lastInputInfo.dwTime = 0;

            var envTicks = (uint)Environment.TickCount;

            if (GetLastInputInfo(ref lastInputInfo))
            {
                uint lastInputTick = lastInputInfo.dwTime;
                idleTime = envTicks - lastInputTick;
            }

            return ((idleTime > 0) ? (idleTime / 1000) : 0);
        }

        public static void UpdateSystemTime(DateTime newDate)
        {
            newDate = newDate.ToUniversalTime();
            var updatedTime = new SystemTime
            {
                Year = (ushort)newDate.Year,
                Month = (ushort)newDate.Month,
                Day = (ushort)newDate.Day,
                Hour = (ushort)newDate.Hour,
                Minute = (ushort)newDate.Minute,
                Second = (ushort)newDate.Second
            };

            //Log.Info(newDate.ToLongTimeString());

            if (!Win32SetSystemTime(ref updatedTime))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        public static string FirstUpper(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            Log.Debug(value);

            var values = value.Trim().Split(' ');
            var buffer = new StringBuilder();
            foreach (var s in values)
            {
                var tmpVal = s.Trim();
                if (!string.IsNullOrEmpty(tmpVal))
                {
                    buffer.Append(tmpVal.Substring(0, 1).ToUpper(_Info) + tmpVal.Substring(1).ToLower(_Info)).Append(" ");
                }
            }

            return buffer.ToString().Trim();
        }

        public static decimal RoundFloor(decimal value, decimal significance)
        {
            try
            {
                if ((value % significance) != 0)
                {
                    return ((int)(value / significance) * significance);
                }

                return Convert.ToDecimal(value);
            }
            catch (Exception e)
            {
                Log.Error(e, e.Message);
            }

            return 0;
        }

        public static decimal GetAmountFromString(string amount)
        {
            try
            {
                return Convert.ToDecimal(amount, CultureInfo.InvariantCulture);
            }
            catch (Exception e)
            {
                Log.Error(e, e.Message);
            }

            return 0;

        }
    }

    // ReSharper disable FieldCanBeMadeReadOnly.Local
    // ReSharper disable MemberCanBePrivate.Local
    // ReSharper disable InconsistentNaming
    [StructLayout(LayoutKind.Sequential)]
    struct LASTINPUTINFO
    {
        public static readonly int SizeOf = Marshal.SizeOf(typeof(LASTINPUTINFO));

        [MarshalAs(UnmanagedType.U4)]
        public uint cbSize;
        [MarshalAs(UnmanagedType.U4)]
        public uint dwTime;
    }

    public struct SystemTime
    {
        public ushort Year;
        public ushort Month;
        public ushort DayOfWeek;
        public ushort Day;
        public ushort Hour;
        public ushort Minute;
        public ushort Second;
        public ushort Millisecond;
    };
    // ReSharper restore MemberCanBePrivate.Local
    // ReSharper restore FieldCanBeMadeReadOnly.Local
    // ReSharper restore InconsistentNaming
}
