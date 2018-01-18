using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RDotNet;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace RdotnetCall
{

    public static class Rcall
    {
     
        private static TextWriter stdOut;

        public static List<string> CreateCharacterVar (string svarname , string svalue)
        {
            try
            {
                REngine.SetEnvironmentVariables();
                REngine engine = REngine.GetInstance();

                CharacterVector charVec = engine.CreateCharacter(svalue);
                engine.SetSymbol(svarname, charVec);
                List<string> lerrmsg = new List<string> {"\nCreated R variable named " + svarname };
                return lerrmsg;
            }
            catch (Exception e)
            {
                string errmsg = "\nException encountered while set R variable :" + svarname + "\n";

                errmsg += "\nError message :\n" + e.Message + "\n" + e.StackTrace + "\n";

                if (e.InnerException != null)
                {
                    errmsg += "InnerException\n" + e.InnerException.Message + "\n" + e.InnerException.StackTrace + "\n";
                }
                List<string> lerrmsg = new List<string> { errmsg };
                return lerrmsg;
            }
        }

        public static List<string> RunRcmd(string sRcmdline)
        {

            stdOut = System.Console.Out;

            try
            {
                // usingRdotNet module to call R :
                // http://jmp75.github.io/rdotnet/
                // http://jmp75.github.io/rdotnet/tut_basic_types/
                // http://jmp75.github.io/rdotnet/getting_started/
                
                REngine.SetEnvironmentVariables();
                REngine engine = REngine.GetInstance();

                // Redirect Console output to a buffer http://stackoverflow.com/questions/4470700/how-to-save-console-writeline-output-to-text-file
                
                StringWriter consoleOut = new StringWriter();
                System.Console.SetOut(consoleOut);


                // run R command
                SymbolicExpression evalreturn = engine.Evaluate(sRcmdline);
                

                //Restore Normal Console Output
                System.Console.SetOut(stdOut);


                List<string> ConsoleStringReturn = new List<string> { consoleOut.ToString() };
                return ConsoleStringReturn;
            }

            catch (Exception e)
            {
                System.Console.SetOut(stdOut);
                string errmsg = "\nException encountered while calling R command :" + sRcmdline + "\n";
                errmsg += "\n Invalid R command have been seen to generate exception , check that the command is correct\n";
                errmsg += "\nError message :\n" + e.Message + "\n";
                if (e.StackTrace.Contains("RDotNet.REngine.Evaluate") == false)
                {
                    errmsg += e.StackTrace + "\n";
                }
                if (e.InnerException != null)
                {
                    errmsg += "InnerException\n" + e.InnerException.Message + "\n" + e.InnerException.StackTrace + "\n";
                }
                if(e.Message.Contains("Windows Registry key 'SOFTWARE\\R-core' not found in HKEY_LOCAL_MACHINE nor HKEY_CURRENT_USER"))
                {
                    errmsg = "\nR needs to be installed on the machine in order to use it from windbg using Rx.dll";
                    errmsg += "\nDownload and install R from https://cran.r-project.org/mirrors.html";
                }
                List<string> lerrmsg = new List<string> { errmsg };
                return lerrmsg;
            }

        }
    }
}