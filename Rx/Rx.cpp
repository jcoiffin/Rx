// All this code is provided "AS IS" with no warranties, and confer no rights.


#include "stdafx.h"

#include <stdio.h>
#include "dbgexts.h"
#include "outputcallbacks.h"
#include "string.h"
#include<strsafe.h>
#include <io.h>
#include <sstream>
#include <list>

_declspec(dllexport) std::list<PCSTR> RunRcommand(PCSTR Rcmdline);

#define INIT_MACRO()														\
	IDebugClient* pDebugCreated;																																		\
	if (SUCCEEDED(pDebugClient->CreateClient(&pDebugCreated)))																											\
	{																																									\
	    IDebugControl* pDebugControl;																																	\
	    if (SUCCEEDED(pDebugCreated->QueryInterface(__uuidof(IDebugControl),(void **)&pDebugControl)))																	\
	    {																																								\
			IDebugSymbols* pDebugSymbols;																																\
			if (SUCCEEDED(pDebugCreated->QueryInterface(__uuidof(IDebugSymbols), (void **)&pDebugSymbols)))																\
      		{																																							\
				COutputCallbacks* pOutputCallbacks = new COutputCallbacks;																								\
				if (pOutputCallbacks != NULL)																															\
				{																																						\
					if (SUCCEEDED(pDebugCreated->SetOutputMask(pOutputCallbacks->SupportedMask())) && SUCCEEDED(pDebugCreated->SetOutputCallbacks(pOutputCallbacks)))	\
					{


#define EXIT_MACRO()														\
						pDebugCreated->SetOutputCallbacks(NULL);	\
					}												\
				}													\
			}														\
			pDebugSymbols->Release();								\
	    }															\
	    pDebugControl->Release();									\
	}	

HRESULT CALLBACK
Help(PDEBUG_CLIENT pDebugClient, PCSTR args)
{
	UNREFERENCED_PARAMETER(args);
	INIT_MACRO();
	if (args && args[0])
	{
		if ((args[0] == 'c') || (args[0] == 'C'))
		{
			pDebugControl->Output(DEBUG_OUTPUT_NORMAL, "!r c <Rcommand>\nRuns a command line in R.\n\n");
			pDebugControl->Output(DEBUG_OUTPUT_NORMAL, "Example :\n !r c \"Hello from windbg\" \n");
		}
		else if ((args[0] == 's') || (args[0] == 'S'))
		{
			pDebugControl->Output(DEBUG_OUTPUT_NORMAL, "!r s <ScriptName> [arguments]\nRuns a script in R.\n\n");
//			pDebugControl->Output(DEBUG_OUTPUT_NORMAL, "Example: \n!r toR !handle 0 2 \n");
//			pDebugControl->Output(DEBUG_OUTPUT_NORMAL, "!r s \"d.R\" -RxgrepFilter \"Handle|HandleCount\" -Rxnewobjdel \"Handle \" -Rxcolumndel \"HandleCount\" -Rxcolumnnames \"Handle,HandleCount\" -RxSort \"HandleCount\" \n");			 
		}
		else if ((args[0] == 't') || (args[0] == 'T'))
		{
			pDebugControl->Output(DEBUG_OUTPUT_NORMAL, "!r toR <Rvariable> <windbg command>\nRuns a windbg command and store the result in a new R variable with the name you specified.\n\n");
			pDebugControl->Output(DEBUG_OUTPUT_NORMAL, "Example: \n!r toR lminR lm\n!r c lminR\n");
		}
	}
	else
	{
		pDebugControl->Output(DEBUG_OUTPUT_NORMAL, "Rx.dll\n------\nThis dll allows you to run R commands inside Windbg\nProvide your feedback to jcoiffin@microsoft.com\n\n");
		pDebugControl->ControlledOutput(DEBUG_OUTCTL_AMBIENT_DML, DEBUG_OUTPUT_NORMAL, "<link cmd = \"!rx.help c\">!r c</link> 		Runs a command line in R.\n");
		pDebugControl->ControlledOutput(DEBUG_OUTCTL_AMBIENT_DML, DEBUG_OUTPUT_NORMAL, "<link cmd = \"!rx.help s\">!r s</link> 		Runs a script in R.\n");
		pDebugControl->ControlledOutput(DEBUG_OUTCTL_AMBIENT_DML, DEBUG_OUTPUT_NORMAL, "<link cmd = \"!rx.help t\">!r toR</link> 	Runs a windbg command and store the result in R variable.\n");
	}
	
	EXIT_MACRO();
	return S_OK;
}

