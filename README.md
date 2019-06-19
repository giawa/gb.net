# GB.net

GB.net is a Game Boy emulator written in C#.

The main goal of this project is to learn about creating emulators.  I've already made a [Chip-8 emulator/interpreter](https://github.com/giawa/chip8) and the Game Boy seemed like the next logical step.  I've also recorded nearly the entire development of this emulator (about 40 hours so far) and will post condensed versions as YouTube videos at a later date.

TODO:
* Support custom key bindings.
* Support more memory controllers (including save files for battery backed RAMs)
* Add sound support
* Use something other than vertical sync for frame/CPU timing
* Pass more acceptance tests
* (Maybe) Add Game Boy Color support
* (Maybe) Add debugging tools

## License
Check the included [LICENSE.md](https://github.com/giawa/gb.net/blob/master/LICENSE.md) file for the license associated with this code.

## Building the Project
This project includes a .sln and .csproj for Visual Studio (tested in 2019).  The output is a .NET Core 2.1 binary, which should be usable on Windows, Mac or Linux.  The project uses a custom compiled version of [ImgUI.NET](https://github.com/giawa/ImGui.NET), [SDL2](https://github.com/flibitijibibo/SDL2-CS) and [OpenGL4CSharp](https://github.com/giawa/opengl4csharp).

## Performance

An i5-4790k desktop computer can run the emulator at approximately 16x speed when compiled in Release mode.  In Debug mode the performance drops to approximately 1.5x speed.

The 1x/2x speed uses vertical sync as the method for timing frames.  This means that the game will only run at ~1x speed when your monitor refresh rate is 60Hz.

## Running The Emulator

Requirements:

* .NET Core 2.1
* SDL2 libraries for your platform must be installed
* GPU with support for OpenGL 4

Run the Emulator and then load a ROM using the 'File' menu.

## Key Bindings

| Game Boy | Key        |
| -------- | ---------- |
| Dpad     | Arrow keys |
| A        | A          |
| B        | S          |
| Start    | Return     |
| Select   | Backslash  |

## Accuracy Comparison

For accuracy comparison the emulator is tested against the most common Blargg test ROMs, as well as the full Mooneye test suite.

### Blargg's tests

| Test              | GB.net     | 
| ----------------- | ---------- | 
| cpu instrs        | :+1:       | 
| dmg sound 2       |            | 
| instr timing      | :+1:       | 
| mem timing 2      | :+1:       | 
| oam bug 2         |            | 

### Mooneye GB acceptance tests

| Test                    | GB.net     | 
| ----------------------- | ---------- | 
| add sp e timing         | :+1:       | 
| boot div dmg0           | :x:        | 
| boot div dmgABCmgb      | :x:        |
| boot div S              | :x:        | 
| boot div2 S             | :x:        | 
| boot hwio dmg0          | :x:        |
| boot hwio dmgABCmgb     | :x:        | 
| boot hwio S             | :x:        | 
| boot regs dmg0          | :x:        | 
| boot regs dmgABC        | :+1:       | 
| boot regs mgb           | :x:        | 
| boot regs sgb           | :x:        | 
| boot regs sgb2          | :x:        | 
| call timing             | :+1:       | 
| call timing2            | :+1:       | 
| call cc_timing          | :+1:       |
| call cc_timing2         | :+1:       | 
| di timing GS            | :x:        |
| div timing              | :x:        |
| ei sequence             | :+1:       | 
| ei timing               | :+1:       |
| halt ime0 ei            | :+1:       | 
| halt ime0 nointr_timing | :x:        | 
| halt ime1 timing        | :+1:       |
| halt ime1 timing2 GS    | :x:        | 
| if ie registers         | :+1:       | 
| intr timing             | :+1:       |
| jp timing               | :+1:       | 
| jp cc timing            | :+1:       | 
| ld hl sp e timing       | :+1:       | 
| oam dma_restart         | :+1:       | 
| oam dma start           | :x:        | 
| oam dma timing          | :+1:       | 
| pop timing              | :x:        | 
| push timing             | :+1:       | 
| rapid di ei             | :+1:       |
| ret timing              | :x:        | 
| ret cc timing           | :+1:       | 
| reti timing             | :x:        | 
| reti intr timing        | :+1:       |
| rst timing              | :+1:       |

#### Bits

| Test           | GB.net     | 
| -------------- | ---------- | 
| mem oam        | :+1:       |
| reg f          | :+1:       |
| unused_hwio GS | :+1:       |

#### Instructions

| Test                        | GB.net     | 
| --------------------------- | ---------- | 
| daa                         | :+1:       | 

#### Interrupt handling

| Test                        | GB.net     | 
| --------------------------- | ---------- |
| ie push                     | :+1:       | 

#### OAM DMA

| Test                        | GB.net     | 
| --------------------------- | ---------- | 
| basic                       | :+1:       | 
| reg_read                    | :+1:       | 
| sources dmgABCmgbS          | :+1:       | 

#### PPU

| Test                        | GB.net     | 
| --------------------------- | ---------- | 
| hblank ly scx timing GS     | :x:        | 
| intr 1 2 timing GS          | :x:        |
| intr 2 0 timing             | :x:        | 
| intr 2 mode0 timing         | :x:        | 
| intr 2 mode3 timing         | :x:        | 
| intr 2 oam ok timing        | :x:        | 
| intr 2 mode0 timing sprites | :x:        | 
| lcdon timing dmgABCmgbS     | :x:        | 
| lcdon write timing GS       | :x:        |
| stat irq blocking           | :x:        |
| stat lyc onoff              | :x:        |
| vblank stat intr GS         | :x:        | 

#### Serial

| Test                        | GB.net     | 
| --------------------------- | ---------- |
| boot sclk align dmgABCmgb   | :x:        |

#### Timer

| Test                 | GB.net     | 
| -------------------- | ---------- | 
| div write            | :+1:       | 
| rapid toggle         | :x:        | 
| tim00 div trigger    | :+1:       | 
| tim00                | :+1:       | 
| tim01 div trigger    | :+1:       | 
| tim01                | :+1:       | 
| tim10 div trigger    | :+1:       | 
| tim10                | :+1:       |
| tim11 div trigger    | :+1:       |
| tim11                | :+1:       |
| tima reload          | :+1:       | 
| tima write reloading | :x:        |
| tma write reloading  | :x:        | 

### Mooneye GB emulator-only tests

#### MBC1

| Test              | GB.net     | 
| ----------------- | ---------- | 
| bits ram en       | :+1:       |
| rom 512Kb         | :+1:       | 
| rom 1Mb           | :+1:       | 
| rom 2Mb           | :+1:       | 
| rom 4Mb           | :+1:       | 
| rom 8Mb           | :+1:       | 
| rom 16Mb          | :+1:       | 
| ram 64Kb          | :+1:       | 
| ram 256Kb         | :+1:       | 
| multicart rom 8Mb | :x:        | 

### Mooneye GB manual tests

| Test            | GB.net     | 
| --------------- | ---------- | 
| sprite priority | :+1:       | 
