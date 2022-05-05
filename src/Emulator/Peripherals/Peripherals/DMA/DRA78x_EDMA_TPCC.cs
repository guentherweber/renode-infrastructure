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


namespace Antmicro.Renode.Peripherals.DMA
{
    [AllowedTranslations( AllowedTranslation.WordToDoubleWord | AllowedTranslation.ByteToDoubleWord)]
    public class DRA78x_EDMA_TPCC : IKnownSize, IDoubleWordPeripheral, IWordPeripheral, IBytePeripheral, IGPIOReceiver
    {

        private readonly DoubleWordRegisterCollection dwordregisters;

        public long Size => 0x100000;

        public GPIO IRQ_Region0{ get; private set; }
        public GPIO IRQ_Region1 { get; private set; }
        public GPIO IRQ_Region2 { get; private set; }
        public GPIO IRQ_Region3 { get; private set; }
        public GPIO IRQ_Region4 { get; private set; }
        public GPIO IRQ_Region5 { get; private set; }
        public GPIO IRQ_Region6 { get; private set; }
        public GPIO IRQ_Region7 { get; private set; }


        public DRA78x_EDMA_TPCC( Machine machine)
        {
            dwordregisters = new DoubleWordRegisterCollection(this);

            IrqPerRegion = new GPIO[8];
            for (int i = 0; i < 8; i++)
            {
               IrqPerRegion[i] = new GPIO();
            }
            IRQ_Region0 = IrqPerRegion[0];
            IRQ_Region1 = IrqPerRegion[1];
            IRQ_Region2 = IrqPerRegion[2];
            IRQ_Region3 = IrqPerRegion[3];
            IRQ_Region4 = IrqPerRegion[4];
            IRQ_Region5 = IrqPerRegion[5];
            IRQ_Region6 = IrqPerRegion[6];
            IRQ_Region7 = IrqPerRegion[7];

            this.machine = machine;
            engine = new DmaEngine(machine);

            DefineRegisters();
            Reset();

        }

        




        public  void Reset()
        {
            dwordregisters.Reset();
        }

        private void CheckQDMA(int ParaIdx, int FieldIdx)
        {
            for (int region=0; region<8; region++)
            {
                if (ParaIdx == ParaPerRegion[region].Value)
                {
                    if (FieldIdx == TriggerWordRegion[region].Value)
                    IntActiveLow8[region].Value = IntEnableLow8[region];
                    IntActiveHigh8[region].Value = IntEnableHigh8[region];
                    if ((IntActiveHigh8[region].Value != 0) || (IntActiveLow8[region].Value != 0))
                    {
                        this.Log(LogLevel.Debug, "Interrupt Region No{4} : DMA Copy Para Index {0} : Src 0x{1:X}, Dst 0x{2:X}, Size 0x{3:X}", ParaIdx, SourceAdr[ParaIdx].Value, DestinationAdr[ParaIdx].Value, (int)ACount[ParaIdx].Value, region);
                        var request = new Request(SourceAdr[ParaIdx].Value, DestinationAdr[ParaIdx].Value, (int)ACount[ParaIdx].Value, TransferType.Byte, TransferType.Byte);
                        engine.IssueCopy(request);
                        IrqPerRegion[region].Set(true);
                    }
                }
            }
        }

        private void ClearInterrupts(int idx)
        {
            for (int region = 0; region < 8; region++)
            {
                if (idx == ParaPerRegion[region].Value)
                {
                    //                    IntActiveLow8[idx].Value = IntEnableLow8[idx];
                    //                    IntActiveHigh8[idx].Value = IntEnableHigh8[idx];
                    if ((IntActiveHigh8[region].Value == 0) && (IntActiveLow8[region].Value == 0))
                    {
                        IrqPerRegion[region].Set(false);
                        this.Log(LogLevel.Noisy, "Interrupt off Region {0}", region);
                    }
                }
            }
        }

        public void StartDmaCopy(int ParaIdx, int FieldIdx)
        {
            CheckQDMA(ParaIdx, FieldIdx);
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
            return 0x00;
//            return (byte)ReadDoubleWord(offset);
        }

        public void WriteByte(long offset, byte value)
        {
//            if (offset >= 0x4000 )
//            WriteDoubleWord(offset, value);
        }

        public uint ReadDoubleWord(long offset)
        {
            return dwordregisters.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            dwordregisters.Write(offset, value);
        }

        private IValueRegisterField IntEnableLow;
        private IValueRegisterField IntEnableHigh;
        private IValueRegisterField IntActiveLow;
        private IValueRegisterField IntActiveHigh;
        private readonly DmaEngine engine;
        private readonly Machine machine;

        private uint[] IntEnableLow8 = new uint[8];
        private uint[] IntEnableHigh8 = new uint[8];
        private uint[] IntEnableSetLow8 = new uint[8];
        private uint[] IntEnableSetHigh8 = new uint[8];
        private uint[] IntEnableClearLow8 = new uint[8];
        private uint[] IntEnableClearHigh8 = new uint[8];

