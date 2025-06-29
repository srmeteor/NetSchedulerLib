using System.Collections.Concurrent;
using Innovative.SolarCalculator;
using NetSchedulerLib.Common;
using NetSchedulerLib.Events;
using NetSchedulerLib.Profiles;
using NetSchedulerLib.Utility;
using Newtonsoft.Json;

namespace NetSchedulerLib;

public class EventScheduler : IDisposable
{
    private readonly string _configFolderPath;

    private readonly ConcurrentDictionary<string, IEsProfile> _profiles;

    public string ConfigFolder => _configFolderPath;

    private double _latitude, _longitude;


    public EventScheduler(string configFolder = "/ES/", double latitude = 0, double longitude = 0)
    {
        _profiles = new ConcurrentDictionary<string, IEsProfile>();
        _latitude = latitude;
        _longitude = longitude;
        _configFolderPath = configFolder;
    }

    /// <summary>
    /// Asynchronously initializes the profile events by reading the configuration file.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            if (!Directory.Exists(_configFolderPath))
            {
                Directory.CreateDirectory(_configFolderPath);
            }

            var configFiles = await FileOperation.GetFileListAsync(_configFolderPath, "*profile.json");

            if (configFiles is {Length: > 0})
            {
                foreach (var file in configFiles)
                {
                    var profCfg =
                        JsonConvert.DeserializeObject<Models.EsProfileCfg>(await FileOperation.ReadFileAsync(file));
                    if (profCfg?.Name == null) continue;
                    var profile = new EsProfile(this, profCfg.Name, profCfg.Description ?? "");
                    if (!_profiles.TryAdd(profile.Name, profile)) continue;
                    Console.WriteLine($"Profile '{profile.Name}' loaded. Try to add events...");
                    if (profCfg.Events is not {Count: > 0}) continue;
                    foreach (var cfg in profCfg.Events)
                    {
                        profile.AddEvent(cfg);
                    }

                    profile.OnProfileEventFired -= ProfileOnOnProfileEventFired;
                    profile.OnProfileEventFired += ProfileOnOnProfileEventFired;
                    if (profile.Changed)
                        _ = profile.SaveAsync();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during profile initialization: {ex.Message}");
        }
    }

    private void ProfileOnOnProfileEventFired(IEsEvent obj)
    {
        Console.WriteLine($"{DateTime.Now} > Event '{obj.Name}' fired.");
    }

    public IEsProfile? GetProfile(string name)
    {
        return _profiles.GetValueOrDefault(name);
    }

    public IEnumerable<IEsProfile> GetProfiles()
    {
        return _profiles.Values.ToList();
    }

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
    /// <param name="astroEvent">The Crestron astronomical event for which to retrieve the solar time.</param>
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

                if (allowPast || solarTime >= DateTime.Now)
                {
                    break;
                }

                calculatingDate = calculatingDate.AddDays(1); // Move to the next day if `allowPast` is false
            } while (true);

            return solarTime;
        }
        catch (Exception e)
        {
            Console.WriteLine(
                $"GetSolarTime Exception: {e.Message}");
        }

        return DateTime.Now;
    }


    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        var profiles = _profiles.Values.ToList();
        foreach (var profile in profiles)
        {
            profile.OnProfileEventFired -= ProfileOnOnProfileEventFired;
            if (profile.Changed)
            {
                Task.Run(async () => await profile.SaveAsync()).Wait();
            }

            profile.Events.Values.ToList().ForEach(ev => { profile.RemoveEvent(ev.Name); });
            profile.Events.Clear();
        }

        _profiles.Clear();
        GC.SuppressFinalize(this);
        _disposed = true;
    }
}