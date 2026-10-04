/* Included ahead of everything when abl_link is cross compiled for Windows with mingw.
   See CMakeLists.txt beside this folder for why. */
#pragma once
#include <winsock2.h>
#include <ws2tcpip.h>
#include <mswsock.h>
#include <windows.h>
#include <objbase.h>
#include <iphlpapi.h>
#include <avrt.h>
#include <process.h>
#include <processthreadsapi.h>
#include <synchapi.h>
#undef interface

/* Link names its threads with SetThreadDescription, which the mingw in the build image (Ubuntu
   22.04's) never declares. A name is only what a debugger shows, so it is a call that does
   nothing here. Defined after windows.h, so a newer mingw that does declare it has already done
   so and is not disturbed. */
#define SetThreadDescription(thread, name) ((void)(thread), (void)(name), (HRESULT)0)
