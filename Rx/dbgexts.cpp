// dbgexts.cpp

#include "stdafx.h"
#include "dbgexts.h"

extern "C" HRESULT CALLBACK
DebugExtensionInitialize(PULONG Version, PULONG Flags)
{
	*Version = DEBUG_EXTENSION_VERSION(EXT_MAJOR_VER, EXT_MINOR_VER);
	*Flags = 0;  // Reserved for future use.
	return S_OK;
}

extern "C" void CALLBACK
DebugExtensionNotify(ULONG Notify, ULONG64 Argument)
{
	UNREFERENCED_PARAMETER(Argument);
	switch (Notify)
	{
	case DEBUG_NOTIFY_SESSION_ACTIVE:
		break;

	case DEBUG_NOTIFY_SESSION_INACTIVE:
		break;

	case DEBUG_NOTIFY_SESSION_ACCESSIBLE:
		break;

	case DEBUG_NOTIFY_SESSION_INACCESSIBLE:
		break;
	}
	return;
}

extern "C" void CALLBACK
DebugExtensionUninitialize(void)
{
	return;
}
