using System;

namespace KachiGun
{
    /// <summary>큰 수 표기: 1,000 = 1k · 10^6 = 1m · 10^9 = 1b · 10^12 = 1t · 그 위는 10^15 = 1aa, 1ab … (세 자리마다 한 단계)</summary>
    public static class Num
    {
        public static string F(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "∞";
            if (v < 0) return "-" + F(-v);
            if (v < 1000) return Math.Floor(v).ToString("0");
            int k = (int)Math.Floor(Math.Log10(v) / 3);
            double m = v / Math.Pow(10, k * 3);
            if (m >= 1000) { k++; m /= 1000; }     // 로그 계산 오차 보정(내림 표기라 999,999 = 999k)
            string unit = Unit(k);
            string s = m < 10 ? Trunc(m, 2).ToString("0.00") : m < 100 ? Trunc(m, 1).ToString("0.0") : Math.Floor(m).ToString("0");
            if (s.Contains(".")) s = s.TrimEnd('0').TrimEnd('.');
            return s + unit;
        }

        static double Trunc(double v, int d) { double p = Math.Pow(10, d); return Math.Floor(v * p) / p; }

        static readonly string[] Named = { "", "k", "m", "b", "t" };

        // 1 → k · 2 → m · 3 → b · 4 → t · 5 → aa · 6 → ab … 30 → az · 31 → ba …
        static string Unit(int k)
        {
            if (k < Named.Length) return Named[k];
            int n = k - Named.Length;
            return $"{(char)('a' + n / 26 % 26)}{(char)('a' + n % 26)}";
        }

        /// <summary>다이아처럼 소수가 생기는 값: 정수 부분만</summary>
        public static string Gem(double v) => F(Math.Floor(v + 1e-6));

        public static string Time(double sec)
        {
            if (sec < 0) sec = 0;
            int s = (int)Math.Ceiling(sec);
            return s >= 60 ? $"{s / 60}:{s % 60:00}" : L.F("{0}초", s);
        }
    }
}
