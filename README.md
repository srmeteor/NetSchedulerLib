# ﻿MIT License

Copyright (c) <year> <your name or organization>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

# NetSchedulerLib
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
There is a TestScheduler project as example of EventScheduler usage. Test-Profile.json is provided (EmbeddedResource) as example profile configuration.
