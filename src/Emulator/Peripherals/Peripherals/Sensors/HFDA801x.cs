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
    public class HFDA801x : II2CPeripheral, IBytePeripheral, IGPIOReceiver
    {
        private int SubAddress = 0x00;
        private int State;
        private readonly ByteRegisterCollection byteregisters;
        private IFlagRegisterField [] ShortedLoad = new IFlagRegisterField[4];
        private IFlagRegisterField[] OpenLoad = new IFlagRegisterField[4];
        private IFlagRegisterField[] ShortToGround = new IFlagRegisterField[4];
        private IFlagRegisterField[] ShortToPower = new IFlagRegisterField[4];
        private IFlagRegisterField[] Play = new IFlagRegisterField[4];
        private IFlagRegisterField[] TweeterError = new IFlagRegisterField[4];
        private IFlagRegisterField [] ChannelStateReport = new IFlagRegisterField[4];
        private IValueRegisterField[] OutputDcLoad = new IValueRegisterField[4];
        private IValueRegisterField[] OutputAcMagnitude = new IValueRegisterField[4];
        private IValueRegisterField[] OutputAcPhase = new IValueRegisterField[4];


        private Boolean bIncrement = false;
        private IFlagRegisterField Ldcs;
        private IFlagRegisterField Ldol;

        private long [] DcLoad = new long[4];
        private long[] AcLoad = new long[4];


        //        Boolean bIncrement;

        private Boolean EnablePin;

        public HFDA801x()
        {
            EnablePin = false;
            DcLoad[0] = 4000;
            DcLoad[1] = 4000;
            DcLoad[2] = 4000;
            DcLoad[3] = 4000;
            AcLoad[0] = 15000;
            AcLoad[1] = 15000;
            AcLoad[2] = 15000;
            AcLoad[3] = 15000;

            byteregisters = new ByteRegisterCollection(this);

            Registers.IBS_ADDR_IB0.Define(byteregisters, 0x00, "IB0")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.IBS_ADDR_IB1.Define(byteregisters, 0x00, "IB1")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.IBS_ADDR_IB2.Define(byteregisters, 0x00, "IB2")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.IBS_ADDR_IB3.Define(byteregisters, 0x00, "IB3")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.IBS_ADDR_IB4.Define(byteregisters, 0x01, "IB4")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.IBS_ADDR_IB5.Define(byteregisters, 0x00, "IB5")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.IBS_ADDR_IB6.Define(byteregisters, 0x00, "IB6")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.IBS_ADDR_IB7.Define(byteregisters, 0x00, "IB7")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.IBS_ADDR_IB8.Define(byteregisters, 0x00, "IB8")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.IBS_ADDR_IB9.Define(byteregisters, 0x00, "IB9")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.IBS_ADDR_IB10.Define(byteregisters, 0x00, "IB10")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.IBS_ADDR_IB11.Define(byteregisters, 0x10, "IB11")
              .WithFlag(7, out Ldcs, FieldMode.Read | FieldMode.Write, name: "Ldcsl")
              .WithFlag(6, out Ldol, FieldMode.Read | FieldMode.Write, name: "Ldol")
              .WithValueField(0, 6, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.IBS_ADDR_IB12.Define(byteregisters, 0x00, "IB12")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.IBS_ADDR_IB13.Define(byteregisters, 0x00, "IB13")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.IBS_ADDR_IB14.Define(byteregisters, 0x00, "IB14")
              .WithFlag(0, out Play[0], FieldMode.Read | FieldMode.Write,
                              writeCallback: (_, value) =>
                              {
                                  ChannelStateReport[0].Value = value;
                              }, name: "Channel 1: Play")
              .WithValueField(1, 7, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.IBS_ADDR_IB15.Define(byteregisters, 0x00, "IB15")
              .WithFlag(0, out Play[1], FieldMode.Read | FieldMode.Write,
                              writeCallback: (_, value) =>
                              {
                                  ChannelStateReport[1].Value = value;
                              }, name: "Channel 2: Play")
              .WithValueField(1, 7, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.IBS_ADDR_IB16.Define(byteregisters, 0x00, "IB16")
              .WithFlag(0, out Play[2], FieldMode.Read | FieldMode.Write,
                              writeCallback: (_, value) =>
                              {
                                  ChannelStateReport[2].Value = value;
                              }, name: "Channel 3: Play")
              .WithValueField(1, 7, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.IBS_ADDR_IB17.Define(byteregisters, 0x00, "IB17")
              .WithFlag(0, out Play[3], FieldMode.Read | FieldMode.Write,

                              writeCallback: (_, value) =>
                              {
                                  ChannelStateReport[3].Value = value;
                              }, name: "Channel 4: Play")
              .WithValueField(1, 7, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.IBS_ADDR_IB18.Define(byteregisters, 0x01, "IB18")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.IBS_ADDR_IB19.Define(byteregisters, 0x98, "IB19")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.IBS_ADDR_IB20.Define(byteregisters, 0x00, "IB20")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.IBS_ADDR_IB21.Define(byteregisters, 0x00, "IB21")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.IBS_ADDR_IB22.Define(byteregisters, 0x00, "IB22")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.IBS_ADDR_IB23.Define(byteregisters, 0x08, "IB23")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.IBS_ADDR_IB24.Define(byteregisters, 0x00, "IB24")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.IBS_ADDR_IB25.Define(byteregisters, 0x00, "IB25")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.IBS_ADDR_IB26.Define(byteregisters, 0x00, "IB26")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");



            Registers.IBS_ADDR_DB0.Define(byteregisters, 0x00, "DB0")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.IBS_ADDR_DB1.Define(byteregisters, 0x00, "DB1")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");

            Registers.IBS_ADDR_DB2.Define(byteregisters, 0xC0, "DB2")
              .WithFlag(0, out ChannelStateReport[0], FieldMode.Read | FieldMode.Set, name: "Channel 1: Play")
              .WithFlag(1, out OpenLoad[0], FieldMode.Read | FieldMode.Set, name: "Channel 1: OpenLoad")
              .WithFlag(2, out ShortToGround[0], FieldMode.Read | FieldMode.Set, name: "Channel 1: ShortToGround")
              .WithFlag(3, out ShortToPower[0], FieldMode.Read | FieldMode.Set, name: "Channel 1: ShortToPower")
              .WithFlag(4, out ShortedLoad[0], FieldMode.Read | FieldMode.Set, name: "Channel 1: ShortedLoad")
              .WithValueField(5, 3, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.IBS_ADDR_DB3.Define(byteregisters, 0xC0, "DB3")
              .WithFlag(0, out ChannelStateReport[1], FieldMode.Read | FieldMode.Set, name: "Channel 2: Play")
              .WithFlag(1, out OpenLoad[1], FieldMode.Read | FieldMode.Set, name: "Channel 2: OpenLoad")
              .WithFlag(2, out ShortToGround[1], FieldMode.Read | FieldMode.Set, name: "Channel 2: ShortToGround")
              .WithFlag(3, out ShortToPower[1], FieldMode.Read | FieldMode.Set, name: "Channel 2: ShortToPower")
              .WithFlag(4, out ShortedLoad[1], FieldMode.Read | FieldMode.Set, name: "Channel 2: ShortedLoad")
              .WithValueField(5, 3, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.IBS_ADDR_DB4.Define(byteregisters, 0xC0, "DB4")
              .WithFlag(0, out ChannelStateReport[2], FieldMode.Read | FieldMode.Set, name: "Channel 3: Play")
              .WithFlag(1, out OpenLoad[2], FieldMode.Read | FieldMode.Set, name: "Channel 3: OpenLoad")
              .WithFlag(2, out ShortToGround[2], FieldMode.Read | FieldMode.Set, name: "Channel 3: ShortToGround")
              .WithFlag(3, out ShortToPower[2], FieldMode.Read | FieldMode.Set, name: "Channel 3: ShortToPower")
              .WithFlag(4, out ShortedLoad[2], FieldMode.Read | FieldMode.Set, name: "Channel 3: ShortedLoad")
              .WithValueField(5, 3, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.IBS_ADDR_DB5.Define(byteregisters, 0xC0, "DB5")
              .WithFlag(0, out ChannelStateReport[3], FieldMode.Read | FieldMode.Set, name: "Channel 4: Play")
              .WithFlag(1, out OpenLoad[3], FieldMode.Read | FieldMode.Set, name: "Channel 4: OpenLoad")
              .WithFlag(2, out ShortToGround[3], FieldMode.Read | FieldMode.Set, name: "Channel 4: ShortToGround")
              .WithFlag(3, out ShortToPower[3], FieldMode.Read | FieldMode.Set, name: "Channel 4: ShortToPower")
              .WithFlag(4, out ShortedLoad[3], FieldMode.Read | FieldMode.Set, name: "Channel 4: ShortedLoad")
              .WithValueField(5, 3, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.IBS_ADDR_DB6.Define(byteregisters, 0xCC, "DB6")
              .WithFlag(0, FieldMode.Read | FieldMode.Set, name: "unused")
              .WithFlag(1, out TweeterError[0], FieldMode.Read | FieldMode.Set, name: "Channel 1: TweeterError")
              .WithValueField(2, 3, FieldMode.Read | FieldMode.Write, name: "unused")
              .WithFlag(5, out TweeterError[1], FieldMode.Read | FieldMode.Set, name: "Channel 2: TweeterError")
              .WithValueField(6, 2, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.IBS_ADDR_DB7.Define(byteregisters, 0xCC, "DB7")
              .WithFlag(0, FieldMode.Read | FieldMode.Set, name: "unused")
              .WithFlag(1, out TweeterError[2], FieldMode.Read | FieldMode.Set, name: "Channel 3: TweeterError")
              .WithValueField(2, 3, FieldMode.Read | FieldMode.Write, name: "unused")
              .WithFlag(5, out TweeterError[3], FieldMode.Read | FieldMode.Set, name: "Channel 4: TweeterError")
              .WithValueField(6, 2, FieldMode.Read | FieldMode.Write, name: "unused");

            Registers.IBS_ADDR_DB8.Define(byteregisters, 0x00, "DB8")
              .WithValueField(0, 8, out OutputDcLoad[0], FieldMode.Read | FieldMode.Write, name: "OutputDcLoad");
            Registers.IBS_ADDR_DB9.Define(byteregisters, 0x00, "DB9")
              .WithValueField(0, 8, out OutputAcMagnitude[0], FieldMode.Read | FieldMode.Write, name: "OutputAcMagnitude");
            Registers.IBS_ADDR_DB10.Define(byteregisters, 0x00, "DB10")
              .WithValueField(0, 8, out OutputAcPhase[0], FieldMode.Read | FieldMode.Write, name: "OutputAcPhase");

            Registers.IBS_ADDR_DB11.Define(byteregisters, 0x00, "DB11")
              .WithValueField(0, 8, out OutputDcLoad[1], FieldMode.Read | FieldMode.Write, name: "OutputDcLoad");
            Registers.IBS_ADDR_DB12.Define(byteregisters, 0x00, "DB12")
              .WithValueField(0, 8, out OutputAcMagnitude[1], FieldMode.Read | FieldMode.Write, name: "OutputAcMagnitude");
            Registers.IBS_ADDR_DB13.Define(byteregisters, 0x00, "DB13")
              .WithValueField(0, 8, out OutputAcPhase[1], FieldMode.Read | FieldMode.Write, name: "OutputAcPhase");

            Registers.IBS_ADDR_DB14.Define(byteregisters, 0x00, "DB14")
              .WithValueField(0, 8, out OutputDcLoad[2], FieldMode.Read | FieldMode.Write, name: "OutputDcLoad");
            Registers.IBS_ADDR_DB15.Define(byteregisters, 0x00, "DB15")
              .WithValueField(0, 8, out OutputAcMagnitude[2], FieldMode.Read | FieldMode.Write, name: "OutputAcMagnitude");
            Registers.IBS_ADDR_DB16.Define(byteregisters, 0x00, "DB16")
              .WithValueField(0, 8, out OutputAcPhase[2], FieldMode.Read | FieldMode.Write, name: "OutputAcPhase");

            Registers.IBS_ADDR_DB17.Define(byteregisters, 0x00, "DB17")
              .WithValueField(0, 8, out OutputDcLoad[3], FieldMode.Read | FieldMode.Write, name: "OutputDcLoad");
            Registers.IBS_ADDR_DB18.Define(byteregisters, 0x00, "DB18")
              .WithValueField(0, 8, out OutputAcMagnitude[3], FieldMode.Read | FieldMode.Write, name: "OutputAcMagnitude");
            Registers.IBS_ADDR_DB19.Define(byteregisters, 0x00, "DB19")
              .WithValueField(0, 8, out OutputAcPhase[3], FieldMode.Read | FieldMode.Write, name: "OutputAcPhase");

            Registers.IBS_ADDR_DB20.Define(byteregisters, 0x00, "DB20")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.IBS_ADDR_DB21.Define(byteregisters, 0x00, "DB21")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.IBS_ADDR_DB22.Define(byteregisters, 0x00, "DB22")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.IBS_ADDR_DB23.Define(byteregisters, 0x00, "DB23")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.IBS_ADDR_DB24.Define(byteregisters, 0x00, "DB24")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.IBS_ADDR_DB25.Define(byteregisters, 0x00, "DB25")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");

            Reset();

        }


        public Boolean IsChannelPlaying(int channel)
        {
            Boolean bRetVal = false;

            if ((ChannelStateReport[channel].Value == true) & (true == EnablePin))
                bRetVal = true;
            return bRetVal;
        }

        public void ClearError(int channel)
        {
            ShortToGround[channel].Value = false;
            ShortToPower[channel].Value = false;
            CheckDcLoad(channel);

        }
        public void SetShortcutToGround(int channel)
        {
            ShortToGround[channel].Value = true;
            ShortToPower[channel].Value = false;
            CheckDcLoad(channel);
        }

        public void SetShortcutToPower(int channel)
        {
            ShortToPower[channel].Value = true;
            ShortToGround[channel].Value = false;
            CheckDcLoad(channel);
        }


        public void SetAcLoad(int channel, long milliohm)
        {
            AcLoad[channel] = milliohm;
        }

        public void SetAcMagnitudeAndPhase(int channel, long magnitude, long phase)
        {
            OutputAcMagnitude[channel].Value = (uint) magnitude / 1000;    //scale to ohm
            OutputAcPhase[channel].Value = (uint) phase;
        }

        public void SetDcLoad(int channel, long milliohm)
        {
            DcLoad[channel] = milliohm;
        }

        private void CheckDcLoad(int channel)
        {
            //Ldcsl  DC diagnostic, short load IB11-d7=0    0.6 0.75 0.9   ohm
            //Ldcsl  DC diagnostic, short load IB11-d7=1    0.38 0.5 0.62  ohm 
            //Ldol  DC diagnostic, open load IB11-d6=0    19.0 25.0 31.0   ohm 
            //Ldol  DC diagnostic, open load IB11-d6=1    11.0 15.0 18.0   ohm 
            int lowValue, highValue;
            OpenLoad[channel].Value = false;
            ShortedLoad[channel].Value = false;

            if (Ldcs.Value == false)
                lowValue = 600;
            else
                lowValue = 380;
            if (Ldol.Value == false)
                highValue = 31000;
            else
                highValue = 18000;

            if (DcLoad[channel] < lowValue)
            {
                ShortedLoad[channel].Value = true;
                this.Log(LogLevel.Noisy, "Shorted Load channel {0}", channel);
            }
            else if (DcLoad[channel] > highValue)
            { 
                OpenLoad[channel].Value = true;
                this.Log(LogLevel.Noisy, "OpenLoad Load channel {0}", channel);
            }

            if (ShortToPower[channel].Value == true)
            {
                this.Log(LogLevel.Noisy, "ShortToPower channel {0}", channel);
                ShortedLoad[channel].Value = false;
                OpenLoad[channel].Value = false;
            }

            if (ShortToGround[channel].Value == true)
            {
                this.Log(LogLevel.Noisy, "ShortToGround channel {0}", channel);
                ShortedLoad[channel].Value = false;
                OpenLoad[channel].Value = false;
            }
        }

        private void CheckAcLoad(int channel)
        {

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

//            this.Log(LogLevel.Noisy, "Reading {0} bytes", count );
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
            for (int i = 0; i < 4; i++)
            {
                CheckDcLoad(i);
                CheckAcLoad(i);
            }

            if ( (State == (int) I2CState.Idle) && data.Length >= 1)
            {
                var test = data[0] & 0x80;

                if (test == 0x80)
                    bIncrement = true;
                else
                    bIncrement = false;

                SubAddress = data[0] & 0x7F;
                State = (int)I2CState.TransmissionActive;
                this.Log(LogLevel.Noisy, "New SubAdress {0} Increment {1} datalength: {2}", SubAddress, bIncrement, data.Length);
                for (var i = 1; i < data.Length; i++)
                {
                    WriteByte (SubAddress, data[i]);
                    if (bIncrement)
                        SubAddress++;
                }
            }
            else
            {
                this.Log(LogLevel.Noisy, "Write SubAdress {0} Increment {1} datalength: {2}", SubAddress, bIncrement, data.Length);
                for (var i = 0; i < data.Length; i++)
                {
                    WriteByte(SubAddress, data[i]);
                    if (bIncrement)
                       SubAddress++;
                }

            }


        }



        private enum I2CState
        {
            Idle = 0x00,
            TransmissionActive = 0x01,
        }


        public byte ReadByte(long offset)
        {
            byte value;

            value = byteregisters.Read(offset);
            this.Log(LogLevel.Noisy, "Read  Register 0x{0:X2} 0x{1:X2}", offset, value);

            return value;
        }

        public void WriteByte(long offset, byte value)
        {
            this.Log(LogLevel.Noisy, "Write  Register 0x{0:X2} 0x{1:X2}", offset, value);
            byteregisters.Write(offset, value);
        }

        public void OnGPIO(int number, bool value)
        {
            EnablePin = value;
            if (number != 0)
            {
                // only 0 as number is allowed
                throw new ArgumentOutOfRangeException();
            }
        }

        private enum Registers : long
        {

            IBS_ADDR_IB0 = 0x00,
            IBS_ADDR_IB1 = 0x01,
            IBS_ADDR_IB2 = 0x02,
            IBS_ADDR_IB3 = 0x03,
            IBS_ADDR_IB4 = 0x04,
            IBS_ADDR_IB5 = 0x05,
            IBS_ADDR_IB6 = 0x06,
            IBS_ADDR_IB7 = 0x07,
            IBS_ADDR_IB8 = 0x08,
            IBS_ADDR_IB9 = 0x09,
            IBS_ADDR_IB10 = 0x0A,
            IBS_ADDR_IB11 = 0x0B,
            IBS_ADDR_IB12 = 0x0C,
            IBS_ADDR_IB13 = 0x0D,
            IBS_ADDR_IB14 = 0x0E,
            IBS_ADDR_IB15 = 0x0F,
            IBS_ADDR_IB16 = 0x10,
            IBS_ADDR_IB17 = 0x11,
            IBS_ADDR_IB18 = 0x12,
            IBS_ADDR_IB19 = 0x13,
            IBS_ADDR_IB20 = 0x14,
            IBS_ADDR_IB21 = 0x15,
            IBS_ADDR_IB22 = 0x16,
            IBS_ADDR_IB23 = 0x17,
            IBS_ADDR_IB24 = 0x18,
            IBS_ADDR_IB25 = 0x19,
            IBS_ADDR_IB26 = 0x1A,


            IBS_ADDR_DB0 = 0x20,
            IBS_ADDR_DB1 = 0x21,
            IBS_ADDR_DB2 = 0x22,
            IBS_ADDR_DB3 = 0x23,
            IBS_ADDR_DB4 = 0x24,
            IBS_ADDR_DB5 = 0x25,
            IBS_ADDR_DB6 = 0x26,
            IBS_ADDR_DB7 = 0x27,
            IBS_ADDR_DB8 = 0x28,
            IBS_ADDR_DB9 = 0x29,

            IBS_ADDR_DB10 = 0x2A,
            IBS_ADDR_DB11 = 0x2B,
            IBS_ADDR_DB12 = 0x2C,
            IBS_ADDR_DB13 = 0x2D,
            IBS_ADDR_DB14 = 0x2E,
            IBS_ADDR_DB15 = 0x2F,
            IBS_ADDR_DB16 = 0x30,
            IBS_ADDR_DB17 = 0x31,
            IBS_ADDR_DB18 = 0x32,
            IBS_ADDR_DB19 = 0x33,

            IBS_ADDR_DB20 = 0x34,
            IBS_ADDR_DB21 = 0x35,
            IBS_ADDR_DB22 = 0x36,
            IBS_ADDR_DB23 = 0x37,
            IBS_ADDR_DB24 = 0x38,
            IBS_ADDR_DB25 = 0x39

        }
    }
}
