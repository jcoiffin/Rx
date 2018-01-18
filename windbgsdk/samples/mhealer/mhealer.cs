//----------------------------------------------------------------------------
//
// Sample of monitoring an application for compatibility problems
// and automatically correcting them.
//
// Copyright (C) Microsoft Corporation, 2005.
//
//----------------------------------------------------------------------------

using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

using Microsoft.Debuggers.DbgEng;

namespace healer
{
    [ StructLayout( LayoutKind.Sequential )]  
    internal class OSVersionInfo 
    {
        public OSVersionInfo()
        {
            this.OSVersionInfoSize = (uint) Marshal.SizeOf( this );
            OSVersionInfo.GetVersionEx(this);
        }

        [DllImport( "kernel32" )]
        private static extern bool GetVersionEx( [In, Out] OSVersionInfo osvi);

        public readonly uint OSVersionInfoSize;
        public uint dwMajorVersion; 
        public uint dwMinorVersion; 
        public uint dwBuildNumber; 
        public uint dwPlatformId;
        [ MarshalAs( UnmanagedType.ByValTStr, SizeConst=128 )]
        public string versionString = null;
    }



    internal class HealerExcept : Exception
    {
        public HealerExcept(int exitCode, string msg) : base(msg)
        {
            this.exitCode = exitCode;
        }


        public int ExitCode
        {
            get
            {
                return this.exitCode;
            }
        }

        private readonly int exitCode;
    }

	public class Sample : IDisposable
	{
        [STAThread]
        static int Main(string[] args)
        {
            try
            {
                using (Sample s = new Sample(args))
                {
                    s.EventLoop();
                }
            }
            catch (Exception e)
            {
                Console.Out.WriteLine("Uhandled exception: {0}", e);
            }
            
            return 0;
        }

		public Sample(string[] args)
		{
            this.osVer = new OSVersionInfo();

            #region Parse commandline

            uint i = 0;

            for (; i < args.Length; ++i)
            {
                if (args[i] == "-plat")
                {
                    if ((args.Length - i) < 2)
                    {
                        throw new HealerExcept(1, "-plat missing argument");
                    }

                    ++i;
                    this.osVer.dwPlatformId = UInt32.Parse(args[i]);
                    this.needVerBps = true;                    
                }
                else if (args[i] == "-v")
                {
                    //this.verbose = true;
                }
                else if (args[i] == "-ver")
                {
                    if ((args.Length - i) < 2)
                    {
                        throw new HealerExcept(1, "-ver missing argument");
                    }

                    ++i;
                 
                    string[] ver = args[i].Split('.');
                    this.osVer.dwMajorVersion = UInt32.Parse(ver[0]);
                    this.osVer.dwMinorVersion = UInt32.Parse(ver[1]);
                    this.osVer.dwBuildNumber = UInt32.Parse(ver[2]);
                }
                else if (args[i] == "-y")
                {
                    if ((args.Length - i) < 2)
                    {
                        throw new HealerExcept(1, "-y missing argument");
                    }

                    ++i;
                 
                    this.sympath = args[i];
                }
                else
                {
                    this.commandLine = "\"" + args[i] + "\"";
                }
            }

            if (this.commandLine == null)
            {
                throw new HealerExcept(1, "No application command line given");
            }

            #endregion

            // TODO make sure we're running on an x86 machine

            this.client = new DebugClient();
            this.symbols = new DebugSymbols(this.client);
            this.ctrl = new DebugControl(this.client);
            this.regs = new DebugRegisters(this.client);
            this.data = new DebugDataSpaces(this.client);

            if (this.sympath != null)
            {
                this.symbols.SetSymbolPath(this.sympath);
            }

//            this.client.DebugEventInterestMask = 
//                DebugEvents.BreakpointEvent | DebugEvents.ExceptionEvent | DebugEvents.CreateProcessEvent |
//                DebugEvents.LoadModuleEvent | DebugEvents.SessionStatusEvent;

            this.client.BreakpointHit += new EventHandler< BreakpointEventArgs >(HandleBp);
            this.client.ExceptionHit += new EventHandler< ExceptionEventArgs >(HandleExcept);
            this.client.ProcessCreated += new EventHandler< CreateProcessEventArgs >(HandleCreateProc);
            this.client.ModuleLoaded += new EventHandler< LoadModuleEventArgs >(HandleLoadMod);
            this.client.SessionStatusChanged += new EventHandler< SessionStatusEventArgs >(HandleSessionStatus);

            this.client.CreateProcess(this.commandLine, CreateProcessOptions.DebugOnlyThisProcess);

            this.versionNumber = (this.osVer.dwMajorVersion & 0xFF) | ((this.osVer.dwMinorVersion & 0xFF) << 8);

            if (this.osVer.dwPlatformId == 2 /* VER_PLATFORM_WIN32_NT */)
            {
                this.versionNumber |= (this.osVer.dwBuildNumber & 0x7fff) << 16;
            }
            else
            {
                this.versionNumber |= 0x80000000;
            }
        }


