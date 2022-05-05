using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Antmicro.Renode.Peripherals.Mocks
{
    class DummyPeripheral : IKnownSize, IDoubleWordPeripheral, IWordPeripheral, IBytePeripheral, IPeripheral
    {
        public DummyPeripheral(Machine machine, uint count = 1, uint resetvalue = 0x00)
        {
            SizeCount = count;
            ResetValue = resetvalue;
            dwordregisters = new DoubleWordRegisterCollection(this);
            DefineRegisters();
            Reset();
        }
        private void DefineRegisters()
        {
            Registers.START_ADR.DefineMany(dwordregisters, SizeCount, (register, idx) =>
                {
                    register
                        .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "REGISTER");

                }, stepInBytes: 4, resetValue: ResetValue, name: "REGISTER");

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
            return (ushort) dwordregisters.Read(offset);
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
            throw new NotImplementedException();
        }

        private readonly DoubleWordRegisterCollection dwordregisters;
        private uint SizeCount;
        private uint ResetValue;

        long IKnownSize.Size => SizeCount;

        private enum Registers : long
        {
            START_ADR = 0x0,
        }
    }
}
