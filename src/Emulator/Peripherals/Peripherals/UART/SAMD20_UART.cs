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
using Antmicro.Renode.Utilities;
using System.Globalization;

namespace Antmicro.Renode.Peripherals.UART
{
    [AllowedTranslations(AllowedTranslation.WordToDoubleWord | AllowedTranslation.ByteToDoubleWord)]
    public class SAMD20_UART : ASK_Uart_Base_S, IDoubleWordPeripheral, IKnownSize, IWordPeripheral, IBytePeripheral
    {

 //       public new event Action<byte> CharReceived;

        public SAMD20_UART(Machine machine): base(machine)
        {
            IRQ = new GPIO();
            dwordregisters = new DoubleWordRegisterCollection(this);

            DefineRegisters();
        }




        public new long Size => 0x100;

        public GPIO IRQ { get; private set; }

        public override Bits StopBits
        {
            get
            {
                if (bStopBit.Value == true)
                    return Bits.Two;
                else
                    return Bits.One;
            }
        }

        public override Parity ParityBit
        {
            get
            {
                if (bParity.Value==true)
                   return Parity.Odd;
                else
                    return Parity.Even;
            }
        }

        public override uint BaudRate
        {
            get
            {
                return baudrate.Value;
            }
        }



        public override void Reset()
        {
            base.Reset();
            dwordregisters.Reset();
            UpdateInterrupts();
        }

        protected override void SendCharacter(byte character)
        {
            base.SendCharacter(character);
            bRXC.Value = true;
            UpdateInterrupts();
        }
/*
        public override void WriteChar(byte value)
        {
            receiveFifo.Enqueue(value);
            bRXC.Value = true;
            UpdateInterrupts();
        }
*/
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
            return (byte) ReadDoubleWord(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            WriteDoubleWord(offset, value);
        }

        public uint ReadDoubleWord(long offset)
        {
            return (dwordregisters.Read(offset));
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            dwordregisters.Write(offset, value);
            UpdateInterrupts();
        }

        private readonly Queue<byte> receiveFifo = new Queue<byte>();


        private IFlagRegisterField bDRE;
        private IFlagRegisterField bDRE_Clear;
        private IFlagRegisterField bDRE_Set;
        private IFlagRegisterField bTXC;
        private IFlagRegisterField bTXC_Clear;
        private IFlagRegisterField bTXC_Set;
        private IFlagRegisterField bRXC;
        private IFlagRegisterField bRXC_Clear;
        private IFlagRegisterField bRXC_Set;
        private IFlagRegisterField bRXS;
        private IFlagRegisterField bRXS_Clear;
        private IFlagRegisterField bRXS_Set;
        private IFlagRegisterField bParity;
        private IFlagRegisterField bStopBit;
        private IValueRegisterField baudrate;
        private DoubleWordRegisterCollection dwordregisters;

