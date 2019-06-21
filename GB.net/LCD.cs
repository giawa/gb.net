using System;
using System.Drawing;
using System.IO;

namespace GB
{
    class LCD
    {
        private Memory _ram;

        public LCD(Memory ram)
        {
            _ram = ram;

            for (int i = 0; i < backgroundTexture.Length; i++)
                backgroundTexture[i] = 255;

            // default gameboy colours (close enough ...)
            SetPalette(new uint[] { 0xFFDDFDEA, 0xFF93D0AA, 0xFF75894F, 0xFF443216 });
        }

        private LCDMode lcdMode = LCDMode.Mode2;
        private int clkCtr;
        private byte lineCtr;

        public LCDMode CurrentMode { get { return lcdMode; } }

        public bool VBlankInterrupt { get; set; }

        public bool StatInterrupt { get; set; }

        public enum LCDMode : byte
        {
            HBlank = 0,
            VBlank = 1,
            Mode2 = 2,
            Mode3 = 3
        }

        public bool Tick1MHz()
        {
            byte ramff40 = _ram.SpecialPurpose[0x140];//_ram[0xff40];
            //if ((ramff40 & 0x80) == 0) return false;
            bool displayActive = (ramff40 & 0x80) == 0x80;

            /*bool frameComplete = Tick4mhz(displayActive);
            if (Tick4mhz(displayActive)) frameComplete = true;
            if (Tick4mhz(displayActive)) frameComplete = true;
            if (Tick4mhz(displayActive)) frameComplete = true;*/
            bool frameComplete = Tick4mhz(displayActive, 4);

            // update the STAT flags with the current mode and LY=LYC
            byte ramff41 = _ram.SpecialPurpose[0x141];//_ram[0xff41];
            ramff41 &= 0b11111100;
            ramff41 |= (byte)lcdMode;
            _ram[0xff41] = ramff41;

            return frameComplete;
        }

        public uint[] backgroundTexture = new uint[160 * 144];

        private uint[] activePalette = new uint[4];

        private void SetPalette(uint[] palette)
        {
            if (palette.Length != 4) return;
            for (int i = 0; i < 4; i++)
            {
                activePalette[i] = palette[i];//BitConverter.GetBytes(palette[i]);
            }
        }

