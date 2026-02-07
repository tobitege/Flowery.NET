using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

#nullable enable

namespace Flowery.Helpers
{
    /// <summary>
    /// Repeat pattern for alarms.
    /// </summary>
    [Flags]
    public enum AlarmRepeat
    {
        /// <summary>No repeat - one-time alarm</summary>
        None = 0,
        /// <summary>Repeat on Sunday</summary>
        Sunday = 1,
        /// <summary>Repeat on Monday</summary>
        Monday = 2,
        /// <summary>Repeat on Tuesday</summary>
        Tuesday = 4,
        /// <summary>Repeat on Wednesday</summary>
        Wednesday = 8,
        /// <summary>Repeat on Thursday</summary>
        Thursday = 16,
        /// <summary>Repeat on Friday</summary>
        Friday = 32,
        /// <summary>Repeat on Saturday</summary>
        Saturday = 64,
        /// <summary>Repeat every day</summary>
        Daily = Sunday | Monday | Tuesday | Wednesday | Thursday | Friday | Saturday,
        /// <summary>Repeat on weekdays (Mon-Fri)</summary>
        Weekdays = Monday | Tuesday | Wednesday | Thursday | Friday,
        /// <summary>Repeat on weekends (Sat-Sun)</summary>
        Weekends = Saturday | Sunday
    }

    /// <summary>
    /// State of an alarm.
    /// </summary>
    public enum AlarmState
    {
        /// <summary>Alarm is scheduled and waiting</summary>
        Scheduled,
        /// <summary>Alarm is currently ringing</summary>
        Ringing,
        /// <summary>Alarm was snoozed</summary>
        Snoozed,
        /// <summary>Alarm is disabled</summary>
        Disabled
    }

    /// <summary>
    /// Represents a scheduled alarm with optional repeat and snooze functionality.
    /// </summary>
    public class Alarm
    {
        private static int _nextId = 1;

        /// <summary>
        /// Unique identifier for the alarm.
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// Time of day for the alarm (hours, minutes, seconds).
        /// </summary>
        public TimeSpan Time { get; set; }

        /// <summary>
        /// Label/name for the alarm.
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// Days to repeat the alarm. Use None for one-time alarm.
        /// </summary>
        public AlarmRepeat Repeat { get; set; } = AlarmRepeat.None;

