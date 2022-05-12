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

        private const int NumberOfADCs = 8;
        private const int NumberOfSteps = 16;

        private IFlagRegisterField bEND_OF_SEQUENCE;
        private IFlagRegisterField bADC_MODULE_ENABLE;
        private IValueRegisterField vADC_STEPENABLE;
        private IValueRegisterField[] Mode = new IValueRegisterField[NumberOfSteps];
        private IFlagRegisterField[]  Fifo = new IFlagRegisterField[NumberOfSteps];
        private IFlagRegisterField StepIdTag;
        private Queue<uint> AdcQueue0;
        private Queue<uint> AdcQueue1;
        private List<uint> adc_values;
        private List<Boolean> step_enable;

        public DRA78x_ADC(Machine machine) : base(machine)
        {
            IRQ = new GPIO();

            // Create a list of adc values
            adc_values = new List<uint>();
            step_enable = new List<Boolean>();

            for (int i=0; i < NumberOfADCs; i++)
            {
                adc_values.Add(0x0000);
            }
            for (int i = 0; i < NumberOfSteps; i++)
            {
                step_enable.Add(false);
            }
            AdcQueue0 = new Queue<uint>();
            AdcQueue1 = new Queue<uint>();

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
                                                this.Log(LogLevel.Noisy, "END_OF_SEQUENCE {0}", bEND_OF_SEQUENCE.Value);
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
                                this.Log(LogLevel.Noisy, "END_OF_SEQUENCE {0}", bEND_OF_SEQUENCE.Value);
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
                .WithFlag(0, out bADC_MODULE_ENABLE, FieldMode.Read | FieldMode.Write, 
                        writeCallback: (_, value) =>
                        {
                            bADC_MODULE_ENABLE.Value = value;
                            this.Log(LogLevel.Noisy, "ADC Module Enable {0}", value);
                            UpdateInterrupts();
                        }, name: "adc module enabled")
                .WithFlag(1, out StepIdTag, FieldMode.Read | FieldMode.Write, name: "STEP_ID_TAG")
                .WithValueField(2,30, FieldMode.Read | FieldMode.Write, name:"Reserved");

            Register.ADC_ADCSTAT.Define(this, 0x00000010, "ADC_ADCSTAT")
                .WithValueField(0, 32, FieldMode.Read, name: "Reserved");


            Register.ADC_ADCRANGE.Define(this, 0x00,  "ADC_ADCRANGE")
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
                            for (int i = 0; i < NumberOfSteps; i++)
                            {
                                if ((channelbits & value) != 0x0000)
                                {
                                    step_enable[i] = true;
                                    this.Log(LogLevel.Noisy, "step  {0} enable", i);
                                }
                                else
                                {
                                    step_enable[i] = false;
                                    this.Log(LogLevel.Noisy, "step  {0} disable", i);
                                }
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
                    uint Value = AdcQueue0.Dequeue();
                    this.Log(LogLevel.Noisy, "ADC Fifo 0 Value 0x{0:X}", Value);
                    return Value;
                }, name: "RESULT")
               .WithTag("Reserved", 12, 20);

            Register.ADC_FIFODATA2.Define(this, 0x00, "ADC_FIFODATA2")
                .WithValueField(0, 12,
                valueProviderCallback: _ =>
                {
                    uint Value = AdcQueue1.Dequeue();
                    this.Log(LogLevel.Noisy, "ADC Fifo 1 Value 0x{0:X}", Value);
                    return Value;
                }, name: "RESULT")
               .WithTag("Reserved", 12, 20);

            Register.ADC_STEPCONFIG1.DefineMany(this, 16, (register, idx) =>
            {
                register
                    .WithValueField(0, 2, out Mode[idx], FieldMode.Read | FieldMode.Write, name: $"MODE{idx}")
                    .WithValueField(2, 3, FieldMode.Read | FieldMode.Write, name: $"AVERAGING{idx}")
                    .WithValueField(5, 14, FieldMode.Read | FieldMode.Write, name: $"unused{idx}")
                    .WithValueField(19, 4, FieldMode.Read | FieldMode.Write, name: $"SEL_INP_SWC{idx}")
                    .WithValueField(23, 3, FieldMode.Read | FieldMode.Write, name: $"unused{idx}")
                    .WithFlag(26, out Fifo[idx], FieldMode.Read | FieldMode.Write, name: $"FIFO_SELECT{idx}")
                    .WithFlag(27, FieldMode.Read | FieldMode.Write, name: $"RANGE_CHECK{idx}")
                    .WithValueField(28, 4, FieldMode.Read | FieldMode.Write, name: $"unused{idx}");
            }, stepInBytes: 0x08, resetValue: 0x00, name: "ADC_STEPCONFIGx");


            Register.ADC_STEPDELAY1.DefineMany(this, 16, (register, idx) =>
            {
                register
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: $"ADC_STEPDELAY{idx}");
            }, stepInBytes: 0x08, resetValue: 0x00, name: "ADC_STEPDELAYx");






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
                {
                    for (int i = 0; i < NumberOfADCs; i++)
                    {
                        if (step_enable[i] == true)
                        {
                            uint Value = adc_values[i];
                            if (StepIdTag.Value)
                            {
                                Value |= (uint)(i << 16);
                            }
                            if (false == Fifo[i].Value)
                                AdcQueue0.Enqueue(Value);
                            else
                                AdcQueue1.Enqueue(Value);
                        }
                        else
                        {
                            if (Mode[i].Value == 0x00)            // if oneshot disable steps
                                step_enable[i] = false;
                        }
                    }
                    this.Log(LogLevel.Noisy, "Activate IRQ");
                    IRQ.Set(true);
                }
                else
                {
                    this.Log(LogLevel.Noisy, "Deactivate IRQ");
                    IRQ.Set(false);
                    AdcQueue0.Clear(); ;
                    AdcQueue1.Clear(); ;
                }
            }
            else
            {
                this.Log(LogLevel.Noisy, "Deactivate IRQ");
                IRQ.Set(false);
                AdcQueue0.Clear(); ;
                AdcQueue1.Clear(); ;
            }
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



        public void WriteAdcValue(int channel, ushort value)
        {
            this.Log(LogLevel.Noisy, "Write ADC value num:{0}, value: 0x{1:X}", channel, value);

            if (channel < NumberOfADCs)
            adc_values[channel] = value;
            UpdateInterrupts();
        }

        public uint ReadAdcValue(int channel)
        {
            if (channel < NumberOfADCs)
                return adc_values[channel];
            return 0;
        }

    }
}

