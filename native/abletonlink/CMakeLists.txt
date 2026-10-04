# Ableton Link's C wrapper, abl_link, built as a shared library this application loads through
# P/Invoke. Link ships it as a static library for C programs to link against; a managed program
# can only reach a shared one, so this is the same source file with SHARED in front of it.
#
# The C++ runtime is linked in statically on every platform, so the library depends on nothing
# but the system's C library (and on Windows the system's socket and network DLLs).
#
# Built by build.sh, which fetches the pinned Link release; LINK_DIR is where it unpacked.

cmake_minimum_required(VERSION 3.16)
project(abl_link_shared CXX C)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

include(${LINK_DIR}/AbletonLinkConfig.cmake)

add_library(abl_link SHARED ${LINK_DIR}/extensions/abl_link/src/abl_link.cpp)
target_include_directories(abl_link PUBLIC ${LINK_DIR}/extensions/abl_link/include)
target_link_libraries(abl_link Ableton::Link)

if(WIN32)
  # Cross compiled with mingw, which Link is not written for, and two things need saying.
  #
  # Link includes <Windows.h>, <WinSock2.h> and <WS2tcpip.h> with capitals, and mingw's headers
  # are lowercase on a case sensitive disc, so windows/ holds three one line aliases.
  #
  # mingw's headers define `interface` as a macro for COM, and rpc.h redefines it every time it is
  # included, with no guard. Link 4.1 uses `interface` as a variable name in Channels.hpp.
  # windows/first.h is included ahead of everything: it pulls in every Windows header Link and
  # asio reach, while they can still be pulled in, and then takes the macro away.
  target_include_directories(abl_link PRIVATE ${CMAKE_CURRENT_LIST_DIR}/windows)

  # Windows 10, which is the oldest .NET 10 runs on anyway, so the headers offer what Link reaches
  # for. See windows/first.h for the one call the build image's mingw lacks even so.
  target_compile_definitions(abl_link PRIVATE _WIN32_WINNT=0x0A00 WINVER=0x0A00)
  target_compile_options(abl_link PRIVATE -include ${CMAKE_CURRENT_LIST_DIR}/windows/first.h)

  # abl_link.h carries no export marks, so everything is exported; the DLL is named abl_link.dll
  # rather than libabl_link.dll, which is the name the runtime looks for on Windows.
  set_target_properties(abl_link PROPERTIES WINDOWS_EXPORT_ALL_SYMBOLS ON PREFIX "")
  target_link_options(abl_link PRIVATE -static -static-libgcc -static-libstdc++)

  # MSVC finds these through #pragma comment(lib), which mingw ignores.
  target_link_libraries(abl_link ws2_32 iphlpapi mswsock avrt winmm)
else()
  target_link_options(abl_link PRIVATE -static-libstdc++ -static-libgcc)
endif()