        /// <summary>
        /// Whether the alarm is enabled.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Snooze duration (default 5 minutes).
        /// </summary>
        public TimeSpan SnoozeDuration { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Maximum number of snoozes allowed (0 = unlimited).
        /// </summary>
        public int MaxSnoozeCount { get; set; } = 3;

        /// <summary>
        /// Current state of the alarm.
        /// </summary>
        public AlarmState State { get; internal set; } = AlarmState.Scheduled;

        /// <summary>
        /// Number of times this alarm has been snoozed since last ring.
        /// </summary>
        public int SnoozeCount { get; internal set; } = 0;

        /// <summary>
        /// The next scheduled time for this alarm.
        /// </summary>
        public DateTimeOffset? NextRingTime { get; internal set; }

        /// <summary>
        /// When the alarm was created.
        /// </summary>
        public DateTimeOffset CreatedAt { get; } = DateTimeOffset.Now;

        /// <summary>
        /// Creates a new alarm.
        /// </summary>
        /// <param name="time">Time of day for the alarm.</param>
        /// <param name="label">Optional label.</param>
        public Alarm(TimeSpan time, string label = "")
        {
            Id = _nextId++;
            Time = time;
            Label = label;
        }

        /// <summary>
        /// Parameterless constructor for serialization.
        /// </summary>
        public Alarm()
        {
            Id = _nextId++;
        }

        /// <summary>
        /// Calculates the next occurrence of this alarm.
        /// </summary>
        public DateTimeOffset? CalculateNextRingTime()
        {
            if (!IsEnabled || State == AlarmState.Disabled)
                return null;

            // If snoozed, return the snooze wake time
            if (State == AlarmState.Snoozed && NextRingTime.HasValue)
                return NextRingTime;

            var now = DateTimeOffset.Now;

            // For one-time alarm (no repeat)
            if (Repeat == AlarmRepeat.None)
            {
                var todayAlarm = now.Date.Add(Time);
                if (todayAlarm > now)
                    return todayAlarm;
                else
                    return todayAlarm.AddDays(1);
            }

            // For repeating alarm, find the next valid day
            for (int i = 0; i < 8; i++)
            {
                var candidate = now.Date.AddDays(i).Add(Time);

                // Skip if it's today but time has passed
                if (i == 0 && candidate <= now)
                    continue;

                var dayOfWeek = candidate.DayOfWeek;
                var repeatFlag = GetRepeatFlag(dayOfWeek);

                if ((Repeat & repeatFlag) != 0)
                    return candidate;
            }

            return null;
        }

        private static AlarmRepeat GetRepeatFlag(DayOfWeek day)
        {
            return day switch
            {
                DayOfWeek.Sunday => AlarmRepeat.Sunday,
                DayOfWeek.Monday => AlarmRepeat.Monday,
                DayOfWeek.Tuesday => AlarmRepeat.Tuesday,
                DayOfWeek.Wednesday => AlarmRepeat.Wednesday,
                DayOfWeek.Thursday => AlarmRepeat.Thursday,
                DayOfWeek.Friday => AlarmRepeat.Friday,
                DayOfWeek.Saturday => AlarmRepeat.Saturday,
                _ => AlarmRepeat.None
            };
        }

        /// <summary>
        /// Formats the next ring time for display.
        /// </summary>
        public string GetNextRingTimeDisplay()
        {
            var next = CalculateNextRingTime();
            if (!next.HasValue)
                return "Not scheduled";

            var diff = next.Value - DateTimeOffset.Now;
            if (diff.TotalHours < 24)
            {
                if (diff.TotalMinutes < 1)
                    return "Less than a minute";
                else if (diff.TotalHours < 1)
                    return $"In {(int)diff.TotalMinutes} minutes";
                else
                    return $"In {(int)diff.TotalHours} hours {diff.Minutes} minutes";
            }
            else
            {
                return next.Value.ToString("ddd, MMM d 'at' h:mm tt");
            }
        }
    }

    /// <summary>
    /// Event args for alarm events.
    /// </summary>
    public class AlarmEventArgs : EventArgs
    {
        /// <summary>
        /// The alarm that triggered the event.
        /// </summary>
        public Alarm Alarm { get; }

        public AlarmEventArgs(Alarm alarm)
        {
            Alarm = alarm;
        }
    }

    /// <summary>
    /// Manages a collection of alarms with automatic scheduling and triggering.
    /// </summary>
    public class AlarmScheduler : IDisposable
    {
        private readonly ObservableCollection<Alarm> _alarms = new();
        private ManagedTimer? _checkTimer;
        private bool _isDisposed;

        /// <summary>
        /// Gets the collection of scheduled alarms.
        /// </summary>
        public ReadOnlyObservableCollection<Alarm> Alarms { get; }

        /// <summary>
        /// Raised when an alarm starts ringing.
        /// </summary>
        public event EventHandler<AlarmEventArgs>? AlarmTriggered;

        /// <summary>
        /// Raised when an alarm is snoozed.
        /// </summary>
        public event EventHandler<AlarmEventArgs>? AlarmSnoozed;

        /// <summary>
        /// Raised when an alarm is dismissed.
        /// </summary>
        public event EventHandler<AlarmEventArgs>? AlarmDismissed;

        public AlarmScheduler()
        {
            Alarms = new ReadOnlyObservableCollection<Alarm>(_alarms);
            StartScheduler();
        }

        /// <summary>
        /// Adds an alarm to the scheduler.
        /// </summary>
        /// <param name="alarm">The alarm to add.</param>
        public void AddAlarm(Alarm alarm)
        {
            alarm.NextRingTime = alarm.CalculateNextRingTime();
            _alarms.Add(alarm);
        }

        /// <summary>
        /// Creates and adds a new alarm.
        /// </summary>
        /// <param name="time">Time of day for the alarm.</param>
        /// <param name="label">Optional label.</param>
        /// <param name="repeat">Repeat pattern.</param>
        /// <returns>The created alarm.</returns>
        public Alarm AddAlarm(TimeSpan time, string label = "", AlarmRepeat repeat = AlarmRepeat.None)
        {
            var alarm = new Alarm(time, label) { Repeat = repeat };
            AddAlarm(alarm);
            return alarm;
        }

