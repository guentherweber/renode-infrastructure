using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    class DRA78x_SpinLock: IKnownSize, IDoubleWordPeripheral, IWordPeripheral, IBytePeripheral, IPeripheral
    {
        public DRA78x_SpinLock(Machine machine)
        {
            dwordregisters = new DoubleWordRegisterCollection(this);
            DefineRegisters();
            Reset();
        }
        private void DefineRegisters()
        {
            Registers.SPINLOCK_REVISION.Define(dwordregisters, 0x00, "SPINLOCK_SYSTATUS")
             .WithValueField(0, 32, FieldMode.Read, name: "reserved");

            Registers.SPINLOCK_SYSCONFIG.Define(dwordregisters, 0x00, "SPINLOCK_SYSTATUS")
             .WithValueField(0, 32, FieldMode.Read, name: "reserved");

            Registers.SPINLOCK_SYSTATUS.Define(dwordregisters, 0x01, "SPINLOCK_SYSTATUS")
             .WithValueField(0, 32, FieldMode.Read, name: "reserved");
            
            Registers.SPINLOCK_LOCK_REG.DefineMany(dwordregisters, 256, (register, idx) =>
                        {
                            register
                                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "LOCK_REG");

                        }, stepInBytes: 4, resetValue: 0x00, name: "LOCK_REG");
            
        }

        public void Reset()
        {
            dwordregisters.Reset();
        }

        public uint ReadDoubleWord(long offset)
        {
            return dwordregisters.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            dwordregisters.Write(offset, value);
        }

        public ushort ReadWord(long offset)
        {
            return (ushort)dwordregisters.Read(offset);
        }

        public void WriteWord(long offset, ushort value)
        {
            dwordregisters.Write(offset, value);
        }

        public byte ReadByte(long offset)
        {
            return (byte)dwordregisters.Read(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            dwordregisters.Write(offset, value);
        }

        void IPeripheral.Reset()
        {
            dwordregisters.Reset();
        }

        private readonly DoubleWordRegisterCollection dwordregisters;

        long IKnownSize.Size => 0x1000;

        private enum Registers : long
        {
            SPINLOCK_REVISION = 0x00,
            SPINLOCK_SYSCONFIG = 0x10,
            SPINLOCK_SYSTATUS = 0x14,
            SPINLOCK_LOCK_REG = 0x800,
        }


    }
}
