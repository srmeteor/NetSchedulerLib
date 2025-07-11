using System.Collections.Concurrent;
using Newtonsoft.Json;

namespace NetSchedulerLib.Common;

public static class Models
{
    public class EsEventCfg
    {
        /// <summary>
        /// Will be automatically changed after sorting events 
        /// </summary>
        [JsonProperty("id")]
        public uint Id { get; set; }

        /// <summary>
        /// Unique Event Name per Profile
        /// </summary>
        [JsonProperty("name")] public string? Name { get; set; }

        /// <summary>
        /// Optional Event Description
        /// </summary>
        [JsonProperty("description")] public string? Description { get; set; }
        
        /// <summary>
        /// Automatically generated describing Event Recurrence
        /// </summary>
        [JsonProperty("rec-description")] public string? RecDescription { get; set; }

        /// <summary>
        /// AbsoluteEvent | AstronomicalEvent
        /// </summary>
        [JsonProperty("type")]
        public string? EventType { get; set; }

        /// <summary>
        /// Enabled | Disabled
        /// </summary>
        [JsonProperty("state")]
        public string? EventState { get; set; }

        /// <summary>
        /// Not Set | EveryNthMinute | EveryNthHour | EveryNthDay/Week/Month/Year
        /// </summary>
        [JsonProperty("frequency")]
        public string? EventRecurrence { get; set; }

        /// <summary>
        /// Every Nth(rate) Minute, Hour, Day, Week, Month, Year
        /// </summary>
        [JsonProperty("rate")]
        public int EventRecurrenceRate { get; set; }

        /// <summary>
        /// Represents Selected weekdays, days in month
        /// </summary>
        [JsonProperty("add-rate")]
        public int EventRecAdditionalRate { get; set; }

        /// <summary>
        /// Represents Astro Event Offset, Sunrise:+75 => 1 Hour and 15 mins after Sunrise
        /// </summary>
        [JsonProperty("astro-offset")]
        public string? AstroOffset { get; set; }

        /// <summary>
        /// next event target time in format yyyy-MM-ddTHH:mm:sszzz
        /// </summary>
        [JsonProperty("target-time")]
        public string? TargetTime { get; set; }
        
        /// <summary>
        /// This property is updated on Event-Fire
        /// </summary>
        [JsonProperty("last-fired")]
        public string? LastFired { get; set; }
        
        /// <summary>
        /// next event target time in format HH:mm
        /// Compatibility with Old event config
        /// </summary>
        [JsonProperty("time")]
        public string? Time { get; set; }
        
        /// <summary>
        /// next event target date in format MM/dd/yyyy
        /// Compatibility with Old event config
        /// </summary>
        [JsonProperty("date")]
        public string? Date { get; set; }

        /// <summary>
        /// Unused property
        /// Compatibility with Old event config
        /// </summary>
        [JsonProperty("acknowledge")] public bool Acknowledge { get; set; }

        /// <summary>
        /// Event Actions (string action representation)
        /// </summary>
        [JsonProperty("actions")] public List<string>? Actions { get; set; }
        
    }
    
    public class EsProfileCfg
    {
        /// <summary>
        /// Unique Name per EventScheduler Instance
        /// </summary>
        [JsonProperty("name")] public string? Name { get; set; }
        
        /// <summary>
        /// Optional Profile Description
        /// </summary>
        [JsonProperty("description")] public string? Description { get; set; }
        
        /// <summary>
        /// Events associated with this Profile
        /// </summary>
        [JsonProperty("events")] public List<EsEventCfg>? Events { get; set; }
        
        /// <summary>
        /// Auto updated on Profile save
        /// </summary>
        [JsonProperty("last-modified")] public string? LastModified { get; set; }
    }
}

public enum ERecurrence
{
    NotSet,
    EveryNthMinute,
    EveryNthHour,
    EveryNthDay,
    EveryNthWeek,
    EveryNthMonth,
    EveryNthYear,
}

public enum EEventState
{
    Enabled,
    Disabled
}


public enum EEventType
{
    AbsoluteEvent,
    AstronomicalEvent,
}

public enum EWeekDays
{
    /// <summary>weekday recurrence is not set</summary>
    NotSet = 0,

    /// <summary>Seventh day of the week</summary>
    Sunday = 1,

    /// <summary>First day of the week</summary>
    Monday = 2,

    /// <summary>Second day of the week</summary>
    Tuesday = 4,

    /// <summary>Third day of the week</summary>
    Wednesday = 8,

    /// <summary>Fourth day of the week</summary>
    Thursday = 16, // 0x00000010

    /// <summary>Fifth day of the week</summary>
    Friday = 32, // 0x00000020

    /// <summary>Sixth day of the week</summary>
    Saturday = 64, // 0x00000040

    /// <summary>Standard days of the work week</summary>
    Workdays = Friday | Thursday | Wednesday | Tuesday | Monday, // 0x0000003E

    /// <summary>Standard weekend days</summary>
    Weekends = Saturday | Sunday, // 0x00000041

    /// <summary>Every day of the week</summary>
    All = Weekends | Workdays, // 0x0000007F
}

public enum EMonthDates
{
    /// <summary>Recurrence monthly date is not set</summary>
    RecurrenceMonthNotSet = 0,

    /// <summary>First of the month</summary>
    FirstOfTheMonth = 2,

    /// <summary>Second of the month</summary>
    SecondOfTheMonth = 4,

