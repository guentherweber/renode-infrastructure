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
    public class PeriodicInterrupt : IPeripheral, IGPIOSender
    {
        public PeriodicInterrupt(Machine machine, long interrupt_Hz = 48000)
        {
            interrupt_Signal = new GPIO();

            Reset();
            // Limit = Frequency [Hz] / Clock [Hz]
            ClockTimer = new LimitTimer(machine.ClockSource, 32000000, this, nameof(ClockTimer), 1, Direction.Ascending, false, WorkMode.Periodic, true, true, 1);

            SetInterruptFrequency(interrupt_Hz);

            ClockTimer.LimitReached += LimitReached;
            ClockTimer.EventEnabled = true;
            //            StartClock();
        }


        public void SetInterruptFrequency(long frequency)
        {
            if (frequency > 0)
            {
                // Limit = Frequency [Hz] / Clock [Hz]
                var old = ClockTimer.Enabled;
                ClockTimer.Enabled = false;
                ClockTimer.ResetValue();
                ClockFrequency = frequency;
                ClockTimer.Limit = (ulong)Math.Round((double)ClockTimer.Frequency / (double)(ClockFrequency));
                ClockTimer.Enabled = old;
            }
        }

        public long GetInterruptFrequency()
        {
            return ClockFrequency;
        }

        public void StartPeriodicInterrupt()
        {
            ClockTimer.Enabled = true;
        }

        public void StopPeriodicInterrupt()
        {
            ClockTimer.Enabled = false;
        }
        private void LimitReached()
        {
            interrupt_Signal.Set();
            interrupt_Signal.Unset();
        }

        public void Reset()
        {
        }

        public GPIO interrupt_Signal { get; }

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
