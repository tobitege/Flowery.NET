using System;
using Flowery.Localization;

#nullable enable

namespace Flowery.Helpers
{
    /// <summary>
    /// Format options for duration display.
    /// </summary>
    public enum DurationFormat
    {
        /// <summary>Numeric format: 01:23:45</summary>
        Numeric,
        /// <summary>Short labels: 1h 23m 45s</summary>
        ShortLabels,
        /// <summary>Long labels: 1 hour 23 minutes 45 seconds</summary>
        LongLabels
    }

    /// <summary>
    /// Clock display format.
    /// </summary>
    public enum ClockFormat
    {
        /// <summary>24-hour format (00:00 - 23:59)</summary>
        TwentyFourHour,
        /// <summary>12-hour format (12:00 AM - 11:59 PM)</summary>
        TwelveHour
    }

    /// <summary>
    /// Time unit for labeling.
    /// </summary>
    public enum TimeUnit
    {
        Hours,
        Minutes,
        Seconds
    }

    /// <summary>
    /// Label format for time units.
    /// </summary>
    public enum TimeLabelFormat
    {
        /// <summary>No label</summary>
        None,
        /// <summary>Short label (h, m, s)</summary>
        Short,
        /// <summary>Long label (hours, minutes, seconds)</summary>
        Long
    }

    /// <summary>
    /// Provides time formatting and localization utilities for clock-related controls.
    /// </summary>
    public static class FloweryTimeHelpers
    {
        /// <summary>
        /// Formats a TimeSpan as a duration string.
        /// </summary>
        /// <param name="duration">The duration to format.</param>
        /// <param name="format">The format to use.</param>
        /// <param name="showSeconds">Whether to include seconds.</param>
        /// <param name="showHours">Whether to include hours (even if 0).</param>
        /// <returns>Formatted duration string.</returns>
        public static string FormatDuration(TimeSpan duration, DurationFormat format, bool showSeconds = true, bool showHours = true)
        {
            // Ensure positive duration
            var totalSeconds = Math.Abs(duration.TotalSeconds);
            var hours = (int)(totalSeconds / 3600);
            var minutes = (int)((totalSeconds % 3600) / 60);
            var seconds = (int)(totalSeconds % 60);

            return format switch
            {
                DurationFormat.Numeric => FormatDurationNumeric(hours, minutes, seconds, showSeconds, showHours),
                DurationFormat.ShortLabels => FormatDurationShortLabels(hours, minutes, seconds, showSeconds, showHours),
                DurationFormat.LongLabels => FormatDurationLongLabels(hours, minutes, seconds, showSeconds, showHours),
                _ => FormatDurationNumeric(hours, minutes, seconds, showSeconds, showHours)
            };
        }

        private static string FormatDurationNumeric(int hours, int minutes, int seconds, bool showSeconds, bool showHours)
        {
            if (showHours && showSeconds)
                return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
            if (showHours)
                return $"{hours:D2}:{minutes:D2}";
            if (showSeconds)
                return $"{minutes:D2}:{seconds:D2}";
            return $"{minutes:D2}";
        }

        private static string FormatDurationShortLabels(int hours, int minutes, int seconds, bool showSeconds, bool showHours)
        {
            var parts = new System.Collections.Generic.List<string>();

            if (showHours && hours > 0)
                parts.Add($"{hours}{GetTimeUnitLabel(TimeUnit.Hours, hours, TimeLabelFormat.Short)}");

            parts.Add($"{minutes}{GetTimeUnitLabel(TimeUnit.Minutes, minutes, TimeLabelFormat.Short)}");

            if (showSeconds)
                parts.Add($"{seconds}{GetTimeUnitLabel(TimeUnit.Seconds, seconds, TimeLabelFormat.Short)}");

            var separator = IsRtlCulture() ? " " : " ";
            return string.Join(separator, parts);
        }

