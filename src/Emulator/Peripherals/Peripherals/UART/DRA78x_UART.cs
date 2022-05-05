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
using System.Collections;
using System.Globalization;

namespace Antmicro.Renode.Peripherals.UART
{
    [AllowedTranslations(AllowedTranslation.WordToDoubleWord | AllowedTranslation.ByteToDoubleWord)]
    public class DRA78x_UART : ASK_Uart_Base_J, IKnownSize, IDoubleWordPeripheral, IWordPeripheral, IBytePeripheral
    {





        public GPIO IRQ { get; private set; }
        public new long Size => 214;
        private readonly object UpdateInterruptsLock;



        public DRA78x_UART(Machine machine) : base(machine)
        {
            IRQ = new GPIO();
            UpdateInterruptsLock = new object();
            dwordregisters = new DoubleWordRegisterCollection(this);
            DefineRegisters();
            Reset();
        }




        public byte ReadByte(long offset)
        {
            return (byte)ReadDoubleWord(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            WriteDoubleWord(offset, value);
        }

        public ushort ReadWord(long offset)
        {
            return (ushort)ReadDoubleWord(offset);
        }

        public void WriteWord(long offset, ushort value)
        {
            WriteDoubleWord(offset, value);
        }

        public uint ReadDoubleWord(long offset)
        {
            return dwordregisters.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            dwordregisters.Write(offset, value);
        }


        public new void Reset()
        {
            dwordregisters.Reset();
            base.Reset();
            UpdateInterrupts();
        }

        private readonly DoubleWordRegisterCollection dwordregisters;
        private IFlagRegisterField bRX_FIFO_E;
        private IValueRegisterField IntEnable;
//        private IFlagRegisterField TxIntEnable;
        private IValueRegisterField IT_TYPE;
        private IFlagRegisterField IT_PENDING;
        private IFlagRegisterField bTxFifoFull;
        private bool bReceiveIrqActive = false;
        private bool bTransmitIrqActive = false;
        private void DefineRegisters()
        {

            Register.UART_RTHR.Define(dwordregisters, 0x00, "UART_THR")
            .WithValueField(0, 8,
                                writeCallback: (_, value) =>
                                {
                                    SendByteToRenode((byte)value);
                                    this.Log(LogLevel.Noisy, "Tx Write Value");
                                },
                                valueProviderCallback: _ =>
                                {
                                    byte value = 0;
                                    this.Log(LogLevel.Noisy, "Rx Read Value");

                                    if (!ReceiveByteFromRenode(out value))
                                    {
                                        this.Log(LogLevel.Warning, "Trying to read data from empty receive fifo");
                                    }

                                    return value;
                                }, name: "data")
            .WithTag("RESERVED", 8, 24);

            Register.UART_IER.Define(dwordregisters, 0x00, "UART_IER")
                            .WithValueField(0, 2, out IntEnable, FieldMode.Read | FieldMode.Write,
                            writeCallback: (_, value) =>
                            {
                                IntEnable.Value = value;
                                this.Log(LogLevel.Noisy, "Int Enable {0:X}", IntEnable.Value);
//                                if (IntEnableValue>=2)

                                UpdateInterrupts();
                            }, name: "UART_IER")
                            .WithValueField(2, 30, FieldMode.Read | FieldMode.Write, name: "reserved");



            Register.UART_IIR.Define(dwordregisters, 0x00, "UART_IIR")
                            .WithFlag(0, out IT_PENDING, name: "IT_PENDING")
                            .WithValueField(1, 5, out IT_TYPE, FieldMode.Read | FieldMode.Write, name: "VT Type")
                            .WithTag("RESERVED", 6, 26);
            Register.UART_LCR.Define(dwordregisters, 0x00, "UART_LCR")
            .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "UART_LCR");
            Register.UART_MCR.Define(dwordregisters, 0x00, "UART_MCR")
            .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "UART_MCR");
            Register.UART_LSR.Define(dwordregisters, 0x00000040, "UART_LSR")
            .WithFlag(0, out bRX_FIFO_E, FieldMode.Read | FieldMode.Set, name: "THR_IT Interrupt Enable")
            .WithTag("RESERVED", 1, 31);
            Register.UART_SPR.Define(dwordregisters, 0x00, "UART_SPR")
            .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "UART_SPR");
            Register.UART_MDR1.Define(dwordregisters, 0x00, "UART_MDR1")
            .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "UART_MDR1");
            Register.UART_SSR.Define(dwordregisters, 0x00, "UART_SSR")
            .WithFlag(0, out bTxFifoFull, FieldMode.Read | FieldMode.Write, name: "TX_FIFO_FULL")
            .WithValueField(1, 31, FieldMode.Read | FieldMode.Write, name: "reserved");
            Register.UART_SCR.Define(dwordregisters, 0x00, "UART_SCR")
            .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "UART_SCR");
            Register.UART_SYSC.Define(dwordregisters, 0x00, "UART_SYSC")
            .WithValueField(0,32, FieldMode.Read | FieldMode.Write, name: "UART_SYSC");
            Register.UART_SYSS.Define(dwordregisters, 0x00000001, "UART_SYSS")
            .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "UART_SYSS");

        }
        private Boolean IntActive = false;

        public override void UpdateInterrupts()
        {
            this.Log(LogLevel.Noisy, "Start UpdateInterrupts");
            lock (UpdateInterruptsLock)
            {
                bReceiveIrqActive = false;
                bTransmitIrqActive = false;

                if (IntEnable.Value >= 1)
                {
                    if (true == CopyMessageToCpuBuffer())
                    {
                        bRX_FIFO_E.Value = true;
                        IT_TYPE.Value = 0x02;
                        bReceiveIrqActive = true;
                    }
                    else
                    {
                        bRX_FIFO_E.Value = false;
                    }
                }

                if (bReceiveIrqActive == false)
                {
                    if (IntEnable.Value >= 2)
                    {
                        IT_TYPE.Value = 0x01;
                        bTransmitIrqActive = true;
                    }
                }

                if ((bReceiveIrqActive == true) || (bTransmitIrqActive == true))
                {
                    IT_PENDING.Value = true;
                    if (IntActive == false)
                    {
                        this.Log(LogLevel.Noisy, "UART IRQ Active:  TX IRQ: {0}  RX IRQ: {1}", bTransmitIrqActive, bReceiveIrqActive);
                        IRQ.Set();
                        IntActive = true;
                    }
                }
                else
                {
                    IT_PENDING.Value = false;
                    if (IntActive == true)
                    {
                        this.Log(LogLevel.Noisy, "UART IRQ Active:  TX IRQ: {0}  RX IRQ: {1}", bTransmitIrqActive, bReceiveIrqActive);
                        IRQ.Unset();
                        IntActive = false;
                    }
                }
                this.Log(LogLevel.Noisy, "End UpdateInterrupts");
            }
        }






        private enum Register : long
        {
            UART_RTHR = 0x0U,
            UART_IER = 0x4U,
            UART_IIR = 0x8U,
            UART_LCR = 0xcU,
            UART_MCR = 0x10U,
            UART_LSR = 0x14U,
            UART_MSR = 0x18U,
            UART_SPR = 0x1cU,
            UART_MDR1 = 0x20U,
            UART_MDR2 = 0x24U,
            UART_SFLSR = 0x28U,
            UART_RESUME = 0x2cU,
            UART_RXFLL = 0x30U,
            UART_RXFLH = 0x34U,
            UART_BLR = 0x38U,
            UART_ACREG = 0x3cU,
            UART_SCR = 0x40U,
            UART_SSR = 0x44U,
            UART_EBLR = 0x48U,
            UART_MVR = 0x50U,
            UART_SYSC = 0x54U,
            UART_SYSS = 0x58U,
            UART_WER = 0x5cU,
            UART_CFPS = 0x60U,
            UART_RXFIFO_LVL = 0x64U,
            UART_TXFIFO_LVL = 0x68U,
            UART_IER2 = 0x6cU,
            UART_ISR2 = 0x70U,
            UART_FREQ_SEL = 0x74U,
            UART_ABAUD_1ST_CHAR = 0x78U,
            UART_BAUD_2ND_CHAR = 0x7cU,
            UART_MDR3 = 0x80U,
            UART_TX_DMA_THRESHOLD = 0x84U,
        };


    }

    public class ASK_Uart_Base_J : IKnownSize, IUART
    {

        public void SendRemote()
        {
            string answer;           
            for (int i = 0; i < 10000; i++)
            {
                SendRemoteCommandHexString("0101");
                for (int j=0;  j<100; j++)
                {
                    UpdateInterrupts();
                    answer = GetRemoteAnswerHexString();
                    if (answer == "\"000101040000\"")
                    {
                        this.Log(LogLevel.Error, answer);
                        break;
                    }
                }
            }
        }

        public ASK_Uart_Base_J(Machine machine)
        {
            RemoteAnswerFifo = new Queue<string>();
            RemoteAnswerFifoLock = new object();
            UartFifoSendToCpu = new Queue<byte>();
            UartFifoSendToCpuLock = new object();
            UartFifoReceiveFromRenode = new Queue<byte>();
            UartFifoReceiveFromRenodeLock = new object();
            UartFifoReceiveFromRobot = new Queue<byte>();
            UartFifoReceiveFromRobotLock = new object();
            WriteCharQueue = new Queue<byte>();
        }

        private int number = 0;

        public void SendRemoteCommandHexString(string remoteString)
        {
            Boolean bcrc = true;
            uint crc;
            byte[] remoteBytes;

            remoteBytes = HexStringToBytes(remoteString);

            lock (UartFifoReceiveFromRobotLock)
            {
                UartFifoReceiveFromRobot.Enqueue(0x1B);  // Escape
                UartFifoReceiveFromRobot.Enqueue(0x00);  // Channel ID 
                if (bcrc)
                    UartFifoReceiveFromRobot.Enqueue(0x01);  // Protocol ID with CRC
                else
                    UartFifoReceiveFromRobot.Enqueue(0x41);  // Protocol ID without CRC

                UartFifoReceiveFromRobot.Enqueue((byte)(remoteBytes.Length & 0x00FF));        // length low
                UartFifoReceiveFromRobot.Enqueue((byte)((remoteBytes.Length & 0xFF00) >> 8));  // length high
                for (int i = 0; i < remoteBytes.Length; i++)
                {
                    UartFifoReceiveFromRobot.Enqueue((byte)remoteBytes[i]);
                }
                if (bcrc)
                {
                    crc = calccrc16(remoteBytes);
                    UartFifoReceiveFromRobot.Enqueue((byte)(crc & 0x00FF));         // crc low
                    UartFifoReceiveFromRobot.Enqueue((byte)((crc & 0xFF00) >> 8));  // crc high
                }
            }
            number++;
            this.Log(LogLevel.Noisy, "SendRemote {0}", number);
            if (UartFifoSendToCpu.Count == 0)
            {
                UpdateInterrupts();
//                System.Threading.Thread.Sleep(50);
            }


        }

        public virtual void UpdateInterrupts()
        {
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


        public string GetRemoteAnswerHexString()
        {
            string data;
            string retVal = "";
            this.Log(LogLevel.Noisy, "Start Get Remote Answer Hex String:");
            lock (RemoteAnswerFifoLock)
            {
                if (RemoteAnswerFifo.Count > 0)
                {
                    data = RemoteAnswerFifo.Dequeue();
                    data = data.Replace("-", "");
                    retVal = data.Insert(0, "\"");
                    retVal += '"';
                }
            }
            if (retVal=="")
                System.Threading.Thread.Sleep(10);
            this.Log(LogLevel.Noisy, "End Get Remote Answer Hex String:");
            return retVal;
        }

        public void Reset()
        {
            RemoteAnswerFifo.Clear();
            UartFifoSendToCpu.Clear();
            UartFifoReceiveFromRenode.Clear();
            UartFifoReceiveFromRobot.Clear();
        }
/*
        public int GetUartFifoSendToCpuCount()
        {
            int retVal;
            lock (UartFifoSendToCpuLock)
            {
                retVal = UartFifoSendToCpu.Count;
            }
            return retVal;
        }
*/
        private Queue<String> RemoteAnswerFifo;
        private Queue<byte> UartFifoSendToCpu;
        private Queue<byte> UartFifoReceiveFromRenode;
        private Queue<byte> UartFifoReceiveFromRobot;
        private Queue<byte> WriteCharQueue;
        protected int tx_remotestate = 0;
        protected byte tx_protocolid = 0;
        protected UInt16 tx_length = 0;
        protected UInt16 tx_crc = 0;
        protected byte[] tx_msgdata;
        protected uint tx_datanr = 0;
        protected Boolean tx_withcrc;
        protected int rx_state = 0;
        protected byte rx_protocolid = 0;
        protected byte rx_channelid = 0;
        protected UInt16 rx_length = 0;
        protected UInt16 rx_crc = 0;
        protected uint rx_datanr = 0;
        protected Boolean rx_withcrc;
        protected Boolean bFifoRxNotEmpty = false;
        protected readonly object RemoteAnswerFifoLock;
        protected readonly object UartFifoSendToCpuLock;
        protected readonly object UartFifoReceiveFromRobotLock;
        protected readonly object UartFifoReceiveFromRenodeLock;

        public event Action<byte> CharReceived;


        protected bool ReceiveByteFromRenode(out byte character)
        {
            character = default(byte);
            Boolean bRetVal = false;
            Boolean bFifoRxEmpty = true;
            lock (UartFifoSendToCpuLock)
            {
                if (UartFifoSendToCpu.Count > 0)
                {
                    character = UartFifoSendToCpu.Dequeue();
                    bRetVal = true;
                }
                if (UartFifoSendToCpu.Count == 0)
                {
                    bFifoRxEmpty = true;
                }
                else
                {
                    bFifoRxEmpty = false;
                }
            }
            if (bFifoRxEmpty == true)
                UpdateInterrupts();
            return bRetVal;
        }

        protected void SendByteToRenode(byte character)
        {
            DecodeRemoteAnswer(character);    // Send character to renode uart implementation
            CharReceived?.Invoke(character);              // Send character to renode uart implementation
        }

        protected void DecodeRemoteAnswer(byte value)
        {
            switch (tx_remotestate)
            {
                case 0:                     // escape
                    if (value == 0x1B)
                    {
                        tx_remotestate = 1;
//                        this.Log(LogLevel.Noisy, "Escape Id: {0:X}", value);
                    }
                    break;

                case 1:                    // channel id
                    tx_remotestate = 2;
//                    this.Log(LogLevel.Noisy, "Channel Id: {0:X}", value);
                    break;

                case 2:                    // protocol id
                    tx_remotestate = 3;
                    tx_protocolid = value;
//                    this.Log(LogLevel.Noisy, "Tx Protocol Id: {0:X}", tx_protocolid);
                    if (tx_protocolid == 0x81)
                    {
                        tx_withcrc = false;
                    }
                    else if (tx_protocolid == 0x01)
                    {
                        tx_withcrc = true;
                    }

                    else
                        tx_remotestate = 0;
                    break;

                case 3:                    // length low
                    tx_length = value;
//                    this.Log(LogLevel.Noisy, "Tx Protocol length low : {0:X}", value);
                    tx_remotestate = 4;
                    break;

                case 4:                    // length high
                    tx_length |= (UInt16)(value << 8);
//                    this.Log(LogLevel.Noisy, "Tx Protocol length high : {0:X}", value);
                    if (tx_length == 0)
                        tx_remotestate = 0;
                    else
                    {
                        tx_msgdata = new byte[tx_length];
                        tx_datanr = 0;
                        tx_remotestate = 5;
                    }
                    break;

                case 5:                    // data
//                    this.Log(LogLevel.Noisy, "Data : {0:X}", value);
                    tx_msgdata[tx_datanr++] = value;
                    if (tx_datanr == tx_length)
                    {
                        if (tx_withcrc == true)
                            tx_remotestate = 6;
                        else
                        {
                            if (tx_protocolid == 0x01)
                            {
//                                this.Log(LogLevel.Noisy, "Add Remote Answer without CRC: {0}", Misc.PrettyPrintCollectionHex(tx_msgdata));
                                lock (RemoteAnswerFifoLock)
                                {
                                    RemoteAnswerFifo.Enqueue(BitConverter.ToString(tx_msgdata));
                                }
                            }
                            else
                            {
                                this.Log(LogLevel.Noisy, "Discard Remote Answer without CRC: {0}", Misc.PrettyPrintCollectionHex(tx_msgdata));
                            }
                            tx_remotestate = 0;
                        }
                    }
                    break;

                case 6:                    // crc low
                    tx_crc = value;
//                    this.Log(LogLevel.Noisy, "CRC low : {0:X}", value);
                    tx_remotestate = 7;
                    break;

                case 7:                    // crc high
                    tx_crc |= (UInt16)(value << 8);
//                    this.Log(LogLevel.Noisy, "CRC high : {0:X}", value);
                    if (tx_crc == calccrc16(tx_msgdata))
                    {
                        if (tx_protocolid == 0x01)
                        {
//                            this.Log(LogLevel.Noisy, "Add Remote Answer with CRC: {0}", Misc.PrettyPrintCollectionHex(tx_msgdata));
                            lock (RemoteAnswerFifoLock)
                            {
                                RemoteAnswerFifo.Enqueue(BitConverter.ToString(tx_msgdata));
                            }
                        }
                        else
                        {
                            this.Log(LogLevel.Noisy, "Discard Remote Answer with CRC: {0}", Misc.PrettyPrintCollectionHex(tx_msgdata));
                        }
                    }
                    else
                        this.Log(LogLevel.Warning, "CRC of send Remote Message invalid");
                    tx_remotestate = 0;
                    break;
            }

        }
        protected string ConvertRemoteToString(byte[] data)
        {
            string hex = BitConverter.ToString(data);
            hex = hex.Replace("-", "");
            String modified = hex.Insert(0, "\"");
            modified += '"';
            return modified;
        }

        protected UInt16 calccrc16(byte[] payload)
        {
            UInt16 crc = 0x63CB;
            for (int i = 0; i < payload.Length; i++)
                crc = (UInt16)(crc16_lookup[(crc ^ payload[i]) & 0xFF] ^ (crc >> 8));
            return crc;
        }

        public void WriteChar(byte value)
        {
            switch (rx_state)
            {
                case 0:                     // escape
                    if (value == 0x1B)
                    {
                        rx_state = 1;
                    }
                    break;

                case 1:                    // channel id
                    rx_state = 2;
                    rx_channelid = value;
                    break;

                case 2:                    // protocol id
                    rx_state = 3;
                    rx_protocolid = value;
                    if (rx_protocolid == 0x41)
                    {
                        WriteCharQueue.Enqueue(0x1B);
                        WriteCharQueue.Enqueue(rx_channelid);
                        WriteCharQueue.Enqueue(rx_protocolid);
                        rx_withcrc = false;
                    }
                    else if ((rx_protocolid == 0x01) || (rx_protocolid == 0x00))
                    {
                        WriteCharQueue.Enqueue(0x1B);
                        WriteCharQueue.Enqueue(rx_channelid);
                        WriteCharQueue.Enqueue(rx_protocolid);
                        rx_withcrc = true;
                    }
                    else
                    {
                        rx_state = 0;
//                        UartFifoReceiveFromRenode.Clear();
                    }
                    break;

                case 3:                    // length low
                    rx_length = value;
                    rx_state = 4;
                    WriteCharQueue.Enqueue(value);
                    break;

                case 4:                    // length high
                    rx_length |= (UInt16)(value << 8);
                    rx_datanr = 0;
                    rx_state = 5;
                    if (rx_length == 0)    // needed for alive
                        rx_state = 6;

                    WriteCharQueue.Enqueue(value);
                    break;

                case 5:                    // data
                    WriteCharQueue.Enqueue(value);
                    rx_datanr++;
                    if (rx_datanr == rx_length)
                    {
                        if (rx_withcrc == true)
                            rx_state = 6;
                        else
                        {
                            this.Log(LogLevel.Noisy, "Send ASAP to CPU buffer without CRC: {0}", Misc.PrettyPrintCollectionHex(UartFifoReceiveFromRenode.ToArray()));
                            lock (UartFifoReceiveFromRenodeLock)
                            {
                                while (WriteCharQueue.Count > 0)
                                    UartFifoReceiveFromRenode.Enqueue(WriteCharQueue.Dequeue());
                            }
                            if (UartFifoSendToCpu.Count == 0)
                                UpdateInterrupts();

                            rx_state = 0;
                        }
                    }
                    break;

                case 6:                    // crc low
                    rx_state = 7;
                    WriteCharQueue.Enqueue(value);
                    break;

                case 7:                    // crc high
                    WriteCharQueue.Enqueue(value);
                    this.Log(LogLevel.Noisy, "Send ASAP to CPU buffer with CRC: {0} {1}", UartFifoReceiveFromRenode.Count, Misc.PrettyPrintCollectionHex(UartFifoReceiveFromRenode.ToArray()));
                    lock (UartFifoReceiveFromRenodeLock)
                    {
                        while (WriteCharQueue.Count > 0)
                            UartFifoReceiveFromRenode.Enqueue(WriteCharQueue.Dequeue());
                    }
                    if (UartFifoSendToCpu.Count == 0)
                        UpdateInterrupts();
                    rx_state = 0;
                    break;
            }

        }

        public Boolean CopyMessageToCpuBuffer()
        {
            Boolean RetVal = false;
            lock (UartFifoSendToCpuLock)
            {
                lock (UartFifoReceiveFromRenodeLock)
                {
                    if (UartFifoReceiveFromRenode.Count>0)
                       this.Log(LogLevel.Noisy, "Copy Msg from Renode");
                    while (UartFifoReceiveFromRenode.Count>0)
                        UartFifoSendToCpu.Enqueue(UartFifoReceiveFromRenode.Dequeue());
                }
                lock (UartFifoReceiveFromRobotLock)
                {
                    if (UartFifoReceiveFromRobot.Count > 0)
                        this.Log(LogLevel.Noisy, "Copy Msg from Robot");
                    while (UartFifoReceiveFromRobot.Count > 0)
                        UartFifoSendToCpu.Enqueue(UartFifoReceiveFromRobot.Dequeue());
                }
                if (UartFifoSendToCpu.Count > 0)
                    RetVal = true;
            }
            return RetVal;

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

        public long Size => throw new NotImplementedException();

        uint IUART.BaudRate => throw new NotImplementedException();

        Bits IUART.StopBits => throw new NotImplementedException();

        Parity IUART.ParityBit => throw new NotImplementedException();
    }
}