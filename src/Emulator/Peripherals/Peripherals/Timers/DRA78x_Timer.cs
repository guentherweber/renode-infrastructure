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

namespace Antmicro.Renode.Peripherals.Timers
{
    [AllowedTranslations( AllowedTranslation.WordToDoubleWord | AllowedTranslation.ByteToDoubleWord)]
    public class DRA78x_Timer : IKnownSize, IDoubleWordPeripheral, IWordPeripheral, IBytePeripheral, IGPIOReceiver, IGPIOSender
    {

        private readonly DoubleWordRegisterCollection dwordregisters;

        public long Size => 112;
        private const long DefaultPeripheralFrequency = 212000000;
        public GPIO IRQ { get; private set; }
        public GPIO PORTIMERPWM { get; private set; }

        private Boolean MAT_Enabled = false;
        private Boolean OVF_Enabled = false;
        private Boolean TCAR_Enabled = false;
        private Boolean TCar1Captured = false;
        private Boolean TCar2Captured = false;

        private LimitTimer Timer;
        private IFlagRegisterField CompareModeEnabled;
        private IFlagRegisterField capture_mode;
        private IValueRegisterField trigger;
        private IFlagRegisterField TCAR_IT_Flag;
        private IFlagRegisterField MAT_IT_Flag;
        private IFlagRegisterField OVF_IT_Flag;
        private IFlagRegisterField TimerEnabled;
        private IFlagRegisterField AutoReload;
        private IValueRegisterField Prescaler;
        private IValueRegisterField TimerStartValue;
        private IValueRegisterField TimerCompareValue;
        private IValueRegisterField TransitionCaptureMode;
        private IValueRegisterField Tcar1;
        private IValueRegisterField Tcar2;

        public DRA78x_Timer( Machine machine, long frequency = DefaultPeripheralFrequency)
        {
            dwordregisters = new DoubleWordRegisterCollection(this);

            IRQ = new GPIO();
            PORTIMERPWM = new GPIO();
            Timer = new LimitTimer(machine.ClockSource, frequency, this, nameof(Timer), 0xFFFFFFFF, Direction.Ascending, false, WorkMode.Periodic, true, true, 1);
            Timer.LimitReached += LimitReached;
            Timer.Enabled = false;
            Timer.EventEnabled = true;


            DefineRegisters();
            Reset();

        }

        private void LimitReached()
        {
            if (capture_mode.Value)
            {
                if ((AutoReload.Value == true) && (Timer.Limit == 0xFFFFFFFF))
                {
                    Timer.Value = TimerStartValue.Value;
                }
                if ((OVF_Enabled) && (Timer.Limit == 0xFFFFFFFF))
                {
                    this.Log(LogLevel.Noisy, "OVF Interrupt occured");
                    OVF_IT_Flag.Value = true;
                    IRQ.Set(true);
                    IRQ.Set(false);
                }
            }

            if (CompareModeEnabled.Value)
            {
                Timer.Enabled = false;

                if (Timer.Limit == 0xFFFFFFFF)
                {
                    Timer.Limit = TimerStartValue.Value;
                    Timer.Value = 0x00;
                    Timer.Enabled = true;
                    PORTIMERPWM.Set();
                }
                else
                {
                    Timer.Limit = 0xFFFFFFFF;
                    Timer.Value = TimerStartValue.Value;
                    Timer.Enabled = true;
                    PORTIMERPWM.Unset();
                }
            }


        }

        public void OnGPIO(int number, bool value)
        {
            if (TCAR_Enabled == true)
            {
                if (value == true)   // positiv edge
                {
                    if ( (TransitionCaptureMode.Value == 0x01) || (TransitionCaptureMode.Value == 0x03) )
                    {
                        if (TCar1Captured == false)
                        {
                            Tcar1.Value = (uint)Timer.Value;
                            this.Log(LogLevel.Noisy, "Capture 1st edge, TCAR1 {0:X}", Tcar1.Value);
                            TCar1Captured = true;
                        }
                        else if (TCar2Captured == false)
                        {
                            Tcar2.Value = (uint)Timer.Value;
                            TCar2Captured = true;
                            this.Log(LogLevel.Noisy, "Capture 2nd edge and activate TCAR interrupt, TCAR2 {0:X}", Tcar2.Value);
                            TCAR_IT_Flag.Value = true;
                            IRQ.Set(true);
                            IRQ.Set(false);
                        }
                    }
                }
                else               // negativ edge
                {
                    if ( (TransitionCaptureMode.Value == 0x02) || (TransitionCaptureMode.Value == 0x03) )
                    {
                        if (TCar1Captured == false)
                        {
                            Tcar1.Value = (uint)Timer.Value;
                            this.Log(LogLevel.Noisy, "Capture 1st edge, TCAR1 {0:X}", Tcar1.Value);
                            TCar1Captured = true;
                        }
                        else if (TCar2Captured == false)
                        {
                            Tcar2.Value = (uint)Timer.Value;
                            TCar2Captured = true;
                            this.Log(LogLevel.Noisy, "Capture 2nd edge and activate TCAR interrupt, TCAR2 {0:X}", Tcar2.Value);
                            TCAR_IT_Flag.Value = true;
                            IRQ.Set(true);
                            IRQ.Set(false);
                        }
                    }

                }
            }
        }

