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
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{ 
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class MultiCore_NVIC : SimpleContainer<IDoubleWordPeripheral>, IDoubleWordPeripheral
    {
        public MultiCore_NVIC(Machine machine):base(machine)
        {
        }

        public uint ReadDoubleWord(long offset)
        {
            IDoubleWordPeripheral nvic;
            TryGetByAddress(machine.SystemBus.GetCurrentCPUId(), out nvic);
            return nvic.ReadDoubleWord(offset);
        }

        public override void Reset()
        {
            foreach (IDoubleWordPeripheral child in Children)
            {
                child.Reset();
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            IDoubleWordPeripheral nvic;
            TryGetByAddress(machine.SystemBus.GetCurrentCPUId(), out nvic);
            nvic.WriteDoubleWord(offset,value);
        }

    }

}

