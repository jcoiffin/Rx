//----------------------------------------------------------------------------
//
// Sample demonstrating creating/attaching to a process and retreiving debug
// events
//
// Copyright (C) Microsoft Corporation, 2005.
//
//----------------------------------------------------------------------------

using System;

using Microsoft.Debuggers.DbgEng;

namespace attach
{
	/// <summary>
	/// 
	/// </summary>
	sealed class Attach 
	{
        [STAThread]
        static int Main(string[] args)
        {
            DebugClient cli = null;
            DebugControl ctrl = null;

            try
            {                        
                cli = new DebugClient();
                ctrl = new DebugControl(cli);

                // ctrl.Execute(OutputControl.ToAllClients, "sxe ibp", ExecuteOptions.Default);
                ctrl.EngineOptions |= EngineOptions.InitialBreak;

                //cli.OnExceptionDebugEvent += new ExceptionEventHandler(HandleException);
                //cli.OnExitProcessDebugEvent += new ExitProcessEventHandler(HandleExitProcess);
                //cli.OnBreakpointDebugEvent += new BreakpointEventHandler(HandleBp);
                //cli.CreateProcess(0, "notepad.exe", CreateProcessOptions.DebugOnlyThisProcess);

                cli.CreateProcessAndAttach(0,
                    @"notepad.exe",
                    CreateProcessOptions.DebugOnlyThisProcess,
                    0,
                    ProcessAttachMode.InvasiveResumeProcess);

                Console.WriteLine(cli.ToString()); //Identity.ToString());                

                Console.WriteLine(ctrl.WaitForEvent()); // <-- BREAKS HERE!!!

                DebugEventInfo lastEvent = ctrl.GetLastEventInformation();

                Console.WriteLine("Last event: {0}", lastEvent.Description);

                /*for (int i = 0; i < 200; i++)
                    Console.WriteLine(cli.ToString()); */
            }           
            catch(Exception e)
            {
                Console.Error.WriteLine("Exception thrown: {0}", e);
                return 1;
            }
            finally
            {
                if (cli != null) cli.Dispose();
                if (ctrl != null) ctrl.Dispose();
            }
            
            return 0;
        }

        static DebugStatus HandleBp(object sender, BreakpointEventArgs e)
        {
            Console.WriteLine("Breakpoint hit");
            return DebugStatus.Go;
        }

        static DebugStatus HandleExitProcess(object sender, ExitProcessEventArgs e)
        {
            Console.WriteLine("Process exited");
            return DebugStatus.Go;
        }

        static DebugStatus HandleException(object sender, ExceptionEventArgs e)
        {
            Console.WriteLine("Exception raised");
            return DebugStatus.Break;
        }
    }
}

