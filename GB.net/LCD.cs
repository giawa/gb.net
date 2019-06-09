using System;
using System.Collections.Generic;
using System.Text;

namespace GB
{
    class LCD
    {
        private Memory _ram;

        public LCD(Memory ram)
        {
            _ram = ram;
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

        public void Tick1mhz()
        {
            Tick4mhz();
            Tick4mhz();
            Tick4mhz();
            Tick4mhz();

            // update the STAT flags with the current mode and LY=LYC
            byte ramff41 = _ram[0xff41];
            ramff41 &= 0b11111000;
            if (_ram[0xff45] == lineCtr) ramff41 |= 0x04;
            ramff41 |= (byte)gpuMode;
            _ram[0xff41] = ramff41;
        }

        public void Tick4mhz()
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
                        }
                        else lineCtr++;
                        _ram[0xff44] = (byte)lineCtr;
                        clkCtr = 0;
                    }
                    break;
            }
        }
    }
}
