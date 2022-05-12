//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Network;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Core.Structure.Registers;
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Memory;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Network
{
    [AllowedTranslations(AllowedTranslation.WordToDoubleWord | AllowedTranslation.ByteToDoubleWord)]

    public class DRA78x_Eth : IMACInterface, IKnownSize, IDoubleWordPeripheral, IWordPeripheral, IBytePeripheral, IGPIOReceiver, IMemory
    {
        public DRA78x_Eth(Machine machine)
        {
            this.Log(LogLevel.Noisy, "Constructor");

            mach = machine;
            dwordregisters = new DoubleWordRegisterCollection(this);
            messagememory = new ArrayMemory((int)BASE_MESSAGELENGTH);
            MAC = new MACAddress();
            IRQ = new GPIO();

            rxQueue = new Queue<EthernetFrame>();
            cptsHighQueue = new Queue<uint>();
            cptsLowQueue = new Queue<uint>();
            cptsLock = new object();

            DefineRegisters();
            Reset();
        }

        public void Reset()
        {
            this.Log(LogLevel.Noisy, "Reset");
            dwordregisters.Reset();
            ResetAleTables();
            messagememory.Reset();


        }


        public GPIO IRQ { get; private set; }

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
            if (offset >= BASE_MESSAGEADR)
            {
                if (rxQueue.Count > 1)
                    CopyRxFrame();
                return messagememory.ReadDoubleWord(offset - BASE_MESSAGEADR);
            }
            else

                return dwordregisters.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if (offset >= BASE_MESSAGEADR)
                messagememory.WriteDoubleWord(offset - BASE_MESSAGEADR, value);
            else
                dwordregisters.Write(offset, value);
        }

        public event Action<EthernetFrame> FrameReady;

        public RxBufferDescriptor GetNextRxDescriptor(int ChannelIndex, RxBufferDescriptor CurrentDescriptor)
        {
            uint Adr=0;
            if (CurrentDescriptor.NextDescriptorPointer.Value == 0)    // End of physical Queue
            {
                if (rx_hdp[ChannelIndex].Value != 0)
                    Adr = rx_hdp[ChannelIndex].Value - BASE_MESSAGEADR_L3;     // use first buffer again
            }
            else
            {
                Adr = CurrentDescriptor.NextDescriptorPointer.Value - BASE_MESSAGEADR_L3;     // use next buffer
            }

            if (Adr != 0)
               return  new RxBufferDescriptor(Adr, messagememory);

            return null;
        }

        public void CopyRxFrame()
        {

            EthernetFrame frame = rxQueue.Peek();
            int ChannelIndex = 1;

            if (frame.UnderlyingPacket.Type == PacketDotNet.EthernetPacketType.PrecisionTimeProtocol)
                ChannelIndex = 0;



            if (rx_hdp[ChannelIndex].Value == 0)
                return;

            int RemainingBytesToCopy = frame.Bytes.Length;
            int NumBytesToCopy = 0;
            int StartIndex = 0;
            Boolean OverrunError = false;
            RxBufferDescriptor StartDescriptor = new RxBufferDescriptor(RxDescriptorAdr[ChannelIndex], messagememory);
            RxBufferDescriptor Descriptor = StartDescriptor;
            RxBufferDescriptor EndDescriptor = StartDescriptor;

            while (RemainingBytesToCopy >0)
            {
                // Check if buffer is free
                if (Descriptor.Owner.Value == true)
                {
                    NumBytesToCopy = Math.Min((int)Descriptor.BufferLength.Value, RemainingBytesToCopy);
                    mach.SystemBus.WriteBytes(frame.Bytes, Descriptor.BufferPointer.Value + Descriptor.BufferOffset.Value, StartIndex, NumBytesToCopy);
                    RemainingBytesToCopy -= NumBytesToCopy;
                    StartIndex += NumBytesToCopy;
                    if (RemainingBytesToCopy > 0)
                        Descriptor = GetNextRxDescriptor(ChannelIndex, Descriptor);
                    if (Descriptor == null)
                    {
                        RemainingBytesToCopy = 0;
                        OverrunError = true;
                        this.Log(LogLevel.Error, "No receive buffers allocated DMA: {0}", ChannelIndex);
                    }

                }
                else
                {
                    this.Log(LogLevel.Error, "Received packet does not fit in free buffers DMA: {0}", ChannelIndex);
                    RemainingBytesToCopy = 0;
                    OverrunError = true;
                }
            }

            if (OverrunError == false)
            {
                rxQueue.Dequeue();         // remove processed frame
                if (frame.UnderlyingPacket.Type == PacketDotNet.EthernetPacketType.PrecisionTimeProtocol)
                {
                    if (ts_pend_enable.Value)
                    {
                        PushCptsData(0x10, 0x04, 1, (uint)frame.Bytes[14] & 0x000F, ((uint)frame.Bytes[44] << 8) | ((uint)frame.Bytes[45]), 0x00);
                        this.Log(LogLevel.Noisy, "Generate IRQ Rx");
                    }
                }

                Descriptor.BufferLength.Value = (uint)NumBytesToCopy;     // Write BufferLength
                Descriptor.EOP.Value = true;                              // Set EOP
                Descriptor.EOQ.Value = true;                              // Set EOQ  
                Descriptor.Store();                                       // Save Values

                StartDescriptor.SOP.Value = true;                         // Set SOP
                if (frame.Length == frame.Bytes.Length)
                    StartDescriptor.PassCrc.Value = false;                     // Set CRC not included
                else
                    StartDescriptor.PassCrc.Value = true;                     // Set CRC included
                StartDescriptor.PacketLength.Value = (uint)frame.Bytes.Length;  // Write PacketLength
                StartDescriptor.Owner.Value = false;                      // Clear Owner
                StartDescriptor.Store();                                  // Save Values

                rx_cp[ChannelIndex].Value = StartDescriptor.BufferPointer.Value;         // write completion register

                Descriptor = GetNextRxDescriptor(ChannelIndex, Descriptor);              // Get Next Descriptor
                if (Descriptor != null)
                   RxDescriptorAdr[ChannelIndex] = Descriptor.Adress;  // Store Physical Adress
                else
                   RxDescriptorAdr[ChannelIndex] = 0;                 // Store Physical Adress

                this.Log(LogLevel.Noisy, "Receive: MsgAdr:{0:X} DMA:{1} Length:{2} SrcIP:{3} DstIP:{4}", Descriptor.BufferPointer.Value+Descriptor.BufferOffset.Value,
                                                                                                        ChannelIndex, frame.Length, frame.SourceIP, frame.DestinationIP);

            }
        }

        public void ReceiveFrame(EthernetFrame frame)
        {
            if (cpdma_rx_enabled.Value)
            {
                rxQueue.Enqueue(frame);
                CopyRxFrame();
            }
        }


        public long Size
        {
            get
            {
                return 0x4000;
            }
        }




        public void OnGPIO(int number, bool value)
        {

            if ((value == true) && (number == 0))
            {
//                    PushCptsData(0x10, 0x03, 0x00, 0x00, 0x00, 0x00);
            }

        }

        public MACAddress MAC { get; set; }
        protected readonly object cptsLock;
        private Queue<EthernetFrame> rxQueue;
        private Queue<uint> cptsHighQueue;
        private Queue<uint> cptsLowQueue;
        private readonly DoubleWordRegisterCollection dwordregisters;

        private const uint BASE_ETH_ADR = 0x48484000;
        private const uint BASE_CPSW_ADR = 0x48484000 - BASE_ETH_ADR;
        private const uint BASE_PORT_ADR = 0x48484100 - BASE_ETH_ADR;
        private const uint BASE_CPDMA_ADR = 0x48484800 - BASE_ETH_ADR;
        private const uint BASE_CPTS_ADR = 0x48484C00 - BASE_ETH_ADR;
        private const uint BASE_ALE_ADR = 0x48484D00 - BASE_ETH_ADR;
        private const uint BASE_SL1_ADR = 0x48484D80 - BASE_ETH_ADR;
        private const uint BASE_SL2_ADR = 0x48484DC0 - BASE_ETH_ADR;
        private const uint BASE_MDIO_ADR = 0x48485000 - BASE_ETH_ADR;
        private const uint BASE_WR_ADR = 0x48485200 - BASE_ETH_ADR;
        private const uint BASE_STATE_RAM = 0x48484A00 - BASE_ETH_ADR;

        private const uint BASE_MESSAGEADR_L3 = 0x48486000;
        private const uint BASE_MESSAGEADR = BASE_MESSAGEADR_L3 - BASE_ETH_ADR;
        private const uint BASE_MESSAGELENGTH = 0x2000;


        private const uint ALE_ENTRY_COUNT = 1024;
        private uint[] Ale_Tblw2 = new uint[ALE_ENTRY_COUNT];
        private uint[] Ale_Tblw1 = new uint[ALE_ENTRY_COUNT];
        private uint[] Ale_Tblw0 = new uint[ALE_ENTRY_COUNT];
        private IValueRegisterField[] tx_hdp = new IValueRegisterField [8];
        private IValueRegisterField[] tx_cp = new IValueRegisterField[8];
        private IValueRegisterField[] rx_hdp = new IValueRegisterField[8];
        private IValueRegisterField[] rx_cp = new IValueRegisterField[8];
        private uint[] RxDescriptorAdr = new uint[8];
        private IValueRegisterField wr_c0_misc_stat;

        private IValueRegisterField ts_ltype1;
        private IValueRegisterField ts_ltype2;
        private IValueRegisterField vlan_ltype1;
        private IValueRegisterField vlan_ltype2;
        private IFlagRegisterField ts_pend_enable;
        private IFlagRegisterField cpdma_tx_enabled;
        private IFlagRegisterField cpdma_rx_enabled;
        private Machine mach;

        private IValueRegisterField entry_pointer_idx;
        private ArrayMemory messagememory;



        private void PushCptsData(uint interruptsource, uint eventtype, uint portnumber, uint messagetype, uint sequenceid, uint timestamp)
        {
            uint DataHigh = 0x00;


            DataHigh = portnumber << 24;
            DataHigh |= eventtype << 20;
            DataHigh |= messagetype << 16;
            DataHigh |= sequenceid;

            lock (cptsLock)
            {

            cptsHighQueue.Enqueue(DataHigh);
            cptsLowQueue.Enqueue(timestamp);
            this.Log(LogLevel.Noisy, "CPTS  event:{0:X} type:{1:X} port:{2} sequence:{3} time:{4}", eventtype, messagetype, portnumber, sequenceid, timestamp);

                wr_c0_misc_stat.Value = interruptsource;    // Misc Interrupt Source for Events
                IRQ.Set();
                IRQ.Unset();
            }
        }


        private void ResetAleTables()
        {
            this.Log(LogLevel.Noisy, "ResetAleTables");
            for (int idx = 0; idx < ALE_ENTRY_COUNT; idx++)
            {
                Ale_Tblw2[idx] = 0;
                Ale_Tblw1[idx] = 0;
                Ale_Tblw0[idx] = 0;
            }
        }


        public TxBufferDescriptor GetNextTxDescriptor(int ChannelIndex, TxBufferDescriptor CurrentDescriptor)
        {
            uint Adr;
            if (CurrentDescriptor.NextDescriptorPointer.Value == 0)
                Adr = tx_hdp[ChannelIndex].Value - BASE_MESSAGEADR_L3;
            else
                Adr = CurrentDescriptor.NextDescriptorPointer.Value - BASE_MESSAGEADR_L3;
            TxBufferDescriptor NextDescriptor = new TxBufferDescriptor(Adr, messagememory);
            return NextDescriptor;
        }

        private void SendFrame(EthernetFrame frame, uint port)
        {
            if (frame.UnderlyingPacket.Type == PacketDotNet.EthernetPacketType.PrecisionTimeProtocol)
            {
                if (ts_pend_enable.Value)
                {
                    PushCptsData(0x10, 0x05, port, (uint)frame.Bytes[14] & 0x000F, ((uint)frame.Bytes[44] << 8) | ((uint)frame.Bytes[45]), 0x00);
                    this.Log(LogLevel.Noisy, "Generate IRQ Tx");
                }
            }
            FrameReady?.Invoke(frame);


        }

        private void CopyFrameToRenode(int ChannelIndex)
        {
            Boolean bEnd = false;
            if (tx_hdp[ChannelIndex].Value == 0)
                return;
            TxBufferDescriptor StartDescriptor = new TxBufferDescriptor(tx_hdp[ChannelIndex].Value - BASE_MESSAGEADR_L3, messagememory);
            TxBufferDescriptor Descriptor = StartDescriptor;

            while (bEnd == false)
            {
                int RemainingBytesToCopy = (int)Descriptor.PacketLength.Value;
                byte[] packetBytes = new byte[StartDescriptor.PacketLength.Value];
                int NumBytesToCopy = 0;
                int StartIndex = 0;
                while (RemainingBytesToCopy > 0)
                {
                    NumBytesToCopy = Math.Min((int)Descriptor.BufferLength.Value, (int)Descriptor.PacketLength.Value);
                    mach.SystemBus.ReadBytes(Descriptor.BufferPointer.Value + Descriptor.BufferOffset.Value, NumBytesToCopy, packetBytes, StartIndex);
                    RemainingBytesToCopy -= NumBytesToCopy;
                    StartIndex += NumBytesToCopy;
                    if (RemainingBytesToCopy > 0)
                        Descriptor = GetNextTxDescriptor(ChannelIndex, Descriptor);
                }
                
                if (Misc.TryCreateFrameOrLogWarning(this, packetBytes, out var packet, addCrc: !StartDescriptor.PassCrc.Value))
                {
                    this.Log(LogLevel.Noisy, "Sending: MsgAdr:{0:X} DMA:{1} Length:{2} SrcIP:{3} DstIP{4}", Descriptor.BufferPointer.Value + Descriptor.BufferOffset.Value,
                                                                                                            ChannelIndex, packet.Length, packet.SourceIP, packet.DestinationIP);
                    SendFrame(packet, StartDescriptor.ToPort.Value);
                    // Release Tx Buffers
                    StartDescriptor.Owner.Value = false;
                    StartDescriptor.Store();
                    tx_cp[ChannelIndex].Value = StartDescriptor.BufferPointer.Value;           // write completion register
                }
                // check if another packet is available
                if (Descriptor.NextDescriptorPointer.Value != 0)
                    Descriptor = GetNextTxDescriptor(ChannelIndex, Descriptor);
                else
                    bEnd = true;

            }
            Descriptor.EOQ.Value = true;
            Descriptor.Store();

            // Enable Next Transfer
            tx_hdp[ChannelIndex].Value = 0;                                                // clear head bufferpointer

        }

        private void DefineRegisters()
        {
            Registers.CPDMA_SOFT_RESET.Define(dwordregisters, 0x00, "CPDMA_SOFT_RESET")
                .WithFlag(0, FieldMode.Read | FieldMode.WriteOneToClear, name: "RESET")
                .WithValueField(1, 31, FieldMode.Read | FieldMode.Write, name: "reserved");
            Registers.SL1_SOFT_RESET.Define(dwordregisters, 0x00, "SL1_SOFT_RESET")
                .WithFlag(0, FieldMode.Read | FieldMode.WriteOneToClear, name: "RESET")
                .WithValueField(1, 31, FieldMode.Read | FieldMode.Write, name: "reserved");
            Registers.SL2_SOFT_RESET.Define(dwordregisters, 0x00, "SL2_SOFT_RESET")
                .WithFlag(0, FieldMode.Read | FieldMode.WriteOneToClear, name: "RESET")
                .WithValueField(1, 31, FieldMode.Read | FieldMode.Write, name: "reserved");
            Registers.CPSW_SOFT_RESET.Define(dwordregisters, 0x00, "CPSW_SOFT_RESET")
                .WithFlag(0, FieldMode.Read | FieldMode.WriteOneToClear, name: "RESET")
                .WithValueField(1, 31, FieldMode.Read | FieldMode.Write, name: "reserved");
            Registers.WR_SOFT_RESET.Define(dwordregisters, 0x00, "WR_SOFT_RESET")
                .WithFlag(0, FieldMode.Read | FieldMode.WriteOneToClear, name: "RESET")
                .WithValueField(1, 31, FieldMode.Read | FieldMode.Write, name: "reserved");

            Registers.CPDMA_RX_INTMASK_CLEAR.Define(dwordregisters, 0x00, "CPDMA_RX_INTMASK_CLEAR")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");
            Registers.CPDMA_TX_INTMASK_CLEAR.Define(dwordregisters, 0x00, "CPDMA_TX_INTMASK_CLEAR")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");
            Registers.CPDMA_DMA_INTMASK_CLEAR.Define(dwordregisters, 0x00, "CPDMA_DMA_INTMASK_CLEAR")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");

            Registers.CPDMA_TX_CONTROL.Define(dwordregisters, 0x00, "CPDMA_TX_CONTROL")
                .WithFlag(0, out cpdma_tx_enabled, FieldMode.Read | FieldMode.Write, name: "TX_EN")
                .WithValueField(1, 31, FieldMode.Read | FieldMode.Write, name: "reserved");
            Registers.CPDMA_RX_CONTROL.Define(dwordregisters, 0x00, "CPDMA_RX_CONTROL")
                .WithFlag(0, out cpdma_rx_enabled, FieldMode.Read | FieldMode.Write, name: "TX_EN")
                .WithValueField(1, 31, FieldMode.Read | FieldMode.Write, name: "reserved");

            Registers.SL1_MACCONTROL.Define(dwordregisters, 0x00, "SL1_MACCONTROL")
                 .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");
            Registers.PORT_P1_PORT_VLAN.Define(dwordregisters, 0x00, "PORT_P1_PORT_VLAN")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");
            Registers.SL1_RX_MAXLEN.Define(dwordregisters, 0x00, "SL1_RX_MAXLEN")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");

            Registers.MDIO_USERACCESS0.Define(dwordregisters, 0x04, "MDIO_USERACCESS0")
                .WithValueField(0, 15, FieldMode.Read, name: "DATA")
                .WithValueField(16, 5, FieldMode.Read | FieldMode.Write, name: "PHYADR")
                .WithValueField(21, 5, FieldMode.Read | FieldMode.Write, name: "REGADR")
                .WithValueField(26, 3, FieldMode.Read | FieldMode.Write, name: "reserved")
                .WithFlag(29, FieldMode.Read | FieldMode.Write, name: "ACK")
                .WithFlag(30, FieldMode.Read | FieldMode.Write, name: "WRITE")
                .WithFlag(31, FieldMode.Read | FieldMode.WriteOneToClear, name: "GO");

            Registers.MDIO_USERACCESS1.Define(dwordregisters, 0x04, "MDIO_USERACCESS1")
                .WithValueField(0, 15, FieldMode.Read, name: "DATA")
                .WithValueField(16, 5, FieldMode.Read | FieldMode.Write, name: "PHYADR")
                .WithValueField(21, 5, FieldMode.Read | FieldMode.Write, name: "REGADR")
                .WithValueField(26, 3, FieldMode.Read | FieldMode.Write, name: "reserved")
                .WithFlag(29, FieldMode.Read | FieldMode.Write, name: "ACK")
                .WithFlag(30, FieldMode.Read | FieldMode.Write, name: "WRITE")
                .WithFlag(31, FieldMode.Read | FieldMode.WriteOneToClear, name: "GO");

            Registers.MDIO_CONTROL.Define(dwordregisters, 0x00, "MDIO_CONTROL")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");



            Registers.WR_C0_MISC_EN.Define(dwordregisters, 0x00, "WR_C0_MISC_EN")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");

            Registers.PORT_P1_CONTROL.Define(dwordregisters, 0x00, "PORT_P1_CONTROL")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");

            Registers.CPSW_TS_LTYPE.Define(dwordregisters, 0x00, "CPSW_TS_LTYPE")
                .WithValueField(0, 16, out ts_ltype1, FieldMode.Read | FieldMode.Write, name: "TS_LTYPE1")
                .WithValueField(16, 16, out ts_ltype2, FieldMode.Read | FieldMode.Write, name: "TS_LTYPE2");

            Registers.CPSW_VLAN_LTYPE.Define(dwordregisters, 0x00, "CPSW_VLAN_LTYPE")
                .WithValueField(0, 16, out vlan_ltype1, FieldMode.Read | FieldMode.Write, name: "VLAN_LTYPE1")
                .WithValueField(16, 16, out vlan_ltype2, FieldMode.Read | FieldMode.Write, name: "VLAN_LTYPE2");

            Registers.CPSW_STAT_PORT_EN.Define(dwordregisters, 0x00, "CPSW_STAT_PORT_EN")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");


            Registers.PORT_P1_TS_SEQ_MTYPE.Define(dwordregisters, 0x00, "PORT_P1_TS_SEQ_MTYPE")
                .WithValueField(0, 16, FieldMode.Read | FieldMode.Write, name: "P1_TS_MSG_TYPE_EN")
                .WithValueField(16, 6, FieldMode.Read | FieldMode.Write, name: "P1_TS_SEQ_ID_OFFSET")
                .WithValueField(22, 10, FieldMode.Read | FieldMode.Write, name: "reserved");

            Registers.PORT_P2_TS_SEQ_MTYPE.Define(dwordregisters, 0x00, "PORT_P2_TS_SEQ_MTYPE")
                .WithValueField(0, 16, FieldMode.Read | FieldMode.Write, name: "P2_TS_MSG_TYPE_EN")
                .WithValueField(16, 6, FieldMode.Read | FieldMode.Write, name: "P2_TS_SEQ_ID_OFFSET")
                .WithValueField(22, 10, FieldMode.Read | FieldMode.Write, name: "reserved");

            Registers.ALE_CONTROL.Define(dwordregisters, 0x00, "ALE_CONTROL")
                .WithValueField(0, 30, FieldMode.Read | FieldMode.Write, name: "reserved")
                .WithFlag(30, FieldMode.Read | FieldMode.Write,
                                            writeCallback: (_, value) =>
                                            {
                                                ResetAleTables();
                                            }, name: "CLEAR_TABLE")
                .WithFlag(31, FieldMode.Read | FieldMode.Write, name: "ENABLE_ALE");

            Registers.ALE_PORTCTL0.Define(dwordregisters, 0x00, "ALE_PORTCTL0")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");

            Registers.ALE_PORTCTL1.Define(dwordregisters, 0x00, "ALE_PORTCTL1")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");

            Registers.ALE_TBLCTL.Define(dwordregisters, 0x00, "ALE_TBLCTL")
                .WithValueField(0, 10, out entry_pointer_idx, FieldMode.Read | FieldMode.Write, name: "ENTRY_POINTER")
                .WithValueField(10, 21, FieldMode.Read | FieldMode.Write, name: "reserved")
                .WithFlag(31, FieldMode.Read | FieldMode.Write, name: "WRITE_RDZ");

            Registers.ALE_TBLW2.Define(dwordregisters, 0x00, "ALE_TBLW2")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write,
                                            writeCallback: (_, value) =>
                                            {
                                                if (entry_pointer_idx.Value < ALE_ENTRY_COUNT)
                                                    Ale_Tblw2[entry_pointer_idx.Value] = value;
                                            },
                                            valueProviderCallback: _ =>
                                            {
                                                return Ale_Tblw2[entry_pointer_idx.Value];
                                            }, name: "ALE_TBLW2");

            Registers.ALE_TBLW1.Define(dwordregisters, 0x00, "ALE_TBLW1")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write,
                                            writeCallback: (_, value) =>
                                            {
                                                if (entry_pointer_idx.Value < ALE_ENTRY_COUNT)
                                                    Ale_Tblw1[entry_pointer_idx.Value] = value;
                                            },
                                            valueProviderCallback: _ =>
                                            {
                                                return Ale_Tblw1[entry_pointer_idx.Value];
                                            }, name: "ALE_TBLW1");

            Registers.ALE_TBLW0.Define(dwordregisters, 0x00, "ALE_TBLW0")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write,
                                            writeCallback: (_, value) =>
                                            {
                                                if (entry_pointer_idx.Value < ALE_ENTRY_COUNT)
                                                    Ale_Tblw0[entry_pointer_idx.Value] = value;
                                            },
                                            valueProviderCallback: _ =>
                                            {
                                                return Ale_Tblw0[entry_pointer_idx.Value];
                                            }, name: "ALE_TBLW0");

            Registers.PORT_P0_TX_PRI_MAP.Define(dwordregisters, 0x00, "PORT_P0_TX_PRI_MAP")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");

            Registers.PORT_P0_CPDMA_RX_CH_MAP.Define(dwordregisters, 0x00, "PORT_P0_CPDMA_RX_CH_MAP")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");

            Registers.PORT_P1_SA_HI.Define(dwordregisters, 0x00, "PORT_P1_SA_HI")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");

            Registers.PORT_P1_SA_LO.Define(dwordregisters, 0x00, "PORT_P1_SA_LO")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");

            Registers.CPDMA_RX0_FREEBUFFER.DefineMany(dwordregisters, 8, (register, idx) =>
            {
                register
                .WithValueField(0, 16, FieldMode.Read | FieldMode.Write, name: "CPDMA_RXx_FREEBUFFER")
                .WithValueField(16, 16, FieldMode.Read | FieldMode.Write, name: "reserved");
            }, stepInBytes: 0x04, resetValue: 0x00, name: "CPDMA_RXx_FREEBUFFER");



            Registers.STATE_RAM_TX0_HDP.DefineMany(dwordregisters, 8, (register, idx) =>
            {
                register
                    .WithValueField(0, 32, out tx_hdp[idx], FieldMode.Read, writeCallback: (_, value) =>
                    {
//                        this.Log(LogLevel.Noisy, "STATE_RAM_TX{0}_HDP: {1:X}", idx, value);
                        tx_hdp[idx].Value = value;
                        CopyFrameToRenode(idx);
                    }, name: $"STATE_RAM_TX{idx}_HDP");
            }, stepInBytes: 0x04, resetValue: 0x00, name: "STATE_RAM_TXx_HDP");

            Registers.STATE_RAM_RX0_HDP.DefineMany(dwordregisters, 8, (register, idx) =>
            {
                register
                    .WithValueField(0, 32, out rx_hdp[idx], FieldMode.Read, writeCallback: (_, value) =>
                    {
                        this.Log(LogLevel.Noisy, "STATE_RAM_RX{0}_HDP: {1:X}", idx, value);
                        rx_hdp[idx].Value = value;
                        if (value != 0)
                           RxDescriptorAdr[idx] = value - BASE_MESSAGEADR_L3;
                        else
                           RxDescriptorAdr[idx] = 0;
                    }, name: $"STATE_RAM_RX{idx}_HDP");
            }, stepInBytes: 0x04, resetValue: 0x00, name: "STATE_RAM_RXx_HDP");

            Registers.STATE_RAM_TX0_CP.DefineMany(dwordregisters, 8, (register, idx) =>
            {
                register
                    .WithValueField(0, 32, out tx_cp[idx], FieldMode.Read,name: $"STATE_RAM_TX{idx}_CP");
            }, stepInBytes: 0x04, resetValue: 0x00, name: "STATE_RAM_TXx_CP");

            Registers.STATE_RAM_RX0_CP.DefineMany(dwordregisters, 8, (register, idx) =>
            {
                register
                    .WithValueField(0, 32, out rx_cp[idx], FieldMode.Read, name: $"STATE_RAM_RX{idx}_CP");
            }, stepInBytes: 0x04, resetValue: 0x00, name: "STATE_RAM_RXx_CP");


            Registers.WR_C0_MISC_STAT.Define(dwordregisters, 0x00, "WR_C0_MISC_STAT")
                .WithValueField(0, 32, out wr_c0_misc_stat, FieldMode.Read | FieldMode.Write, name: "WR_C0_MISC_STAT");


            Registers.CPTS_CONTROL.Define(dwordregisters, 0x00, "CPTS_CONTROL")
                .WithValueField(0, 32, FieldMode.Read | FieldMode.Write, name: "reserved");

            Registers.CPTS_INT_ENABLE.Define(dwordregisters, 0x00, "CPTS_INT_ENABLE")
                .WithFlag(0, out ts_pend_enable, FieldMode.Read | FieldMode.Write, name: "reserved")
                .WithValueField(1, 31, FieldMode.Read | FieldMode.Write, name: "reserved");

            Registers.CPTS_INTSTAT_RAW.Define(dwordregisters, 0x00, "CPTS_INTSTAT_RAW")
                .WithFlag(0, FieldMode.Read | FieldMode.Write,
                        valueProviderCallback: _ =>
                        {
                            if (cptsHighQueue.Count > 0)
                                return true;
                            else
                                return false;
                        }, name: "TS_PEND_RAW")
                .WithValueField(1, 31, FieldMode.Read | FieldMode.Write, name: "reserved");


            Registers.CPTS_EVENT_HIGH.Define(dwordregisters, 0x00, "CPTS_EVENT_HIGH")
                .WithValueField(0, 32, FieldMode.Read,
                        valueProviderCallback: _ =>
                        {
                            return cptsHighQueue.Peek();
                        }, name: "CPTS_EVENT_HIGH");
            /*
                            .WithValueField(0, 16, out cpts_sequence_id, FieldMode.Read, name: "CPTS_SEQUENCE_ID")
                            .WithValueField(16, 4, out cpts_message_type, FieldMode.Read, name: "CPTS_MESSAGE_TYPE")
                            .WithValueField(20, 4, out cpts_event_type, FieldMode.Read, name: "CPTS_EVENT_TYPE")
                            .WithValueField(24, 5, out cpts_port_number, FieldMode.Read, name: "CPTS_PORT_NUMBER")
                            .WithValueField(29, 3, FieldMode.Read | FieldMode.Write, name: "reserved");
            */
            Registers.CPTS_EVENT_LOW.Define(dwordregisters, 0x00, "CPTS_EVENT_LOW")
                .WithValueField(0, 32, FieldMode.Read,
                        valueProviderCallback: _ =>
                        {
                            return cptsLowQueue.Peek();
                        }, name: "CPTS_EVENT_LOW");

            
            Registers.CPTS_EVENT_POP.Define(dwordregisters, 0x00, "CPTS_EVENT_POP")
                .WithFlag(0, FieldMode.Read | FieldMode.WriteOneToClear, writeCallback: (_, value) =>
                {
                    this.Log(LogLevel.Noisy, "EVENT POP {0}", value);
                    if (value)
                    {
                        cptsLowQueue.Dequeue();
                        cptsHighQueue.Dequeue();

                    }
                }, name: "CPTS_EVENT_POP")
                .WithValueField(1, 31, FieldMode.Read | FieldMode.Write, name: "reserved");

            Registers.CPDMA_EOI_VECTOR.Define(dwordregisters, 0x00, "CPDMA_EOI_VECTOR")
                .WithValueField(0, 5, FieldMode.Read | FieldMode.Write, name: "CPDMA_EOI_VECTOR")
                .WithValueField(5, 27, FieldMode.Read | FieldMode.Write, name: "reserved");

        }



        private enum Registers : long
        {
            MDIO_LINKINTRAW = BASE_MDIO_ADR + 0x10U,
            MDIO_USERACCESS0 = BASE_MDIO_ADR + 0x80U,
            MDIO_USERINTMASKSET = BASE_MDIO_ADR + 0x28U,
            MDIO_USERINTMASKCLR = BASE_MDIO_ADR + 0x2cU,
            MDIO_VER = BASE_MDIO_ADR + 0x0U,
            MDIO_USERPHYSEL0 = BASE_MDIO_ADR + 0x84U,
            MDIO_CONTROL = BASE_MDIO_ADR + 0x4U,
            MDIO_USERINTRAW = BASE_MDIO_ADR + 0x20U,
            MDIO_ALIVE = BASE_MDIO_ADR + 0x8U,
            MDIO_USERINTMASKED = BASE_MDIO_ADR + 0x24U,
            MDIO_USERACCESS1 = BASE_MDIO_ADR + 0x88U,
            MDIO_LINK = BASE_MDIO_ADR + 0xcU,
            MDIO_LINKINTMASKED = BASE_MDIO_ADR + 0x14U,
            MDIO_USERPHYSEL1 = BASE_MDIO_ADR + 0x8cU,

            CPDMA_DMA_INTSTAT_RAW = BASE_CPDMA_ADR + 0xb0U,
            CPDMA_RX0_PENDTHRESH = BASE_CPDMA_ADR + 0xc0U,
            CPDMA_TX_PRI6_RATE = BASE_CPDMA_ADR + 0x48U,
            CPDMA_DMA_INTMASK_SET = BASE_CPDMA_ADR + 0xb8U,
            CPDMA_IN_VECTOR = BASE_CPDMA_ADR + 0x90U,
            CPDMA_RX3_PENDTHRESH = BASE_CPDMA_ADR + 0xccU,
            CPDMA_TX_PRI3_RATE = BASE_CPDMA_ADR + 0x3cU,
            CPDMA_DMA_INTSTAT_MASKED = BASE_CPDMA_ADR + 0xb4U,
            CPDMA_TX_INTMASK_CLEAR = BASE_CPDMA_ADR + 0x8cU,
            CPDMA_RX_BUFFER_OFFSET = BASE_CPDMA_ADR + 0x28U,
            CPDMA_RX_INTMASK_SET = BASE_CPDMA_ADR + 0xa8U,
            CPDMA_TX_PRI4_RATE = BASE_CPDMA_ADR + 0x40U,
            CPDMA_RX6_FREEBUFFER = BASE_CPDMA_ADR + 0xf8U,
            CPDMA_RX2_FREEBUFFER = BASE_CPDMA_ADR + 0xe8U,
            CPDMA_SOFT_RESET = BASE_CPDMA_ADR + 0x1cU,
            CPDMA_TX_PRI0_RATE = BASE_CPDMA_ADR + 0x30U,
            CPDMA_TX_INTMASK_SET = BASE_CPDMA_ADR + 0x88U,
            CPDMA_RX4_PENDTHRESH = BASE_CPDMA_ADR + 0xd0U,
            CPDMA_EMCONTROL = BASE_CPDMA_ADR + 0x2cU,
            CPDMA_RX4_FREEBUFFER = BASE_CPDMA_ADR + 0xf0U,
            CPDMA_DMACONTROL = BASE_CPDMA_ADR + 0x20U,
            CPDMA_TX_TEARDOWN = BASE_CPDMA_ADR + 0x8U,
            CPDMA_RX3_FREEBUFFER = BASE_CPDMA_ADR + 0xecU,
            CPDMA_RX_IDVER = BASE_CPDMA_ADR + 0x10U,
            CPDMA_DMASTATUS = BASE_CPDMA_ADR + 0x24U,
            CPDMA_RX_INTSTAT_MASKED = BASE_CPDMA_ADR + 0xa4U,
            CPDMA_TX_IDVER = BASE_CPDMA_ADR + 0x0U,
            CPDMA_TX_INTSTAT_RAW = BASE_CPDMA_ADR + 0x80U,
            CPDMA_RX_TEARDOWN = BASE_CPDMA_ADR + 0x18U,
            CPDMA_RX7_PENDTHRESH = BASE_CPDMA_ADR + 0xdcU,
            CPDMA_EOI_VECTOR = BASE_CPDMA_ADR + 0x94U,
            CPDMA_RX_INTMASK_CLEAR = BASE_CPDMA_ADR + 0xacU,
            CPDMA_TX_PRI7_RATE = BASE_CPDMA_ADR + 0x4cU,
            CPDMA_DMA_INTMASK_CLEAR = BASE_CPDMA_ADR + 0xbcU,
            CPDMA_TX_PRI1_RATE = BASE_CPDMA_ADR + 0x34U,
            CPDMA_TX_PRI5_RATE = BASE_CPDMA_ADR + 0x44U,
            CPDMA_RX0_FREEBUFFER = BASE_CPDMA_ADR + 0xe0U,
            CPDMA_TX_PRI2_RATE = BASE_CPDMA_ADR + 0x38U,
            CPDMA_RX7_FREEBUFFER = BASE_CPDMA_ADR + 0xfcU,
            CPDMA_RX5_PENDTHRESH = BASE_CPDMA_ADR + 0xd4U,
            CPDMA_RX5_FREEBUFFER = BASE_CPDMA_ADR + 0xf4U,
            CPDMA_TX_CONTROL = BASE_CPDMA_ADR + 0x4U,
            CPDMA_TX_INTSTAT_MASKED = BASE_CPDMA_ADR + 0x84U,
            CPDMA_RX_INTSTAT_RAW = BASE_CPDMA_ADR + 0xa0U,
            CPDMA_RX1_FREEBUFFER = BASE_CPDMA_ADR + 0xe4U,
            CPDMA_RX6_PENDTHRESH = BASE_CPDMA_ADR + 0xd8U,
            CPDMA_RX_CONTROL = BASE_CPDMA_ADR + 0x14U,
            CPDMA_RX1_PENDTHRESH = BASE_CPDMA_ADR + 0xc4U,
            CPDMA_RX2_PENDTHRESH = BASE_CPDMA_ADR + 0xc8U,

            CPTS_EVENT_LOW = BASE_CPTS_ADR + 0x34U,
            CPTS_TS_PUSH = BASE_CPTS_ADR + 0xcU,
            CPTS_IDVER = BASE_CPTS_ADR + 0x0U,
            CPTS_TS_LOAD_VAL = BASE_CPTS_ADR + 0x10U,
            CPTS_INTSTAT_RAW = BASE_CPTS_ADR + 0x20U,
            CPTS_EVENT_HIGH = BASE_CPTS_ADR + 0x38U,
            CPTS_TS_LOAD_EN = BASE_CPTS_ADR + 0x14U,
            CPTS_INT_ENABLE = BASE_CPTS_ADR + 0x28U,
            CPTS_CONTROL = BASE_CPTS_ADR + 0x4U,
            CPTS_EVENT_POP = BASE_CPTS_ADR + 0x30U,
            CPTS_INTSTAT_MASKED = BASE_CPTS_ADR + 0x24U,

            SL1_RX_PAUSE = BASE_SL1_ADR + 0x18U,
            SL1_RX_MAXLEN = BASE_SL1_ADR + 0x10U,
            SL1_TX_GAP = BASE_SL1_ADR + 0x28U,
            SL1_TX_PAUSE = BASE_SL1_ADR + 0x1cU,
            SL1_IDVER = BASE_SL1_ADR + 0x0U,
            SL1_RX_PRI_MAP = BASE_SL1_ADR + 0x24U,
            SL1_EMCONTROL = BASE_SL1_ADR + 0x20U,
            SL1_MACSTATUS = BASE_SL1_ADR + 0x8U,
            SL1_SOFT_RESET = BASE_SL1_ADR + 0xcU,
            SL1_BOFFTEST = BASE_SL1_ADR + 0x14U,
            SL1_MACCONTROL = BASE_SL1_ADR + 0x4U,

            SL2_RX_PAUSE = BASE_SL2_ADR + 0x18U,
            SL2_RX_MAXLEN = BASE_SL2_ADR + 0x10U,
            SL2_TX_GAP = BASE_SL2_ADR + 0x28U,
            SL2_TX_PAUSE = BASE_SL2_ADR + 0x1cU,
            SL2_IDVER = BASE_SL2_ADR + 0x0U,
            SL2_RX_PRI_MAP = BASE_SL2_ADR + 0x24U,
            SL2_EMCONTROL = BASE_SL2_ADR + 0x20U,
            SL2_MACSTATUS = BASE_SL2_ADR + 0x8U,
            SL2_SOFT_RESET = BASE_SL2_ADR + 0xcU,
            SL2_BOFFTEST = BASE_SL2_ADR + 0x14U,
            SL2_MACCONTROL = BASE_SL2_ADR + 0x4U,

            CPSW_PTYPE = BASE_CPSW_ADR + 0x10U,
            CPSW_VLAN_LTYPE = BASE_CPSW_ADR + 0x28U,
            CPSW_STAT_PORT_EN = BASE_CPSW_ADR + 0xcU,
            CPSW_CONTROL = BASE_CPSW_ADR + 0x4U,
            CPSW_SOFT_IDLE = BASE_CPSW_ADR + 0x14U,
            CPSW_ID_VER = BASE_CPSW_ADR + 0x0U,
            CPSW_TX_START_WDS = BASE_CPSW_ADR + 0x20U,
            CPSW_SOFT_RESET = BASE_CPSW_ADR + 0x8U,
            CPSW_GAP_THRESH = BASE_CPSW_ADR + 0x1cU,
            CPSW_TS_LTYPE = BASE_CPSW_ADR + 0x2cU,
            CPSW_THRU_RATE = BASE_CPSW_ADR + 0x18U,
            CPSW_DLR_LTYPE = BASE_CPSW_ADR + 0x30U,
            CPSW_FLOW_CONTROL = BASE_CPSW_ADR + 0x24U,
            CPSW_EEE_PRESCALE = BASE_CPSW_ADR + 0x34U,

            WR_C1_RX_THRESH_STAT = BASE_WR_ADR + 0x50U,
            WR_C0_RX_STAT = BASE_WR_ADR + 0x44U,
            WR_C0_RX_THRESH_STAT = BASE_WR_ADR + 0x40U,
            WR_C1_MISC_EN = BASE_WR_ADR + 0x2cU,
            WR_C2_RX_THRESH_EN = BASE_WR_ADR + 0x30U,
            WR_C2_MISC_STAT = BASE_WR_ADR + 0x6cU,
            WR_C0_RX_EN = BASE_WR_ADR + 0x14U,
            WR_C2_RX_IMAX = BASE_WR_ADR + 0x80U,
            WR_C1_TX_STAT = BASE_WR_ADR + 0x58U,
            WR_C1_RX_STAT = BASE_WR_ADR + 0x54U,
            WR_C0_RX_THRESH_EN = BASE_WR_ADR + 0x10U,
            WR_C1_RX_EN = BASE_WR_ADR + 0x24U,
            WR_C1_TX_EN = BASE_WR_ADR + 0x28U,
            WR_C0_RX_IMAX = BASE_WR_ADR + 0x70U,
            WR_C2_RX_EN = BASE_WR_ADR + 0x34U,
            WR_C1_RX_IMAX = BASE_WR_ADR + 0x78U,
            WR_C0_MISC_EN = BASE_WR_ADR + 0x1cU,
            WR_CONTROL = BASE_WR_ADR + 0x8U,
            WR_SOFT_RESET = BASE_WR_ADR + 0x4U,
            WR_C0_TX_STAT = BASE_WR_ADR + 0x48U,
            WR_C2_RX_STAT = BASE_WR_ADR + 0x64U,
            WR_C2_MISC_EN = BASE_WR_ADR + 0x3cU,
            WR_IDVER = BASE_WR_ADR + 0x0U,
            WR_C2_TX_IMAX = BASE_WR_ADR + 0x84U,
            WR_C1_TX_IMAX = BASE_WR_ADR + 0x7cU,
            WR_RGMII_CTL = BASE_WR_ADR + 0x88U,
            WR_C1_RX_THRESH_EN = BASE_WR_ADR + 0x20U,
            WR_C1_MISC_STAT = BASE_WR_ADR + 0x5cU,
            WR_C0_TX_EN = BASE_WR_ADR + 0x18U,
            WR_C2_TX_EN = BASE_WR_ADR + 0x38U,
            WR_C0_MISC_STAT = BASE_WR_ADR + 0x4cU,
            WR_C2_RX_THRESH_STAT = BASE_WR_ADR + 0x60U,
            WR_C0_TX_IMAX = BASE_WR_ADR + 0x74U,
            WR_C2_TX_STAT = BASE_WR_ADR + 0x68U,
            WR_INT_CONTROL = BASE_WR_ADR + 0xcU,
            WR_STATUS = BASE_WR_ADR + 0x8cU,

            PORT_P2_RX_DSCP_PRI_MAP7 = BASE_PORT_ADR + 0x24cU,
            PORT_P2_TX_IN_CTL = BASE_PORT_ADR + 0x210U,
            PORT_P2_BLK_CNT = BASE_PORT_ADR + 0x20cU,
            PORT_P1_RX_DSCP_PRI_MAP0 = BASE_PORT_ADR + 0x130U,
            PORT_P1_SA_LO = BASE_PORT_ADR + 0x120U,
            PORT_P2_RX_DSCP_PRI_MAP1 = BASE_PORT_ADR + 0x234U,
            PORT_P0_RX_DSCP_PRI_MAP7 = BASE_PORT_ADR + 0x4cU,
            PORT_P0_MAX_BLKS = BASE_PORT_ADR + 0x8U,
            PORT_P1_CONTROL = BASE_PORT_ADR + 0x100U,
            PORT_P2_TX_PRI_MAP = BASE_PORT_ADR + 0x218U,
            PORT_P2_RX_DSCP_PRI_MAP2 = BASE_PORT_ADR + 0x238U,
            PORT_P2_SA_HI = BASE_PORT_ADR + 0x224U,
            PORT_P2_SA_LO = BASE_PORT_ADR + 0x220U,
            PORT_P0_CPDMA_TX_PRI_MAP = BASE_PORT_ADR + 0x1cU,
            PORT_P2_RX_DSCP_PRI_MAP4 = BASE_PORT_ADR + 0x240U,
            PORT_P0_BLK_CNT = BASE_PORT_ADR + 0xcU,
            PORT_P0_RX_DSCP_PRI_MAP0 = BASE_PORT_ADR + 0x30U,
            PORT_P0_TX_IN_CTL = BASE_PORT_ADR + 0x10U,
            PORT_P0_RX_DSCP_PRI_MAP5 = BASE_PORT_ADR + 0x44U,
            PORT_P2_SEND_PERCENT = BASE_PORT_ADR + 0x228U,
            PORT_P1_RX_DSCP_PRI_MAP7 = BASE_PORT_ADR + 0x14cU,
            PORT_P0_RX_DSCP_PRI_MAP1 = BASE_PORT_ADR + 0x34U,
            PORT_P1_RX_DSCP_PRI_MAP1 = BASE_PORT_ADR + 0x134U,
            PORT_P1_TX_IN_CTL = BASE_PORT_ADR + 0x110U,
            PORT_P0_RX_DSCP_PRI_MAP4 = BASE_PORT_ADR + 0x40U,
            PORT_P2_TS_SEQ_MTYPE = BASE_PORT_ADR + 0x21cU,
            PORT_P0_PORT_VLAN = BASE_PORT_ADR + 0x14U,
            PORT_P1_RX_DSCP_PRI_MAP4 = BASE_PORT_ADR + 0x140U,
            PORT_P2_RX_DSCP_PRI_MAP5 = BASE_PORT_ADR + 0x244U,
            PORT_P2_CONTROL = BASE_PORT_ADR + 0x200U,
            PORT_P1_RX_DSCP_PRI_MAP5 = BASE_PORT_ADR + 0x144U,
            PORT_P0_CPDMA_RX_CH_MAP = BASE_PORT_ADR + 0x20U,
            PORT_P1_RX_DSCP_PRI_MAP2 = BASE_PORT_ADR + 0x138U,
            PORT_P1_MAX_BLKS = BASE_PORT_ADR + 0x108U,
            PORT_P1_RX_DSCP_PRI_MAP3 = BASE_PORT_ADR + 0x13cU,
            PORT_P1_TX_PRI_MAP = BASE_PORT_ADR + 0x118U,
            PORT_P2_RX_DSCP_PRI_MAP3 = BASE_PORT_ADR + 0x23cU,
            PORT_P1_RX_DSCP_PRI_MAP6 = BASE_PORT_ADR + 0x148U,
            PORT_P1_SA_HI = BASE_PORT_ADR + 0x124U,
            PORT_P2_RX_DSCP_PRI_MAP6 = BASE_PORT_ADR + 0x248U,
            PORT_P2_MAX_BLKS = BASE_PORT_ADR + 0x208U,
            PORT_P0_RX_DSCP_PRI_MAP3 = BASE_PORT_ADR + 0x3cU,
            PORT_P0_TX_PRI_MAP = BASE_PORT_ADR + 0x18U,
            PORT_P1_TS_SEQ_MTYPE = BASE_PORT_ADR + 0x11cU,
            PORT_P0_RX_DSCP_PRI_MAP6 = BASE_PORT_ADR + 0x48U,
            PORT_P2_RX_DSCP_PRI_MAP0 = BASE_PORT_ADR + 0x230U,
            PORT_P0_CONTROL = BASE_PORT_ADR + 0x0U,
            PORT_P1_SEND_PERCENT = BASE_PORT_ADR + 0x128U,
            PORT_P1_BLK_CNT = BASE_PORT_ADR + 0x10cU,
            PORT_P0_RX_DSCP_PRI_MAP2 = BASE_PORT_ADR + 0x38U,
            PORT_P2_PORT_VLAN = BASE_PORT_ADR + 0x214U,
            PORT_P1_PORT_VLAN = BASE_PORT_ADR + 0x114U,

            ALE_TBLW2 = BASE_ALE_ADR + 0x34U,
            ALE_PORTCTL4 = BASE_ALE_ADR + 0x50U,
            ALE_TBLCTL = BASE_ALE_ADR + 0x20U,
            ALE_UNKNOWN_VLAN = BASE_ALE_ADR + 0x18U,
            ALE_PORTCTL1 = BASE_ALE_ADR + 0x44U,
            ALE_TBLW1 = BASE_ALE_ADR + 0x38U,
            ALE_IDVER = BASE_ALE_ADR + 0x0U,
            ALE_PORTCTL2 = BASE_ALE_ADR + 0x48U,
            ALE_PRESCALE = BASE_ALE_ADR + 0x10U,
            ALE_PORTCTL0 = BASE_ALE_ADR + 0x40U,
            ALE_CONTROL = BASE_ALE_ADR + 0x8U,
            ALE_TBLW0 = BASE_ALE_ADR + 0x3cU,
            ALE_PORTCTL5 = BASE_ALE_ADR + 0x54U,
            ALE_PORTCTL3 = BASE_ALE_ADR + 0x4cU,


            STATE_RAM_TX0_HDP = BASE_STATE_RAM + 0x00,
            STATE_RAM_TX1_HDP = BASE_STATE_RAM + 0x04,
            STATE_RAM_TX2_HDP = BASE_STATE_RAM + 0x08,
            STATE_RAM_TX3_HDP = BASE_STATE_RAM + 0x0C,
            STATE_RAM_TX4_HDP = BASE_STATE_RAM + 0x10,
            STATE_RAM_TX5_HDP = BASE_STATE_RAM + 0x14,
            STATE_RAM_TX6_HDP = BASE_STATE_RAM + 0x18,
            STATE_RAM_TX7_HDP = BASE_STATE_RAM + 0x1C,
            STATE_RAM_RX0_HDP = BASE_STATE_RAM + 0x20,
            STATE_RAM_RX1_HDP = BASE_STATE_RAM + 0x24,
            STATE_RAM_RX2_HDP = BASE_STATE_RAM + 0x28,
            STATE_RAM_RX3_HDP = BASE_STATE_RAM + 0x2C,
            STATE_RAM_RX4_HDP = BASE_STATE_RAM + 0x30,
            STATE_RAM_RX5_HDP = BASE_STATE_RAM + 0x34,
            STATE_RAM_RX6_HDP = BASE_STATE_RAM + 0x38,
            STATE_RAM_RX7_HDP = BASE_STATE_RAM + 0x3C,
            STATE_RAM_TX0_CP = BASE_STATE_RAM + 0x40,
            STATE_RAM_TX1_CP = BASE_STATE_RAM + 0x44,
            STATE_RAM_TX2_CP = BASE_STATE_RAM + 0x48,
            STATE_RAM_TX3_CP = BASE_STATE_RAM + 0x4C,
            STATE_RAM_TX4_CP = BASE_STATE_RAM + 0x50,
            STATE_RAM_TX5_CP = BASE_STATE_RAM + 0x54,
            STATE_RAM_TX6_CP = BASE_STATE_RAM + 0x58,
            STATE_RAM_TX7_CP = BASE_STATE_RAM + 0x5C,
            STATE_RAM_RX0_CP = BASE_STATE_RAM + 0x60,
            STATE_RAM_RX1_CP = BASE_STATE_RAM + 0x64,
            STATE_RAM_RX2_CP = BASE_STATE_RAM + 0x68,
            STATE_RAM_RX3_CP = BASE_STATE_RAM + 0x6C,
            STATE_RAM_RX4_CP = BASE_STATE_RAM + 0x70,
            STATE_RAM_RX5_CP = BASE_STATE_RAM + 0x74,
            STATE_RAM_RX6_CP = BASE_STATE_RAM + 0x78,
            STATE_RAM_RX7_CP = BASE_STATE_RAM + 0x7C,


            MESSAGE_BUFFER = 0x2000


        }
    }
    public class TxBufferDescriptor
    {
        private DoubleWordRegister word1, word2, word3, word4;
        public IValueRegisterField NextDescriptorPointer;
        public IValueRegisterField BufferPointer;
        public IValueRegisterField BufferLength;
        public IValueRegisterField BufferOffset;
        public IValueRegisterField PacketLength;
        public IValueRegisterField ToPort;
        public IFlagRegisterField ToPortEnable;
        public IFlagRegisterField PassCrc;
        public IFlagRegisterField TDownComplete;
        public IFlagRegisterField EOQ;
        public IFlagRegisterField Owner;
        public IFlagRegisterField EOP;
        public IFlagRegisterField SOP;
        uint Adress;
        ArrayMemory msgMemory;

        public TxBufferDescriptor(uint Adress, ArrayMemory messageMemory)
        {
            this.Adress = Adress;
            msgMemory = messageMemory;
            if (Adress < messageMemory.Size)
            {

                uint Val = msgMemory.ReadDoubleWord(Adress + 0);
                word1 = new DoubleWordRegister(null, Val)
                        .WithValueField(0, 32, out NextDescriptorPointer, FieldMode.Read | FieldMode.Write);

                Val = msgMemory.ReadDoubleWord(Adress + 4);
                word2 = new DoubleWordRegister(null, Val)
                        .WithValueField(0, 32, out BufferPointer, FieldMode.Read | FieldMode.Write);

                Val = msgMemory.ReadDoubleWord(Adress + 8);
                word3 = new DoubleWordRegister(null, Val)
                        .WithValueField(0, 16, out BufferLength, FieldMode.Read | FieldMode.Write)
                        .WithValueField(16, 16, out BufferOffset, FieldMode.Read | FieldMode.Write);

                Val = msgMemory.ReadDoubleWord(Adress + 12);
                word4 = new DoubleWordRegister(null, Val)
                        .WithValueField(0, 11, out PacketLength, FieldMode.Read | FieldMode.Write)
                        .WithValueField(16, 2, out ToPort, FieldMode.Read | FieldMode.Write)
                        .WithFlag(20, out ToPortEnable, FieldMode.Read | FieldMode.Write)
                        .WithFlag(26, out PassCrc, FieldMode.Read | FieldMode.Write)
                        .WithFlag(27, out TDownComplete, FieldMode.Read | FieldMode.Write)
                        .WithFlag(28, out EOQ, FieldMode.Read | FieldMode.Write)
                        .WithFlag(29, out Owner, FieldMode.Read | FieldMode.Write)
                        .WithFlag(30, out EOP, FieldMode.Read | FieldMode.Write)
                        .WithFlag(31, out SOP, FieldMode.Read | FieldMode.Write);
            }
        }

        public void Store()
        {
            msgMemory.WriteDoubleWord(Adress + 12, word4.Value);

        }
    }

    public class RxBufferDescriptor
    {
        private DoubleWordRegister word1, word2, word3, word4;
        public IValueRegisterField NextDescriptorPointer;
        public IValueRegisterField BufferPointer;
        public IValueRegisterField BufferLength;
        public IValueRegisterField BufferOffset;
        public IValueRegisterField PacketLength;
        public IValueRegisterField FromPort;
        public IFlagRegisterField  RxVlanEncap;
        public IValueRegisterField PktErr;
        public IFlagRegisterField PassCrc;
        public IFlagRegisterField TDownComplete;
        public IFlagRegisterField EOQ;
        public IFlagRegisterField Owner;
        public IFlagRegisterField EOP;
        public IFlagRegisterField SOP;
        public IFlagRegisterField Overrun;
        public IFlagRegisterField MacCtl;
        public IFlagRegisterField Short;
        public IFlagRegisterField Long;
        public uint Adress;
        ArrayMemory msgMemory;

        public RxBufferDescriptor(uint Adress, ArrayMemory messageMemory)
        {
            if (Adress < messageMemory.Size)
            {
                this.Adress = Adress;
                msgMemory = messageMemory;
                uint Val = msgMemory.ReadDoubleWord(Adress + 0);
                word1 = new DoubleWordRegister(null, Val)
                        .WithValueField(0, 32, out NextDescriptorPointer, FieldMode.Read | FieldMode.Write);

                Val = msgMemory.ReadDoubleWord(Adress + 4);
                word2 = new DoubleWordRegister(null, Val)
                        .WithValueField(0, 32, out BufferPointer, FieldMode.Read | FieldMode.Write);

                Val = msgMemory.ReadDoubleWord(Adress + 8);
                word3 = new DoubleWordRegister(null, Val)
                        .WithValueField(0, 11, out BufferLength, FieldMode.Read | FieldMode.Write)
                        .WithValueField(16, 11, out BufferOffset, FieldMode.Read | FieldMode.Write);

                Val = msgMemory.ReadDoubleWord(Adress + 12);
                word4 = new DoubleWordRegister(null, Val)
                        .WithValueField(0, 11, out PacketLength, FieldMode.Read | FieldMode.Write)
                        .WithValueField(16, 3, out FromPort, FieldMode.Read | FieldMode.Write)
                        .WithFlag(19, out RxVlanEncap, FieldMode.Read | FieldMode.Write)
                        .WithValueField(20, 2, out PktErr, FieldMode.Read | FieldMode.Write)
                        .WithFlag(22, out Overrun, FieldMode.Read | FieldMode.Write)
                        .WithFlag(23, out MacCtl, FieldMode.Read | FieldMode.Write)
                        .WithFlag(24, out Short, FieldMode.Read | FieldMode.Write)
                        .WithFlag(25, out Long, FieldMode.Read | FieldMode.Write)
                        .WithFlag(26, out PassCrc, FieldMode.Read | FieldMode.Write)
                        .WithFlag(27, out TDownComplete, FieldMode.Read | FieldMode.Write)
                        .WithFlag(28, out EOQ, FieldMode.Read | FieldMode.Write)
                        .WithFlag(29, out Owner, FieldMode.Read | FieldMode.Write)
                        .WithFlag(30, out EOP, FieldMode.Read | FieldMode.Write)
                        .WithFlag(31, out SOP, FieldMode.Read | FieldMode.Write);
            }


        }

        public void Store()
        {
            msgMemory.WriteDoubleWord(Adress + 8, word4.Value);
            msgMemory.WriteDoubleWord(Adress + 12, word4.Value);
        }
    }

}

