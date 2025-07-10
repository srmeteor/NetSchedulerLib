using System.Collections.Concurrent;
using NetSchedulerLib.Common;
using NetSchedulerLib.Events;
using NetSchedulerLib.Utility;
using Newtonsoft.Json;
using Serilog;
using LoggerExtensions = NetSchedulerLib.Utility.LoggerExtensions;

namespace NetSchedulerLib.Profiles;

public class EsProfile : IEsProfile, IDisposable
{
    private static readonly ILogger Logg = LoggerExtensions.GetLoggerFor<EsProfile>();


    #region Properties
    
    public string Name { get; }
    public string Description { get; private set; }
    
    private bool _changed;

    public bool Changed
    {
        get => _changed;
        set
        {
            if (_changed == value) return;
            _changed = value;
            if (_changed)
            {
                _saveTimer.Change(3 * 1000, Timeout.Infinite); // Save in 3 seconds in case of multiple events
            }
        }
    }

    public ConcurrentDictionary<string, IEsEvent> Events { get; }

    private readonly string _configFilePath;

    public EventScheduler Owner { get; private set; }

    public event Action<IEsEvent>? OnProfileEventFired;
    
    private readonly Timer _saveTimer;

    // Semaphore for synchronizing writes to the configuration file
    private static readonly SemaphoreSlim ProfileSemaphore = new(1, 1);
    
    #endregion

    #region  Constructor *********************************************************************

    /// <summary>
    /// Represents a profile in the Event Scheduler system, managing a collection of scheduled events,
    /// and providing functionality for profile configuration persistence and event handling.
    /// </summary>
    public EsProfile(EventScheduler parent, string name, string description = "")
    {
        Owner = parent;
        Name = name;
        Description = description;
        Events = new ConcurrentDictionary<string, IEsEvent>();
        _saveTimer = new Timer(SaveHandler, null, Timeout.Infinite, Timeout.Infinite); // Save every 5 minutes
        _configFilePath = Path.Combine(parent.ConfigFolder, $"{name}-Profile.json");
    }

    #endregion
    
    #region Helper

    public List<IEsEvent> GetEvents()
    {
        var events = Events.Values.ToList();
        // Default Sort by TargetTime
        events.Sort((a, b) => a.TargetTime.CompareTo(b.TargetTime));
        return events;
        
    }

 

    #endregion
    
    #region Add/Remove

    public bool EnableAllEvents()
    {
        try
        {
            _saveTimer.Change(3 * 1000, Timeout.Infinite);
            return Events.Values.ToList()
                .Where(eve => eve.EventState == EEventState.Disabled)
                .Aggregate(true, (current, ev) => current && ev.Enable());
        }
        catch (Exception e)
        {
            Logg.Error($"EnableAllEvents Exception: {e.Message}");
        }
        return false;
    }

    public bool DisableAllEvents()
    {
        try
        {
            _saveTimer.Change(3 * 1000, Timeout.Infinite);
            return Events.Values.ToList()
                .Where(eve => eve.EventState == EEventState.Enabled)
                .Aggregate(true, (current, ev) => current && ev.Disable());
        }
        catch (Exception e)
        {
            Logg.Error($"DisableAllEvents Exception: {e.Message}");
        }
        return false;
    }