//Enforcing a single command with switchs to avoid args starting with doublequotes => appears to be a limitation from dbgexts ,strings starting with doublequotes are cut.
HRESULT CALLBACK
r (PDEBUG_CLIENT pDebugClient, PCSTR args)
{
	UNREFERENCED_PARAMETER(args);
	INIT_MACRO();
	
	try
	{
		std::list<PCSTR> Rcmdresultlist;
		std::list<PCSTR>::iterator Rcmdresultline;
		if (((args[0] == 't') || (args[0] == 'T')) && ((args[1] == 'o') || (args[1] == 'O')) && ((args[2] == 'r') || (args[2] == 'R')) && (args[3] == ' '))
		{
			// skipping tor<space> at te beginning of args to get the cmd to run
			// skip until whitespace as it is starting with variable name
			char* scmdtorun = NULL;
			char *sRvariable = NULL;

			sRvariable = strtok_s((char *)args + 4, " ", &scmdtorun);
			pOutputCallbacks->Clear();

			pDebugControl->Execute(DEBUG_OUTCTL_THIS_CLIENT | DEBUG_OUTCTL_NOT_LOGGED, scmdtorun, DEBUG_EXECUTE_NOT_LOGGED | DEBUG_EXECUTE_NO_REPEAT);


			if (pOutputCallbacks->BufferNormal())
			{
				// create szwindbgcmdresult variable with to tor<space>sRvariable<space>windbgcmd
				size_t souputlength = strlen(pOutputCallbacks->BufferNormal()) + 1;

				char* szwindbgcmdresult = new char[souputlength + 4 + strlen(sRvariable) + 1];
				
				strcpy_s(szwindbgcmdresult, 5, "tor ");
				strcpy_s(szwindbgcmdresult + 4, strlen(sRvariable) + 1, sRvariable);
				szwindbgcmdresult[strlen(sRvariable) + 4] = ' ';
				strcpy_s(szwindbgcmdresult + 4 + strlen(sRvariable) + 1, strlen(pOutputCallbacks->BufferNormal()) + 1, pOutputCallbacks->BufferNormal());

				Rcmdresultlist = RunRcommand(szwindbgcmdresult);
			}
			else
			{
				pDebugControl->Output(DEBUG_OUTPUT_NORMAL, "Windbg command provided returned no result. Thus no data was imported in R.");
			}
		}
		else
		{
			Rcmdresultlist = RunRcommand(args);
		}
		for (Rcmdresultline = Rcmdresultlist.begin(); Rcmdresultline != Rcmdresultlist.end(); ++Rcmdresultline)
		{
			pDebugControl->Output(DEBUG_OUTPUT_NORMAL, *Rcmdresultline);
			pDebugControl->Output(DEBUG_OUTPUT_NORMAL, "\n");
		}
	}
	catch (const std::exception& ex)
	{
		pDebugControl->Output(DEBUG_OUTPUT_NORMAL, "\nexception encountered : \n");
		pDebugControl->Output(DEBUG_OUTPUT_NORMAL, ex.what() );
	}

	catch (...)
	{
		pDebugControl->Output(DEBUG_OUTPUT_NORMAL, "\nexception encountered");
	}

	pDebugControl->Output(DEBUG_OUTPUT_NORMAL, "\n");

	EXIT_MACRO();
	return S_OK;
}

HRESULT CALLBACK
HELP(PDEBUG_CLIENT pDebugClient, PCSTR args)
{
	Help(pDebugClient, args);
	return S_OK;
}

HRESULT CALLBACK
help(PDEBUG_CLIENT pDebugClient, PCSTR args)
{
	Help(pDebugClient, args);
	return S_OK;
}

HRESULT CALLBACK
R(PDEBUG_CLIENT pDebugClient, PCSTR args)
{
	r(pDebugClient, args);
	return S_OK;
}


