using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{

//    [AllowedTranslations(AllowedTranslation.WordToDoubleWord | AllowedTranslation.ByteToDoubleWord)]
    public class DRA78x_C66 : IKnownSize, IDoubleWordPeripheral, IWordPeripheral, IBytePeripheral, IPeripheral
    {
        public long Size => 0x28;

        public DRA78x_C66(Machine machine, ulong alivecounteradr, uint aliveperiodms, ulong mailboxbaseadr, ulong mailboxnr, ulong protocolfileadr,
                          uint protocolversion, uint filesetversion)
        {
            this.machine = machine;
            dwordregisters = new DoubleWordRegisterCollection(this);
            DefineRegisters();
            mailbox_base_adr = mailboxbaseadr;
            mailbox_nr = mailboxnr;
            alive_counter_adr = alivecounteradr;
            alive_period_ms = aliveperiodms;
            protocol_file_adr = protocolfileadr;
            protocol_version = protocolversion;
            file_set_version = filesetversion;

            // Frequency / Limit  =  clock  [hz]
            // Limit = Frequency [Hz] / Clock [Hz]
            // Limit / Frequency = clock [s]
            // Limit = clock [s] * (Frequency [Hz] / divider)
            // Set Limit in ms
            AliveTimer = new LimitTimer(machine.ClockSource, 1000000, this, nameof(AliveTimer), alive_period_ms, Direction.Descending, false, WorkMode.Periodic,false,false,950);
            AliveTimer.LimitReached += AliveIncrement;
            AliveTimer.EventEnabled = true;
            AliveTimer.Enabled = false;

            Reset();

        }

        public byte ReadByte(long offset)
        {
            return (byte) ReadDoubleWord(offset);
        }

        public uint ReadDoubleWord(long offset)
        {
            return dwordregisters.Read(offset);
        }

        public ushort ReadWord(long offset)
        {
            return (ushort)ReadDoubleWord(offset);
        }

        public void Reset()
        {
            dwordregisters.Reset();
        }

        public void WriteByte(long offset, byte value)
        {
            WriteDoubleWord(offset, value);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            dwordregisters.Write(offset, value);
        }

        public void WriteWord(long offset, ushort value)
        {
            WriteDoubleWord(offset, value);
        }


        private readonly DoubleWordRegisterCollection dwordregisters;
        private Machine machine;
        private ulong mailbox_nr;
        private ulong mailbox_base_adr;
        private LimitTimer AliveTimer;
        private UInt32 AliveCounter = 1;
        private uint alive_period_ms;
        private ulong alive_counter_adr;
        private ulong protocol_file_adr;
        private uint protocol_version;
        private uint file_set_version;

        private void DefineRegisters()
        {
            Registers.RM_DSP_RSTCTRL.Define(dwordregisters, 0x01, "RM_DSP_RSTCTR")
                 .WithFlag(0, FieldMode.Read | FieldMode.Set,
                                  writeCallback: (_, value) =>
                                  {
                                      this.Log(LogLevel.Noisy, "RST DSP LRST {0}", value);
                                      if (value == false)
                                      {
                                          StartDsp();
                                      }
                                      else
                                      {
                                          StopDsp();
                                      }

                                  }, name: "RST_DSP_LRST")
                 .WithFlag(1, FieldMode.Read | FieldMode.Set,
                 writeCallback: (_, value) =>
                 {
//                     this.Log(LogLevel.Noisy, "RST DSP {0}", value);
                 }, name: "RST_DSP")
                 .WithValueField(2, 30, FieldMode.Read, name: "reserved");

            Registers.RM_DSP_RSTST.Define(dwordregisters, 0x03, "RM_DSP_RSTST")
                 .WithFlag(0, FieldMode.Read | FieldMode.Set, name: "RST_DSP_LRST")
                 .WithFlag(1, FieldMode.Read | FieldMode.Set, name: "RST_DSP")
                 .WithFlag(2, FieldMode.Read | FieldMode.Set, name: "RST_DSP_EMU")
                 .WithFlag(3, FieldMode.Read | FieldMode.Set, name: "RST_DSP_EMU_REQ")
                 .WithValueField(4, 28, FieldMode.Read, name: "reserved");
        }


        public void SendVersionFromDsp()
        {
            ulong mailbox_offset_adr = 0x40 + 4 * mailbox_nr;
            ulong adr = mailbox_base_adr + mailbox_offset_adr;
            UInt32 filenumber = 0x01;
            UInt32 message = (filenumber << 0x5) | 0x05;

            this.Log(LogLevel.Noisy, "Send Version and ProtocolInfo from DSP");
            machine.SystemBus.WriteDoubleWord(adr, message);
        }

        public void SendVersionAndStatusFromDsp()
        {
            ulong mailbox_offset_adr = 0x40 + 4 * mailbox_nr;
            ulong adr = mailbox_base_adr + mailbox_offset_adr;
            UInt32 filenumber = 0x01 | 0x04;
            UInt32 message = (filenumber << 0x5) | 0x05;

            this.Log(LogLevel.Noisy, "Send Version and Status from DSP");
            machine.SystemBus.WriteDoubleWord(adr, message);
        }

        private void StartDsp()
        {
            // Write Protocol Version to Filesystem
            machine.SystemBus.WriteDoubleWord(protocol_file_adr, protocol_version);
            machine.SystemBus.WriteDoubleWord(protocol_file_adr + 0x04, file_set_version);
            AliveTimer.Enabled = true;
            AliveCounter = 1;
            SendVersionFromDsp();
        }

        private void AliveIncrement ()
        {
            machine.SystemBus.WriteDoubleWord(alive_counter_adr, AliveCounter++);
        }
        private void WriteStatustoAdress(ulong fileadr)
        {
            UInt32 value = 1;

            ///	\brief Global processor load
            machine.SystemBus.WriteDoubleWord(fileadr, 0x00000001);
            fileadr += 4;
            value += 1;
            ///	\brief Hwi processor load
            machine.SystemBus.WriteDoubleWord(fileadr, 0x00000002);
            fileadr += 4;
            value += 1;
            ///	\brief Swi rocessor load
            machine.SystemBus.WriteDoubleWord(fileadr, 0x00000003);
            fileadr += 4;
            value += 1;
            ///	\brief Task processor load
            for (int i = 0; i < 7; i++)
            {
                machine.SystemBus.WriteDoubleWord(fileadr, value);
                fileadr += 4;
                value += 1;
            }
            ///	\brief Min. number of free bytes in the stack
            for (int i = 0; i < 7; i++)
            {
                machine.SystemBus.WriteDoubleWord(fileadr, value);
                fileadr += 4;
                value += 1;
            }
            /// \brief The minimum time [ms] left of each task until the supervisor report period would be exceeded (worst case)
            for (int i = 0; i < 7; i++)
            {
                machine.SystemBus.WriteDoubleWord(fileadr, value);
                fileadr += 2;
                value += 1;
            }
        }

        private void SendStatus ()
        { 
            ulong mailbox_offset_adr = 0x40 + 4 * mailbox_nr;
            ulong adr = mailbox_base_adr + mailbox_offset_adr;
            UInt32 filenumber = 0x04;
            UInt32 message = (filenumber << 0x5) | 0x05;

            this.Log(LogLevel.Noisy, "Send Status from DSP");
            machine.SystemBus.WriteDoubleWord(adr, message);
        }



        private void StopDsp()
        {
            AliveTimer.Enabled = false;
        }

        private enum Registers : long
        {
            RM_DSP_RSTCTRL = 0x10,
            RM_DSP_RSTST = 0x14,
        }

    }
}
