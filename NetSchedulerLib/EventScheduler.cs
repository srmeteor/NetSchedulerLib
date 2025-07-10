using System.Collections.Concurrent;
using Innovative.SolarCalculator;
using NetSchedulerLib.Common;
using NetSchedulerLib.Events;
using NetSchedulerLib.Profiles;
using NetSchedulerLib.Utility;
using Newtonsoft.Json;
using Serilog;
using LoggerExtensions = NetSchedulerLib.Utility.LoggerExtensions;

namespace NetSchedulerLib;

public class EventScheduler : IDisposable
{

    private readonly ConcurrentDictionary<string, IEsProfile> _profiles;
    private static readonly ILogger Logg = LoggerExtensions.GetLoggerFor<EventScheduler>();


    public string ConfigFolder { get; }

    private readonly double _latitude;
    private readonly double _longitude;
    
    public event Action<IEsEvent>? OnEventFired;


    #region Constructor ********************************************************

    
    /// <summary>
    /// Provides functionality to manage and schedule events based on profiles.
    /// Handles initialization, profile retrieval, and solar time calculations.
    /// </summary>
    /// <param name="configFolder">folder with profile's config files</param>
    /// <param name="latitude">Latitude for Solar calculations</param>
    /// <param name="longitude">Longitude for Solar calculations</param>
    public EventScheduler(string configFolder = "ES/", double latitude = 0, double longitude = 0)
    {
        _profiles = new ConcurrentDictionary<string, IEsProfile>();
        _latitude = latitude;
        _longitude = longitude;
        ConfigFolder = configFolder;
    }

    /// <summary>
    /// Asynchronously initializes the profiles with events by reading files from configuration folder.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            if (!Directory.Exists(ConfigFolder))
            {
                Directory.CreateDirectory(ConfigFolder);
            }

            var configFiles = await FileOperation.GetFileListAsync(ConfigFolder, "*rofile.json");