        private static string FormatDurationLongLabels(int hours, int minutes, int seconds, bool showSeconds, bool showHours)
        {
            var parts = new System.Collections.Generic.List<string>();

            if (showHours && hours > 0)
                parts.Add($"{hours} {GetTimeUnitLabel(TimeUnit.Hours, hours, TimeLabelFormat.Long)}");

            parts.Add($"{minutes} {GetTimeUnitLabel(TimeUnit.Minutes, minutes, TimeLabelFormat.Long)}");

            if (showSeconds)
                parts.Add($"{seconds} {GetTimeUnitLabel(TimeUnit.Seconds, seconds, TimeLabelFormat.Long)}");

            return string.Join(" ", parts);
        }

        /// <summary>
        /// Gets the localized label for a time unit with proper pluralization.
        /// </summary>
        /// <param name="unit">The time unit.</param>
        /// <param name="value">The numeric value (used for pluralization).</param>
        /// <param name="format">The label format (Short or Long).</param>
        /// <returns>Localized label string.</returns>
        public static string GetTimeUnitLabel(TimeUnit unit, int value, TimeLabelFormat format)
        {
            if (format == TimeLabelFormat.None)
                return string.Empty;

            var isPlural = value != 1;
            var suffix = format == TimeLabelFormat.Short ? "_Short" : (isPlural ? "_Plural" : "_Singular");

            var key = unit switch
            {
                TimeUnit.Hours => $"Clock_Hours{suffix}",
                TimeUnit.Minutes => $"Clock_Minutes{suffix}",
                TimeUnit.Seconds => $"Clock_Seconds{suffix}",
                _ => string.Empty
            };

            // Get default fallback based on unit and format
            var fallback = GetTimeUnitFallback(unit, format, isPlural);
            return FloweryLocalization.GetStringInternal(key, fallback);
        }

        private static string GetTimeUnitFallback(TimeUnit unit, TimeLabelFormat format, bool isPlural)
        {
            return (unit, format, isPlural) switch
            {
                (TimeUnit.Hours, TimeLabelFormat.Short, _) => "h",
                (TimeUnit.Minutes, TimeLabelFormat.Short, _) => "m",
                (TimeUnit.Seconds, TimeLabelFormat.Short, _) => "s",
                (TimeUnit.Hours, TimeLabelFormat.Long, false) => "hour",
                (TimeUnit.Hours, TimeLabelFormat.Long, true) => "hours",
                (TimeUnit.Minutes, TimeLabelFormat.Long, false) => "minute",
                (TimeUnit.Minutes, TimeLabelFormat.Long, true) => "minutes",
                (TimeUnit.Seconds, TimeLabelFormat.Long, false) => "second",
                (TimeUnit.Seconds, TimeLabelFormat.Long, true) => "seconds",
                _ => string.Empty
            };
        }

        /// <summary>
        /// Formats time components as a clock display string.
        /// </summary>
        /// <param name="hours">Hours (0-23 for 24h, 1-12 for 12h).</param>
        /// <param name="minutes">Minutes (0-59).</param>
        /// <param name="seconds">Seconds (0-59).</param>
        /// <param name="format">Clock format (12h or 24h).</param>
        /// <param name="showSeconds">Whether to include seconds.</param>
        /// <param name="separator">Separator between components.</param>
        /// <returns>Formatted clock string.</returns>
        public static string FormatClockTime(int hours, int minutes, int seconds, ClockFormat format, bool showSeconds = true, string separator = ":")
        {
            var displayHours = hours;
            var amPm = string.Empty;

            if (format == ClockFormat.TwelveHour)
            {
                amPm = hours >= 12 ? GetPmLabel() : GetAmLabel();
                displayHours = hours % 12;
                if (displayHours == 0) displayHours = 12;
            }

            var timeStr = showSeconds
                ? $"{displayHours:D2}{separator}{minutes:D2}{separator}{seconds:D2}"
                : $"{displayHours:D2}{separator}{minutes:D2}";

            if (format == ClockFormat.TwelveHour && !string.IsNullOrEmpty(amPm))
            {
                timeStr = IsRtlCulture() ? $"{amPm} {timeStr}" : $"{timeStr} {amPm}";
            }

            return timeStr;
        }

