using System.Globalization;
using NetSchedulerLib.Common;
using NetSchedulerLib.Profiles;
using Serilog;
using LoggerExtensions = NetSchedulerLib.Utility.LoggerExtensions;

namespace NetSchedulerLib.Events;

public class EsEvent : IEsEvent, IDisposable
{
    #region Properties

    /// <summary>
    /// Logger instance used for logging events, errors, and other activities within the <see cref="EsEvent"/> class.
    /// </summary>
    /// <remarks>
    /// This logger is initialized using the <see cref="NetSchedulerLib.Utility.LoggerExtensions.GetLoggerFor{T}"/> method
    /// and is scoped specifically to the <see cref="EsEvent"/> class. It provides mechanisms to log informational messages,
    /// errors, and exceptions that occur during the lifecycle of an event, such as its creation, state changes, or execution failures.
    /// </remarks>
    private static readonly ILogger Logg = LoggerExtensions.GetLoggerFor<EsEvent>();

    /// <summary>
    /// Event triggered when an <see cref="IEsEvent"/> is fired.
    /// </summary>
    /// <remarks>
    /// This event provides notifications to subscribers whenever an associated event is fired.
    /// It delivers an <see cref="IEsEvent"/> instance as a parameter, allowing subscribers to
    /// handle or process the corresponding event details accordingly. The event is primarily
    /// used to perform actions or propagate changes linked to the specific event being triggered.
    /// </remarks>
    /// 
    public event Action<IEsEvent>? OnEventFired;

    /// <summary>
    /// Id is Auto changed while sorting events
    /// Id-1 meaning this is first event to fire
    /// </summary>
    public uint Id { get; set; }

    /// <summary>
    /// Represents the name of the event within the <see cref="EsEvent"/> class.
    /// </summary>
    /// <remarks>
    /// The <c>Name</c> property uniquely identifies the event and is utilized in various operations, such as
    /// adding, removing, or managing events within the context of an <see cref="EsProfile"/>. Its value is immutable
    /// and is assigned during the creation of the event, ensuring consistent identification throughout the event's lifecycle.
    /// </remarks>
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
    private object? UserObject { get; set; }
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
            else
            {
                Logg.Error($"Target time is not set for event {Name}. Using current time as default.");
                TargetTime = DateTime.Now.AddMinutes(5); // default to 5 minutes from now
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

            if (Recurrence == ERecurrence.NotSet && TargetTime < DateTime.Now)
            {
                Logg.Error(
                    $"Event {Name} could not be created: TargetTime {TargetTime} is in the past and Recurrence is not set.");
                    Dispose();
                return;
            }
            
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

    /// <summary>
    /// Initializes and starts the event's timer to fire at the next full minute.
    /// </summary>
    /// <exception cref="Exception">Thrown when an error occurs while starting the event, containing the original exception as inner exception.</exception>
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

    #region Check if Firing

    /// <summary>
    /// Validates the time for the event execution and processes the event accordingly.
    /// </summary>
    /// <param name="state">State object supplied by the scheduler, can be null.</param>
    /// <exception cref="Exception">Thrown if there is an error during event processing or scheduling the next check time.</exception>
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
    
    
    #endregion
    
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
    
    #region Actions

    public void AddActions(List<string>? actions)
    {
        if (actions is null) return;
        foreach (var action in actions)
        {
            AddAction(action);
        }
    }

    public void RemoveActions(List<string>? actions)
    {
        if (actions is null) return;
        foreach (var action in actions)
        {
            RemoveAction(action);
        }
    }

    public List<string> GetActions()
    {
        if (UserObject is List<string> actions)
        {
            return actions;
        }
        return new List<string>();
    }

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

    public void RemoveAction(string? action)
    {
        if (string.IsNullOrWhiteSpace(action)) return;
        if (UserObject is not List<string> actions) return;
        if (!actions.Contains(action)) return;
        actions.Remove(action);
        UserObject = actions;
        Profile.Changed = true;
    }

    public void ClearActions()
    {
        UserObject = new List<string>();
        Profile.Changed = true;
    }

    public void SetActions(List<string>? actions)
    {
        if (actions is null) return;
        UserObject = actions;
        Profile.Changed = true;
    }

    public bool HasActions()
    {
        return UserObject is List<string> { Count: > 0 };
    }


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
    
    #region Dispose
    
    private bool _disposed;
    
    public void Dispose()
    {
        if (_disposed) return;
        
        _checkTimer.Dispose();
        GC.SuppressFinalize(this);
        Logg.Debug($"Event: '{Name}' disposed.");
        _disposed = true;
    }


    #endregion
 
}