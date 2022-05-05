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
    public class TAS6424x : II2CPeripheral, IBytePeripheral, IGPIOReceiver
    {

        public TAS6424x()
        {
            EnablePin = false;
            byteregisters = new ByteRegisterCollection(this);

            Registers.MODECONTROL.Define(byteregisters, 0x00, "MODECONTROL")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.MISCCONTROL1.Define(byteregisters, 0x32, "MISCCONTROL1")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.MISCCONTROL2.Define(byteregisters, 0x62, "MISCCONTROL2")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.SAPCONTROL.Define(byteregisters, 0x04, "SAPCONTROL")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.CHANNELSTATECONTROL.Define(byteregisters, 0x55, "CHANNELSTATECONTROL")
              .WithValueField(0, 2, out ChannelStateControl[3], FieldMode.Read | FieldMode.Write,
                  writeCallback: (_, value) =>
                  {
                      if (value != 0x3)
                      {
                          ChannelStateReport[3].Value = value;
                      }
                  }, name: "Channel4")
              .WithValueField(2, 2, out ChannelStateControl[2], FieldMode.Read | FieldMode.Write,
                  writeCallback: (_, value) =>
                  {
                      if (value != 0x3)
                          ChannelStateReport[2].Value = value;
                  }, name: "Channel3")
              .WithValueField(4, 2, out ChannelStateControl[1], FieldMode.Read | FieldMode.Write,
                  writeCallback: (_, value) =>
                  {
                      if (value != 0x3)
                          ChannelStateReport[1].Value = value;
                  }, name: "Channel 2")
              .WithValueField(6, 2, out ChannelStateControl[0], FieldMode.Read | FieldMode.Write,
                  writeCallback: (_, value) =>
                  {
                      if (value != 0x3)
                          ChannelStateReport[0].Value = value;
                  }, name: "Channel 1");

            Registers.CHANNEL1VOLUME.Define(byteregisters, 0xCF, "CHANNEL1VOLUME")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.CHANNEL2VOLUME.Define(byteregisters, 0xCF, "CHANNEL2VOLUME")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.CHANNEL3VOLUME.Define(byteregisters, 0xCF, "CHANNEL3VOLUME")
             .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.CHANNEL4VOLUME.Define(byteregisters, 0xCF, "CHANNEL4VOLUME")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");


            Registers.DCDIAGNOSTICCONTROL1.Define(byteregisters, 0x00, "DCDIAGNOSTICCONTROL1")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.DCDIAGNOSTICCONTROL2.Define(byteregisters, 0x11, "DCDIAGNOSTICCONTROL2")
              .WithValueField(0, 4, out DCThresholdLow[1], FieldMode.Read | FieldMode.Write, name: "ch2: DC Load Threshold")
              .WithValueField(4, 4, out DCThresholdLow[0], FieldMode.Read | FieldMode.Write, name: "ch1: DC Load Threshold");
            Registers.DCDIAGNOSTICCONTROL3.Define(byteregisters, 0x11, "DCDIAGNOSTICCONTROL3")
              .WithValueField(0, 4, out DCThresholdLow[3], FieldMode.Read | FieldMode.Write, name: "ch4: DC Load Threshold")
              .WithValueField(4, 4, out DCThresholdLow[2], FieldMode.Read | FieldMode.Write, name: "ch3: DC Load Threshold");



            Registers.DCLOADDIAGNOSTICREPORT1.Define(byteregisters, 0x00, "DCLOADDIAGNOSTICREPORT1")
              .WithFlag(0, out ShortedLoad[1], FieldMode.Read, name: "Channel 2: ShortedLoad")
              .WithFlag(1, out OpenLoad[1], FieldMode.Read, name: "Channel 2: OpenLoad")
              .WithFlag(2, out ShortToPower[1], FieldMode.Read, name: "Channel 2: ShortToPower")
              .WithFlag(3, out ShortToGround[1], FieldMode.Read, name: "Channel 2: ShortToGround")
              .WithFlag(4, out ShortedLoad[0], FieldMode.Read, name: "Channel 1: ShortedLoad")
              .WithFlag(5, out OpenLoad[0], FieldMode.Read, name: "Channel 1: OpenLoad")
              .WithFlag(6, out ShortToPower[0], FieldMode.Read, name: "Channel 1: ShortToPower")
              .WithFlag(7, out ShortToGround[0], FieldMode.Read, name: "Channel 1: ShortToGround");

            Registers.DCLOADDIAGNOSTICREPORT2.Define(byteregisters, 0x00, "DCLOADDIAGNOSTICREPORT2")
              .WithFlag(0, out ShortedLoad[3], FieldMode.Read, name: "Channel 4: ShortedLoad")
              .WithFlag(1, out OpenLoad[3], FieldMode.Read, name: "Channel 4: OpenLoad")
              .WithFlag(2, out ShortToPower[3], FieldMode.Read, name: "Channel 4: ShortToPower")
              .WithFlag(3, out ShortToGround[3], FieldMode.Read, name: "Channel 4: ShortToGround")
              .WithFlag(4, out ShortedLoad[2], FieldMode.Read, name: "Channel 3: ShortedLoad")
              .WithFlag(5, out OpenLoad[2], FieldMode.Read, name: "Channel 3: OpenLoad")
              .WithFlag(6, out ShortToPower[2], FieldMode.Read, name: "Channel 3: ShortToPower")
              .WithFlag(7, out ShortToGround[2], FieldMode.Read, name: "Channel 3: ShortToGround");


            Registers.DCLOADDIAGNOSTICREPORT3.Define(byteregisters, 0x00, "DCLOADDIAGNOSTICREPORT3")
              .WithFlag(0, out LineOut[3], FieldMode.Read, name: "Channel 4: Line Output")
              .WithFlag(1, out LineOut[2], FieldMode.Read, name: "Channel 3: Line Output")
              .WithFlag(2, out LineOut[1], FieldMode.Read, name: "Channel 2: Line Output")
              .WithFlag(3, out LineOut[0], FieldMode.Read, name: "Channel 1: Line Output")
              .WithValueField(4, 4, name: "unused");


            Registers.CHANNELSTATEREPORT.Define(byteregisters, 0x55, "CHANNELSTATEREPORT")
              .WithValueField(0, 2, out ChannelStateReport[3], FieldMode.Read, name: "Channel 4")
              .WithValueField(2, 2, out ChannelStateReport[2], FieldMode.Read, name: "Channel 3")
              .WithValueField(4, 2, out ChannelStateReport[1], FieldMode.Read, name: "Channel 2")
              .WithValueField(6, 2, out ChannelStateReport[0], FieldMode.Read, name: "Channel 1");

            Registers.CHANNELFAULTS.Define(byteregisters, 0x00, "CHANNELFAULTS")
              .WithValueField(0, 8, FieldMode.Read, name: "unused");
            Registers.GLOBALFAULTS1.Define(byteregisters, 0x00, "GLOBALFAULTS1")
              .WithValueField(0, 8, FieldMode.Read, name: "unused");
            Registers.GLOBALFAULTS2.Define(byteregisters, 0x00, "GLOBALFAULTS2")
              .WithValueField(0, 8, FieldMode.Read, name: "unused");
            Registers.WARNINGS.Define(byteregisters, 0x20, "WARNINGS")
              .WithValueField(0, 8, FieldMode.Read, name: "unused");
            Registers.PINCONTROL.Define(byteregisters, 0x00, "PINCONTROL")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.ACLOADDIAGNOSTICCONTROL1.Define(byteregisters, 0x00, "ACLOADDIAGNOSTICCONTROL1")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.ACLOADDIAGNOSTICCONTROL2.Define(byteregisters, 0x00, "ACLOADDIAGNOSTICCONTROL2")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");

            Registers.ACLOADDIAGNOSTICREPORT1.Define(byteregisters, 0x00, "ACLOADDIAGNOSTICREPORT1")
              .WithValueField(0, 8, out AcImpedance[0], FieldMode.Read, name: "Channel1: AC Impedance");
            Registers.ACLOADDIAGNOSTICREPORT2.Define(byteregisters, 0x00, "ACLOADDIAGNOSTICREPORT2")
              .WithValueField(0, 8, out AcImpedance[1], FieldMode.Read, name: "Channel2: AC Impedance");
            Registers.ACLOADDIAGNOSTICREPORT3.Define(byteregisters, 0x00, "ACLOADDIAGNOSTICREPORT3")
              .WithValueField(0, 8, out AcImpedance[2], FieldMode.Read, name: "Channel3: AC Impedance");
            Registers.ACLOADDIAGNOSTICREPORT4.Define(byteregisters, 0x00, "ACLOADDIAGNOSTICREPORT4")
              .WithValueField(0, 8, out AcImpedance[3], FieldMode.Read, name: "Channel4: AC Impedance");

            Registers.ACLOADDIAGNOSTICPHASEREPORTHIGH.Define(byteregisters, 0x00, "ACLOADDIAGNOSTICPHASEREPORTHIGH")
              .WithValueField(0, 8, FieldMode.Read, name: "unused");
            Registers.ACLOADDIAGNOSTICPHASEREPORTLOW.Define(byteregisters, 0x00, "ACLOADDIAGNOSTICPHASEREPORTLOW")
              .WithValueField(0, 8, FieldMode.Read, name: "unused");
            Registers.ACLOADDIAGNOSTICSTIREPORTHIGH.Define(byteregisters, 0x00, "ACLOADDIAGNOSTICSTIREPORTHIGH")
              .WithValueField(0, 8, FieldMode.Read, name: "unused");
            Registers.ACLOADDIAGNOSTICSTIREPORTLOW.Define(byteregisters, 0x00, "ACLOADDIAGNOSTICSTIREPORTLOW")
              .WithValueField(0, 8, FieldMode.Read, name: "unused");
            Registers.RESERVED1.Define(byteregisters, 0x00, "RESERVED1")
              .WithValueField(0, 8, name: "unused");
            Registers.RESERVED2.Define(byteregisters, 0x00, "RESERVED2")
              .WithValueField(0, 8, name: "unused");
            Registers.MISCCONTROL3.Define(byteregisters, 0x00, "MISCCONTROL3")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.CLIPCONTROL.Define(byteregisters, 0x01, "CLIPCONTROL")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.CLIPWINDOW.Define(byteregisters, 0x14, "CLIPWINDOW")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.CLIPWARNING.Define(byteregisters, 0x00, "CLIPWARNING")
              .WithValueField(0, 8, FieldMode.Read, name: "unused");
            Registers.ILIMITSTATUS.Define(byteregisters, 0x00, "ILIMITSTATUS")
              .WithValueField(0, 8, FieldMode.Read, name: "unused");
            Registers.MISCCONTROL4.Define(byteregisters, 0x00, "MISCCONTROL4")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.RESERVED3.Define(byteregisters, 0x00, "RESERVED3")
              .WithValueField(0, 8, name: "unused");
            Registers.MISCCONTROL5.Define(byteregisters, 0x0A, "MISCCONTROL5")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.SPREADSPECTRUMCONTROL1.Define(byteregisters, 0x00, "SPREADSPECTRUMCONTROL1")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");
            Registers.SPREADSPECTRUMCONTROL2.Define(byteregisters, 0x3F, "SPREADSPECTRUMCONTROL2")
              .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "unused");

            Reset();

        }



        public Boolean IsChannelPlaying (int channel)
        {
            Boolean bRetVal = false;
            if ((ChannelStateReport[channel].Value == 0x00) & (true == EnablePin) )
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
            ShortToGround[channel].Value = false;
            ShortToPower[channel].Value = true;
            CheckDcLoad(channel);
        }



        public void SetDcLoad(int channel, int milliohm)
        {
            DcLoad[channel] = milliohm;
        }

        private void CheckDcLoad(int channel)
        {
            int lowValue, highValue;

            lowValue = ((int) DCThresholdLow[channel].Value * 500) + 500;
            highValue = 40000;
            OpenLoad[channel].Value = false;
            ShortedLoad[channel].Value = false;

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

        public void SetLineOut(int channel)
        {
            LineOut[channel].Value = true;
        }

        public void SetAcLoad(int channel, long milliohm)
        {
            AcLoad[channel] = milliohm;
        }

        public void SetAcMagnitudeAndPhase(int channel, long magnitude, long phase)
        {
            AcImpedance[channel].Value = (uint) (( magnitude/1000.0) / 0.2496);
//            OutputAcPhase[channel].Value = (uint)phase;
        }


        public void Reset()
        {
            SubAddress = 0x00;
            State = (int)I2CState.Idle;
            byteregisters.Reset();
            DcLoad[0] = 4000;
            DcLoad[1] = 4000;
            DcLoad[2] = 4000;
            DcLoad[3] = 4000;
            AcLoad[0] = 15000;
            AcLoad[1] = 15000;
            AcLoad[2] = 15000;
            AcLoad[3] = 15000;
        }

        public void FinishTransmission()
        {
            this.Log(LogLevel.Noisy, "Finish Transmission");
            State = (int)I2CState.Idle;
        }

        public byte[] Read(int count = 1)
        {
            int Current_Adr = SubAddress;
            var result = new byte[count];

            for (var i = 0; i < result.Length; i++)
            {
                result[i] = ReadByte(SubAddress);
                SubAddress++;
            }
//            this.Log(LogLevel.Noisy, "Read {0} bytes from register {1:X2}: {2}", count, Misc.PrettyPrintCollectionHex(result));
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
                SubAddress = data[0];
                int Print_Adr = SubAddress;
                State = (int)I2CState.TransmissionActive;
                for (var i = 1; i < data.Length; i++)
                {
                    WriteByte (SubAddress, data[i]);
                    SubAddress++;
                }
                this.Log(LogLevel.Noisy, "Write {0} bytes to register {1:X2}: {2}", data.Length - 1, Print_Adr, Misc.PrettyPrintCollectionHex(data));
            }
            else
            {
                this.Log(LogLevel.Noisy, "Write {0} bytes to register {1:X2}: {2}", data.Length, SubAddress, Misc.PrettyPrintCollectionHex(data));
                for (var i = 0; i < data.Length; i++)
                {
                    WriteByte(SubAddress, data[i]);
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
            this.Log(LogLevel.Noisy, "Read  TAS Register 0x{0:X2} 0x{1:X2}", offset, value);

            return value;
        }

        public void WriteByte(long offset, byte value)
        {
            this.Log(LogLevel.Noisy, "Write TAS Register 0x{0:X2} 0x{1:X2}", offset, value);
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

        private int SubAddress = 0x00;
        private int State;
        private readonly ByteRegisterCollection byteregisters;
        private IValueRegisterField [] ChannelStateControl = new IValueRegisterField[4];

        private IValueRegisterField [] ChannelStateReport = new IValueRegisterField[4];
        private IFlagRegisterField [] ShortedLoad = new IFlagRegisterField [4];

        private IFlagRegisterField[] OpenLoad = new IFlagRegisterField[4];

        private IFlagRegisterField[] ShortToGround = new IFlagRegisterField[4];

        private IFlagRegisterField[] ShortToPower = new IFlagRegisterField[4];

        private IValueRegisterField[] AcImpedance = new IValueRegisterField[4];
        private IValueRegisterField[] DCThresholdLow = new IValueRegisterField[4];

        private Boolean EnablePin;
        private IFlagRegisterField[] LineOut = new IFlagRegisterField[4];

        private long[] DcLoad = new long[4];
        private long[] AcLoad = new long[4];



        private enum Registers : long
        {
            MODECONTROL = 0x00,
            MISCCONTROL1 = 0x01,
            MISCCONTROL2 = 0x02,
            SAPCONTROL = 0x03,
            CHANNELSTATECONTROL = 0x04,
            CHANNEL1VOLUME = 0x05,
            CHANNEL2VOLUME = 0x06,
            CHANNEL3VOLUME = 0x07,
            CHANNEL4VOLUME = 0x08,
            DCDIAGNOSTICCONTROL1 = 0x09,
            DCDIAGNOSTICCONTROL2 = 0x0A,
            DCDIAGNOSTICCONTROL3 = 0x0B,
            DCLOADDIAGNOSTICREPORT1 = 0x0C,
            DCLOADDIAGNOSTICREPORT2 = 0x0D,
            DCLOADDIAGNOSTICREPORT3 = 0x0E,
            CHANNELSTATEREPORT = 0x0F,
            CHANNELFAULTS = 0x10,
            GLOBALFAULTS1 = 0x11,
            GLOBALFAULTS2 = 0x12,
            WARNINGS = 0x13,
            PINCONTROL = 0x14,
            ACLOADDIAGNOSTICCONTROL1 = 0x15,
            ACLOADDIAGNOSTICCONTROL2 = 0x16,
            ACLOADDIAGNOSTICREPORT1 = 0x17,
            ACLOADDIAGNOSTICREPORT2 = 0x18,
            ACLOADDIAGNOSTICREPORT3 = 0x19,
            ACLOADDIAGNOSTICREPORT4 = 0x1A,
            ACLOADDIAGNOSTICPHASEREPORTHIGH = 0x1B,
            ACLOADDIAGNOSTICPHASEREPORTLOW = 0x1C,
            ACLOADDIAGNOSTICSTIREPORTHIGH = 0x1D,
            ACLOADDIAGNOSTICSTIREPORTLOW = 0x1E,
            RESERVED1 = 0x1F,
            RESERVED2 = 0x20,
            MISCCONTROL3 = 0x21,
            CLIPCONTROL = 0x22,
            CLIPWINDOW = 0x23,
            CLIPWARNING = 0x24,
            ILIMITSTATUS = 0x25,
            MISCCONTROL4 = 0x26,
            RESERVED3 = 0x27,
            MISCCONTROL5 = 0x28,
            SPREADSPECTRUMCONTROL1 = 0x77,
            SPREADSPECTRUMCONTROL2 = 0x78,

        }
    }
}