        /// <summary>
        /// Formats a DateTime as a clock display string.
        /// </summary>
        /// <param name="time">The time to format.</param>
        /// <param name="format">Clock format (12h or 24h).</param>
        /// <param name="showSeconds">Whether to include seconds.</param>
        /// <param name="separator">Separator between components.</param>
        /// <returns>Formatted clock string.</returns>
        public static string FormatClockTime(DateTime time, ClockFormat format, bool showSeconds = true, string separator = ":")
        {
            return FormatClockTime(time.Hour, time.Minute, time.Second, format, showSeconds, separator);
        }

        /// <summary>
        /// Gets the localized AM label.
        /// </summary>
        public static string GetAmLabel() => FloweryLocalization.GetStringInternal("Clock_AM", "AM");

        /// <summary>
        /// Gets the localized PM label.
        /// </summary>
        public static string GetPmLabel() => FloweryLocalization.GetStringInternal("Clock_PM", "PM");

        /// <summary>
        /// Gets whether the current culture uses right-to-left text direction.
        /// </summary>
        public static bool IsRtlCulture() => FloweryLocalization.Instance.IsRtl;

        /// <summary>
        /// Calculates the next occurrence of a daily time.
        /// </summary>
        /// <param name="time">The time of day (e.g., 07:30:00).</param>
        /// <returns>The next occurrence of that time.</returns>
        public static DateTimeOffset GetNextOccurrence(TimeSpan time)
        {
            var now = DateTimeOffset.Now;
            var today = now.Date.Add(time);

            if (now.TimeOfDay < time)
            {
                return today;
            }
            else
            {
                return today.AddDays(1);
            }
        }

