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
        private int mbc1_4000_bank, mbc1_a000_bank;
        private int mbc1_16_8_offset;

        public bool WriteProtected
        {
            get { return !mbc1_enable_bank; }
        }

        private byte[] externalRam;

        public byte ExternalRAM(int address)
        {
            if (RAMSize == 0) return (byte)255;
            else return externalRam[address % externalRam.Length];
        }

        public void SetExternalRAM(int address, byte value)
        {
            // TODO:  Support banking of the RAM
            if (RAMSize == 0) return;
            else externalRam[address % externalRam.Length] = value;
        }

        public byte this[int a]
        {
            get
            {
                if (Type == CartridgeType.MBC1 || Type == CartridgeType.MBC1_RAM || Type == CartridgeType.MBC1_RAM_BATT)
                {
                    if (a >= 0x4000)
                    {
                        int bank = Math.Max(1, mbc1_4000_bank | mbc1_16_8_offset);
                        if (bank == 0x20 || bank == 0x40 || bank == 0x60)
                            bank = 1;
                        int address = (0x4000 * (bank - 1) + a);
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
                        if (mbc1_4_32_mode) mbc1_16_8_offset = 0;
                    }
                    else if (a >= 0x2000 && a <= 0x3fff)
                    {
                        mbc1_4000_bank = value & 0x1f;
                    }
                    else if (a >= 0x0000 && a <= 0x1fff)
                    {
                        mbc1_enable_bank = (value & 0x0f) == 0x0a;
                    }
                    else if (a >= 0x4000 && a <= 0x5fff)
                    {
                        if (mbc1_4_32_mode)
                        {
                            mbc1_a000_bank = value & 0x03;
                        }
                        else
                        {
                            mbc1_16_8_offset = ((value & 0x03) << 6);
                        }
                    }
                }
                else
                {
                    data[a] = value;
                }
            }
        }
    }
}
