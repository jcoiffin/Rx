// RManaged.cpp

// used https://stackoverflow.com/questions/2921702/change-c-cli-project-to-another-framework-than-4-0-with-vs2010
//to change TargetFrameworkVersion to 4.5.2 requiered for recent RdotNetlibrary


#include "stdafx.h"
#include <vcclr.h>
#include <atlstr.h>
#include <list>

using namespace System;
using namespace System::Reflection;
using namespace System::IO;

namespace RManaged
{


	public ref class RManagedCall
	{
	public:
		System::Collections::Generic::List<String^>^ sresult;

		void CallRunRcmd(String^ sRcmdline)
		{
			sresult = RdotnetCall::Rcall::RunRcmd(sRcmdline);
		}

		void CallCreateCharacterVar(String^ svarname, String^ svalue)
		{
			sresult = RdotnetCall::Rcall::CreateCharacterVar(svarname, svalue);
		}
	};



	public ref class DoWork
	{

		public:  std::list<PCSTR> RunRcommand(String ^sRcmdline)
		{	
			std::list<PCSTR> Resultlist;
			try
			{				
					// dlls are failing to load as assembly loader is using windbg path instead of dll path.
					// used ResolveEventHandler to fix assembly path https://social.msdn.microsoft.com/Forums/vstudio/en-US/780eb1b2-05be-42d3-8315-4ae82bb79e71/ccli-how-to-add-a-reference?forum=vcgeneral
					AppDomain^ currentDomain = AppDomain::CurrentDomain;
					currentDomain->AssemblyResolve += gcnew ResolveEventHandler(DoWork::MyResolveEventHandler);
					
					RManagedCall^ myRManagedCall = gcnew RManagedCall();
					myRManagedCall->CallRunRcmd(sRcmdline);
					for each (String ^sResultline in myRManagedCall->sresult)
					{
						IntPtr p = System::Runtime::InteropServices::Marshal::StringToHGlobalAnsi(sResultline);	
						Resultlist.push_back(static_cast<PCSTR>(p.ToPointer()));
					};
			}
			catch (Exception ^e)
			{
				String ^sResult = "\nException while calling R command : \n" + e->Message + "\n" + e->StackTrace + "\n";
				if (e->InnerException)
				{
					sResult += "InnerException\n" + e->InnerException->Message + "\n" + e->InnerException->StackTrace + "\n";
				}
				IntPtr perrmsg = System::Runtime::InteropServices::Marshal::StringToHGlobalAnsi(sResult);
				Resultlist.push_back(static_cast<PCSTR>(perrmsg.ToPointer()));
			}
			catch (...)
			{
				Resultlist.push_back((PCSTR)("\nException while calling R command."));
			}

			return (Resultlist);

		}

	public:  std::list<PCSTR> SetRCharacterVar(String^ svarname, String^ svalue)
	{		
		std::list<PCSTR> Resultlist;
		try
		{
			AppDomain^ currentDomain = AppDomain::CurrentDomain;
			currentDomain->AssemblyResolve += gcnew ResolveEventHandler(DoWork::MyResolveEventHandler);

			RManagedCall^ myRManagedCall = gcnew RManagedCall();
			myRManagedCall->CallCreateCharacterVar(svarname, svalue);
			for each (String ^sResultline in myRManagedCall->sresult)
			{
				IntPtr p = System::Runtime::InteropServices::Marshal::StringToHGlobalAnsi(sResultline);
				Resultlist.push_back(static_cast<PCSTR>(p.ToPointer()));
			};
		}
		catch (Exception ^e)
		{
			String ^sResult = "\nException while setting R variable : \n" + e->Message + "\n" + e->StackTrace + "\n";
			if (e->InnerException)
			{
				sResult += "InnerException\n" + e->InnerException->Message + "\n" + e->InnerException->StackTrace + "\n";
			}
			IntPtr perrmsg = System::Runtime::InteropServices::Marshal::StringToHGlobalAnsi(sResult);
			Resultlist.push_back(static_cast<PCSTR>(perrmsg.ToPointer()));
		}
		catch (...)
		{
			Resultlist.push_back((PCSTR)("\nException while setting R variable."));
		}

		return (Resultlist);		
	}

		public:	static Assembly^ MyResolveEventHandler(Object^ sender, ResolveEventArgs^ args)
		{

			Assembly^ MyAssembly;
			Assembly^ objExecutingAssemblies;
			String^ strTempAssmbPath = "";

			objExecutingAssemblies = Assembly::GetExecutingAssembly();
			array<AssemblyName ^>^ arrReferencedAssmbNames = objExecutingAssemblies->GetReferencedAssemblies();


			for each (AssemblyName^ strAssmbName in arrReferencedAssmbNames)
			{				
				if (strAssmbName->FullName->Substring(0, strAssmbName->FullName->IndexOf(",")) == args->Name->Substring(0, args->Name->IndexOf(",")))
				{
					String ^currentdllPath = Path::GetDirectoryName(Assembly::GetExecutingAssembly()->Location);
					strTempAssmbPath = Path::Combine(currentdllPath, "RdotnetCall.dll");						
					break;
				}

			}
			if (File::Exists(strTempAssmbPath))
			{
				MyAssembly = Assembly::LoadFrom(strTempAssmbPath);			
				return MyAssembly;
			}
			return nullptr;
		}


	};
}


