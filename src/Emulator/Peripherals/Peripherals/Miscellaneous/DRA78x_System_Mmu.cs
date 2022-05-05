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
    class DRA78x_System_Mmu : IKnownSize, IDoubleWordPeripheral, IWordPeripheral, IBytePeripheral, IPeripheral
    {
        public DRA78x_System_Mmu(Machine machine)
        {
            dwordregisters = new DoubleWordRegisterCollection(this);
            DefineRegisters();
            Reset();
        }
        private void DefineRegisters()
        {
            Registers.MMU_SYSSTATUS.Define(dwordregisters, 0x01, "IPU_MMU_REVISION")
             .WithValueField(0, 32, FieldMode.Read, name: "reserved");

            Registers.MMU_LOCK.Define(dwordregisters, 0x00, "IPU_MMU_REVISION")
             .WithValueField(0, 32, FieldMode.Read, name: "reserved");

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
            MMU_SYSSTATUS = 0x14,
            MMU_LOCK = 50,

        }


    }
}
