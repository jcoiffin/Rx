//----------------------------------------------------------------------------
//
// Example of how to connect to a debugger server and execute
// a command when the server is broken in.
//
// Copyright (C) Microsoft Corporation, 2005.
//
//----------------------------------------------------------------------------

using System;

using Microsoft.Debuggers.DbgEng;

namespace remmon
{
	/// <summary>
	/// 
	/// </summary>
	public class RemoteExecute : IDisposable
	{
        [STAThread]
        static int Main(string[] args)
        {
            try
            {
                using (RemoteExecute sample = new RemoteExecute(args))
                {
                    sample.WaitForBreakIn();
                    sample.ExecuteCommand();
                }
            }
            catch(RemmonExcept e)
            {
                Console.Error.WriteLine(e.Message);
                return e.ExitCode;
            }
            catch(Exception e)
            {
                Console.Error.WriteLine("Uhandled exception: {0}", e);
                return 1;
            }
            
            return 0;
        }


        private RemoteExecute(string[] args)
        {
            for (uint i = 0; i < args.Length; ++i)
            {
                if (args[i] == "-b")
                {
                    this.forceBreak = true;
                }
                else if (args[i] == "-cmd")
                {
                    if ((args.Length - i) < 2)
                    {
                        throw new RemmonExcept(1, "-cmd missing argument");
                    }

                    ++i;
                    this.command = args[i];
                }
                else if (args[i] == "-remote")
                {
                    if ((args.Length - i) < 2)
                    {
                        throw new RemmonExcept(1, "-remote missing argument");
                    }

                    ++i;
                    this.connect = args[i];
                }
                else
                {
                    throw new RemmonExcept(1, "Unknown command line argument '" + args[i] + "'");
                }
            }          
  
            if (this.connect == null)
            {
                throw new RemmonExcept(1, "Missing remote connect string");
            }

            this.client = new DebugClient(this.connect);
            this.ctrl = new DebugControl(this.client);

            // this.client.DebugEventInterestMask = DebugEvents.EngineChangeEvent;            

            this.client.EngineStateChanged += new EventHandler< EngineStateChangeEventArgs >(client_OnChangeEngineState);            

            this.exitDispatchClient = new DebugClient(this.connect);
        }


        private void WaitForBreakIn()
        {
            if (this.forceBreak)
            {
                this.ctrl.SetInterrupt(Interrupt.Active);
            }

            while (this.ctrl.ExecutionStatus != DebugStatus.Break)
            {
                if (!this.client.DispatchCallbacks())
                {
                    throw new RemmonExcept(1, "DispatchCallbacks() return false");
                }
            }
        }

        private void ExecuteCommand()
        {
            if (this.command != null)
            {
                this.client.DebugOutput += new EventHandler< DebugOutputEventArgs >(client_OnDebugOutput);

                try
                {
                    Console.Out.WriteLine("Executing '{0}' on server", this.command);
                    this.ctrl.Execute(OutputControl.ToAllClients, this.command, ExecuteOptions.Echo);                
                }
                finally
                {
                    this.client.DebugOutput -= new EventHandler<DebugOutputEventArgs>(client_OnDebugOutput);
                }
            }
            else
            {
                throw new RemmonExcept(1, "No command was provided");
            }
        }


        private class RemmonExcept : Exception
        {
            public RemmonExcept(int exitCode, string msg) : base(msg)
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


        private void client_OnChangeEngineState(object sender, EngineStateChangeEventArgs args)
        {
            if ((args.Change & EngineStateChange.ExecutionStatus) != 0)
            {
                this.execStatus = (Microsoft.Debuggers.DbgEng.DebugStatus) args.Argument;

                this.exitDispatchClient.ExitDispatch(this.client);
            }
        }

        private static void client_OnDebugOutput(object sender, DebugOutputEventArgs args)
        {
            Console.Out.Write(args.Output);
        }        

        #region IDisposable Members

        public void Dispose()
        {
            this.ctrl.Dispose();
            this.exitDispatchClient.Dispose();
            this.client.EndSession(EndSessionMode.Disconnect);
            this.client.Dispose();
        }

        #endregion

        private readonly bool forceBreak = false;
        private readonly string command = null;
        private readonly string connect = null;

        private Microsoft.Debuggers.DbgEng.DebugStatus execStatus;

        private Microsoft.Debuggers.DbgEng.DebugClient client;
        private Microsoft.Debuggers.DbgEng.DebugClient exitDispatchClient;
        private Microsoft.Debuggers.DbgEng.DebugControl ctrl;

        
    }
}