    public bool RemoveAllEvents()
    {
        try
        {
            _saveTimer.Change(3 * 1000, Timeout.Infinite);
            return Events.Values.ToList()
                .Aggregate(true, (current, ev) => 
                    current && RemoveEvent(ev.Name));
        }
        catch (Exception e)
        {
            Logg.Error($"RemoveAllEvents Exception: {e.Message}");
        }
        
        return false;
    }
        
        
    public bool AddEvent(Models.EsEventCfg esEventCfg, bool overwrite = true)
    {
        try
        {
            var esEvent = new EsEvent(esEventCfg, this);

            if (overwrite)
            {
                Logg.Debug($"Overwrite flag is TRUE, trying to Remove existing event: {esEvent.Name} before adding new event.");
                var removed = RemoveEvent(esEvent.Name);
                Logg.Debug($"Removed existing event: '{esEvent.Name}' => {removed}");
            }

            bool added = Events.TryAdd(esEvent.Name, esEvent);
            if (!added)
            {
                Logg.Error($"Failed to add event: {esEvent.Name} already exists.");
                return false;
            }
            esEvent.OnEventFired += HandleEventFired;
            _changed = true;
            Logg.Debug($"Profile: '{Name}' Added event {esEvent.Name} => {esEvent.TargetTime}");
            _saveTimer.Change(3 * 1000, Timeout.Infinite);
            return true;

        }
        catch (Exception ex)
        {
            Logg.Error($"Failed to add event: {ex.Message}");
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
                _saveTimer.Change(3 * 1000, Timeout.Infinite);
                return true;
            }
        }
        catch (Exception ex)
        {
            Logg.Error($"Failed to remove event: {ex.Message}");
        }

        return false;
    }
    
    #endregion
    
    #region Callback
    
    /// <summary>
    /// Handles when an event is fired and updates its target time in the configuration.
    /// </summary>
    private void HandleEventFired(IEsEvent firedEvent)
    {
        try
        {
            OnProfileEventFired?.Invoke(firedEvent);
            // Logg.Information($"Profile: '{Name}' Event '{firedEvent.Name}' fired. Handling update");
            // Allow other events to fire before saving
            _saveTimer.Change(3 * 1000, Timeout.Infinite); // Save in 30 seconds in case of multiple events
        }
        catch (Exception ex)
        {
            Logg.Error($"Error handling fired event: {ex.Message}");
        }
    }
    
    #endregion
    
    #region Save
    
    private void SaveHandler(object? state)
    {
        if (!_changed) return;
        _ = SaveAsync();
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
            
            

            // Sort events to ensure consistent ordering by TargetTime
            var sortedEvents = Events.Values.OrderBy(e => e.TargetTime).ToList();

            // Update Ids sequentially from 1 to events.Count
            for (int i = 0; i < sortedEvents.Count; i++)
            {
                sortedEvents[i].Id = (uint)(i + 1);
                profile.Events.Add(GetEventCfg(sortedEvents[i]));
            }

            
            profile.LastModified = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");

            string json = JsonConvert.SerializeObject(profile, Formatting.Indented);
            
            await FileOperation.UpdateFileAsync(json, _configFilePath, FileMode.Create);
            _changed = false;
            Logg.Debug($"Profile: '{Name}' saved.");
        }
        catch (Exception ex)
        {
            Logg.Error($"Failed to save profile: {ex.Message}");
        }
        finally
        {
            ProfileSemaphore.Release();
        }
    }

    public Models.EsEventCfg GetEventCfg(IEsEvent ev)
    {

        return new Models.EsEventCfg()
        {
            Id = ev.Id,
            Name = ev.Name,
            Description = ev.Description,
            RecDescription = ev.RecDescription,
            EventState = ev.EventState.ToString(),
            EventType = ev.EventType.ToString(),
            EventRecurrence = ev.Recurrence.ToString(),
            EventRecurrenceRate = (int)ev.Rate,
            EventRecAdditionalRate = ev.AdditionalRate,
            TargetTime = ev.TargetTime.ToString("yyyy-MM-ddTHH:mm:sszzz"),
            LastFired = ev.LastFired?.ToString("yyyy-MM-ddTHH:mm:sszzz"),
            AstroOffset = ev.AstroOffset,
            Time = ev.Time,
            Date = ev.Date,
            Actions = ev.GetActions(),
        };
    }
    
    #endregion
    
    #region Dispose

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _saveTimer.Dispose();
        if (_changed)
        {
            Task.Run(async () => await SaveAsync()).Wait();
        }

        Events.Values.ToList().ForEach(ev => { RemoveEvent(ev.Name); });
        Events.Clear();
        GC.SuppressFinalize(this);
        Logg.Debug($"Profile: '{Name}' disposed.");
        _disposed = true;
    }
    
    #endregion
    
}