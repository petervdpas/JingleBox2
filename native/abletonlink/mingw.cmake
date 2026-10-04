# Windows, cross compiled. The -posix compilers rather than the plain ones, because Link uses
# std::thread and std::mutex and the win32 thread model of mingw's GCC has neither.
set(CMAKE_SYSTEM_NAME Windows)
set(CMAKE_C_COMPILER x86_64-w64-mingw32-gcc-posix)
set(CMAKE_CXX_COMPILER x86_64-w64-mingw32-g++-posix)
set(CMAKE_RC_COMPILER x86_64-w64-mingw32-windres)