        private void DefineRegisters()
        {

            Register.ControlA.Define(dwordregisters, 0x00, "ControlA")
            .WithFlag(0, name: "Software Reset")
            .WithFlag(1, name: "Enable")
            .WithValueField(2, 3, name: "Mode")
            .WithTag("Reserved", 5, 2)
            .WithFlag(7, name: "Run In Standby")
            .WithFlag(8, name: "Immediate Buffer Overflow Notification")
            .WithTag("Reserved", 9, 7)
            .WithFlag(16, name: "Transmit Data Pinout")
            .WithTag("Reserved", 17, 2)
            .WithValueField(20, 2, name: "Receive Data Pinout")
            .WithTag("Reserved", 22, 2)
            .WithValueField(24, 4, name: "Frame Format")
            .WithFlag(28, name: "Communication Mode")
            .WithFlag(29, name: "Clock Polarity")
            .WithFlag(30, name: "Data Order")
            .WithTag("Reserved", 31, 1);

            Register.ControlB.Define(dwordregisters, 0x00, "ControlB")
                .WithValueField(0, 3, name: "Character Size")
                .WithTag("Reserved", 3, 3)
                .WithFlag(6, out bStopBit, FieldMode.Read | FieldMode.Write, name: "Stop Bit Mode")
                .WithTag("Reserved", 7, 2)
                .WithFlag(9, name: "SFDE")
                .WithTag("Reserved", 10, 3)
                .WithFlag(13, out bParity, FieldMode.Read | FieldMode.Write, name: "PMODE")
                .WithTag("RESERVED", 14, 2)
                .WithFlag(16, name: "TXEN")
                .WithFlag(17, name: "RXEN")
                .WithTag("RESERVED", 18, 14);

            Register.DebugControl.Define(dwordregisters, 0x00, "DebugControl")
                .WithTag("RESERVED", 0, 1);


            Register.Baudrate.Define(dwordregisters, 0x00, "Baudrate")
                .WithValueField(0, 16, out baudrate, FieldMode.Read | FieldMode.Write, name: "Baudraute");

            Register.IntenClr.Define(dwordregisters, 0x00, "IntenClr")
            .WithFlag(0, out bDRE_Clear, FieldMode.Read | FieldMode.Set,
            writeCallback: (_, value) =>
            {
                if (value == true)
                {
                    bDRE_Set.Value = false;
                }
            }, name: "Data Register Empty Interrupt Enable")
            .WithFlag(1, out bTXC_Clear, FieldMode.Read | FieldMode.Set,
            writeCallback: (_, value) =>
            {
                if (value == true)
                {
                    bTXC_Set.Value = false;
                }
            }, name: "Transmit Complete interrupt is disabled.")
            .WithFlag(2, out bRXC_Clear, FieldMode.Read | FieldMode.Set,
            writeCallback: (_, value) =>
            {
                if (value == true)
                {
                    bRXC_Set.Value = false;
                }
            }, name: "Receive Complete Interrupt Enable")
            .WithFlag(3, out bRXS_Clear, FieldMode.Read | FieldMode.Set,
            writeCallback: (_, value) =>
            {
                if (value == true)
                {
                    bRXS_Set.Value = false;
                }
            }, name: "Receive Start Interrupt Enable")
            .WithTag("RESERVED", 4, 4);

            Register.IntenSet.Define(dwordregisters, 0x00, "IntenSet")
            .WithFlag(0, out bDRE_Set, FieldMode.Read | FieldMode.Set,
            writeCallback: (_, value) =>
            {
                if (value == true)
                {
                    bDRE_Clear.Value = false;
                }
            }, name: "Data Register Empty Interrupt Enable")
            .WithFlag(1, out bTXC_Set, FieldMode.Read | FieldMode.Set,
             writeCallback: (_, value) =>
             {
                 if (value == true)
                 {
                     bTXC_Clear.Value = false;
                 }
             }, name: "Transmit Complete interrupt is disabled.")
            .WithFlag(2, out bRXC_Set, FieldMode.Read | FieldMode.Set,
             writeCallback: (_, value) =>
             {
                 if (value == true)
                 {
                     bRXC_Clear.Value = false;
                 }
             }, name: "Receive Complete Interrupt Enable")
            .WithFlag(3, out bRXS_Set, FieldMode.Read | FieldMode.Set,
             writeCallback: (_, value) =>
             {
                 if (value == true)
                 {
                     bRXS_Clear.Value = false;
                 }
             }, name: "Receive Start Interrupt Enable")
            .WithTag("RESERVED", 4, 4);


            Register.IntFlag.Define(dwordregisters, 0x01, "IntFlag")
            .WithFlag(0, out bDRE, FieldMode.Read, name: "Data Register Empty")
            .WithFlag(1, out bTXC, FieldMode.WriteOneToClear | FieldMode.Read, name: "Transmit Complete.")
            .WithFlag(2, out bRXC, FieldMode.Read, name: "Receive Complete")
            .WithFlag(3, out bRXS, FieldMode.WriteOneToClear | FieldMode.Read, name: "Receive Start")
            .WithTag("RESERVED", 4, 4);

            Register.Status.Define(dwordregisters, 0x00, "Status")
            .WithFlag(0, name: "Parity Error")
            .WithFlag(1, name: "Frame Error")
            .WithFlag(2, name: "Buffer Overflow")
            .WithTag("RESERVED", 3, 12)
            .WithFlag(15, name: "Synchronization Busy");

            Register.Data.Define(dwordregisters, 0x00, "Data")
            .WithValueField(0, 9,
                                writeCallback: (_, value) =>
                                {
                                    this.Log(LogLevel.Noisy, "SAMD2x_UART: Data Send");
                                    bDRE.Value = false;
                                    SendCharacter((byte)value);
//                                    CharReceived?.Invoke((byte)value);
                                    bTXC.Value = true;
                                    bDRE.Value = true;
                                },
                                valueProviderCallback: _ =>
                                {
                                    byte value = 0;
/*
                                    if (receiveFifo.Count > 0)
                                    {
                                        value = receiveFifo.Dequeue();
                                        this.Log(LogLevel.Noisy, "SAMD2x_UART: Data Receive");
                                    }
                                    if (receiveFifo.Count == 0)
                                    {
                                        bRXC.Value = false;
                                        this.Log(LogLevel.Noisy, "SAMD2x_UART: RXC = false");
                                        UpdateInterrupts();
                                    }
*/
                                    if (!TryGetCharacter(out value))
                                    {
                                        this.Log(LogLevel.Noisy, "Trying to read data from empty receive fifo");
                                        bRXC.Value = false;
                                        UpdateInterrupts();
                                    }

                                    return value;
                                }, name: "data")
            .WithTag("RESERVED", 9, 7);


        }