        private void DefineRegisters()
        {
            Registers.TIMER_TIDR.Define(dwordregisters, 0x00, "TIMER_TIDR")
            .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "TIMER_TIDR");

            Registers.TIMER_TIOCP_CFG.Define(dwordregisters, 0x00, "TIMER_TIOCP_CFG")
            .WithFlag(0, FieldMode.Read | FieldMode.WriteOneToClear, name: "SOFTRESET")
            .WithFlag(1, FieldMode.Read | FieldMode.Write, name: "EMUFREE")
            .WithValueField(2, 2, FieldMode.Read | FieldMode.Write, name: "IDLE_MODE")
            .WithValueField(4, 28, FieldMode.Read | FieldMode.Write, name: "reserved");

            Registers.TIMER_IRQ_EOI.Define(dwordregisters, 0x00, "TIMER_IRQ_EOI")
            .WithFlag(0, FieldMode.Read | FieldMode.Write, name: "LINE_NUMBER")
            .WithValueField(1, 31, FieldMode.Read | FieldMode.Write, name: "reserved");

            Registers.TIMER_IRQSTATUS_RAW.Define(dwordregisters, 0x00, "TIMER_IRQSTATUS_RAW")
            .WithFlag(0, FieldMode.Read | FieldMode.WriteOneToClear, name: "MAT_IT_FLAG")
            .WithFlag(1, FieldMode.Read | FieldMode.WriteOneToClear, name: "OVF_IT_FLAG")
            .WithFlag(2, FieldMode.Read | FieldMode.WriteOneToClear, name: "TCAR_IT_FLAG")
            .WithValueField(3, 29, FieldMode.Read | FieldMode.Write, name: "reserved");

            Registers.TIMER_IRQSTATUS.Define(dwordregisters, 0x00, "TIMER_IRQSTATUS")
            .WithFlag(0, out MAT_IT_Flag, FieldMode.Read | FieldMode.WriteOneToClear, name: "MAT_IT_FLAG")
            .WithFlag(1, out OVF_IT_Flag, FieldMode.Read | FieldMode.WriteOneToClear, name: "OVF_IT_FLAG")
            .WithFlag(2, out TCAR_IT_Flag, FieldMode.Read | FieldMode.WriteOneToClear,
                                writeCallback: (_, value) =>
                                {
                                    if (value == true)
                                    {
                                        TCar1Captured = false;
                                        TCar2Captured = false;
                                        this.Log(LogLevel.Noisy, "TCAR_IT Flag status: cleared");
                                    }
                                    else
                                       this.Log(LogLevel.Noisy, "TCAR_IT Flag status: active");
                                }, name: "TCAR_IT_FLAG")
            .WithValueField(3, 29, FieldMode.Read | FieldMode.Write, name: "reserved");

            Registers.TIMER_IRQENABLE_SET.Define(dwordregisters, 0x00, "TIMER_IRQENABLE_SET")
            .WithFlag(0, FieldMode.Read | FieldMode.Write,
                                writeCallback: (_, value) =>
                                {
                                    if (value == true)
                                        MAT_Enabled = true;
                                    this.Log(LogLevel.Noisy, "MAT_Enabled: {0}", MAT_Enabled);
                                }, name: "MAT_EN_FLAG")
            .WithFlag(1, FieldMode.Read | FieldMode.Write,
                                writeCallback: (_, value) =>
                                {
                                    if (value == true)
                                        OVF_Enabled = true;
                                    this.Log(LogLevel.Noisy, "OVF_Enabled: {0}", OVF_Enabled);
                                }, name: "OVF_EN_FLAG")
            .WithFlag(2, FieldMode.Read | FieldMode.Write,
                                writeCallback: (_, value) =>
                                {
                                    if (value == true)
                                        TCAR_Enabled = true;
                                    this.Log(LogLevel.Noisy, "TCAR_Enabled: {0}", TCAR_Enabled);
                                }, name: "TCAR_EN_FLAG")
            .WithValueField(3, 29, FieldMode.Read | FieldMode.Write, name: "reserved");