    /// <summary>Third of the month</summary>
    ThirdOfTheMonth = 8,

    /// <summary>Fourth of the month</summary>
    FourthOfTheMonth = 16, // 0x00000010

    /// <summary>Fifth of the month</summary>
    FifthOfTheMonth = 32, // 0x00000020

    /// <summary>Sixth of the month</summary>
    SixthOfTheMonth = 64, // 0x00000040

    /// <summary>Seventh of the month</summary>
    SeventhOfTheMonth = 128, // 0x00000080

    /// <summary>Eighth Of The Month</summary>
    EighthOfTheMonth = 256, // 0x00000100

    /// <summary>Ninth Of The Month</summary>
    NinthOfTheMonth = 512, // 0x00000200

    /// <summary>Tenth Of The Month</summary>
    TenthOfTheMonth = 1024, // 0x00000400

    /// <summary>Eleventh Of The Month</summary>
    EleventhOfTheMonth = 2048, // 0x00000800

    /// <summary>Twelfth Of The Month</summary>
    TwelfthOfTheMonth = 4096, // 0x00001000

    /// <summary>Thirteenth Of The Month</summary>
    ThirteenthOfTheMonth = 8192, // 0x00002000

    /// <summary>Fourteenth Of The Month</summary>
    FourteenthOfTheMonth = 16384, // 0x00004000

    /// <summary>Fifteenth Of The Month</summary>
    FifteenthOfTheMonth = 32768, // 0x00008000

    /// <summary>Sixteenth Of The Month</summary>
    SixteenthOfTheMonth = 65536, // 0x00010000

    /// <summary>Seventeenth Of The Month</summary>
    SeventeenthOfTheMonth = 131072, // 0x00020000

    /// <summary>Eighteenth Of The Month</summary>
    EighteenthOfTheMonth = 262144, // 0x00040000

    /// <summary>Nineteenth Of The Month</summary>
    NineteenthOfTheMonth = 524288, // 0x00080000

    /// <summary>Twentieth Of The Month</summary>
    TwentiethOfTheMonth = 1048576, // 0x00100000

    /// <summary>TwentyFirst Of The Month</summary>
    TwentyFirstOfTheMonth = 2097152, // 0x00200000

    /// <summary>TwentySecond Of The Month</summary>
    TwentySecondOfTheMonth = 4194304, // 0x00400000

    /// <summary>TwentyThird Of The Month</summary>
    TwentyThirdOfTheMonth = 8388608, // 0x00800000

    /// <summary>TwentyFourth Of The Month</summary>
    TwentyFourthOfTheMonth = 16777216, // 0x01000000

    /// <summary>TwentyFifth Of The Month</summary>
    TwentyFifthOfTheMonth = 33554432, // 0x02000000

    /// <summary>TwentySixth Of The Month</summary>
    TwentySixthOfTheMonth = 67108864, // 0x04000000

    /// <summary>TwentySeventh Of The Month</summary>
    TwentySeventhOfTheMonth = 134217728, // 0x08000000

    /// <summary>TwentyEighth Of The Month</summary>
    TwentyEighthOfTheMonth = 268435456, // 0x10000000

    /// <summary>TwentyNinth Of The Month</summary>
    TwentyNinthOfTheMonth = 536870912, // 0x20000000

    /// <summary>Thirtieth Of The Month</summary>
    ThirtiethOfTheMonth = 1073741824, // 0x40000000

    /// <summary>ThirtyFirst Of The Month</summary>
    ThirtyFirstOfTheMonth = -2147483648, // 0x80000000
}

public enum EAstroEvent
    {
        /// <summary>event not defined</summary>
        NotSet,

        /// <summary>
        /// The time at which the sun is 6 degrees below the horizon in the evening. At this time objects are distinguishable
        /// and some stars and planets are visible to the naked eye
        /// </summary>
        DuskCivil,

        /// <summary>
        /// Is when the sun is 12 degrees below the horizon in the evening. At this time, objects are no longer distinguishable,
        /// and the horizon is no longer visible to the naked eye
        /// </summary>
        DuskNautical,

        /// <summary>
        /// The time at which the sun is 18 degrees below the horizon in the evening. At this time the sun no longer illuminates
        /// the sky, and thus no longer interferes with astronomical observations
        /// </summary>
        DuskAstronomical,

        /// <summary>
        /// Astronomical dawn is defined as the moment after which the sky is no longer completely dark.
        /// This occurs when the Sun is 18 degrees below the horizon in the morning.
        /// </summary>
        DawnAstronomical,

        /// <summary>
        /// Nautical dawn is the time at which there is enough sunlight for the horizon and some objects to be distinguishable;
        /// formally, when the Sun is 12 degrees below the horizon in the morning
        /// </summary>
        DawnNautical,

        /// <summary>
        /// Civil dawn is the time at which there is enough light for objects to be distinguishable, so that
        /// outdoor activities can commence; formally, when the Sun is 6 degrees below the horizon in the morning.
        /// </summary>
        DawnCivil,

        /// <summary>
        /// The rise or ascent of the sun above the horizon in the morning.
        /// </summary>
        Sunrise,

        /// <summary>
        /// The setting or descent of the sun below the horizon in the evening.
        /// </summary>
        Sunset,

        /// <summary>
        /// Solar noon is the time at which the sun reaches the highest point in it's daily trajectory.  This requires firmware version 1.0012.0007 or later
        /// </summary>
        SolarNoon,
    }