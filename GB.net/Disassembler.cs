using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GB
{
    public class Disassembler
    {
        public Disassembler(string file, int startPosition = 0x150)
        {
            if (!file.EndsWith(".gb")) return;

            using (BinaryReader reader = new BinaryReader(new FileStream(file, FileMode.Open)))
            {
                reader.BaseStream.Position = startPosition;

                for (int i = 0; i < 20; i++)
                {
                    ParseOpcode(reader);
                }
            }
        }

        private void ParseOpcode(BinaryReader reader)
        {
            int memory = (int)reader.BaseStream.Position;
            byte opcode = reader.ReadByte();
            int advance = 0;

            if (opcode == 0xCB)
            {
                byte cb = reader.ReadByte();

                Console.WriteLine($"0x${memory.ToString("X")}: Found CB opcode 0x{cb.ToString("X")}");
            }
            else
            {
                string opcodeName = string.Empty;
                byte lowNibble = (byte)(opcode & 0x0F);

                switch (opcode & 0xF0)
                {
                    case 0x40:
                    case 0x50:
                    case 0x60:
                    case 0x70:
                        if (opcode == 0x76) opcodeName = "HALT";
                        else opcodeName = "LD";
                        break;
                    case 0x80:
                        if (lowNibble < 8) opcodeName = "ADD";
                        else opcodeName = "ADC";
                        break;
                    case 0x90:
                        if (lowNibble < 8) opcodeName = "SUB";
                        else opcodeName = "SBC";
                        break;
                    case 0xA0:
                        if (lowNibble < 8) opcodeName = "AND";
                        else opcodeName = "XOR";
                        break;
                    case 0xB0:
                        if (lowNibble < 8) opcodeName = "OR";
                        else opcodeName = "CP";
                        break;
                    default:
                        switch (lowNibble)
                        {
                            case 0x00:
                                if (opcode == 0x00) opcodeName = "NOP";
                                else if (opcode == 0x10) opcodeName = "STOP";
                                else if (opcode < 0x40) opcodeName = "JR";
                                else if (opcode < 0xE0) opcodeName = "RET";
                                else opcodeName = "LDH";
                                break;
                            case 0x01:
                                if (opcode < 0x40) opcodeName = "LD";
                                else opcodeName = "POP";
                                break;
                            case 0x02:
                                if (opcode < 0x40) opcodeName = "LD";
                                else if (opcode < 0xE0) opcodeName = "JP";
                                else opcodeName = "LD";
                                break;
                            case 0x03:
                                if (opcode < 0x40) opcodeName = "INC";
                                else if (opcode == 0xC3) opcodeName = "JP";
                                else if (opcode == 0xF3) opcodeName = "DI";
                                else opcodeName = "undefined";
                                break;
                            case 0x04:
                                if (opcode < 0x40) opcodeName = "INC";
                                else if (opcode < 0xE0) opcodeName = "CALL";
                                else opcodeName = "undefined";
                                break;
                            case 0x05:
                                if (opcode < 0x40) opcodeName = "DEC";
                                else opcodeName = "PUSH";
                                break;
                            case 0x06:
                                if (opcode < 0x40) opcodeName = "LD";
                                else if (opcode == 0xC6) opcodeName = "ADD";
                                else if (opcode == 0xD6) opcodeName = "SUB";
                                else if (opcode == 0xE6) opcodeName = "AND";
                                else if (opcode == 0xF6) opcodeName = "OR";
                                break;
                            case 0x07:
                                if (opcode == 0x07) opcodeName = "RLCA";
                                else if (opcode == 0x17) opcodeName = "RLA";
                                else if (opcode == 0x27) opcodeName = "DAA";
                                else if (opcode == 0x37) opcodeName = "SCF";
                                else opcodeName = "RST";
                                break;
                            case 0x08:
                                if (opcode == 0x08) opcodeName = "LD";
                                else if (opcode < 0x40) opcodeName = "JR";
                                else if (opcode < 0xE0) opcodeName = "RET";
                                else if (opcode == 0xE8) opcodeName = "ADD";
                                else opcodeName = "LD";
                                break;
                            case 0x09:
                                if (opcode < 0x40) opcodeName = "ADD";
                                else if (opcode == 0xC9) opcodeName = "RET";
                                else if (opcode == 0xD9) opcodeName = "RETI";
                                else if (opcode == 0xE9) opcodeName = "JMP";
                                else if (opcode == 0xF9) opcodeName = "LD";
                                break;
                            case 0x0A:
                                if (opcode < 0x40) opcodeName = "LD";
                                else if (opcode < 0xE0) opcodeName = "JP";
                                else opcodeName = "LD";
                                break;
                            case 0x0B:
                                if (opcode < 0x40) opcodeName = "DEC";
                                else if (opcode == 0xCB) opcodeName = "Special Opcode CB";
                                else if (opcode == 0xFB) opcodeName = "EI";
                                else opcodeName = "undefined";
                                break;
                            case 0x0C:
                                if (opcode < 0x40) opcodeName = "INC";
                                else if (opcode < 0xE0) opcodeName = "CALL";
                                else opcodeName = "undefined";
                                break;
                            case 0x0D:
                                if (opcode < 0x40) opcodeName = "DEC";
                                else if (opcode == 0xCD) opcodeName = "CALL";
                                else opcodeName = "undefined";
                                break;
                            case 0x0E:
                                if (opcode < 0x40) opcodeName = "LD";
                                else if (opcode == 0xCE) opcodeName = "ADC";
                                else if (opcode == 0xDE) opcodeName = "SBC";
                                else if (opcode == 0xEE) opcodeName = "XOR";
                                else opcodeName = "CP";
                                break;
                            case 0x0F:
                                if (opcode == 0x0F) opcodeName = "RRCA";
                                else if (opcode == 0x1F) opcodeName = "RRA";
                                else if (opcode == 0x2F) opcodeName = "CPL";
                                else if (opcode == 0x3F) opcodeName = "CCF";
                                else opcodeName = "RST";
                                break;
                        }
                        break;
                }

                Console.WriteLine($"0x${memory.ToString("X")}: Found opcode 0x{opcode.ToString("X2")} {opcodeName}");
            }

            switch (opcode)
            {
                case 0x18:
                case 0x20:
                case 0x30:
                case 0x28:
                case 0x38:
                case 0xE8:
                case 0xF8:
                    // read in 8 bits of signed data (r8)
                    advance = 1;
                    break;
                case 0xE0:
                case 0xF0:
                    // read in 8 bits of unsigned data (a8)
                    advance = 1;
                    break;
                case 0x06:
                case 0x0E:
                case 0x16:
                case 0x1E:
                case 0x26:
                case 0x2E:
                case 0x36:
                case 0x3E:
                case 0xC6:
                case 0xCE:
                case 0xD6:
                case 0xDE:
                case 0xE6:
                case 0xEE:
                case 0xF6:
                case 0xFE:
                    // read in 8 bits of data (d8)
                    advance = 1;
                    break;
                case 0x01:
                case 0x11:
                case 0x21:
                case 0x31:
                    // read in 16 bits of data (d16)
                    advance = 2;
                    break;
                case 0x08:
                case 0xC2:
                case 0xC3:
                case 0xC4:
                case 0xCA:
                case 0xCC:
                case 0xCD:
                case 0xD2:
                case 0xD4:
                case 0xDA:
                case 0xDC:
                case 0xEA:
                case 0xFA:
                    // read in 16 bits of data (a16)
                    advance = 2;
                    break;
            }

            reader.ReadBytes(advance);
        }
    }
}
