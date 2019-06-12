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
        private int lineCtr;

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
            ramff41 &= 0b11111000;
            if (_ram[0xff45] == lineCtr) ramff41 |= 0x04;
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
            int windowTileMap = (_ram[0xff40] & 0x40) == 0x40 ? 0x9c00 : 0x9800;
            int bgTileData = (_ram[0xff40] & 0x10) == 0x10 ? 0x8000 : 0x8800;
            int bgTileMap = (_ram[0xff40] & 0x08) == 0x08 ? 0x9c00 : 0x9800;
            bgTileData -= 0x8000;
            bgTileMap -= 0x8000;

            int scx = _ram[0xff43];
            int scy = _ram[0xff42];

            // this is going to be slow!
            for (int x = 0; x < 160;)
            {
                int p = x * 4 + y * 160 * 4;

                int tx = (x + scx) & 255;
                int ty = (y + scy) & 255;

                int tilex = tx / 8;
                int tiley = ty / 8;

                int tile = _ram.VideoMemory[bgTileMap + tilex + tiley * 32];

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

            /*for (int x = 0; x < 32; x++)
            {
                int tile = _ram.VideoMemory[bgTileMap + x + y * 32];
                int _x = scx + x * 8;
                if (_x >= 160 || _x < 0) break;

                // what a weird way to store pixel data ... each pixel spans 2 bytes
                for (int j = 0; j < 8; j++)
                {
                    int _y = y * 8 + j - scy;
                    if (_y >= 144 || _y < 0) continue;
                    int p = _x * 4 + _y * 160 * 4;
                    
                    byte b1 = _ram.VideoMemory[bgTileData + tile * 16 + j * 2];
                    byte b2 = _ram.VideoMemory[bgTileData + tile * 16 + j * 2 + 1];

                    for (int k = 7; k >= 0; k--)
                    {
                        int pixel = (((b2 >> k) & 0x01) << 1) | ((b1 >> k) & 0x01);

                        Array.Copy(activePalette[pixel], 0, backgroundTexture, p, 4);
                        p += 4;
                    }
                }
            }*/
        }

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
                        _ram[0xff44] = (byte)lineCtr;
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
                            _ram[0xff44] = (byte)lineCtr;
                            clkCtr = 0;
                            return true;

                            /*var bitmapData = temp.LockBits(new Rectangle(0, 0, 160, 144), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                            Marshal.Copy(backgroundTexture, 0, bitmapData.Scan0, backgroundTexture.Length);
                            temp.UnlockBits(bitmapData);
                            temp.Save($"frame{f}.png", System.Drawing.Imaging.ImageFormat.Png);
                            f++;*/

                            // 157 frames at 59.727fps = ~6.62s...  Doesn't seem far off
                        }
                        else
                        {
                            lineCtr++;
                            _ram[0xff44] = (byte)lineCtr;
                            clkCtr = 0;
                        }
                    }
                    break;
            }

            return false;
        }
    }
}
