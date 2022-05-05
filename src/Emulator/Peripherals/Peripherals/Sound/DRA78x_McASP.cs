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

namespace Antmicro.Renode.Peripherals.Sound
{
    [AllowedTranslations( AllowedTranslation.WordToDoubleWord | AllowedTranslation.ByteToDoubleWord)]
    public class DRA78x_McASP : IKnownSize, IDoubleWordPeripheral, IWordPeripheral, IBytePeripheral, IGPIOReceiver
    {
        
        private readonly DoubleWordRegisterCollection dwordregisters;

        public long Size => 0x2000;
        private const long DefaultPeripheralFrequency = 32000000;
        private IValueRegisterField Pins;
        public GPIO IRQ { get; private set; }



        public DRA78x_McASP( Machine machine, long frequency = DefaultPeripheralFrequency)
        {
            dwordregisters = new DoubleWordRegisterCollection(this);

//            IRQ = new GPIO();


            DefineRegisters();
            Reset();

        }

        
        private void DefineRegisters()
        {
            Registers.MCASP_GBLCTL.Define(dwordregisters, 0x00, "MCASP_GBLCTL")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "MCASP_GBLCTL");
            Registers.MCASP_AHCLKXCTL.Define(dwordregisters, 0x00, "MCASP_AHCLKXCTL")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "MCASP_AHCLKXCTL");
            Registers.MCASP_ACLKXCTL.Define(dwordregisters, 0x00, "MCASP_ACLKXCTL")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "MCASP_ACLKXCTL");
            Registers.MCASP_PWRIDLESYSCONFIG.Define(dwordregisters, 0x00, "MCASP_PWRIDLESYSCONFIG")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "MCASP_PWRIDLESYSCONFIG");
            Registers.MCASP_TXMASK.Define(dwordregisters, 0x00, "MCASP_TXMASK")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "MCASP_TXMASK");
            Registers.MCASP_TXFMT.Define(dwordregisters, 0x00, "MCASP_TXFMT")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "MCASP_TXFMT");
            Registers.MCASP_TXFMCTL.Define(dwordregisters, 0x00, "MCASP_TXFMCTL")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "MCASP_TXFMCTL");
            Registers.MCASP_TXCLKCHK.Define(dwordregisters, 0x00, "MCASP_TXCLKCHK")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "MCASP_TXCLKCHK");
            Registers.MCASP_TXTDM.Define(dwordregisters, 0x00, "MCASP_TXTDM")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "MCASP_TXTDM");
            Registers.MCASP_RXMASK.Define(dwordregisters, 0x00, "MCASP_RXMASK")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "MCASP_RXMASK");
            Registers.MCASP_RXFMT.Define(dwordregisters, 0x00, "MCASP_RXFMT")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "MCASP_RXFMT");


            Registers.MCASP_RXFMCTL.Define(dwordregisters, 0x00, "MCASP_RXFMCTL")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "MCASP_RXFMCTL");
            Registers.MCASP_AHCLKRCTL.Define(dwordregisters, 0x00, "MCASP_AHCLKRCTL")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "MCASP_AHCLKRCTL");
            Registers.MCASP_ACLKRCTL.Define(dwordregisters, 0x00, "MCASP_ACLKRCTL")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "MCASP_ACLKRCTL");
            Registers.MCASP_RXCLKCHK.Define(dwordregisters, 0x00, "MCASP_RXCLKCHK")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "MCASP_RXCLKCHK");
            Registers.MCASP_RXTDM.Define(dwordregisters, 0x00, "MCASP_RXTDM")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "MCASP_RXTDM");

            Registers.MCASP_TXSTAT.Define(dwordregisters, 0x0000, "MCASP_TXSTAT")
                    .WithFlag(0, FieldMode.Read | FieldMode.Write, name: "XUNDRN")
                    .WithFlag(1, FieldMode.Read | FieldMode.WriteOneToClear, name: "XSYNCERR")
                    .WithFlag(2, FieldMode.Read | FieldMode.WriteOneToClear, name: "XCKFAIL")
                    .WithFlag(3, FieldMode.Read | FieldMode.Write, name: "XTDMSLOT")
                    .WithFlag(4, FieldMode.Read | FieldMode.WriteOneToClear, name: "XLAST")
                    .WithFlag(5, FieldMode.Read | FieldMode.WriteOneToClear, name: "XDATA")
                    .WithFlag(6, FieldMode.Read | FieldMode.WriteOneToClear, name: "XSTAFRM")
                    .WithFlag(7, FieldMode.Read | FieldMode.WriteOneToClear, name: "XDMAERR")
                    .WithFlag(8, FieldMode.Read | FieldMode.WriteOneToClear, name: "XERR")
                    .WithValueField(9, 23, FieldMode.Read | FieldMode.Write, name: "unused");

            Registers.MCASP_RXSTAT.Define(dwordregisters, 0x00, "MCASP_RXSTAT")
                    .WithFlag(0, FieldMode.Read | FieldMode.Write, name: "RUNDRN")
                    .WithFlag(1, FieldMode.Read | FieldMode.WriteOneToClear, name: "RSYNCERR")
                    .WithFlag(2, FieldMode.Read | FieldMode.WriteOneToClear, name: "RCKFAIL")
                    .WithFlag(3, FieldMode.Read | FieldMode.Write, name: "RTDMSLOT")
                    .WithFlag(4, FieldMode.Read | FieldMode.WriteOneToClear, name: "RLAST")
                    .WithFlag(5, FieldMode.Read | FieldMode.WriteOneToClear, name: "RDATA")
                    .WithFlag(6, FieldMode.Read | FieldMode.WriteOneToClear, name: "RSTAFRM")
                    .WithFlag(7, FieldMode.Read | FieldMode.WriteOneToClear, name: "RDMAERR")
                    .WithFlag(8, FieldMode.Read | FieldMode.WriteOneToClear, name: "RERR")
                    .WithValueField(9, 23, FieldMode.Read | FieldMode.Write, name: "unused");

            Registers.MCASP_XRSRCTL0.Define(dwordregisters, 0x00, "MCASP_XRSRCTL0")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "MCASP_XRSRCTL0");
            Registers.MCASP_XRSRCTL1.Define(dwordregisters, 0x00, "MCASP_XRSRCTL1")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "MCASP_XRSRCTL1");
            Registers.MCASP_XRSRCTL2.Define(dwordregisters, 0x00, "MCASP_XRSRCTL2")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "MCASP_XRSRCTL2");
            Registers.MCASP_XRSRCTL3.Define(dwordregisters, 0x00, "MCASP_XRSRCTL3")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "MCASP_XRSRCTL3");
            Registers.MCASP_XRSRCTL4.Define(dwordregisters, 0x00, "MCASP_XRSRCTL4")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "MCASP_XRSRCTL4");
            Registers.MCASP_XRSRCTL5.Define(dwordregisters, 0x00, "MCASP_XRSRCTL5")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "MCASP_XRSRCTL5");
            Registers.MCASP_XRSRCTL6.Define(dwordregisters, 0x00, "MCASP_XRSRCTL6")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "MCASP_XRSRCTL6");
            Registers.MCASP_XRSRCTL7.Define(dwordregisters, 0x00, "MCASP_XRSRCTL7")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "MCASP_XRSRCTL7");
            Registers.MCASP_XRSRCTL8.Define(dwordregisters, 0x00, "MCASP_XRSRCTL80")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "MCASP_XRSRCTL8");
            Registers.MCASP_XRSRCTL9.Define(dwordregisters, 0x00, "MCASP_XRSRCTL9")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "MCASP_XRSRCTL9");
            Registers.MCASP_XRSRCTL10.Define(dwordregisters, 0x00, "MCASP_XRSRCTL10")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "MCASP_XRSRCTL10");
            Registers.MCASP_XRSRCTL11.Define(dwordregisters, 0x00, "MCASP_XRSRCTL11")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "MCASP_XRSRCTL11");
            Registers.MCASP_XRSRCTL12.Define(dwordregisters, 0x00, "MCASP_XRSRCTL12")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "MCASP_XRSRCTL12");
            Registers.MCASP_XRSRCTL13.Define(dwordregisters, 0x00, "MCASP_XRSRCTL13")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "MCASP_XRSRCTL13");
            Registers.MCASP_XRSRCTL14.Define(dwordregisters, 0x00, "MCASP_XRSRCTL14")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "MCASP_XRSRCTL14");
            Registers.MCASP_XRSRCTL15.Define(dwordregisters, 0x00, "MCASP_XRSRCTL15")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "MCASP_XRSRCTL15");

            Registers.MCASP_TXDITCTL.Define(dwordregisters, 0x00, "MCASP_TXDITCTL")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "MCASP_TXDITCTL");
            Registers.MCASP_PFUNC.Define(dwordregisters, 0x00, "MCASP_PFUNC")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "MCASP_PFUNC");
            Registers.MCASP_PDIR.Define(dwordregisters, 0x00, "MCASP_PDIR")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "MCASP_PDIR");
            Registers.MCASP_EVTCTLX.Define(dwordregisters, 0x00, "MCASP_EVTCTLX")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "MCASP_EVTCTLX");
            Registers.MCASP_EVTCTLR.Define(dwordregisters, 0x00, "MCASP_EVTCTLR")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "MCASP_EVTCTLR");


            Registers.MCASP_PDIN.Define(dwordregisters, 0x00, "MCASP_PDIN")
                    .WithValueField(0, 32, out Pins, FieldMode.Read | FieldMode.Write, name: "events");
