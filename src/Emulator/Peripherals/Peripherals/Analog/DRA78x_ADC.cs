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


namespace Antmicro.Renode.Peripherals.Analog
{

    [AllowedTranslations(AllowedTranslation.WordToDoubleWord | AllowedTranslation.ByteToDoubleWord)]
    public class DRA78x_ADC : BasicDoubleWordPeripheral, IKnownSize, IWordPeripheral, IBytePeripheral
    {

        private const int NumberOfADCs = 16;

        private IFlagRegisterField bEND_OF_SEQUENCE;
        private IFlagRegisterField bADC_MODULE_ENABLE;
        private IValueRegisterField vADC_STEPENABLE;


        //            private IValueRegisterField vADC_CTRL;

        private List<ushort> adc_values;
        private List<Boolean> adc_active;
        private int CurrentStep = 0;

        public DRA78x_ADC(Machine machine) : base(machine)
        {
            IRQ = new GPIO();

            // Create a list of adc values
            adc_values = new List<ushort>();
            adc_active = new List<Boolean>();

            for (int i=0; i < NumberOfADCs; i++)
            {
                adc_values.Add(0x0000);
                adc_active.Add(false);
            }

            DefineRegisters();
        }


        private void DefineRegisters()
        {
            Register.ADC_REVISION.Define(this, 0x00, "ADC_REVISION")
                .WithTag("Reserved", 0, 32);
            Register.ADC_SYSCONFIG.Define(this, 0x00, "ADC_SYSCONFIG")
                .WithTag("Reserved", 0, 32);
            Register.ADC_IRQSTATUS_RAW.Define(this, 0x00, "ADC_IRQSTATUS_RAW")
                .WithTag("Reserved", 0, 32);
            Register.ADC_IRQSTATUS.Define(this, 0x00, "ADC_IRQSTATUS")
                .WithReservedBits(0, 1)
                .WithValueField(1, 8, name: "unused")
                .WithReservedBits(9, 23);

            Register.ADC_IRQENABLE_SET.Define(this, 0x00, "ADC_IRQENABLE_SET")
                .WithTag("Reserved", 0, 1)
                .WithFlag(1, out bEND_OF_SEQUENCE, FieldMode.Read | FieldMode.Set,
                                            writeCallback: (_, value) =>
                                            {
                                                bEND_OF_SEQUENCE.Value = true;
                                                UpdateInterrupts();
                                            }, name: "END_OF_SEQUENCE Interrupt Enable Set")
                .WithTag("Reserved", 2, 30);
            Register.ADC_IRQENABLE_CLR.Define(this, 0x00, "ADC_IRQENABLE_CLR")
                .WithTag("Reserved", 0, 1)
                .WithFlag(1, out bEND_OF_SEQUENCE, FieldMode.WriteOneToClear | FieldMode.Read,
                        writeCallback: (_, value) =>
                        {
                            if (value == true)
                            {
                                bEND_OF_SEQUENCE.Value = false;
                                UpdateInterrupts();
                            }
                        }, name: "END_OF_SEQUENCE Interrupt Enable Clear")
                .WithTag("Reserved", 2, 30);
            Register.ADC_IRQWAKEUP.Define(this, 0x00, "ADC_IRQWAKEUP")
                .WithTag("Reserved", 0, 32);
            Register.ADC_DMAENABLE_SET.Define(this, 0x00, "ADC_DMAENABLE_SET")
                .WithTag("Reserved", 0, 32);
            Register.ADC_DMAENABLE_CLR.Define(this, 0x00, "ADC_DMAENABLE_CLR")
                .WithTag("Reserved", 0, 32);

            Register.ADC_CTRL.Define(this, 0x00, "ADC_CTRL")
                .WithFlag(0, out bADC_MODULE_ENABLE, FieldMode.Read | FieldMode.Set, 
                        writeCallback: (_, value) =>
                        {
                            CurrentStep = 0;
                            bADC_MODULE_ENABLE.Value = value;
                            UpdateInterrupts();
                        }, name: "adc module enabled")
                .WithValueField(1,31, FieldMode.Read | FieldMode.Write, name:"Reserved");

            Register.ADC_ADCSTAT.Define(this, 0x00000010, "ADC_ADCSTAT")
                .WithTag("Reserved", 0, 32);
            Register.ADC_ADCRANGE.Define(this, 0x00, "ADC_ADCRANGE")
                .WithTag("Reserved", 0, 32);
            Register.ADC_CLKDIV.Define(this, 0x00, "ADC_CLKDIV")
                .WithTag("Reserved", 0, 32);
            Register.ADC_MISC.Define(this, 0x00, "ADC_MISC")
                .WithTag("Reserved", 0, 32);
            Register.ADC_STEPENABLE.Define(this, 0x01, "ADC_STEPENABLE")
                .WithFlag(0, name: "Reserved")
                .WithValueField(1, 16, out vADC_STEPENABLE,
                        writeCallback: (_, value) =>
                        {
                            uint channelbits = 0x0001;
                            for (ushort i = 0; i < NumberOfADCs; i++)
                            {
                                if ((channelbits & value) != 0x0000)
                                    adc_active[i] = true;
                                else
                                    adc_active[i] = false;
                                channelbits = channelbits << 1;
                            }
                        }, name: "ADC_STEPENABLEbits")
                .WithReservedBits(17, 15);
            Register.ADC_IDLECONFIG.Define(this, 0x00, "ADC_IDLECONFIG")
                .WithTag("Reserved", 0, 32);
            Register.ADC_TS_CHARGE_STEPCONFIG.Define(this, 0x00, "ADC_TS_CHARGE_STEPCONFIG")
                .WithTag("Reserved", 0, 32);
            Register.ADC_TS_CHARGE_DELAY.Define(this, 0x00, "ADC_TS_CHARGE_DELAY")
                .WithTag("Reserved", 0, 32);
            Register.ADC_IRQ_EOI.Define(this, 0x00, "ADC_IRQ_EOI")
                .WithTag("Reserved", 0, 32);

            Register.ADC_FIFOCOUNT1.Define(this, 0x00, "ADC_FIFOCOUNT1")
               .WithTag("Reserved", 0, 32);
            Register.ADC_FIFOCOUNT2.Define(this, 0x00, "ADC_FIFOCOUNT2")
               .WithTag("Reserved", 0, 32);
            Register.ADC_FIFOTHRESHOLD1.Define(this, 0x00, "ADC_FIFOTHRESHOLD1")
               .WithTag("Reserved", 0, 32);
            Register.ADC_FIFOTHRESHOLD2.Define(this, 0x00, "ADC_FIFOTHRESHOLD2")
               .WithTag("Reserved", 0, 32);

            Register.ADC_FIFODATA1.Define(this, 0x00, "ADC_FIFODATA1")
                .WithValueField(0, 12,
                valueProviderCallback: _ =>
                {
                    return GetAdcValue();
                }, name: "RESULT")
               .WithTag("Reserved", 12, 20);

            Register.ADC_FIFODATA2.Define(this, 0x00, "ADC_FIFODATA2")
               .WithTag("Reserved", 0, 32);


            Register.ADC_STEPCONFIG1.Define(this, 0x00, "ADC_STEPCONFIG1")
                .WithValueField(0, 32, name: "unused");
            Register.ADC_STEPCONFIG2.Define(this, 0x00, "ADC_STEPCONFIG2")
               .WithValueField(0, 32, name: "unused");
            Register.ADC_STEPCONFIG3.Define(this, 0x00, "ADC_STEPCONFIG3")
               .WithValueField(0, 32, name: "unused");
            Register.ADC_STEPCONFIG4.Define(this, 0x00, "ADC_STEPCONFIG4")
               .WithValueField(0, 32, name: "unused");
            Register.ADC_STEPCONFIG5.Define(this, 0x00, "ADC_STEPCONFIG5")
               .WithValueField(0, 32, name: "unused");
            Register.ADC_STEPCONFIG6.Define(this, 0x00, "ADC_STEPCONFIG6")
               .WithValueField(0, 32, name: "unused");
            Register.ADC_STEPCONFIG7.Define(this, 0x00, "ADC_STEPCONFIG7")
               .WithValueField(0, 32, name: "unused");
            Register.ADC_STEPCONFIG8.Define(this, 0x00, "ADC_STEPCONFIG8")
               .WithValueField(0, 32, name: "unused");
            Register.ADC_STEPCONFIG9.Define(this, 0x00, "ADC_STEPCONFIG9")
               .WithValueField(0, 32, name: "unused");
            Register.ADC_STEPCONFIG10.Define(this, 0x00, "ADC_STEPCONFIG10")
               .WithValueField(0, 32, name: "unused");
            Register.ADC_STEPCONFIG11.Define(this, 0x00, "ADC_STEPCONFIG11")
               .WithValueField(0, 32, name: "unused");
            Register.ADC_STEPCONFIG12.Define(this, 0x00, "ADC_STEPCONFIG12")
               .WithValueField(0, 32, name: "unused");
            Register.ADC_STEPCONFIG13.Define(this, 0x00, "ADC_STEPCONFIG13")
               .WithValueField(0, 32, name: "unused");
            Register.ADC_STEPCONFIG14.Define(this, 0x00, "ADC_STEPCONFIG14")
               .WithValueField(0, 32, name: "unused");
            Register.ADC_STEPCONFIG15.Define(this, 0x00, "ADC_STEPCONFIG15")
               .WithValueField(0, 32, name: "unused");
            Register.ADC_STEPCONFIG16.Define(this, 0x00, "ADC_STEPCONFIG16")
               .WithValueField(0, 32, name: "unused");

            Register.ADC_STEPDELAY1.Define(this, 0x00, "ADC_STEPDELAY1")
               .WithTag("Reserved", 0, 32);
            Register.ADC_STEPDELAY2.Define(this, 0x00, "ADC_STEPDELAY2")
               .WithTag("Reserved", 0, 32);
            Register.ADC_STEPDELAY3.Define(this, 0x00, "ADC_STEPDELAY3")
               .WithTag("Reserved", 0, 32);
            Register.ADC_STEPDELAY4.Define(this, 0x00, "ADC_STEPDELAY4")
               .WithTag("Reserved", 0, 32);
            Register.ADC_STEPDELAY5.Define(this, 0x00, "ADC_STEPDELAY5")
               .WithTag("Reserved", 0, 32);
            Register.ADC_STEPDELAY6.Define(this, 0x00, "ADC_STEPDELAY6")
               .WithTag("Reserved", 0, 32);
            Register.ADC_STEPDELAY7.Define(this, 0x00, "ADC_STEPDELAY7")
               .WithTag("Reserved", 0, 32);
            Register.ADC_STEPDELAY8.Define(this, 0x00, "ADC_STEPDELAY8")
               .WithTag("Reserved", 0, 32);
            Register.ADC_STEPDELAY9.Define(this, 0x00, "ADC_STEPDELAY9")
               .WithTag("Reserved", 0, 32);
            Register.ADC_STEPDELAY10.Define(this, 0x00, "ADC_STEPDELAY10")
               .WithTag("Reserved", 0, 32);
            Register.ADC_STEPDELAY11.Define(this, 0x00, "ADC_STEPDELAY11")
               .WithTag("Reserved", 0, 32);
            Register.ADC_STEPDELAY12.Define(this, 0x00, "ADC_STEPDELAY12")
               .WithTag("Reserved", 0, 32);
            Register.ADC_STEPDELAY13.Define(this, 0x00, "ADC_STEPDELAY13")
               .WithTag("Reserved", 0, 32);
            Register.ADC_STEPDELAY14.Define(this, 0x00, "ADC_STEPDELAY14")
               .WithTag("Reserved", 0, 32);
            Register.ADC_STEPDELAY15.Define(this, 0x00, "ADC_STEPDELAY15")
               .WithTag("Reserved", 0, 32);
            Register.ADC_STEPDELAY16.Define(this, 0x00, "ADC_STEPDELAY16")
               .WithTag("Reserved", 0, 32);



        }



