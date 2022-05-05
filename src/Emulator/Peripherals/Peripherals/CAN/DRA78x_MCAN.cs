//
// Copyright (c) 2010-2020 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Globalization;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.CAN;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.CAN
{
    [AllowedTranslations(AllowedTranslation.WordToDoubleWord | AllowedTranslation.ByteToDoubleWord)]
    public class DRA78x_MCAN : IKnownSize, IDoubleWordPeripheral, IWordPeripheral, IMemory
    {
        public DRA78x_MCAN(Machine machine)
        {
            mach = machine;
            IRQ = new GPIO();
            dwordregisters = new DoubleWordRegisterCollection(this);
            messagememory = new ArrayMemory(SizeofMessageMemory);
            rx0Queue = new Queue<DRA78x_CAN_Msg>();
            txQueue = new Queue<DRA78x_CAN_Msg>();
            rx0QueueLock = new object();
            txQueueLock = new object();

            DefineRegisters();
            UpdateInterrupts();
            PeriodicCanTimer = new LimitTimer(mach.ClockSource, 32000000, this, nameof(PeriodicCanTimer), 1, Direction.Ascending, false, WorkMode.Periodic, true, true, 1);
            PeriodicCanTimer.Enabled = false;
            PeriodicCanTimer.LimitReached += SendCyclicCan;
        }

        public byte ReadByte(long offset)
        {
            byte retval;
            if (offset < SizeofMessageMemory)
                retval = messagememory.ReadByte(offset);
            else
                retval = (byte)ReadDoubleWord(offset);

            return retval;
        }

        public void WriteByte(long offset, byte value)
        {
            if (offset < SizeofMessageMemory)
                messagememory.WriteByte(offset, value);
            else
                WriteDoubleWord(offset, value);
        }


        public ushort ReadWord(long offset)
        {
            ushort retval;
            if (offset < SizeofMessageMemory)
                retval = messagememory.ReadWord(offset);
            else
                retval = (ushort)ReadDoubleWord(offset);

            return retval;
        }

        public void WriteWord(long offset, ushort value)
        {
            if (offset < SizeofMessageMemory)
                messagememory.WriteWord(offset, value);
            else
                WriteDoubleWord(offset, value);
        }




        public uint ReadDoubleWord(long offset)
        {
            uint retval;
            if (offset < SizeofMessageMemory)
                retval = messagememory.ReadDoubleWord(offset);
            else
                retval = dwordregisters.Read(offset);
            return retval;
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if (offset < SizeofMessageMemory)
                messagememory.WriteDoubleWord(offset, value);
            else
            {
                dwordregisters.Write(offset, value);
                if (offset == (long)Registers.MCAN_IR)
                    UpdateInterrupts();
            }
        }


        public void Reset()
        {
            dwordregisters.Reset();
            UpdateInterrupts();
        }

        public long Size => 0x2000;
        public GPIO IRQ { get; private set; }

        private void CopyMsgFromMessageRamToTxQueue()
        {
            DRA78x_CAN_Msg msg = new DRA78x_CAN_Msg();
            msg.Identifier = new byte[4];
            uint Offset = TxStartAddress.Value << 2;
            uint DataLength = 0;
            uint word1 = messagememory.ReadDoubleWord(Offset);
            if ((word1 & 0x80000000) == 0x80000000)
                msg.ESI = true;
            else
                msg.ESI = false;

            if ((word1 & 0x40000000) == 0x40000000)
                msg.XTD = true;
            else
                msg.XTD = false;

            if ((word1 & 0x20000000) == 0x20000000)
                msg.RTR = true;
            else
                msg.RTR = false;

            if (msg.XTD == true)
            {
                uint data = word1 & 0x1FFFFFFF;
                msg.Identifier[3] = (byte)(data >> 0);
                msg.Identifier[2] = (byte)(data >> 8);
                msg.Identifier[1] = (byte)(data >> 16);
                msg.Identifier[0] = (byte)(data >> 24);
//                this.Log(LogLevel.Noisy, " Tx Message Identifier 29bit 0x{0:X}{1:X}{2:X}{3:X}", msg.Identifier[0], msg.Identifier[1], msg.Identifier[3], msg.Identifier[0]);
            }
            else
            {
                uint data = word1 & 0x1FFFFFFF;
                data = data >> 18;
                msg.Identifier[1] = (byte)(data >> 0);
                msg.Identifier[0] = (byte)(data >> 8);
//                this.Log(LogLevel.Noisy, " Tx Message Identifier 11bit 0x{0:X}{1:X}", msg.Identifier[0], msg.Identifier[1]);
            }
            Offset += 4;
            uint word2 = messagememory.ReadDoubleWord(Offset);
            msg.MM = (byte)((word2 & 0xFF000000) >> 24);
            if ((word2 & 0x00200000) == 0x00200000)
                msg.FDF = true;
            if ((word2 & 0x00100000) == 0x00100000)
                msg.BRS = true;

            msg.DLC = (byte)((word2 & 0x000F0000) >> 16);

            if (msg.DLC == 0)
                DataLength = 0;
            else if (msg.DLC == 1)
                DataLength = 1;
            else if (msg.DLC == 2)
                DataLength = 2;
            else if (msg.DLC == 3)
                DataLength = 3;
            else if (msg.DLC == 4)
                DataLength = 4;
            else if (msg.DLC == 5)
                DataLength = 5;
            else if (msg.DLC == 6)
                DataLength = 6;
            else if (msg.DLC == 7)
                DataLength = 7;
            else if (msg.DLC == 8)
                DataLength = 8;
            else if (msg.DLC == 9)
                DataLength = 8;
            else if (msg.DLC == 10)
                DataLength = 12;
            else if (msg.DLC == 11)
                DataLength = 16;
            else if (msg.DLC == 12)
                DataLength = 20;
            else if (msg.DLC == 13)
                DataLength = 24;
            else if (msg.DLC == 14)
                DataLength = 32;
            else if (msg.DLC == 15)
                DataLength = 48;

            //Read data bytes
            msg.DB = new byte[DataLength];
//            this.Log(LogLevel.Noisy, " Tx Message Datalength 0x{0:X}", DataLength);
            Offset+=4;
            for (int i = 0; i < DataLength; i++)
            {
                msg.DB[i] = messagememory.ReadByte(Offset++);
//                this.Log(LogLevel.Noisy, " Tx Message Databyte 0x{0:X}", msg.DB[i]);
            }
            this.Log(LogLevel.Noisy, "TX " + msg.GetAllValues());

            //Fill Tx Event Fifo
            Offset = TxEventStartAddress.Value << 2;
            uint Data = word1;
            messagememory.WriteDoubleWord(Offset, Data);
            Offset += 4;
            Data = msg.TXTS & 0x0000FFFF;
            Data |= (msg.DLC & 0x0F) << 16;
            if (msg.BRS)
                Data |= 0x00100000;
            if (msg.FDF)
                Data |= 0x00200000;
            Data |= (uint)(msg.ET & 0x03) << 22;
            Data |= (uint)(msg.MM & 0x0F) << 24;
            messagememory.WriteDoubleWord(Offset, Data);

            this.Log(LogLevel.Noisy, msg.GetAllValues() + " Copy new Message from CPU Message RAM to Renode Queue");
            lock (txQueueLock)
            {
                txQueue.Enqueue(msg);
            }
            UpdateInterrupts();

        }


        private void CopyNextMsgfromRxQueue0ToMessageRam()
        {
            lock (rx0QueueLock)
            {
                if (rx0Queue.Count > 0)
                {
                    DRA78x_CAN_Msg msg = rx0Queue.Dequeue();
                    uint Offset = Rx0StartAddress.Value << 2;
                    uint data = 0x00;

                    if (msg.XTD == true)
                    {
                        data |= (uint)(msg.Identifier[3]) << 0;
                        data |= (uint)(msg.Identifier[2]) << 8;
                        data |= (uint)(msg.Identifier[1]) << 16;
                        data |= (uint)(msg.Identifier[0]) << 24;
                        data &= 0x1FFFFFFF;
                    }
                    else
                    {
                        data |= (uint)(msg.Identifier[1]) << 0;
                        data |= (uint)(msg.Identifier[0]) << 8;
                        data &= 0x7FF;
                        data = data << 18;
                        data &= 0x1FFFFFFF;
                    }
                    if (msg.RTR)
                        data |= 0x20000000;
                    if (msg.XTD)
                        data |= 0x40000000;
                    if (msg.ESI)
                        data |= 0x80000000;
                    messagememory.WriteDoubleWord(Offset, data);
                    Offset += 4;



                    data = msg.RXTS & 0x0000FFFF;
                    if (msg.BRS)
                        data |= 0x00100000;
                    if (msg.FDF)
                        data |= 0x00200000;
                    if (msg.ANMF)
                        data |= 0x80000000;
                    data |= (msg.FIDX & 0x7F) << 24;
                    data |= (msg.DLC & 0x0F) << 16;
                    messagememory.WriteDoubleWord(Offset, data);
                    Offset += 4;


                    //Write data bytes
                    for (int i = 0; i < msg.DB.Length; i++)
                    {
                        messagememory.WriteByte(Offset++, msg.DB[i]);
                    }
                    RxFIFO0NewMessage.Value = true;
                    msg0InLength.Value = 1;
                    this.Log(LogLevel.Noisy, msg.GetAllValues() + " Copy Msg From Renode Queue 0 To CPU Message Ram");
                }
            }
            UpdateInterrupts();

        }


        public void SetBusOff(Boolean status)
        {
            BusOffStatus.Value = status;
            BusOff.Value = status;
            UpdateInterrupts();
        }

        public string GetMessageHexString()
        {
            DRA78x_CAN_Msg msg;
            string answer ="";
            lock (txQueueLock)
            {
                if (txQueue.Count > 0)
                {
                    msg = txQueue.Dequeue();
                    if (msg.XTD)
                        answer = BitConverter.ToString(msg.Identifier);
                    else
                        answer = BitConverter.ToString(msg.Identifier, 0, 2);


                    answer += " " + BitConverter.ToString(msg.DB);

                    answer += " " + msg.DLC.ToString();

                    if (msg.XTD)
                        answer += " True";
                    else
                        answer += " False";

                    if (msg.RTR)
                        answer += " True";
                    else
                        answer += " False";

                    if (msg.FDF)
                        answer += " True";
                    else
                        answer += " False";

                    if (msg.BRS)
                        answer += " True";
                    else
                        answer += " False";

                    answer = answer.Replace("-", "");
                    answer = answer.Insert(0, "\"");
                    answer += '"';

                    this.Log(LogLevel.Noisy, msg.GetAllValues() + " Renode Get Message");
                }
                else
                    this.Log(LogLevel.Noisy, " Renode Get Empty Message ");
            }
            if (answer == "")
                System.Threading.Thread.Sleep(10);

            return answer;
        }
        public void SendWakeup ()
        {
            SendMessage("501", "0101000000000000", 8);
        }

        public void SendGetSession()
        {
            SendMessage("6BE", "0322f186CCCCCCCC", 8);
        }

        public void SendMessage(string Identifier, string Data = "", uint DataLengthCode= 0, bool ExtendedIdentifier = false,
                                    bool RemoteFrame = false, bool FDFormat = false, bool BitrateSwitch = false )
        {
            DRA78x_CAN_Msg msg = new DRA78x_CAN_Msg();
            msg.Identifier = HexStringToBytes(Identifier);
            msg.XTD = ExtendedIdentifier;
            if ( (msg.XTD == true) & (msg.Identifier.Length != 4) )
            {
                throw new ArgumentException($"Identifier not in Extended Format 29bit");
            }
            else if ((msg.XTD == false) & (msg.Identifier.Length != 2))
            {
                throw new ArgumentException($"Identifier not in Normal Format 11bit");
            }
            msg.DLC = DataLengthCode;

            msg.RTR = RemoteFrame;
            msg.FDF = FDFormat;
            msg.ESI = false;
            msg.BRS = BitrateSwitch;
            msg.ANMF = false;
            msg.RXTS = 0x00;
            msg.FIDX = 0x00;
            msg.DLC = 0x00;
            if (Data.Length > 0)
            {
                msg.DB = HexStringToBytes(Data);
            }
            if (msg.DB.Length == 0)
                msg.DLC = 0;
            else if (msg.DB.Length == 1)
                msg.DLC = 1;
            else if (msg.DB.Length == 2)
                msg.DLC = 2;
            else if (msg.DB.Length == 3)
                msg.DLC = 3;
            else if (msg.DB.Length == 4)
                msg.DLC = 4;
            else if (msg.DB.Length == 5)
                msg.DLC = 5;
            else if (msg.DB.Length == 6)
                msg.DLC = 6;
            else if (msg.DB.Length == 7)
                msg.DLC = 7;
            else if (msg.DB.Length == 8)
                msg.DLC = 8;
            if (msg.DB.Length > 8)
                msg.DLC = 9;
            if (msg.DB.Length > 12)
                msg.DLC = 10;
            if (msg.DB.Length > 16)
                msg.DLC = 11;
            if (msg.DB.Length > 20)
                msg.DLC = 12;
            if (msg.DB.Length > 24)
                msg.DLC = 13;
            if (msg.DB.Length > 32)
                msg.DLC = 14;
            if (msg.DB.Length > 48)
                msg.DLC = 15;
            this.Log(LogLevel.Noisy, msg.GetAllValues() + " Renode Send Message");
            lock (rx0QueueLock)
            {
                rx0Queue.Enqueue(msg);
            }
            if (msg0InLength.Value == 0)
                CopyNextMsgfromRxQueue0ToMessageRam();
        }


        public void SetCyclicMessage(string Identifier, string Data = "", uint Period_ms=500, uint DataLengthCode = 0, bool ExtendedIdentifier = false,
                                    bool RemoteFrame = false, bool FDFormat = false, bool BitrateSwitch = false)
        {
            this.Log(LogLevel.Noisy, "SetCyclicCan");
            cyclic_msg.Identifier = HexStringToBytes(Identifier);
            cyclic_msg.XTD = ExtendedIdentifier;
            if ((cyclic_msg.XTD == true) & (cyclic_msg.Identifier.Length != 4))
            {
                throw new ArgumentException($"Identifier not in Extended Format 29bit");
            }
            else if ((cyclic_msg.XTD == false) & (cyclic_msg.Identifier.Length != 2))
            {
                throw new ArgumentException($"Identifier not in Normal Format 11bit");
            }
            cyclic_msg.DLC = DataLengthCode;

            cyclic_msg.RTR = RemoteFrame;
            cyclic_msg.FDF = FDFormat;
            cyclic_msg.ESI = false;
            cyclic_msg.BRS = BitrateSwitch;
            cyclic_msg.ANMF = false;
            cyclic_msg.RXTS = 0x00;
            cyclic_msg.FIDX = 0x00;
            cyclic_msg.DLC = 0x00;
            if (Data.Length > 0)
            {
                cyclic_msg.DB = HexStringToBytes(Data);
            }
            if (cyclic_msg.DB.Length == 0)
                cyclic_msg.DLC = 0;
            else if (cyclic_msg.DB.Length == 1)
                cyclic_msg.DLC = 1;
            else if (cyclic_msg.DB.Length == 2)
                cyclic_msg.DLC = 2;
            else if (cyclic_msg.DB.Length == 3)
                cyclic_msg.DLC = 3;
            else if (cyclic_msg.DB.Length == 4)
                cyclic_msg.DLC = 4;
            else if (cyclic_msg.DB.Length == 5)
                cyclic_msg.DLC = 5;
            else if (cyclic_msg.DB.Length == 6)
                cyclic_msg.DLC = 6;
            else if (cyclic_msg.DB.Length == 7)
                cyclic_msg.DLC = 7;
            else if (cyclic_msg.DB.Length == 8)
                cyclic_msg.DLC = 8;
            if (cyclic_msg.DB.Length > 8)
                cyclic_msg.DLC = 9;
            if (cyclic_msg.DB.Length > 12)
                cyclic_msg.DLC = 10;
            if (cyclic_msg.DB.Length > 16)
                cyclic_msg.DLC = 11;
            if (cyclic_msg.DB.Length > 20)
                cyclic_msg.DLC = 12;
            if (cyclic_msg.DB.Length > 24)
                cyclic_msg.DLC = 13;
            if (cyclic_msg.DB.Length > 32)
                cyclic_msg.DLC = 14;
            if (cyclic_msg.DB.Length > 48)
                cyclic_msg.DLC = 15;

            // Limit = Frequency [Hz] / Period [Hz]
            double period_hz = 1.0 / ((double)Period_ms / 1000.0);
            PeriodicCanTimer.Limit = (ulong)Math.Round(PeriodicCanTimer.Frequency / (double) (period_hz*2));

        }
        public void StopCyclicMessage()
        {
            PeriodicCanTimer.Enabled = false;
            this.Log(LogLevel.Noisy, "StopCyclicCan");
        }


        public void StartCyclicMessage()
        {
            PeriodicCanTimer.Enabled = true;
            this.Log(LogLevel.Noisy, "StartCyclicCan");
        }


        private void SendCyclicCan()
        {
            this.Log(LogLevel.Noisy, "SendCyclicCan");
            lock (rx0QueueLock)
            {
                rx0Queue.Enqueue(cyclic_msg);
            }
            if (RxFIFO0NewMessage.Value == false)
                CopyNextMsgfromRxQueue0ToMessageRam();
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
        private void UpdateInterrupts()
        {
            uint IE = dwordregisters.Read((long)Registers.MCAN_IE);
            uint IR = dwordregisters.Read((long)Registers.MCAN_IR);


            if ((IE & IR) > 0)
            {
                this.Log(LogLevel.Noisy, "{0:X} {1:X} Interrupt Activated ", IE, IR);
                IRQ.Set(true);
            }
            else
            {
                this.Log(LogLevel.Noisy, "{0:X} {1:X} Interrupt Deactivated", IE, IR);
                IRQ.Set(false);
            }
        }

        private Machine mach;
        private LimitTimer PeriodicCanTimer;
        DRA78x_CAN_Msg cyclic_msg = new DRA78x_CAN_Msg();
        private Queue<DRA78x_CAN_Msg> rx0Queue;
        private Queue<DRA78x_CAN_Msg> txQueue;
        protected readonly object rx0QueueLock;
        protected readonly object txQueueLock;
        private IFlagRegisterField RxFIFO0NewMessageInterruptEnable;
        private IFlagRegisterField AccesstoReservedAddressEnable;
        private IFlagRegisterField TxEventFIFOFullInterruptEnable;
        private IFlagRegisterField TxFIFOEmptyInterruptEnable;
        private IFlagRegisterField RxFIFO1MessageLostInterruptEnable;
        private IFlagRegisterField RxFIFO1FullInterruptEnable;
        private IFlagRegisterField RxFIFO1NewMessageInterruptEnable;
        private IFlagRegisterField RxFIFO0MessageLostInterruptEnable;
        private IFlagRegisterField RxFIFO0FullInterruptEnable;

        private IFlagRegisterField RxFIFO0NewMessage;
        private IFlagRegisterField AccesstoReservedAddress;
        private IFlagRegisterField TxEventFIFOFull;
        private IFlagRegisterField TxEventNewEntry;
        private IFlagRegisterField RxFIFO1MessageLost;
        private IFlagRegisterField RxFIFO1Full;
        private IFlagRegisterField RxFIFO1NewMessage;
        private IFlagRegisterField RxFIFO0MessageLost;
        private IFlagRegisterField RxFIFO0Full;
        private IFlagRegisterField BusOff;
        private IFlagRegisterField BusOffStatus;
        private IValueRegisterField msg0InLength;
        private IValueRegisterField msg1InLength;
        private IValueRegisterField rxfifo0size;
        private IValueRegisterField rxfifo1size;
        private IValueRegisterField Rx0StartAddress;
        private IValueRegisterField TxStartAddress;
        private IValueRegisterField TxEventStartAddress;
        private IValueRegisterField TxEventCount;

        private const int SizeofMessageMemory = 1600 * 4;

        private readonly DoubleWordRegisterCollection dwordregisters;
        private ArrayMemory messagememory;



        private void DefineRegisters()
        {

            Registers.MCAN_IR.Define(dwordregisters, 00, "MCAN_IR")
                    .WithFlag(0, out RxFIFO0NewMessage, FieldMode.Read | FieldMode.WriteOneToClear, name: "RF0N")
                    .WithFlag(1, FieldMode.Read | FieldMode.WriteOneToClear, name: "RF0W")
                    .WithFlag(2, out RxFIFO0Full, FieldMode.Read | FieldMode.WriteOneToClear, name: "RF0F")
                    .WithFlag(3, out RxFIFO0MessageLost, FieldMode.Read | FieldMode.WriteOneToClear, name: "RF0L")
                    .WithFlag(4, out RxFIFO1NewMessage, FieldMode.Read | FieldMode.WriteOneToClear, name: "RF1N")
                    .WithFlag(5, FieldMode.Read | FieldMode.WriteOneToClear, name: "RF1W")
                    .WithFlag(6, out RxFIFO1Full, FieldMode.Read | FieldMode.WriteOneToClear, name: "RF1F")
                    .WithFlag(7, out RxFIFO1MessageLost, FieldMode.Read | FieldMode.WriteOneToClear, name: "RF1L")
                    .WithFlag(8, FieldMode.Read | FieldMode.WriteOneToClear, name: "HPM")
                    .WithFlag(9, FieldMode.Read | FieldMode.WriteOneToClear, name: "TC")
                    .WithFlag(10, FieldMode.Read | FieldMode.WriteOneToClear, name: "TCF")
                    .WithFlag(11, FieldMode.Read | FieldMode.WriteOneToClear, name: "TFE")
                    .WithFlag(12, out TxEventNewEntry, FieldMode.Read | FieldMode.WriteOneToClear, name: "TEFN")
                    .WithFlag(13, FieldMode.Read | FieldMode.WriteOneToClear, name: "TEFW")
                    .WithFlag(14, out TxEventFIFOFull, FieldMode.Read | FieldMode.WriteOneToClear, name: "TEFF")
                    .WithFlag(15, FieldMode.Read | FieldMode.WriteOneToClear, name: "TEFL")
                    .WithFlag(16, FieldMode.Read | FieldMode.WriteOneToClear, name: "TSW")
                    .WithFlag(17, FieldMode.Read | FieldMode.WriteOneToClear, name: "MRAF")
                    .WithFlag(18, FieldMode.Read | FieldMode.WriteOneToClear, name: "TOO")
                    .WithFlag(19, FieldMode.Read | FieldMode.WriteOneToClear, name: "DRX")
                    .WithFlag(20, FieldMode.Read | FieldMode.WriteOneToClear, name: "BEC")
                    .WithFlag(21, FieldMode.Read | FieldMode.WriteOneToClear, name: "BEU")
                    .WithFlag(22, FieldMode.Read | FieldMode.WriteOneToClear, name: "ELO")
                    .WithFlag(23, FieldMode.Read | FieldMode.WriteOneToClear, name: "EP")
                    .WithFlag(24, FieldMode.Read | FieldMode.WriteOneToClear, name: "EW")
                    .WithFlag(25, out BusOff, FieldMode.Read | FieldMode.WriteOneToClear, name: "BOE")
                    .WithFlag(26, FieldMode.Read | FieldMode.WriteOneToClear, name: "WDI")
                    .WithFlag(27, FieldMode.Read | FieldMode.WriteOneToClear, name: "PEA")
                    .WithFlag(28, FieldMode.Read | FieldMode.WriteOneToClear, name: "PED")
                    .WithFlag(29, FieldMode.Read | FieldMode.WriteOneToClear, name: "ARA")
                    .WithFlag(30, out AccesstoReservedAddress, FieldMode.Read | FieldMode.WriteOneToClear, name: "reserved")
                    .WithFlag(31, FieldMode.Read | FieldMode.WriteOneToClear, name: "reserved");

            Registers.MCAN_IE.Define(dwordregisters, 00, "MCAN_IE")
                        .WithFlag(0, out RxFIFO0NewMessageInterruptEnable, FieldMode.Read | FieldMode.Write, name: "RF0NE")
                        .WithFlag(1, FieldMode.Read | FieldMode.Write, name: "RF0WE")
                        .WithFlag(2, out RxFIFO0FullInterruptEnable, FieldMode.Read | FieldMode.Write, name: "RF0FE")
                        .WithFlag(3, out RxFIFO0MessageLostInterruptEnable, FieldMode.Read | FieldMode.Write, name: "RF0LE")
                        .WithFlag(4, out RxFIFO1NewMessageInterruptEnable, FieldMode.Read | FieldMode.Write, name: "RF1NE")
                        .WithFlag(5, FieldMode.Read | FieldMode.Write, name: "RF1WE")
                        .WithFlag(6, out RxFIFO1FullInterruptEnable, FieldMode.Read | FieldMode.Write, name: "RF1FE")
                        .WithFlag(7, out RxFIFO1MessageLostInterruptEnable, FieldMode.Read | FieldMode.Write, name: "RF1LE")
                        .WithFlag(8, FieldMode.Read | FieldMode.Write, name: "HPME")
                        .WithFlag(9, FieldMode.Read | FieldMode.Write, name: "TCE")
                        .WithFlag(10, FieldMode.Read | FieldMode.Write, name: "TCFE")
                        .WithFlag(11, FieldMode.Read | FieldMode.Write, name: "TFEE")
                        .WithFlag(12, out TxFIFOEmptyInterruptEnable, FieldMode.Read | FieldMode.Write, name: "TEFNE")
                        .WithFlag(13, FieldMode.Read | FieldMode.Write, name: "TEFWE")
                        .WithFlag(14, out TxEventFIFOFullInterruptEnable, FieldMode.Read | FieldMode.Write, name: "TEFFE")
                        .WithFlag(15, FieldMode.Read | FieldMode.Write, name: "TEFLE")
                        .WithFlag(16, FieldMode.Read | FieldMode.Write, name: "TSWE")
                        .WithFlag(17, FieldMode.Read | FieldMode.Write, name: "MRAFE")
                        .WithFlag(18, FieldMode.Read | FieldMode.Write, name: "TOOE")
                        .WithFlag(19, FieldMode.Read | FieldMode.Write, name: "DRXE")
                        .WithFlag(20, FieldMode.Read | FieldMode.Write, name: "BECE")
                        .WithFlag(21, FieldMode.Read | FieldMode.Write, name: "BEUE")
                        .WithFlag(22, FieldMode.Read | FieldMode.Write, name: "ELOE")
                        .WithFlag(23, FieldMode.Read | FieldMode.Write, name: "EPE")
                        .WithFlag(24, FieldMode.Read | FieldMode.Write, name: "EWE")
                        .WithFlag(25, FieldMode.Read | FieldMode.Write, name: "BOE")
                        .WithFlag(26, FieldMode.Read | FieldMode.Write, name: "WDIE")
                        .WithFlag(27, FieldMode.Read | FieldMode.Write, name: "PEAE")
                        .WithFlag(28, FieldMode.Read | FieldMode.Write, name: "PEDE")
                        .WithFlag(29, out AccesstoReservedAddressEnable, FieldMode.Read | FieldMode.Set, name: "ARAE")
                        .WithFlag(30, FieldMode.Read | FieldMode.Write, name: "reserved")
                        .WithFlag(31, FieldMode.Read | FieldMode.Write, name: "reserved");

            Registers.MCAN_ILS.Define(dwordregisters, 00, "MCAN_ILS")
                        .WithFlag(0, FieldMode.Read | FieldMode.Write, name: "RF0NL")
                        .WithFlag(1, FieldMode.Read | FieldMode.Write, name: "RF0WL")
                        .WithFlag(2, FieldMode.Read | FieldMode.Write, name: "RF0FL")
                        .WithFlag(3, FieldMode.Read | FieldMode.Write, name: "RF0LL")
                        .WithFlag(4, FieldMode.Read | FieldMode.Write, name: "RF1NL")
                        .WithFlag(5, FieldMode.Read | FieldMode.Write, name: "RF1WL")
                        .WithFlag(6, FieldMode.Read | FieldMode.Write, name: "RF1FL")
                        .WithFlag(7, FieldMode.Read | FieldMode.Write, name: "RF1LL")
                        .WithFlag(8, FieldMode.Read | FieldMode.Write, name: "HPML")
                        .WithFlag(9, FieldMode.Read | FieldMode.Write, name: "TCL")
                        .WithFlag(10, FieldMode.Read | FieldMode.Write, name: "TCFL")
                        .WithFlag(11, FieldMode.Read | FieldMode.Write, name: "TFEL")
                        .WithFlag(12, FieldMode.Read | FieldMode.Write, name: "TEFNL")
                        .WithFlag(13, FieldMode.Read | FieldMode.Write, name: "TEFWL")
                        .WithFlag(14, FieldMode.Read | FieldMode.Write, name: "TEFFL")
                        .WithFlag(15, FieldMode.Read | FieldMode.Write, name: "TEFLL")
                        .WithFlag(16, FieldMode.Read | FieldMode.Write, name: "TSWL")
                        .WithFlag(17, FieldMode.Read | FieldMode.Write, name: "MRAFL")
                        .WithFlag(18, FieldMode.Read | FieldMode.Write, name: "TOOL")
                        .WithFlag(19, FieldMode.Read | FieldMode.Write, name: "DRXL")
                        .WithFlag(20, FieldMode.Read | FieldMode.Write, name: "BECL")
                        .WithFlag(21, FieldMode.Read | FieldMode.Write, name: "BEUL")
                        .WithFlag(22, FieldMode.Read | FieldMode.Write, name: "ELOL")
                        .WithFlag(23, FieldMode.Read | FieldMode.Write, name: "EPL")
                        .WithFlag(24, FieldMode.Read | FieldMode.Write, name: "EWL")
                        .WithFlag(25, FieldMode.Read | FieldMode.Write, name: "BOL")
                        .WithFlag(26, FieldMode.Read | FieldMode.Write, name: "WDIL")
                        .WithFlag(27, FieldMode.Read | FieldMode.Write, name: "PEAL")
                        .WithFlag(28, FieldMode.Read | FieldMode.Write, name: "PEDL")
                        .WithFlag(29, FieldMode.Read | FieldMode.Write, name: "ARAL")
                        .WithFlag(30, FieldMode.Read | FieldMode.Write, name: "reserved")
                        .WithFlag(31, FieldMode.Read | FieldMode.Write, name: "reserved");


            Registers.MCAN_ILE.Define(dwordregisters, 00, "MCAN_ILE")
                        .WithFlag(0, FieldMode.Read | FieldMode.Write, name: "EINT0")
                        .WithFlag(1, FieldMode.Read | FieldMode.Write, name: "EINT1")
                        .WithValueField(2, 30, FieldMode.Read | FieldMode.Write, name: "reserved");
            Registers.MCAN_CCCR.Define(dwordregisters, 00, "MCAN_CCCR")
                        .WithFlag(0, FieldMode.Read | FieldMode.Write, name: "INIT")
                        .WithValueField(1, 31, FieldMode.Read | FieldMode.Write, name: "reserved");
            Registers.MCAN_MCANSS_CTRL.Define(dwordregisters, 00, "MCAN_MCANSS_CTRL")
                        .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");
            Registers.MCAN_NBTP.Define(dwordregisters, 00, "MCAN_NBTP")
                        .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");
            Registers.MCAN_GFC.Define(dwordregisters, 00, "MCAN_GFC")
                        .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");
            Registers.MCAN_SIDFC.Define(dwordregisters, 00, "MCAN_SIDFC")
                        .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");
            Registers.MCAN_XIDFC.Define(dwordregisters, 00, "MCAN_XIDFC")
                        .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");
            Registers.MCAN_XIDAM.Define(dwordregisters, 00, "MCAN_XIDAM")
                        .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");
            Registers.MCAN_RXESC.Define(dwordregisters, 00, "MCAN_RXESC")
                        .WithValueField(0, 3, out rxfifo0size, FieldMode.Read | FieldMode.Write, name: "F0DS")
                        .WithFlag(3, FieldMode.Read | FieldMode.Write, name: "reserved")
                        .WithValueField(4, 3, out rxfifo1size, FieldMode.Read | FieldMode.Write, name: "F1DS")
                        .WithFlag(7, FieldMode.Read | FieldMode.Write, name: "reserved")
                        .WithValueField(8, 3, FieldMode.Read | FieldMode.Write, name: "RBDS")
                        .WithValueField(11, 21, FieldMode.Read | FieldMode.Write, name: "reserved");

            Registers.MCAN_RXF0C.Define(dwordregisters, 00, "MCAN_RXF0C")
                        .WithValueField(0, 2, FieldMode.Read | FieldMode.Write, name: "reserved")
                        .WithValueField(2, 14, out Rx0StartAddress, FieldMode.Read | FieldMode.Write, name: "F0SA")
                        .WithValueField(16, 7, FieldMode.Read | FieldMode.Write, name: "F0S")
                        .WithFlag(23, FieldMode.Read | FieldMode.Write, name: "reserved")
                        .WithValueField(24, 7, FieldMode.Read | FieldMode.Write, name: "F0WM")
                        .WithFlag(31, FieldMode.Read | FieldMode.Write, name: "F0OM");

            Registers.MCAN_TXEFC.Define(dwordregisters, 00, "MCAN_TXEFC")
                        .WithValueField(0, 2, FieldMode.Read | FieldMode.Write, name: "reserved")
                        .WithValueField(2, 14, out TxEventStartAddress, FieldMode.Read | FieldMode.Write, name: "EFSA")
                        .WithValueField(16, 6, FieldMode.Read | FieldMode.Write, name: "EFS")
                        .WithValueField(22, 2, FieldMode.Read | FieldMode.Write, name: "reserved")
                        .WithValueField(24, 6, FieldMode.Read | FieldMode.Write, name: "EFWM")
                        .WithValueField(30, 2, FieldMode.Read | FieldMode.Write, name: "reserved");

            Registers.MCAN_TXESC.Define(dwordregisters, 00, "MCAN_TXESC")
                        .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");

            Registers.MCAN_TXBC.Define(dwordregisters, 00, "MCAN_TXBC")
                        .WithValueField(0, 2, FieldMode.Read, name: "reserved")
                        .WithValueField(2, 14, out TxStartAddress, FieldMode.Read | FieldMode.Write, name: "TBSA")
                        .WithValueField(16, 6, FieldMode.Read | FieldMode.Write, name: "NDTB")
                        .WithValueField(22, 2, FieldMode.Read | FieldMode.Write, name: "reserved")
                        .WithValueField(24, 6, FieldMode.Read | FieldMode.Write, name: "TFQS")
                        .WithFlag(30, FieldMode.Read | FieldMode.Write, name: "TFQM")
                        .WithFlag(31, FieldMode.Read | FieldMode.Write, name: "reserved");

            Registers.MCAN_RXF0S.Define(dwordregisters, 00, "MCAN_RXF0S")
                        .WithValueField(0, 7, out msg0InLength, FieldMode.Read, name: "F0FL")
                        .WithFlag(7, FieldMode.Read, name: "reserved")
                        .WithValueField(8, 6, FieldMode.Read, name: "F0GI")
                        .WithValueField(14, 2, FieldMode.Read, name: "reserved")
                        .WithValueField(16, 6, FieldMode.Read, name: "F0PI")
                        .WithValueField(22, 2, FieldMode.Read, name: "reserved")
                        .WithFlag(24, FieldMode.Read, name: "F0F")
                        .WithFlag(25, FieldMode.Read, name: "RF0L")
                        .WithValueField(26, 6, FieldMode.Read, name: "reserved");

            Registers.MCAN_RXF1S.Define(dwordregisters, 00, "MCAN_RXF1S")
                        .WithValueField(0, 7, out msg1InLength, FieldMode.Read, name: "F1FL")
                        .WithFlag(7, FieldMode.Read, name: "reserved")
                        .WithValueField(8, 6, FieldMode.Read, name: "F1GI")
                        .WithValueField(14, 2, FieldMode.Read, name: "reserved")
                        .WithValueField(16, 6, FieldMode.Read, name: "F1PI")
                        .WithValueField(22, 2, FieldMode.Read, name: "reserved")
                        .WithFlag(24, FieldMode.Read, name: "F1F")
                        .WithFlag(25, FieldMode.Read, name: "RF1L")
                        .WithValueField(26, 6, FieldMode.Read, name: "reserved");

            Registers.MCAN_RXF0A.Define(dwordregisters, 00, "MCAN_RXF0A")
                        .WithValueField(0, 6, FieldMode.Read | FieldMode.Write, writeCallback: (_, value) =>
                            {
                                this.Log(LogLevel.Noisy, "Rx Message received");
                                msg0InLength.Value = 0;
                                if (RxFIFO0NewMessage.Value == false)
                                    CopyNextMsgfromRxQueue0ToMessageRam();
                            }, name: "F0AI")
                        .WithValueField(6, 26, FieldMode.Read | FieldMode.Write, name: "reserved");

            Registers.MCAN_RXF1A.Define(dwordregisters, 00, "MCAN_RXF1A")
                        .WithValueField(0, 6, FieldMode.Read | FieldMode.Write, writeCallback: (_, value) =>
                        {
                        }, name: "F1AI")
                        .WithValueField(6, 26, FieldMode.Read | FieldMode.Write, name: "reserved");

            Registers.MCAN_TXFQS.Define(dwordregisters, 00, "MCAN_TXFQS")
                        .WithValueField(0, 6, FieldMode.Read, name: "TFFL")
                        .WithValueField(6, 2, FieldMode.Read, name: "reserved")
                        .WithValueField(8, 5, FieldMode.Read, name: "TFGI")
                        .WithValueField(13, 3, FieldMode.Read, name: "reserved")
                        .WithValueField(16, 5, FieldMode.Read, name: "TFQPI")
                        .WithFlag(21, FieldMode.Read, name: "TFQF")
                        .WithValueField(22, 8, FieldMode.Read, name: "reserved");

            Registers.MCAN_TXBAR.Define(dwordregisters, 00, "MCAN_TXBAR")
                    .WithFlag(0, FieldMode.Read | FieldMode.Set, writeCallback: (_, value) =>
                    {
                        if (value == true)
                        {
                            TxEventNewEntry.Value = true;
                            TxEventCount.Value = 1;
                            this.Log(LogLevel.Noisy, "Tx Message send");
                            CopyMsgFromMessageRamToTxQueue();
                        }

                    }, name: "AR0")
                    .WithFlag(1, FieldMode.Read | FieldMode.Set, name: "AR1")
                    .WithFlag(2, FieldMode.Read | FieldMode.Set, name: "AR2")
                    .WithFlag(3, FieldMode.Read | FieldMode.Set, name: "AR3")
                    .WithFlag(4, FieldMode.Read | FieldMode.Set, name: "AR4")
                    .WithFlag(5, FieldMode.Read | FieldMode.Set, name: "AR5")
                    .WithFlag(6, FieldMode.Read | FieldMode.Set, name: "AR6")
                    .WithFlag(7, FieldMode.Read | FieldMode.Set, name: "AR7")
                    .WithFlag(8, FieldMode.Read | FieldMode.Set, name: "AR8")
                    .WithFlag(9, FieldMode.Read | FieldMode.Set, name: "AR9")
                    .WithFlag(10, FieldMode.Read | FieldMode.Set, name: "AR10")
                    .WithFlag(11, FieldMode.Read | FieldMode.Set, name: "AR11")
                    .WithFlag(12, FieldMode.Read | FieldMode.Set, name: "AR12")
                    .WithFlag(13, FieldMode.Read | FieldMode.Set, name: "AR13")
                    .WithFlag(14, FieldMode.Read | FieldMode.Set, name: "AR14")
                    .WithFlag(15, FieldMode.Read | FieldMode.Set, name: "AR15")
                    .WithFlag(16, FieldMode.Read | FieldMode.Set, name: "AR16")
                    .WithFlag(17, FieldMode.Read | FieldMode.Set, name: "AR17")
                    .WithFlag(18, FieldMode.Read | FieldMode.Set, name: "AR18")
                    .WithFlag(19, FieldMode.Read | FieldMode.Set, name: "AR19")
                    .WithFlag(20, FieldMode.Read | FieldMode.Set, name: "AR20")
                    .WithFlag(21, FieldMode.Read | FieldMode.Set, name: "AR21")
                    .WithFlag(22, FieldMode.Read | FieldMode.Set, name: "AR22")
                    .WithFlag(23, FieldMode.Read | FieldMode.Set, name: "AR23")
                    .WithFlag(24, FieldMode.Read | FieldMode.Set, name: "AR24")
                    .WithFlag(25, FieldMode.Read | FieldMode.Set, name: "AR25")
                    .WithFlag(26, FieldMode.Read | FieldMode.Set, name: "AR26")
                    .WithFlag(27, FieldMode.Read | FieldMode.Set, name: "AR27")
                    .WithFlag(28, FieldMode.Read | FieldMode.Set, name: "AR28")
                    .WithFlag(29, FieldMode.Read | FieldMode.Set, name: "AR29")
                    .WithFlag(30, FieldMode.Read | FieldMode.Set, name: "AR30")
                    .WithFlag(31, FieldMode.Read | FieldMode.Set, name: "AR31");

            Registers.MCAN_TXEFS.Define(dwordregisters, 00, "MCAN_TXEFS")
                        .WithValueField(0, 6, out TxEventCount, FieldMode.Read, name: "EFFL")
                        .WithValueField(6, 2, FieldMode.Read, name: "reserved")
                        .WithValueField(8, 5, FieldMode.Read, name: "EFGI")
                        .WithValueField(13, 3, FieldMode.Read, name: "reserved")
                        .WithValueField(16, 5, FieldMode.Read, name: "EFPI")
                        .WithValueField(21, 3, FieldMode.Read, name: "reserved")
                        .WithFlag(24, FieldMode.Read, name: "EFF")
                        .WithFlag(25, FieldMode.Read, name: "TEFL")
                        .WithValueField(26, 6, FieldMode.Read, name: "reserved");

            Registers.MCAN_TXEFA.Define(dwordregisters, 00, "MCAN_TXEFA")
                        .WithValueField(0, 5, FieldMode.Read | FieldMode.Write, writeCallback: (_, value) =>
                        {
                            TxEventCount.Value = 0;
                            this.Log(LogLevel.Noisy, "TxEventCount = 0");
                        }, name: "EFAI")
                        .WithValueField(5, 27, FieldMode.Read | FieldMode.Write, name: "reserved");

            Registers.MCAN_PSR.Define(dwordregisters, 00, "MCAN_PSR")
                        .WithValueField(0, 6, FieldMode.Read, name: "reserved")
                        .WithFlag(7, out BusOffStatus, FieldMode.Read, name: "BOE")
                        .WithValueField(8, 24, FieldMode.Read, name: "reserved");

        }
        private enum Registers
        {
            MCAN_MCANSS_PID = 0x1900,
            MCAN_MCANSS_CTRL = 0x1904,
            MCAN_MCANSS_STAT = 0x1908,
            MCAN_MCANSS_ICS = 0x190c,
            MCAN_MCANSS_IRS = 0x1910,
            MCAN_MCANSS_IECS = 0x1914,
            MCAN_MCANSS_IE = 0x1918,
            MCAN_MCANSS_IES = 0x191c,
            MCAN_MCANSS_EOI = 0x1920,
            MCAN_MCANSS_EXT_TS_PRESCALER = 0x1924,
            MCAN_MCANSS_EXT_TS_UNSERVICED_INTR_CNTR = 0x1928,
            MCAN_ECC_EOI = 0x1980,
            MCAN_CREL = 0x1a00,
            MCAN_ENDN = 0x1a04,
            MCAN_CUST = 0x1a08,
            MCAN_DBTP = 0x1a0c,
            MCAN_TEST = 0x1a10,
            MCAN_RWD = 0x1a14,
            MCAN_CCCR = 0x1a18,
            MCAN_NBTP = 0x1a1c,
            MCAN_TSCC = 0x1a20,
            MCAN_TSCV = 0x1a24,
            MCAN_TOCC = 0x1a28,
            MCAN_TOCV = 0x1a2c,
            MCAN_ECR = 0x1a40,
            MCAN_PSR = 0x1a44,
            MCAN_TDCR = 0x1a48,
            MCAN_IR = 0x1a50,
            MCAN_IE = 0x1a54,
            MCAN_ILS = 0x1a58,
            MCAN_ILE = 0x1a5c,
            MCAN_GFC = 0x1a80,
            MCAN_SIDFC = 0x1a84,
            MCAN_XIDFC = 0x1a88,
            MCAN_XIDAM = 0x1a90,
            MCAN_HPMS = 0x1a94,
            MCAN_NDAT1 = 0x1a98,
            MCAN_NDAT2 = 0x1a9c,
            MCAN_RXF0C = 0x1aa0,
            MCAN_RXF0S = 0x1aa4,
            MCAN_RXF0A = 0x1aa8,
            MCAN_RXBC = 0x1aac,
            MCAN_RXF1C = 0x1ab0,
            MCAN_RXF1S = 0x1ab4,
            MCAN_RXF1A = 0x1ab8,
            MCAN_RXESC = 0x1abc,
            MCAN_TXBC = 0x1ac0,
            MCAN_TXFQS = 0x1ac4,
            MCAN_TXESC = 0x1ac8,
            MCAN_TXBRP = 0x1acc,
            MCAN_TXBAR = 0x1ad0,
            MCAN_TXBCR = 0x1ad4,
            MCAN_TXBTO = 0x1ad8,
            MCAN_TXBCF = 0x1adc,
            MCAN_TXBTIE = 0x1ae0,
            MCAN_TXBCIE = 0x1ae4,
            MCAN_TXEFC = 0x1af0,
            MCAN_TXEFS = 0x1af4,
            MCAN_TXEFA = 0x1af8,
            MCAN_ECC_AGGR_REVISION = 0x1c00,
            MCAN_ECC_AGGR_VECTOR = 0x1c08,
            MCAN_ECC_AGGR_MISC_STATUS = 0x1c0c,
            MCAN_ECC_AGGR_WRAP_REVISION = 0x1c10,
            MCAN_ECC_AGGR_CONTROL = 0x1c14,
            MCAN_ECC_AGGR_ERROR_CTRL1 = 0x1c18,
            MCAN_ECC_AGGR_ERROR_CTRL2 = 0x1c1c,
            MCAN_ECC_AGGR_ERROR_STATUS1 = 0x1c20,
            MCAN_ECC_AGGR_ERROR_STATUS2 = 0x1c24,
            MCAN_ECC_AGGR_SEC_EOI_REG = 0x1c3c,
            MCAN_ECC_AGGR_SEC_STATUS_REG0 = 0x1c40,
            MCAN_ECC_AGGR_SEC_ENABLE_SET_REG0 = 0x1c80,
            MCAN_ECC_AGGR_SEC_ENABLE_CLR_REG0 = 0x1cc0,
            MCAN_ECC_AGGR_DED_EOI_REG = 0x1d3c,
            MCAN_ECC_AGGR_DED_STATUS_REG0 = 0x1d40,
            MCAN_ECC_AGGR_DED_ENABLE_SET_REG0 = 0x1d80,
            MCAN_ECC_AGGR_DED_ENABLE_CLR_REG0 = 0x1dc0,
        }
    }


    public class DRA78x_CAN_Msg
    {
        public DRA78x_CAN_Msg()
        {
            RTR = false;
            XTD = false;
            ESI = false;
            RXTS = 0x00;
            DLC = 0x00;
            BRS = false;
            FDF = false;
            ANMF = false;
            FIDX = 0x00;

        }
        public string GetAllValues ()
        {
            string msg = "";
            msg = "ID " + BitConverter.ToString(Identifier);
            if (ESI)
                msg += " ESI ";
            if (XTD)
                msg += "XTD ";
            if (XTD)
                msg += "RTR ";
            if (XTD)
                msg += "RTR ";
            if (ANMF)
                msg += "ANMF ";
            if (BRS)
                msg += "BRS ";
            if (FDF)
                msg += "FDF ";
            if (EFC)
                msg += "EFC ";

            msg += " FIDX " + FIDX.ToString();
            msg += " RXTS " + RXTS.ToString();
            msg += " TXTS " + TXTS.ToString();
            msg += " ET " + ET.ToString();

            msg += " DLC " + DLC.ToString();
            msg += " DB " + BitConverter.ToString(DB);
            return msg;
        }
        public Boolean ESI { get; set; }
        public Boolean XTD { get; set; }
        public Boolean RTR { get; set; }
        public byte[] Identifier { get; set; }
        public Boolean ANMF { get; set; }
        public uint FIDX { get; set; }
        public Boolean FDF { get; set; }
        public Boolean BRS { get; set; }
        public uint DLC { get; set; }
        public uint RXTS { get; set; }
        public byte []  DB { get; set; }

        public byte MM { set; get; }
        public Boolean EFC { set; get; }
        public byte ET { set; get; }
        public uint TXTS { get; set; }
    }

}

