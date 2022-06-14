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
    class DRA78x_Ipu_Mmu : IKnownSize, IDoubleWordPeripheral, IWordPeripheral, IBytePeripheral, IPeripheral
    {
        public DRA78x_Ipu_Mmu(Machine machine)
        {
            dwordregisters = new DoubleWordRegisterCollection(this);
            DefineRegisters();
            Reset();
        }
        private void DefineRegisters()
        {
            Registers.IPU_MMU_REVISION.Define(dwordregisters, 0x01, "IPU_MMU_REVISION")
             .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");
            Registers.IPU_MMU_SYSSTATUS.Define(dwordregisters, 0x01, "IPU_MMU_SYSSTATUS")
             .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");
            Registers.IPU_MMU_SYSCONFIG.Define(dwordregisters, 0x00, "IPU_MMU_SYSCONFIG")
             .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");
            Registers.IPU_MMU_IRQSTATUS.Define(dwordregisters, 0x00, "IPU_MMU_IRQSTATUS")
             .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");

            Registers.IPU_MMU_IRQENABLE.Define(dwordregisters, 0x00, "IPU_MMU_IRQENABLE")
             .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");
            Registers.IPU_MMU_CNTL.Define(dwordregisters, 0x00, "IPU_MMU_CNTL")
             .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");
            Registers.IPU_MMU_LOCK.Define(dwordregisters, 0x00, "IPU_MMU_LOCK")
             .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");
            Registers.IPU_MMU_LD_TLB.Define(dwordregisters, 0x00, "IPU_MMU_LD_TLB")
             .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");
            Registers.IPU_MMU_TTB.Define(dwordregisters, 0x00, "IPU_MMU_LD_TTB")
             .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");
            Registers.IPU_MMU_CAM.Define(dwordregisters, 0x00, "IPU_MMU_CAM")
             .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");
            Registers.IPU_MMU_RAM.Define(dwordregisters, 0x00, "IPU_MMU_RAM")
             .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");
            Registers.IPU_MMU_GFLUSH.Define(dwordregisters, 0x00, "IPU_MMU_GFLUSH")
             .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");
            Registers.IPU_MMU_GPR.Define(dwordregisters, 0x00, "IPU_MMU_GPR")
             .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");

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
            IPU_MMU_REVISION  = 0x00,
            IPU_MMU_SYSCONFIG = 0x10,
            IPU_MMU_SYSSTATUS = 0x14,
            IPU_MMU_IRQSTATUS = 0x18,
            IPU_MMU_IRQENABLE = 0x1C,
            IPU_MMU_CNTL      = 0x44,
            IPU_MMU_TTB       = 0x4C,
            IPU_MMU_LOCK = 0x50,
            IPU_MMU_LD_TLB = 0x54,
            IPU_MMU_CAM = 0x58,
            IPU_MMU_RAM = 0x5C,
            IPU_MMU_GFLUSH = 0x60,
            IPU_MMU_GPR = 0x88,

        }


    }
}
