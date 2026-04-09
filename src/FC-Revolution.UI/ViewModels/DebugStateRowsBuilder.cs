using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal static class DebugStateRowsBuilder
{
    public static void BuildRegisterRows(ObservableCollection<MemoryPageRow> target, CoreDebugState state)
    {
        BuildCategoryRows(target, state, "registers");
    }

    public static void BuildPpuRows(ObservableCollection<MemoryPageRow> target, CoreDebugState state)
    {
        BuildCategoryRows(target, state, "video");
    }

    public static void BuildDisasmRows(
        ObservableCollection<MemoryPageRow> target,
        CoreDebugState state,
        ushort startAddress,
        IReadOnlyList<byte> opcodes)
    {
        target.Clear();
        for (var row = 0; row < DebugViewModel.DisasmPageSize / 3; row++)
        {
            var cells = new List<MemoryCellItem>();
            for (var column = 0; column < 3; column++)
            {
                var index = row * 3 + column;
                var address = unchecked((ushort)(startAddress + index));
                var opcode = index < opcodes.Count ? opcodes[index] : (byte)0;
                var prefix = address == state.InstructionPointer ? ">" : " ";
                cells.Add(new MemoryCellItem
                {
                    Address = address,
                    DisplayAddress = $"${address:X4}",
                    Value = $"{prefix}{opcode:X2} {Disasm(opcode)}"
                });
            }

            target.Add(new MemoryPageRow
            {
                RowHeader = $"L{row + 1:X1}",
                Cells = cells
            });
        }
    }

    private static void BuildCategoryRows(
        ObservableCollection<MemoryPageRow> target,
        CoreDebugState state,
        string category)
    {
        target.Clear();
        foreach (var section in state.Sections.Where(section => string.Equals(section.Category, category, StringComparison.OrdinalIgnoreCase)))
        {
            target.Add(new MemoryPageRow
            {
                RowHeader = section.SectionId,
                Cells = section.Values
                    .Select((value, index) => new MemoryCellItem
                    {
                        Address = (ushort)index,
                        DisplayAddress = value.Label,
                        Value = $"{value.Label}:{value.Value}"
                    })
                    .ToList()
            });
        }
    }

    private static string Disasm(byte opcode) => opcode switch
    {
        0x00 => "BRK", 0x01 => "ORA (zp,X)", 0x05 => "ORA zp", 0x06 => "ASL zp",
        0x08 => "PHP", 0x09 => "ORA #", 0x0A => "ASL A", 0x0D => "ORA abs",
        0x10 => "BPL", 0x18 => "CLC", 0x20 => "JSR abs", 0x21 => "AND (zp,X)",
        0x24 => "BIT zp", 0x25 => "AND zp", 0x26 => "ROL zp", 0x28 => "PLP",
        0x29 => "AND #", 0x2A => "ROL A", 0x2C => "BIT abs", 0x2D => "AND abs",
        0x30 => "BMI", 0x38 => "SEC", 0x40 => "RTI", 0x41 => "EOR (zp,X)",
        0x45 => "EOR zp", 0x46 => "LSR zp", 0x48 => "PHA", 0x49 => "EOR #",
        0x4A => "LSR A", 0x4C => "JMP abs", 0x4D => "EOR abs", 0x50 => "BVC",
        0x58 => "CLI", 0x60 => "RTS", 0x61 => "ADC (zp,X)", 0x65 => "ADC zp",
        0x66 => "ROR zp", 0x68 => "PLA", 0x69 => "ADC #", 0x6A => "ROR A",
        0x6C => "JMP (ind)", 0x6D => "ADC abs", 0x70 => "BVS", 0x78 => "SEI",
        0x81 => "STA (zp,X)", 0x84 => "STY zp", 0x85 => "STA zp", 0x86 => "STX zp",
        0x88 => "DEY", 0x8A => "TXA", 0x8C => "STY abs", 0x8D => "STA abs",
        0x8E => "STX abs", 0x90 => "BCC", 0x91 => "STA (zp),Y", 0x94 => "STY zp,X",
        0x95 => "STA zp,X", 0x96 => "STX zp,Y", 0x98 => "TYA", 0x99 => "STA abs,Y",
        0x9A => "TXS", 0x9D => "STA abs,X", 0xA0 => "LDY #", 0xA1 => "LDA (zp,X)",
        0xA2 => "LDX #", 0xA4 => "LDY zp", 0xA5 => "LDA zp", 0xA6 => "LDX zp",
        0xA8 => "TAY", 0xA9 => "LDA #", 0xAA => "TAX", 0xAC => "LDY abs",
        0xAD => "LDA abs", 0xAE => "LDX abs", 0xB0 => "BCS", 0xB1 => "LDA (zp),Y",
        0xB4 => "LDY zp,X", 0xB5 => "LDA zp,X", 0xB6 => "LDX zp,Y", 0xB8 => "CLV",
        0xB9 => "LDA abs,Y", 0xBA => "TSX", 0xBC => "LDY abs,X", 0xBD => "LDA abs,X",
        0xBE => "LDX abs,Y", 0xC0 => "CPY #", 0xC1 => "CMP (zp,X)", 0xC4 => "CPY zp",
        0xC5 => "CMP zp", 0xC6 => "DEC zp", 0xC8 => "INY", 0xC9 => "CMP #",
        0xCA => "DEX", 0xCC => "CPY abs", 0xCD => "CMP abs", 0xCE => "DEC abs",
        0xD0 => "BNE", 0xD5 => "CMP zp,X", 0xD6 => "DEC zp,X", 0xD8 => "CLD",
        0xD9 => "CMP abs,Y", 0xDD => "CMP abs,X", 0xDE => "DEC abs,X", 0xE0 => "CPX #",
        0xE4 => "CPX zp", 0xE5 => "SBC zp", 0xE6 => "INC zp", 0xE8 => "INX",
        0xE9 => "SBC #", 0xEA => "NOP", 0xEC => "CPX abs", 0xED => "SBC abs",
        0xEE => "INC abs", 0xF0 => "BEQ", 0xF5 => "SBC zp,X", 0xF6 => "INC zp,X",
        0xF8 => "SED", 0xF9 => "SBC abs,Y", 0xFD => "SBC abs,X", 0xFE => "INC abs,X",
        _ => "???"
    };
}
