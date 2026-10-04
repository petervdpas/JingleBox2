# The one place the three abl_link builds are made, so they come out of one known toolchain.
#
# Ubuntu 22.04 for its C library, 2.35: a library built against a newer one refuses to load on an
# older system, and this is under both Raspberry Pi OS bookworm (2.36) and the oldest Linux .NET 10
# supports. Built on the machine this was written on (Debian trixie, 2.41) it asked for 2.38.
#
# arm64 and Windows are cross compiled rather than emulated, so no binfmt or qemu is needed.
FROM ubuntu:22.04
ENV DEBIAN_FRONTEND=noninteractive
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      cmake ninja-build g++ \
      g++-aarch64-linux-gnu binutils-aarch64-linux-gnu \
      g++-mingw-w64-x86-64 binutils-mingw-w64-x86-64 \
 && rm -rf /var/lib/apt/lists/*