        /// <summary>
        /// Removes an alarm from the scheduler.
        /// </summary>
        /// <param name="alarm">The alarm to remove.</param>
        public bool RemoveAlarm(Alarm alarm)
        {
            return _alarms.Remove(alarm);
        }

        /// <summary>
        /// Removes an alarm by ID.
        /// </summary>
        /// <param name="id">The alarm ID.</param>
        public bool RemoveAlarm(int id)
        {
            var alarm = FindAlarm(id);
            return alarm != null && _alarms.Remove(alarm);
        }

        /// <summary>
        /// Finds an alarm by ID.
        /// </summary>
        /// <param name="id">The alarm ID.</param>
        public Alarm? FindAlarm(int id)
        {
            foreach (var alarm in _alarms)
            {
                if (alarm.Id == id)
                    return alarm;
            }
            return null;
        }

        /// <summary>
        /// Snoozes a ringing alarm.
        /// </summary>
        /// <param name="alarm">The alarm to snooze.</param>
        public void Snooze(Alarm alarm)
        {
            if (alarm.State != AlarmState.Ringing)
                return;

            if (alarm.MaxSnoozeCount > 0 && alarm.SnoozeCount >= alarm.MaxSnoozeCount)
            {
                // Max snoozes reached, dismiss instead
                Dismiss(alarm);
                return;
            }

            alarm.State = AlarmState.Snoozed;
            alarm.SnoozeCount++;
            alarm.NextRingTime = DateTimeOffset.Now.Add(alarm.SnoozeDuration);

            AlarmSnoozed?.Invoke(this, new AlarmEventArgs(alarm));
        }

        /// <summary>
        /// Dismisses a ringing or snoozed alarm.
        /// </summary>
        /// <param name="alarm">The alarm to dismiss.</param>
        public void Dismiss(Alarm alarm)
        {
            if (alarm.State != AlarmState.Ringing && alarm.State != AlarmState.Snoozed)
                return;

            alarm.State = AlarmState.Scheduled;
            alarm.SnoozeCount = 0;

            // If it's a one-time alarm, disable it
            if (alarm.Repeat == AlarmRepeat.None)
            {
                alarm.IsEnabled = false;
                alarm.State = AlarmState.Disabled;
            }

            // Calculate next ring time
            alarm.NextRingTime = alarm.CalculateNextRingTime();

            AlarmDismissed?.Invoke(this, new AlarmEventArgs(alarm));
        }

        /// <summary>
        /// Enables or disables an alarm.
        /// </summary>
        /// <param name="alarm">The alarm.</param>
        /// <param name="enabled">Whether to enable or disable.</param>
        public void SetEnabled(Alarm alarm, bool enabled)
        {
            alarm.IsEnabled = enabled;
            alarm.State = enabled ? AlarmState.Scheduled : AlarmState.Disabled;
            alarm.NextRingTime = alarm.CalculateNextRingTime();
        }

        private void StartScheduler()
        {
            _checkTimer = new ManagedTimer(TimeSpan.FromSeconds(1));
            _checkTimer.Tick += CheckAlarms;
            _checkTimer.Start();
        }

        private void CheckAlarms(object? sender, EventArgs e)
        {
            var now = DateTimeOffset.Now;

            foreach (var alarm in _alarms)
            {
                if (!alarm.IsEnabled)
                    continue;

                if (alarm.State == AlarmState.Ringing)
                    continue; // Already ringing

                var nextRing = alarm.NextRingTime ?? alarm.CalculateNextRingTime();
                if (nextRing.HasValue && now >= nextRing.Value)
                {
                    // Trigger the alarm
                    alarm.State = AlarmState.Ringing;
                    AlarmTriggered?.Invoke(this, new AlarmEventArgs(alarm));
                }
            }
        }

        /// <summary>
        /// Disposes the scheduler and stops checking for alarms.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _checkTimer?.Dispose();
            _checkTimer = null;
            _isDisposed = true;
        }
    }
}
