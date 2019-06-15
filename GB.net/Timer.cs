namespace GB
{
    class Timer
    {
        private Memory _ram;

        public Timer(Memory ram)
        {
            _ram = ram;
        }

        public int Counter {  get { return ctr; } }

        private int ctr = 0;

        public bool TimerInterrupt;

        private void TickTIMA()
        {
            tickCount++;

            var tma = _ram[0xff05] + 1;
            if (tma > 255)
            {
                TimerInterrupt = true;
                _ram[0xff05] = _ram[0xff06];
            }
            else
            {
                _ram[0xff05] = (byte)tma;
            }
        }

        private int tickCount = 0;
        private int ctrCount = 0;

        public void Tick1MHz()
        {
            var tac = _ram[0xff07];
            var div = _ram[0xff04];

            // writing to the DIV register will set the top bits of counter
            if (div != (ctr >> 6)) ctr = (div << 6) | (ctr & 0x3f);
            ctr = (ctr + 1) & 16383;
            ctrCount++;

            _ram.SetFF04((byte)(ctr >> 6));

            if ((tac & 0x04) == 0x04)
            {
                switch (tac & 0x03)
                {
                    case 0x03:
                        if ((ctr & 0x003f) == 0) TickTIMA();
                        break;
                    case 0x02:
                        if ((ctr & 0x000f) == 0) TickTIMA();
                        break;
                    case 0x01:
                        if ((ctr & 0x0003) == 0) TickTIMA();
                        break;
                    case 0x00:
                        if ((ctr & 0x00ff) == 0) TickTIMA();
                        break;
                }
            }
        }
    }
}
