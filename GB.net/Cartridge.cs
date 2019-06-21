using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GB
{
    class Cartridge
    {
        public enum CartridgeType : byte
        {
            RomOnly = 0,
            MBC1 = 1,
            MBC1_RAM = 2,
            MBC1_RAM_BATT = 3,
            MBC2 = 5,
            MBC2_BATT = 6,
            ROM_RAM = 8,
            ROM_RAM_BATT = 9,
            MMO1 = 0x0b,
            MMO1_SRAM = 0x0c,
            MMO1_SRAM_BATT = 0x0d,
            MBC3_TIMER_BATT = 0x0f,
            MBC3_TIMER_RAM_BATT = 0x10,
            MBC3 = 0x11,
            MBC3_RAM = 0x12,
            MBC3_RAM_BATT = 0x13,
            MBC5 = 0x19,
            MBC5_RAM = 0x1a,
            MBC5_RAM_BATT = 0x1b,
            MBC5_RUMBLE = 0x1c,
            MBC5_RUMBLE_SRAM = 0x1d,
            MBC5_RUMBLE_SRAM_BATT = 0x1e,
            POCKET_CAMERA = 0x1f,
            TAMA5 = 0xfd,
            HUC3 = 0xfe,
            JUC1 = 0xff
        }

        public CartridgeType Type { get; private set; }

        public byte[] data;

        public int RAMSize { get; private set; }

        public Cartridge(string file)
        {
            using (BinaryReader reader = new BinaryReader(new FileStream(file, FileMode.Open)))
            {
                data = reader.ReadBytes((int)reader.BaseStream.Length);

                Type = (CartridgeType)data[0x147];
                switch (data[0x149])
                {
                    case 0x00: RAMSize = 0; break;
                    case 0x01: RAMSize = 2048; break;
                    case 0x02: RAMSize = 8192; break;
                    case 0x03: RAMSize = 32768; break;
                    case 0x04: RAMSize = 16 * 8192; break;
                    case 0x05: RAMSize = 8 * 8192; break;
                }
            }

            externalRam = new byte[RAMSize];
            for (int i = 0; i < externalRam.Length; i++) externalRam[i] = 0xff;
        }

        private bool mbc1_4_32_mode, mbc1_enable_bank;
        private int mbc1_4000_bank, mbc1_bank2;

        private int mbc3_register0, mbc3_register1, mbc3_register2, mbc3_register3;

        public void ExportState(int version, BinaryWriter output)
        {
            output.Write(mbc1_4_32_mode);
            output.Write(mbc1_enable_bank);
            output.Write(mbc1_4000_bank);
            output.Write(mbc1_bank2);
            output.Write(mbc3_register0);
            output.Write(mbc3_register1);
            output.Write(mbc3_register2);
            output.Write(mbc3_register3);
            output.Write(RAMSize);
            output.Write(externalRam);
        }

        public void ImportState(int version, BinaryReader input)
        {
            mbc1_4_32_mode = input.ReadBoolean();
            mbc1_enable_bank = input.ReadBoolean();
            mbc1_4000_bank = input.ReadInt32();
            mbc1_bank2 = input.ReadInt32();
            mbc3_register0 = input.ReadInt32();
            mbc3_register1 = input.ReadInt32();
            mbc3_register2 = input.ReadInt32();
            mbc3_register3 = input.ReadInt32();
            RAMSize = input.ReadInt32();
            externalRam = input.ReadBytes(RAMSize);
        }

        public bool WriteProtected
        {
            get
            {
                if (Type == CartridgeType.MBC1 || Type == CartridgeType.MBC1_RAM || Type == CartridgeType.MBC1_RAM_BATT)
                {
                    return !mbc1_enable_bank;
                }
                else if (Type == CartridgeType.MBC3 || Type == CartridgeType.MBC3_RAM || Type == CartridgeType.MBC3_RAM_BATT || Type == CartridgeType.MBC3_TIMER_BATT || Type == CartridgeType.MBC3_TIMER_RAM_BATT)
                {
                    return (mbc3_register0 & 0x0f) != 0x0a;
                }
                else
                {
                    return false;
                }
            }
        }

        private byte[] externalRam;

        public byte ExternalRAM(int address)
        {
            if (RAMSize == 0) return (byte)255;
            else
            {
                if (Type == CartridgeType.MBC1 || Type == CartridgeType.MBC1_RAM || Type == CartridgeType.MBC1_RAM_BATT)
                {
                    if (mbc1_enable_bank)
                    {
                        int addr = (mbc1_4_32_mode ? mbc1_bank2 * 8192 : 0) + address;
                        return externalRam[addr % externalRam.Length];
                    }
                    else return 255;
                }
                else if (Type == CartridgeType.MBC3 || Type == CartridgeType.MBC3_RAM || Type == CartridgeType.MBC3_RAM_BATT || Type == CartridgeType.MBC3_TIMER_BATT || Type == CartridgeType.MBC3_TIMER_RAM_BATT)
                {
                    int addr = (mbc3_register2 * 8192) + address;
                    if (addr >= externalRam.Length) return 0xff;
                    return externalRam[addr];
                }
                else
                {
                    return externalRam[address % externalRam.Length];
                }
            }
        }

        public void SetExternalRAM(int address, byte value)
        {
            if (RAMSize == 0) return;
            else
            {
                if (Type == CartridgeType.MBC1 || Type == CartridgeType.MBC1_RAM || Type == CartridgeType.MBC1_RAM_BATT)
                {
                    // TODO:  Support banking of the RAM
                    if (mbc1_enable_bank)
                    {
                        int addr = (mbc1_4_32_mode ? mbc1_bank2 * 8192 : 0) + address;
                        externalRam[addr % externalRam.Length] = value;
                    }
                    else return;
                }
                else if (Type == CartridgeType.MBC3 || Type == CartridgeType.MBC3_RAM || Type == CartridgeType.MBC3_RAM_BATT || Type == CartridgeType.MBC3_TIMER_BATT || Type == CartridgeType.MBC3_TIMER_RAM_BATT)
                {
                    int addr = (mbc3_register2 * 8192) + address;
                    externalRam[addr] = value;
                }
                else
                {
                    externalRam[address % externalRam.Length] = value;
                }
            }
        }

        public byte this[int a]
        {
            get
            {
                if (Type == CartridgeType.MBC1 || Type == CartridgeType.MBC1_RAM || Type == CartridgeType.MBC1_RAM_BATT)
                {
                    if (a >= 0x4000)
                    {
                        int bank = Math.Max(1, mbc1_4000_bank | (mbc1_bank2 << 5));
                        int address = (0x4000 * (bank - 1) + a);
                        return data[address % data.Length];
                    }
                    else
                    {
                        if (mbc1_4_32_mode)
                        {
                            int bank = mbc1_bank2 << 5;
                            return data[(0x4000 * bank + a) % data.Length];
                        }
                        else
                        {
                            return data[a];
                        }
                    }
                }
                else if (Type == CartridgeType.MBC3 || Type == CartridgeType.MBC3_RAM || Type == CartridgeType.MBC3_RAM_BATT || Type == CartridgeType.MBC3_TIMER_BATT || Type == CartridgeType.MBC3_TIMER_RAM_BATT)
                {
                    if (a >= 0x4000)
                    {
                        int address = (0x4000 * (mbc3_register1 - 1) + a);
                        return data[address % data.Length];
                    }
                    else
                    {
                        return data[a];
                    }
                }
                else
                {
                    return data[a];
                }
            }
            set
            {
                if (Type == CartridgeType.MBC1 || Type == CartridgeType.MBC1_RAM || Type == CartridgeType.MBC1_RAM_BATT)
                {
                    if (a >= 0x6000 && a <= 0x7fff)
                    {
                        mbc1_4_32_mode = (value & 0x01) == 0x01;
                    }
                    else if (a >= 0x2000 && a <= 0x3fff)
                    {
                        mbc1_4000_bank = value & 0x1f;
                        if (mbc1_4000_bank == 0)
                            mbc1_4000_bank = 1;
                    }
                    else if (a >= 0x0000 && a <= 0x1fff)
                    {
                        mbc1_enable_bank = (value & 0x0f) == 0x0a;
                    }
                    else if (a >= 0x4000 && a <= 0x5fff)
                    {
                        mbc1_bank2 = value & 0x03;
                    }
                }
                else if (Type == CartridgeType.MBC3 || Type == CartridgeType.MBC3_RAM || Type == CartridgeType.MBC3_RAM_BATT || Type == CartridgeType.MBC3_TIMER_BATT || Type == CartridgeType.MBC3_TIMER_RAM_BATT)
                {
                    if (a >= 0x000 && a <= 0x1fff)
                    {
                        mbc3_register0 = value;
                    }
                    else if (a >= 0x2000 && a <= 0x3fff)
                    {
                        mbc3_register1 = Math.Max(1, value & 0x7f);
                    }
                    else if (a >= 0x4000 && a <= 0x5fff)
                    {
                        mbc3_register2 = value;
                    }
                    else if (a >= 0x6000 && a <= 0x7ffff)
                    {
                        mbc3_register3 = value;
                    }
                }
                else
                {
                    // ROM is read only!
                    //data[a] = value;
                }
            }
        }
    }
}