        public void EventLoop()
        {
            for (;;)
            {                
                if (this.ctrl.WaitForEvent() == false)
                {
                    throw new HealerExcept(1, "WaitForEvent() returned an unexpected false value");
                }
                
                if (MessageBox.Show(
                        "An unusual event occurred.  Ignore it?",
                        "Unhandled Event",
                        MessageBoxButtons.YesNo) == DialogResult.No)
                {
                    throw new HealerExcept(1, "Unhandled event");
                }

                this.ctrl.ExecutionStatus = DebugStatus.GoHandled;
            }
        }


        public void ApplyExePatches(string imageName, ulong baseOffset)
        {
            if (imageName == null)
            {
                imageName = "<Unknown>";
            }

            Console.Out.WriteLine("HEALER: Executable '{0}' loaded at {1}", imageName, baseOffset.ToString("x"));
        }

        public void ApplyDllPatches(string imageName, ulong baseOffset)
        {
            if (imageName == null)
            {
                imageName = "<Unknown>";
            }

            Console.Out.WriteLine("HEALER: DLL '{0}' loaded at {1}", imageName, baseOffset.ToString("x"));
        }

        public void AddVersionBps()
        {
            this.getVersionBp = this.ctrl.AddBreakpoint(BreakpointMode.Code);
            this.getVersionExBp = this.ctrl.AddBreakpoint(BreakpointMode.Code);

            this.getVersionBp.SetOffsetExpression("kernel32!GetVersion");
            this.getVersionExBp.SetOffsetExpression("kernel32!GetVersionEx");
            this.getVersionBp.Options = BreakpointOptions.Enabled;
            this.getVersionExBp.Options = BreakpointOptions.Enabled;

            this.getVersionRetBp = this.ctrl.AddBreakpoint(BreakpointMode.Code);
            this.getVersionExRetBp = this.ctrl.AddBreakpoint(BreakpointMode.Code);
        }


        #region IDisposable Members

        public void Dispose()
        {
            this.client.BreakpointHit -= new EventHandler<BreakpointEventArgs>(HandleBp);
            this.client.ExceptionHit -= new EventHandler<ExceptionEventArgs>(HandleExcept);
            this.client.ProcessCreated -= new EventHandler<CreateProcessEventArgs>(HandleCreateProc);
            this.client.ModuleLoaded -= new EventHandler<LoadModuleEventArgs>(HandleLoadMod);
            this.client.SessionStatusChanged -= new EventHandler<SessionStatusEventArgs>(HandleSessionStatus);

            if (this.client != null) this.client.Dispose();
            if (this.symbols != null) this.symbols.Dispose();            
            if (this.regs != null) this.regs.Dispose();
            if (this.ctrl != null) this.ctrl.Dispose();
            if (this.data != null) this.data.Dispose();            
        }

        #endregion

        private OSVersionInfo osVer;
        private readonly bool needVerBps = false;
        //private readonly bool verbose = false;
        private readonly string sympath = null;
        private readonly string commandLine = null;
        private readonly uint versionNumber;
        private ulong osVerOffset;
        private uint eaxIndex;

        private Microsoft.Debuggers.DbgEng.DebugClient client = null;
        private Microsoft.Debuggers.DbgEng.DebugSymbols symbols = null;
        private Microsoft.Debuggers.DbgEng.DebugControl ctrl = null;
        private Microsoft.Debuggers.DbgEng.DebugRegisters regs = null;
        private Microsoft.Debuggers.DbgEng.DebugDataSpaces data = null;

