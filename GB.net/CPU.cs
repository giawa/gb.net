﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace GB
{
    class Cartridge
    {
        public byte[] data;

        public Cartridge(string file)
        {
            using (BinaryReader reader = new BinaryReader(new FileStream(file, FileMode.Open)))
            {
                data = reader.ReadBytes((int)reader.BaseStream.Length);
            }
        }
    }

    class CPU
    {
        public Memory RAM { get; private set; }

        public CPU(Memory ram)
        {
            RAM = ram;
        }

        public Cartridge Cartridge { get; private set; }

        public void LoadCartridge(Cartridge cart)
        {
            Cartridge = cart;
            PC = 0;
            RAM.Reset();

            byte[] cartData = new byte[32768];
            Array.Copy(cart.data, cartData, Math.Min(32768, cart.data.Length));
            RAM.Cartridge = cartData;
        }

        public void SetPC(ushort pc)
        {
            PC = pc;
            RAM[0xff50] = 1;    // disable bootloader
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

        private bool IME = true;

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
                    byte temp = RAM[HL];
                    ExtendedOpcode(opcode, ref temp);
                    RAM[HL] = temp;
                    break;
                case 7:
                    ExtendedOpcode(opcode, ref A);
                    break;
            }

            yield return null;
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
                case 0x78:
                    // BIT 7
                    F &= 0b00010000;    // clears Z, N and leaves C unaffected
                    F |= 0b00100000;    // sets H
                    F |= (byte)(~register & 0x80);  // already in the correct position, no shift
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

        private void call(ushort nn)
        {
            push(PC);
            PC = nn;
        }

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

        List<ushort> pcHistory = new List<ushort>();

        public IEnumerable CreateStateMachine()
        {
            while (running)
            {
                if (PC == 0xc501)
                {
                    //Console.WriteLine("Passed");
                }
                if (PC == 0xc8b3)
                {
                    //Console.WriteLine("stop");
                }

                pcHistory.Add(PC);
                byte opcode = RAM[PC++];
                //Console.WriteLine(PC.ToString());
                byte lowNibble = (byte)(opcode & 0x0f);
                string opcodeName = string.Empty;

                byte op2 = GetCommonOp2((byte)(opcode & 0x07));

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
                        case 0x70:
                            if (opcode == 0x76) opcodeName = "HALT";
                            else ld(opcode, op2);
                            break;
                        case 0x80:
                            if (lowNibble < 8) add(op2);
                            else adc(op2);
                            break;
                        case 0x90:
                            if (lowNibble < 8) sub(op2);
                            else sbc(op2);
                            break;
                        case 0xA0:
                            if (lowNibble < 8) and(op2);
                            else xor(op2);
                            break;
                        case 0xB0:
                            if (lowNibble < 8) or(op2);
                            else cp(op2);   // CP n
                            break;
                        default:
                            switch (lowNibble)
                            {
                                case 0x00:
                                    if (opcode == 0x00) ;// opcodeName = "NOP";
                                    else if (opcode == 0x10)
                                    {
                                        // TODO:  Need to wait for a button press
                                        running = false;
                                        Console.WriteLine();
                                        for (int i = 0; i < 100 && i < pcHistory.Count; i++)
                                            Console.WriteLine("0x" + pcHistory[pcHistory.Count - 1 - i].ToString("X"));
                                    }
                                    else if (opcode == 0x20)
                                    {
                                        // JR NZ,r8
                                        sbyte imm8 = imm8s();
                                        if ((F & 0x80) == 0x00) PC = (ushort)(imm8 + PC);
                                    }
                                    else if (opcode == 0x30)
                                    {
                                        // JR NC,r8
                                        sbyte imm8 = imm8s();
                                        if ((F & 0x10) == 0x00) PC = (ushort)(imm8 + PC);
                                    }
                                    else if (opcode == 0xC0)
                                    {
                                        // RET NZ
                                        if ((F & 0x80) == 0x00) PC = pop();
                                    }
                                    else if (opcode == 0xD0)
                                    {
                                        // RET NZ
                                        if ((F & 0x10) == 0x00) PC = pop();
                                    }
                                    else if (opcode == 0xE0) RAM[0xff00 + imm8()] = A;  // LDH (0xff00+n),A
                                    else if (opcode == 0xF0) A = RAM[0xff00 + imm8()];  // LDH A,(0xff00+n)
                                    break;
                                case 0x01:
                                    if (opcode == 0x01) BC = imm16();       // LD BC,d16
                                    else if (opcode == 0x11) DE = imm16();  // LD DE,d16
                                    else if (opcode == 0x21) HL = imm16();  // LD HL,d16
                                    else if (opcode == 0x31) SP = imm16();  // LD SP,d16
                                    else if (opcode == 0xC1) BC = pop();
                                    else if (opcode == 0xD1) DE = pop();
                                    else if (opcode == 0xE1) HL = pop();
                                    else if (opcode == 0xF1) AF = pop();
                                    break;
                                case 0x02:
                                    if (opcode == 0x02) RAM[BC] = A;        // LD (BC),A
                                    else if (opcode == 0x12) RAM[DE] = A;   // LD (DE),A
                                    else if (opcode == 0x22) RAM[HL++] = A; // LD (HL+),A
                                    else if (opcode == 0x32) RAM[HL--] = A; // LD (HL-),A
                                    else if (opcode == 0xC2)
                                    {
                                        // JP NZ,a16
                                        ushort imm = imm16();
                                        if ((F & 0x80) == 0x00) PC = imm;
                                    }
                                    else if (opcode == 0xD2)
                                    {
                                        // JP NC,a16
                                        ushort imm = imm16();
                                        if ((F & 0x10) == 0x00) PC = imm;
                                    }
                                    else if (opcode == 0xE2) RAM[0xff00 + C] = A;   // LD (C+0xff00),A
                                    else if (opcode == 0xF2) A = RAM[0xff00 + C];   // LD A,(C+0xff00)
                                    break;
                                case 0x03:
                                    if (opcode == 0x03) BC++;
                                    else if (opcode == 0x13) DE++;
                                    else if (opcode == 0x23) HL++;
                                    else if (opcode == 0x33) SP++;
                                    else if (opcode == 0xC3) PC = imm16();
                                    else if (opcode == 0xF3)    // DI
                                    {
                                        // TODO:  User manual makes no mention of it, but
                                        // the reverse engineered manual says that DI doesn't take
                                        // effect until the next instruction.  This could cause issues
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
                                        inc8(ref temp);
                                        RAM[HL] = temp;
                                    }
                                    else if (opcode == 0xC4)
                                    {
                                        // CALL NZ,a16
                                        ushort imm = imm16();
                                        if ((F & 0x80) == 0x00) call(imm);
                                    }
                                    else if (opcode == 0xD4)
                                    {
                                        // CALL NC,a16
                                        ushort imm = imm16();
                                        if ((F & 0x10) == 0x00) call(imm);
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
                                        dec8(ref temp);
                                        RAM[HL] = temp;
                                    }
                                    else if (opcode == 0xC5) push(BC);
                                    else if (opcode == 0xD5) push(DE);
                                    else if (opcode == 0xE5) push(HL);
                                    else if (opcode == 0xF5) push(AF);
                                    break;
                                case 0x06:
                                    if (opcode == 0x06) B = imm8();         // LD B,d8
                                    else if (opcode == 0x16) D = imm8();    // LD D,d8
                                    else if (opcode == 0x26) H = imm8();    // LD H,d8
                                    else if (opcode == 0x36) RAM[HL] = imm8();  // LD (HL),d8
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
                                    else if (opcode == 0x27) opcodeName = "DAA";
                                    else if (opcode == 0x37)            // SCF
                                    {
                                        F &= 0x80;
                                        F |= 0x10;
                                    }
                                    else if (opcode == 0xC7) call(0x0000);  // RST 00h
                                    else if (opcode == 0xD7) call(0x0010);  // RST 10h
                                    else if (opcode == 0xE7) call(0x0020);  // RST 20h
                                    else if (opcode == 0xF7) call(0x0030);  // RST 30h
                                    break;
                                case 0x08:
                                    if (opcode == 0x08)
                                    {
                                        // LD (nn),SP
                                        var addr = imm16();
                                        RAM[addr] = (byte)(SP & 0xff);
                                        RAM[addr + 1] = (byte)((SP >> 8) & 0xff);
                                    }
                                    else if (opcode == 0x18)
                                    {
                                        sbyte imm8 = imm8s();
                                        PC = (ushort)(imm8 + PC);
                                    }
                                    else if (opcode == 0x28)
                                    {
                                        sbyte imm8 = imm8s();
                                        if ((F & 0x80) == 0x80) PC = (ushort)(imm8 + PC);
                                    }
                                    else if (opcode == 0x38)
                                    {
                                        sbyte imm8 = imm8s();
                                        if ((F & 0x10) == 0x10) PC = (ushort)(imm8 + PC);
                                    }
                                    else if (opcode == 0xC8)
                                    {
                                        // RET Z
                                        if ((F & 0x80) == 0x80) PC = pop();
                                    }
                                    else if (opcode == 0xD8)
                                    {
                                        // RET C
                                        if ((F & 0x10) == 0x10) PC = pop();
                                    }
                                    else if (opcode == 0xE8) SP = addsp(imm8s());   // ADD SP,imm8s
                                    else if (opcode == 0xF8) HL = addsp(imm8s());   // LD HL,SP+imm8s
                                    break;
                                case 0x09:
                                    if (opcode == 0x09) add16(BC);      // ADD HL,BC
                                    else if (opcode == 0x19) add16(DE); // ADD HL,DE
                                    else if (opcode == 0x29) add16(HL); // ADD HL,HL
                                    else if (opcode == 0x39) add16(SP); // ADD HL,SP
                                    else if (opcode == 0xC9) PC = pop();// RET
                                    else if (opcode == 0xD9)    // RETI
                                    {
                                        // TODO:  User manual makes no mention of it, but
                                        // the reverse engineered manual says that EI doesn't take
                                        // effect until the next instruction.  This could cause issues
                                        PC = pop();
                                        IME = true;
                                    }
                                    else if (opcode == 0xE9) PC = HL;   // JMP HL
                                    else if (opcode == 0xF9) SP = HL;   // LD SP,HL
                                    break;
                                case 0x0A:
                                    if (opcode == 0x0A) A = RAM[BC];        // LD A,(BC)
                                    else if (opcode == 0x1A) A = RAM[DE];   // LD A,(DE)
                                    else if (opcode == 0x2A) A = RAM[HL++]; // LD A,(HL++)
                                    else if (opcode == 0x3A) A = RAM[HL--]; // LD A,(HL--)
                                    else if (opcode == 0xCA)
                                    {
                                        // JP Z,a16
                                        ushort imm = imm16();
                                        if ((F & 0x80) == 0x80) PC = imm;
                                    }
                                    else if (opcode == 0xDA)
                                    {
                                        // JP C,a16
                                        ushort imm = imm16();
                                        if ((F & 0x10) == 0x10) PC = imm;
                                    }
                                    else if (opcode == 0xEA) RAM[imm16()] = A;
                                    else if (opcode == 0xFA) A = RAM[imm16()];
                                    break;
                                case 0x0B:
                                    if (opcode == 0x0B) BC--;
                                    else if (opcode == 0x1B) DE--;
                                    else if (opcode == 0x2B) HL--;
                                    else if (opcode == 0x3B) SP--;
                                    else if (opcode == 0xCB) throw new Exception();
                                    else if (opcode == 0xFB)    // EI
                                    {
                                        // TODO:  User manual makes no mention of it, but
                                        // the reverse engineered manual says that EI doesn't take
                                        // effect until the next instruction.  This could cause issues
                                        IME = true;
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
                                        if ((F & 0x80) == 0x80) call(imm);
                                    }
                                    else if (opcode == 0xDC)
                                    {
                                        // CALL C,a16
                                        ushort imm = imm16();
                                        if ((F & 0x10) == 0x10) call(imm);
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
                                        ushort nextPC = imm16();
                                        call(nextPC);
                                    }
                                    else opcodeName = "undefined";
                                    break;
                                case 0x0E:
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
                                    else if (opcode == 0xCF) call(0x0008);  // RST 08h
                                    else if (opcode == 0xDF) call(0x0018);  // RST 18h
                                    else if (opcode == 0xEF) call(0x0028);  // RST 28h
                                    else if (opcode == 0xFF) call(0x0038);  // RST 38h
                                    break;
                            }
                            break;
                    }
                }

                if (!string.IsNullOrEmpty(opcodeName))
                {
                    Console.WriteLine("Unimplemented opcode " + opcodeName);
                }
                
                yield return null;
            }
        }
    }
}