        private void UpdateInterrupts()
        {
            bool bDRE_IntActive = false;
            bool bTXC_IntActive = false;
            bool bRXC_IntActive = false;
            bool bRXS_IntActive = false;

            if (bDRE_Set.Value & bDRE.Value)
            {
                bDRE_IntActive = true;
                this.Log(LogLevel.Noisy, "SAMD2x_UART: DRE Int Active");
            }
            else
                this.Log(LogLevel.Noisy, "SAMD2x_UART: DRE Int Off");


            if (bTXC_Set.Value & bTXC.Value)
            {
                bTXC_IntActive = true;
                this.Log(LogLevel.Noisy, "SAMD2x_UART: TXC Int Active");
            }
            else
                this.Log(LogLevel.Noisy, "SAMD2x_UART: TXC Int Off");

            if (bRXC_Set.Value & bRXC.Value)
            {
                bRXC_IntActive = true;
                this.Log(LogLevel.Noisy, "SAMD2x_UART: RXC Int Active");
            }
            else
                this.Log(LogLevel.Noisy, "SAMD2x_UART: RXC Int Off");

            if (bRXS_Set.Value & bRXS.Value)
            {
                bRXS_IntActive = true;
                this.Log(LogLevel.Noisy, "SAMD2x_UART: RXS Int Active");
            }
            else
                this.Log(LogLevel.Noisy, "SAMD2x_UART: RXS Int Off");

            // Set or Clear Interrupt
            IRQ.Set(bRXS_IntActive | bRXC_IntActive | bTXC_IntActive | bDRE_IntActive);

        }


        private enum Register : long
        {
            ControlA= 0x00,
            ControlB= 0x04,
            DebugControl = 0x08,
            Baudrate =0x0A,
            IntenClr = 0x0C,
            IntenSet = 0x0D,
            IntFlag = 0x0E,
            Status = 0x10,
            Data = 0x18
        }


        protected override void CharWritten()
        {
            //            throw new NotImplementedException();
        }

        protected override void QueueEmptied()
        {
            this.Log(LogLevel.Noisy, "Queue Emptied");
//            bRXC.Value = false;
//            UpdateInterrupts();           //            throw new NotImplementedException();
        }

    }

    public class ASK_Uart_Base_S : UARTBase
    {

        public ASK_Uart_Base_S(Machine machine) : base(machine)
        {
            sendRemoteFifo = new Queue<byte[]>();
        }




        public void SendRemoteCommandBytes(byte[] values)
        {
            Boolean bcrc = true;

            this.Log(LogLevel.Noisy, "Send Remote Command crc={0} msg:{1}", bcrc, Misc.PrettyPrintCollectionHex(values));
            uint crc;
            WriteChar(0x1B);  // Escape
            WriteChar(0x00);  // Channel ID 
            if (bcrc)
                WriteChar(0x01);  // Protocol ID with CRC
            else
                WriteChar(0x41);  // Protocol ID without CRC

            WriteChar((byte)(values.Length & 0x00FF));        // length low
            WriteChar((byte)((values.Length & 0xFF00) >> 8));  // length high
            for (int i = 0; i < values.Length; i++)
            {
                WriteChar((byte)values[i]);
            }
            if (bcrc)
            {
                crc = calccrc16(values);
                WriteChar((byte)(crc & 0x00FF));         // crc low
                WriteChar((byte)((crc & 0xFF00) >> 8));  // crc high
            }

        }

        public void SendRemoteCommandHexString(string values)
        {
            SendRemoteCommandBytes(HexStringToBytes(values));
        }


        private byte[] HexStringToBytes(string data)
        {
            if (data.Length % 2 == 1)
            {
                data = "0" + data;
            }

            var bytes = new byte[data.Length / 2];
            for (var i = 0; i < bytes.Length; ++i)
            {
                if (!Byte.TryParse(data.Substring(i * 2, 2), NumberStyles.HexNumber, null, out bytes[i]))
                {
                    throw new ArgumentException($"Data not in hex format at index {i * 2} (\"{data.Substring(i * 2, 2)}\")");
                }
            }

            return bytes;
        }

        // 0	ServiceID True	1	The ID of the requested service
        // 1	MethodID True	1	The ID for the requested method
        // 2	Payload  LSB first

