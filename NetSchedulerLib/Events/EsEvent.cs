using System.Globalization;
using NetSchedulerLib.Common;
using NetSchedulerLib.Profiles;

namespace NetSchedulerLib.Events;

public class EsEvent : IEsEvent, IDisposable
{
    public event Action<IEsEvent>? OnEventFired;
    public uint Id { get; }
    public string Name { get; }
    public bool Changed { get; }
    public IEsProfile Profile { get; private set; }
    public DateTime TargetTime { get; private set; }
    public EEventState EventState { get; private set; }
    public EEventType EventType { get; private set;}
    public object UserObject { get; set; }
    public ERecurrence Recurrence { get; }
    public uint Rate { get; }
    public int AdditionalRate { get; private set; }
    public string AstroOffset { get; private set; }

    private Timer? _checkTimer;
    
    
    
    public EsEvent(Models.EsEventCfg esEventCfgCfg, IEsProfile profile)
    {
        try
        {
            Id = esEventCfgCfg.Id;
            Name = esEventCfgCfg.Name?.Trim() ?? throw new Exception("Name cannot be null");
            Profile = profile;
            

            Recurrence = Enum.TryParse(esEventCfgCfg.EventRecurrence, true, out ERecurrence rec)
                ? rec
                : ERecurrence.NotSet;
            
            Rate = (uint) esEventCfgCfg.EventRecurrenceRate;
            AdditionalRate = esEventCfgCfg.EventRecAdditionalRate;
            
            EventState = Enum.TryParse(esEventCfgCfg.EventState, true, out EEventState state)
                ? state
                : EEventState.Enabled;
            
            EventType = Enum.TryParse(esEventCfgCfg.EventType, true, out EEventType type) 
                ? type
                : EEventType.AbsoluteEvent;
            
            AstroOffset = !string.IsNullOrEmpty(esEventCfgCfg.AstroOffset) 
                ? esEventCfgCfg.AstroOffset 
                : EventType == EEventType.AstronomicalEvent 
                    ? "Sunset:-10" // Default to sunset
                    : string.Empty;

            UserObject = esEventCfgCfg.Actions ?? new List<string>();
            
            // Define the format to parse the string
            string format = "yyyy-MM-ddTHH:mm:sszzz";
            if (esEventCfgCfg.TargetTime != null)
                TargetTime = DateTime.ParseExact(esEventCfgCfg.TargetTime, format, CultureInfo.InvariantCulture);

            TargetTime = TargetTime.AddSeconds(60 - TargetTime.Second); // Round up to the next minute ( 60 - Second)
            
            // In case of past Target Time
            CalculateNextTime();
            
            if (TargetTime < DateTime.Now)
            {
                throw new Exception("Target Time cannot be in the past");
            }
            
            // Start the timer
            Start();
            Console.WriteLine($"Event {Name} created, TargetTime: {TargetTime}, Recurrence: {Recurrence}, Rate: {Rate}, AdditionalRate: {AdditionalRate}");
        }
        catch (Exception e)
        {
            throw new Exception("Error creating EsEvent: ", e);
        }

    }

    private void Start()
    {
        try
        {
            DateTime now = DateTime.Now;
            int millisecondsUntilNextMinute = (60 - now.Second) * 1000 - now.Millisecond;

            _checkTimer = new Timer(CheckTime, null, millisecondsUntilNextMinute, 60000);

        }
        catch (Exception e)
        {
            throw new Exception("Error Starting EsEvent: ", e);
        }
    }

    private void CheckTime(object? state)
    {
        try
        {
            // If Not yet => exit
            if (DateTime.Now < TargetTime) return;
            
            // Fire Event
            OnEventFired?.Invoke(this);

            if (Recurrence == ERecurrence.NotSet)
            {
                Profile.RemoveEvent(Name);
                return;
            }
            CalculateNextTime();
            DateTime now = DateTime.Now;
            int millisecondsUntilNextMinute = (60 - now.Second) * 1000 - now.Millisecond;
            _checkTimer?.Change(millisecondsUntilNextMinute, 60000);
            Profile.Changed = true;
            
        }
        catch (Exception e)
        {
            throw new Exception("Error Checking Target time: ", e);
        }
    }

