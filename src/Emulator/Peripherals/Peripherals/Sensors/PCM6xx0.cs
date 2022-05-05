//
// Copyright (c) 2010-2020 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class PCM6xx0 : II2CPeripheral, IBytePeripheral
    {
        private int SubAddress = 0x00;
        private int State;
        private readonly ByteRegisterCollection byteregisters;



        public PCM6xx0()
        {
            byteregisters = new ByteRegisterCollection(this);

            Registers.PAGE.Define(byteregisters, 0x00, "PAGE")
              .WithValueField(0, 8, out page, name: "page");

            Registers.DIAG_MON_MSB_IN1P.Define(byteregisters, 0x00, "DIAG_MON_MSB_IN1P")
              .WithValueField(0, 8, out msb_1p, name: "DIAG_MON_MSB_IN1P");
            Registers.DIAG_MON_LSB_IN1P.Define(byteregisters, 0x02, "DIAG_MON_LSB_IN1P")
              .WithValueField(0, 4, name: "channel")
              .WithValueField(4, 4, out lsb_1p, name: "DIAG_MON_LSB_IN1P");
            Registers.DIAG_MON_MSB_IN1M.Define(byteregisters, 0x00, "DIAG_MON_MSB_IN1M")
              .WithValueField(0, 8, out msb_1m, name: "DIAG_MON_MSB_IN1M");
            Registers.DIAG_MON_LSB_IN1M.Define(byteregisters, 0x03, "DIAG_MON_LSB_IN1M")
              .WithValueField(0, 4, name: "channel")
              .WithValueField(4, 4, out lsb_1m, name: "DIAG_MON_LSB_IN1M");


            Registers.DIAG_MON_MSB_IN2P.Define(byteregisters, 0x00, "DIAG_MON_MSB_IN2P")
              .WithValueField(0, 8, out msb_2p, name: "DIAG_MON_MSB_IN2P");
            Registers.DIAG_MON_LSB_IN2P.Define(byteregisters, 0x04, "DIAG_MON_LSB_IN2P")
              .WithValueField(0, 4, name: "channel")
              .WithValueField(4, 4, out lsb_2p, name: "DIAG_MON_LSB_IN2P");
            Registers.DIAG_MON_MSB_IN2M.Define(byteregisters, 0x00, "DIAG_MON_MSB_IN2M")
              .WithValueField(0, 8, out msb_2m, name: "DIAG_MON_MSB_IN2M");
            Registers.DIAG_MON_LSB_IN2M.Define(byteregisters, 0x05, "DIAG_MON_LSB_IN2M")
              .WithValueField(0, 4, name: "channel")
              .WithValueField(4, 4, out lsb_2m, name: "DIAG_MON_LSB_IN2M");

            Registers.DIAG_MON_MSB_IN3P.Define(byteregisters, 0x00, "DIAG_MON_MSB_IN3P")
              .WithValueField(0, 8, out msb_3p, name: "DIAG_MON_MSB_IN3P");
            Registers.DIAG_MON_LSB_IN3P.Define(byteregisters, 0x06, "DIAG_MON_LSB_IN3P")
              .WithValueField(0, 4, name: "channel")
              .WithValueField(4, 4, out lsb_3p, name: "DIAG_MON_LSB_IN3P");
            Registers.DIAG_MON_MSB_IN3M.Define(byteregisters, 0x00, "DIAG_MON_MSB_IN3M")
              .WithValueField(0, 8, out msb_3m, name: "DIAG_MON_MSB_IN3M");
            Registers.DIAG_MON_LSB_IN3M.Define(byteregisters, 0x07, "DIAG_MON_LSB_IN3M")
              .WithValueField(0, 4, name: "channel")
              .WithValueField(4, 4, out lsb_3m, name: "DIAG_MON_LSB_IN3M");

            Registers.DIAG_MON_MSB_IN4P.Define(byteregisters, 0x00, "DIAG_MON_MSB_IN4P")
              .WithValueField(0, 8, out msb_4p, name: "DIAG_MON_MSB_IN4P");
            Registers.DIAG_MON_LSB_IN4P.Define(byteregisters, 0x08, "DIAG_MON_LSB_IN4P")
              .WithValueField(0, 4, name: "channel")
              .WithValueField(4, 4, out lsb_4p, name: "DIAG_MON_LSB_IN4P");
            Registers.DIAG_MON_MSB_IN4M.Define(byteregisters, 0x00, "DIAG_MON_MSB_IN4M")
              .WithValueField(0, 8, out msb_4m, name: "DIAG_MON_MSB_IN4M");
            Registers.DIAG_MON_LSB_IN4M.Define(byteregisters, 0x09, "DIAG_MON_LSB_IN4M")
              .WithValueField(0, 4, name: "channel")
              .WithValueField(4, 4, out lsb_4m, name: "DIAG_MON_LSB_IN4M");

            Registers.DIAG_MON_MSB_IN5P.Define(byteregisters, 0x00, "DIAG_MON_MSB_IN5P")
              .WithValueField(0, 8, out msb_5p, name: "DIAG_MON_MSB_IN5P");
            Registers.DIAG_MON_LSB_IN5P.Define(byteregisters, 0x0A, "DIAG_MON_LSB_IN5P")
              .WithValueField(0, 4, name: "channel")
              .WithValueField(4, 4, out lsb_5p, name: "DIAG_MON_LSB_IN5P");
            Registers.DIAG_MON_MSB_IN5M.Define(byteregisters, 0x00, "DIAG_MON_MSB_IN5M")
              .WithValueField(0, 8, out msb_5m, name: "DIAG_MON_MSB_IN5M");
            Registers.DIAG_MON_LSB_IN5M.Define(byteregisters, 0x0B, "DIAG_MON_LSB_IN5M")
              .WithValueField(0, 4, name: "channel")
              .WithValueField(4, 4, out lsb_5m, name: "DIAG_MON_LSB_IN5M");

            Registers.DIAG_MON_MSB_IN6P.Define(byteregisters, 0x00, "DIAG_MON_MSB_IN6P")
              .WithValueField(0, 8, out msb_6p, name: "DIAG_MON_MSB_IN6P");
            Registers.DIAG_MON_LSB_IN6P.Define(byteregisters, 0x0C, "DIAG_MON_LSB_IN6P")
              .WithValueField(0, 4, name: "channel")
              .WithValueField(4, 4, out lsb_6p, name: "DIAG_MON_LSB_IN6P");
            Registers.DIAG_MON_MSB_IN6M.Define(byteregisters, 0x00, "DIAG_MON_MSB_IN6M")
              .WithValueField(0, 8, out msb_6m, name: "DIAG_MON_MSB_IN6M");
            Registers.DIAG_MON_LSB_IN6M.Define(byteregisters, 0x0D, "DIAG_MON_LSB_IN6M")
              .WithValueField(0, 4, name: "channel")
              .WithValueField(4, 4, out lsb_6m, name: "DIAG_MON_LSB_IN6M");

            Registers.DIAG_MON_MSB_VBAT.Define(byteregisters, 0x00, "DIAG_MON_MSB_VBAT")
              .WithValueField(0, 8, out msb_vbat, name: "DIAG_MON_MSB_VBAT");
            Registers.DIAG_MON_LSB_VBAT.Define(byteregisters, 0x00, "DIAG_MON_LSB_VBAT")
              .WithValueField(0, 4, name: "channel")
              .WithValueField(4, 4, out lsb_vbat, name: "DIAG_MON_LSB_VBAT");

            Registers.DIAG_MON_MSB_MBIAS.Define(byteregisters, 0x00, "DIAG_MON_MSB_MBIAS")
              .WithValueField(0, 8, out msb_mbias, name: "DIAG_MON_MSB_MBIAS");
            Registers.DIAG_MON_LSB_MBIAS.Define(byteregisters, 0x01, "DIAG_MON_LSB_MBIAS")
              .WithValueField(0, 4, name: "channel")
              .WithValueField(4, 4, out lsb_mbias, name: "DIAG_MON_LSB_MBIAS");

            Registers.DIAG_MON_MSB_TEMP.Define(byteregisters, 0x00, "DIAG_MON_MSB_TEMP")
              .WithValueField(0, 8, out msb_temp, name: "DIAG_MON_MSB_TEMP");
            Registers.DIAG_MON_LSB_TEMP.Define(byteregisters, 0x0E, "DIAG_MON_LSB_TEMP")
              .WithValueField(0, 4, name: "channel")
              .WithValueField(4, 4, out lsb_temp, name: "DIAG_MON_LSB_TEMP");

            Registers.DIAG_MON_MSB_LOAD.Define(byteregisters, 0x00, "DIAG_MON_MSB_LOAD")
              .WithValueField(0, 8, out msb_load, name: "DIAG_MON_MSB_LOAD");
            Registers.DIAG_MON_LSB_LOAD.Define(byteregisters, 0x0F, "DIAG_MON_LSB_LOAD")
              .WithValueField(0, 4, name: "channel")
              .WithValueField(4, 4, out lsb_load, name: "DIAG_MON_LSB_LOAD");

            Registers.INT_LTCH0.Define(byteregisters, 0x00, "INT_LTCH0")
              .WithValueField(0, 8, out latch0, name: "INT_LTCH0");

            Registers.INT_LTCH3.Define(byteregisters, 0x00, "INT_LTCH3")
              .WithValueField(0, 5, name: "reserved")
              .WithValueField(5, 3, out latch3, name: "INT_LTCH3");

            Reset();

        }


        public void Reset()
        {
            SubAddress = 0x00;
            State = (int)I2CState.Idle;
            byteregisters.Reset();
        }

        public void FinishTransmission()
        {
            this.Log(LogLevel.Noisy, "Finish Transmission");
            State = (int)I2CState.Idle;
        }

        public byte[] Read(int count = 1)
        {

            this.Log(LogLevel.Noisy, "Reading {0} bytes", count);
            var result = new byte[count];

            for (var i = 0; i < result.Length; i++)
            {
                result[i] = ReadByte(SubAddress);
                SubAddress++;
            }
            return result;
        }


        public void Write(byte[] data)
        {
            this.Log(LogLevel.Noisy, "Written {0} bytes of data: {1}", data.Length, Misc.PrettyPrintCollectionHex(data));

            if ((State == (int)I2CState.Idle) && data.Length >= 1)
            {
                var test = data[0] & 0x80;
                /*
                                if (test == 0x80)
                                    bIncrement = true;
                                else
                                    bIncrement = false;
                */
                SubAddress = data[0] & 0x7F;
                State = (int)I2CState.TransmissionActive;
                for (var i = 1; i < data.Length; i++)
                {
                    WriteByte(SubAddress, data[i]);
                    SubAddress++;
                }
            }
            else
            {
                for (var i = 0; i < data.Length; i++)
                {
                    WriteByte(SubAddress, data[i]);
                    SubAddress++;
                }

            }


        }

        private IValueRegisterField page;
        private IValueRegisterField msb_1p;
        private IValueRegisterField lsb_1p;
        private IValueRegisterField msb_1m;
        private IValueRegisterField lsb_1m;

        private IValueRegisterField msb_2p;
        private IValueRegisterField lsb_2p;
        private IValueRegisterField msb_2m;
        private IValueRegisterField lsb_2m;

        private IValueRegisterField msb_3p;
        private IValueRegisterField lsb_3p;
        private IValueRegisterField msb_3m;
        private IValueRegisterField lsb_3m;

        private IValueRegisterField msb_4p;
        private IValueRegisterField lsb_4p;
        private IValueRegisterField msb_4m;
        private IValueRegisterField lsb_4m;

        private IValueRegisterField msb_5p;
        private IValueRegisterField lsb_5p;
        private IValueRegisterField msb_5m;
        private IValueRegisterField lsb_5m;

        private IValueRegisterField msb_6p;
        private IValueRegisterField lsb_6p;
        private IValueRegisterField msb_6m;
        private IValueRegisterField lsb_6m;

        private IValueRegisterField msb_vbat;
        private IValueRegisterField lsb_vbat;
        private IValueRegisterField msb_mbias;
        private IValueRegisterField lsb_mbias;

        private IValueRegisterField msb_temp;
        private IValueRegisterField lsb_temp;
        private IValueRegisterField msb_load;
        private IValueRegisterField lsb_load;

        private IValueRegisterField latch0;
        private IValueRegisterField latch3;

        private enum I2CState
        {
            Idle = 0x00,
            TransmissionActive = 0x01,
        }


        public byte ReadByte(long offset)
        {
            byte value=0;
            offset += (page.Value << 8);
            value = byteregisters.Read(offset);
            this.Log(LogLevel.Noisy, "Read  PCM6xx0 Register 0x{0:X2} 0x{1:X2}", offset, value);

            return value;
        }

        public void WriteByte(long offset, byte value)
        {
            offset += (page.Value << 8);
            this.Log(LogLevel.Noisy, "Write PCM6xx0 Register 0x{0:X2} 0x{1:X2}", offset, value);
            byteregisters.Write(offset, value);
        }

        public void WritePlusAdcValue(int channel, short value)
        {
            int lsb = value & 0x000F;
            int msb = (value & 0x0FF0) >> 4;
            if (channel == 1)
            {
                msb_1p.Value = (uint) msb;
                lsb_1p.Value = (uint) lsb;
            }
            else if (channel == 2)
            {
                msb_2p.Value = (uint)msb;
                lsb_2p.Value = (uint)lsb;
            }
            else if (channel == 3)
            {
                msb_3p.Value = (uint)msb;
                lsb_3p.Value = (uint)lsb;
            }
            else if (channel == 4)
            {
                msb_4p.Value = (uint)msb;
                lsb_4p.Value = (uint)lsb;
            }
            else if (channel == 5)
            {
                msb_5p.Value = (uint)msb;
                lsb_5p.Value = (uint)lsb;
            }
            else if (channel == 6)
            {
                msb_6p.Value = (uint)msb;
                lsb_6p.Value = (uint)lsb;
            }

        }
        public void WriteMinusAdcValue(int channel, short value)
        {
            int lsb = value & 0x000F;
            int msb = (value & 0x0FF0) >> 4;
            if (channel == 1)
            {
                msb_1m.Value = (uint)msb;
                lsb_1m.Value = (uint)lsb;
            }
            else if (channel == 2)
            {
                msb_2m.Value = (uint)msb;
                lsb_2m.Value = (uint)lsb;
            }
            else if (channel == 3)
            {
                msb_3m.Value = (uint)msb;
                lsb_3m.Value = (uint)lsb;
            }
            else if (channel == 4)
            {
                msb_4m.Value = (uint)msb;
                lsb_4m.Value = (uint)lsb;
            }
            else if (channel == 5)
            {
                msb_5m.Value = (uint)msb;
                lsb_5m.Value = (uint)lsb;
            }
            else if (channel == 6)
            {
                msb_6m.Value = (uint)msb;
                lsb_6m.Value = (uint)lsb;
            }

        }

        public void WriteVBatValue(short value)
        {
            int lsb = value & 0x000F;
            int msb = (value & 0x0FF0) >> 4;
            msb_vbat.Value = (uint)msb;
            lsb_vbat.Value = (uint)lsb;
        }

        public void WriteMBiasValue(short value)
        {
            int lsb = value & 0x000F;
            int msb = (value & 0x0FF0) >> 4;
            msb_mbias.Value = (uint)msb;
            lsb_mbias.Value = (uint)lsb;
        }

        public void WriteTemperatureValue(short value)
        {
            int lsb = value & 0x000F;
            int msb = (value & 0x0FF0) >> 4;
            msb_temp.Value = (uint)msb;
            lsb_temp.Value = (uint)lsb;
        }

        public void WriteLoadValue(short value)
        {
            int lsb = value & 0x000F;
            int msb = (value & 0x0FF0) >> 4;
            msb_load.Value = (uint)msb;
            lsb_load.Value = (uint)lsb;
        }

        public void WriteLatch0Value(short value)
        {
            latch0.Value = (uint) value;
        }

        public void WriteLatch1Value(short value)
        {
            latch0.Value = (uint)value;
        }

        private enum Registers : long
        {
            PAGE = 0x00,
            INT_LTCH0 = 0x2C,
            INT_LTCH3 = 0x37,
            DIAG_MON_MSB_VBAT = 0x015A,
            DIAG_MON_LSB_VBAT = 0x015B,
            DIAG_MON_MSB_MBIAS = 0x015C,
            DIAG_MON_LSB_MBIAS = 0x015D,

            DIAG_MON_MSB_IN1P = 0x015E,
            DIAG_MON_LSB_IN1P = 0x015F,
            DIAG_MON_MSB_IN1M = 0x0160,
            DIAG_MON_LSB_IN1M = 0x0161,

            DIAG_MON_MSB_IN2P = 0x0162,
            DIAG_MON_LSB_IN2P = 0x0163,
            DIAG_MON_MSB_IN2M = 0x0164,
            DIAG_MON_LSB_IN2M = 0x0165,

            DIAG_MON_MSB_IN3P = 0x0166,
            DIAG_MON_LSB_IN3P = 0x0167,
            DIAG_MON_MSB_IN3M = 0x0168,
            DIAG_MON_LSB_IN3M = 0x0169,

            DIAG_MON_MSB_IN4P = 0x016A,
            DIAG_MON_LSB_IN4P = 0x016B,
            DIAG_MON_MSB_IN4M = 0x016C,
            DIAG_MON_LSB_IN4M = 0x016D,

            DIAG_MON_MSB_IN5P = 0x016E,
            DIAG_MON_LSB_IN5P = 0x016F,
            DIAG_MON_MSB_IN5M = 0x0170,
            DIAG_MON_LSB_IN5M = 0x0171,

            DIAG_MON_MSB_IN6P = 0x0172,
            DIAG_MON_LSB_IN6P = 0x0173,
            DIAG_MON_MSB_IN6M = 0x0174,
            DIAG_MON_LSB_IN6M = 0x0175,

            DIAG_MON_MSB_TEMP = 0x0176,
            DIAG_MON_LSB_TEMP = 0x0177,

            DIAG_MON_MSB_LOAD = 0x0178,
            DIAG_MON_LSB_LOAD = 0x0179,
        }
    }
}