__declspec(dllexport) std::list<PCSTR> RunRcommand(PCSTR Rcmd)
{
	std::list<PCSTR> Resultlist;
	try
	{
		String ^sRcmd = gcnew String(Rcmd);
		String^ scurrentdllPath = Path::GetDirectoryName(Assembly::GetExecutingAssembly()->Location);
		Directory::SetCurrentDirectory(scurrentdllPath);

		if (sRcmd)
		{
			// removing whitespace at the beginning of the command 
			while (sRcmd[0].Equals(' '))
			{
				sRcmd = sRcmd->Substring(1);
			}

			String ^scmdswitch = sRcmd->Substring(0, 2);
			
			if (scmdswitch->Equals("c "))
			{
				sRcmd = sRcmd->Substring(2);
				// removing whitespace after the switch
				while (sRcmd[0].Equals(' '))
				{
					sRcmd = sRcmd->Substring(1);
				}

				RManaged::DoWork work;
				return work.RunRcommand(sRcmd);
			}
			else if (scmdswitch->Equals("s "))
			{
				String ^sRscript = "";
				String ^sParam = "";
				sRcmd = sRcmd->Substring(2);
				// removing whitespace after the switch
				while (sRcmd[0].Equals(' '))
				{
					sRcmd = sRcmd->Substring(1);
				}

				if ((sRcmd[0].Equals('\'')) || (sRcmd[0].Equals('\"')))
				{
					char csep = (char)sRcmd[0];
					// remove firt quote and split at the second one
					sRcmd = sRcmd->Substring(1);
					if (sRcmd->Split(csep, 2)->Length > 1)
					{
						sRscript = sRcmd->Split(csep, 2)[0];
						sParam = sRcmd->Substring(sRscript->Length + 1); // +1 for removing the quote at the end
					}
					else
					{
						sRscript = sRcmd;
					}
				}
				else
				{
					// if there is no quotes , ".R " is suposed to be the end of the script name
					array< String^ >^ assep = gcnew array< String^ >(2);
					assep[0] = ".R ";
					assep[1] = ".R\"";
					if (sRcmd->Split(assep, 2, StringSplitOptions::RemoveEmptyEntries)->Length > 1)
					{
						sRscript = sRcmd->Split(assep, 2, StringSplitOptions::RemoveEmptyEntries)[0] + ".R";
						sParam = sRcmd->Substring(sRscript->Length + 1); //+1 for removing space or \" at the beginning
					}
					else
					{
						sRscript = sRcmd;
					}
				}

				if (sParam->Length > 1)
				{
					while ((sParam[0].Equals(' ')) && (sParam[1].Equals(' ')))  // removing multiple spaces before first parameter
					{
						sParam = sParam->Substring(1);
					}
				}

				if (File::Exists(sRscript))
				{
					// info how to run scripts : https://rdotnet.codeplex.com/discussions/453899
					RManaged::DoWork work;

					if (sParam->Length > 0)
					{
						Resultlist = work.SetRCharacterVar("RxArgs", sRcmd->Substring(4));
						
						sParam = sParam->Replace("\"", "\\\"");
						String ^paramcmd = "commandArgs <- function(trailingOnly = TRUE) RxArgs";

						Resultlist = work.RunRcommand(paramcmd);

						std::list<PCSTR>::iterator Paramresultline;
						for (Paramresultline = Resultlist.begin(); Paramresultline != Resultlist.end(); ++Paramresultline)
						{
							String ^sParamresultline = gcnew String(*Paramresultline);
							if (sParamresultline->Contains("Exception"))
							{
								return Resultlist;
							}
						}

					}
					String ^scripcmd = "source('" + sRscript + "')";
					return work.RunRcommand(scripcmd->Replace('\\', '/'));
				}
				else
				{
					Resultlist.push_back((PCSTR)("\nFailed to find the file specified"));
					return (Resultlist);
				}
			}
			else if (sRcmd->Substring(0, 4)->Equals("tor "))
			{
				RManaged::DoWork work;
				String ^sRvariablename = sRcmd->Substring(4)->Split(' ', 2)[0];
				String ^sWindbgcmd = sRcmd->Substring(sRvariablename->Length + 1 + 4);
				Resultlist = work.SetRCharacterVar(sRvariablename, sWindbgcmd);
				work.RunRcommand(sRvariablename + " <- unlist(strsplit(" + sRvariablename + ", \"\\n\"))");
				return (Resultlist);
			}
			else
			{				
				Resultlist.push_back((PCSTR)("\nUnrecognized parameter , check !rx.help"));
				return (Resultlist);
			}
		}
		else
		{
			Resultlist.push_back((PCSTR)("\nMissing parameters , check !rx.help"));
			return (Resultlist);
		}
	}
	catch (Exception ^e)
	{
		String ^sResult = "\nException calling R : \n" + e->Message + "\n" + e->StackTrace + "\n";
		if (e->InnerException)
		{
			sResult += "InnerException\n" + e->InnerException->Message + "\n" + e->InnerException->StackTrace + "\n";
		}
		IntPtr perrmsg = System::Runtime::InteropServices::Marshal::StringToHGlobalAnsi(sResult);
		Resultlist.push_back(static_cast<PCSTR>(perrmsg.ToPointer()));
	}
	catch (...)
	{
		Resultlist.push_back((PCSTR)("\nException calling R script ."));
	}
	return (Resultlist);
}