/*                    .WithFlag(26, FieldMode.Read | FieldMode.WriteOneToClear, name: "ACLKX")
                    .WithFlag(27, FieldMode.Read | FieldMode.WriteOneToClear, name: "AHCLKX")
                    .WithFlag(28, FieldMode.Read | FieldMode.WriteOneToClear, name: "AFSX")
                    .WithFlag(29, FieldMode.Read | FieldMode.WriteOneToClear, name: "ACLKR")
                    .WithFlag(30, FieldMode.Read | FieldMode.WriteOneToClear, name: "reserved")
                    .WithFlag(31, FieldMode.Read | FieldMode.WriteOneToClear, name: "AFSR");
*/



        }




        public  void Reset()
        {
            dwordregisters.Reset();
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

        public void OnGPIO(int number, bool value)
        {
            this.Log(LogLevel.Noisy, "OnGPIO {0} {1}", number, value);
            if (value == true)
                Pins.Value = 0xFFFFFFFF;
            else
                Pins.Value = 0x00000000;
        }

        private enum Registers : long
        {

            MCASP_PID                = 0x0,
            MCASP_PWRIDLESYSCONFIG   = 0x4,
            MCASP_PFUNC              = 0x10,
            MCASP_PDIR               = 0x14,
            MCASP_PDOUT              = 0x18,
            MCASP_PDIN               = 0x1c,
            MCASP_PDSET              = 0x1c,
            MCASP_PDCLR              = 0x20,
            MCASP_GBLCTL             = 0x44,
            MCASP_AMUTE              = 0x48,
            MCASP_LBCTL              = 0x4c,
            MCASP_TXDITCTL           = 0x50,
            MCASP_GBLCTLR            = 0x60,
            MCASP_RXMASK             = 0x64,
            MCASP_RXFMT              = 0x68,
            MCASP_RXFMCTL            = 0x6c,
            MCASP_ACLKRCTL           = 0x70,
            MCASP_AHCLKRCTL          = 0x74,
            MCASP_RXTDM              = 0x78,
            MCASP_EVTCTLR            = 0x7c,
            MCASP_RXSTAT             = 0x80,
            MCASP_RXTDMSLOT          = 0x84,
            MCASP_RXCLKCHK           = 0x88,
            MCASP_REVTCTL            = 0x8c,
            MCASP_GBLCTLX            = 0xa0,
            MCASP_TXMASK             = 0xa4,
            MCASP_TXFMT              = 0xa8,
            MCASP_TXFMCTL            = 0xac,
            MCASP_ACLKXCTL           = 0xb0,
            MCASP_AHCLKXCTL          = 0xb4,
            MCASP_TXTDM              = 0xb8,
            MCASP_EVTCTLX            = 0xbc,
            MCASP_TXSTAT             = 0xc0,
            MCASP_TXTDMSLOT          = 0xc4,
            MCASP_TXCLKCHK           = 0xc8,
            MCASP_XEVTCTL            = 0xcc,
            MCASP_CLKADJEN           = 0xd0,
            MCASP_DITCSRA0           = 0x100,
            MCASP_DITCSRA1           = 0x104,
            MCASP_DITCSRA2           = 0x108,
            MCASP_DITCSRA3           = 0x10c,
            MCASP_DITCSRA4           = 0x110,
            MCASP_DITCSRA5           = 0x114,
            MCASP_DITCSRB0           = 0x118,
            MCASP_DITCSRB1           = 0x11c,
            MCASP_DITCSRB2           = 0x120,
            MCASP_DITCSRB3           = 0x124,
            MCASP_DITCSRB4           = 0x128,
            MCASP_DITCSRB5           = 0x12c,
            MCASP_DITUDRA0           = 0x130,
            MCASP_DITUDRA1           = 0x134,
            MCASP_DITUDRA2           = 0x138,
            MCASP_DITUDRA3           = 0x13c,
            MCASP_DITUDRA4           = 0x140,
            MCASP_DITUDRA5           = 0x144,
            MCASP_DITUDRB0           = 0x148,
            MCASP_DITUDRB1           = 0x14c,
            MCASP_DITUDRB2           = 0x150,
            MCASP_DITUDRB3           = 0x154,
            MCASP_DITUDRB4           = 0x158,
            MCASP_DITUDRB5           = 0x15c,
            MCASP_XRSRCTL0           = 0x180,
            MCASP_XRSRCTL1           = 0x184,
            MCASP_XRSRCTL2           = 0x188,
            MCASP_XRSRCTL3           = 0x18c,
            MCASP_XRSRCTL4           = 0x190,
            MCASP_XRSRCTL5           = 0x194,
            MCASP_XRSRCTL6           = 0x198,
            MCASP_XRSRCTL7           = 0x19c,
            MCASP_XRSRCTL8           = 0x1a0,
            MCASP_XRSRCTL9           = 0x1a4,
            MCASP_XRSRCTL10          = 0x1a8,
            MCASP_XRSRCTL11          = 0x1ac,
            MCASP_XRSRCTL12          = 0x1b0,
            MCASP_XRSRCTL13          = 0x1b4,
            MCASP_XRSRCTL14          = 0x1b8,
            MCASP_XRSRCTL15          = 0x1bc,
            MCASP_TXBUF0             = 0x200,
            MCASP_TXBUF1             = 0x204,
            MCASP_TXBUF2             = 0x208,
            MCASP_TXBUF3             = 0x20c,
            MCASP_TXBUF4             = 0x210,
            MCASP_TXBUF5             = 0x214,
            MCASP_TXBUF6             = 0x218,
            MCASP_TXBUF7             = 0x21c,
            MCASP_TXBUF8             = 0x220,
            MCASP_TXBUF9             = 0x224,
            MCASP_TXBUF10            = 0x228,
            MCASP_TXBUF11            = 0x22c,
            MCASP_TXBUF12            = 0x230,
            MCASP_TXBUF13            = 0x234,
            MCASP_TXBUF14            = 0x238,
            MCASP_TXBUF15            = 0x23c,
            MCASP_RXBUF0             = 0x280,
            MCASP_RXBUF1             = 0x284,
            MCASP_RXBUF2             = 0x288,
            MCASP_RXBUF3             = 0x28c,
            MCASP_RXBUF4             = 0x290,
            MCASP_RXBUF5             = 0x294,
            MCASP_RXBUF6             = 0x298,
            MCASP_RXBUF7             = 0x29c,
            MCASP_RXBUF8             = 0x2a0,
            MCASP_RXBUF9             = 0x2a4,
            MCASP_RXBUF10            = 0x2a8,
            MCASP_RXBUF11            = 0x2ac,
            MCASP_RXBUF12            = 0x2b0,
            MCASP_RXBUF13            = 0x2b4,
            MCASP_RXBUF14            = 0x2b8,
            MCASP_RXBUF15            = 0x2bc,
            MCASP_WFIFOCTL           = 0x1000,
            MCASP_RFIFOCTL           = 0x1008,
            MCASP_WFIFOSTS           = 0x1004,
            MCASP_RFIFOSTS           = 0x100c
                                  
//MCASP_SRCTL(x)                           MCASP_XRSRCTL0  (MCASP_REG_OFFSET * (x)))
//MCASP_TXBUF(x)                           MCASP_TXBUF0 + (MCASP_REG_OFFSET* (x)))
//MCASP_RXBUF(x)                           MCASP_RXBUF0 + (MCASP_REG_OFFSET* (x)))
//MCASP_DITCSRA(x)                         MCASP_DITCSRA0 + (MCASP_REG_OFFSET* (x)))
//MCASP_DITCSRB(x)                         MCASP_DITCSRB0 + (MCASP_REG_OFFSET* (x)))
//MCASP_DITUDRA(x)                         MCASP_DITUDRA0 + (MCASP_REG_OFFSET* (x)))
//MCASP_DITUDRB(x)                         MCASP_DITUDRB0 + (MCASP_REG_OFFSET* (x)))


        }

    }
}