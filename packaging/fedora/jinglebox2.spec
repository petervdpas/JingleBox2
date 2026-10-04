Name:           jinglebox2
Version:        1.0.0
Release:        1%{?dist}
Summary:        Pad launcher, recorder and tracker for radio and live shows

License:        GPL-2.0-only AND LicenseRef-BASS
URL:            https://github.com/petervdpas/JingleBox2
Source0:        %{name}-%{version}.tar.gz

BuildArch:      x86_64

# Self-contained /opt payload:
# Prevent rpmbuild from auto-generating broken symbol-version deps like:
#   libdl.so.2(GLIBC_2.17), libpthread.so.0(GLIBC_2.17), libm.so.6(GLIBC_2.17)
AutoReqProv:    no

# Minimal runtime dependency (Fedora has it anyway, but this keeps RPM sane)
Requires:       glibc

# Common runtime libs Avalonia/Skia typically needs on Fedora (safe to keep)
Requires:       alsa-lib
Requires:       fontconfig
Requires:       freetype
Requires:       libX11
Requires:       mesa-libGL

%global debug_package %{nil}
%global appdir /opt/JingleBox2

%description
JingleBox2 fires jingles from a wall of pads, records and edits takes, and writes music
in a tracker with its own synths, samplers and drum machines beside VST3 and CLAP
plugins. It plays its audio through BASS, a proprietary library by Un4seen Developments
that is free of charge for non-commercial use.

%prep
%autosetup -n %{name}-%{version}

%build
# No build here; we package pre-published self-contained output.

%install
rm -rf %{buildroot}

# App payload
mkdir -p %{buildroot}%{appdir}
cp -a payload/* %{buildroot}%{appdir}/

# Ensure main binary is executable
chmod 0755 %{buildroot}%{appdir}/JingleBox2 || :

# Wrapper
install -Dpm 0755 packaging/fedora/jinglebox2.sh %{buildroot}%{_bindir}/jinglebox2

# Desktop file + icon
install -Dpm 0644 packaging/fedora/jinglebox2.desktop %{buildroot}%{_datadir}/applications/jinglebox2.desktop
install -Dpm 0644 packaging/fedora/icons/jinglebox2.png %{buildroot}%{_datadir}/icons/hicolor/256x256/apps/jinglebox2.png

%files
%license LICENSE LICENSE.EXCEPTION
%{_bindir}/jinglebox2
%{_datadir}/applications/jinglebox2.desktop
%{_datadir}/icons/hicolor/256x256/apps/jinglebox2.png
%dir %{appdir}
%{appdir}/*

%changelog
* Sun Dec 21 2025 Peter van de Pas - 1.0.0-1
- Initial Fedora package (self-contained)
