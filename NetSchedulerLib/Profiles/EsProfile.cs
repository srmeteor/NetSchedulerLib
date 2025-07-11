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

    /// <summary>
    /// Indicates whether the profile's state or configuration has been modified.
    /// </summary>
    /// <remarks>
    /// The <c>_changed</c> field is used internally to track if any changes
    /// have been made that require persistence. When set to <c>true</c>, a timer
    /// is triggered to save updates after a short delay, ensuring that changes
    /// are consistently and efficiently written to the storage.
    /// </remarks>
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

    /// <summary>
    /// Provides access to the collection of events associated with this profile.
    /// </summary>
    /// <remarks>
    /// The <c>Events</c> property is a thread-safe dictionary that stores events managed by the profile.
    /// Events are identified by their unique string-based keys. This collection is used internally
    /// to retrieve, manipulate, or iterate over events within the profile.
    /// </remarks>
    private ConcurrentDictionary<string, IEsEvent> Events { get; }

    /// <summary>
    /// Represents the file path where the profile's configuration data is stored.
    /// </summary>
    /// <remarks>
    /// The <c>_configFilePath</c> is initialized based on the associated <c>EventScheduler</c>'s configuration folder
    /// and the profile's name. It is used to save and load persistent profile data, such as events and metadata,
    /// ensuring consistency and recovery across application sessions.
    /// </remarks>
    private readonly string _configFilePath;
    
    public EventScheduler Owner { get; private set; }


    public event Action<IEsEvent>? OnProfileEventFired;

    /// <summary>
    /// Manages the scheduling of save operations for the profile.
    /// </summary>
    /// <remarks>
    /// The <c>_saveTimer</c> is responsible for triggering save operations after a specified delay.
    /// It is used to batch multiple configuration changes into a single save operation, reducing
    /// the frequency of disk writes. It is typically activated when the <c>Changed</c> property of
    /// the <see cref="EsProfile"/> is set to <c>true</c>.
    /// </remarks>
    private readonly Timer _saveTimer;

    /// <summary>
    /// A static semaphore used to regulate concurrent access to shared resources
    /// within the context of profile operations.
    /// </summary>
    /// <remarks>
    /// The <c>ProfileSemaphore</c> is utilized to ensure thread-safe operations
    /// when handling asynchronous tasks related to the saving and updating of profiles.
    /// It limits access to a single thread at a time, preventing race conditions
    /// and ensuring data consistency during critical operations.
    /// </remarks>
    private static readonly SemaphoreSlim ProfileSemaphore = new(1, 1);
    
    #endregion

    #region  Constructor *********************************************************************

    /// <summary>
    /// Represents a profile containing configurable events for a scheduler.
    /// Provides functionality to manage, add, remove, enable, disable, and save events asynchronously.
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

    /// <summary>
    /// Enables all events in the profile that are currently disabled.
    /// Iterates through the list of events, enabling each event and handling errors.
    /// </summary>
    /// <returns>
    /// A boolean indicating whether all events were successfully enabled.
    /// Returns false if an exception occurs during the process.
    /// </returns>
    public bool EnableAllEvents()
    {
        try
        {
            // _saveTimer.Change(3 * 1000, Timeout.Infinite);
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

    /// <summary>
    /// Disables all events associated with the profile.
    /// Attempts to set all enabled events to a disabled state and updates their status accordingly.
    /// </summary>
    /// <returns>True if all events were successfully disabled, otherwise false.</returns>
    public bool DisableAllEvents()
    {
        try
        {
            // _saveTimer.Change(3 * 1000, Timeout.Infinite); // Disable() will set Changed to true
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

    /// <summary>
    /// Removes all events associated with the profile.
    /// Triggers the profile's save operation with a slight delay and ensures all events are removed safely.
    /// </summary>
    /// <returns>
    /// True if all events are successfully removed; otherwise, false.
    /// </returns>
    public bool RemoveAllEvents()
    {
        try
        {
            // _saveTimer.Change(3 * 1000, Timeout.Infinite); Remove() will set Change
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


    /// <summary>
    /// Adds a new event to the profile using the specified event configuration.
    /// Provides an option to overwrite an existing event with the same name if already present.
    /// </summary>
    /// <param name="esEventCfg">The configuration details of the event to be added.</param>
    /// <param name="overwrite">Indicates whether to overwrite an existing event with the same name. The default value is true.</param>
    /// <returns>True if the event was successfully added; otherwise, false.</returns>
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
            
            // Use Setter to activate Save Timer
            Changed = true;
            Logg.Debug($"Profile: '{Name}' Added event {esEvent.Name} => {esEvent.TargetTime}");
            return true;

        }
        catch (Exception ex)
        {
            Logg.Error($"Failed to add event: {ex.Message}");
        }

        return false;
    }

    /// <summary>
    /// Removes an event by its name from the profile.
    /// Ensures that associated resources are properly disposed of and updates the profile's state.
    /// </summary>
    /// <param name="eventName">The name of the event to be removed. It must not be null or whitespace.</param>
    /// <returns>
    /// Returns true if the event was successfully removed; otherwise, false.
    /// Returns false if the event does not exist or an error occurs during the removal process.
    /// </returns>
    public bool RemoveEvent(string? eventName)
    {
        if (string.IsNullOrWhiteSpace(eventName)) return false;

        try
        {
            if (Events.TryRemove(eventName, out var removedEvent))
            {
                removedEvent.OnEventFired -= HandleEventFired;
                removedEvent.Dispose();
                // Use Setter to activate Save Timer
                Changed = true;
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
            Logg.Debug($"Profile: '{Name}' Event '{firedEvent.Name}' fired. Handling update");
            // Allow other events to fire before saving
            // It is managed with Change Property
            // _saveTimer.Change(3 * 1000, Timeout.Infinite); // Save in 30 seconds in case of multiple events
        }
        catch (Exception ex)
        {
            Logg.Error($"Error handling fired event: {ex.Message}");
        }
    }
    
    #endregion
    
    #region Save

    /// <summary>
    /// Handles the saving of the profile if there are any unsaved changes.
    /// Triggered periodically or explicitly to persist the profile state.
    /// </summary>
    /// <param name="state">An optional state object passed to the handler, not utilized in the current implementation.</param>
    private void SaveHandler(object? state)
    {
        if (!_changed) return;
        _ = SaveAsync();
    }
    
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

    /// <summary>
    /// Generates an event configuration object from the provided event instance.
    /// Extracts event details such as ID, name, description, state, type, recurrence properties,
    /// target time, last fired time, and associated actions.
    /// </summary>
    /// <param name="ev">The event instance from which the configuration is created.</param>
    /// <returns>A configuration object that represents the specified event.</returns>
    private Models.EsEventCfg GetEventCfg(IEsEvent ev)
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