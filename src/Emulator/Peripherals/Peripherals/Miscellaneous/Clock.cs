//
// Copyright (c) 2010-2021 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class Clock : IPeripheral, IGPIOSender
    {
        public Clock(Machine machine, long clock0_Hz = 48000) 
        {
            clock0_Signal = new GPIO();

            Reset();
            // Limit = Frequency [Hz] / Clock [Hz]
            ClockTimer = new LimitTimer(machine.ClockSource, 32000000, this, nameof(ClockTimer), 1, Direction.Ascending, false, WorkMode.Periodic, true, true, 1);

            SetClockFrequency(clock0_Hz);

            ClockTimer.LimitReached += LimitReached;
            ClockTimer.EventEnabled = true;
//            StartClock();
        }


        public void SetClockFrequency(long frequency)
        {
            if (frequency > 0)
            {
                // Limit = Frequency [Hz] / Clock [Hz]
                var old = ClockTimer.Enabled;
                ClockTimer.Enabled = false;
                ClockTimer.ResetValue();
                ClockFrequency = frequency;
                ClockTimer.Limit = (ulong) Math.Round((double)ClockTimer.Frequency / (double) (ClockFrequency*2));
                ClockTimer.Enabled = old;
            }
        }

        public long GetClockFrequency()
        {
            return ClockFrequency;
        }

        public void StartClock()
        {
            ClockTimer.Enabled = true;
        }

        public void StopClock()
        {
            ClockTimer.Enabled = false;
        }
        private void LimitReached()
        {
            clock0_Signal.Toggle();
//            clock0_Signal.Set();
//            clock0_Signal.Unset();
        }

        public void Reset()
        {
//            throw new NotImplementedException();
        }

        public GPIO clock0_Signal { get; }

        public event Action<bool> StateChanged;

        private LimitTimer ClockTimer;
        private long ClockFrequency = 48000;


        private void OnStateChange(bool value)
        {
            var sc = StateChanged;
            if (sc != null)
            {
                sc(value);
            }
        }

    }
}
