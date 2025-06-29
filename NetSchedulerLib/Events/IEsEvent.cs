using NetSchedulerLib.Common;
using NetSchedulerLib.Profiles;

namespace NetSchedulerLib.Events;

public interface IEsEvent
{
    event Action<IEsEvent> OnEventFired;
    uint Id { get; }
    string Name { get; }
    
    bool Changed { get; }
    IEsProfile Profile { get; }
    DateTime TargetTime { get; }
    
    EEventState EventState { get; }
    
    EEventType EventType { get; }
    /// <summary>
    /// Could be List of Actions to execute
    /// </summary>
    object UserObject  { get; }
    ERecurrence Recurrence { get; }
    
    

    /// <summary>
    /// For EveryNth... Recurrencies.
    /// default is 1, so EveryNthDay, with rate 1 is equivalent to Daily
    /// </summary>
    uint Rate { get; }

    /// <summary>
    /// For EveryNthWeek Recurrence represents days of the week to be fired on,
    /// For EveryNthMonth Recurrence represents Month dates to be fired on
    /// </summary>
    int AdditionalRate { get; }
    
    string AstroOffset { get; }


    void Dispose();
}