        public long Size => 4096;

        public GPIO IRQ { get; private set; }


        public override void Reset()
        {
            base.Reset();
            UpdateInterrupts();
        }

        private void UpdateInterrupts()
        {
            // Set or Clear Interrupt
            if (bEND_OF_SEQUENCE.Value == true)
            {
                if (bADC_MODULE_ENABLE.Value == true)
                    IRQ.Set(true);
                else
                    IRQ.Set(false);
            }
            else
                IRQ.Set(false);
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

            private enum Register : long
        {
            ADC_REVISION                                                = 0x0U,
            ADC_SYSCONFIG                                               = 0x10U,
            ADC_IRQSTATUS_RAW                                           = 0x24U,
            ADC_IRQSTATUS                                               = 0x28U,
            ADC_IRQENABLE_SET                                           = 0x2cU,
            ADC_IRQENABLE_CLR                                           = 0x30U,
            ADC_IRQWAKEUP                                               = 0x34U,
            ADC_DMAENABLE_SET                                           = 0x38U,
            ADC_DMAENABLE_CLR                                           = 0x3cU,
            ADC_CTRL                                                    = 0x40U,
            ADC_ADCSTAT                                                 = 0x44U,
            ADC_ADCRANGE                                                = 0x48U,
            ADC_CLKDIV                                                  = 0x4cU,
            ADC_MISC                                                    = 0x50U,
            ADC_STEPENABLE                                              = 0x54U,
            ADC_IDLECONFIG                                              = 0x58U,
            ADC_TS_CHARGE_STEPCONFIG                                    = 0x5cU,
            ADC_TS_CHARGE_DELAY                                         = 0x60U,
            ADC_IRQ_EOI                                                 = 0x20U,


            ADC_STEPCONFIG1 = 0x64U + ((0) * 0x8U),
            ADC_STEPCONFIG2 = 0x64U + ((1) * 0x8U),
            ADC_STEPCONFIG3 = 0x64U + ((2) * 0x8U),
            ADC_STEPCONFIG4 = 0x64U + ((3) * 0x8U),
            ADC_STEPCONFIG5 = 0x64U + ((4) * 0x8U),
            ADC_STEPCONFIG6 = 0x64U + ((5) * 0x8U),
            ADC_STEPCONFIG7 = 0x64U + ((6) * 0x8U),
            ADC_STEPCONFIG8 = 0x64U + ((7) * 0x8U),
            ADC_STEPCONFIG9 = 0x64U + ((8) * 0x8U),
            ADC_STEPCONFIG10 = 0x64U + ((9) * 0x8U),
            ADC_STEPCONFIG11 = 0x64U + ((10) * 0x8U),
            ADC_STEPCONFIG12 = 0x64U + ((11) * 0x8U),
            ADC_STEPCONFIG13 = 0x64U + ((12) * 0x8U),
            ADC_STEPCONFIG14 = 0x64U + ((13) * 0x8U),
            ADC_STEPCONFIG15 = 0x64U + ((14) * 0x8U),
            ADC_STEPCONFIG16 = 0x64U + ((15) * 0x8U),

            ADC_STEPDELAY1 = 0x68U + ((0) * 0x8U),
            ADC_STEPDELAY2 = 0x68U + ((1) * 0x8U),
            ADC_STEPDELAY3 = 0x68U + ((2) * 0x8U),
            ADC_STEPDELAY4 = 0x68U + ((3) * 0x8U),
            ADC_STEPDELAY5 = 0x68U + ((4) * 0x8U),
            ADC_STEPDELAY6 = 0x68U + ((5) * 0x8U),
            ADC_STEPDELAY7 = 0x68U + ((6) * 0x8U),
            ADC_STEPDELAY8 = 0x68U + ((7) * 0x8U),
            ADC_STEPDELAY9 = 0x68U + ((8) * 0x8U),
            ADC_STEPDELAY10 = 0x68U + ((9) * 0x8U),
            ADC_STEPDELAY11 = 0x68U + ((10) * 0x8U),
            ADC_STEPDELAY12 = 0x68U + ((11) * 0x8U),
            ADC_STEPDELAY13 = 0x68U + ((12) * 0x8U),
            ADC_STEPDELAY14 = 0x68U + ((13) * 0x8U),
            ADC_STEPDELAY15 = 0x68U + ((14) * 0x8U),
            ADC_STEPDELAY16 = 0x68U + ((15) * 0x8U),


            ADC_FIFOCOUNT1 = 0xe4U + ((0) * 0xcU),
            ADC_FIFOCOUNT2 = 0xe4U + ((1) * 0xcU),

            ADC_FIFOTHRESHOLD1 = 0xe8U + ((0) * 0xcU),
            ADC_FIFOTHRESHOLD2 = 0xe8U + ((1) * 0xcU),

            ADC_DMAREQ1 = 0xecU + ((0) * 0xcU),
            ADC_DMAREQ2 = 0xecU + ((1) * 0xcU),

            ADC_FIFODATA1 = 0x100,
            ADC_FIFODATA2 = 0x200

        }

        private ushort GetAdcValue()
        {
            Boolean bfound = false;
            ushort Value = 0x00;
            while (bfound == false)
            {
                if (adc_active[CurrentStep] == true)
                {
                    bfound = true;
                    Value = adc_values[CurrentStep];
                }
                CurrentStep++;
                if (CurrentStep>= NumberOfADCs)
                {
                    bfound = true;
                    CurrentStep = 0;
                }
            }
            return Value;
        }

        public void WriteAdcValue(int offset, ushort value)
        {
            if (offset < NumberOfADCs)
               adc_values[offset] = value;
            UpdateInterrupts();
        }

        public ushort ReadAdcValue(int offset)
        {
            if (offset < NumberOfADCs)
                return adc_values[offset];
            return 0;
        }

    }
}

