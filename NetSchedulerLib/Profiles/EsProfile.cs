using System.Collections.Concurrent;
using NetSchedulerLib.Common;
using NetSchedulerLib.Events;
using NetSchedulerLib.Utility;
using Newtonsoft.Json;

namespace NetSchedulerLib.Profiles;

public class EsProfile : IEsProfile
{
    public string Name { get; }
    public string Description { get; private set; }
    
    private bool _changed;
    
    public bool Changed
    {
        get => _changed;
        set => _changed = value;
    }

    public ConcurrentDictionary<string, IEsEvent> Events { get; }

    private readonly string _configFilePath;

    public EventScheduler Owner { get; private set; }

    public event Action<IEsEvent>? OnProfileEventFired;
    
    private readonly Timer _saveTimer;

    // Semaphore for synchronizing writes to the configuration file
    private static readonly SemaphoreSlim ProfileSemaphore = new(1, 1);

    // Constructor
    public EsProfile(EventScheduler parent, string name, string description = "")
    {
        Owner = parent;
        Name = name;
        Description = description;
        Events = new ConcurrentDictionary<string, IEsEvent>();
        _saveTimer = new Timer(SaveHandler, null, Timeout.Infinite, Timeout.Infinite); // Save every 5 minutes
        _configFilePath = Path.Combine(parent.ConfigFolder, $"{name}-profile.json");
    }

    private void SaveHandler(object? state)
    {
        if (!_changed) return;
        _ = SaveAsync();
        _changed = false;
    }

    /// <summary>
    /// Asynchronously initializes the profile events by reading the configuration file.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            if (!File.Exists(_configFilePath))
            {
                Console.WriteLine($"Profile config file not found at {_configFilePath}. Creating a new one.");
                await SaveAsync(); // Save an empty config file if none exists
                return;
            }

            string jsonData = await FileOperation.ReadFileAsync(_configFilePath);
            if (string.IsNullOrWhiteSpace(jsonData)) return;

            var profileCfg = JsonConvert.DeserializeObject<Models.EsProfileCfg>(jsonData);
            if (profileCfg == null) return;

            Description = profileCfg.Description ?? "";
            if (profileCfg.Events is {Count: > 0})
            {
                foreach (var cfg in profileCfg.Events)
                {
                    AddEvent(cfg); // Populate events from deserialized configurations
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during profile initialization: {ex.Message}");
        }
    }

    /// <summary>
    /// Adds an event to the profile and updates the configuration asynchronously.
    /// </summary>
    public bool AddEvent(Models.EsEventCfg esEventCfg)
    {
        try
        {
            var esEvent = new EsEvent(esEventCfg, this);

            bool added = Events.TryAdd(esEvent.Name, esEvent);
            if (!added) return false;
            esEvent.OnEventFired += HandleEventFired;
            _changed = true;
            Console.WriteLine($"Profile: '{Name}' Added event {esEvent.Name} => {esEvent.TargetTime}");
            return true;

        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to add event: {ex.Message}");
        }

        return false;
    }

    /// <summary>
    /// Removes an event from the profile and updates the configuration asynchronously.
    /// </summary>
    public bool RemoveEvent(string? eventName)
    {
        if (string.IsNullOrWhiteSpace(eventName)) return false;

        try
        {
            if (Events.TryRemove(eventName, out var removedEvent))
            {
                removedEvent.OnEventFired -= HandleEventFired;
                removedEvent.Dispose();
                _changed = true; // Fire-and-forget configuration save
                return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to remove event: {ex.Message}");
        }

        return false;
    }

    

    /// <summary>
    /// Handles when an event is fired and updates its target time in the configuration.
    /// </summary>
    private void HandleEventFired(IEsEvent firedEvent)
    {
        try
        {
            OnProfileEventFired?.Invoke(firedEvent);
            _saveTimer.Change(30 * 1000, Timeout.Infinite); // Save in 30 seconds in case of multiple events
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling fired event: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves the profile (and its events) to the configuration file asynchronously.
    /// </summary>
    public async Task SaveAsync()
    {
        await ProfileSemaphore.WaitAsync();
        try
        {
            var profile = new Models.EsProfileCfg()
            {
                Name = Name,
                Description = Description,
                Events = new List<Models.EsEventCfg>()
            };
            
            profile.Events = Events.Values.Select(e => new Models.EsEventCfg
            {
                Id = e.Id,
                Name = e.Name,
                Description = e.Profile.Description,
                EventState = e.EventState.ToString(),
                EventType = e.EventType.ToString(),
                EventRecurrence = e.Recurrence.ToString(),
                EventRecurrenceRate = (int)e.Rate,
                EventRecAdditionalRate = (int)e.AdditionalRate,
                TargetTime = e.TargetTime.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                AstroOffset = e.AstroOffset,
                Actions = (List<string>)e.UserObject,
            }).ToList();
            
            profile.LastModified = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");

            string json = JsonConvert.SerializeObject(profile, Formatting.Indented);
            
            await FileOperation.UpdateFileAsync(json, _configFilePath, FileMode.Create);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save profile: {ex.Message}");
        }
        finally
        {
            ProfileSemaphore.Release();
        }
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}