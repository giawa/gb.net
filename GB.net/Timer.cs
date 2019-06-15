namespace GB
{
    class Timer
    {
        private Memory _ram;

        public Timer(Memory ram)
        {
            _ram = ram;
        }

        public bool TimerInterrupt;

        public void TickTIMA()
        {
            var tma = _ram.SpecialPurpose[0x105] + 1;//_ram[0xff05] + 1;
            if (tma > 255)
            {
                TimerInterrupt = true;
                //_ram.SpecialPurpose[0x105] = _ram[0xff06];
                _ram.SpecialPurpose[0x105] = 0;
                _ram.ReloadTIMA();
            }
            else
            {
                _ram.SpecialPurpose[0x105] = (byte)tma;
            }
        }

        public void Tick1MHz()
        {
            var tac = _ram.SpecialPurpose[0x107];//_ram[0xff07];
            int ctr = _ram.TimerCounter;

            if ((tac & 0x04) == 0x04)
            {
                switch (tac & 0x03)
                {
                    case 0x03:
                        if ((ctr & 0x003f) == 0x003f) TickTIMA();
                        break;
                    case 0x02:
                        if ((ctr & 0x000f) == 0x000f) TickTIMA();
                        break;
                    case 0x01:
                        if ((ctr & 0x0003) == 0x0003) TickTIMA();
                        break;
                    case 0x00:
                        if ((ctr & 0x00ff) == 0x00ff) TickTIMA();
                        break;
                }
            }

            _ram.TimerCounter = (ctr + 1) & 16383;
            _ram.SpecialPurpose[0x104] = (byte)(ctr >> 6);
        }
    }
}
