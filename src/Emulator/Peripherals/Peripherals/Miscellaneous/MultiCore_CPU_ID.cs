//
// Copyright (c) 2010-2021 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
// Copyright (c) 2020-2021 Microsoft
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class MultiCore_CPU_ID : IDoubleWordPeripheral, IWordPeripheral, IBytePeripheral, IKnownSize
    {
        public MultiCore_CPU_ID(Machine machine)
        {
            this.machine = machine;

        }

        public uint ReadDoubleWord(long offset)
        {
            return (uint) machine.SystemBus.GetCurrentCPUId();
        }

        public void WriteDoubleWord(long offset, uint value)
        {
        }

        public ushort ReadWord(long offset)
        {
            return (ushort)machine.SystemBus.GetCurrentCPUId();
        }

        public void WriteWord(long offset, ushort value)
        {
        }

        public byte ReadByte(long offset)
        {
            return (byte)machine.SystemBus.GetCurrentCPUId();
        }

        public void WriteByte(long offset, byte value)
        {
        }

        public void Reset()
        {
        }

        public long Size
        {
            get
            {
                return 4;
            }
        }
        
    private Machine machine;

    }

}

