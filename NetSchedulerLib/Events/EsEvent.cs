using System.Globalization;
using NetSchedulerLib.Common;
using NetSchedulerLib.Profiles;
using Serilog;
using LoggerExtensions = NetSchedulerLib.Utility.LoggerExtensions;

namespace NetSchedulerLib.Events;

public class EsEvent : IEsEvent, IDisposable
{
    #region Properties
    
    private static readonly ILogger Logg = LoggerExtensions.GetLoggerFor<EsEvent>();

    
    public event Action<IEsEvent>? OnEventFired;
    public uint Id { get; set; }
    public string Name { get; }
    public string Description { get; set; }
    public string RecDescription { get; set; }
    public IEsProfile Profile { get; private set; }
    public DateTime TargetTime { get; private set; }
    public DateTime? LastFired { get; private set; }
    public string? Time { get; set; }
    public string? Date { get; set; }
    public EEventState EventState { get; private set; }
    public EEventType EventType { get; private set;}
    private object UserObject { get; set; }
    public ERecurrence Recurrence { get; }
    public uint Rate { get; }
    public int AdditionalRate { get; private set; }
    public string AstroOffset { get; private set; }


    private readonly Timer _checkTimer;
    
    #endregion

    #region Constructor ********************************************************************

    /// <summary>
    /// Creating a new EventScheduler event
    /// </summary>
    /// <param name="esEventCfg">JSON event configuration</param>
    /// <param name="profile">profile event is added to</param>
    /// <exception cref="Exception">Name can't be empty and must be unique within profile, TargetTime must be in the future, or Recurrence must be set</exception>
    public EsEvent(Models.EsEventCfg esEventCfg, IEsProfile profile)
    {
        try
        {
            Id = esEventCfg.Id;
            Name = esEventCfg.Name?.Trim() ?? throw new Exception("Name cannot be null");
            Profile = profile;
            
            
            

            Recurrence = Enum.TryParse(esEventCfg.EventRecurrence, true, out ERecurrence rec)
                ? rec
                : ERecurrence.NotSet;
            
            Rate = (uint) esEventCfg.EventRecurrenceRate;
            AdditionalRate = esEventCfg.EventRecAdditionalRate;

            Description = string.IsNullOrWhiteSpace(esEventCfg.Description)
                ? "-"
                : esEventCfg.Description;
            
            EventState = Enum.TryParse(esEventCfg.EventState, true, out EEventState state)
                ? state
                : EEventState.Enabled;
            
            EventType = Enum.TryParse(esEventCfg.EventType, true, out EEventType type) 
                ? type
                : EEventType.AbsoluteEvent;
            
            // AstroOffset = !string.IsNullOrWhiteSpace(esEventCfg.AstroOffset) 
            //     ? esEventCfg.AstroOffset 
            //     : EventType == EEventType.AstronomicalEvent 
            //         ? "Sunset:-10" // Default to sunset
            //         : string.Empty;
            
            AstroOffset = EventType == EEventType.AstronomicalEvent
                ? !string.IsNullOrWhiteSpace(esEventCfg.AstroOffset) 
                    ? esEventCfg.AstroOffset 
                    : "Sunset:-10" // Default to sunset
                : string.Empty;

            UserObject = esEventCfg.Actions ?? new List<string>();
            
            // Define the format to parse the string
            string format = "yyyy-MM-ddTHH:mm:sszzz";
            if (esEventCfg.TargetTime is { Length: > 0 })
            {
                TargetTime = DateTime.ParseExact(esEventCfg.TargetTime, format, CultureInfo.InvariantCulture);
            }
            else if (esEventCfg is { Time: not null, Date: not null })
            {
                format = "MM/dd/yyyy HH:mm";
                TargetTime = DateTime.ParseExact($"{esEventCfg.Date} {esEventCfg.Time}", format, CultureInfo.InvariantCulture);
            }
            Time = TargetTime.ToString("HH:mm");
            Date = TargetTime.ToString("MM/dd/yyyy");

            format = "yyyy-MM-ddTHH:mm:sszzz";
            LastFired = esEventCfg.LastFired != null && DateTime.TryParseExact(esEventCfg.LastFired, format,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var lastFired)
                ? lastFired
                : null;
            
            // In the case of past Target Time
            CalculateNextTime();

            // Round to the nearest minute
            TargetTime = EventScheduler.RoundToNearestMinute(TargetTime);
            
            RecDescription = EventScheduler.GetRecurrenceDescription(TargetTime, Recurrence, Rate, AdditionalRate);
            
            _checkTimer = new Timer(CheckTime, null, Timeout.Infinite, Timeout.Infinite);
            
            // Start the timer
            if (EventState == EEventState.Enabled)
            {
                Start();
                Logg.Information(
                    $"Event {Name} created and Started => TargetTime: {TargetTime}, " +
                    $"Recurrence: {Recurrence}, Rate: {Rate}, AdditionalRate: {AdditionalRate}");
                
            }
            else
            {
                Logg.Information(
                    $"Event {Name} created but not started (disabled), TargetTime: {TargetTime}, Recurrence: {Recurrence}, Rate: {Rate}, AdditionalRate: {AdditionalRate}");

            }
        }
        catch (Exception e)
        {
            Logg.Error($"Error creating EsEvent: {e.Message}," +
                              $"Stack Trace: {e.StackTrace}");
            throw new Exception("Error creating EsEvent: ", e);
        }

    }


