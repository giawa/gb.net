using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace GB
{
    class CPU
    {
        public Memory RAM { get; private set; }

        public CPU(Memory ram)
        {
            RAM = ram;

            RAM[0xff40] = 0x83;

            Breakpoints = new List<ushort>();
        }

        public Cartridge Cartridge { get; private set; }

        public void LoadCartridge(Cartridge cart)
        {
            Cartridge = cart;
            PC = 0;
            RAM.Reset();

            RAM.Cartridge = cart;
        }

        public void SetPC(ushort pc)
        {
            PC = pc;
            RAM[0xff50] = 1;    // disable bootloader
            A = 0x11;   // gameboy color
            SP = 0xfffe;
        }

        private byte A, F;
        private byte B, C;
        private byte D, E;
        private byte H, L;
        private ushort SP, PC;

        public ushort GetPC()
        {
            return PC;
        }

        public byte GetA()
        {
            return A;
        }

        //private bool nextIMEState = false;
        private bool nextIME = false;
        private bool IME = false;

        private ushort AF
        {
            get { return (ushort)((A << 8) | F); }
            set
            {
                A = (byte)((value & 0xff00) >> 8);
                F = (byte)(value & 0xff);
            }
        }

        private ushort BC
        {
            get { return (ushort)((B << 8) | C); }
            set
            {
                B = (byte)((value & 0xff00) >> 8);
                C = (byte)(value & 0xff);
            }
        }

        private ushort DE
        {
            get { return (ushort)((D << 8) | E); }
            set
            {
                D = (byte)((value & 0xff00) >> 8);
                E = (byte)(value & 0xff);
            }
        }

        private ushort HL
        {
            get { return (ushort)((H << 8) | L); }
            set
            {
                H = (byte)((value & 0xff00) >> 8);
                L = (byte)(value & 0xff);
            }
        }

        private bool running = true;

        private ushort imm16()
        {
            byte imm1 = RAM[PC++];
            byte imm2 = RAM[PC++];
            return BitConverter.ToUInt16(new byte[] { imm1, imm2 }, 0);
        }

        private short imm16s()
        {
            byte imm1 = RAM[PC++];
            byte imm2 = RAM[PC++];
            return BitConverter.ToInt16(new byte[] { imm1, imm2 }, 0);
        }

        private byte imm8()
        {
            return RAM[PC++];
        }

        private sbyte imm8s()
        {
            return (sbyte)RAM[PC++];
        }

        private byte GetCommonOp2(byte lowNibble)
        {
            switch (lowNibble)
            {
                case 0:
                    return B;
                case 1:
                    return C;
                case 2:
                    return D;
                case 3:
                    return E;
                case 4:
                    return H;
                case 5:
                    return L;
                case 6:
                    return RAM[HL];
                default:
                    return A;
            }
        }

        private IEnumerable RunExtendedOpcode(byte opcode)
        {
            // each column of opcodes run on a particular register, so grab that register here
            switch (opcode & 0x07)
            {
                case 0:
                    ExtendedOpcode(opcode, ref B);
                    break;
                case 1:
                    ExtendedOpcode(opcode, ref C);
                    break;
                case 2:
                    ExtendedOpcode(opcode, ref D);
                    break;
                case 3:
                    ExtendedOpcode(opcode, ref E);
                    break;
                case 4:
                    ExtendedOpcode(opcode, ref H);
                    break;
                case 5:
                    ExtendedOpcode(opcode, ref L);
                    break;
                case 6:
                    // this sucks, since RAM is an indexer we need a temporary store
                    yield return null;
                    byte temp = RAM[HL];
                    if (opcode < 0x40 || opcode > 0x7f) yield return null;
                    ExtendedOpcode(opcode, ref temp);
                    RAM[HL] = temp;
                    break;
                case 7:
                    ExtendedOpcode(opcode, ref A);
                    break;
            }

            yield return null;
        }

        private void bit(int bit, ref byte register)
        {
            F &= 0x10;
            F |= 0x20;
            if (((register >> bit) & 0x01) == 0) F |= 0x80;
        }

        private void res(int bit, ref byte register)
        {
            register = (byte)(register & ~(1 << bit));
        }

        private void set(int bit, ref byte register)
        {
            register = (byte)(register | (1 << bit));
        }

        // flags register (F) is Z N H C 0 0 0 0

        private void ExtendedOpcode(byte opcode, ref byte register)
        {
            switch (opcode & 0xf8)
            {
                case 0x00:  // RLC
                    rlc(ref register);
                    break;
                case 0x08:  // RRC
                    rrc(ref register);
                    break;
                case 0x10:  // RL
                    rl(ref register);
                    break;
                case 0x18:  // RR
                    rr(ref register);
                    break;
                case 0x20:
                    sla(ref register);
                    break;
                case 0x28:
                    sra(ref register);
                    break;
                case 0x30:
                    register = (byte)((register << 4) | ((register & 0xf0) >> 4));
                    if (register == 0) F = 0x80;
                    else F = 0;
                    break;
                case 0x38:  // SRL
                    F = (byte)((register & 0x01) << 4);
                    register >>= 1;
                    if (register == 0) F |= 0x80;
                    break;
                case 0x40:
                case 0x48:
                case 0x50:
                case 0x58:
                case 0x60:
                case 0x68:
                case 0x70:
                case 0x78:
                    bit(((opcode - 0x40) >> 3), ref register);
                    break;
                case 0x80:
                case 0x88:
                case 0x90:
                case 0x98:
                case 0xa0:
                case 0xa8:
                case 0xb0:
                case 0xb8:
                    res(((opcode - 0x80) >> 3), ref register);
                    break;
                case 0xc0:
                case 0xc8:
                case 0xd0:
                case 0xd8:
                case 0xe0:
                case 0xe8:
                case 0xf0:
                case 0xf8:
                    set(((opcode - 0xc0) >> 3), ref register);
                    break;
                default:
                    Console.WriteLine("Unimplemented extended opcode.");
                    break;
            }
        }

        #region ALU
        private void inc8(ref byte register)
        {
            F &= 0b00010000;
            if ((register & 0x0f) == 0x0f) F |= 0b00100000;
            if (register == 255) F |= 0x80;
            register++;
        }

        private void dec8(ref byte register)
        {
            F &= 0b00010000;
            F |= 0b01000000;
            if ((register & 0x0f) == 0x00) F |= 0b00100000;
            if (register == 1) F |= 0x80;
            register--;
        }

        private void ld(byte opcode, byte op2)
        {
            switch (opcode & 0xF8)
            {
                case 0x40:
                    B = op2;
                    break;
                case 0x48:
                    C = op2;
                    break;
                case 0x50:
                    D = op2;
                    break;
                case 0x58:
                    E = op2;
                    break;
                case 0x60:
                    H = op2;
                    break;
                case 0x68:
                    L = op2;
                    break;
                case 0x70:
                    RAM[HL] = op2;
                    break;
                case 0x78:
                    A = op2;
                    break;
            }
        }

        private ushort pop()
        {
            int temp = RAM[SP++];
            temp |= (RAM[SP++] << 8);

            return (ushort)temp;
        }

        private void push(ushort nn)
        {
            RAM[--SP] = (byte)((nn >> 8) & 0xff);
            RAM[--SP] = (byte)(nn & 0xff);
        }

        /*private void call(ushort nn)
        {
            push(PC);
            PC = nn;
        }*/

        private void rl(ref byte register)
        {
            int carry = (F >> 4) & 0x01;
            F = (byte)((register & 0x80) >> 3);
            register = (byte)((register << 1) | carry);
            if (register == 0) F |= 0x80;       // sets Z
        }

        private void rlc(ref byte register)
        {
            F = (byte)((register & 0x80) >> 3); // sets C and clears N and H
            register = (byte)((register << 1) | (register >> 7));
            if (register == 0) F |= 0x80;       // sets Z
        }

        private void sla(ref byte register)
        {
            F = (byte)((register & 0x80) >> 3); // sets C and clears N and H
            register = (byte)(register << 1);
            if (register == 0) F |= 0x80;       // sets Z
        }

        private void rr(ref byte register)
        {
            int carry = (F << 3) & 0x80;
            F = (byte)((register & 0x01) << 4);
            register = (byte)((register >> 1) | carry);
            if (register == 0) F |= 0x80;       // sets Z
        }

        private void rrc(ref byte register)
        {
            F = (byte)((register & 0x01) << 4); // sets C and clears N and H
            register = (byte)((register >> 1) | ((register & 0x01) << 7));
            if (register == 0) F |= 0x80;       // sets Z
        }

        private void sra(ref byte register)
        {
            F = (byte)((register & 0x01) << 4); // sets C and clears N and H
            register = (byte)(register >> 1);
            if ((register & 0x40) == 0x40) register |= 0x80;
            if (register == 0) F |= 0x80;       // sets Z
        }

        private void cp(byte n)
        {
            F = 0x40;
            if (n == A) F |= 0x80;
            if ((A & 0x0f) < (n & 0x0f)) F |= 0x20;
            if (A < n) F |= 0x10;
        }

        private void add(byte n)
        {
            int result = A + n;
            F = 0;
            if ((A & 0x0f) + (n & 0x0f) > 0x0f) F |= 0x20;
            if (result > 255) F |= 0x10;
            A = (byte)(result);
            if (A == 0) F |= 0x80;
        }

        private void adc(byte n)
        {
            int carry = (F & 0x10) >> 4;
            F = 0;
            int result = A + n + carry;
            if ((A & 0x0f) + (n & 0x0f) + carry > 0x0f) F |= 0x20;
            if (result > 255) F |= 0x10;
            A = (byte)(result);
            if (A == 0) F |= 0x80;
        }

        private void add16(ushort n)
        {
            int result = n + HL;
            int bit11 = (n & 0xFFF) + (HL & 0xFFF);
            F &= 0x80;
            if (bit11 > 0xfff) F |= 0x20;
            if (result > 0xffff) F |= 0x10;
            HL = (ushort)result;
        }

        private ushort addsp(sbyte n)
        {
            int result = n + SP;
            int bit3 = (n & 0xF) + (SP & 0xF);
            int bit7 = (n & 0xFF) + (SP & 0xFF);
            F = 0x00;
            if (bit3 > 0xF) F |= 0x20;
            if (bit7 > 0xff) F |= 0x10;
            return (ushort)result;
        }

        private void sub(byte n)
        {
            int result = A - n;
            F = 0x40;
            if ((A & 0x0f) < (n & 0x0f)) F |= 0x20;
            if (A < n) F |= 0x10;
            A = (byte)result;
            if (A == 0) F |= 0x80;
        }

        private void sbc(byte n)
        {
            int carry = (F & 0x10) >> 4;
            int result = A - n - carry;
            F = 0x40;
            if ((A & 0x0f) < (n & 0x0f) + carry) F |= 0x20;
            if (A < n + carry) F |= 0x10;
            A = (byte)result;
            if (A == 0) F |= 0x80;
        }

        private void and(byte n)
        {
            A &= n;
            F &= 0x0F;
            F |= 0x20;
            if (A == 0) F |= 0x80;
        }

        private void or(byte n)
        {
            A |= n;
            F &= 0x0F;
            if (A == 0) F |= 0x80;
        }

        private void xor(byte n)
        {
            A ^= n;
            F &= 0x0F;
            if (A == 0) F |= 0x80;
        }
        #endregion

        public bool CheckInterrupts()
        {
            //RAM[0xff0f] |= Interrupts;

            if (IME || stopped)
            {
                var IF = RAM[0xff0f];
                bool vblankInterrupt = (IF & 0x01) == 0x01;
                bool lcdcStatusInterrupt = (IF & 0x02) == 0x02;
                bool timerInterrupt = (IF & 0x04) == 0x04;
                bool serialInterrupt = (IF & 0x08) == 0x08;
                bool inputInterrupt = (IF & 0x10) == 0x10;

                var IE = RAM[0xffff];
                if (stopped) IE = 0x1f;
                //if (halted) IE |= 0x1f;
                if (IE == 0) return false;
                bool imeState = IME;

                if (vblankInterrupt && CallISR(IE, 0x01, 0x0040, PC, imeState)) return true;
                if (lcdcStatusInterrupt && CallISR(IE, 0x02, 0x0048, PC, imeState)) return true;
                if (timerInterrupt && CallISR(IE, 0x04, 0x0050, PC, imeState)) return true;
                if (serialInterrupt && CallISR(IE, 0x08, 0x0058, PC, imeState)) return true;
                if (inputInterrupt && CallISR(IE, 0x10, 0x0060, PC, imeState)) return true;
            }

            return false;
        }

        private bool CallISR(int ie, byte mask, ushort isr, ushort pc, bool imeState)
        {
            if ((ie & mask) == mask)
            {
                if (IME || halted)
                {
                    halted = false;

                    // service interrupt normally
                    nextIME = IME = false;
                    RAM[--SP] = (byte)((pc >> 8) & 0xff);

                    // it's possible that pushing the PC will write to IF, which can cancel the interrupt
                    if ((RAM[0xffff] & mask) == mask)
                    {
                        // this interrupt is still valid, so clear it and finish up
                        PC = isr;
                        RAM[0xff0f] ^= mask;
                        RAM[--SP] = (byte)(pc & 0xff);
                        return true;
                    }
                    else
                    {
                        // this interrupt was cancelled, so finish pushing PC, set PC to 0 and continue servicing interrupts
                        PC = 0;
                        RAM[--SP] = (byte)(pc & 0xff);
                        return false;
                    }
                }
                else if (imeState)
                {
                    // PC was already pushed by a cancelled interrupt
                    PC = isr;
                    RAM[0xff0f] ^= mask;
                    return true;
                }
                else if (stopped)
                {
                    RAM[0xff0f] ^= mask;
                    return true;
                }
                else RAM[0xff0f] ^= mask;
            }
            return false;
        }

        private bool halted = false, stopped = false, haltBug = false;
        private static List<byte> specialRegistersUsed = new List<byte>();

        public List<ushort> Breakpoints { get; private set; }

        public IEnumerable CreateStateMachine()
        {
            byte IF = 0, IE = 0;

            while (running)
            {
                int cycle = 0;

                foreach (var clk in ExecuteInstruction())
                {
                    yield return null;

                    if (cycle == 0 && (IME || halted || stopped))
                    {
                        IF = RAM[0xff0f];
                        IE = RAM[0xffff];

                        if ((IE & IF) != 0)
                        {
                            bool imeState = IME;

                            bool serviced = CallISR(IE & IF, 0x01, 0x0040, PC, imeState);
                            if (!serviced) serviced = CallISR(IE & IF, 0x02, 0x0048, PC, imeState);
                            if (!serviced) serviced = CallISR(IE & IF, 0x04, 0x0050, PC, imeState);
                            if (!serviced) serviced = CallISR(IE & IF, 0x08, 0x0058, PC, imeState);
                            if (!serviced) serviced = CallISR(IE & IF, 0x10, 0x0060, PC, imeState);

                            yield return null;
                            yield return null;
                            yield return null;
                            yield return null;
                            yield return null;
                            break;
                        }
                    }

                    cycle++;
                }
            }
        }

        private IEnumerable ExecuteInstruction()
        {
            if (halted)
            {
                yield return null;  // always need at least one clock cycle
                yield break;
            }

            foreach (var pc in Breakpoints)
            {
                if (pc == PC)
                {
                    Console.WriteLine("Hit 0x" + pc.ToString());
                    //LCD temp = new LCD(RAM);
                    //temp.DumpTiles(0x8000);
                }
            }

            byte opcode = RAM[PC];
            yield return null;  // opcode fetch takes 1 cycle
            if (haltBug)
            {
                haltBug = false;
            }
            else
            {
                PC++;
            }

            byte lowNibble = (byte)(opcode & 0x0f);
            byte op2;
            string opcodeName = string.Empty;

            if (opcode == 0xCB)
            {
                foreach (var clk in RunExtendedOpcode(RAM[PC++]))
                    yield return null;
            }
            else
            {
                switch (opcode & 0xf0)
                {
                    case 0x40:
                    case 0x50:
                    case 0x60:
                        op2 = GetCommonOp2((byte)(opcode & 0x07));
                        if ((opcode & 0x07) == 0x06) yield return null;  // RAM[HL] access
                        ld(opcode, op2);
                        break;
                    case 0x70:
                        op2 = GetCommonOp2((byte)(opcode & 0x07));
                        if (opcode == 0x76) // HALT
                        {
                            if (!IME && !nextIME)
                            {
                                haltBug = true;
                            }
                            else
                            {
                                IME = nextIME;
                                halted = true;
                                yield break;
                            }
                        }
                        else if (opcode < 0x78 || opcode == 0x7E)
                        {
                            ld(opcode, op2);
                            yield return null;  // RAM[HL] access
                        }
                        else ld(opcode, op2);
                        break;
                    case 0x80:
                        op2 = GetCommonOp2((byte)(opcode & 0x07));
                        if ((opcode & 0x07) == 6) yield return null;  // RAM[HL] access
                        if (lowNibble < 8) add(op2);
                        else adc(op2);
                        break;
                    case 0x90:
                        op2 = GetCommonOp2((byte)(opcode & 0x07));
                        if ((opcode & 0x07) == 6) yield return null;  // RAM[HL] access
                        if (lowNibble < 8) sub(op2);
                        else sbc(op2);
                        break;
                    case 0xA0:
                        op2 = GetCommonOp2((byte)(opcode & 0x07));
                        if ((opcode & 0x07) == 6) yield return null;  // RAM[HL] access
                        if (lowNibble < 8) and(op2);
                        else xor(op2);
                        break;
                    case 0xB0:
                        op2 = GetCommonOp2((byte)(opcode & 0x07));
                        if ((opcode & 0x07) == 6) yield return null;  // RAM[HL] access
                        if (lowNibble < 8) or(op2);
                        else cp(op2);   // CP n
                        break;
                    default:
                        switch (lowNibble)
                        {
                            case 0x00:
                                if (opcode == 0x00) ;       // NOP
                                else if (opcode == 0x10)    // STOP
                                {
                                    var ff4d = RAM[0xff4d];
                                    if ((ff4d & 0x01) == 0x01)
                                    {
                                        // toggles CPU/timer speed
                                        RAM[0xff4d] = (byte)((ff4d & 0x80) == 0x80 ? 0 : 0x80);
                                    }
                                    else
                                    {
                                        stopped = true;
                                        while (!CheckInterrupts())
                                            yield return null;
                                        stopped = false;
                                    }
                                }
                                else if (opcode == 0x20)
                                {
                                    // JR NZ,r8
                                    sbyte imm = imm8s();
                                    yield return null;  // 1 cycle for imm8
                                    if ((F & 0x80) == 0x00)
                                    {
                                        PC = (ushort)(imm + PC);
                                        yield return null;  // 1 cycle for PC
                                    }
                                }
                                else if (opcode == 0x30)
                                {
                                    // JR NC,r8
                                    sbyte imm = imm8s();
                                    yield return null;  // 1 cycle for imm8
                                    if ((F & 0x10) == 0x00)
                                    {
                                        PC = (ushort)(imm + PC);
                                        yield return null;  // 1 cycle for PC
                                    }
                                }
                                else if (opcode == 0xC0)
                                {
                                    // RET NZ
                                    yield return null;
                                    if ((F & 0x80) == 0x00)
                                    {
                                        PC = pop();
                                        yield return null;
                                        yield return null;
                                        yield return null;
                                    }
                                }
                                else if (opcode == 0xD0)
                                {
                                    // RET NZ
                                    yield return null;
                                    if ((F & 0x10) == 0x00)
                                    {
                                        PC = pop();
                                        yield return null;
                                        yield return null;
                                        yield return null;
                                    }
                                }
                                else if (opcode == 0xE0)
                                {
                                    var imm = imm8();
                                    yield return null;
                                    if (imm >= 0x10 && imm <= 0x3f)
                                    {
                                        RAM[0xff00 + imm] = A;  // LDH (0xff00+n),A
                                    }
                                    else
                                    {
                                        RAM[0xff00 + imm] = A;  // LDH (0xff00+n),A
                                        if (!specialRegistersUsed.Contains(imm))
                                        {
                                            specialRegistersUsed.Add(imm);
                                        }
                                    }
                                    yield return null;
                                }
                                else if (opcode == 0xF0)
                                {
                                    var imm = imm8();
                                    yield return null;
                                    A = RAM[0xff00 + imm];  // LDH A,(0xff00+n)
                                    yield return null;
                                }
                                break;
                            case 0x01:
                                yield return null;
                                yield return null;
                                if (opcode == 0x01) BC = imm16();       // LD BC,d16
                                else if (opcode == 0x11) DE = imm16();  // LD DE,d16
                                else if (opcode == 0x21) HL = imm16();  // LD HL,d16
                                else if (opcode == 0x31) SP = imm16();  // LD SP,d16
                                else if (opcode == 0xC1) BC = pop();
                                else if (opcode == 0xD1) DE = pop();
                                else if (opcode == 0xE1) HL = pop();
                                else if (opcode == 0xF1) AF = (ushort)(pop() & 0xfff0);
                                break;
                            case 0x02:
                                if (opcode == 0x02)
                                {
                                    RAM[BC] = A;        // LD (BC),A
                                    yield return null;      // 1 cycle to update RAM
                                }
                                else if (opcode == 0x12)
                                {
                                    RAM[DE] = A;   // LD (DE),A
                                    yield return null;      // 1 cycle to update RAM
                                }
                                else if (opcode == 0x22)
                                {
                                    RAM[HL++] = A; // LD (HL+),A
                                    yield return null;      // 1 cycle to update RAM
                                }
                                else if (opcode == 0x32)
                                {
                                    RAM[HL--] = A; // LD (HL-),A
                                    yield return null;      // 1 cycle to update RAM
                                }
                                else if (opcode == 0xC2)
                                {
                                    // JP NZ,a16
                                    ushort imm = imm16();
                                    yield return null;  // 2 cycles for the imm16
                                    yield return null;
                                    if ((F & 0x80) == 0x00)
                                    {
                                        PC = imm;
                                        yield return null;  // 1 cycle to set the PC
                                    }
                                }
                                else if (opcode == 0xD2)
                                {
                                    // JP NC,a16
                                    ushort imm = imm16();
                                    yield return null;  // 2 cycles for the imm16
                                    yield return null;
                                    if ((F & 0x10) == 0x00)
                                    {
                                        PC = imm;
                                        yield return null;  // 1 cycle to set the PC
                                    }
                                }
                                else if (opcode == 0xE2)
                                {
                                    RAM[0xff00 + C] = A;    // LD (C+0xff00),A
                                    yield return null;      // 1 cycle to update RAM
                                }
                                else if (opcode == 0xF2)
                                {
                                    A = RAM[0xff00 + C];    // LD A,(C+0xff00)
                                    yield return null;      // 1 cycle to get data from RAM
                                }
                                break;
                            case 0x03:
                                if (opcode == 0x03)
                                {
                                    BC++;
                                    yield return null;  // 1 cycle for 16b operation
                                }
                                else if (opcode == 0x13)
                                {
                                    DE++;
                                    yield return null;  // 1 cycle for 16b operation
                                }
                                else if (opcode == 0x23)
                                {
                                    HL++;
                                    yield return null;  // 1 cycle for 16b operation
                                }
                                else if (opcode == 0x33)
                                {
                                    SP++;
                                    yield return null;  // 1 cycle for 16b operation
                                }
                                else if (opcode == 0xC3)
                                {
                                    ushort imm = imm16();
                                    yield return null;  // 2 cycles for the imm16
                                    yield return null;
                                    PC = imm;
                                    yield return null;  // 1 cycle to set the PC
                                }
                                else if (opcode == 0xF3)    // DI
                                {
                                    // TODO:  User manual makes no mention of it, but
                                    // the reverse engineered manual says that DI doesn't take
                                    // effect until the next instruction.  This could cause issues
                                    nextIME = false;
                                    IME = false;
                                }
                                else opcodeName = "undefined";
                                break;
                            case 0x04:
                                if (opcode == 0x04) inc8(ref B);
                                else if (opcode == 0x14) inc8(ref D);
                                else if (opcode == 0x24) inc8(ref H);
                                else if (opcode == 0x34)
                                {
                                    byte temp = RAM[HL];
                                    yield return null;  // 1 cycle to read RAM
                                    inc8(ref temp);
                                    RAM[HL] = temp;
                                    yield return null;  // 1 cycle to write RAM
                                }
                                else if (opcode == 0xC4)
                                {
                                    // CALL NZ,a16
                                    ushort imm = imm16();
                                    yield return null;
                                    yield return null;
                                    if ((F & 0x80) == 0x00)
                                    {
                                        yield return null;  // 1 clock internal delay
                                        RAM[--SP] = (byte)((PC >> 8) & 0xff);
                                        yield return null;
                                        RAM[--SP] = (byte)(PC & 0xff);
                                        yield return null;  // 1 clock to set PC
                                        PC = imm;
                                    }
                                }
                                else if (opcode == 0xD4)
                                {
                                    // CALL NC,a16
                                    ushort imm = imm16();
                                    yield return null;
                                    yield return null;
                                    if ((F & 0x10) == 0x00)
                                    {
                                        yield return null;  // 1 clock internal delay
                                        RAM[--SP] = (byte)((PC >> 8) & 0xff);
                                        yield return null;
                                        RAM[--SP] = (byte)(PC & 0xff);
                                        yield return null;  // 1 clock to set PC
                                        PC = imm;
                                    }
                                }
                                else opcodeName = "undefined";
                                break;
                            case 0x05:
                                if (opcode == 0x05) dec8(ref B);
                                else if (opcode == 0x15) dec8(ref D);
                                else if (opcode == 0x25) dec8(ref H);
                                else if (opcode == 0x35)
                                {
                                    byte temp = RAM[HL];
                                    yield return null;
                                    dec8(ref temp);
                                    RAM[HL] = temp;
                                    yield return null;
                                }
                                else if (opcode == 0xC5)
                                {
                                    yield return null;
                                    yield return null;
                                    push(BC);
                                    yield return null;
                                }
                                else if (opcode == 0xD5)
                                {
                                    yield return null;
                                    yield return null;
                                    push(DE);
                                    yield return null;
                                }
                                else if (opcode == 0xE5)
                                {
                                    yield return null;
                                    yield return null;
                                    push(HL);
                                    yield return null;
                                }
                                else if (opcode == 0xF5)
                                {
                                    yield return null;
                                    yield return null;
                                    push(AF);
                                    yield return null;
                                }
                                break;
                            case 0x06:
                                yield return null;
                                if (opcode == 0x06) B = imm8();         // LD B,d8
                                else if (opcode == 0x16) D = imm8();    // LD D,d8
                                else if (opcode == 0x26) H = imm8();    // LD H,d8
                                else if (opcode == 0x36)
                                {
                                    RAM[HL] = imm8();  // LD (HL),d8
                                    yield return null;
                                }
                                else if (opcode == 0xC6) add(imm8());   // ADD,d8
                                else if (opcode == 0xD6) sub(imm8());   // SUB,d8
                                else if (opcode == 0xE6) and(imm8());   // AND,d8
                                else if (opcode == 0xF6) or(imm8());    // OR,d8
                                break;
                            case 0x07:
                                if (opcode == 0x07)     // RLCA
                                {
                                    rlc(ref A);
                                    F &= 0x7F;
                                }
                                else if (opcode == 0x17) // RLA
                                {
                                    rl(ref A);
                                    F &= 0x7F;
                                }
                                else if (opcode == 0x27)    // DAA
                                {
                                    int correction = 0;
                                    if (((A & 0x0f) > 9 && (F & 0x40) == 0x00) || (F & 0x20) == 0x20) correction |= 0x06;
                                    if ((A > 0x99 && (F & 0x40) == 0x00) || (F & 0x10) == 0x10) correction |= 0x60;

                                    if ((F & 0x40) == 0x40) A = (byte)(A - correction);
                                    else A = (byte)(A + correction);

                                    F &= 0x40;
                                    if (A == 0) F |= 0x80;
                                    if ((correction & 0x60) == 0x60) F |= 0x10;
                                }
                                else if (opcode == 0x37)    // SCF
                                {
                                    F &= 0x80;
                                    F |= 0x10;
                                }
                                else
                                {
                                    // C7 = RST 00h, D7 = RST 10h, E7 = RST 20h, F7 = RST 30h
                                    ushort rst = (ushort)((opcode & 0xf0) - 0xC0);

                                    yield return null;  // 1 clock internal delay
                                    RAM[--SP] = (byte)((PC >> 8) & 0xff);
                                    yield return null;
                                    RAM[--SP] = (byte)(PC & 0xff);
                                    yield return null;  // 1 clock to set PC
                                    PC = rst;
                                }
                                break;
                            case 0x08:
                                if (opcode == 0x08)
                                {
                                    // LD (nn),SP
                                    var addr = imm16();
                                    yield return null;
                                    yield return null;
                                    RAM[addr] = (byte)(SP & 0xff);
                                    yield return null;
                                    RAM[addr + 1] = (byte)((SP >> 8) & 0xff);
                                    yield return null;
                                }
                                else if (opcode == 0x18)
                                {
                                    sbyte imm = imm8s();
                                    yield return null;
                                    PC = (ushort)(imm + PC);
                                    yield return null;
                                }
                                else if (opcode == 0x28)
                                {
                                    sbyte imm = imm8s();
                                    yield return null;
                                    if ((F & 0x80) == 0x80)
                                    {
                                        PC = (ushort)(imm + PC);
                                        yield return null;
                                    }
                                }
                                else if (opcode == 0x38)
                                {
                                    sbyte imm = imm8s();
                                    yield return null;
                                    if ((F & 0x10) == 0x10)
                                    {
                                        PC = (ushort)(imm + PC);
                                        yield return null;
                                    }
                                }
                                else if (opcode == 0xC8)
                                {
                                    // RET Z
                                    yield return null;
                                    if ((F & 0x80) == 0x80)
                                    {
                                        PC = pop();
                                        yield return null;
                                        yield return null;
                                        yield return null;
                                    }
                                }
                                else if (opcode == 0xD8)
                                {
                                    // RET C
                                    yield return null;
                                    if ((F & 0x10) == 0x10)
                                    {
                                        PC = pop();
                                        yield return null;
                                        yield return null;
                                        yield return null;
                                    }
                                }
                                else if (opcode == 0xE8)
                                {
                                    var imm = imm8s();
                                    yield return null;
                                    SP = addsp(imm);   // ADD SP,imm8s
                                    yield return null;
                                    yield return null;
                                }
                                else if (opcode == 0xF8)
                                {
                                    var imm = imm8s();
                                    yield return null;
                                    HL = addsp(imm);   // LD HL,SP+imm8s
                                    yield return null;
                                }
                                break;
                            case 0x09:
                                if (opcode < 0x49) yield return null;
                                if (opcode == 0x09) add16(BC);      // ADD HL,BC
                                else if (opcode == 0x19) add16(DE); // ADD HL,DE
                                else if (opcode == 0x29) add16(HL); // ADD HL,HL
                                else if (opcode == 0x39) add16(SP); // ADD HL,SP
                                else if (opcode == 0xC9)
                                {
                                    PC = pop();// RET
                                    yield return null;
                                    yield return null;
                                    yield return null;
                                }
                                else if (opcode == 0xD9)    // RETI
                                {
                                    // TODO:  User manual makes no mention of it, but
                                    // the reverse engineered manual says that EI doesn't take
                                    // effect until the next instruction.  This could cause issues
                                    PC = pop();
                                    nextIME = true;
                                    yield return null;
                                    yield return null;
                                    yield return null;
                                }
                                else if (opcode == 0xE9) PC = HL;   // JMP HL
                                else if (opcode == 0xF9)
                                {
                                    SP = HL;   // LD SP,HL
                                    yield return null;
                                }
                                break;
                            case 0x0A:
                                if (opcode == 0x0A)
                                {
                                    A = RAM[BC];        // LD A,(BC)
                                    yield return null;
                                }
                                else if (opcode == 0x1A)
                                {
                                    A = RAM[DE];   // LD A,(DE)
                                    yield return null;
                                }
                                else if (opcode == 0x2A)
                                {
                                    A = RAM[HL++]; // LD A,(HL++)
                                    yield return null;
                                }
                                else if (opcode == 0x3A)
                                {
                                    A = RAM[HL--]; // LD A,(HL--)
                                    yield return null;
                                }
                                else if (opcode == 0xCA)
                                {
                                    // JP Z,a16
                                    ushort imm = imm16();
                                    yield return null;
                                    yield return null;
                                    if ((F & 0x80) == 0x80)
                                    {
                                        PC = imm;
                                        yield return null;
                                    }
                                }
                                else if (opcode == 0xDA)
                                {
                                    // JP C,a16
                                    ushort imm = imm16();
                                    yield return null;
                                    yield return null;
                                    if ((F & 0x10) == 0x10)
                                    {
                                        PC = imm;
                                        yield return null;
                                    }
                                }
                                else if (opcode == 0xEA)
                                {
                                    var imm = imm16();
                                    yield return null;
                                    yield return null;
                                    RAM[imm] = A;
                                    yield return null;
                                }
                                else if (opcode == 0xFA)
                                {
                                    var imm = imm16();
                                    yield return null;
                                    yield return null;
                                    A = RAM[imm];
                                    yield return null;
                                }
                                break;
                            case 0x0B:
                                if (opcode < 0x4B) yield return null;
                                if (opcode == 0x0B) BC--;
                                else if (opcode == 0x1B) DE--;
                                else if (opcode == 0x2B) HL--;
                                else if (opcode == 0x3B) SP--;
                                else if (opcode == 0xCB) throw new Exception();
                                else if (opcode == 0xFB)
                                {
                                    // EI is handled at the end of this statement, after IME is set
                                }
                                else opcodeName = "undefined";
                                break;
                            case 0x0C:
                                if (opcode == 0x0C) inc8(ref C);
                                else if (opcode == 0x1C) inc8(ref E);
                                else if (opcode == 0x2C) inc8(ref L);
                                else if (opcode == 0x3C) inc8(ref A);
                                else if (opcode == 0xCC)
                                {
                                    // CALL Z,a16
                                    ushort imm = imm16();
                                    yield return null;
                                    yield return null;
                                    if ((F & 0x80) == 0x80)
                                    {
                                        yield return null;  // 1 clock internal delay
                                        RAM[--SP] = (byte)((PC >> 8) & 0xff);
                                        yield return null;
                                        RAM[--SP] = (byte)(PC & 0xff);
                                        yield return null;  // 1 clock to set PC
                                        PC = imm;
                                    }
                                }
                                else if (opcode == 0xDC)
                                {
                                    // CALL C,a16
                                    ushort imm = imm16();
                                    yield return null;
                                    yield return null;
                                    if ((F & 0x10) == 0x10)
                                    {
                                        yield return null;  // 1 clock internal delay
                                        RAM[--SP] = (byte)((PC >> 8) & 0xff);
                                        yield return null;
                                        RAM[--SP] = (byte)(PC & 0xff);
                                        yield return null;  // 1 clock to set PC
                                        PC = imm;
                                    }
                                }
                                else opcodeName = "undefined";
                                break;
                            case 0x0D:
                                if (opcode == 0x0D) dec8(ref C);
                                else if (opcode == 0x1D) dec8(ref E);
                                else if (opcode == 0x2D) dec8(ref L);
                                else if (opcode == 0x3D) dec8(ref A);
                                else if (opcode == 0xCD)
                                {
                                    yield return null;  // 2 clocks to load imm16
                                    yield return null;
                                    ushort imm = imm16();
                                    yield return null;  // 1 clock internal delay
                                    RAM[--SP] = (byte)((PC >> 8) & 0xff);
                                    yield return null;
                                    RAM[--SP] = (byte)(PC & 0xff);
                                    yield return null;  // 1 clock to set PC
                                    PC = imm;
                                }
                                else opcodeName = "undefined";
                                break;
                            case 0x0E:
                                yield return null;  // these all take an extra cycle to load imm8
                                if (opcode == 0x0E) C = imm8();         // LD C,d8
                                else if (opcode == 0x1E) E = imm8();    // LD E,d8
                                else if (opcode == 0x2E) L = imm8();    // LD L,d8
                                else if (opcode == 0x3E) A = imm8();    // LD A,d8
                                else if (opcode == 0xCE) adc(imm8());   // ADC d8
                                else if (opcode == 0xDE) sbc(imm8());   // SBC d8
                                else if (opcode == 0xEE) xor(imm8());   // XOR d8
                                else if (opcode == 0xFE) cp(imm8());    // CP d8
                                break;
                            case 0x0F:
                                if (opcode == 0x0F)         // RRCA
                                {
                                    rrc(ref A);
                                    F &= 0x7F;
                                }
                                else if (opcode == 0x1F)    // RRA
                                {
                                    rr(ref A);
                                    F &= 0x7F;
                                }
                                else if (opcode == 0x2F)    // CPL
                                {
                                    A = (byte)~A;
                                    F |= 0b01100000;
                                }
                                else if (opcode == 0x3F)    // CCF
                                {
                                    F &= 0b10010000;
                                    F ^= 0b00010000;
                                }
                                else
                                {
                                    // 0xCF = RST 08h, 0xDF = RST 18h, 0xEF = RST 28h, 0xFF = RST 38h
                                    ushort rst = (ushort)((opcode & 0xf0) - 0xC0 + 0x08);

                                    yield return null;  // 1 clock internal delay
                                    RAM[--SP] = (byte)((PC >> 8) & 0xff);
                                    yield return null;
                                    RAM[--SP] = (byte)(PC & 0xff);
                                    yield return null;  // 1 clock to set PC
                                    PC = rst;
                                }
                                break;
                        }
                        break;
                }
            }

            if (!string.IsNullOrEmpty(opcodeName))
            {
                Console.WriteLine("Unimplemented opcode " + opcodeName);
            }

            IME = nextIME;
            if (opcode == 0xFB)
                nextIME = true; // EI
        }
    }
}
