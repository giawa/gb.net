using SDL2;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace GB
{
    class Sound
    {
        private Memory _ram;
        private SDL.SDL_AudioSpec audioSpec;

        public Sound(Memory ram)
        {
            _ram = ram;

            audioSpec = new SDL.SDL_AudioSpec();
            audioSpec.channels = 1;
            audioSpec.freq = 44100;
            audioSpec.samples = 512;
            audioSpec.format = SDL.AUDIO_S16;
            audioSpec.callback = new SDL.SDL_AudioCallback(FillSamples);

            //SDL.SDL_OpenAudio(ref audioSpec, IntPtr.Zero);
            SDL.SDL_AudioSpec have;
            device = SDL.SDL_OpenAudioDevice(null, 0, ref audioSpec, out have, 0);
            SDL.SDL_PauseAudioDevice(device, 0);
        }

        ~Sound()
        {
        }

        private int ctr;
        private int sample;
        private uint device;

        internal struct SquareWaveInfo
        {
            public int X;           // determines frequency via formula 1048576 / (2 * 4 * (2048 - X)) Hz
            public byte Amplitude;  // amplitude is a 4 bit value
            public byte DutyCycle;  // 00 = 12.5%, 01 = 25%, 10 = 50%, 11 = 75%

            public SquareWaveInfo(int x, byte amplitude, byte dutyCycle)
            {
                X = x;
                Amplitude = amplitude;
                DutyCycle = dutyCycle;
            }
        }

        internal struct NoiseWaveInfo
        {
            public int Frequency;
            public byte Amplitude;
            public bool SevenBit;

            public NoiseWaveInfo(int f, byte amplitude, bool sevenBit)
            {
                Frequency = f;
                Amplitude = amplitude;
                SevenBit = sevenBit;
            }
        }

        private ConcurrentQueue<SquareWaveInfo> sampler1 = new ConcurrentQueue<SquareWaveInfo>();
        private ConcurrentQueue<SquareWaveInfo> sampler2 = new ConcurrentQueue<SquareWaveInfo>();
        private ConcurrentQueue<NoiseWaveInfo> sampler4 = new ConcurrentQueue<NoiseWaveInfo>();

        public class SquareWaveGenerator
        {
            public int SweepTime;
            public bool SubtractSweep;
            public int SweepShift;

            public int Length;
            public bool UseLength;

            public int DutyCycle;

            public int DefaultEnvelope;
            public bool AmplifyEnvelope;
            public int EnvelopeSteps;

            public int X;

            private int SweepCtr;
            public bool Active;
            private int EnvelopeCtr;

            private int Envelope;

            public void InitializeSound1(Memory ram)
            {
                var nr10 = ram.SpecialPurpose[0x110];

                SweepShift = nr10 & 0x07;
                SubtractSweep = (nr10 & 0x08) == 0x08;
                SweepTime = (nr10 >> 4) & 0x07;

                var nr11 = ram.SpecialPurpose[0x111];

                Length = 64 - (nr11 & 0x3f);
                DutyCycle = (nr11 >> 6) & 0x03;

                var nr12 = ram.SpecialPurpose[0x112];

                EnvelopeSteps = nr12 & 0x07;
                AmplifyEnvelope = (nr12 & 0x08) == 0x08;
                DefaultEnvelope = (nr12 >> 4) & 0x0f;
                Envelope = DefaultEnvelope;

                var nr14 = ram.SpecialPurpose[0x114];

                X = ram.SpecialPurpose[0x113] | ((nr14 & 0x07) << 8);
                UseLength = (nr14 & 0x40) == 0x40;

                Active = true;
            }

            public void InitializeSound2(Memory ram)
            {
                SweepTime = 0;

                var nr21 = ram.SpecialPurpose[0x116];

                Length = 64 - (nr21 & 0x3f);
                DutyCycle = (nr21 >> 6) & 0x03;

                var nr22 = ram.SpecialPurpose[0x117];

                EnvelopeSteps = nr22 & 0x07;
                AmplifyEnvelope = (nr22 & 0x08) == 0x08;
                DefaultEnvelope = (nr22 >> 4) & 0x0f;
                Envelope = DefaultEnvelope;

                var nr23 = ram.SpecialPurpose[0x118];
                var nr24 = ram.SpecialPurpose[0x119];

                X = nr23 | ((nr24 & 0x07) << 8);//(nr23 & 0x07) | ((nr23 >> 3) & 0x18) | ((nr24 & 0x3f) << 5);
                UseLength = (nr24 & 0x40) == 0x40;

                Active = true;

                if (UseLength) Console.WriteLine(Length.ToString());
            }

            internal SquareWaveInfo GetInfo()
            {
                if (Active) return new SquareWaveInfo(X, (byte)Envelope, (byte)DutyCycle);
                else return new SquareWaveInfo();
            }

            public void Tick256Hz()
            {
                if (!Active)
                {
                    Envelope = 0;
                    return;
                }

                if (SweepTime != 0)
                {
                    SweepCtr++;

                    if (SweepCtr >= SweepTime * 2 && SweepShift != 0)
                    {
                        if (SubtractSweep) X = X - X / (1 << (SweepShift - 1));
                        else X = X + X / (1 << (SweepShift - 1));

                        if (X >= 2048)
                        {
                            Active = false;
                        }
                    }
                }

                if (UseLength)
                {
                    Length = Length - 1;

                    if (Length == 0)
                    {
                        Active = false;
                    }
                }

                if (EnvelopeSteps != 0)
                {
                    EnvelopeCtr++;

                    if (EnvelopeCtr >= EnvelopeSteps * 4)
                    {
                        if (AmplifyEnvelope) Envelope = Math.Min(15, Envelope + 1);
                        else Envelope = Math.Max(0, Envelope - 1);

                        EnvelopeCtr = 0;
                    }
                }
            }
        }

        private SquareWaveGenerator sound1gen = new SquareWaveGenerator();
        private SquareWaveGenerator sound2gen = new SquareWaveGenerator();

        private int slowTickCtr = 0;

        public void InitSound1(Memory ram)
        {
            sound1gen.InitializeSound1(ram);
        }

        public void InitSound2(Memory ram)
        {
            sound2gen.InitializeSound2(ram);
        }

        private int sound4Length;
        private int sound4EnvelopeSteps;
        private int sound4Envelope;
        private int sound4DefaultEnvelope;
        private bool sound4AmplifyEnvelope;
        private int sound4DivRatio;
        private bool sound4SevenBit;
        private int sound4ShiftClock;
        private bool sound4UseLength;

        public void InitSound4(Memory ram)
        {
            sound4Length = 64 - (ram.SpecialPurpose[0x120] & 0x3f);

            var nr42 = ram.SpecialPurpose[0x121];
            sound4EnvelopeSteps = nr42 & 0x07;
            sound4AmplifyEnvelope = (nr42 & 0x08) == 0x08;
            sound4DefaultEnvelope = (nr42 & 0xf0) >> 4;

            var nr43 = ram.SpecialPurpose[0x122];
            sound4DivRatio = nr43 & 0x07;
            sound4SevenBit = (nr43 & 0x08) == 0x08;
            sound4ShiftClock = (nr43 & 0xf0) >> 4;

            var nr44 = ram.SpecialPurpose[0x123];
            sound4UseLength = (nr44 & 0x40) == 0x40;

            whiteNoiseDivRatio = sound4DivRatio * (1 << (sound4ShiftClock + 1));
            if (sound4ShiftClock == 14 || sound4ShiftClock == 15)
                whiteNoiseDivRatio = int.MaxValue;
            sound4Envelope = sound4DefaultEnvelope;
        }

        private int whiteNoiseCtr = 0;
        private int whiteNoiseDivRatio = 1;
        private int lfsr = 1;
        private int sound4EnvelopeCtr;

        private int _256ctr = 0;

        public void Tick1MHz()
        {
            if (device < 2) return;

            ctr++;
            slowTickCtr++;

            if (slowTickCtr == 3906)
            {
                _256ctr++;
                //if ((_256ctr % 256) == 0) Console.WriteLine((_256ctr >> 8).ToString());

                /*var nr12 = _ram.SpecialPurpose[0x112];
                bool AmplifyEnvelope = (nr12 & 0x08) == 0x08;
                if (AmplifyEnvelope != sound1gen.AmplifyEnvelope)
                    sound1gen.Active = false;

                var nr22 = _ram.SpecialPurpose[0x117];
                AmplifyEnvelope = (nr22 & 0x08) == 0x08;
                if (AmplifyEnvelope != sound2gen.AmplifyEnvelope)
                    sound2gen.Active = false;

                var nr42 = _ram.SpecialPurpose[0x121];
                AmplifyEnvelope = (nr42 & 0x08) == 0x08;
                if (AmplifyEnvelope != sound4AmplifyEnvelope)
                    sound4Envelope = 0;*/

                if (_ram.SpecialPurpose[0x112] == 0x08) sound1gen.Active = false;
                if (_ram.SpecialPurpose[0x114] == 0x80) sound1gen.Active = false;

                if (_ram.SpecialPurpose[0x117] == 0x08) sound2gen.Active = false;
                if (_ram.SpecialPurpose[0x119] == 0x80) sound2gen.Active = false;

                //sound1gen.Tick256Hz();
                sound2gen.Tick256Hz();

                if (sound4UseLength && sound4Length > 0)
                {
                    sound4Length--;
                    if (sound4Length == 0) sound4Envelope = 0;
                }

                /*var nr42 = _ram.SpecialPurpose[0x121];
                sound4EnvelopeSteps = nr42 & 0x07;
                sound4AmplifyEnvelope = (nr42 & 0x08) == 0x08;
                sound4DefaultEnvelope = (nr42 & 0xf0) >> 4;*/

                if (sound4EnvelopeSteps != 0)
                {
                    sound4EnvelopeCtr++;

                    if (sound4EnvelopeCtr >= sound4EnvelopeSteps * 4)
                    {
                        if (sound4AmplifyEnvelope) sound4Envelope = Math.Min(15, sound4Envelope + 1);
                        else sound4Envelope = Math.Max(0, sound4Envelope - 1);

                        sound4EnvelopeCtr = 0;
                    }
                }

                slowTickCtr = 0;
            }

            // only do every few clock cycle since CPU can't update registers fast enough anyways
            if (ctr == 4)//(ctr % 4) == 0)
            {
                //bool outputEnabled = (_ram.SpecialPurpose[0x124] & 0x88) != 0;
                byte soundEnabled = _ram.SpecialPurpose[0x126];
                bool outputEnabled = ((soundEnabled & 0x80) == 0x80);
                if (outputEnabled)
                {
                    byte soundMapping = _ram.SpecialPurpose[0x125];
                    bool sound1 = (soundMapping & 0x01) != 0;// && (soundEnabled & 0x01) != 0;
                    bool sound2 = (soundMapping & 0x02) != 0;// && (soundEnabled & 0x02) != 0;
                    bool sound3 = (soundMapping & 0x04) != 0;// && (soundEnabled & 0x04) != 0;
                    bool sound4 = (soundMapping & 0x08) != 0;// && (soundEnabled & 0x08) != 0;

                    if (sound1)
                    {
                        sampler1.Enqueue(sound1gen.GetInfo());
                    }
                    else
                    {
                        sampler1.Enqueue(new SquareWaveInfo());
                    }

                    if (sound2)
                    {
                        sampler2.Enqueue(sound2gen.GetInfo());
                    }
                    else
                    {
                        sampler2.Enqueue(new SquareWaveInfo());
                    }

                    if (sound4)
                    {
                        sampler4.Enqueue(new NoiseWaveInfo(whiteNoiseDivRatio, (byte)sound4Envelope, sound4SevenBit));
                    }
                    else
                    {
                        sampler4.Enqueue(new NoiseWaveInfo());
                    }
                }
                else
                {
                    sampler1.Enqueue(new SquareWaveInfo());
                    sampler2.Enqueue(new SquareWaveInfo());
                    sampler4.Enqueue(new NoiseWaveInfo());
                }

                ctr = 0;
            }
        }

        private SquareWaveInfo lastSample1, lastSample2;
        private NoiseWaveInfo lastSample4;

        private double sampleRatio = 6;
        private long bigRatio = (60000 << filterAmount);
        private double samples = 0;
        private const int filterAmount = 10;

        private void FillSamples(IntPtr userdata, IntPtr stream, int length)
        {
            short[] waveData = new short[length / 2];

            bigRatio += sampler1.Count - (bigRatio >> filterAmount);

            for (int i = 0; i < waveData.Length; i++)
            {
                for (int j = 0; j < Math.Floor(sampleRatio); j++)
                {
                    if (sampler1.TryDequeue(out var newSample1))
                        lastSample1 = newSample1;
                    if (sampler2.TryDequeue(out var newSample2))
                        lastSample2 = newSample2;
                    if (sampler4.TryDequeue(out var newSample4))
                        lastSample4 = newSample4;

                    // every sample will move the lfsr by 4
                    for (int k = 0; k < 4; k++)
                    {
                        whiteNoiseCtr++;
                        if (whiteNoiseCtr >= lastSample4.Frequency)
                        {
                            int xor = (lfsr & 0x01) ^ ((lfsr & 0x02) >> 1);
                            lfsr = (lfsr >> 1) | (xor << 14);
                            if (lastSample4.SevenBit) lfsr = (lfsr & 0x3f) | (xor << 6);

                            whiteNoiseCtr = 0;
                        }
                    }
                }

                samples = samples - Math.Floor(samples) + sampleRatio;

                int s1 = 0, s2 = 0, s4 = 0;

                if (lastSample1.X != 0 && lastSample1.Amplitude > 0)
                {
                    double f1 = 1048576 / (2 * 4 * (2048 - lastSample1.X));
                    double offset = (lastSample1.DutyCycle == 0 ? -0.75 : (lastSample1.DutyCycle == 1 ? -.5 : (lastSample1.DutyCycle == 2 ? 0 : 0.5)));
                    double t1 = Math.Sin(sample * Math.PI * 2 * f1 / 44100) + offset;
                    s1 = (t1 > 0 ? short.MaxValue : short.MinValue) / 15;
                    s1 *= lastSample1.Amplitude;
                }
                if (lastSample2.X != 0 && lastSample2.Amplitude > 0)
                {
                    double f2 = 1048576 / (2 * 4 * (2048 - lastSample2.X));
                    double offset = (lastSample2.DutyCycle == 0 ? -0.75 : (lastSample2.DutyCycle == 1 ? -.5 : (lastSample2.DutyCycle == 2 ? 0 : 0.5)));
                    double t2 = Math.Sin(sample * Math.PI * 2 * f2 / 44100) + offset;
                    s2 = (t2 > 0 ? short.MaxValue : short.MinValue) / 15;
                    s2 *= lastSample2.Amplitude;
                }
                if (lastSample4.Amplitude != 0)
                {
                    s4 = ((lfsr & 0x01) == 0x01 ? short.MinValue : short.MaxValue) / 15;
                    s4 *= lastSample4.Amplitude;
                }

                waveData[i] = (short)Math.Min(short.MaxValue, Math.Max(short.MinValue, s1 + s2 + s4));

                sample++;
            }

            sampleRatio = (((bigRatio >> filterAmount) / 10000.0) + 1);
            //Console.WriteLine(sampleRatio.ToString() + " " + (bigRatio >> filterAmount) + " " + samples);

            // have a slight audio glitch every _many_ hours to avoid overflow
            if (sample > 2000000000) sample = 0;

            Marshal.Copy(waveData, 0, stream, waveData.Length);
        }
    }
}