            Registers.TIMER_IRQENABLE_CLR.Define(dwordregisters, 0x00, "TIMER_IRQENABLE_CLR")
            .WithFlag(0, FieldMode.Read | FieldMode.Write,
                                writeCallback: (_, value) =>
                                {
                                    if (value == true)
                                        MAT_Enabled = false;
                                    this.Log(LogLevel.Noisy, "MAT_Enabled: {0}", MAT_Enabled);
                                }, name: "MAT_EN_FLAG")
            .WithFlag(1, FieldMode.Read | FieldMode.Write,
                                writeCallback: (_, value) =>
                                {
                                    if (value == true)
                                        OVF_Enabled = false;
                                    this.Log(LogLevel.Noisy, "OVF_Enabled: {0}", OVF_Enabled);
                                }, name: "OVF_EN_FLAG")
            .WithFlag(2, FieldMode.Read | FieldMode.Write,
                                writeCallback: (_, value) =>
                                {
                                    if (value == true)
                                        TCAR_Enabled = false;
                                    this.Log(LogLevel.Noisy, "TCAR_Enabled: {0}", TCAR_Enabled);
                                }, name: "TCAR_EN_FLAG")
            .WithValueField(3, 29, FieldMode.Read | FieldMode.Write, name: "reserved");

            Registers.TIMER_IRQWAKEEN.Define(dwordregisters, 0x00, "TIMER_IRQWAKEEN")
            .WithFlag(0, FieldMode.Read | FieldMode.Write, name: "TCAR_WUP_ENA")
            .WithFlag(1, FieldMode.Read | FieldMode.Write, name: "OVF_WUP_ENA")
            .WithFlag(2, FieldMode.Read | FieldMode.Write, name: "TCAR_WUP_ENA")
            .WithValueField(3, 29, FieldMode.Read | FieldMode.Write, name: "reserved");

            Registers.TIMER_TCLR.Define(dwordregisters, 0x00, "TIMER_TCLR")
            .WithFlag(0, out TimerEnabled, FieldMode.Read | FieldMode.Write,
                                writeCallback: (_, value) =>
                                {
                                   Timer.Enabled = value;
                                   this.Log(LogLevel.Noisy, "Timer Enabled: {0}", Timer.Enabled);
                                }, name: "ST")
            .WithFlag(1, out AutoReload, FieldMode.Read | FieldMode.Write,
                                writeCallback: (_, value) =>
                                {
//                                    Timer.AutoUpdate = value;
                                    this.Log(LogLevel.Noisy, "AutoReload Enabled: {0}", value);
                                }, name: "AR")
            .WithValueField(2, 3, out Prescaler, FieldMode.Read | FieldMode.Write,name: "PTV")
            .WithFlag(5, FieldMode.Read | FieldMode.Write,
                                writeCallback: (_, value) =>
                                {
                                    if (value == true)
                                        Timer.Divider = (int) Math.Pow((double)(Prescaler.Value + 1) , 2.0);
                                    else
                                        Timer.Divider = 1;
                                    this.Log(LogLevel.Noisy, "Timer Divider: {0}", Timer.Divider);

                                }, name: "PRE")
            .WithFlag(6, out CompareModeEnabled, FieldMode.Read | FieldMode.Write,
                                writeCallback: (_, value) =>
                                {
                                    if (value)
                                    {
                                        this.Log(LogLevel.Noisy, "Compare Mode Enabled: {0}", value);
//                                        Timer.Mode = WorkMode.OneShot;
                                    }
                                    else
                                        this.Log(LogLevel.Noisy, "Compare Mode Enabled: {0}", value);

                                }, name: "CE")
            .WithFlag(7, FieldMode.Read | FieldMode.Write, name: "SCPWM")
            .WithValueField(8, 2, out TransitionCaptureMode, FieldMode.Read | FieldMode.Write, name: "TCM")
            .WithValueField(10, 2, out trigger, FieldMode.Read | FieldMode.Write, name: "TRG")
            .WithFlag(12, FieldMode.Read | FieldMode.Write, name: "PT")
            .WithFlag(13, out capture_mode, FieldMode.Read | FieldMode.Write, name: "CAPT_MODE")
            .WithFlag(14, FieldMode.Read | FieldMode.Write, name: "GPO_CFG")
            .WithValueField(15, 17, FieldMode.Read | FieldMode.Write, name: "reserved");

