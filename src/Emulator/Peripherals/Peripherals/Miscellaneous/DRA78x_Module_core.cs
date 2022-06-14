
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;


using Antmicro.Renode.Core.Structure.Registers;



namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    class DRA78x_Module_Core : IKnownSize, IDoubleWordPeripheral, IWordPeripheral, IBytePeripheral, IPeripheral
    {
        public DRA78x_Module_Core(Machine machine)
        {

            mach = machine;
            dwordregisters = new DoubleWordRegisterCollection(this);
            DefineRegisters();
            Reset();
        }
        private void DefineRegisters()
        {
            Registers.CTRL_CORE_ROM_AUXBOOT0.Define(dwordregisters, 0x00, "CTRL_CORE_ROM_AUXBOOT0")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "AUXBOOT0");

            Registers.CTRL_CORE_ROM_AUXBOOT1.Define(dwordregisters, 0x00, "CTRL_CORE_ROM_AUXBOOT1")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "AUXBOOT1");
            
            Registers.CM_L4PER_CLKSTCTRL.Define(dwordregisters, 0x1000000, "CM_L4PER_CLKSTCTRL")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");

            Registers.GPIO2_CLKCTRL.Define(dwordregisters, 0x01000000, "GPIO2_CLKCTRL")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write,  name: "reserved");

            Registers.CTRL_DUMMY1.Define(dwordregisters, 0x01, "CTRL_DUMMY1")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");
            Registers.CTRL_DUMMY2.Define(dwordregisters, 0x01, "CTRL_DUMMY2")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");
            Registers.DSP1_CLKSTCTRL.Define(dwordregisters, 0x00000100, "DSP1_CLKSTCTRL")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");
            Registers.DSP2_CLKSTCTRL.Define(dwordregisters, 0x00000100, "DSP2_CLKSTCTRL")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");

            Registers.CTRL_CORE_ADR0.DefineMany(dwordregisters, 0x729, (register, idx) =>
            {
                register
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");

            }, stepInBytes: 4, resetValue: 0x00, name: "CTRL_CORE_ADR0");

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
        private Machine mach;
        long IKnownSize.Size => 0x8000;



        private enum Registers : long
        {
            CTRL_CORE_ADR0         = 0x0000,
            CTRL_CORE_ROM_AUXBOOT0 = 0x1CA8,
            CTRL_CORE_ROM_AUXBOOT1 = 0x1CAC,
            CTRL_DUMMY2            = 0x3238,
            CTRL_DUMMY1            = 0x32AC,
            DSP1_CLKSTCTRL         = 0x3400,
            DSP2_CLKSTCTRL         = 0x3600,
            GPIO2_CLKCTRL          = 0x7760,
            CM_L4PER_CLKSTCTRL     = 0x7700,

        }


    }
}