        private IValueRegisterField[] IntActiveLow8 = new IValueRegisterField[8];
        private IValueRegisterField[] IntActiveHigh8 = new IValueRegisterField[8];
        private IValueRegisterField[] ParaPerRegion = new IValueRegisterField[8];
        private IValueRegisterField[] TriggerWordRegion = new IValueRegisterField[8];
        private GPIO[] IrqPerRegion = new GPIO[8];

        private IValueRegisterField[] ParaPerChannel = new IValueRegisterField[64];
        private IValueRegisterField[] TriggerWordChannel = new IValueRegisterField[64];

        private IValueRegisterField[] SourceAdr = new IValueRegisterField[512];
        private IValueRegisterField[] DestinationAdr = new IValueRegisterField[512];
        private IValueRegisterField[] ACount = new IValueRegisterField[512];
        private IValueRegisterField[] BCount = new IValueRegisterField[512];


        private void DefineRegisters()
        {
            Registers.EDMA_TPCC_PID.Define(dwordregisters, 0x00, "EDMA_TPCC_PID")
                .WithValueField(0, 32,  FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_PID");
            Registers.EDMA_TPCC_CCCFG.Define(dwordregisters, 0x01315045, "EDMA_TPCC_CCCFG")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_CCCFG");
            Registers.EDMA_TPCC_CLKGDIS.Define(dwordregisters, 0x00, "EDMA_TPCC_CLKGDIS")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_CLKGDIS");
            Registers.EDMA_TPCC_QDMAQNUM.Define(dwordregisters, 0x00, "EDMA_TPCC_QDMAQNUM")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_QDMAQNUM");
            Registers.EDMA_TPCC_QUETCMAP.Define(dwordregisters, 0x00, "EDMA_TPCC_QUETCMAP")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_QUETCMAP");
            Registers.EDMA_TPCC_QUEPRI.Define(dwordregisters, 0x00, "EDMA_TPCC_QUEPRI")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_QUEPRI");
            Registers.EDMA_TPCC_EMR.Define(dwordregisters, 0x00, "EDMA_TPCC_EMR")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_EMR");
            Registers.EDMA_TPCC_EMRH.Define(dwordregisters, 0x00, "EDMA_TPCC_EMRH")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_EMRH");
            Registers.EDMA_TPCC_EMCR.Define(dwordregisters, 0x00, "EDMA_TPCC_EMCR")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_EMCR");
            Registers.EDMA_TPCC_EMCRH.Define(dwordregisters, 0x00, "EDMA_TPCC_EMCRH")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_EMCRH");
            Registers.EDMA_TPCC_QEMR.Define(dwordregisters, 0x00, "EDMA_TPCC_QEMR")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_QEMR");
            Registers.EDMA_TPCC_QEMCR.Define(dwordregisters, 0x00, "EDMA_TPCC_QEMCR")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_QEMCR");
            Registers.EDMA_TPCC_CCERR.Define(dwordregisters, 0x00, "EDMA_TPCC_CCERR")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_CCERR");
            Registers.EDMA_TPCC_CCERRCLR.Define(dwordregisters, 0x00, "EDMA_TPCC_CCERRCLR")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_CCERRCLR");
            Registers.EDMA_TPCC_EEVAL.Define(dwordregisters, 0x00, "EDMA_TPCC_EEVAL")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_EEVAL");

            Registers.EDMA_TPCC_QWMTHRA.Define(dwordregisters, 0x00, "EDMA_TPCC_QWMTHRA")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_QWMTHRA");
            Registers.EDMA_TPCC_QWMTHRB.Define(dwordregisters, 0x00, "EDMA_TPCC_QWMTHRB")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_QWMTHRB");
            Registers.EDMA_TPCC_CCSTAT.Define(dwordregisters, 0x00, "EDMA_TPCC_CCSTAT")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_CCSTAT");
            Registers.EDMA_TPCC_AETCTL.Define(dwordregisters, 0x00, "EDMA_TPCC_AETCTL")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_AETCTL");
            Registers.EDMA_TPCC_AETSTAT.Define(dwordregisters, 0x00, "EDMA_TPCC_AETSTAT")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_AETCMD");
            Registers.EDMA_TPCC_AETCMD.Define(dwordregisters, 0x00, "EDMA_TPCC_AETCMD")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_EEVAL");
            Registers.EDMA_TPCC_MPFAR.Define(dwordregisters, 0x00, "EDMA_TPCC_MPFARL")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_MPFAR");
            Registers.EDMA_TPCC_MPFSR.Define(dwordregisters, 0x00, "EDMA_TPCC_MPFSR")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_MPFSR");
            Registers.EDMA_TPCC_MPFCR.Define(dwordregisters, 0x00, "EDMA_TPCC_MPFCR")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_MPFCR");
            Registers.EDMA_TPCC_MPPAG.Define(dwordregisters, 0x00, "EDMA_TPCC_MPPAG")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_MPPAG");

            Registers.EDMA_TPCC_ER.Define(dwordregisters, 0x00, "EDMA_TPCC_ER")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_ER");
            Registers.EDMA_TPCC_ERH.Define(dwordregisters, 0x00, "EDMA_TPCC_ERH")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_ERH");
            Registers.EDMA_TPCC_ECR.Define(dwordregisters, 0x00, "EDMA_TPCC_ECR")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_ECR");
            Registers.EDMA_TPCC_ECRH.Define(dwordregisters, 0x00, "EDMA_TPCC_ECRH")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_ECRH");
            Registers.EDMA_TPCC_ESR.Define(dwordregisters, 0x00, "EDMA_TPCC_ESR")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_ESR");
            Registers.EDMA_TPCC_ESRH.Define(dwordregisters, 0x00, "EDMA_TPCC_ESRH")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_ESRH");
            Registers.EDMA_TPCC_CER.Define(dwordregisters, 0x00, "EDMA_TPCC_CER")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_CER");
            Registers.EDMA_TPCC_CERH.Define(dwordregisters, 0x00, "EDMA_TPCC_CERH")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_CERH");
            Registers.EDMA_TPCC_EER.Define(dwordregisters, 0x00, "EDMA_TPCC_EER")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_");
            Registers.EDMA_TPCC_EERH.Define(dwordregisters, 0x00, "EDMA_TPCC_EERH")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_EERH");


           
            Registers.EDMA_TPCC_EECR.Define(dwordregisters, 0x00, "EDMA_TPCC_EECR")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_EECR");
            Registers.EDMA_TPCC_EECRH.Define(dwordregisters, 0x00, "EDMA_TPCC_EECRH")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_EECRH");
            Registers.EDMA_TPCC_EESR.Define(dwordregisters, 0x00, "EDMA_TPCC_")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_EESR");
            Registers.EDMA_TPCC_EESRH.Define(dwordregisters, 0x00, "EDMA_TPCC_EESRH")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_EESRH");
            Registers.EDMA_TPCC_SER.Define(dwordregisters, 0x00, "EDMA_TPCC_SER")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_SER");
            Registers.EDMA_TPCC_SERH.Define(dwordregisters, 0x00, "EDMA_TPCC_SERH")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_SERH");
            Registers.EDMA_TPCC_SECR.Define(dwordregisters, 0x00, "EDMA_TPCC_SECR")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_SECR");
            Registers.EDMA_TPCC_SECRH.Define(dwordregisters, 0x00, "EDMA_TPCC_SECRH")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_SECRH");

            Registers.EDMA_TPCC_IER.Define(dwordregisters, 0x00, "EDMA_TPCC_IER")
                    .WithValueField(0, 32, out IntEnableLow,  FieldMode.Read | FieldMode.Write,  name: "EDMA_TPCC_IER");
            Registers.EDMA_TPCC_IERH.Define(dwordregisters, 0x00, "EDMA_TPCC_IERH")
                    .WithValueField(0, 32, out IntEnableHigh, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_IERH");

            Registers.EDMA_TPCC_IECR.Define(dwordregisters, 0x00, "EDMA_TPCC_IECR")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write,
                        writeCallback: (_, value) =>
                        {
                            IntEnableLow.Value &= ~value; 
                        }, name: "EDMA_TPCC_IECR");
            Registers.EDMA_TPCC_IECRH.Define(dwordregisters, 0x00, "EDMA_TPCC_IECRH")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write,
                        writeCallback: (_, value) =>
                        {
                            IntEnableHigh.Value &= ~value;
                        }, name: "EDMA_TPCC_IECRH");

            Registers.EDMA_TPCC_IESR.Define(dwordregisters, 0x00, "EDMA_TPCC_IESR")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write,
                         writeCallback: (_, value) =>
                         {
                             IntEnableLow.Value |= value;
                         }, name: "EDMA_TPCC_IESR");
            Registers.EDMA_TPCC_IESRH.Define(dwordregisters, 0x00, "EDMA_TPCC_IESRH")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write,
                        writeCallback: (_, value) =>
                        {
                            IntEnableHigh.Value |= value;
                        }, name: "EDMA_TPCC_IESRH");

