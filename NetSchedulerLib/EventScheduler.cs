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

    
    private static readonly ILogger Logg = LoggerExtensions.GetLoggerFor<EventScheduler>();
    
    #region Properties
    
    /// <summary>
    /// A thread-safe collection that stores and manages event profiles within the system.
    /// </summary>
    /// <remarks>
    /// The `_profiles` variable is a read-only instance of `ConcurrentDictionary` that maps profile names (as keys) to their respective `IEsProfile` instances (as values).
    /// It is used for registering and retrieving profiles, ensuring thread-safe operations in a multi-threaded environment within the `EventScheduler` class.
    /// </remarks>
    private readonly ConcurrentDictionary<string, IEsProfile> _profiles;


    /// <summary>
    /// Gets the directory path used to store configuration files for `EventScheduler`.
    /// </summary>
    /// <remarks>
    /// The `ConfigFolder` property represents the root folder where configuration files,
    /// such as profile-specific JSON files, are saved or loaded.
    /// If the specified directory does not exist, it is automatically created during the initialization process.
    /// This ensures that all relevant configuration data for the scheduler is organized and persisted within a dedicated folder.
    /// </remarks>
    public string ConfigFolder { get; }

    /// <summary>
    /// Represents the latitude coordinate used for solar time calculations in the `EventScheduler`.
    /// </summary>
    /// <remarks>
    /// The `_latitude` variable is a read-only field storing the geographic latitude of the location
    /// where solar time-related events are being calculated. It is provided during the initialization
    /// of the `EventScheduler` instance and used within solar calculation methods, ensuring accurate
    /// event timing based on the sun's position.
    /// </remarks>
    private readonly double _latitude;

    /// <summary>
    /// Represents the geographic longitude used for solar time calculations in the EventScheduler.
    /// </summary>
    /// <remarks>
    /// The `_longitude` variable is a read-only `double` that stores the longitude coordinate in decimal degrees.
    /// It is utilized for computing solar events and times in conjunction with the _latitude, providing an accurate geographic context for scheduling operations.
    /// The value is initialized via the EventScheduler constructor and remains unchanged throughout the scheduler's lifecycle.
    /// </remarks>
    private readonly double _longitude;

    /// <summary>
    /// Event that is triggered whenever an event profile fires an associated event.
    /// </summary>
    /// <remarks>
    /// The `OnEventFired` event provides notifications whenever an `IEsEvent` instance
    /// is fired within the `EventScheduler`. Subscribers can attach handlers to this event
    /// to react to specific occurrences or perform custom actions when events are dispatched.
    /// </remarks>
    public event Action<IEsEvent>? OnEventFired;

    #endregion

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

    /// <summary>
    /// Handles the firing of a profile-specific event and invokes the global event handler if assigned.
    /// </summary>
    /// <param name="obj">The event object containing details about the fired event.</param>
    private void ProfileOnOnProfileEventFired(IEsEvent obj)
    {
        OnEventFired?.Invoke(obj);
        // _logger.Information($"EventScheduler: {DateTime.Now}> Profile: '{obj.Profile.Name}' Event: '{obj.Name}' fired.");
    }
    
    #endregion

    #region Helper Methods

    /// <summary>
    /// Adds a new profile to the event scheduler with the specified name and optional description.
    /// </summary>
    /// <param name="profileName">The name of the profile to be added.</param>
    /// <param name="description">An optional description for the profile.</param>
    /// <returns>True if the profile is successfully added; otherwise, false.</returns>
    public bool AddProfile(string? profileName, string? description = null)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(profileName);


            var profile = new EsProfile(this, profileName, description ?? "");
            if (_profiles.TryAdd(profileName, profile))
            {
                Logg.Information($"Profile '{profile.Name}' successfully added.");
                return true;
            }

            Logg.Error($"Profile '{profile.Name}' already exists. Skipping...");


        }
        catch (Exception e)
        {
            Logg.Error($"AddProfile Error: {e.Message}");
        }

        return false;
    }

    /// <summary>
    /// Removes a profile from the scheduler and its associated resources.
    /// </summary>
    /// <param name="profileName">The name of the profile to be removed.</param>
    public void RemoveProfile(string profileName)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(profileName);
            if (_profiles.TryRemove(profileName, out var profile))
            {
                profile.OnProfileEventFired -= ProfileOnOnProfileEventFired;
                profile.Dispose();
                var result = RemoveProfileConfig(profileName).GetAwaiter().GetResult();
                if (result)
                {
                    Logg.Information($"Profile '{profile.Name}'successfully removed.");
                }
                else
                {
                    Logg.Error($"Profile '{profile.Name}' config file could not be removed.");
                }
            }
            else
            {
                Logg.Error($"Profile '{profileName}' does not exist. Skipping...");
            }

        }
        catch (Exception e)
        {
            Logg.Error($"RemoveProfile Error: {e.Message}");
        }
    }

    /// <summary>
    /// Removes the configuration file associated with the specified profile.
    /// Handles file deletion and logging for the operation.
    /// </summary>
    /// <param name="profileName">The name of the profile whose configuration file should be removed.</param>
    /// <returns>Returns true if the profile configuration file was successfully deleted; otherwise, false.</returns>
    private async Task<bool> RemoveProfileConfig(string profileName)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(profileName);
            var configFile = Path.Combine(ConfigFolder, $"{profileName}-Profile.json");
            if (File.Exists(configFile))
            {
                var result = await FileOperation.FileDeleteAsync(configFile);
                Logg.Information($"Profile '{profileName}' config file successfully removed.");
                return result;
            }
        }
        catch (Exception e)
        {
            Logg.Error($"RemoveProfileConfig Error: {e.Message}");
        }
        return false;
    }

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
                // Map Month dates (0-30) to the corresponding EMonthDates bit (1-31)
                int currentDayBit = 1 << nextTime.Day;

                // Check if the `currentDayBit` is part of the `additionalRate` and is in the future
                if ((additionalRate & currentDayBit) != 0 && nextTime > currentDate.AddMinutes(1))
                {
                    return nextTime; // Valid recurrence day found
                }

                // Increment to the next date
                nextTime = nextTime.AddDays(1);
                daysChecked++;
            }

            // Move to the next month block based on the recurrence rate
            nextTime = nextTime.AddMonths((int)rate - 1); // Skip to the next applicable month
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

            // Do not calculate for times earlier than 03:00 => in case DST change
            // So use 3:10 as calculated time
            
            calculatingDate = calculatingDate.AddMinutes(190 - calculatingTime.TotalMinutes);
            

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

                if (allowPast || solarTime > DateTime.Now.AddMinutes(1))
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