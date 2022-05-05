//
// Copyright (c) 2010-2020 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using System.Collections.Generic;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.SPI
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class DRA78x_SPI : NullRegistrationPointPeripheralContainer<ISPIPeripheral>, IDoubleWordPeripheral, IKnownSize
    {
        private uint FrameLength;
        private uint Command;
        private uint WordLength;
        private DoubleWordRegisterCollection registers;
        private IValueRegisterField datareg0;
        private IValueRegisterField datareg1;
        private IValueRegisterField datareg2;
        private IValueRegisterField datareg3;
        private IValueRegisterField cmdreg;
        private uint NumOfWords = 0;
        private bool TransmissionStarted;
        public DRA78x_SPI(Machine machine) : base(machine)
        {

            var registerMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.QSPI_SPI_DATA_REG, new DoubleWordRegister(this)
                    .WithValueField(0,32, out datareg0, FieldMode.Write | FieldMode.Read, valueProviderCallback: _ =>
                     {
                         return ReadData();
                     }
                    , name: "QSPI_SPI_DATA_REG_0")
                },
                {(long)Registers.QSPI_SPI_DATA_REG_1, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out datareg1, FieldMode.Write | FieldMode.Read, valueProviderCallback: _ =>
                     {
                         return ReadData();
                     } , name: "QSPI_SPI_DATA_REG_1")
                },
                {(long)Registers.QSPI_SPI_DATA_REG_2, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out datareg2, FieldMode.Write | FieldMode.Read, valueProviderCallback: _ =>
                     {
                         return ReadData();
                     } , name: "QSPI_SPI_DATA_REG_2")
                },
                {(long)Registers.QSPI_SPI_DATA_REG_3, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out datareg3, FieldMode.Write | FieldMode.Read, valueProviderCallback: _ =>
                     {
                         return ReadData();
                     } , name: "QSPI_SPI_DATA_REG_3")
                },

                {
                (long)Registers.QSPI_SPI_SWITCH_REG, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write | FieldMode.Read, name: "MMPT_S")
                    .WithFlag(1, FieldMode.Write | FieldMode.Read, name: "MM_INT_EN")
                    .WithReservedBits(2, 30)
                },

                {(long)Registers.QSPI_SPI_CMD_REG, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out cmdreg, FieldMode.Write, writeCallback: (_, val) =>
                           {
                                cmdreg.Value = val;
                                WriteData();

                           }, name: "QSPI_SPI_CMD_REG")

                },

                {
                (long)Registers.QSPI_SPI_CLOCK_CNTRL_REG, new DoubleWordRegister(this)
                    .WithValueField(0,32, FieldMode.Write | FieldMode.Read, name: "QSPI_SPI_CLOCK_CNTRL_REG")
                },
                {
                (long)Registers.QSPI_SPI_DC_REG, new DoubleWordRegister(this)
                    .WithValueField(0,32, FieldMode.Write | FieldMode.Read, name: "QSPI_SPI_DC_REG")
                },
                {
                (long)Registers.QSPI_SPI_STATUS_REG, new DoubleWordRegister(this)
                    .WithValueField(0,32, FieldMode.Write | FieldMode.Read, name: "QSPI_SPI_STATUS_REG")
                },
                {
                (long)Registers.QSPI_SPI_SETUP0_REG, new DoubleWordRegister(this)
                    .WithValueField(0,32, FieldMode.Write | FieldMode.Read, name: "QSPI_SPI_SETUP0_REG")
                },

            };
            registers = new DoubleWordRegisterCollection(this, registerMap);
        }

        private uint ReadData()
        {
            uint value = RegisteredPeripheral.Transmit((byte)0x00);
            NumOfWords--;

            if (NumOfWords <= 0)
            {
                RegisteredPeripheral.FinishTransmission();
                TransmissionStarted = false;
            }

            return value;

        }
        private void WriteData()
        {
            Command = BitHelper.GetValue(cmdreg.Value, 16, 3);
            WordLength = BitHelper.GetValue(cmdreg.Value, 19, 7) + 1;
            FrameLength = BitHelper.GetValue(cmdreg.Value, 0, 12) + 1;

            if (Command == 0x02)
            {
                if (TransmissionStarted == false)
                {
                    NumOfWords = FrameLength;
                    TransmissionStarted = true;
                }
                TransmitWord(WordLength);
                NumOfWords--;
            }
            if ((Command == 0x00) || (Command == 0x04))
            {
                RegisteredPeripheral.FinishTransmission();
                TransmissionStarted = false;
            }

            if (NumOfWords <= 0)
            {
                RegisteredPeripheral.FinishTransmission();
                TransmissionStarted = false;
            }
        }
        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public override void Reset()
        {
            registers.Reset();
            TransmissionStarted = false;
        }


        public long Size => 0x1000000;

        private void TransmitWord(uint WordLength)
        {
            if (WordLength > 120)
                RegisteredPeripheral.Transmit((byte)BitHelper.GetValue(datareg3.Value, 24, 8));
            if (WordLength > 112)
                RegisteredPeripheral.Transmit((byte)BitHelper.GetValue(datareg3.Value, 16, 8));
            if (WordLength > 104)
                RegisteredPeripheral.Transmit((byte)BitHelper.GetValue(datareg3.Value, 8, 8));
            if (WordLength > 96)
                RegisteredPeripheral.Transmit((byte)BitHelper.GetValue(datareg3.Value, 0, 8));


            if (WordLength > 88)
                RegisteredPeripheral.Transmit((byte)BitHelper.GetValue(datareg2.Value, 24, 8));
            if (WordLength > 80)
                RegisteredPeripheral.Transmit((byte)BitHelper.GetValue(datareg2.Value, 16, 8));
            if (WordLength > 72)
                RegisteredPeripheral.Transmit((byte)BitHelper.GetValue(datareg2.Value, 8, 8));
            if (WordLength > 64)
                RegisteredPeripheral.Transmit((byte)BitHelper.GetValue(datareg2.Value, 0, 8));


            if (WordLength > 56)
                RegisteredPeripheral.Transmit((byte)BitHelper.GetValue(datareg1.Value, 24, 8));
            if (WordLength > 48)
                RegisteredPeripheral.Transmit((byte)BitHelper.GetValue(datareg1.Value, 16, 8));
            if (WordLength > 40)
                RegisteredPeripheral.Transmit((byte)BitHelper.GetValue(datareg1.Value, 8, 8));
            if (WordLength > 32)
                RegisteredPeripheral.Transmit((byte)BitHelper.GetValue(datareg1.Value, 0, 8));

            if (WordLength > 24)
                RegisteredPeripheral.Transmit((byte)BitHelper.GetValue(datareg0.Value, 24, 8));
            if (WordLength > 16)
                RegisteredPeripheral.Transmit((byte)BitHelper.GetValue(datareg0.Value, 16, 8));
            if (WordLength > 8)
                RegisteredPeripheral.Transmit((byte)BitHelper.GetValue(datareg0.Value, 8, 8));
            if (WordLength > 0)
                RegisteredPeripheral.Transmit((byte)BitHelper.GetValue(datareg0.Value, 0, 8));






        }



        private enum Registers
        {
            QSPI_PID = 0x00,
            QSPI_SYSCONFIG = 0x10,
            QSPI_INTR_STATUS_RAW_SET = 0x20,
            QSPI_INTR_STATUS_ENABLED_CLEAR = 0x24,
            QSPI_INTR_ENABLE_SET_REG = 0x28,
            QSPI_INTR_ENABLE_CLEAR_REG = 0x2c,
            QSPI_SPI_CLOCK_CNTRL_REG = 0x40,
            QSPI_SPI_DC_REG = 0x44,
            QSPI_SPI_CMD_REG = 0x48,
            QSPI_SPI_STATUS_REG = 0x4c,
            QSPI_SPI_DATA_REG = 0x50,
            QSPI_SPI_SETUP0_REG = 0x54,
            QSPI_SPI_SWITCH_REG = 0x64,
            QSPI_SPI_SETUP1_REG = 0x58,
            QSPI_SPI_SETUP2_REG = 0x5c,
            QSPI_SPI_SETUP3_REG = 0x60,
            QSPI_SPI_DATA_REG_1 = 0x68,
            QSPI_SPI_DATA_REG_2 = 0x6c,
            QSPI_SPI_DATA_REG_3 = 0x70
        }
    }
}