        public void DumpTiles(int bgTileData = 0x8000)
        {
            bgTileData -= 0x8000;

            for (int i = 0; i < 256; i++)
            {
                Bitmap temp = new Bitmap(8, 8);

                // what a weird way to store pixel data ... each pixel spans 2 bytes
                for (int j = 0; j < 8; j++)
                {
                    byte b1 = _ram.VideoMemory[bgTileData + i * 16 + j * 2];
                    byte b2 = _ram.VideoMemory[bgTileData + i * 16 + j * 2 + 1];

                    for (int k = 7; k >= 0; k--)
                    {
                        int pixel = (((b2 >> k) & 0x01) << 1) | ((b1 >> k) & 0x01);
                        Color c = (pixel == 0 ? Color.White :
                            (pixel == 1 ? Color.Gray :
                            (pixel == 2 ? Color.DarkGray : Color.Black)));

                        temp.SetPixel(7 - k, j, c);
                    }
                }

                temp.Save($"tiles/tile{i}.png", System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        private byte[] fullBackground = new byte[256 * 256 * 4];

        /*public byte[] DumpBackground()
        {
            var ff40 = _ram[0xff40];
            int windowTileMap = (ff40 & 0x40) == 0x40 ? 0x9c00 : 0x9800;
            int bgTileData = (ff40 & 0x10) == 0x10 ? 0x8000 : 0x8800;
            int bgTileMap = (ff40 & 0x08) == 0x08 ? 0x9c00 : 0x9800;
            bgTileData -= 0x8000;
            bgTileMap -= 0x8000;

            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    int tile = _ram.VideoMemory[bgTileMap + x + y * 32];

                    // what a weird way to store pixel data ... each pixel spans 2 bytes
                    for (int j = 0; j < 8; j++)
                    {
                        int p = x * 4 * 8 + (y * 8 + j) * 256 * 4;

                        byte b1 = _ram.VideoMemory[bgTileData + tile * 16 + j * 2];
                        byte b2 = _ram.VideoMemory[bgTileData + tile * 16 + j * 2 + 1];

                        for (int k = 7; k >= 0; k--)
                        {
                            int pixel = (((b2 >> k) & 0x01) << 1) | ((b1 >> k) & 0x01);

                            Array.Copy(activePalette[pixel], 0, fullBackground, p, 4);
                            p += 4;
                        }
                    }
                }
            }

            return fullBackground;
        }*/

        /// <summary>
        /// The window can be enabled/disabled as part of a scanline interrupt,
        /// but if re-enabled it will carry on where it left off.  For this reason
        /// we need to track the y position for every line it is enabled.
        /// </summary>
        private int windowY;

        private void DrawLine(int y)
        {
            var ff40 = _ram[0xff40];
            int windowTileMap = (ff40 & 0x40) == 0x40 ? 0x9c00 : 0x9800;
            int bgTileData = (ff40 & 0x10) == 0x10 ? 0x8000 : 0x8800;
            int bgTileMap = (ff40 & 0x08) == 0x08 ? 0x9c00 : 0x9800;
            windowTileMap -= 0x8000;
            bgTileData -= 0x8000;
            bgTileMap -= 0x8000;

            byte palettebg = _ram[0xff47];
            byte palette0 = _ram[0xff48];
            byte palette1 = _ram[0xff49];
            for (int i = 0; i < 4; i++)
            {
                palettebglookup[i] = (palettebg >> (2 * i)) & 0x03;
                palette0lookup[i] = (palette0 >> (2 * i)) & 0x03;
                palette1lookup[i] = (palette1 >> (2 * i)) & 0x03;
            }

            for (int i = 0; i < 160; i++) lineData[i] = -1;

            Array.Clear(backgroundTexture, y * 160, 160);

            int bgstartx = 0, bgendx = 160;

            // this is the window, drawn on top of the background
            if ((ff40 & 0x20) == 0x20)
            {
                int wy = _ram[0xff4a];

                if (y >= wy)
                {
                    int wx = _ram[0xff4b];
                    wx = Math.Max(0, wx - 7);

                    if (wx >= 0 && wx <= 166)
                    {
                        for (int x = wx; x < 160;)
                        {
                            int tx = (x - wx) & 255;
                            int ty = (windowY) & 255;

                            int tilex = tx / 8;
                            int tiley = ty / 8;

                            int tile = _ram.VideoMemory[windowTileMap + tilex + tiley * 32];

                            if (bgTileData == 0x800)
                            {
                                if (tile >= 0x80) tile -= 128;
                                else tile += 128;
                            }

                            int j = ty % 8;
                            int k = 7 - (tx % 8);

                            int b1 = _ram.VideoMemory[bgTileData + tile * 16 + j * 2];
                            int b2 = _ram.VideoMemory[bgTileData + tile * 16 + j * 2 + 1];

                            if (k == 7 && (x + 8) < 160)
                            {
                                for (; k >= 0; k--)
                                {
                                    int pixel = (((b2 >> k) & 0x01) << 1) | ((b1 >> k) & 0x01);
                                    pixel = palettebglookup[pixel];
                                    lineData[x] = (pixel << 24) | 0xffff;

                                    x++;
                                }
                            }
                            else
                            {
                                int pixel = (((b2 >> k) & 0x01) << 1) | ((b1 >> k) & 0x01);
                                pixel = palettebglookup[pixel];
                                lineData[x] = (pixel << 24) | 0xffff;

                                x++;
                            }
                        }

                        windowY++;
                    }

                    bgendx = wx - 1;
                }
            }

            // draw the background first
            int scx = _ram[0xff43];
            int scy = _ram[0xff42];

            // this is going to be slow!
            for (int x = bgstartx; x < bgendx;)
            {
                int tx = (x + scx) & 255;
                int ty = (y + scy) & 255;

                int tilex = tx / 8;
                int tiley = ty / 8;

                int tile = _ram.VideoMemory[bgTileMap + tilex + tiley * 32];

                if (bgTileData == 0x800)
                {
                    if (tile >= 0x80) tile -= 128;
                    else tile += 128;
                }

                int j = ty % 8;
                int k = 7 - (tx % 8);

                int b1 = _ram.VideoMemory[bgTileData + tile * 16 + j * 2];
                int b2 = _ram.VideoMemory[bgTileData + tile * 16 + j * 2 + 1];

                if (k == 7 && (x + 8) < 160)
                {
                    for (; k >= 0; k--)
                    {
                        int pixel = (((b2 >> k) & 0x01) << 1) | ((b1 >> k) & 0x01);
                        pixel = palettebglookup[pixel];
                        lineData[x] = (pixel << 24) | 0xff;

                        x++;
                    }
                }
                else
                {
                    int pixel = (((b2 >> k) & 0x01) << 1) | ((b1 >> k) & 0x01);
                    pixel = palettebglookup[pixel];
                    lineData[x] = (pixel << 24) | 0xff;

                    x++;
                }
            }

            // now draw the sprites
            int displayedSprites = 0;

            Span<byte> oamMemory = new Span<byte>(_ram.SpecialPurpose, 0, 256);

            for (int i = 0; i < 40; i++)
            {
                int spriteY = oamMemory[i * 4];
                if (spriteY == 0 || spriteY >= 144 + 16) continue;
                spriteY -= 16;

                if (spriteY <= y && spriteY + 8 > y)
                {
                    // sprite exists on this line, populate the line data
                    int spriteX = oamMemory[i * 4 + 1] - 8;
                    var character = oamMemory[i * 4 + 2];
                    var attr = oamMemory[i * 4 + 3];

                    int b1, b2;
                    // check if y is flipped
                    if ((attr & 0x40) == 0x40)
                    {
                        b1 = _ram.VideoMemory[character * 16 + (7 - (y - spriteY)) * 2];
                        b2 = _ram.VideoMemory[character * 16 + (7 - (y - spriteY)) * 2 + 1];
                    }
                    else
                    {
                        b1 = _ram.VideoMemory[character * 16 + (y - spriteY) * 2];
                        b2 = _ram.VideoMemory[character * 16 + (y - spriteY) * 2 + 1];
                    }

                    displayedSprites++;
                    if (displayedSprites > 10) break;

                    for (int x = Math.Max(0, spriteX); x < Math.Min(160, spriteX + 8); x++)
                    {
                        // check if this sprite is under a window pixel
                        if ((lineData[x] & 0xffff) == 0xffff && (attr & 0x80) == 0x80) continue;

                        int _x = x - spriteX;
                        int pixel;
                        // check if x is flipped
                        if ((attr & 0x20) == 0x20)
                        {
                            pixel = (((b2 >> _x) & 0x01) << 1) | ((b1 >> _x) & 0x01);
                        }
                        else
                        {
                            pixel = (((b2 >> (7 - _x)) & 0x01) << 1) | ((b1 >> (7 - _x)) & 0x01);
                        }
                        // get the correct palette for this pixel
                        pixel = (pixel == 0 ? -1 : ((attr & 0x10) == 0x10) ? palette1lookup[pixel] : palette0lookup[pixel]) & 0xff;
                        // now merge the pixel information into the line data (which already contains background information)
                        int background = (lineData[x] >> 24);
                        // taking care of order depending on the spriteX position
                        if ((lineData[x] & 0xff) == 0xff) lineData[x] = pixel | ((spriteX + 8) << 8) | (attr << 16) | (background << 24);
                        else if (((lineData[x] >> 8) & 0xff) > spriteX + 8 && pixel != 255 && pixel != 0) lineData[x] = pixel | ((spriteX + 8) << 8) | (attr << 16) | (background << 24);
                    }
                }
            }

            // TODO:  lineData could just be a 144x160 int array to be passed to a shader
            // Then this routine can be done on the GPU very easily
            for (int i = 0, p = y * 160; i < 160; i++, p++)
            {
                if (lineData[i] == -1) continue;
                int pixel = lineData[i] & 0xff;
                int attr = (lineData[i] >> 16) & 0xff;
                int background = (lineData[i] >> 24) & 0xff;

                if (background < 4 && (pixel == 0xff || (background > 0 && (attr & 0x80) == 0x80)))
                {
                    backgroundTexture[p] = activePalette[background];
                    //Array.Copy(activePalette[background], 0, backgroundTexture, p, 4);
                }
                else if (pixel < 4)
                {
                    backgroundTexture[p] = activePalette[pixel];
                    //Array.Copy(activePalette[pixel], 0, backgroundTexture, p, 4);
                }
            }
        }

        private int[] palettebglookup = new int[4];
        private int[] palette0lookup = new int[4];
        private int[] palette1lookup = new int[4];
        private int[] lineData = new int[160];

        private void SetCoincidenceFlag()
        {
            if (_ram.SpecialPurpose[0x145] == lineCtr)
            {
                _ram.SpecialPurpose[0x141] |= 0x04;
                var ff41 = _ram.SpecialPurpose[0x0141];
                if ((ff41 & 0x40) == 0x40) StatInterrupt = true;
            }
            else _ram.SpecialPurpose[0x141] &= 0b11111011;
        }

        private bool Tick4mhz(bool displayActive, int clks = 1)
        {
            clkCtr += clks;

            switch (lcdMode)
            {
                case LCDMode.HBlank:
                    if (clkCtr == 204)
                    {
                        //if (displayActive) DrawLine(lineCtr);

                        if (lineCtr == 143)
                        {
                            lcdMode = LCDMode.VBlank;

                            VBlankInterrupt = true;

                            var ff41 = _ram.SpecialPurpose[0x0141];
                            if ((ff41 & 0x10) == 0x10) StatInterrupt = true;
                        }
                        else
                        {
                            lcdMode = LCDMode.Mode2;

                            var ff41 = _ram.SpecialPurpose[0x0141];
                            if ((ff41 & 0x20) == 0x20) StatInterrupt = true;
                        }

                        
                        SetCoincidenceFlag();
                        lineCtr++;
                        _ram.SetFF44(lineCtr);
                        clkCtr = 0;
                    }
                    break;
                case LCDMode.Mode2:
                    if (clkCtr == 80)
                    {
                        lcdMode = LCDMode.Mode3;
                        clkCtr = 0;
                    }
                    break;
                case LCDMode.Mode3:
                    if (clkCtr == 172)
                    {
                        var ff41 = _ram.SpecialPurpose[0x0141];
                        if ((ff41 & 0x08) == 0x08) StatInterrupt = true;
                        lcdMode = LCDMode.HBlank;
                        clkCtr = 0;

                        if (displayActive) DrawLine(lineCtr);
                    }
                    break;
                case LCDMode.VBlank:
                    if (clkCtr == 456)
                    {
                        if (lineCtr == 153)
                        {
                            var ff41 = _ram.SpecialPurpose[0x0141];
                            if ((ff41 & 0x20) == 0x20) StatInterrupt = true;
                            lcdMode = LCDMode.Mode2;

                            
                            SetCoincidenceFlag();
                            lineCtr = 0;
                            _ram.SetFF44(lineCtr);
                            clkCtr = 0;
                            windowY = 0;
                            return displayActive;
                        }
                        else
                        {
                            
                            SetCoincidenceFlag();
                            lineCtr++;
                            _ram.SetFF44(lineCtr);
                            clkCtr = 0;
                        }
                    }
                    break;
            }

            return false;
        }

        public void ExportState(int version, BinaryWriter output)
        {
            output.Write(clkCtr);
            output.Write(lineCtr);
            output.Write((byte)lcdMode);
            output.Write(VBlankInterrupt);
            output.Write(StatInterrupt);
            for (int i = 0; i < 4; i++) output.Write(activePalette[i]);
        }

        public void ImportState(int version, BinaryReader input)
        {
            clkCtr = input.ReadInt32();
            lineCtr = input.ReadByte();
            lcdMode = (LCDMode)input.ReadByte();
            VBlankInterrupt = input.ReadBoolean();
            StatInterrupt = input.ReadBoolean();
            for (int i = 0; i < 4; i++) activePalette[i] = input.ReadUInt32();
        }
    }
}
