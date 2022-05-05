
//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using System;
using Antmicro.Migrant;
using Antmicro.Renode.Peripherals.GPIOPort;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    
    [AllowedTranslations(AllowedTranslation.WordToDoubleWord | AllowedTranslation.ByteToDoubleWord)]
    public class DRA78x_GPIO : BaseGPIOPort, IKnownSize, IDoubleWordPeripheral, IWordPeripheral, IBytePeripheral
    {

        private readonly DoubleWordRegisterCollection dwordregisters;
        private IValueRegisterField OUTSET;
        private IValueRegisterField OUT;
        private IValueRegisterField OUTCLR;
        private IValueRegisterField IN;

        private const int NumberOfPins = 32;

        public DRA78x_GPIO(Machine machine) : base(machine, NumberOfPins)
        {
            dwordregisters = new DoubleWordRegisterCollection(this);

            DefineRegisters();
        }


        private void DefineRegisters()
        {
            Registers.GPIO_SYSCONFIG.Define(dwordregisters, 0x00000000, "GPIO_SYSCONFIG")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "GPIO_SYSCONFIG");

            Registers.GPIO_CTRL.Define(dwordregisters, 0x00000000, "GPIO_CTRL")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "GPIO_CTRL");

            Registers.GPIO_SYSSTATUS.Define(dwordregisters, 0x00000001, "GPIO_SYSSTATUS")
                .WithFlag(0, FieldMode.Read, name: "Reset")
                .WithReservedBits(1, 31);

            Registers.GPIO_OE.Define(dwordregisters, 0x00000000, "GPIO_OE")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "GPIO_OE");


            Registers.GPIO_DATAIN.Define(dwordregisters, 0x00000000, "GPIO_DATAIN")
                .WithValueField(0, 32, out IN, FieldMode.Read, name: "GPIO_DATAIN");

            Registers.GPIO_IRQSTATUS_0.Define(dwordregisters, 0x00000000, "GPIO_IRQSTATUS_0")
                .WithValueField(0, 32, FieldMode.Read, name: "GPIO_IRQSTATUS_0");

            Registers.GPIO_IRQSTATUS_SET_0.Define(dwordregisters, 0x00000000, "GPIO_IRQSTATUS_SET_0")
                .WithValueField(0, 32, FieldMode.Read, name: "GPIO_IRQSTATUS_SET_0");

            Registers.GPIO_LEVELDETECT0.Define(dwordregisters, 0x00000000, "GPIO_LEVELDETECT0")
                .WithValueField(0, 32, FieldMode.Read, name: "GPIO_LEVELDETECT0");
            Registers.GPIO_LEVELDETECT1.Define(dwordregisters, 0x00000000, "GPIO_LEVELDETECT1")
                .WithValueField(0, 32, FieldMode.Read, name: "GPIO_LEVELDETECT1");
            Registers.GPIO_RISINGDETECT.Define(dwordregisters, 0x00000000, "GPIO_RISINGDETECT")
                .WithValueField(0, 32, FieldMode.Read, name: "GPIO_RISINGDETECT");
            Registers.GPIO_FALLINGDETECT.Define(dwordregisters, 0x00000000, "GPIO_FALLINGDETECT")
                .WithValueField(0, 32, FieldMode.Read, name: "GPIO_FALLINGDETECT");


            Registers.GPIO_DATAOUT.Define(dwordregisters, 0x00, "OUT")
            .WithValueField(0, 32, out OUT, FieldMode.Read | FieldMode.Write,
            writeCallback: (_, value) =>
            {
                OUT.Value = value;
                UpdateState();
            }, name: "GPIO_DATAOUT");


            Registers.GPIO_CLEARDATAOUT.Define(dwordregisters, 0x00, "OUTCLR")
            .WithValueField(0, 32, out OUTCLR, FieldMode.Read | FieldMode.Write,
            writeCallback: (_, value) =>
            {
                OUT.Value = ~value & OUT.Value;
                OUTCLR.Value = ~OUT.Value;
                OUTSET.Value = ~OUTCLR.Value;
                UpdateState();
            }, name: "GPIO_CLEARDATAOUT");

            Registers.GPIO_SETDATAOUT.Define(dwordregisters, 0x00, "OUTSET")
            .WithValueField(0, 32, out OUTSET, FieldMode.Read | FieldMode.Write,
            writeCallback: (_, value) =>
            {
                OUT.Value = value | OUT.Value;
                OUTSET.Value = OUT.Value;
                OUTCLR.Value = ~OUTSET.Value;
                UpdateState();
            }, name: "GPIO_SETDATAOUT");

        }



        public long Size => 408;


        public override void OnGPIO(int number, bool value)
        {
            base.OnGPIO(number, value);
            Connections[number].Set(value);
            uint val = (uint)0x01 << number;

            if (value)
            {
                IN.Value |= val;
            }
            else
            {
                IN.Value &= ~val;
            }
        }

        private void UpdateState()
        {
            var value = OUT.Value;
            for (var i = 0; i < NumberOfPins; i++)
            {
                var state = ((value & 1u) == 1);

                State[i] = state;
                if (state)
                {
                    Connections[i].Set();
                }
                else
                {
                    Connections[i].Unset();
                }

                value >>= 1;
            }
        }



        public override void Reset()
        {
            base.Reset();
        }


        public ushort ReadWord(long offset)
        {
            return (ushort)ReadDoubleWord(offset);
        }

        public void WriteWord(long offset, ushort value)
        {
            WriteDoubleWord(offset, value);
        }

        public byte ReadByte(long offset)
        {
            return (byte)ReadDoubleWord(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            WriteDoubleWord(offset, value);
        }


        private enum Registers : long
        {
            GPIO_REVISION                 = 0x0U,
            GPIO_SYSCONFIG                = 0x10U,
            GPIO_IRQSTATUS_RAW_0          = 0x24U,
            GPIO_IRQSTATUS_RAW_1          = 0x28U,
            GPIO_IRQSTATUS_0              = 0x2CU,
            GPIO_IRQSTATUS_1              = 0x30U,
            GPIO_IRQSTATUS_SET_0          = 0x34U,
            GPIO_IRQSTATUS_SET_1          = 0x38U,
            GPIO_IRQSTATUS_CLR_0          = 0x3cU,
            GPIO_IRQSTATUS_CLR_1          = 0x40U,
            GPIO_IRQWAKEN_0               = 0x44U,
            GPIO_IRQWAKEN_1               = 0x48U,
            GPIO_SYSSTATUS                = 0x114U,
            GPIO_CTRL                     = 0x130U,
            GPIO_OE                       = 0x134U,
            GPIO_DATAIN                   = 0x138U,
            GPIO_DATAOUT                  = 0x13CU,
            GPIO_LEVELDETECT0             = 0x140U,
            GPIO_LEVELDETECT1             = 0x144U,
            GPIO_RISINGDETECT             = 0x148U,
            GPIO_FALLINGDETECT            = 0x14CU,
            GPIO_DEBOUNCENABLE            = 0x150U,
            GPIO_DEBOUNCINGTIME           = 0x154U,
            GPIO_CLEARDATAOUT             = 0x190U,
            GPIO_SETDATAOUT               = 0x194U
        }


        public uint ReadDoubleWord( long offset)
        {
            return dwordregisters.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            dwordregisters.Write(offset, value);
        }

        public Boolean IsGPIOHigh (int Pin)
        {
            Boolean retVal = false;
            uint val = dwordregisters.Read(0x10);
            uint test = 1;
            uint bitpos = test << Pin;
            uint result = bitpos & val;
            if (result > 0)
               retVal = true;
            return retVal;
        }
    }
}