            if (configFiles is {Length: > 0})
            {
                foreach (var file in configFiles)
                {
                    var profCfg =
                        JsonConvert.DeserializeObject<Models.EsProfileCfg>(await FileOperation.ReadFileAsync(file));
                    if (profCfg?.Name == null) continue;
                    var profile = new EsProfile(this, profCfg.Name, profCfg.Description ?? "");
                    if (!_profiles.TryAdd(profile.Name, profile))
                    {
                        Logg.Information($"Profile '{profile.Name}' already exists. Skipping...");
                        continue;
                    }

                    if (profCfg.Events is not { Count: > 0 })
                    {
                        Logg.Information($"Profile '{profile.Name}' has no events. Skipping...");
                        continue;
                    }
                    Logg.Information($"Profile '{profile.Name}' loaded. Trying to add {profCfg.Events.Count} events...");

                    foreach (var eventCfg in profCfg.Events)
                    {
                        var ok = profile.AddEvent(eventCfg);
                        Logg.Information($"Event '{eventCfg.Name}' => {(ok ? "Added Successfully" : "*Error* Adding")}");
                    }

                    profile.OnProfileEventFired -= ProfileOnOnProfileEventFired;
                    profile.OnProfileEventFired += ProfileOnOnProfileEventFired;
                    // if (profile.Changed)
                    //     _ = profile.SaveAsync();
                }
            }
        }
        catch (Exception ex)
        {
            Logg.Error($"Error during profile initialization: {ex.Message}");
        }
    }
    
    

    #endregion

    #region Event Handlers
    
    private void ProfileOnOnProfileEventFired(IEsEvent obj)
    {
        OnEventFired?.Invoke(obj);
        // _logger.Information($"EventScheduler: {DateTime.Now}> Profile: '{obj.Profile.Name}' Event: '{obj.Name}' fired.");
    }
    
    #endregion

    #region Helper Methods

    /// <summary>
    /// Retrieves a profile by its name from the available profiles.
    /// </summary>
    /// <param name="name">The name of the profile to retrieve.</param>
    /// <returns>The profile matching the specified name, or null if not found.</returns>
    public IEsProfile? GetProfile(string name)
    {
        return _profiles.GetValueOrDefault(name);
    }

    /// <summary>
    /// Retrieves all profiles currently managed by the scheduler.
    /// </summary>
    /// <returns>A list of profiles implementing the <see cref="IEsProfile"/> interface.</returns>
    public List<IEsProfile> GetProfiles()
    {
        return _profiles.Values.ToList();
    }
    
    #endregion
    
    #region Event Helper methods
    
        /// <summary>
    /// Generates a human-readable description for the specified recurrence schedule.
    /// </summary>
    /// <param name="targetTime">Target TimeDate of the event the description is for</param>
    /// <param name="recurrence">The recurrence type of the event.</param>
    /// <param name="rate">The rate at which the event recurs.</param>
    /// <param name="additionalRate">Additional parameters defining specific recurrence details.</param>
    /// <returns>A string description of the recurrence schedule.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the recurrence type is not recognized.</exception>
    public static string GetRecurrenceDescription(DateTime targetTime, ERecurrence recurrence, uint rate, int additionalRate)
    {
        switch (recurrence)
        {
            case ERecurrence.NotSet:
                return "One time event";
            case ERecurrence.EveryNthMinute:
                return $"Every({rate})Minute";
            case ERecurrence.EveryNthHour:
                return $"Every({rate})Hour";
            case ERecurrence.EveryNthDay:
                return $"Every({rate})Day";
            case ERecurrence.EveryNthWeek:
                return $"Every({rate})Week ({GetRecurrenceDaysDescription(additionalRate)})";

            case ERecurrence.EveryNthMonth:
                return $"Every({rate})Month ({GetRecurrenceDatesDescription(additionalRate)})";
            case ERecurrence.EveryNthYear:
                return $"Every({rate})Year ({targetTime.Day:D2}/{targetTime.Month:D2})";
            default:
                throw new ArgumentOutOfRangeException(nameof(recurrence), recurrence, null);
        }
    }

    /// <summary>
    /// Returns a formatted string description of the days of
    /// the week based on the provided additionalRate bitmask.
    /// bit0 Sunday, bit1 Monday, ..., bit6 Saturday
    /// </summary>
    /// <param name="additionalRate">An integer bitmask representing days of the week, where each bit corresponds to a day starting from Sunday (bit 0) to Saturday (bit 6).</param>
    /// <returns>A string formatted with the short names of the days represented in the bitmask or "-" if no days are selected.</returns>
    private static string GetRecurrenceDaysDescription(int additionalRate)
    {
        var recDays = new List<string>();
    
        for (int i = 0; i < 7; i++)
        {
            // Check if the bit for this day is set in the additionalRate
            if ((additionalRate & (1 << i)) != 0)
            {
                // Use DateTime.DayOfWeek with modular arithmetic to map day index to a short name
                DayOfWeek dayOfWeek = (DayOfWeek)i;
                recDays.Add(dayOfWeek.ToString()[..2]); // Get the first two characters
            }
        }

        return recDays.Count > 0 ? "-" + string.Join("-", recDays) + "-" : "-";

    }

    /// <summary>
    /// Returns a formatted string description of dates for a monthly
    /// event based on the given additionalRate configuration.
    /// 1st bit1, 2nd bit2, ... 31st bit31 (bit0 is ignored)
    /// </summary>
    /// <param name="additionalRate">An integer where each bit represents a specific day of the month (1 to 31),
    /// indicating which days are part of the recurrence.
    /// </param>
    /// <returns>A formatted string containing the recurring days in a specific pattern, or a default value if no days are set.</returns>
    private static string GetRecurrenceDatesDescription(int additionalRate)
    {
        var recDates = new List<string>();
        for (int i = 1; i <= 31; i++)
        {
            // Check if the bit for this day (day-1 index) is set
            if ((additionalRate & (1 << i)) != 0)
            {
                recDates.Add(i.ToString()); // Add the day to the list
            }
        }
        return recDates.Count > 0 ? "-" + string.Join(".-", recDates) + ".-" : "-";
    }
    
    /// <summary>
    /// Rounds a given DateTime to the nearest minute by evaluating the seconds component.
    /// </summary>
    /// <param name="dateTime">The DateTime object to be rounded.</param>
    /// <returns>A DateTime object rounded to the nearest minute.</returns>
    public static DateTime RoundToNearestMinute(DateTime dateTime)
    {
        return dateTime.Second >= 30 ? dateTime.AddSeconds(60 - dateTime.Second).AddMilliseconds(-dateTime.Millisecond) : // Round up
            dateTime.AddSeconds(-dateTime.Second).AddMilliseconds(-dateTime.Millisecond); // Round down
    }
    
    
       /// <summary>
    /// Calculates the next weekly event occurrence based on the provided date, recurrence rate,
    /// and additional rate representing valid days of the week.
    /// </summary>
    /// <param name="currentDate">The current date from which to calculate the next event occurrence.</param>
    /// <param name="rate">The weekly recurrence rate indicating how often the event repeats in weeks.</param>
    /// <param name="additionalRate">
    /// A bitmask representing the valid days of the week for the event to occur.
    /// Each bit corresponds to a day of the week, starting from Sunday as the least significant bit.
    /// </param>
    /// <returns>
    /// The calculated date and time of the next occurrence of the event.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the rate is not greater than zero or additionalRate does not represent valid days of the week.
    /// </exception>
    public static DateTime CalculateNextWeeklyEvent(DateTime currentDate, uint rate, int additionalRate)
    {
        if (rate <= 0)
            throw new ArgumentException("Rate must be greater than zero.", nameof(rate));

        if (additionalRate <= 0)
            throw new ArgumentException("AdditionalRate must represent valid days of the week.", nameof(additionalRate));

        DateTime nextTime = currentDate;

        // Continue checking weeks until a valid day is found
        while (true)
        {
            int daysChecked = 0;

            // Check the current week for a valid recurrence day
            while (daysChecked < 7)
            {
                // Map DayOfWeek (0-6) to the corresponding EWeekDays bit (1-64)
                int currentDayBit = 1 << ((int)nextTime.DayOfWeek + 1 - 1);

                // Check if the `currentDayBit` is part of the `additionalRate` and is in the future
                if ((additionalRate & currentDayBit) != 0 && nextTime > currentDate.AddMinutes(1))
                {
                    return nextTime; // Valid recurrence day found
                }

                // Increment to the next day
                nextTime = nextTime.AddDays(1);
                daysChecked++;
            }

            // Move to the next week block based on the recurrence rate
            nextTime = nextTime.AddDays(7 * ((int)rate - 1)); // Skip to the next applicable week block
        }
    }

    /// <summary>
    /// Calculates and retrieves a list of valid days within a month based on the given additional rate.
    /// </summary>
    /// <param name="additionalRate">An integer value representing a bitmask where each bit corresponds to a day of the month.
    /// If a bit is set, the corresponding day is included in the output.
    /// 1st bit1, 2nd bit2, ... 31st bit31 (bit0 is ignored)</param>
    /// <returns>A list of integers representing the days of the month indicated by the additional rate bitmask.</returns>
    public static List<int> GetMonthDates(int additionalRate)
    {
        var dates = new List<int>();

        for (int day = 1; day <= 31; day++)
        {
            // Check if the bit for this day (day-1 index) is set
            if ((additionalRate & (1 << day)) != 0)
            {
                dates.Add(day); // Add the day to the list
            }
        }
        Logg.Debug($"Month Dates: {string.Join(",", dates)}");
        return dates;
    }

    /// <summary>
    /// Calculates and retrieves a list of valid days within a week based on the given additional rate.
    /// </summary>
    /// <param name="additionalRate">An integer value representing a bitmask where each bit corresponds to a day of the week.
    /// If a bit is set, the corresponding day is included in the output.
    /// Sunday bit0, Monday bit1, ..., Saturday bit6 </param>
    /// <returns>A list of integers representing the days of the week indicated by the additional rate bitmask.</returns>
    public static List<int> GetWeekDays(int additionalRate)
    {
        var days = new List<int>();
        try
        {
            
    
            for (int i = 0; i < 7; i++)
            {
                // Check if the bit for this day is set in the additionalRate
                if ((additionalRate & (1 << i)) != 0)
                {
                    days.Add(i); // Get the first two characters
                }
            }
            Logg.Debug($"Week Days: {string.Join(",", days)}");

            return days;


        }
        catch (Exception e)
        {
            Logg.Error($"GetWeekDays Exception: {e.Message}");
        }
        
        
        return days;
    }

    /// <summary>
    /// Calculates the next monthly event occurrence based on the specified recurrence settings.
    /// </summary>
    /// <param name="currentDate">The current date and time from which to calculate the next occurrence.</param>
    /// <param name="rate">The number of months to skip between event occurrences.</param>
    /// <param name="additionalRate">Represents the valid days of the month for the occurrence.</param>
    /// <returns>The calculated date and time of the next occurrence.</returns>
    /// <exception cref="ArgumentException">Thrown when the rate is less than or equal to zero, or the additionalRate is invalid.</exception>
    public static DateTime CalculateNextMonthlyEvent(DateTime currentDate, uint rate, int additionalRate)
    {
        if (rate <= 0)
            throw new ArgumentException("Rate must be greater than zero.", nameof(rate));

        if (additionalRate <= 0)
            throw new ArgumentException("AdditionalRate must represent valid dates of the month.", nameof(additionalRate));

        DateTime nextTime = currentDate;

        while (true)
        {
            int daysChecked = 0;

            // Check the current week for a valid recurrence day
            while (daysChecked < DateTime.DaysInMonth(currentDate.Year, currentDate.Month))
            {
                // Map DayOfWeek (0-6) to the corresponding EWeekDays bit (1-64)
                int currentDayBit = 1 << nextTime.Day;

                // Check if the `currentDayBit` is part of the `additionalRate` and is in the future
                if ((additionalRate & currentDayBit) != 0 && nextTime > currentDate.AddMinutes(1))
                {
                    return nextTime; // Valid recurrence day found
                }

                // Increment to the next day
                nextTime = nextTime.AddDays(1);
                daysChecked++;
            }

            // Move to the next week block based on the recurrence rate
            nextTime = nextTime.AddMonths((int)rate - 1); // Skip to the next applicable month block
        }
        
    }
    
    #endregion

    #region Solar Time-Support
    
    
    /// <summary>
    /// Get the solar times for a specific date.
    /// </summary>
    /// <param name="reqDate">The date for which to retrieve the solar times.</param>
    /// <returns>The solar times for the specified date.</returns>
    private SolarTimes GetSolarTimesForDate(DateTime reqDate)
    {
        return new SolarTimes(reqDate, _latitude, _longitude);
    }

    /// <summary>
    /// Retrieves the solar time for the specified astronomical event and date.
    /// </summary>
    /// <param name="astroEvent">The astronomical event to retrieve the solar time.</param>
    /// <param name="date">The date for which to retrieve the solar time.</param>
    /// <param name="allowPast">Allow calculated date time to be in the past (Default-false)</param>
    /// <returns>The solar time for the specified astronomical event and date.</returns>
    public DateTime GetSolarTime(EAstroEvent astroEvent, DateTime date,
        bool allowPast = false)
    {
        try
        {
            var calculatingDate = date;
            var calculatingTime = date.TimeOfDay;

            // Do not calculate for times earlier than 03:30 => in case DST change
            if (calculatingTime.TotalMinutes < 210) // 210 = 3 hours 30 minutes
            {
                calculatingDate = calculatingDate.AddHours(3).AddMinutes(30);
            }

            DateTime solarTime;
            do
            {
                var solarTimes = GetSolarTimesForDate(calculatingDate);
                solarTime = astroEvent switch
                {
                    EAstroEvent.DuskCivil => solarTimes.DuskCivil,
                    EAstroEvent.DawnCivil => solarTimes.DawnCivil,
                    EAstroEvent.DuskNautical => solarTimes.DuskNautical,
                    EAstroEvent.DawnNautical => solarTimes.DawnNautical,
                    EAstroEvent.DuskAstronomical => solarTimes.DuskAstronomical,
                    EAstroEvent.DawnAstronomical => solarTimes.DawnAstronomical,
                    EAstroEvent.Sunrise => solarTimes.Sunrise,
                    _ => solarTimes.Sunset // Default to sunset
                };

                if (allowPast || solarTime >= DateTime.Now.AddMinutes(1))
                {
                    break;
                }

                calculatingDate = calculatingDate.AddDays(1); // Move to the next day if `allowPast` is false
            } while (true);

            return solarTime;
        }
        catch (Exception e)
        {
            Logg.Error(
                $"GetSolarTime Exception: {e.Message}");
        }

        return DateTime.Now;
    }

    #endregion

    #region IDisposable Support
    
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        var profiles = _profiles.Values.ToList();
        foreach (var profile in profiles)
        {
            profile.OnProfileEventFired -= ProfileOnOnProfileEventFired;
            profile.Dispose();
        }

        _profiles.Clear();
        GC.SuppressFinalize(this);
        _disposed = true;
    }
    
    #endregion
    
}