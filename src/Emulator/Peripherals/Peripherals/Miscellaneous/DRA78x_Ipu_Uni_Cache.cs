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
    class DRA78x_Ipu_Uni_Cache : IKnownSize, IDoubleWordPeripheral, IWordPeripheral, IBytePeripheral, IPeripheral
    {
        public DRA78x_Ipu_Uni_Cache(Machine machine)
        {
            dwordregisters = new DoubleWordRegisterCollection(this);
            DefineRegisters();
            Reset();
        }
        private void DefineRegisters()
        {
            Registers.CACHE_CONFIG.Define(dwordregisters, 0x00, "CACHE_CONFIG")
                .WithValueField(0, 32, FieldMode.Read, name: "reserved");
            Registers.CACHE_MAINT.Define(dwordregisters, 0x00, "CACHE_MAINT")
                .WithValueField(0, 32, FieldMode.Read, name: "reserved");
            Registers.CACHE_MTSTART.Define(dwordregisters, 0x00, "CACHE_MTSTART")
                .WithValueField(0, 32, FieldMode.Read, name: "reserved");
            Registers.CACHE_MTEND.Define(dwordregisters, 0x00, "CACHE_MTEND")
                .WithValueField(0, 32, FieldMode.Read, name: "reserved");

            Registers.CACHE_SCTM_CTCNTL.Define(dwordregisters, 0x00, "CACHE_SCTM_CTCNTL")
                .WithValueField(0, 32, FieldMode.Read, name: "reserved");
            Registers.CACHE_SCTM_CTCR_WT.Define(dwordregisters, 0x00, "CACHE_SCTM_CTCR_WT")
                .WithValueField(0, 32, FieldMode.Read, name: "reserved");
            Registers.CACHE_SCTM_CTCNTR.Define(dwordregisters, 0x00, "CACHE_SCTM_CTCNTR")
                .WithValueField(0, 32, FieldMode.Read, name: "reserved");
            Registers.CACHE_MMU_SMALL_POLICY.Define(dwordregisters, 0x00, "CACHE_MMU_SMALL_POLICY")
                .WithValueField(0, 32, FieldMode.Read, name: "reserved");


            /*
                                    Registers.SPINLOCK_LOCK_REG.DefineMany(dwordregisters, 256, (register, idx) =>
                                    {
                                        register
                                            .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "LOCK_REG");

                                    }, stepInBytes: 4, resetValue: 0x00, name: "LOCK_REG");
                        */
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
            CACHE_CONFIG = 0x04,
            CACHE_MAINT = 0x10,
            CACHE_MTSTART = 0x14,
            CACHE_MTEND = 0x18,
            CACHE_SCTM_CTCNTL = 0x400,
            CACHE_SCTM_CTCR_WT = 0x500,
            CACHE_SCTM_CTCNTR = 0x580,
            CACHE_MMU_SMALL_POLICY = 0xA44

        }


    }
}
