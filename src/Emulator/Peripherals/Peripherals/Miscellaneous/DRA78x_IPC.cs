using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Migrant;
using Antmicro.Renode.Time;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Peripherals.Sensors;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    [AllowedTranslations(AllowedTranslation.WordToDoubleWord | AllowedTranslation.ByteToDoubleWord)]
    public class DRA78x_IPC : IKnownSize, IDoubleWordPeripheral, IWordPeripheral, IBytePeripheral
    {


        public long Size => 0x1000;

        public GPIO IRQ_User0 { get; private set; }
        public GPIO IRQ_User1 { get; private set; }
        public GPIO IRQ_User2 { get; private set; }
        public GPIO IRQ_User3 { get; private set; }



        public DRA78x_IPC(Machine machine)
        {
            this.machine = machine;
            dwordregisters = new DoubleWordRegisterCollection(this);

            IRQ_User0 = new GPIO();
            IRQ_User1 = new GPIO();
            IRQ_User2 = new GPIO();
            IRQ_User3 = new GPIO();

            irqs.Add(0, IRQ_User0);
            irqs.Add(1, IRQ_User1);
            irqs.Add(2, IRQ_User2);
            irqs.Add(3, IRQ_User3);


            for (int i=0; i < NumberOfMessageQueues; i++)
                msgQueues.Add(i, new Queue<UInt32>());

            DefineRegisters();
            Reset();

        }






        public void Reset()
        {
            dwordregisters.Reset();
        }


        public uint ReadMessage(int box)
        {
            uint message = ReadDoubleWord((long)Registers.MAILBOX_MESSAGE + 4 * box);
            return message;
        }

        public uint ReadMsgStatus(int box)
        {
            uint count = ReadDoubleWord((long)Registers.MAILBOX_MSGSTATUS + 4 * box);
            this.Log(LogLevel.Noisy, "Read Message Status:  box {0}  num msgs {1}", box, count);
            return count;
        }
        public uint ReadFifoStatus(int box)
        {
            uint status = ReadDoubleWord((long)Registers.MAILBOX_FIFOSTATUS + 4 * box);
            this.Log(LogLevel.Noisy, "Read Fifo Status:  box {0}  fifostatus {1}", box, status);
            return status;
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

        public uint ReadDoubleWord(long offset)
        {
            return dwordregisters.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            dwordregisters.Write(offset, value);
        }
        private void UpdateInterrupt()
        {
            for (int user = 0; user < NumberOfUsers; user++)
            {
                if ((IntEnableSet[user].Value & IRQ_statusclr[user].Value) > 0)
                {
                    irqs[user].Set(true);
                    this.Log(LogLevel.Noisy, "Int Status user {0} active", user);
                }
                else
                {
                    irqs[user].Set(false);
                    this.Log(LogLevel.Noisy, "Int Status user {0} inactive", user);
                }
            }

        }
        private readonly DoubleWordRegisterCollection dwordregisters;
        private const int NumberOfUsers = 4;
        private const int NumberOfMessageQueues = 12;
        private IValueRegisterField[] NumberofMsgInQueue = new IValueRegisterField[NumberOfMessageQueues];
        private IValueRegisterField[] IRQ_statusclr = new IValueRegisterField[NumberOfUsers];
        private IValueRegisterField[] IRQ_statusraw = new IValueRegisterField[NumberOfUsers];
        private IValueRegisterField[] IntEnableSet = new IValueRegisterField[NumberOfUsers];
        private IValueRegisterField[] IntEnableClr = new IValueRegisterField[NumberOfUsers];

        private IDictionary<int, Queue<UInt32>> msgQueues = new Dictionary<int, Queue<UInt32>>();
        private IDictionary<int, GPIO> irqs = new Dictionary<int, GPIO>();
        private Machine machine;

        private void SignalNewMessage(int box)
        {
            byte bytepos = (byte)(box * 2);
            for (int user = 0; user < NumberOfUsers; user++)
            {
                var state = BitHelper.IsBitSet(IntEnableSet[user].Value, bytepos);
                IRQ_statusraw[user].SetBit(bytepos, state);
                IRQ_statusclr[user].SetBit(bytepos, state);
            }
            UpdateInterrupt();
        }

        private void SignalEmptyMessage(int box)
        {
            byte bytepos = (byte)((box * 2) + 1);
            for (int user = 0; user < NumberOfUsers; user++)
            {
                var state = BitHelper.IsBitSet(IntEnableSet[user].Value, bytepos);
                IRQ_statusraw[user].SetBit(bytepos, state);
                IRQ_statusclr[user].SetBit(bytepos, state);
            }
            UpdateInterrupt();
        }


        private void DefineRegisters()
        {
            Registers.MAILBOX_SYSCONFIG.Define(dwordregisters, 0x00, "MAILBOX_SYSCONFIG")
                 .WithFlag(0, FieldMode.Read | FieldMode.WriteOneToClear, name: "SOFTRESET")
                 .WithFlag(1, FieldMode.Read | FieldMode.Set, name: "reserved")
                 .WithValueField(2, 2, FieldMode.Read | FieldMode.Write, name: "SIDLEMODE")
                 .WithValueField(4, 28, FieldMode.Read | FieldMode.Write, name: "reserved");

            Registers.MAILBOX_MESSAGE.DefineMany(dwordregisters, NumberOfMessageQueues, (register, idx) =>
            {
                register
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write,
                    writeCallback: (_, value) =>
                    {
                        this.Log(LogLevel.Noisy, "Send Message box {0} msg 0x{1:X}", idx, value);
                        msgQueues[idx].Enqueue(value);
                        SignalNewMessage(idx);
                    },
                    valueProviderCallback: _ =>
                    {
                        var msg = msgQueues[idx].Dequeue();
                        this.Log(LogLevel.Noisy, "Receive Message box {0} msg 0x{1:X}", idx, msg);
                        if (msgQueues.Count == 0)
                            SignalEmptyMessage(idx);
                        return msg;
                    }, name: "MAILBOX_MESSAGE");

            }, stepInBytes: 4, resetValue: 0x00, name: "MAILBOX_MESSAGE");

            Registers.MAILBOX_MSGSTATUS.DefineMany(dwordregisters, NumberOfMessageQueues, (register, idx) =>
            {
                register
                    .WithValueField(0, 32, out NumberofMsgInQueue[idx], FieldMode.Read,
                                        valueProviderCallback: _ =>
                                        {
                                            if (idx != 2)
                                                return (uint)msgQueues[idx].Count;
                                            else
                                                return 0;
                                        }, name: "NBOFMSGMBM");

            }, stepInBytes: 4, resetValue: 0x00, name: "MSGSTATUS");

            Registers.MAILBOX_FIFOSTATUS.DefineMany(dwordregisters, NumberOfMessageQueues, (register, idx) =>
            {
                register
                    .WithFlag(0, FieldMode.Read, name: "FIFOFULLMBM")
                    .WithValueField(1, 31, FieldMode.Read, name: "reserved");

            }, stepInBytes: 4, resetValue: 0x00, name: "FIFOSTATUS");


            Registers.MAILBOX_IRQSTATUS_RAW.DefineMany(dwordregisters, NumberOfUsers, (register, idx) =>
            {
                register
                    .WithValueField(0, 32, out IRQ_statusraw[idx], FieldMode.Read | FieldMode.Set, name: "MAILBOX_IRQSTATUS_RAW");

            }, stepInBytes: 0x10, resetValue: 0x00, name: "MAILBOX_IRQSTATUS_RAW");

            Registers.MAILBOX_IRQSTATUS_CLR.DefineMany(dwordregisters, NumberOfUsers, (register, idx) =>
            {
                register
                    .WithValueField(0, 32, out IRQ_statusclr[idx], FieldMode.Read | FieldMode.WriteOneToClear, writeCallback: (_, value) =>
                    {
                        IRQ_statusclr[idx].Value &= ~value;
                        this.Log(LogLevel.Noisy, "Int Status Clear user {0} statusclr Reg 0x{1:X}", idx, IRQ_statusclr[idx].Value);
                        for (byte bitpos = 0; bitpos < NumberOfMessageQueues * 2; bitpos += 2)
                        {
                            if (BitHelper.IsBitSet(IRQ_statusclr[idx].Value, bitpos))
                                this.Log(LogLevel.Noisy, "message pending user:{0} Box: {1}", idx, bitpos / 2);
                            else
                                this.Log(LogLevel.Noisy, "no message pending user:{0} Box: {1}", idx, bitpos / 2);
                        }
                        for (byte bitpos = 1; bitpos < NumberOfMessageQueues * 2 + 1; bitpos += 2)
                        {
                            if (BitHelper.IsBitSet(IRQ_statusclr[idx].Value, bitpos))
                                this.Log(LogLevel.Noisy, "message queue not full user:{0} Box: {1}", idx, bitpos / 2);
                            else
                                this.Log(LogLevel.Noisy, "message queue full user:{0} Box: {1}", idx, bitpos / 2);

                        }
                        UpdateInterrupt();
                    },
                    name: "MAILBOX_IRQSTATUS_CLR");

            }, stepInBytes: 0x10, resetValue: 0x00, name: "MAILBOX_IRQSTATUS_CLR");

            Registers.MAILBOX_IRQENABLE_CLR.DefineMany(dwordregisters, NumberOfUsers, (register, idx) =>
            {
                register
                    .WithValueField(0, 32, out IntEnableClr[idx], FieldMode.Read | FieldMode.WriteOneToClear, writeCallback: (_, value) =>
                    {
                        IntEnableClr[idx].Value &= ~value;
                        IntEnableSet[idx].Value &= ~value;
                        this.Log(LogLevel.Noisy, "Int Enable Clear user {0} ClrReg 0x{1:X}  SetReg 0x{2:X}", idx, IntEnableClr[idx].Value, IntEnableSet[idx].Value);
                        for (byte bitpos = 0; bitpos < NumberOfMessageQueues * 2; bitpos += 2)
                        {
                            if (BitHelper.IsBitSet(IntEnableSet[idx].Value, bitpos))
                                this.Log(LogLevel.Noisy, "Int Enable NewMessag User:{0} Box: {1}", idx, bitpos / 2);
                            else
                                this.Log(LogLevel.Noisy, "Int Disable NewMessag  User:{0} Box: {1}", idx, bitpos / 2);

                        }
                        for (byte bitpos = 1; bitpos < NumberOfMessageQueues * 2 + 1; bitpos += 2)
                        {
                            if (BitHelper.IsBitSet(IntEnableSet[idx].Value, bitpos))
                                this.Log(LogLevel.Noisy, "Int Enable NotFull User:{0} Box: {1}", idx, (bitpos - 1) / 2);
                            else
                                this.Log(LogLevel.Noisy, "Int Disable NotFull  User:{0} Box: {1}", idx, (bitpos - 1) / 2);
                        }

                    }, name: "MAILBOX_IRQENABLE_CLR");

            }, stepInBytes: 0x10, resetValue: 0x00, name: "MAILBOX_IRQENABLE_CLR");

            Registers.MAILBOX_IRQENABLE_SET.DefineMany(dwordregisters, NumberOfUsers, (register, idx) =>
            {
                register
                    .WithValueField(0, 32, out IntEnableSet[idx], FieldMode.Read | FieldMode.Set, writeCallback: (_, value) =>
                    {
                        IntEnableSet[idx].Value |= value;
                        IntEnableClr[idx].Value &= ~value;
                        this.Log(LogLevel.Noisy, "Int Enable Set user {0} ClrReg 0x{1:X}  SetReg 0x{2:X}", idx, IntEnableClr[idx].Value, IntEnableSet[idx].Value);
                        for (byte bitpos = 0; bitpos < NumberOfMessageQueues * 2; bitpos += 2)
                        {
                            if (BitHelper.IsBitSet(IntEnableSet[idx].Value, bitpos))
                                this.Log(LogLevel.Noisy, "INT Enabled New Message User:{0} Box: {1}", idx, bitpos / 2);
                            else
                                this.Log(LogLevel.Noisy, "INT Disabled New Message User:{0} Box: {1}", idx, bitpos / 2);

                        }
                        for (byte bitpos = 1; bitpos < NumberOfMessageQueues * 2 + 1; bitpos += 2)
                        {
                            if (BitHelper.IsBitSet(IntEnableSet[idx].Value, bitpos))
                                this.Log(LogLevel.Noisy, "INT Enabled Not Full  User:{0} Box: {1}", idx, (bitpos - 1) / 2);
                            else
                                this.Log(LogLevel.Noisy, "INT Disable Not Full User:{0} Box: {1}", idx, (bitpos - 1) / 2);

                        }
                    }, name: "MAILBOX_IRQENABLE_SET");

            }, stepInBytes: 0x10, resetValue: 0x00, name: "MAILBOX_IRQENABLE_SET");


            Registers.MAILBOX_EOI.Define(dwordregisters, 0x00, "MAILBOX_EOI")
                 .WithValueField(0, 32, FieldMode.Write, name: "reserved");

        }


        private enum Registers : long
        {
            MAILBOX_REVISION      = 0x0,
            MAILBOX_SYSCONFIG     = 0x10,

            MAILBOX_MESSAGE       = 0x40,
            MAILBOX_FIFOSTATUS    = 0x80,
            MAILBOX_MSGSTATUS     = 0xC0,

            MAILBOX_IRQSTATUS_RAW = 0x100,
            MAILBOX_IRQSTATUS_CLR = 0x104,
            MAILBOX_IRQENABLE_CLR = 0x10C,
            MAILBOX_IRQENABLE_SET = 0x108,

            MAILBOX_EOI           = 0x140

        }


    }
}