# Project Name
NetSchedulerLib is lite, Net8 based, Event Scheduler Library, created based on Crestron.SimplSharp.Scheduler.
Purpose of this Library is to replace Crestron Scheduler on Net8 based systems, but it can be used in other Net based systems

## Features
- Create Profile(s) by providing EsProfile JSON config
- Add Events to existing Profile(s) by providing EsEvent JSON config
- Event recurrence is defined similar as in Crestron Library using EveryNthDay, EveryNthMonth and EveryNthYear. Additionally
EveryNthMinute and EveryNthHour is also possible. There is EventRecurrenceRate property defining repeating rate replacing Nth and EventRecAdditionalRate is used for EveryNthWeek (defining weekdays) and EveryNthMonth (defining month dates).
- After Event firing, Profile is automatically updated 3 seconds after last event is fired, optimizing file operations.
## Prerequisites
- NET8.0 with included nuget packages: SolarCalculator (for calculating Astronomical events times), Newtonsoft.Json (for configurations) and Serilog for logging

## Installation
Provide step-by-step instructions for installing and running the project. For example: