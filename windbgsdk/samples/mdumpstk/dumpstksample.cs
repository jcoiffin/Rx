//----------------------------------------------------------------------------
//
// Simple example of how to open a dump file and get its stack.
//
// This is not a debugger extension.  It is a tool that can be used to replace
// the debugger.
//
// Copyright (C) Microsoft Corporation, 2005.
//
//----------------------------------------------------------------------------

using System;

using Microsoft.Debuggers.DbgEng;

namespace dumpstk
{
	/// <summary>
	/// 
	/// </summary>
	sealed class DumpStkSample : IDisposable
	{
        [STAThread]
        static int Main(string[] args)
        {
            try
            {                        
                DumpStkSample sample = new DumpStkSample(args);                
                sample.DumpStack();
            }           
            catch (DumpStkExcept e)
            {
                Console.Error.WriteLine(e.Message);
                return e.ExitCode;
            }
            catch(Exception e)
            {
                Console.Error.WriteLine("Unhandled exception: {0}", e);
                return 1;
            }
            
            return 0;
        }

        private class DumpStkExcept : Exception
        {
            public DumpStkExcept(int exitCode, string msg) :
                base(msg)
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

		private DumpStkSample(string[] args)
		{		
	        this.client = new DebugClient();
            this.ctrl = new DebugControl(this.client);
            this.symbols = new DebugSymbols(this.client);

            this.traceFrom.Initialize();

            #region Parse commandline
            for (uint i = 0; i < args.Length; ++i)
            {
                if (args[i] == "-a")
                {
                    if ((args.Length - i) < 4)
                    {
                        throw new DumpStkExcept(1, "-a illegal argument type");
                    }

                    for (uint j = 0; j < 3; ++i)
                    {
                        ++i;
                        this.traceFrom[i] = UInt64.Parse(args[i]);
                    }
                }                
                else if (args[i] == "-i")
                {
                    if ((args.Length - i) < 2)
                    {
                        throw new DumpStkExcept(1, "-i missing argument");
                    }

                    ++i;
                    this.imagePath = args[i];
                }
                else if (args[i] == "-y")
                {
                    if ((args.Length - i) < 2)
                    {
                        throw new DumpStkExcept(1, "-y missing argument");
                    }

                    ++i;
                    this.symbolsPath = args[i];
                }
                else if (args[i] == "-z")
                {
                    if ((args.Length - i) < 2)
                    {
                        throw new DumpStkExcept(1, "-z missing argument");
                    }

                    ++i;
                    this.dumpFile = args[i];
                }
                else
                {
                    throw new DumpStkExcept(1, "Unknown command line argument '" + args[i] + "'");
                }
                
            }
            #endregion

            this.ApplyCommandLineArguments();
		}

        ~DumpStkSample()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    this.client.Dispose();
                    this.ctrl.Dispose();
                    this.symbols.Dispose();
                }                
            }

            this.disposed = true;
        }


        private void ApplyCommandLineArguments()
        {
            //this.client.OnDebugOutput += new DebugOutputDel(this.DebugOutputHandler);   

            //using (OutputLineReader lineRdr = new OutputLineReader(this.client)) 
            using (DebugReader dbgRdr = new DebugReader(this.client))
            {
                string dbgLine;

                //lineRdr.OnOutputLine += new LineReaderDeleg(this.DebugOutputHandler);   

                if (this.imagePath != null)
                {
                    this.symbols.SetImagePath(this.imagePath);
                }

                if (this.symbolsPath != null)
                {
                    this.symbols.SetSymbolPath(this.symbolsPath);
                }
                
                if (this.dumpFile != null)
                {
                    this.client.OpenDumpFile(this.dumpFile);                            

                    while ((dbgLine = dbgRdr.ReadLine()) != null)
                    {
                        Console.Out.WriteLine(dbgLine);
                    }
                }
                else
                {
                    throw new DumpStkExcept(1, "No dump file specified");
                }

                this.ctrl.EngineOptions |= EngineOptions.InitialBreak;
                this.client.ExceptionHit += new EventHandler<ExceptionEventArgs>(ExceptionHandler);

                if (this.ctrl.WaitForEvent() == false)
                {
                    throw new DumpStkExcept(1, "WaitForEvent failed");
                }            

                while ((dbgLine = dbgRdr.ReadLine()) != null)
                {
                    Console.Out.WriteLine(dbgLine);
                }
            }
        }

        private void ExceptionHandler(object sender, ExceptionEventArgs args)
        {
            Console.Out.WriteLine("Exception hit: {0}", args.ExceptionRecord.ExceptionCode);
        }

        public void DumpStack()
        {
            using (DebugReader dbgRdr = new DebugReader(this.client))
            {
                uint count = 50;
                DebugStackTrace frames;

                Console.Out.WriteLine("First {0} frames of the call stack:", count);
                
                frames = this.ctrl.GetStackTrace(this.traceFrom[0], this.traceFrom[1], this.traceFrom[2], count);

                DebugSymbols sym = new DebugSymbols(this.client);

                foreach (DebugStackFrame f in frames)
                {
                    string frameSym;
                    ulong displace;

                    sym.GetNameByOffset(f.InstructionOffset, out frameSym, out displace);                    
                }

                StackTraceOutput outputInfo = 
                    StackTraceOutput.SourceLine | StackTraceOutput.FrameAddresses | StackTraceOutput.ColumnNames | StackTraceOutput.FrameNumbers;

                this.ctrl.OutputStackTrace(
                    OutputControl.ToAllClients, 
                    frames,
                    outputInfo);

                string dbgLine;                

                while ((dbgLine = dbgRdr.ReadLine()) != null)
                {

                    Console.Out.WriteLine(dbgLine);
                }
            }
        }

        // Event handler
        private void DebugOutputHandler(/*OutputFlags mask,*/ string output)
        {
            //Console.Write(output);
            Console.Out.WriteLine(output);
        }


        #region IDisposable Members

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        private bool disposed = false;

        private Microsoft.Debuggers.DbgEng.DebugClient client;
        private Microsoft.Debuggers.DbgEng.DebugControl ctrl;
        private Microsoft.Debuggers.DbgEng.DebugSymbols symbols;        

        private UInt64[] traceFrom = new UInt64[3];
        private readonly string imagePath = null;
        private readonly string symbolsPath = null;
        private readonly string dumpFile = null;        
    }
}