        private Microsoft.Debuggers.DbgEng.DebugBreakpoint getVersionBp = null;
        private Microsoft.Debuggers.DbgEng.DebugBreakpoint getVersionExBp = null;
        private Microsoft.Debuggers.DbgEng.DebugBreakpoint getVersionRetBp = null;
        private Microsoft.Debuggers.DbgEng.DebugBreakpoint getVersionExRetBp = null;

        #region Debug events handlers

        private void HandleBp(object sender, BreakpointEventArgs args)
        {       
            uint bpId = args.Breakpoint.Id;
            
            if (bpId == this.getVersionBp.Id)
            {
                this.getVersionRetBp.Offset = this.ctrl.ReturnOffset;
                this.getVersionRetBp.Options = BreakpointOptions.Enabled;
            }
            else if (bpId == this.getVersionExBp.Id)
            {
                ulong[] ptrs = new ulong[1];
                this.data.ReadPointersVirtual(1, this.regs.StackOffset + 4, ptrs);
                this.osVerOffset = ptrs[0];

                this.getVersionExRetBp.Offset = this.ctrl.ReturnOffset;
                this.getVersionExRetBp.Options = BreakpointOptions.Enabled;
            }
            else if (bpId == this.getVersionRetBp.Id)
            {
                this.getVersionRetBp.Options &= ~BreakpointOptions.Enabled;               

                this.regs.SetValue(this.eaxIndex, this.versionNumber);

                Console.Out.WriteLine("HEALER: GetVersion returns {0}", this.versionNumber.ToString("x"));
            }
            else if (bpId == this.getVersionExRetBp.Id)
            {
                this.getVersionExRetBp.Options &= ~BreakpointOptions.Enabled;

                int sizeOfOsVer = Marshal.SizeOf(this.osVer);

                byte[] rawOsVer = new byte[sizeOfOsVer];

                GCHandle gchOsVer = GCHandle.Alloc(this.osVer, GCHandleType.Pinned);

                try
                {
                    Marshal.Copy((IntPtr) gchOsVer, rawOsVer, 0, sizeOfOsVer);
                    this.data.WriteVirtual(this.osVerOffset, rawOsVer);
                }
                finally
                {
                    gchOsVer.Free();
                }

                Console.Out.WriteLine("HEALER: GetVersionEx returns {0}", this.versionNumber.ToString("x"));
            }
            else
            {
                return; 
            }

            args.EventStatus = DebugStatus.Go;
        }

        private void HandleExcept(object sender, ExceptionEventArgs args)
        {
            /*for (uint regIndex = 0; regIndex < this.regs.NumberRegisters; ++regIndex)
            {
                RegisterDescription regDesc = this.regs.GetDescription(regIndex);
                DebugValue regVal = this.regs.GetValue(regIndex);

                Console.Out.WriteLine("Register: name={0}, valuetype={1}, registertype={2}, value={3}",
                    regDesc.Name, regDesc.Type, regDesc.RegisterType, (UInt64) regVal);
            }*/


            if (!args.IsFirstChance || 
                (args.ExceptionRecord.ExceptionCode != ExceptionCode.PrivilegedInstruction /* STATUS_PRIVILEGED_INSTRUCTION */))
            {
                return;
            }

            byte[] instr = new byte[1];
            this.data.ReadVirtual(args.ExceptionRecord.ExceptionAddress, instr);

            if ((instr[0] != 0xFB) && (instr[0] != 0xFA))
            {
                return;
            }

            instr[0] = 0x90;

            this.data.WriteVirtual(args.ExceptionRecord.ExceptionAddress, instr);

            args.EventStatus = DebugStatus.GoHandled;
        }

        private void HandleCreateProc(object sender, CreateProcessEventArgs args)
        {            
            this.ApplyExePatches(args.ImageName, args.BaseOffset);

            if (this.needVerBps)
            {
                this.AddVersionBps();
            }

            args.EventStatus = DebugStatus.Go;
        }

        private void HandleLoadMod(object sender, LoadModuleEventArgs args)
        {      
            this.ApplyDllPatches(args.ImageName, args.BaseOffset);
            args.EventStatus = DebugStatus.Go;
        }

        private void HandleSessionStatus(object sender, SessionStatusEventArgs args)
        {
            if (args.SessionStatus != SessionStatus.Active)
            {
                return;
            }

            this.eaxIndex = this.regs.GetIndexByName("eax");
        }

        #endregion
    }
}
