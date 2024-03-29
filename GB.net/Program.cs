﻿using ImGuiNET;
using OpenGL;
using SDL2;
using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;

namespace GB
{
    class Program
    {
        private static int _width = 1280, _height = 720;

        private static bool mouseleft, mouseright;
        private static int mouseX, mouseY, mouseWheel;

        private static Memory ram;
        private static CPU cpu;
        private static LCD lcd;
        private static Timer timer;
        private static Sound sound;

        static void Main(string[] args)
        {
            if (SDL.SDL_Init(SDL.SDL_INIT_EVERYTHING) < 0)
            {
                Console.WriteLine("SDL failed to init.");
                return;
            }

            SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_DOUBLEBUFFER, 1);
            SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_DEPTH_SIZE, 24);

            SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_CONTEXT_MAJOR_VERSION, 3);

            // set to the gameboy resolution with 4x scaling for now
            IntPtr window = SDL.SDL_CreateWindow("GameBoy Emulator", 128, 128, _width, _height, SDL.SDL_WindowFlags.SDL_WINDOW_OPENGL | SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN | SDL.SDL_WindowFlags.SDL_WINDOW_RESIZABLE);

            if (window == IntPtr.Zero)
            {
                Console.WriteLine("SDL could not create a window.");
                return;
            }

            IntPtr context = SDL.SDL_GL_CreateContext(window);

            if (context == IntPtr.Zero)
            {
                Console.WriteLine("SDL could not create a valid OpenGL context.");
                return;
            }

            SDL.SDL_GL_SetSwapInterval(1);

            SDL.SDL_Event sdlEvent;
            bool running = true;

            Gui.Init();
            Gui.Reshape(_width, _height);
            var imguiContext = ImGui.CreateContext();
            ImGui.StyleColorsDark();
            Gui.RecreateFontDeviceTexture();

            bool demoWindow = true;
            byte joypad = 0x0f;

            int scaling = 2, speed = 1;

            while (running)
            {
                //try
                {
                    while (SDL.SDL_PollEvent(out sdlEvent) != 0)
                    {
                        //if (ram != null) ram.JoyPad = 0;

                        if (sdlEvent.type == SDL.SDL_EventType.SDL_QUIT)
                        {
                            running = false;
                        }
                        else if (sdlEvent.type == SDL.SDL_EventType.SDL_KEYDOWN && ram != null)
                        {
                            if (sdlEvent.key.keysym.sym == SDL.SDL_Keycode.SDLK_LEFT) ram.JoyPad |= Memory.JoyPadButton.Left;
                            if (sdlEvent.key.keysym.sym == SDL.SDL_Keycode.SDLK_UP) ram.JoyPad |= Memory.JoyPadButton.Up;
                            if (sdlEvent.key.keysym.sym == SDL.SDL_Keycode.SDLK_DOWN) ram.JoyPad |= Memory.JoyPadButton.Down;
                            if (sdlEvent.key.keysym.sym == SDL.SDL_Keycode.SDLK_RIGHT) ram.JoyPad |= Memory.JoyPadButton.Right;
                            if (sdlEvent.key.keysym.sym == SDL.SDL_Keycode.SDLK_a) ram.JoyPad |= Memory.JoyPadButton.A;
                            if (sdlEvent.key.keysym.sym == SDL.SDL_Keycode.SDLK_s) ram.JoyPad |= Memory.JoyPadButton.B;
                            if (sdlEvent.key.keysym.sym == SDL.SDL_Keycode.SDLK_RETURN) ram.JoyPad |= Memory.JoyPadButton.Start;
                            if (sdlEvent.key.keysym.sym == SDL.SDL_Keycode.SDLK_BACKSLASH) ram.JoyPad |= Memory.JoyPadButton.Select;
                        }
                        else if (sdlEvent.type == SDL.SDL_EventType.SDL_KEYUP)
                        {
                            if (sdlEvent.key.keysym.sym == SDL.SDL_Keycode.SDLK_LEFT) ram.JoyPad &= ~Memory.JoyPadButton.Left;
                            if (sdlEvent.key.keysym.sym == SDL.SDL_Keycode.SDLK_UP) ram.JoyPad &= ~Memory.JoyPadButton.Up;
                            if (sdlEvent.key.keysym.sym == SDL.SDL_Keycode.SDLK_DOWN) ram.JoyPad &= ~Memory.JoyPadButton.Down;
                            if (sdlEvent.key.keysym.sym == SDL.SDL_Keycode.SDLK_RIGHT) ram.JoyPad &= ~Memory.JoyPadButton.Right;
                            if (sdlEvent.key.keysym.sym == SDL.SDL_Keycode.SDLK_a) ram.JoyPad &= ~Memory.JoyPadButton.A;
                            if (sdlEvent.key.keysym.sym == SDL.SDL_Keycode.SDLK_s) ram.JoyPad &= ~Memory.JoyPadButton.B;
                            if (sdlEvent.key.keysym.sym == SDL.SDL_Keycode.SDLK_RETURN) ram.JoyPad &= ~Memory.JoyPadButton.Start;
                            if (sdlEvent.key.keysym.sym == SDL.SDL_Keycode.SDLK_BACKSLASH) ram.JoyPad &= ~Memory.JoyPadButton.Select;
                        }
                        else if (sdlEvent.type == SDL.SDL_EventType.SDL_MOUSEBUTTONDOWN)
                        {
                            if (sdlEvent.button.button == 1) mouseleft = true;
                            if (sdlEvent.button.button == 3) mouseright = true;
                        }
                        else if (sdlEvent.type == SDL.SDL_EventType.SDL_MOUSEBUTTONUP)
                        {
                            if (sdlEvent.button.button == 1) mouseleft = false;
                            if (sdlEvent.button.button == 3) mouseright = false;
                        }
                        else if (sdlEvent.type == SDL.SDL_EventType.SDL_MOUSEMOTION)
                        {
                            mouseX = sdlEvent.motion.x;
                            mouseY = sdlEvent.motion.y;
                        }
                        else if (sdlEvent.type == SDL.SDL_EventType.SDL_MOUSEWHEEL)
                        {
                            mouseWheel = sdlEvent.wheel.y;
                        }
                        else if (sdlEvent.type == SDL.SDL_EventType.SDL_WINDOWEVENT)
                        {
                            switch (sdlEvent.window.windowEvent)
                            {
                                case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_RESIZED:
                                    OnReshape(sdlEvent.window.data1, sdlEvent.window.data2);
                                    break;
                            }
                        }
                    }

                    // set up the OpenGL viewport and clear both the color and depth bits
                    Gl.Viewport(0, 0, _width, _height);
                    Gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

                    UpdateImGuiInput();

                    ImGui.NewFrame();
                    //ImGui.ShowDemoWindow(ref demoWindow);
                    if (ImGui.BeginMainMenuBar())
                    {
                        if (ImGui.BeginMenu("File"))
                        {
                            if (ImGui.MenuItem("Open File", "Ctrl+O"))
                            {
                                showOpenDialog = true;
                                openDialog = new FileDialog();
                                ImGui.SetNextWindowPos(new Vector2(100, 100));
                            }
                            
                            ImGui.Separator();
                            if (ImGui.MenuItem("Exit"))
                            {
                                running = false;
                            }
                            ImGui.EndMenu();
                        }
                        if (cpu != null && ImGui.BeginMenu("Emulation"))
                        {
                            if (ImGui.MenuItem("1x Scaling", null, (scaling == 1)))
                            {
                                scaling = 1;
                            }
                            if (ImGui.MenuItem("2x Scaling", null, (scaling == 2)))
                            {
                                scaling = 2;
                            }
                            if (ImGui.MenuItem("3x Scaling", null, (scaling == 3)))
                            {
                                scaling = 3;
                            }
                            if (ImGui.MenuItem("4x Scaling", null, (scaling == 4)))
                            {
                                scaling = 4;
                            }
                            ImGui.Separator();
                            if (ImGui.MenuItem("1x Speed", null, (speed == 1)))
                            {
                                speed = 1;
                                SDL.SDL_GL_SetSwapInterval(1);
                            }
                            if (ImGui.MenuItem("2x Speed", null, (speed == 2)))
                            {
                                speed = 2;
                                SDL.SDL_GL_SetSwapInterval(1);
                            }
                            if (ImGui.MenuItem("Maximum Speed", null, (speed == 0)))
                            {
                                speed = 0;
                                SDL.SDL_GL_SetSwapInterval(0);
                            }
                            ImGui.Separator();
                            if (ImGui.MenuItem("Export State"))
                            {
                                // run the processor until the next opcode is reached so that we have finished up the current opcode
                                int opcode = cpu.CurrentOpcode;

                                int fail = 0;
                                while (opcode == cpu.CurrentOpcode && fail++ < 17556)
                                {
                                    lcd.Tick1MHz();
                                    ram.Tick1MHz();
                                    timer.Tick1MHz();
                                    cpu.Tick1MHz();
                                }

                                if (fail >= 17556)
                                {
                                    Console.WriteLine("CPU may be stopped or halted, could not export contents.");
                                    continue;
                                }

                                using (BinaryWriter output = new BinaryWriter(new FileStream("dump.bin", FileMode.Create)))
                                {
                                    output.Write((int)0);    // version 0
                                    lcd.ExportState(0, output);
                                    output.Write(timer.TimerInterrupt);
                                    ram.ExportState(0, output);
                                    cpu.Cartridge.ExportState(0, output);
                                    cpu.ExportState(0, output);

                                    Console.WriteLine("Dumped state to dump.bin");
                                }
                            }
                            if (ImGui.MenuItem("Import State"))
                            {
                                using (BinaryReader input = new BinaryReader(new FileStream("dump.bin", FileMode.Open)))
                                {
                                    var cartridge = cpu.Cartridge;
                                    cpu = new CPU(ram); // clear any internal state
                                    cpu.LoadCartridge(cartridge);
                                    var version = input.ReadInt32();
                                    lcd.ImportState(version, input);
                                    timer.TimerInterrupt = input.ReadBoolean();
                                    ram.ImportState(version, input);
                                    cpu.Cartridge.ImportState(version, input);
                                    cpu.ImportState(version, input);

                                    Console.WriteLine("Imported state from dump.bin");
                                }
                            }
                            ImGui.EndMenu();
                        }
                        ImGui.EndMainMenuBar();
                    }

                    if (showOpenDialog)
                    {
                        if (openDialog.DisplayFileDialog("Open File", new string[] { ".gb" }, Directory.GetCurrentDirectory(), "."))
                        {
                            showOpenDialog = false;

                            if (openDialog.IsOk && !string.IsNullOrEmpty(openDialog.FullPath))
                            {
                                Console.WriteLine("Open file: " + openDialog.FullPath);

                                if (File.Exists(openDialog.FullPath))
                                {
                                    Cartridge gamecart = new Cartridge(openDialog.FullPath);
                                    ram = new Memory();
                                    sound = new Sound(ram);
                                    ram.Sound = sound;
                                    timer = new Timer(ram);
                                    ram.Timer = timer;
                                    cpu = new CPU(ram);
                                    lcd = new LCD(ram);
                                    ram.LCD = lcd;
                                    cpu.LoadCartridge(gamecart);
                                    cpu.SetPC(0x100);   // skip to the entry point of the game
                                }
                            }
                        }
                    }

                    // run a full frame of gameboy data
                    if (cpu != null)
                    {
                        bool frameReady = false;

                        Stopwatch watch = Stopwatch.StartNew();

                        for (int i = 0; i < Math.Max(speed, 1); i++)
                        {
                            frameReady = false;
                            int ticks = 0;

                            while (!frameReady && ticks < 17556)
                            {
                                // support GBC double speed
                                if ((/*ram[0xff4d]*/ram.SpecialPurpose[0x14d] & 0x80) == 0x80)
                                {
                                    // register all interrupts
                                    if (timer.TimerInterrupt)
                                    {
                                        ram[0xff0f] |= 0x04;
                                        timer.TimerInterrupt = false;
                                    }
                                    timer.Tick1MHz();
                                    ram.Tick1MHz();
                                    cpu.Tick1MHz();
                                }

                                // register all interrupts
                                if (lcd.VBlankInterrupt)
                                {
                                    ram[0xff0f] |= 0x01;
                                    lcd.VBlankInterrupt = false;
                                }
                                if (timer.TimerInterrupt)
                                {
                                    ram[0xff0f] |= 0x04;
                                    timer.TimerInterrupt = false;
                                }
                                if (lcd.StatInterrupt)
                                {
                                    ram[0xff0f] |= 0x02;
                                    lcd.StatInterrupt = false;
                                }
                                var ff00 = (byte)(ram.GetJoyPad() & 0x0f);
                                var xorff00 = ff00 ^ joypad;
                                if (xorff00 != 0 && (joypad & xorff00) != 0) ram[0xff0f] |= 0x10;
                                joypad = ff00;

                                frameReady = lcd.Tick1MHz();
                                ram.Tick1MHz();
                                sound.Tick1MHz();
                                timer.Tick1MHz();   // TODO:  "If a TMA write is executed with the same 
                                                    // timing of TMA being transferred to TIMA, then the TMA
                                                    // write goes to TIMA as well" (p 26 Gameboy Dev Manual)

                                
                                cpu.Tick1MHz();

                                ticks++;
                            }

                            if (ticks >= 17556 && !frameReady) ;
                            else if (!frameReady)
                            {
                                cpu = null;
                                break;
                            }
                        }

                        watch.Stop();
                        //Console.WriteLine("Frame took {0}ms {1} ticks", watch.ElapsedMilliseconds, watch.ElapsedTicks);

                        // did the program terminate?
                        if (frameReady)
                        {
                            //lcd.DumpTiles((ram[0xff40] & 0x10) == 0x10 ? 0x8000 : 0x8800);
                            //Console.WriteLine("Running at {0}MHz", ticks / 1000000.0 * 60);

                            if (frameTexture != null) frameTexture.Dispose();
                            var bitmapHandle = GCHandle.Alloc(lcd.backgroundTexture, GCHandleType.Pinned);
                            frameTexture = new Texture(bitmapHandle.AddrOfPinnedObject(), 160, 144, PixelFormat.Rgba, PixelInternalFormat.Rgba);
                            bitmapHandle.Free();

                            /*if (bgTexture != null) bgTexture.Dispose();
                            bitmapHandle = GCHandle.Alloc(lcd.DumpTiles(0x8000), GCHandleType.Pinned);
                            bgTexture = new Texture(bitmapHandle.AddrOfPinnedObject(), 128, 128, PixelFormat.Rgba, PixelInternalFormat.Rgba);
                            bitmapHandle.Free();

                            if (bgTexture2 != null) bgTexture2.Dispose();
                            bitmapHandle = GCHandle.Alloc(lcd.DumpTiles(0x8800), GCHandleType.Pinned);
                            bgTexture2 = new Texture(bitmapHandle.AddrOfPinnedObject(), 128, 128, PixelFormat.Rgba, PixelInternalFormat.Rgba);
                            bitmapHandle.Free();*/
                            //Console.WriteLine("Frame after {0}ms", watch.ElapsedMilliseconds);
                        }
                    }

                    if (frameTexture != null)
                    {
                        ImGui.Begin("Emulator");

                        ImGui.Image((IntPtr)frameTexture.TextureID, new Vector2(160 * scaling, 144 * scaling));

                        ImGui.End();
                    }

                    if (bgTexture != null)
                    {
                        ImGui.Begin("0x8000 Tiles");

                        ImGui.Image((IntPtr)bgTexture.TextureID, new Vector2(128 * 2, 128 * 2));

                        ImGui.End();
                    }

                    if (bgTexture2 != null)
                    {
                        ImGui.Begin("0x8800 Tiles");

                        ImGui.Image((IntPtr)bgTexture2.TextureID, new Vector2(128 * 2, 128 * 2));

                        ImGui.End();
                    }

                    ImGui.Render();

                    // try to render imgui
                    var drawData = ImGui.GetDrawData();
                    Gui.RenderImDrawData(drawData);

                    SDL.SDL_GL_SwapWindow(window);
                }
                /*catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }*/
            }

            Gui.Dispose();

            SDL.SDL_GL_DeleteContext(context);
            SDL.SDL_DestroyWindow(window);
        }

        private static Texture frameTexture, bgTexture, bgTexture2;

        private static bool showOpenDialog = false;
        private static FileDialog openDialog = new FileDialog();

        private static void OnReshape(int width, int height)
        {
            _width = width;
            _height = height;
            Gui.Reshape(width, height);
        }

        private static void UpdateImGuiInput()
        {
            ImGuiIOPtr io = ImGui.GetIO();

            Vector2 mousePosition = new Vector2(mouseX, mouseY);

            io.MouseDown[0] = mouseleft;// leftPressed || snapshot.IsMouseDown(MouseButton.Left);
            io.MouseDown[1] = mouseright;// rightPressed || snapshot.IsMouseDown(MouseButton.Right);
            io.MouseDown[2] = false;// middlePressed || snapshot.IsMouseDown(MouseButton.Middle);
            io.MousePos = mousePosition;
            io.MouseWheel = mouseWheel;

            mouseWheel = 0;
        }
    }
}