    private void CalculateNextTime()
    {
        try
        {
            switch (Recurrence)
            {
                case ERecurrence.EveryNthWeek:
                {
                    var days = GetWeekDays(AdditionalRate);
                    if (TargetTime > DateTime.Now &&
                        days.Contains(TargetTime.Day))
                        break;
                    TargetTime = CalculateNextWeeklyEvent(TargetTime, Rate, AdditionalRate);
                    

                    break;
                }
                case ERecurrence.EveryNthMonth:
                {
                    var dates = GetMonthDates(AdditionalRate);
                    if (TargetTime > DateTime.Now &&
                        dates.Contains(TargetTime.Day))
                        break;
                    TargetTime = CalculateNextMonthlyEvent(TargetTime, Rate, AdditionalRate);
                    break;
                }
                case ERecurrence.NotSet:
                    break;
                case ERecurrence.EveryNthMinute:
                    while (TargetTime < DateTime.Now)
                        TargetTime = TargetTime.AddMinutes(Rate);
                    break;
                case ERecurrence.EveryNthHour:
                    while (TargetTime < DateTime.Now)
                        TargetTime = TargetTime.AddHours(Rate);
                    break;
                case ERecurrence.EveryNthDay:
                    while (TargetTime < DateTime.Now)
                        TargetTime = TargetTime.AddDays(Rate);
                    break;
                case ERecurrence.EveryNthYear:
                    while (TargetTime < DateTime.Now)
                        TargetTime = TargetTime.AddYears((int) Rate);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }


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
                Console.WriteLine(
                    $"Astronomical Event: {astroEvent} at {TargetTime} with offset {offsetValue}"
                );

            }
            
            Console.WriteLine($"Next time calculated: {TargetTime}");

        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
    
    private DateTime CalculateNextWeeklyEvent(DateTime currentDate, uint rate, int additionalRate)
    {
        if (rate <= 0)
            throw new ArgumentException("Rate must be greater than zero.", nameof(rate));

        if (additionalRate <= 0)
            throw new ArgumentException("AdditionalRate must represent valid days of the week.", nameof(additionalRate));

        DateTime nextTime = currentDate;

        // Check the remaining days of the current week
        for (int i = 0; i < 7; i++)
        {
            int dayBit = 1 << (int)nextTime.DayOfWeek;
            if ((additionalRate & dayBit) > 0 && nextTime > currentDate)
            {
                return nextTime;
            }
            nextTime = nextTime.AddDays(1);
        }

        // Jump to the next applicable week based on the rate
        nextTime = currentDate.AddDays(7 * (int)rate);

        // Find the first matching day in the new range
        for (int i = 0; i < 7; i++)
        {
            int dayBit = 1 << (int)nextTime.DayOfWeek;
            if ((additionalRate & dayBit) > 0)
            {
                return nextTime;
            }
            nextTime = nextTime.AddDays(1);
        }

        throw new InvalidOperationException(
            "Unable to calculate the next recurrence time.");
    }

    private List<int> GetMonthDates(int monthDates)
    {
        var dates = new List<int>();

        // Iterate through all days in the month (1 to 31)
        for (int day = 1; day <= 31; day++)
        {
            // Check if the bit corresponding to the current day is set
            if ((monthDates & (1 << (day - 1))) > 0)
            {
                dates.Add(day);
            }
        }

        return dates;
    }

    private List<int> GetWeekDays(int weekDays)
    {
        var days = new List<int>();
        try
        {
            if (((int)EWeekDays.Sunday & weekDays) > 0) days.Add((int)DayOfWeek.Sunday);
            if (((int)EWeekDays.Monday & weekDays) > 0) days.Add((int)DayOfWeek.Monday);
            if (((int)EWeekDays.Tuesday & weekDays) > 0) days.Add((int)DayOfWeek.Tuesday);
            if (((int)EWeekDays.Wednesday & weekDays) > 0) days.Add((int)DayOfWeek.Wednesday);
            if (((int)EWeekDays.Thursday & weekDays) > 0) days.Add((int)DayOfWeek.Thursday);
            if (((int)EWeekDays.Friday & weekDays) > 0) days.Add((int)DayOfWeek.Friday);
            if (((int)EWeekDays.Saturday & weekDays) > 0) days.Add((int)DayOfWeek.Saturday);
            

        }
        catch (Exception e)
        {
            Console.WriteLine($"GetWeekDays Exception: {e.Message}");
        }
        
        
        return days;
    }

    private DateTime CalculateNextMonthlyEvent(DateTime currentDate, uint rate, int additionalRate)
    {
        if (rate <= 0)
            throw new ArgumentException("Rate must be greater than zero.", nameof(rate));

        if (additionalRate <= 0)
            throw new ArgumentException("AdditionalRate must represent valid dates of the month.", nameof(additionalRate));

        DateTime nextTime = currentDate;
        int currentDay = currentDate.Day;

        // Find next available day in the current month
        for (int day = currentDay + 1; day <= DateTime.DaysInMonth(nextTime.Year, nextTime.Month); day++)
        {
            int dayBit = 1 << (day - 1); // Map day to bit (e.g., 1 = bit 0, 2 = bit 1, etc.)
            if ((additionalRate & dayBit) > 0)
            {
                return new DateTime(nextTime.Year, nextTime.Month, day, nextTime.Hour, nextTime.Minute, nextTime.Second);
            }
        }

        // If no valid day is found, jump to the next applicable month based on the rate
        nextTime = nextTime.AddMonths((int) rate);

        // Search for the first matching day in the new month
        for (int day = 1; day <= DateTime.DaysInMonth(nextTime.Year, nextTime.Month); day++)
        {
            int dayBit = 1 << (day - 1);
            if ((additionalRate & dayBit) > 0)
            {
                return new DateTime(nextTime.Year, nextTime.Month, day, nextTime.Hour, nextTime.Minute, nextTime.Second);
            }
        }

        throw new InvalidOperationException("Unable to calculate the next recurrence time.");
    }
    
    
    private bool _disposed;
    
    public void Dispose()
    {
        if (_disposed) return;
        
        _checkTimer?.Dispose();
        _checkTimer = null;
        GC.SuppressFinalize(this);
        _disposed = true;
    }
    
}