        /// <summary>
        /// Calculates the next occurrence of a time on specific days of the week.
        /// </summary>
        /// <param name="time">The time of day.</param>
        /// <param name="allowedDays">Days of the week when this time is valid.</param>
        /// <returns>The next occurrence, or null if no valid days provided.</returns>
        public static DateTimeOffset? GetNextOccurrence(TimeSpan time, DayOfWeek[] allowedDays)
        {
            if (allowedDays == null || allowedDays.Length == 0)
                return null;

            var now = DateTimeOffset.Now;
            var allowedSet = new System.Collections.Generic.HashSet<DayOfWeek>(allowedDays);

            // Check up to 7 days ahead
            for (int i = 0; i < 7; i++)
            {
                var candidate = now.Date.AddDays(i).Add(time);

                // Skip if it's today but the time has passed
                if (i == 0 && now.TimeOfDay >= time)
                    continue;

                if (allowedSet.Contains(candidate.DayOfWeek))
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// Parses a time string in common formats (HH:MM, HH:MM:SS, H:MM AM/PM).
        /// </summary>
        /// <param name="input">The time string to parse.</param>
        /// <param name="result">The parsed TimeSpan if successful.</param>
        /// <returns>True if parsing succeeded.</returns>
        public static bool TryParseTime(string input, out TimeSpan result)
        {
            result = TimeSpan.Zero;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            input = input.Trim();

            // Check for AM/PM suffix
            var isAm = false;
            var isPm = false;
            var upperInput = input.ToUpperInvariant();

            if (upperInput.EndsWith("AM") || upperInput.EndsWith(GetAmLabel().ToUpperInvariant()))
            {
                isAm = true;
                input = input.Substring(0, input.Length - 2).Trim();
            }
            else if (upperInput.EndsWith("PM") || upperInput.EndsWith(GetPmLabel().ToUpperInvariant()))
            {
                isPm = true;
                input = input.Substring(0, input.Length - 2).Trim();
            }

            // Try to parse as TimeSpan
            if (TimeSpan.TryParse(input, out var ts))
            {
                if (isAm && ts.Hours == 12)
                {
                    ts = ts.Subtract(TimeSpan.FromHours(12));
                }
                else if (isPm && ts.Hours < 12)
                {
                    ts = ts.Add(TimeSpan.FromHours(12));
                }

                result = ts;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Parses a duration string (e.g., "1h 30m", "1:30:00", "90m").
        /// </summary>
        /// <param name="input">The duration string to parse.</param>
        /// <param name="result">The parsed TimeSpan if successful.</param>
        /// <returns>True if parsing succeeded.</returns>
        public static bool TryParseDuration(string input, out TimeSpan result)
        {
            result = TimeSpan.Zero;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            input = input.Trim().ToLowerInvariant();

            // Try standard TimeSpan.Parse first
            if (TimeSpan.TryParse(input, out result))
                return true;

            // Try parsing labeled format (1h 30m 45s)
            var hours = 0;
            var minutes = 0;
            var seconds = 0;
            var currentNumber = string.Empty;
            var foundAny = false;

            foreach (var ch in input)
            {
                if (char.IsDigit(ch))
                {
                    currentNumber += ch;
                }
                else if (ch == 'h' && currentNumber.Length > 0)
                {
                    hours = int.Parse(currentNumber);
                    currentNumber = string.Empty;
                    foundAny = true;
                }
                else if (ch == 'm' && currentNumber.Length > 0)
                {
                    minutes = int.Parse(currentNumber);
                    currentNumber = string.Empty;
                    foundAny = true;
                }
                else if (ch == 's' && currentNumber.Length > 0)
                {
                    seconds = int.Parse(currentNumber);
                    currentNumber = string.Empty;
                    foundAny = true;
                }
            }

            if (foundAny)
            {
                result = new TimeSpan(hours, minutes, seconds);
                return true;
            }

            return false;
        }

        #region Data Exchange Helpers

        /// <summary>
        /// Formats a TimeSpan to a precise string including milliseconds.
        /// </summary>
        /// <param name="duration">The duration to format.</param>
        /// <param name="includeMilliseconds">Whether to include milliseconds.</param>
        /// <returns>Formatted string like "01:23:45.678" or "01:23:45".</returns>
        public static string FormatDurationPrecise(TimeSpan duration, bool includeMilliseconds = true)
        {
            var negative = duration < TimeSpan.Zero;
            duration = duration.Duration(); // Absolute value

            if (includeMilliseconds)
            {
                var result = $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}.{duration.Milliseconds:D3}";
                return negative ? $"-{result}" : result;
            }
            else
            {
                var result = $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
                return negative ? $"-{result}" : result;
            }
        }

        /// <summary>
        /// Formats a TimeSpan as milliseconds (total).
        /// Useful for data exchange where a single number is preferred.
        /// </summary>
        /// <param name="duration">The duration.</param>
        /// <returns>Total milliseconds as a long.</returns>
        public static long ToMilliseconds(TimeSpan duration) => (long)duration.TotalMilliseconds;

        /// <summary>
        /// Creates a TimeSpan from milliseconds.
        /// </summary>
        /// <param name="milliseconds">Total milliseconds.</param>
        /// <returns>A TimeSpan.</returns>
        public static TimeSpan FromMilliseconds(long milliseconds) => TimeSpan.FromMilliseconds(milliseconds);

        /// <summary>
        /// Formats a TimeSpan as seconds with decimal (e.g., 125.678).
        /// Useful for data exchange.
        /// </summary>
        /// <param name="duration">The duration.</param>
        /// <returns>Total seconds as a double.</returns>
        public static double ToSeconds(TimeSpan duration) => duration.TotalSeconds;

        /// <summary>
        /// Creates a TimeSpan from seconds (with decimal for sub-seconds).
        /// </summary>
        /// <param name="seconds">Total seconds.</param>
        /// <returns>A TimeSpan.</returns>
        public static TimeSpan FromSeconds(double seconds) => TimeSpan.FromSeconds(seconds);

        /// <summary>
        /// Parses a precise duration string with optional milliseconds.
        /// Supports: "HH:MM:SS.mmm", "HH:MM:SS", "MM:SS.mmm", "MM:SS", "SS.mmm", seconds as number.
        /// </summary>
        /// <param name="input">The string to parse.</param>
        /// <param name="result">The parsed TimeSpan if successful.</param>
        /// <returns>True if parsing succeeded.</returns>
        public static bool TryParseDurationPrecise(string input, out TimeSpan result)
        {
            result = TimeSpan.Zero;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            input = input.Trim();

            // Handle negative durations
            var negative = input.StartsWith("-");
            if (negative)
                input = input.Substring(1);

            // Try standard TimeSpan.Parse first (handles "HH:MM:SS.mmm" format)
            if (TimeSpan.TryParse(input, out result))
            {
                if (negative) result = result.Negate();
                return true;
            }

            // Try parsing as pure seconds with decimal (e.g., "125.678")
            if (double.TryParse(input, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var seconds))
            {
                result = TimeSpan.FromSeconds(seconds);
                if (negative) result = result.Negate();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Formats a DateTimeOffset to ISO 8601 format for data exchange.
        /// </summary>
        /// <param name="timestamp">The timestamp.</param>
        /// <returns>ISO 8601 formatted string.</returns>
        public static string ToIso8601(DateTimeOffset timestamp) => timestamp.ToString("o");

        /// <summary>
        /// Parses an ISO 8601 timestamp string.
        /// </summary>
        /// <param name="input">The ISO 8601 string.</param>
        /// <param name="result">The parsed DateTimeOffset if successful.</param>
        /// <returns>True if parsing succeeded.</returns>
        public static bool TryParseIso8601(string input, out DateTimeOffset result)
        {
            return DateTimeOffset.TryParse(input,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out result);
        }

        /// <summary>
        /// Formats a TimeSpan to ISO 8601 duration format (e.g., "PT1H30M45.5S").
        /// </summary>
        /// <param name="duration">The duration.</param>
        /// <returns>ISO 8601 duration string.</returns>
        public static string ToIso8601Duration(TimeSpan duration)
        {
            var negative = duration < TimeSpan.Zero;
            duration = duration.Duration();

            var parts = new System.Collections.Generic.List<string>();
            if (duration.Hours > 0)
                parts.Add($"{duration.Hours}H");
            if (duration.Minutes > 0)
                parts.Add($"{duration.Minutes}M");
            if (duration.Seconds > 0 || duration.Milliseconds > 0 || parts.Count == 0)
            {
                var seconds = duration.Seconds + (duration.Milliseconds / 1000.0);
                parts.Add($"{seconds:0.###}S");
            }

            var result = $"PT{string.Join("", parts)}";
            return negative ? $"-{result}" : result;
        }

        /// <summary>
        /// Parses an ISO 8601 duration string (e.g., "PT1H30M45S").
        /// </summary>
        /// <param name="input">The ISO 8601 duration string.</param>
        /// <param name="result">The parsed TimeSpan if successful.</param>
        /// <returns>True if parsing succeeded.</returns>
        public static bool TryParseIso8601Duration(string input, out TimeSpan result)
        {
            result = TimeSpan.Zero;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            input = input.Trim().ToUpperInvariant();

            var negative = input.StartsWith("-");
            if (negative)
                input = input.Substring(1);

            if (!input.StartsWith("PT"))
                return false;

            input = input.Substring(2); // Remove "PT"

            double hours = 0, minutes = 0, seconds = 0;
            var currentNumber = string.Empty;

            foreach (var ch in input)
            {
                if (char.IsDigit(ch) || ch == '.')
                {
                    currentNumber += ch;
                }
                else if (ch == 'H' && currentNumber.Length > 0)
                {
                    hours = double.Parse(currentNumber, System.Globalization.CultureInfo.InvariantCulture);
                    currentNumber = string.Empty;
                }
                else if (ch == 'M' && currentNumber.Length > 0)
                {
                    minutes = double.Parse(currentNumber, System.Globalization.CultureInfo.InvariantCulture);
                    currentNumber = string.Empty;
                }
                else if (ch == 'S' && currentNumber.Length > 0)
                {
                    seconds = double.Parse(currentNumber, System.Globalization.CultureInfo.InvariantCulture);
                    currentNumber = string.Empty;
                }
            }

            result = TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);
            if (negative) result = result.Negate();
            return true;
        }

        #endregion
    }
}
