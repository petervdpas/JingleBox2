Name:           jinglebox2
Version:        1.0.0
Release:        1%{?dist}
Summary:        Lightweight audio pad launcher

License:        MIT
URL:            https://github.com/petervdpas/JingleBox2
Source0:        %{name}-%{version}.tar.gz

BuildArch:      x86_64

# Self-contained: DO NOT require dotnet runtime.
# You may still need basic GUI/audio libs at runtime depending on what Avalonia/Skia touches on Fedora.
# If you want to declare them, uncomment and adjust as needed:
# Requires:       alsa-lib
# Requires:       mesa-libGL
# Requires:       libX11
# Requires:       fontconfig
# Requires:       freetype

%description
JingleBox2 is a lightweight cross-platform audio pad launcher built with .NET and Avalonia UI.

%prep
%autosetup -n %{name}-%{version}

%build
# No build here; we package pre-published self-contained output.

%install
rm -rf %{buildroot}

# Install app payload to /opt
mkdir -p %{buildroot}/opt/JingleBox2
cp -a payload/* %{buildroot}/opt/JingleBox2/

# Wrapper in /usr/bin
install -Dpm 0755 packaging/fedora/jinglebox2.sh %{buildroot}/usr/bin/jinglebox2

# Desktop file
install -Dpm 0644 packaging/fedora/jinglebox2.desktop %{buildroot}/usr/share/applications/jinglebox2.desktop

# Icon (hicolor)
install -Dpm 0644 packaging/fedora/icons/jinglebox2.png %{buildroot}/usr/share/icons/hicolor/256x256/apps/jinglebox2.png

%files
%license LICENSE
/usr/bin/jinglebox2
/usr/share/applications/jinglebox2.desktop
/usr/share/icons/hicolor/256x256/apps/jinglebox2.png
/opt/JingleBox2

%changelog
* Sun Dec 21 2025 Peter van de Pas - 1.0.0-1
- Initial Fedora package (self-contained)
