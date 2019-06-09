﻿namespace GB
{
    class LCD
    {
        private Memory _ram;

        public LCD(Memory ram)
        {
            _ram = ram;

            for (int i = 0; i < backgroundTexture.Length; i++)
                backgroundTexture[i] = 255;
        }

        private Mode gpuMode = Mode.Mode2;
        private int clkCtr;
        private int lineCtr;

        private enum Mode
        {
            HBlank = 0,
            VBlank = 1,
            Mode2 = 2,
            Mode3 = 3
        }

        public bool Tick1mhz()
        {
            bool frameComplete = Tick4mhz();
            if (Tick4mhz()) frameComplete = true;
            if (Tick4mhz()) frameComplete = true;
            if (Tick4mhz()) frameComplete = true;

            // update the STAT flags with the current mode and LY=LYC
            byte ramff41 = _ram[0xff41];
            ramff41 &= 0b11111000;
            if (_ram[0xff45] == lineCtr) ramff41 |= 0x04;
            ramff41 |= (byte)gpuMode;
            _ram[0xff41] = ramff41;

            return frameComplete;
        }

        public byte[] backgroundTexture = new byte[160 * 144 * 4];

        private void DrawLine(int y)
        {
            int windowTileMap = (_ram[0xff40] & 0x40) == 0x40 ? 0x9c00 : 0x9800;
            int bgTileData = (_ram[0xff40] & 0x10) == 0x10 ? 0x8000 : 0x8800;
            int bgTileMap = (_ram[0xff40] & 0x08) == 0x08 ? 0x9c00 : 0x9800;
            bgTileData -= 0x8000;
            bgTileMap -= 0x8000;

            int scx = _ram[0xff43];
            int scy = _ram[0xff42];

            for (int x = 0; x < 32; x++)
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
                    //int p = x * 8 * 4 + (y * 8 + j) * 32 * 8 * 4;
                    
                    byte b1 = _ram.VideoMemory[bgTileData + tile * 16 + j * 2];
                    byte b2 = _ram.VideoMemory[bgTileData + tile * 16 + j * 2 + 1];

                    for (int k = 7; k >= 0; k--)
                    {
                        int pixel = (((b2 >> k) & 0x01) << 1) | ((b1 >> k) & 0x01);

                        byte c = (byte)(pixel == 0 ? 255 : (pixel == 1 ? 160 : (pixel == 2 ? 100 : 0)));
                        backgroundTexture[p++] = c;      // B
                        backgroundTexture[p++] = c;      // G
                        backgroundTexture[p++] = c;      // R
                        backgroundTexture[p++] = 255;    // A
                    }
                }
            }
        }

        private bool Tick4mhz()
        {
            clkCtr++;

            switch (gpuMode)
            {
                case Mode.Mode2:
                    if (clkCtr == 80)
                    {
                        gpuMode = Mode.Mode3;
                        clkCtr = 0;
                    }
                    break;
                case Mode.Mode3:
                    if (clkCtr == 172)
                    {
                        gpuMode = Mode.HBlank;
                        clkCtr = 0;
                    }
                    break;
                case Mode.HBlank:
                    if (clkCtr == 204)
                    {
                        DrawLine(lineCtr);

                        if (lineCtr == 143) gpuMode = Mode.VBlank;
                        else gpuMode = Mode.Mode2;
                        lineCtr++;
                        _ram[0xff44] = (byte)lineCtr;
                        clkCtr = 0;
                    }
                    break;
                case Mode.VBlank:
                    if (clkCtr == 456)
                    {
                        if (lineCtr == 153)
                        {
                            gpuMode = Mode.Mode2;
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