        //            0   HandshakeAnswer(always 0x00)   True    1   Indicates the Answer as Handshake Answer
        //            1   ServiceID True    1   The ID for the requested service
        //            2   MethodID    True    1   The ID for the requested method
        //            3   Status Code True    1   Indicates if the method is executed successfully or if an error occurred.
        //            4   PayloadLength True    2   which length has the remote payload  LSB
        //            5   Payload MSB
        //            6   Payload


        public byte[] GetRemoteAnswerBytes()
        {
            byte[] data;
            if (sendRemoteFifo.Count > 0)
            {
                data = sendRemoteFifo.Dequeue();
                this.Log(LogLevel.Noisy, "Get Remote Command: {0}", Misc.PrettyPrintCollectionHex(data));
                return (data);
            }
            data = new byte[] { 0x55, (byte)sendRemoteFifo.Count };
            return data;
        }

        public string GetRemoteAnswerHexString()
        {
            byte[] data;

            if (sendRemoteFifo.Count > 0)
            {
                data = sendRemoteFifo.Dequeue();
                string hex = BitConverter.ToString(data);
                hex = hex.Replace("-", "");
                this.Log(LogLevel.Noisy, "Get Remote Command: {0}", Misc.PrettyPrintCollectionHex(data));
                String modified = hex.Insert(0, "\"");
                modified += '"';
                return modified;
            }
            return "";
        }

        public override void Reset()
        {
            sendRemoteFifo.Clear();
            base.Reset();
        }

        private Queue<byte[]> sendRemoteFifo;
        protected int remotestate = 0;
        protected byte protocolid = 0;
        protected UInt16 length = 0;
        protected UInt16 crc = 0;
        protected byte[] msgdata;
        protected uint datanr = 0;
        protected Boolean withcrc;



        protected virtual void SendCharacter(byte character)
        {
            base.TransmitCharacter(character);
            DecodeRemoteAnswer(character);
        }

        protected void DecodeRemoteAnswer(byte value)
        {
            switch (remotestate)
            {
                case 0:                     // escape
                    if (value == 0x1B)
                        remotestate = 1;
                    break;

                case 1:                    // channel id
                    remotestate = 2;
                    break;

                case 2:                    // protocol id
                    remotestate = 3;
                    protocolid = value;
                    if (protocolid == 0x41)
                        withcrc = false;
                    else if (protocolid == 0x01)
                        withcrc = true;
                    else
                        remotestate = 0;
                    break;

                case 3:                    // length low
                    length = value;
                    remotestate = 4;
                    break;

                case 4:                    // length high
                    length |= (UInt16)(value << 8);
                    msgdata = new byte[length];
                    datanr = 0;
                    if (withcrc == true)
                        remotestate = 5;
                    else
                        remotestate = 7;
                    break;

                case 5:                    // data
                    msgdata[datanr++] = value;
                    if (datanr == length)
                    {
                        if (withcrc == true)
                            remotestate = 6;
                        else
                        {
                            this.Log(LogLevel.Noisy, "Add Remote Answer without CRC: {0}", Misc.PrettyPrintCollectionHex(msgdata));
                            sendRemoteFifo.Enqueue(msgdata);
                            remotestate = 0;
                        }
                    }
                    break;

                case 6:                    // crc low
                    crc = value;
                    remotestate = 7;
                    break;

                case 7:                    // crc high
                    crc |= (UInt16)(value << 8);
                    if (crc == calccrc16(msgdata))
                    {
                        this.Log(LogLevel.Noisy, "Add Remote Answer with CRC: {0}", Misc.PrettyPrintCollectionHex(msgdata));
                        sendRemoteFifo.Enqueue(msgdata);
                    }
                    else
                        this.Log(LogLevel.Warning, "CRC of send Remote Message invalid");
                    remotestate = 0;
                    break;
            }

        }


        protected UInt16 calccrc16(byte[] payload)
        {
            UInt16 crc = 0x63CB;
            for (int i = 0; i < payload.Length; i++)
                crc = (UInt16)(crc16_lookup[(crc ^ payload[i]) & 0xFF] ^ (crc >> 8));
            return crc;
        }

        protected override void CharWritten()
        {
            throw new NotImplementedException();
        }

        protected override void QueueEmptied()
        {
            throw new NotImplementedException();
        }