    #endregion
    
    #region Start (Enable-Disable)
    
    private void Start()
    {
        try
        {
            DateTime now = DateTime.Now;

            // Calculate the next full minute with zero seconds
            DateTime nextFullMinute = now.AddMinutes(1).AddSeconds(-now.Second).AddMilliseconds(-now.Millisecond);

            // Calculate the milliseconds until the next full minute
            int millisecondsUntilNextMinute = (int)(nextFullMinute - now).TotalMilliseconds;

            // Set the timer to fire at the exact next full minute
            _checkTimer.Change(millisecondsUntilNextMinute, Timeout.Infinite);
        }
        catch (Exception e)
        {
            throw new Exception("Error Starting EsEvent: ", e);
        }
    }
    
    public bool Enable()
    {
        try
        {
            CalculateNextTime();
            Start();
            EventState = EEventState.Enabled;
            Profile.Changed = true;
            return true;
        }
        catch (Exception e)
        {
            Logg.Error("Error Enabling EsEvent: ", e);
        }

        return false;
    }

    public bool Disable()
    {
        if (EventState == EEventState.Disabled) return true;
        _checkTimer.Change(Timeout.Infinite, Timeout.Infinite);
        EventState = EEventState.Disabled;
        Profile.Changed = true;
        return true;
    }
    
    #endregion

    
    
    private void CheckTime(object? state)
    {
        try
        {
            DateTime now = DateTime.Now;

            // Check if the current time matches the TargetTime
            if (now >= TargetTime)
            {
                // Fire the event
                OnEventFired?.Invoke(this);
                Profile.Changed = true;
                LastFired = now;

                // If this is a recurring event, calculate the next occurrence
                if (Recurrence != ERecurrence.NotSet)
                {
                    CalculateNextTime();
                }
                else
                {
                    // If the event is non-recurring, stop processing it further
                    Profile.RemoveEvent(Name);
                    return;
                }
            }

            // Calculate the next full minute (mm:00:000)
            DateTime nextFullMinute = now.AddMinutes(1).AddSeconds(-now.Second).AddMilliseconds(-now.Millisecond);
            int millisecondsUntilNextMinute = (int)(nextFullMinute - now).TotalMilliseconds;

            // Update the timer to trigger at the start of the next minute
            _checkTimer.Change(millisecondsUntilNextMinute, Timeout.Infinite);
        }
        catch (Exception e)
        {
            Logg.Error($"Error in CheckTime: {e.Message}");
        }
    }
    
    
    
    
    #region CalculateNextTime