            Registers.EDMA_TPCC_IPR.Define(dwordregisters, 0x00, "EDMA_TPCC_IPR")
                    .WithValueField(0, 32, out IntActiveHigh, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_IPR");
            Registers.EDMA_TPCC_IPRH.Define(dwordregisters, 0x00, "EDMA_TPCC_IPRH")
                    .WithValueField(0, 32, out IntActiveLow, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_IPRH");

            Registers.EDMA_TPCC_ICR.Define(dwordregisters, 0x00, "EDMA_TPCC_ICR")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write,
                        writeCallback: (_, value) =>
                        {
                            IntActiveLow.Value &= ~value;
                        }, name: "EDMA_TPCC_ICR");
            Registers.EDMA_TPCC_ICRH.Define(dwordregisters, 0x00, "EDMA_TPCC_ICRH")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write,
                        writeCallback: (_, value) =>
                        {
                            IntActiveLow.Value &= ~value;
                        }, name: "EDMA_TPCC_ICRH");



            Registers.EDMA_TPCC_IEVAL.Define(dwordregisters, 0x00, "EDMA_TPCC_")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_IEVAL");


            Registers.EDMA_TPCC_QEER.Define(dwordregisters, 0x00, "EDMA_TPCC_QEER")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_QEER");
            Registers.EDMA_TPCC_QEECR.Define(dwordregisters, 0x00, "EDMA_TPCC_QEECR")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_QEECR");
            Registers.EDMA_TPCC_QEESR.Define(dwordregisters, 0x00, "EDMA_TPCC_QEESR")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_QEESR");
            Registers.EDMA_TPCC_QSER.Define(dwordregisters, 0x00, "EDMA_TPCC_QSER")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_QSER");
            Registers.EDMA_TPCC_QSECR.Define(dwordregisters, 0x00, "EDMA_TPCC_QSECR")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_QSECR");
            Registers.EDMA_TPCC_QER.Define(dwordregisters, 0x00, "EDMA_TPCC_QER")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_");
            
