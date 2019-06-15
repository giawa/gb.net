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

        public Cartridge(string file)
        {
            using (BinaryReader reader = new BinaryReader(new FileStream(file, FileMode.Open)))
            {
                data = reader.ReadBytes((int)reader.BaseStream.Length);

                Type = (CartridgeType)data[0x147];
            }
        }

        private bool mbc1_4_32_mode, mbc1_enable_bank;
        private int mbc1_4000_bank, mbc1_a000_bank;
        private int mbc1_16_8_offset;

        public byte this[int a]
        {
            get
            {
                if (Type == CartridgeType.MBC1)
                {
                    if (a >= 0x4000)
                    {
                        if (mbc1_4_32_mode)
                        {
                            // TODO:  Implement RAM?
                            int bank = Math.Max(1, mbc1_4000_bank) - 1;
                            return data[0x4000 * bank + a];
                        }
                        else
                        {
                            int bank = Math.Max(1, mbc1_4000_bank + 0x20 * mbc1_a000_bank) - 1;
                            return data[0x4000 * bank + a];
                        }
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
                if (Type == CartridgeType.MBC1)
                {
                    if (a >= 0x6000 && a <= 0x7fff)
                        mbc1_4_32_mode = (value & 0x01) == 0x01;
                    else if (a >= 0x2000 && a <= 0x3fff)
                        mbc1_4000_bank = value & 0x1f;
                    else if (mbc1_4_32_mode)
                    {
                        // in 4/32 mode using an MBC1 memory model
                        if (a >= 0x4000 && a <= 0x5fff)
                            mbc1_a000_bank = value & 0x03;
                        else if (a >= 0x000 && a <= 0x1fff)
                            mbc1_enable_bank = (value & 0x0f) == 0x0a;
                    }
                    else
                    {
                        // in 16/8 mode using an MBC1 memory model
                        if (a >= 0x4000 && a <= 0x5fff)
                            mbc1_16_8_offset = (value << 6);
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