    /// <summary>
    /// Calculates the next target time for the event based on its recurrence settings and event type.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an unsupported recurrence type is encountered.</exception>
    private void CalculateNextTime()
    {
        try
        {
            // get the next DateTime
            switch (Recurrence)
            {
                case ERecurrence.EveryNthWeek:
                {
                    var days = EventScheduler.GetWeekDays(AdditionalRate);
                    if (TargetTime > DateTime.Now.AddMinutes(1) &&
                        days.Contains((int)TargetTime.DayOfWeek))
                        break;
                    TargetTime = EventScheduler.CalculateNextWeeklyEvent(TargetTime, Rate, AdditionalRate);
                    

                    break;
                }
                case ERecurrence.EveryNthMonth:
                {
                    var dates = EventScheduler.GetMonthDates(AdditionalRate);
                    if (TargetTime > DateTime.Now.AddMinutes(1) &&
                        dates.Contains(TargetTime.Day))
                        break;
                    TargetTime = EventScheduler.CalculateNextMonthlyEvent(TargetTime, Rate, AdditionalRate);
                    break;
                }
                case ERecurrence.NotSet:
                    break;
                case ERecurrence.EveryNthMinute:
                    while (TargetTime < DateTime.Now.AddMinutes(1))
                        TargetTime = TargetTime.AddMinutes(Rate);
                    break;
                case ERecurrence.EveryNthHour:
                    while (TargetTime < DateTime.Now.AddMinutes(1))
                        TargetTime = TargetTime.AddHours(Rate);
                    break;
                case ERecurrence.EveryNthDay:
                    while (TargetTime < DateTime.Now.AddMinutes(1))
                        TargetTime = TargetTime.AddDays(Rate);
                    break;
                case ERecurrence.EveryNthYear:
                    while (TargetTime < DateTime.Now.AddMinutes(1))
                        TargetTime = TargetTime.AddYears((int) Rate);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            
            // if Astronomical Event, calculate Astro Time 
            if (EventType == EEventType.AstronomicalEvent)
            {
                var astroEvent = Enum.TryParse(AstroOffset.Split(':')[0], true, out EAstroEvent astroEventEnum)
                    ? astroEventEnum
                    : EAstroEvent.Sunset; // default to Sunset
                var offsetMin = int.TryParse(AstroOffset.Split(':')[1],
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture, out int offsetValue)
                    ? offsetValue
                    : 0;

                TargetTime = Profile.Owner.GetSolarTime(astroEvent, TargetTime).AddMinutes(offsetMin);
                // Round up to the nearest minute
                TargetTime = EventScheduler.RoundToNearestMinute(TargetTime);
                Logg.Debug(
                    $"Astronomical Event: {astroEvent} at {TargetTime} with offset {offsetValue}"
                );

            }

            Logg.Information($"Event '{Name}' => Next time calculated: {TargetTime}");

        }
        catch (Exception e)
        {
            Logg.Error($"CalculateNextTime Exception: {e.Message}");
        }
    }
    
    
    #endregion
    
    #region Dispose/Sort/Equals/GetHashCode
    
    private bool _disposed;
    
    public void Dispose()
    {
        if (_disposed) return;
        
        _checkTimer.Dispose();
        GC.SuppressFinalize(this);
        Logg.Debug($"Event: '{Name}' disposed.");
        _disposed = true;
    }
    
    #region Actions

    /// <summary>
    /// Adds a collection of actions to the current event.
    /// </summary>
    /// <param name="actions">The list of actions to be added to the event.</param>
    /// <exception cref="NotImplementedException">Thrown when the method is not implemented.</exception>
    public void AddActions(List<string>? actions)
    {
        if (actions is null) return;
        foreach (var action in actions)
        {
            AddAction(action);
        }
    }

    /// <summary>
    /// Removes a list of actions from the event.
    /// </summary>
    /// <param name="actions">The list of actions to be removed. If null, no actions are removed.</param>
    public void RemoveActions(List<string>? actions)
    {
        if (actions is null) return;
        foreach (var action in actions)
        {
            RemoveAction(action);
        }
    }

    /// <summary>
    /// Retrieves the list of actions associated with the event.
    /// </summary>
    /// <returns>A list of strings representing the actions.</returns>
    /// <exception cref="NotImplementedException">Thrown when the method is not implemented.</exception>
    public List<string> GetActions()
    {
        if (UserObject is List<string> actions)
        {
            return actions;
        }
        return new List<string>();
    }

    /// <summary>
    /// Adds an action to the event.
    /// </summary>
    /// <param name="action">The action to add.</param>
    /// <exception cref="NotImplementedException">Thrown when the method is not implemented.</exception>
    public void AddAction(string action)
    {
        if (string.IsNullOrWhiteSpace(action)) return;
        if (UserObject is not List<string> actions)
        {
            actions = new List<string>();
        }
        if (actions.Contains(action)) return;
        actions.Add(action);
        UserObject = actions;
        Profile.Changed = true;
    }

    /// <summary>
    /// Removes an action from the event.
    /// </summary>
    /// <param name="action">The action to remove.</param>
    /// <exception cref="NotImplementedException">Thrown when the method is not implemented.</exception>
    public void RemoveAction(string? action)
    {
        if (string.IsNullOrWhiteSpace(action)) return;
        if (UserObject is not List<string> actions) return;
        if (!actions.Contains(action)) return;
        actions.Remove(action);
        UserObject = actions;
        Profile.Changed = true;
    }

    /// <summary>
    /// Removes all actions associated with the event.
    /// </summary>
    /// <exception cref="NotImplementedException">Thrown when the method is not implemented.</exception>
    public void ClearActions()
    {
        UserObject = new List<string>();
        Profile.Changed = true;
    }

    /// <summary>
    /// Replaces the current list of actions associated with the event.
    /// </summary>
    /// <param name="actions">The new list of actions to associate with the event. Existing actions will be replaced.</param>
    public void SetActions(List<string> actions)
    {
        UserObject = actions;
        Profile.Changed = true;
    }

    /// <summary>
    /// Determines whether the event has any associated actions.
    /// </summary>
    /// <returns>True if the event has one or more actions; otherwise, false.</returns>
    public bool HasActions()
    {
        return UserObject is List<string> { Count: > 0 };
    }


    /// <summary>
    /// Checks whether a specified action exists within the event using the specified string comparison option.
    /// </summary>
    /// <param name="action">The action to check for within the event.</param>
    /// <param name="comparison">The type of string comparison to use when evaluating the action.</param>
    /// <returns>Returns true if the action exists in the event, otherwise false.</returns>
    public bool HasAction(string action, StringComparison comparison)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException("Action cannot be null or whitespace.", nameof(action));
        }

        // Retrieve the current list of actions
        var actions = GetActions();

        // Check if the action exists using the specified StringComparison
        return actions.Any(existingAction => existingAction.Equals(action, comparison));
    }


    /// <summary>
    /// Executes all actions associated with the event.
    /// </summary>
    /// <param name="actionCallback">A general callback to execute for each action. This can be any delegate capable of processing the action and its context.</param>
    public void ExecuteActions(Action<string, object?>? actionCallback)
    {
        if (actionCallback == null) return;
        if (UserObject is not List<string> actions || actions.Count == 0) return;

        foreach (var action in actions)
        {
            // Use the provided callback to process actions asynchronously
            Task.Run(() => actionCallback(action, this));
        }
    }
    
    #endregion


    #endregion
 
}