//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using System;

namespace Antmicro.Renode.Peripherals.I2C
{

    [AllowedTranslations(AllowedTranslation.WordToDoubleWord | AllowedTranslation.ByteToDoubleWord)]
    public class DRA78x_I2C_Master : SimpleContainer<II2CPeripheral>, IKnownSize, IWordPeripheral, IBytePeripheral, IDoubleWordPeripheral
    {

        private readonly DoubleWordRegisterCollection dwordregisters;
        private List<byte> txpacket = new List<byte>();
        II2CPeripheral slave;
        private IValueRegisterField SlaveAddress;
        private IValueRegisterField Length;
        private IValueRegisterField tx_stat;
        private IValueRegisterField rx_stat;
        private IFlagRegisterField bEnabled;
        private Boolean bXDRE_Interrupt_Enabled;
        private Boolean bRDRE_Interrupt_Enabled;
        private Boolean bXRDY_Interrupt_Enabled;
        private Boolean bRRDY_Interrupt_Enabled;
        private Boolean bARDY_Interrupt_Enabled;
        private IFlagRegisterField bXDRE_IE_Set;
        private IFlagRegisterField bRDRE_IE_Set;
        private IFlagRegisterField bXRDY_IE_Set;
        private IFlagRegisterField bRRDY_IE_Set;
        private IFlagRegisterField bARDY_IE_Set;
        private IFlagRegisterField bXDRE_IE_Clr;
        private IFlagRegisterField bRDRE_IE_Clr;
        private IFlagRegisterField bXRDY_IE_Clr;
        private IFlagRegisterField bRRDY_IE_Clr;
        private IFlagRegisterField bARDY_IE_Clr;
        private IFlagRegisterField bXDR_Active;
        private IFlagRegisterField bRDR_Active;
        private IFlagRegisterField bXDRRAW_Active;
        private IFlagRegisterField bRDRRAW_Active;
        private IFlagRegisterField bXRDY_Active;
        private IFlagRegisterField bXRDYRAW_Active;
        private IFlagRegisterField bARDY_Active;
        private IFlagRegisterField bARDYRAW_Active;
        private IFlagRegisterField bRRDY_Active;
        private IFlagRegisterField bRRDYRAW_Active;
        private IFlagRegisterField bTRX;

        public DRA78x_I2C_Master(Machine machine) : base(machine)
        {
            IRQ = new GPIO();
            dwordregisters = new DoubleWordRegisterCollection(this);
            DefineRegisters();

        }

