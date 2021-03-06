﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GB
{
    class Memory
    {
        private byte[] bootstrap = new byte[] { 0x31, 0xFE, 0xFF, 0xAF, 0x21, 0xFF, 0x9F, 0x32, 0xCB, 0x7C, 0x20, 0xFB, 0x21, 0x26, 0xFF, 0x0E, 0x11, 0x3E, 0x80, 0x32, 0xE2, 0x0C, 0x3E, 0xF3, 0xE2, 0x32, 0x3E, 0x77, 0x77, 0x3E, 0xFC, 0xE0, 0x47, 0x11, 0x04, 0x01, 0x21, 0x10, 0x80, 0x1A, 0xCD, 0x95, 0x00, 0xCD, 0x96, 0x00, 0x13, 0x7B, 0xFE, 0x34, 0x20, 0xF3, 0x11, 0xD8, 0x00, 0x06, 0x08, 0x1A, 0x13, 0x22, 0x23, 0x05, 0x20, 0xF9, 0x3E, 0x19, 0xEA, 0x10, 0x99, 0x21, 0x2F, 0x99, 0x0E, 0x0C, 0x3D, 0x28, 0x08, 0x32, 0x0D, 0x20, 0xF9, 0x2E, 0x0F, 0x18, 0xF3, 0x67, 0x3E, 0x64, 0x57, 0xE0, 0x42, 0x3E, 0x91, 0xE0, 0x40, 0x04, 0x1E, 0x02, 0x0E, 0x0C, 0xF0, 0x44, 0xFE, 0x90, 0x20, 0xFA, 0x0D, 0x20, 0xF7, 0x1D, 0x20, 0xF2, 0x0E, 0x13, 0x24, 0x7C, 0x1E, 0x83, 0xFE, 0x62, 0x28, 0x06, 0x1E, 0xC1, 0xFE, 0x64, 0x20, 0x06, 0x7B, 0xE2, 0x0C, 0x3E, 0x87, 0xE2, 0xF0, 0x42, 0x90, 0xE0, 0x42, 0x15, 0x20, 0xD2, 0x05, 0x20, 0x4F, 0x16, 0x20, 0x18, 0xCB, 0x4F, 0x06, 0x04, 0xC5, 0xCB, 0x11, 0x17, 0xC1, 0xCB, 0x11, 0x17, 0x05, 0x20, 0xF5, 0x22, 0x23, 0x22, 0x23, 0xC9, 0xCE, 0xED, 0x66, 0x66, 0xCC, 0x0D, 0x00, 0x0B, 0x03, 0x73, 0x00, 0x83, 0x00, 0x0C, 0x00, 0x0D, 0x00, 0x08, 0x11, 0x1F, 0x88, 0x89, 0x00, 0x0E, 0xDC, 0xCC, 0x6E, 0xE6, 0xDD, 0xDD, 0xD9, 0x99, 0xBB, 0xBB, 0x67, 0x63, 0x6E, 0x0E, 0xEC, 0xCC, 0xDD, 0xDC, 0x99, 0x9F, 0xBB, 0xB9, 0x33, 0x3E, 0x3C, 0x42, 0xB9, 0xA5, 0xB9, 0xA5, 0x42, 0x3C, 0x21, 0x04, 0x01, 0x11, 0xA8, 0x00, 0x1A, 0x13, 0xBE, 0x20, 0xFE, 0x23, 0x7D, 0xFE, 0x34, 0x20, 0xF5, 0x06, 0x19, 0x78, 0x86, 0x23, 0x05, 0x20, 0xFB, 0x86, 0x20, 0xFE, 0x3E, 0x01, 0xE0, 0x50 };
        private bool firstBoot = true;

        public Timer Timer { get; set; }

        public LCD LCD { get; set; }

        public Sound Sound { get; set; }

        [Flags]
        public enum JoyPadButton : byte
        {
            Right = 1,
            Left = 2,
            Up = 4,
            Down = 8,
            A = 16,
            B = 32,
            Select = 64,
            Start = 128
        }

        public JoyPadButton JoyPad { get; set; }

        public byte GetJoyPad()
        {
            switch (specialPurpose[0x100] & 0x30)
            {
                case 0x00:
                    return (byte)((((byte)JoyPad & 0xf0) >> 4) | ((byte)~JoyPad & 0x0f));
                case 0x10:
                    return (byte)((((byte)~JoyPad & 0xf0) >> 4) | specialPurpose[0x100]);
                case 0x20:
                    return (byte)(((byte)~JoyPad & 0x0f) | specialPurpose[0x100]);
                default:
                    // neither P14 nor P15 is pulled low, so P10-P13 must all be high
                    return 0x0f;
            }
        }

        /// <summary>
        /// When the DIV register is written to it clears the internal counter
        /// used by the timer.
        /// The timer counters increment when certain bits transition, so we also
        /// need to check for those here.
        /// </summary>
        private void SetDIV(byte value)
        {
            var oldCounter = TimerCounter;
            TimerCounter = (value << 6);
            if ((specialPurpose[0x107] & 0x04) == 0x04)
            {
                switch (specialPurpose[0x107] & 0x03)
                {
                    case 0x00:
                        if (((oldCounter >> 7) & 0x01) == 0x01 && ((TimerCounter >> 7) & 0x01) == 0x00)
                        {
                            Timer.TickTIMA();
                        }
                        break;
                    case 0x01:
                        if (((oldCounter >> 1) & 0x01) == 0x01 && ((TimerCounter >> 1) & 0x01) == 0x00)
                        {
                            Timer.TickTIMA();
                        }
                        break;
                    case 0x02:
                        if (((oldCounter >> 3) & 0x01) == 0x01 && ((TimerCounter >> 3) & 0x01) == 0x00)
                        {
                            Timer.TickTIMA();
                        }
                        break;
                    case 0x03:
                        if (((oldCounter >> 5) & 0x01) == 0x01 && ((TimerCounter >> 5) & 0x01) == 0x00)
                        {
                            Timer.TickTIMA();
                        }
                        break;
                }
            }
            specialPurpose[0x104] = value;
        }

        public int TimerCounter = 0;

        public byte this[int a]
        {
            get
            {
                if (dmaCtr == 161 || dmaCtr == -1)
                {
                    if (a < 256 && firstBoot) return bootstrap[a];
                    else if (a < 32768) return Cartridge[a];
                    else if (a < 0xA000)
                    {
                        if (LCD.CurrentMode == LCD.LCDMode.Mode3 && SpecialPurpose[0x104] >= 0x80)
                            return 255;
                        else return videoRam[a - 0x8000];
                    }
                    else if (a < 0xC000) return Cartridge.ExternalRAM(a - 0xA000);
                    else if (a < 0xFE00) return internalRam[(a - 0xC000) % 8192];
                    else if (a == 0xff00) return (byte)(GetJoyPad() | 0b11000000);          // P1
                    else if (a == 0xff04) return (byte)(TimerCounter >> 6);
                    else if (a == 0xff26)
                        return specialPurpose[0x126];
                    else return specialPurpose[a & 511];
                }
                else
                {
                    if (a < 0xFE00 && a >= 0xC000) return internalRam[(a - 0xC000) % 8192];
                    else if (a < 0xff00) return 255;
                    else return specialPurpose[a & 511];
                }
            }
            set
            {
                if (a >= 0xff00 && a < 0xff80)
                {
                    if (a == 0xff46)
                    {
                        dmaCtr = -2;
                        specialPurpose[a & 511] = value;
                        Tick1MHz();
                        return;
                    }
                    else if (a == 0xff44)
                    {
                        specialPurpose[a & 511] = 0;
                        return;
                    }
                    else if (a == 0xff50 && value == 1)
                    {
                        firstBoot = false;
                        return;
                    }
                    else if (a == 0xff04)
                    {
                        SetDIV(value);
                        return;
                    }
                    else if (a == 0xff05)
                    {
                        if (!timaDelayed) specialPurpose[0x105] = value;
                        return;
                    }
                    else if (a == 0xff14)
                    {
                        specialPurpose[0x114] = value;
                        if ((value & 0x80) == 0x80) Sound.InitSound1(this);
                        return;
                    }
                    else if (a == 0xff17)
                    {
                        specialPurpose[0x117] = value;
                        Console.WriteLine("0xff17: 0x" + value.ToString("X"));
                        //if ((value & 0x80) == 0x80) Sound.InitSound2(this);
                        return;
                    }
                    else if (a == 0xff19)
                    {
                        specialPurpose[0x119] = value;
                        Console.WriteLine("0xff19: 0x" + value.ToString("X"));
                        if ((value & 0x80) == 0x80) Sound.InitSound2(this);
                        return;
                    }
                    else if (a == 0xff23)
                    {
                        specialPurpose[0x123] = value;
                        if ((value & 0x80) == 0x80) Sound.InitSound4(this);
                        return;
                    }
                    else if (a == 0xff24)
                    {
                        specialPurpose[0x124] = value;
                        //Console.WriteLine(value);
                        return;
                    }
                    else if (a == 0xff10) return;
                    else if (a == 0xff1A) return;   // NR30
                    else if (a == 0xff1C) return;   // NR32
                    else if (a == 0xff20) return;   // NR41
                    else if (a == 0xff23) return;   // NR44
                    else if (a == 0xff26) value |= 0x70;    // NR52
                    else if (a == 0xff40) value |= 0x01;    //return (byte)(specialPurpose[0x140] | 0x01);
                    else if (a == 0xff41) value |= 0x80;    //return (byte)(0x80 | (specialPurpose[0x141] & 0x7f)); // STAT
                    else if (a == 0xff03 || a == 0xff08 || (a >= 0xff09 && a <= 0xff0e)) return;    // unmapped
                    else if (a == 0xff15 || a == 0xff1f || (a >= 0xff27 && a <= 0xff29)) return;    // unmapped
                    else if (a >= 0xff4c && a <= 0xff7f) return;    // unmapped
                    else if (a == 0xff0f) value |= 0xE0;
                    else if (a == 0xff07) value |= 0xf8;
                    else if (a == 0xff02) value |= 0b01111110;
                }

                if (dmaCtr == 161 || dmaCtr == -1)
                {
                    if (a < 32768) Cartridge[a] = value;
                    else if (a < 0xA000)
                    {
                        if (LCD.CurrentMode != LCD.LCDMode.Mode3 || SpecialPurpose[0x104] < 0x80) videoRam[a - 0x8000] = value;
                        //else Console.WriteLine("stop");
                    }
                    else if (a < 0xC000)
                    {
                        if (!Cartridge.WriteProtected)
                            Cartridge.SetExternalRAM(a - 0xA000, value);
                    }
                    else if (a < 0xFE00) internalRam[(a - 0xC000) % 8192] = value;
                    else specialPurpose[a & 511] = value;
                }
                else
                {
                    if (a < 0xff00) return;
                    else specialPurpose[a & 511] = value;
                }
            }
        }

        public void SetFF44(byte value)
        {
            specialPurpose[0x144] = value;
        }

        public byte[] VideoMemory { get { return videoRam; } }

        public byte[] SpecialPurpose { get { return specialPurpose; } }

        //public byte[] Cartridge { get; set; }
        public Cartridge Cartridge { get; set; }

        private byte[] videoRam = new byte[8192];
        private byte[] internalRam = new byte[8192];
        private byte[] specialPurpose = new byte[512];

        public void ExportState(int version, BinaryWriter output)
        {
            output.Write(TimerCounter);
            output.Write(firstBoot);
            output.Write(reloadTima);
            output.Write(timaDelayed);
            output.Write(dmaCtr);
            output.Write(videoRam);
            output.Write(internalRam);
            output.Write(specialPurpose);
        }

        public void ImportState(int version, BinaryReader input)
        {
            TimerCounter = input.ReadInt32();
            firstBoot = input.ReadBoolean();
            reloadTima = input.ReadBoolean();
            timaDelayed = input.ReadBoolean();
            dmaCtr = input.ReadInt32();
            videoRam = input.ReadBytes(8192);
            internalRam = input.ReadBytes(8192);
            specialPurpose = input.ReadBytes(512);
        }

        public void Reset()
        {
            firstBoot = true;
            Cartridge = null;

            Random generator = new Random(Environment.TickCount);
            generator.NextBytes(videoRam);

            specialPurpose[0x102] = 0b01111110;
            specialPurpose[0x110] = 0x80;
            specialPurpose[0x11A] = 0x7F;
            specialPurpose[0x11C] = 0x9F;
            specialPurpose[0x120] = 0b11000000;
            specialPurpose[0x123] = 0x3f;
            specialPurpose[0x126] = 0x70;
            specialPurpose[0x140] = 0x01;
            specialPurpose[0x141] = 0x80;
            specialPurpose[0x103] = 0xff;
            specialPurpose[0x108] = 0xff;
            for (int i = 0x109; i <= 0x10e; i++) specialPurpose[i] = 0xff;
            specialPurpose[0x115] = 0xff;
            specialPurpose[0x11f] = 0xff;
            for (int i = 0x127; i <= 0x129; i++) specialPurpose[i] = 0xff;
            for (int i = 0x14c; i <= 0x17f; i++) specialPurpose[i] = 0xff;
        }

        public Memory()
        {
            Reset();
        }

        private bool reloadTima = false;
        private bool timaDelayed = false;

        public void ReloadTIMA()
        {
            reloadTima = true;
        }

        // A DMA transfer moves 160 bytes of memory up to OAM memory.
        // The DMA takes 1 cycle to start, which is why it is initialized to '-2'.
        // The -2 value comes because the RAM ticks after the CPU, whereas in real
        // life it would tick _with_ the CPU.  So we need to add one extra cycle delay
        // to be correct.
        // The DMA also takes 1 clock cycle to wrap up, which is why we run until 161.
        // During the DMA (from 0 to 161) the RAM returns 0xff for all reads (except high RAM).
        // The DMA start, restart and timing have been verified with mooneye test ROMs.
        private int dmaCtr = 161;

        public void Tick1MHz()
        {
            // Perform DMA
            if (dmaCtr < 161)
            {
                if (dmaCtr >= 0 && dmaCtr < 160)
                {
                    byte ff46 = specialPurpose[0x0146];
                    int address = 0x100 * ff46 + dmaCtr;
                    byte memory = (address < 32768 ? Cartridge[address] : (address < 0xa000 ? videoRam[address - 0x8000] : (address < 0xc000 ? Cartridge.ExternalRAM(address - 0xA000) : internalRam[(address - 0xC000) % 8192])));
                    specialPurpose[dmaCtr] = memory;
                }
                dmaCtr++;
            }

            // Reload the TIMA register with TMA
            if (reloadTima)
            {
                specialPurpose[0x105] = specialPurpose[0x0106];
                reloadTima = false;
                timaDelayed = true;
            }
            else timaDelayed = false;
        }
    }
}
