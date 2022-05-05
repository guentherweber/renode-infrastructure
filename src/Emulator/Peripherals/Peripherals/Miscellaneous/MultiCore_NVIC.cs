//
// Copyright (c) 2010-2021 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
// Copyright (c) 2020-2021 Microsoft
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{ 
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class MultiCore_NVIC : IDoubleWordPeripheral
    {
        public MultiCore_NVIC(Machine machine, IDoubleWordPeripheral nvic0, IDoubleWordPeripheral nvic1)
        {
            this.machine = machine;
            this.nvics.Add(nvic0);
            this.nvics.Add(nvic1);
        }

        public uint ReadDoubleWord(long offset)
        {
            return nvics[machine.SystemBus.GetCurrentCPUId()].ReadDoubleWord(offset);
        }


        public void WriteDoubleWord(long offset, uint value)
        {
            nvics[machine.SystemBus.GetCurrentCPUId()].WriteDoubleWord(offset, value);
        }


        public void Reset()
        {
            nvics[machine.SystemBus.GetCurrentCPUId()].Reset();
        }

        private Machine machine;
        private List<IDoubleWordPeripheral> nvics = new List<IDoubleWordPeripheral>();

    }

}

