//----------------------------------------------------------------------------
//
// This sample demonstrates the use of DbgEng custom output formatter
//
// Copyright (C) Microsoft Corporation, 2005.
//
//----------------------------------------------------------------------------

using System;

using Microsoft.Debuggers.DbgEng;

sealed class Format
{
    [STAThread]
    static int Main(string[] args)
    {
        DebugClient cli = null;
        DebugControl ctrl = null;
        DebugSymbols sym = null;
        DebugRegisters reg = null;

        try
        {
            cli = new DebugClient();
            ctrl = new DebugControl(cli);
            sym = new DebugSymbols(cli);
            reg = new DebugRegisters(cli);
            
            ctrl.Execute(OutputControl.ToAllClients, "sxe ibp", ExecuteOptions.Default);

            cli.CreateProcessAndAttach(
                "notepad.exe",
                CreateProcessOptions.DebugOnlyThisProcess,
                0,
                ProcessAttachMode.InvasiveResumeProcess);

            ctrl.WaitForEvent();
            
            UInt64 currentAddress = reg.InstructionOffset;
            
            sym.SymbolOptions |= SymbolOptions.LoadLines;            
            sym.SetSymbolPath("srv*"); // Make sure that symsrv.dll is in the path!
            sym.Reload();

            DebugFormatter formatter = new DebugFormatter(cli);           

            string addrInfo1 = String.Format(formatter, "Current address {0:x} \"{0:y}\"", currentAddress);

            Console.Out.WriteLine(addrInfo1);
            
            string addrInfo2 = String.Format(formatter, "Current address +0n16 is {0:x} \"{0:ys}\"", currentAddress + 16);

            Console.Out.WriteLine(addrInfo2);

            Console.Out.WriteLine(String.Format(formatter, "Zero address is {0} \"{0:ys}\"", 0));

            cli.DebugOutput += new EventHandler<DebugOutputEventArgs>(HandleOutput);

            ctrl.Output(OutputModes.Normal, "Debugger output: Current address is {0:x} \"{0:ys}\"", currentAddress);

            Console.Out.WriteLine("Current address (32-bit formatted) is {0}", 
                                  DebugFormatter.FormatAddress(currentAddress, AddressFormatOptions.Force32Bit));
            Console.Out.WriteLine("Current address (64-bit formatted) is {0}", 
                                  DebugFormatter.FormatAddress(currentAddress, AddressFormatOptions.Force64Bit));
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
            if (sym != null) sym.Dispose();
            if (reg != null) reg.Dispose();
        }
        
        return 0;
    }

    static void HandleOutput(object sender, DebugOutputEventArgs args)
    {
        Console.Out.WriteLine(args.Output);
    }
}