        protected List<UInt16> crc16_lookup = new List<UInt16>()
        {
            0x0000, 0x1189, 0x2312, 0x329B,
            0x4624, 0x57AD, 0x6536, 0x74BF,
            0x8C48, 0x9DC1, 0xAF5A, 0xBED3,
            0xCA6C, 0xDBE5, 0xE97E, 0xF8F7,
            0x1081, 0x0108, 0x3393, 0x221A,
            0x56A5, 0x472C, 0x75B7, 0x643E,
            0x9CC9, 0x8D40, 0xBFDB, 0xAE52,
            0xDAED, 0xCB64, 0xF9FF, 0xE876,
            0x2102, 0x308B, 0x0210, 0x1399,
            0x6726, 0x76AF, 0x4434, 0x55BD,
            0xAD4A, 0xBCC3, 0x8E58, 0x9FD1,
            0xEB6E, 0xFAE7, 0xC87C, 0xD9F5,
            0x3183, 0x200A, 0x1291, 0x0318,
            0x77A7, 0x662E, 0x54B5, 0x453C,
            0xBDCB, 0xAC42, 0x9ED9, 0x8F50,
            0xFBEF, 0xEA66, 0xD8FD, 0xC974,
            0x4204, 0x538D, 0x6116, 0x709F,
            0x0420, 0x15A9, 0x2732, 0x36BB,
            0xCE4C, 0xDFC5, 0xED5E, 0xFCD7,
            0x8868, 0x99E1, 0xAB7A, 0xBAF3,
            0x5285, 0x430C, 0x7197, 0x601E,
            0x14A1, 0x0528, 0x37B3, 0x263A,
            0xDECD, 0xCF44, 0xFDDF, 0xEC56,
            0x98E9, 0x8960, 0xBBFB, 0xAA72,
            0x6306, 0x728F, 0x4014, 0x519D,
            0x2522, 0x34AB, 0x0630, 0x17B9,
            0xEF4E, 0xFEC7, 0xCC5C, 0xDDD5,
            0xA96A, 0xB8E3, 0x8A78, 0x9BF1,
            0x7387, 0x620E, 0x5095, 0x411C,
            0x35A3, 0x242A, 0x16B1, 0x0738,
            0xFFCF, 0xEE46, 0xDCDD, 0xCD54,
            0xB9EB, 0xA862, 0x9AF9, 0x8B70,
            0x8408, 0x9581, 0xA71A, 0xB693,
            0xC22C, 0xD3A5, 0xE13E, 0xF0B7,
            0x0840, 0x19C9, 0x2B52, 0x3ADB,
            0x4E64, 0x5FED, 0x6D76, 0x7CFF,
            0x9489, 0x8500, 0xB79B, 0xA612,
            0xD2AD, 0xC324, 0xF1BF, 0xE036,
            0x18C1, 0x0948, 0x3BD3, 0x2A5A,
            0x5EE5, 0x4F6C, 0x7DF7, 0x6C7E,
            0xA50A, 0xB483, 0x8618, 0x9791,
            0xE32E, 0xF2A7, 0xC03C, 0xD1B5,
            0x2942, 0x38CB, 0x0A50, 0x1BD9,
            0x6F66, 0x7EEF, 0x4C74, 0x5DFD,
            0xB58B, 0xA402, 0x9699, 0x8710,
            0xF3AF, 0xE226, 0xD0BD, 0xC134,
            0x39C3, 0x284A, 0x1AD1, 0x0B58,
            0x7FE7, 0x6E6E, 0x5CF5, 0x4D7C,
            0xC60C, 0xD785, 0xE51E, 0xF497,
            0x8028, 0x91A1, 0xA33A, 0xB2B3,
            0x4A44, 0x5BCD, 0x6956, 0x78DF,
            0x0C60, 0x1DE9, 0x2F72, 0x3EFB,
            0xD68D, 0xC704, 0xF59F, 0xE416,
            0x90A9, 0x8120, 0xB3BB, 0xA232,
            0x5AC5, 0x4B4C, 0x79D7, 0x685E,
            0x1CE1, 0x0D68, 0x3FF3, 0x2E7A,
            0xE70E, 0xF687, 0xC41C, 0xD595,
            0xA12A, 0xB0A3, 0x8238, 0x93B1,
            0x6B46, 0x7ACF, 0x4854, 0x59DD,
            0x2D62, 0x3CEB, 0x0E70, 0x1FF9,
            0xF78F, 0xE606, 0xD49D, 0xC514,
            0xB1AB, 0xA022, 0x92B9, 0x8330,
            0x7BC7, 0x6A4E, 0x58D5, 0x495C,
            0x3DE3, 0x2C6A, 0x1EF1, 0x0F78
        };

        public override Bits StopBits => throw new NotImplementedException();

        public override Parity ParityBit => throw new NotImplementedException();

        public override uint BaudRate => throw new NotImplementedException();

        public long Size => throw new NotImplementedException();
    }

}