        private void DefineRegisters()
        {
            Registers.I2C_SYSS.Define(dwordregisters, 0x0001, "I2C_SYSS")
            .WithTag("RESERVED", 0, 16);

            Registers.I2C_SYSC.Define(dwordregisters, 0x00000000, "I2C_SYSC")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "I2C_SYSC");
            Registers.I2C_PSC.Define(dwordregisters, 0x00000000, "I2C_PSC")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "I2C_PSC");
            Registers.I2C_SCLL.Define(dwordregisters, 0x00000000, "I2C_SCLL")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "I2C_SCLL");
            Registers.I2C_SCLH.Define(dwordregisters, 0x00000000, "I2C_SCLH")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "I2C_SCLH");


            Registers.I2C_IRQENABLE_SET.Define(dwordregisters, 0x00, "I2C_IRQENABLE_SET")
                .WithFlag(0, FieldMode.Read | FieldMode.Set, name: "AL_IE")
                .WithFlag(1, FieldMode.Read | FieldMode.Set, name: "NACK_IE")
                .WithFlag(2, out bARDY_IE_Set, FieldMode.Read | FieldMode.Set, writeCallback: (_, value) =>
                {
                    if (value == true)
                        bARDY_Interrupt_Enabled = UpdateInterruptEnable(true, bARDY_IE_Set, bARDY_IE_Clr);
                }, name: "ARDY_IE")
                .WithFlag(3, out bRRDY_IE_Set, FieldMode.Read | FieldMode.Set, writeCallback: (_, value) =>
                {
                    if (value == true)
                        bRRDY_Interrupt_Enabled = UpdateInterruptEnable(true, bRRDY_IE_Set, bRRDY_IE_Clr);
                }, name: "RRDY_IE")
                .WithFlag(4, out bXRDY_IE_Set, FieldMode.Read | FieldMode.Set, writeCallback: (_, value) =>
                {
                    if (value == true)
                        bXRDY_Interrupt_Enabled = UpdateInterruptEnable(true, bXRDY_IE_Set, bXRDY_IE_Clr);
                }, name: "XRDY_IE")
                .WithFlag(5, FieldMode.Read | FieldMode.Set, name: "GC_IE")
                .WithFlag(6, FieldMode.Read | FieldMode.Set, name: "STC_IE")
                .WithFlag(7, FieldMode.Read | FieldMode.Set, name: "AERR_IE")
                .WithFlag(8, FieldMode.Read | FieldMode.Set, name: "BF_IE")
                .WithFlag(9, FieldMode.Read | FieldMode.Set, name: "AAS_IE")
                .WithFlag(10, FieldMode.Read | FieldMode.Set, name: "XUDF")
                .WithFlag(11, FieldMode.Read | FieldMode.Set, name: "ROVR")
                .WithFlag(12, FieldMode.Read | FieldMode.Set, name: "reserved")
                .WithFlag(13, out bRDRE_IE_Set, FieldMode.Read | FieldMode.Set, writeCallback: (_, value) =>
                {
                    if (value == true)
                        bRDRE_Interrupt_Enabled = UpdateInterruptEnable(true, bRDRE_IE_Set, bRDRE_IE_Clr);
                }, name: "RDR_IE")

                .WithFlag(14, out bXDRE_IE_Set, FieldMode.Read | FieldMode.Set, writeCallback: (_, value) =>
                {
                    if (value == true)
                        bXDRE_Interrupt_Enabled = UpdateInterruptEnable(true, bXDRE_IE_Set, bXDRE_IE_Clr);
                }, name: "XDR_IE")
                .WithFlag(15, FieldMode.Read | FieldMode.Set, name: "reserved");


            Registers.I2C_IRQENABLE_CLR.Define(dwordregisters, 0x00, "I2C_IRQENABLE_CLR")
                .WithFlag(0, FieldMode.Read | FieldMode.WriteOneToClear, name: "AL_IE")
                .WithFlag(1, FieldMode.WriteOneToClear | FieldMode.Read, name: "NACK_IE")
                .WithFlag(2, out bARDY_IE_Clr, FieldMode.Read | FieldMode.WriteOneToClear, writeCallback: (_, value) =>
                {
                    if (value == true)
                        bARDY_Interrupt_Enabled = UpdateInterruptEnable(false, bARDY_IE_Set, bARDY_IE_Clr);
                }, name: "ARDY_IE")
                .WithFlag(3, out bRRDY_IE_Clr, FieldMode.Read | FieldMode.WriteOneToClear, writeCallback: (_, value) =>
                {
                    if (value == true)
                        bRRDY_Interrupt_Enabled = UpdateInterruptEnable(false, bRRDY_IE_Set, bRRDY_IE_Clr);
                }, name: "RRDY_IE")
                .WithFlag(4, out bXRDY_IE_Clr, FieldMode.Read | FieldMode.WriteOneToClear, writeCallback: (_, value) =>
                {
                    if (value == true)
                        bXRDY_Interrupt_Enabled = UpdateInterruptEnable(false, bXRDY_IE_Set, bXRDY_IE_Clr);
                }, name: "XRDY_IE")
                .WithFlag(5, FieldMode.Read | FieldMode.WriteOneToClear, name: "GC_IE")
                .WithFlag(6, FieldMode.Read | FieldMode.WriteOneToClear, name: "STC_IE")
                .WithFlag(7, FieldMode.Read | FieldMode.WriteOneToClear, name: "AERR_IE")
                .WithFlag(8, FieldMode.Read | FieldMode.WriteOneToClear, name: "BF_IE")
                .WithFlag(9, FieldMode.Read | FieldMode.WriteOneToClear, name: "AAS_IE")
                .WithFlag(10, FieldMode.Read | FieldMode.WriteOneToClear, name: "XUDF")
                .WithFlag(11, FieldMode.Read | FieldMode.WriteOneToClear, name: "ROVR")
                .WithFlag(12, FieldMode.Read | FieldMode.WriteOneToClear, name: "reserved")
                .WithFlag(13, out bRDRE_IE_Clr, FieldMode.Read | FieldMode.WriteOneToClear, writeCallback: (_, value) =>
                {
                    if (value == true)
                        bRDRE_Interrupt_Enabled = UpdateInterruptEnable(false, bRDRE_IE_Set, bRDRE_IE_Clr);
                }, name: "RDR_IE")
                .WithFlag(14, out bXDRE_IE_Clr, FieldMode.Read | FieldMode.WriteOneToClear, writeCallback: (_, value) =>
                {
                    if (value == true)
                        bXDRE_Interrupt_Enabled = UpdateInterruptEnable(false, bXDRE_IE_Set, bXDRE_IE_Clr);
                }, name: "XDR_IE")
                .WithFlag(15, FieldMode.Read | FieldMode.WriteOneToClear, name: "reserved");

            Registers.I2C_IRQSTATUS.Define(dwordregisters, 0x0004, "I2C_IRQSTATUS")
                .WithFlag(0, FieldMode.Read | FieldMode.WriteOneToClear, name: "AL")
                .WithFlag(1, FieldMode.Read | FieldMode.WriteOneToClear, name: "NACK")
                .WithFlag(2, out bARDY_Active, FieldMode.Read | FieldMode.WriteOneToClear, name: "ARDY")
                .WithFlag(3, out bRRDY_Active, FieldMode.Read | FieldMode.WriteOneToClear, name: "RRDY")
                .WithFlag(4, out bXRDY_Active, FieldMode.Read | FieldMode.WriteOneToClear, name: "XRDY")
                .WithFlag(5, FieldMode.Read | FieldMode.WriteOneToClear, name: "GC")
                .WithFlag(6, FieldMode.Read | FieldMode.WriteOneToClear, name: "STC")
                .WithFlag(7, FieldMode.Read | FieldMode.WriteOneToClear, name: "AERR")
                .WithFlag(8, FieldMode.Read | FieldMode.WriteOneToClear, name: "BF")
                .WithFlag(9, FieldMode.Read | FieldMode.WriteOneToClear, name: "AAS")
                .WithFlag(10, FieldMode.Read | FieldMode.WriteOneToClear, name: "XUDF")
                .WithFlag(11, FieldMode.Read | FieldMode.WriteOneToClear, name: "ROVR")
                .WithFlag(12, FieldMode.Read, name: "BB")
                .WithFlag(13, out bRDR_Active, FieldMode.Read | FieldMode.WriteOneToClear, name: "RDR")
                .WithFlag(14, out bXDR_Active, FieldMode.Read | FieldMode.WriteOneToClear, name: "XDR")
                .WithFlag(15, FieldMode.Read | FieldMode.WriteOneToClear, name: "reserved");

            Registers.I2C_IRQSTATUS_RAW.Define(dwordregisters, 0x0004, "I2C_IRQSTATUS_RAW")
                .WithFlag(0, FieldMode.Read | FieldMode.Write, name: "AL")
                .WithFlag(1, FieldMode.Read | FieldMode.Write, name: "NACK")
                .WithFlag(2, out bARDYRAW_Active, FieldMode.Read | FieldMode.Write, name: "ARDY")
                .WithFlag(3, out bRRDYRAW_Active, FieldMode.Read | FieldMode.Write, name: "RRDY")
                .WithFlag(4, out bXRDYRAW_Active, FieldMode.Read | FieldMode.Write, name: "XRDY")
                .WithFlag(5, FieldMode.Read | FieldMode.Write, name: "GC")
                .WithFlag(6, FieldMode.Read | FieldMode.Write, name: "STC")
                .WithFlag(7, FieldMode.Read | FieldMode.Write, name: "AERR")
                .WithFlag(8, FieldMode.Read | FieldMode.Write, name: "BF")
                .WithFlag(9, FieldMode.Read | FieldMode.Write, name: "AAS")
                .WithFlag(10, FieldMode.Read | FieldMode.Write, name: "XUDF")
                .WithFlag(11, FieldMode.Read | FieldMode.Write, name: "ROVR")
                .WithFlag(12, FieldMode.Read, name: "BB")
                .WithFlag(13, out bRDRRAW_Active, FieldMode.Read | FieldMode.Write, name: "XDR")
                .WithFlag(14, out bXDRRAW_Active, FieldMode.Read | FieldMode.Write, name: "XDR")
                .WithFlag(15, FieldMode.Read | FieldMode.WriteOneToClear, name: "reserved");

            Registers.I2C_CON.Define(dwordregisters, 0x0000, "I2C_CON")
                .WithFlag(0, FieldMode.Read | FieldMode.Write, name: "STT")
                .WithFlag(1, FieldMode.Read | FieldMode.Write,
                                            writeCallback: (_, value) =>
                                            {
                                                if (bTRX.Value)
                                                {
                                                    //  bXRDY_Active.Value = true;
//                                                    bXDR_Active.Value = true;
                                                }
                                                else
                                                {
//                                                    bRRDY_Active.Value = true;
//                                                    bRDR_Active.Value = true;
                                                }
//                                                bARDY_Active.Value = false;
                                            }, name: "STP")
                .WithFlag(2, FieldMode.Read | FieldMode.Write, name: "reserved")
                .WithFlag(3, FieldMode.Read | FieldMode.Write, name: "reserved")
                .WithFlag(4, FieldMode.Read | FieldMode.Write, name: "XOA3")
                .WithFlag(5, FieldMode.Read | FieldMode.Write, name: "XOA2")
                .WithFlag(6, FieldMode.Read | FieldMode.Write, name: "XOA1")
                .WithFlag(7, FieldMode.Read | FieldMode.Write, name: "XOA0")
                .WithFlag(8, FieldMode.Read | FieldMode.Write, name: "XSA")
                .WithFlag(9, out bTRX, FieldMode.Read | FieldMode.Write,
                                            writeCallback: (_, value) =>
                                            {
                                                if ((value == true) && (slave != null))
                                                {
                                                    slave.FinishTransmission();
                                                }

                                            }, name: "TRX")
                .WithFlag(10, FieldMode.Read | FieldMode.Write, name: "MST")
                .WithFlag(11, FieldMode.Read | FieldMode.Write, name: "STB")
                .WithValueField(12, 2, FieldMode.Read | FieldMode.Write, name: "OPMODE")
                .WithFlag(14, FieldMode.Read | FieldMode.Set, name: "reserved")
                .WithFlag(15, out bEnabled, FieldMode.Read | FieldMode.Write, name: "I2C_EN");


            Registers.I2C_DATA.Define(dwordregisters, 0x0140, "I2C_DATA")
            .WithValueField(0, 8, FieldMode.Read | FieldMode.Write,
                                            writeCallback: (_, value) =>
                                            {
                                                this.Log(LogLevel.Noisy, "DRA78x I2C Master send Data 0x{0:X} to  0x{1:X} Length {2}", value, SlaveAddress.Value <<1, Length.Value);
                                                if ((Length.Value > 0) && (slave != null))
                                                {
                                                    txpacket.Add((byte)value);
                                                    slave.Write(txpacket.ToArray());
                                                    txpacket.Clear();
                                                    Length.Value--;
                                                }

//                                                bARDY_Active.Value = true;
//                                                bXRDY_Active.Value = true;
//                                                bXDR_Active.Value = true;

                                            },
                                valueProviderCallback: _ =>
                                {
                                    if ((Length.Value > 0) && (slave != null))
                                    {
                                        var rxpacket = slave.Read(1);
                                        this.Log(LogLevel.Noisy, "DRA78x I2C Master read Data 0x{0:X} to  0x{1:X} Lenght {2}", rxpacket, SlaveAddress.Value << 1 , Length.Value);
                                        Length.Value--;
                                        return (rxpacket[0]);
                                    }
//                                    bARDY_Active.Value = true;
//                                    bRRDY_Active.Value = true;

                                    return 0x00; // character;
                                }, name: "I2C_DATA")

            .WithValueField(8, 8, FieldMode.Read | FieldMode.Write, name: "reserved");

            Registers.I2C_SYSTEST.Define(dwordregisters, 0x0140, "I2C_SYSTEST")
            .WithValueField(0, 16, FieldMode.Read | FieldMode.Write, name: "I2C_SYSTEST");

            Registers.I2C_SA.Define(dwordregisters, 0x0140, "I2C_SA")
            .WithValueField(0, 10, out SlaveAddress, FieldMode.Read | FieldMode.Write,
                                writeCallback: (_, value) =>
                                {
                                    int adr = (int)SlaveAddress.Value << 1;
                                    TryGetByAddress(adr, out slave);
                                }, name: "I2C_SA")
            .WithValueField(10, 6, FieldMode.Read | FieldMode.Write, name: "reserved");

            Registers.I2C_CNT.Define(dwordregisters, 0x0140, "I2C_CNT")
                                .WithValueField(0, 16, out Length, FieldMode.Read | FieldMode.Write,
                                writeCallback: (_, value) =>
                                {
//                                    if (value == 0)
//                                    slave.FinishTransmission();
                                    tx_stat.Value = value;
                                    rx_stat.Value = value;
//                                    bARDY_Active.Value = true;
                                    UpdateInterrupts();

                                }, name: "I2C_CNT");

            Registers.I2C_BUFSTAT.Define(dwordregisters, 0xC000, "I2C_BUFSTAT")
            .WithValueField(0, 6, out tx_stat, FieldMode.Read | FieldMode.Write, name: "TX_STAT")
            .WithValueField(6, 2, FieldMode.Read | FieldMode.Write, name: "reserved")
            .WithValueField(8, 6, out rx_stat, FieldMode.Read | FieldMode.Write, name: "RX_STAT")
            .WithValueField(14, 2, FieldMode.Read | FieldMode.Write, name: "fifo_depth");

            Registers.I2C_BUF.Define(dwordregisters, 0x0000, "I2C_BUF")
            .WithValueField(0, 15, FieldMode.Read | FieldMode.Write, name: "I2C_BUF");

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

        public long Size => 214;

        public GPIO IRQ { get; private set; }


        public override void Reset()
        {
            dwordregisters.Reset();
            UpdateInterrupts();
            bARDY_Active.Value = false;
            bARDYRAW_Active.Value = false;
            bXRDY_Active.Value = false;
            bXRDYRAW_Active.Value = false;
            bRRDY_Active.Value = false;
            bRRDYRAW_Active.Value = false;

        }

        private void UpdateInterrupts()
        {
            if (Length.Value == 0)
            {
                this.Log(LogLevel.Noisy, "UpdateInterrupts length = 0");
                if (bARDY_Interrupt_Enabled == true)
                {
                    bARDY_Active.Value = true;
                    bARDYRAW_Active.Value = true;
                }
                bXRDY_Active.Value = false;
                bXRDYRAW_Active.Value = false;
                bRRDY_Active.Value = false;
                bRRDYRAW_Active.Value = false;
            }
            else
            {
                this.Log(LogLevel.Noisy, "UpdateInterrupts length != 0");
                bARDY_Active.Value = false;
                bARDYRAW_Active.Value = false;
                if (bTRX.Value)
                {
                    this.Log(LogLevel.Noisy, "UpdateInterrupts bTRX = true");
                    if (bXDRE_Interrupt_Enabled == true)
                    {
                        bXDR_Active.Value = true;
                        bXDRRAW_Active.Value = true;
                    }
                    bRDR_Active.Value = false;
                    bRDRRAW_Active.Value = false;
                }
                else
                {
                    this.Log(LogLevel.Noisy, "UpdateInterrupts bTRX = false");
                    if (bRDRE_Interrupt_Enabled == true)
                    {
                        bRDR_Active.Value = true;
                        bRDRRAW_Active.Value = true;
                    }
                    bXDR_Active.Value = false;
                    bXDRRAW_Active.Value = false;
                }
            }

            if (bEnabled.Value == true)
            {
                //                if (bXDRE_Interrupt_Enabled || bXDRE_Interrupt_Enabled|| bARDY_Interrupt_Enabled || bXRDY_Interrupt_Enabled || bRRDY_Interrupt_Enabled)
                //                    IRQ.Set(bXDR_Active.Value | bARDY_Active.Value | bXRDY_Active.Value | bRRDY_Active.Value);
                if (bXDRE_Interrupt_Enabled || bRDRE_Interrupt_Enabled | bARDY_Active.Value)
                {
                    this.Log(LogLevel.Noisy, "EnableInterrupt");
                    IRQ.Set(bXDR_Active.Value | bRDR_Active.Value | bARDY_Active.Value);
                }
                else
                {
                    this.Log(LogLevel.Noisy, "DisableInterrupt");
                    IRQ.Set(false);
                }
            }
            else
            {
                this.Log(LogLevel.Noisy, "DisableInterrupt");
                IRQ.Set(false);
            }

        }

        private Boolean UpdateInterruptEnable(Boolean flag, IFlagRegisterField SetFlag, IFlagRegisterField ClrFlag)
        {
            Boolean retVal = true;
            if (flag == true)
            {
                SetFlag.Value = true;
                ClrFlag.Value = false;
            }
            else
            {
                SetFlag.Value = false;
                ClrFlag.Value = true;
                retVal = false;
            }
            return retVal;
        }



        public uint ReadDoubleWord(long offset)
        {
            return dwordregisters.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            dwordregisters.Write(offset, value);
            UpdateInterrupts();
        }

        public enum Registers : long
        {
            I2C_REVNB_LO = 0x0U,
            I2C_REVNB_HI = 0x4U,
            I2C_SYSC = 0x10U,
            I2C_IRQSTATUS_RAW = 0x24U,
            I2C_IRQSTATUS = 0x28U,
            I2C_IRQENABLE_SET = 0x2cU,
            I2C_IRQENABLE_CLR = 0x30U,
            I2C_WE = 0x34U,
            I2C_DMARXENABLE_SET = 0x38U,
            I2C_DMATXENABLE_SET = 0x3cU,
            I2C_DMARXENABLE_CLR = 0x40U,
            I2C_DMATXENABLE_CLR = 0x44U,
            I2C_DMARXWAKE_EN = 0x48U,
            I2C_DMATXWAKE_EN = 0x4cU,
            I2C_SYSS = 0x90U,
            I2C_BUF = 0x94U,
            I2C_CNT = 0x98U,
            I2C_DATA = 0x9cU,
            I2C_CON = 0xa4U,
            I2C_OA = 0xa8U,
            I2C_SA = 0xacU,
            I2C_PSC = 0xb0U,
            I2C_SCLL = 0xb4U,
            I2C_SCLH = 0xb8U,
            I2C_SYSTEST = 0xbcU,
            I2C_BUFSTAT = 0xc0U,
            I2C_OA1 = 0xc4U,
            I2C_OA2 = 0xc8U,
            I2C_OA3 = 0xccU,
            I2C_ACTOA = 0xd0U,
            I2C_SBLOCK = 0xd4U
        }

    }
}