            Registers.TIMER_TCRR.Define(dwordregisters, 0x00, "TIMER_TCRR")
            .WithValueField(0, 32, FieldMode.Read | FieldMode.Write,
                                writeCallback: (_, value) =>
                                {
                                    Timer.Value = value;
                                    this.Log(LogLevel.Noisy, "Timer Value Write: {0:X}", value);
                                },                                
                                valueProviderCallback: _ =>
                                {
                                    this.Log(LogLevel.Noisy, "Timer Value Read: {0:X}", Timer.Value);
                                    return (uint) Timer.Value;
                                }, name: "TIMER_TCRR");

            Registers.TIMER_TLDR.Define(dwordregisters, 0x00, "TIMER_TLDR")
            .WithValueField(0, 32, out TimerStartValue, FieldMode.Read | FieldMode.Write, name: "TIMER_TLDR");

            Registers.TIMER_TTGR.Define(dwordregisters, 0xFFFFFFFF, "TIMER_TTGR")
            .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "TIMER_TTGR");

            Registers.TIMER_TWPS.Define(dwordregisters, 0x00, "TIMER_TWPS")
            .WithFlag(0, FieldMode.Read | FieldMode.Write, name: "W_PEND_TCLR")
            .WithFlag(1, FieldMode.Read | FieldMode.Write, name: "W_PEND_TCRR")
            .WithFlag(2, FieldMode.Read | FieldMode.Write, name: "W_PEND_TLDR")
            .WithFlag(3, FieldMode.Read | FieldMode.Write, name: "W_PEND_TTGR")
            .WithFlag(4, FieldMode.Read | FieldMode.Write, name: "W_PEND_TMAR")
            .WithFlag(5, FieldMode.Read | FieldMode.Write, name: "W_PEND_TPIR")
            .WithFlag(6, FieldMode.Read | FieldMode.Write, name: "W_PEND_TNIR")
            .WithFlag(7, FieldMode.Read | FieldMode.Write, name: "W_PEND_TCVR")
            .WithFlag(8, FieldMode.Read | FieldMode.Write, name: "W_PEND_TOCR")
            .WithFlag(9, FieldMode.Read | FieldMode.Write, name: "W_PEND_TOWR")
            .WithValueField(10, 22, FieldMode.Read | FieldMode.Write, name: "TIMER_TWPS");

            Registers.TIMER_TMAR.Define(dwordregisters, 0x00, "TIMER_TMAR")
            .WithValueField(0, 32, out TimerCompareValue, FieldMode.Read | FieldMode.Write, name: "TIMER_TMAR");

            Registers.TIMER_TCAR1.Define(dwordregisters, 0x00, "TIMER_TCAR1")
            .WithValueField(0, 32, out Tcar1, FieldMode.Read, name: "TIMER_TCAR1");

            Registers.TIMER_TSICR.Define(dwordregisters, 0x00, "TIMER_TSICR")
            .WithFlag(0, FieldMode.Read | FieldMode.Write, name: "reserved")
            .WithFlag(1, FieldMode.Read | FieldMode.Write, name: "SFT")
            .WithFlag(2, FieldMode.Read | FieldMode.Write, name: "POSTED")
            .WithFlag(3, FieldMode.Read | FieldMode.Write, name: "READ_MODE")
            .WithValueField(4, 28, FieldMode.Read | FieldMode.Write, name: "reserved");

            Registers.TIMER_TCAR2.Define(dwordregisters, 0x00, "TIMER_TCAR2")
            .WithValueField(0, 32, out Tcar2, FieldMode.Read, name: "TIMER_TCAR2");


        }




        public  void Reset()
        {
            dwordregisters.Reset();
            Timer.Reset();

            UpdateInterrupts();
        }

        private void UpdateInterrupts()
        {

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


        private enum Registers : long
        {

            TIMER_TIDR             = 0x0U,
            TIMER_TIOCP_CFG        = 0x10U,
            TIMER_IRQ_EOI          = 0x20U,
            TIMER_IRQSTATUS_RAW    = 0x24U,
            TIMER_IRQSTATUS        = 0x28U,
            TIMER_IRQENABLE_SET    = 0x2cU,
            TIMER_IRQENABLE_CLR    = 0x30U,
            TIMER_IRQWAKEEN        = 0x34U,
            TIMER_TCLR             = 0x38U,
            TIMER_TCRR             = 0x3cU,
            TIMER_TLDR             = 0x40U,
            TIMER_TTGR             = 0x44U,
            TIMER_TWPS             = 0x48U,
            TIMER_TMAR             = 0x4cU,
            TIMER_TCAR1            = 0x50U,
            TIMER_TSICR            = 0x54U,
            TIMER_TCAR2            = 0x58U
        }
    }
}