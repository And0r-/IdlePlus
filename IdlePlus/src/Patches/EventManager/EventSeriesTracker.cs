using System;

namespace IdlePlus.Patches.Minigame
{
    public static class EventSeriesTracker
    {
        // Stores the timestamp of the last StartGame event.
        public static DateTime LastEventStart { get; set; } = DateTime.MinValue;
        // Indicates if an event series is active.
        public static bool IsSeriesActive { get; set; } = false;
        // Stores the event type of the last event.
        public static string LastEventType { get; set; } = "";
    }
}