            Registers.EDMA_TPCC_QSTATN_0.Define(dwordregisters, 0x00, "EDMA_TPCC_QSTATN_0")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_QSTATN_0");
            Registers.EDMA_TPCC_QSTATN_1.Define(dwordregisters, 0x00, "EDMA_TPCC_QSTATN_1")
                    .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_QSTATN_1");

            Registers.EDMA_TPCC_IER_RN.DefineMany(dwordregisters, 8, (register, idx) =>
            {
                register
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        return IntEnableLow8[idx];
                    }, name: $"EDMA_TPCC_IER_RN_{idx}");
            }, stepInBytes: 0x200, resetValue: 0x00, name: "EDMA_TPCC_IER_RN");

            Registers.EDMA_TPCC_IERH_RN.DefineMany(dwordregisters, 8, (register, idx) =>
            {
                register
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        return IntEnableHigh8[idx];
                    }, name: $"EDMA_TPCC_IERH_RN_{idx}");
            }, stepInBytes: 0x200, resetValue: 0x00, name: "EDMA_TPCC_IERH_RN");

            Registers.EDMA_TPCC_IECR_RN.DefineMany(dwordregisters, 8, (register, idx) =>
            {
                register
                    .WithValueField(0, 32, FieldMode.Read, writeCallback: (_, value) =>
                        {
                            IntEnableLow8[idx] &= ~value;
                        }, name: $"EDMA_TPCC_IECR_RN_{idx}");
            }, stepInBytes: 0x200, resetValue: 0x00, name: "EDMA_TPCC_IECR_RN");

            Registers.EDMA_TPCC_IECRH_RN.DefineMany(dwordregisters, 8, (register, idx) =>
            {
                register
                    .WithValueField(0, 32, FieldMode.Read, writeCallback: (_, value) =>
                    {
                        IntEnableHigh8[idx] &= ~value;
                    }, name: $"EDMA_TPCC_IECRH_RN_{idx}");
            }, stepInBytes: 0x200, resetValue: 0x00, name: "EDMA_TPCC_IECRH_RN");

            Registers.EDMA_TPCC_IESR_RN.DefineMany(dwordregisters, 8, (register, idx) =>
            {
                register
                    .WithValueField(0, 32, FieldMode.Read, writeCallback: (_, value) =>
                    {
                        IntEnableLow8[idx] |= value;
                    }, name: $"EDMA_TPCC_IESR_RN_{idx}");
            }, stepInBytes: 0x200, resetValue: 0x00, name: "EDMA_TPCC_IESR_RN");

            Registers.EDMA_TPCC_IESRH_RN.DefineMany(dwordregisters, 8, (register, idx) =>
            {
                register
                    .WithValueField(0, 32, FieldMode.Read, writeCallback: (_, value) =>
                    {
                        IntEnableHigh8[idx] |= value;
                    }, name: $"EDMA_TPCC_IESRH_RN_{idx}");
            }, stepInBytes: 0x200, resetValue: 0x00, name: "EDMA_TPCC_IESRH_RN");


            Registers.EDMA_TPCC_IPR_RN.DefineMany(dwordregisters, 8, (register, idx) =>
            {
                register
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        return IntActiveLow8[idx].Value;
                    }, name: $"EDMA_TPCC_IPR_RN_{idx}");
            }, stepInBytes: 0x200, resetValue: 0x00, name: "EDMA_TPCC_IPR_RN");

            Registers.EDMA_TPCC_IPRH_RN.DefineMany(dwordregisters, 8, (register, idx) =>
            {
                register
                    .WithValueField(0, 32, out IntActiveHigh8[idx], FieldMode.Read, valueProviderCallback: _ =>
                    {
                        return IntActiveHigh8[idx].Value;
                    }, name: $"EDMA_TPCC_IPRH_RN_{idx}");
            }, stepInBytes: 0x200, resetValue: 0x00, name: "EDMA_TPCC_IPRH_RN");

            Registers.EDMA_TPCC_ICR_RN.DefineMany(dwordregisters, 8, (register, idx) =>
            {
                register
                    .WithValueField(0, 32, out IntActiveLow8[idx], FieldMode.Read, writeCallback: (_, value) =>
                    {
                        IntActiveLow8[idx].Value &= ~value;
                        ClearInterrupts(idx);
                    }, name: $"EDMA_TPCC_IPR_RN_{idx}");
            }, stepInBytes: 0x200, resetValue: 0x00, name: "EDMA_TPCC_ICR_RN");

            Registers.EDMA_TPCC_ICRH_RN.DefineMany(dwordregisters, 8, (register, idx) =>
            {
                register
                    .WithValueField(0, 32, out IntActiveHigh8[idx], FieldMode.Read, writeCallback: (_, value) =>
                    {
                        IntActiveHigh8[idx].Value &= ~value;
                        ClearInterrupts(idx);
                    }, name: $"EDMA_TPCC_ICRH_RN_{idx}");
            }, stepInBytes: 0x200, resetValue: 0x00, name: "EDMA_TPCC_ICRH_RN");

            Registers.EDMA_TPCC_QCHMAPN.DefineMany(dwordregisters, 8, (register, idx) =>
            {
                register
                    .WithValueField(0, 2, FieldMode.Read | FieldMode.Write, name: $"EDMA_TPCC_QCHMAPN_reserved")
                    .WithValueField(2, 3, out TriggerWordRegion[idx], FieldMode.Read | FieldMode.Write, name: $"EDMA_TPCC_QCHMAPN_TRWORD_")
                    .WithValueField(5, 9, out ParaPerRegion[idx], FieldMode.Read | FieldMode.Write,  name: $"EDMA_TPCC_QCHMAPN_PAENTRY_")
                    .WithValueField(14, 18, FieldMode.Read | FieldMode.Write, name: $"EDMA_TPCC_QCHMAPN_reserved");
            }, stepInBytes: 4, resetValue: 0x00, name: "EDMA_TPCC_QCHMAPN");

            Registers.EDMA_TPCC_DCHMAPN.DefineMany(dwordregisters, 64, (register, idx) =>
            {
                register
                    .WithValueField(0, 2, FieldMode.Read | FieldMode.Write, name: $"EDMA_TPCC_DCHMAPN_reserved")
                    .WithValueField(2, 3, out TriggerWordChannel[idx], FieldMode.Read | FieldMode.Write, name: $"EDMA_TPCC_DCHMAPN_TRWORD_")
                    .WithValueField(5, 9, out ParaPerChannel[idx], FieldMode.Read | FieldMode.Write, name: $"EDMA_TPCC_DCHMAPN_PAENTRY_")
                    .WithValueField(14, 18, FieldMode.Read | FieldMode.Write, name: $"EDMA_TPCC_DCHMAPN_reserved");
            }, stepInBytes: 4, resetValue: 0x00, name: "EDMA_TPCC_DCHMAPN");

            Registers.EDMA_TPCC_OPTC.DefineMany(dwordregisters, 512, (register, idx) =>
            {
                register
                    .WithValueField(0, 12, FieldMode.Read | FieldMode.Write, name: $"reserved")
                    .WithValueField(12, 6, FieldMode.Read | FieldMode.Write, name: $"EDMA_TPCC_OPTC_TCC")
                    .WithValueField(18, 14, FieldMode.Read | FieldMode.Write, name: $"reserved");
            }, stepInBytes: 0x20, resetValue: 0x00, name: "EDMA_TPCC_OPTC");



            Registers.EDMA_TPCC_SRC.DefineMany(dwordregisters, 512, (register, idx) =>
            {
                register
                    .WithValueField(0, 32, out SourceAdr[idx], FieldMode.Read | FieldMode.Write, name: $"EDMA_TPCC_SRC");
            }, stepInBytes: 0x20, resetValue: 0x00, name: "EDMA_TPCC_SRC");

            Registers.EDMA_TPCC_ABCNT.DefineMany(dwordregisters, 512, (register, idx) =>
            {
                register
                    .WithValueField(0, 16, out ACount[idx], FieldMode.Read | FieldMode.Write, name: $"EDMA_TPCC_ACNT")
                    .WithValueField(16, 16, out BCount[idx], FieldMode.Read | FieldMode.Write, name: $"EDMA_TPCC_BCNT");
            }, stepInBytes: 0x20, resetValue: 0x00, name: "EDMA_TPCC_ABCNT");

            Registers.EDMA_TPCC_DST.DefineMany(dwordregisters, 512, (register, idx) =>
            {
                register
                   .WithValueField(0, 32, out DestinationAdr[idx], FieldMode.Read | FieldMode.Write,
                        writeCallback: (_, value) =>
                        {
                            DestinationAdr[idx].Value = value;
                            StartDmaCopy(idx, 3);
                        }, name: "EDMA_TPCC_DST_");
            }, stepInBytes: 0x20, resetValue: 0x00, name: "EDMA_TPCC_DST");

            Registers.EDMA_TPCC_LNK.DefineMany(dwordregisters, 512, (register, idx) =>
            {
                register
                   .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_LNK");
            }, stepInBytes: 0x20, resetValue: 0x00, name: "EDMA_TPCC_LNK");

            Registers.EDMA_TPCC_IEVAL_RN.DefineMany(dwordregisters, 8, (register, idx) =>
            {
                register
                   .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_IEVAL_RN");
            }, stepInBytes: 0x200, resetValue: 0x00, name: "EDMA_TPCC_IEVAL_RN");

            for (int i = 0; i < 8; i++)
            {
                var reg = new DoubleWordRegister(this, 0x00);
                reg.WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_DRAEM_" + i);
                dwordregisters.AddRegister(0x340 + i * 8, reg);

                reg = new DoubleWordRegister(this, 0x00);
                reg.WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_DRAEHM_" + i);
                dwordregisters.AddRegister(0x344 + i * 8, reg);

                reg = new DoubleWordRegister(this, 0x00);
                reg.WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_QRAEN_" + i);
                dwordregisters.AddRegister(0x380 + i * 4, reg);

                reg = new DoubleWordRegister(this, 0x00);
                reg.WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_MPPAN_" + i);
                dwordregisters.AddRegister(0x810 + i * 4, reg);


                reg = new DoubleWordRegister(this, 0x00);
                reg.WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_DMAQNUMN_" + i);
                dwordregisters.AddRegister(0x240 + i * 4, reg);

                reg = new DoubleWordRegister(this, 0x00);
                reg.WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_ER_RN_" + i);
                dwordregisters.AddRegister(0x2000 + i * 0x200, reg);

                reg = new DoubleWordRegister(this, 0x00);
                reg.WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_ECR_RN_" + i);
                dwordregisters.AddRegister(0x2004 + i * 0x200, reg);

                reg = new DoubleWordRegister(this, 0x00);
                reg.WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_ECRH_RN_" + i);
                dwordregisters.AddRegister(0x2008 + i * 0x200, reg);

                reg = new DoubleWordRegister(this, 0x00);
                reg.WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_ESR_RN_" + i);
                dwordregisters.AddRegister(0x200C + i * 0x200, reg);

                reg = new DoubleWordRegister(this, 0x00);
                reg.WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_ESRH_RN_" + i);
                dwordregisters.AddRegister(0x2010 + i * 0x200, reg);

                reg = new DoubleWordRegister(this, 0x00);
                reg.WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_CER_RN_" + i);
                dwordregisters.AddRegister(0x2014 + i * 0x200, reg);

                reg = new DoubleWordRegister(this, 0x00);
                reg.WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_CERH_RN_" + i);
                dwordregisters.AddRegister(0x2018 + i * 0x200, reg);

                reg = new DoubleWordRegister(this, 0x00);
                reg.WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_EER_RN_" + i);
                dwordregisters.AddRegister(0x201C + i * 0x200, reg);



                reg = new DoubleWordRegister(this, 0x00);
                reg.WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_EER_RN_" + i);
                dwordregisters.AddRegister(0x2020 + i * 0x200, reg);

                reg = new DoubleWordRegister(this, 0x00);
                reg.WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_EERH_RN_" + i);
                dwordregisters.AddRegister(0x2024 + i * 0x200, reg);

                reg = new DoubleWordRegister(this, 0x00);
                reg.WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_EECR_RN_" + i);
                dwordregisters.AddRegister(0x2028 + i * 0x200, reg);

                reg = new DoubleWordRegister(this, 0x00);
                reg.WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_EECRH_RN_" + i);
                dwordregisters.AddRegister(0x202C + i * 0x200, reg);


                reg = new DoubleWordRegister(this, 0x00);
                reg.WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_EESR_RN_" + i);
                dwordregisters.AddRegister(0x2030 + i * 0x200, reg);

                reg = new DoubleWordRegister(this, 0x00);
                reg.WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_EESRH_RN_" + i);
                dwordregisters.AddRegister(0x2034 + i * 0x200, reg);

                reg = new DoubleWordRegister(this, 0x00);
                reg.WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_SER_RN_" + i);
                dwordregisters.AddRegister(0x2038 + i * 0x200, reg);

                reg = new DoubleWordRegister(this, 0x00);
                reg.WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_SERH_RN_" + i);
                dwordregisters.AddRegister(0x203C + i * 0x200, reg);


                reg = new DoubleWordRegister(this, 0x00);
                reg.WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_SECR_RN_" + i);
                dwordregisters.AddRegister(0x2040 + i * 0x200, reg);

                reg = new DoubleWordRegister(this, 0x00);
                reg.WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_SECRH_RN_" + i);
                dwordregisters.AddRegister(0x2044 + i * 0x200, reg);



                reg = new DoubleWordRegister(this, 0x00);
                reg.WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_QER_RN_" + i);
                dwordregisters.AddRegister(0x207C + i * 0x200, reg);

                reg = new DoubleWordRegister(this, 0x00);
                reg.WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_QER_RN_" + i);
                dwordregisters.AddRegister(0x2080 + i * 0x200, reg);

                reg = new DoubleWordRegister(this, 0x00);
                reg.WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_QEER_RN_" + i);
                dwordregisters.AddRegister(0x2084 + i * 0x200, reg);

                reg = new DoubleWordRegister(this, 0x00);
                reg.WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_QEECR_RN_" + i);
                dwordregisters.AddRegister(0x2088 + i * 0x200, reg);

                reg = new DoubleWordRegister(this, 0x00);
                reg.WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_QEESR_RN_" + i);
                dwordregisters.AddRegister(0x208C + i * 0x200, reg);

                reg = new DoubleWordRegister(this, 0x00);
                reg.WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_QSER_RN_" + i);
                dwordregisters.AddRegister(0x2090 + i * 0x200, reg);

                reg = new DoubleWordRegister(this, 0x00);
                reg.WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_QSECR_RN_" + i);
                dwordregisters.AddRegister(0x2094 + i * 0x200, reg);

            }

            for (int i = 0; i < 16; i++)
            {
                var reg = new DoubleWordRegister(this, 0x00);
                reg.WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_Q0E_" + i);
                dwordregisters.AddRegister(0x400 + i * 4, reg);

                reg = new DoubleWordRegister(this, 0x00);
                reg.WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "EDMA_TPCC_Q1E_" + i);
                dwordregisters.AddRegister(0x440 + i * 4, reg);

            }



        }

        public void OnGPIO(int number, bool value)
        {
            this.Log(LogLevel.Noisy, "OnGPIO {0} {1}", number, value);
//            throw new NotImplementedException();
        }

        private enum Registers : long
        {
            TEST = 3000,
            EDMA_TPCC_PID                    = 0x0,
            EDMA_TPCC_CCCFG                  = 0x4,
            EDMA_TPCC_CLKGDIS                = 0xfc,
            EDMA_TPCC_DCHMAPN                = 0x100,
            EDMA_TPCC_QCHMAPN                = 0x200,
            EDMA_TPCC_QDMAQNUM               = 0x260,
            EDMA_TPCC_QUETCMAP               = 0x280,
            EDMA_TPCC_QUEPRI                 = 0x284,
            EDMA_TPCC_EMR                    = 0x300,
            EDMA_TPCC_EMRH                   = 0x304,
            EDMA_TPCC_EMCR                   = 0x308,
            EDMA_TPCC_EMCRH                  = 0x30c,
            EDMA_TPCC_QEMR                   = 0x310,
            EDMA_TPCC_QEMCR                  = 0x314,
            EDMA_TPCC_CCERR                  = 0x318,
            EDMA_TPCC_CCERRCLR               = 0x31c,
            EDMA_TPCC_EEVAL                  = 0x320,



            EDMA_TPCC_QSTATN_0 = 0x600 + (0) * 4,
            EDMA_TPCC_QSTATN_1 = 0x600 + (1) * 4,

            EDMA_TPCC_QWMTHRA                = 0x620,
            EDMA_TPCC_QWMTHRB                = 0x624,
            EDMA_TPCC_CCSTAT                 = 0x640,
            EDMA_TPCC_AETCTL                 = 0x700,
            EDMA_TPCC_AETSTAT                = 0x704,
            EDMA_TPCC_AETCMD                 = 0x708,
            EDMA_TPCC_MPFAR                  = 0x800,
            EDMA_TPCC_MPFSR                  = 0x804,
            EDMA_TPCC_MPFCR                  = 0x808,
            EDMA_TPCC_MPPAG                  = 0x80c,



            EDMA_TPCC_ER                     = 0x1000,
            EDMA_TPCC_ERH                    = 0x1004,
            EDMA_TPCC_ECR                    = 0x1008,
            EDMA_TPCC_ECRH                   = 0x100c,
            EDMA_TPCC_ESR                    = 0x1010,
            EDMA_TPCC_ESRH                   = 0x1014,
            EDMA_TPCC_CER                    = 0x1018,
            EDMA_TPCC_CERH                   = 0x101c,
            EDMA_TPCC_EER                    = 0x1020,
            EDMA_TPCC_EERH                   = 0x1024,
            EDMA_TPCC_EECR                   = 0x1028,
            EDMA_TPCC_EECRH                  = 0x102c,
            EDMA_TPCC_EESR                   = 0x1030,
            EDMA_TPCC_EESRH                  = 0x1034,
            EDMA_TPCC_SER                    = 0x1038,
            EDMA_TPCC_SERH                   = 0x103c,
            EDMA_TPCC_SECR                   = 0x1040,
            EDMA_TPCC_SECRH                  = 0x1044,
            EDMA_TPCC_IER                    = 0x1050,
            EDMA_TPCC_IERH                   = 0x1054,
            EDMA_TPCC_IECR                   = 0x1058,
            EDMA_TPCC_IECRH                  = 0x105c,
            EDMA_TPCC_IESR                   = 0x1060,
            EDMA_TPCC_IESRH                  = 0x1064,
            EDMA_TPCC_IPR                    = 0x1068,
            EDMA_TPCC_IPRH                   = 0x106c,
            EDMA_TPCC_ICR                    = 0x1070,
            EDMA_TPCC_ICRH                   = 0x1074,
            EDMA_TPCC_IEVAL                  = 0x1078,
            EDMA_TPCC_QER                    = 0x1080,
            EDMA_TPCC_QEER                   = 0x1084,
            EDMA_TPCC_QEECR                  = 0x1088,
            EDMA_TPCC_QEESR                  = 0x108c,
            EDMA_TPCC_QSER                   = 0x1090,
            EDMA_TPCC_QSECR                  = 0x1094,

            EDMA_TPCC_IER_RN                 = 0x2050,
            EDMA_TPCC_IERH_RN                = 0x2054,
            EDMA_TPCC_IECR_RN               = 0x2058,
            EDMA_TPCC_IECRH_RN              = 0x205C,
            EDMA_TPCC_IESR_RN               = 0x2060,
            EDMA_TPCC_IESRH_RN              = 0x2064,
            EDMA_TPCC_IPR_RN                = 0x2068,
            EDMA_TPCC_IPRH_RN               = 0x206C,
            EDMA_TPCC_ICR_RN                = 0x2070,
            EDMA_TPCC_ICRH_RN               = 0x2074,
            EDMA_TPCC_IEVAL_RN              = 0x2078,
            EDMA_TPCC_OPTC                  = 0x4000,
            EDMA_TPCC_SRC                   = 0x4004,
            EDMA_TPCC_ABCNT                 = 0x4008,
            EDMA_TPCC_DST                   = 0x400C,
            EDMA_TPCC_LNK                   = 0x4014,
            /*
                        EDMA_TC_PID = 0x0,
                        EDMA_TC_TCCFG       = 0x4,
                        EDMA_TC_SYSCONFIG   = 0x10,
                        EDMA_TC_TCSTAT      = 0x100,
                        EDMA_TC_INTSTAT     = 0x104,
                        EDMA_TC_INTEN       = 0x108,
                        EDMA_TC_INTCLR      = 0x10c,
                        EDMA_TC_INTCMD      = 0x110,
                        EDMA_TC_ERRSTAT     = 0x120,
                        EDMA_TC_ERREN       = 0x124,
                        EDMA_TC_ERRCLR      = 0x128,
                        EDMA_TC_ERRDET      = 0x12c,
                        EDMA_TC_ERRCMD      = 0x130,
                        EDMA_TC_RDRATE      = 0x140,

                        EDMA_TC_POPT        = 0x0,
                        EDMA_TC_PSRC        = 0x4,
                        EDMA_TC_PCNT        = 0x8,
                        EDMA_TC_PDST        = 0xc,
                        EDMA_TC_PBIDX       = 0x10,
                        EDMA_TC_PMPPRXY     = 0x14,
                        EDMA_TC_SAOPT       = 0x240,
                        EDMA_TC_SASRC       = 0x244,
                        EDMA_TC_SACNT       = 0x248,
                        EDMA_TC_SADST       = 0x24c,
                        EDMA_TC_SABIDX      = 0x250,
                        EDMA_TC_SAMPPRXY    = 0x254,
                        EDMA_TC_SACNTRLD    = 0x258,
                        EDMA_TC_SASRCBREF   = 0x25c,
                        EDMA_TC_SADSTBREF   = 0x260,
                        EDMA_TC_DFCNTRLD    = 0x280,
                        EDMA_TC_DFSRCBREF   = 0x284,
                        EDMA_TC_DFDSTBREF   = 0x288,
                        EDMA_TC_DFOPT_0     = 0x300 + (0 * 64),
                        EDMA_TC_DFOPT_1     = 0x300 + (1 * 64),
                        EDMA_TC_DFSRC_0     = 0x304 + (0 * 64),
                        EDMA_TC_DFSRC_1     = 0x304 + (1 * 64),
                        EDMA_TC_DFCNT_0     = 0x308 + (0 * 64),
                        EDMA_TC_DFCNT_1     = 0x308 + (1 * 64),
                        EDMA_TC_DFDST_0     = 0x30c + (0 * 64),
                        EDMA_TC_DFDST_1     = 0x30c + (1 * 64),
                        EDMA_TC_DFBIDX_0    = 0x310 + (0 * 64),
                        EDMA_TC_DFBIDX_1    = 0x310 + (1 * 64),
                        EDMA_TC_DFMPPRXY_0  = 0x314 + (0 * 64),
                        EDMA_TC_DFMPPRXY_1  = 0x314 + (1 * 64)
            */
        }

    }
}