using System;
using System.Drawing;

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

        public enum LCDMode
        {
            HBlank = 0,
            VBlank = 1,
            Mode2 = 2,
            Mode3 = 3
        }

        public bool Tick1MHz()
        {
            byte ramff40 = _ram[0xff40];
            if ((ramff40 & 0x80) == 0) return false;

            bool frameComplete = Tick4mhz();
            if (Tick4mhz()) frameComplete = true;
            if (Tick4mhz()) frameComplete = true;
            if (Tick4mhz()) frameComplete = true;

            // update the STAT flags with the current mode and LY=LYC
            byte ramff41 = _ram[0xff41];
            ramff41 &= 0b11111100;
            ramff41 |= (byte)lcdMode;
            _ram[0xff41] = ramff41;

            return frameComplete;
        }

        public byte[] backgroundTexture = new byte[160 * 144 * 4];

        private byte[][] activePalette = new byte[4][];

        private void SetPalette(uint[] palette)
        {
            if (palette.Length != 4) return;
            for (int i = 0; i < 4; i++)
            {
                activePalette[i] = BitConverter.GetBytes(palette[i]);
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

        public byte[] DumpBackground()
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
        }

        private void DrawLine(int y)
        {
            var ff40 = _ram[0xff40];
            int windowTileMap = (ff40 & 0x40) == 0x40 ? 0x9c00 : 0x9800;
            int bgTileData = (ff40 & 0x10) == 0x10 ? 0x8000 : 0x8800;
            int bgTileMap = (ff40 & 0x08) == 0x08 ? 0x9c00 : 0x9800;
            bgTileData -= 0x8000;
            bgTileMap -= 0x8000;

            // draw the background first
            int scx = _ram[0xff43];
            int scy = _ram[0xff42];

            //Array.Clear(backgroundTexture, 160 * 4 * y, 160 * 4);
            for (int i = 0; i < 160 * 4; i++)
                backgroundTexture[i + 160 * 4 * y] = 255;

            // this is going to be slow!
            for (int x = 0; x < 160;)
            {
                int p = x * 4 + y * 160 * 4;

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

                byte b1 = _ram.VideoMemory[bgTileData + tile * 16 + j * 2];
                byte b2 = _ram.VideoMemory[bgTileData + tile * 16 + j * 2 + 1];

                if (k == 7 && (x + 8) < 160)
                {
                    for (; k >= 0; k--)
                    {
                        int pixel = (((b2 >> k) & 0x01) << 1) | ((b1 >> k) & 0x01);
                        Array.Copy(activePalette[pixel], 0, backgroundTexture, p, 4);
                        p += 4;
                    }

                    x += 8;
                }
                else
                {
                    int pixel = (((b2 >> k) & 0x01) << 1) | ((b1 >> k) & 0x01);
                    Array.Copy(activePalette[pixel], 0, backgroundTexture, p, 4);

                    x++;
                }
            }

            // now draw the sprites
            int displayedSprites = 0;

            Span<byte> oamMemory = new Span<byte>(_ram.SpecialPurpose, 0, 256);
            byte palette0 = _ram[0xff48];
            byte palette1 = _ram[0xff49];
            for (int i = 0; i < 4; i++)
            {
                palette0lookup[i] = (palette0 >> (2 * i)) & 0x03;
                palette1lookup[i] = (palette1 >> (2 * i)) & 0x03;
            }

            Array.Clear(activeSprites, 0, 10);
            for (int i = 0; i < 160; i++) lineData[i] = -1;

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

                    byte b1, b2;
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
                    if (displayedSprites > 9) break;

                    for (int x = Math.Max(0, spriteX); x < Math.Min(160, spriteX + 8); x++)
                    {
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
                        //if (pixel != 0)
                        {
                            pixel = (pixel == 0 ? 0 : ((attr & 0x10) == 0x10) ? palette1lookup[pixel] : palette0lookup[pixel]);
                            //if (pixel != 0)
                            {
                                if (lineData[x] == -1) lineData[x] = pixel | (spriteX << 8);
                                else if ((lineData[x] >> 8) > spriteX && pixel != 0) lineData[x] = pixel | (spriteX << 8);
                            }
                        }
                    }
                }
            }

            for (int i = 0, p = y * 160 * 4; i < 160; i++, p += 4)
            {
                if (lineData[i] == -1) continue;
                int pixel = lineData[i] & 0xff;
                if (pixel > 0)
                {
                    Array.Copy(activePalette[pixel], 0, backgroundTexture, p, 4);
                }
            }

            /*for (int x = 0; x < 160; x++)
            {
                // only 10 sprites can be shown per line
                if (displayedSprites > 9) break;

                for (int i = 0; i < 40; i++)
                {
                    int spriteY = oamMemory[i * 4];//_ram[0xffe0 + i * 4];// + 16;
                    // sprites with 0 or 160 y values do not affect total sprite count
                    if (spriteY == 0 || spriteY >= 144 + 16) continue;
                    spriteY -= 16;

                    if (spriteY <= y && spriteY + 8 > y)
                    {
                        int spriteX = oamMemory[i * 4 + 1] - 8;//_ram[0xffe1 + i * 4] - 8;

                        if (spriteX <= x && spriteX + 8 > x)
                        {
                            if (y == 48)
                            {
                                Console.WriteLine("stop");
                            }
                            var character = oamMemory[i * 4 + 2];
                            var attr = oamMemory[i * 4 + 3];

                            byte b1, b2;
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
                            int p = x * 4 + y * 160 * 4;

                            for (int _x = x - spriteX; _x < 8; _x++)
                            {
                                //int _x = x - spriteX;
                                int pixel;
                                // check if x is flipped
                                if ((attr & 0x20) == 0x20)
                                {
                                    pixel = (((b2 >> _x) & 0x01) << 1) | ((b1 >> _x) & 0x01);
                                }
                                else
                                {
                                    pixel = (((b2 >> (7 - _x)) & 0x01) << 1) | ((b1 >> (7 -_x)) & 0x01);
                                }
                                if (pixel != 0)
                                {
                                    pixel = ((attr & 0x10) == 0x10) ? palette1lookup[pixel] : palette0lookup[pixel];
                                    if (pixel != 0) Array.Copy(activePalette[pixel], 0, backgroundTexture, p, 4);
                                }
                                p += 4;
                                x++;
                            }
                            x--;

                            displayedSprites++;

                            break;
                        }
                    }
                }
            }*/
        }

        int[] palette0lookup = new int[4];
        int[] palette1lookup = new int[4];
        int[] activeSprites = new int[10];
        int[] lineData = new int[160];

        private bool Tick4mhz()
        {
            clkCtr++;

            switch (lcdMode)
            {
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
                        lcdMode = LCDMode.HBlank;
                        clkCtr = 0;
                    }
                    break;
                case LCDMode.HBlank:
                    if (clkCtr == 204)
                    {
                        DrawLine(lineCtr);

                        if (lineCtr == 143)
                        {
                            lcdMode = LCDMode.VBlank;
                            VBlankInterrupt = true;
                        }
                        else lcdMode = LCDMode.Mode2;
                        lineCtr++;
                        _ram.SetFF44(lineCtr);
                        if (_ram[0xff45] == lineCtr) _ram[0xff41] |= 0x04;
                        else _ram[0xff41] &= 0b11111011;
                        clkCtr = 0;
                    }
                    break;
                case LCDMode.VBlank:
                    if (clkCtr == 456)
                    {
                        if (lineCtr == 153)
                        {
                            lcdMode = LCDMode.Mode2;
                            lineCtr = 0;
                            _ram.SetFF44(lineCtr);
                            if (_ram[0xff45] == lineCtr) _ram[0xff41] |= 0x04;
                            else _ram[0xff41] &= 0b11111011;
                            clkCtr = 0;
                            return true;
                        }
                        else
                        {
                            lineCtr++;
                            _ram.SetFF44(lineCtr);
                            if (_ram[0xff45] == lineCtr) _ram[0xff41] |= 0x04;
                            else _ram[0xff41] &= 0b11111011;
                            clkCtr = 0;
                        }
                    }
                    break;
            }

            return false;
        }
    }
}
