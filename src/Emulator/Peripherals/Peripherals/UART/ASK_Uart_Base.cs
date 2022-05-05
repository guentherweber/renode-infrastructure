//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using System;
using Antmicro.Renode.Utilities;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace Antmicro.Renode.Peripherals.UART
{
    public class ASK_Uart_Base : UARTBase
    {

        public ASK_Uart_Base(Machine machine) : base(machine)
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
            SendRemoteCommandBytes ( HexStringToBytes(values));
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
            data = new byte[] { 0x55, (byte) sendRemoteFifo.Count };
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
        protected uint   datanr= 0;
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
