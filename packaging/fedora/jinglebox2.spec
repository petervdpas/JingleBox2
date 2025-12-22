Name:           jinglebox2
Version:        1.0.0
Release:        1%{?dist}
Summary:        Lightweight audio pad launcher

License:        MIT
URL:            https://github.com/petervdpas/JingleBox2
Source0:        %{name}-%{version}.tar.gz

BuildArch:      x86_64

%global debug_package %{nil}
%global appdir /opt/JingleBox2

%description
JingleBox2 is a lightweight cross-platform audio pad launcher built with .NET and Avalonia UI.

%prep
%autosetup -n %{name}-%{version}

%build
# No build here; we package pre-published self-contained output.

%install
rm -rf %{buildroot}

# App payload
mkdir -p %{buildroot}%{appdir}
cp -a payload/* %{buildroot}%{appdir}/

# Ensure main binary is executable (adjust name if different)
chmod 0755 %{buildroot}%{appdir}/JingleBox2 || :

# Wrapper
install -Dpm 0755 packaging/fedora/jinglebox2.sh %{buildroot}%{_bindir}/jinglebox2

# Desktop file + icon
install -Dpm 0644 packaging/fedora/jinglebox2.desktop %{buildroot}%{_datadir}/applications/jinglebox2.desktop
install -Dpm 0644 packaging/fedora/icons/jinglebox2.png %{buildroot}%{_datadir}/icons/hicolor/256x256/apps/jinglebox2.png

%files
%license LICENSE
%{_bindir}/jinglebox2
%{_datadir}/applications/jinglebox2.desktop
%{_datadir}/icons/hicolor/256x256/apps/jinglebox2.png
%dir %{appdir}
%{appdir}/*

%changelog
* Sun Dec 21 2025 Peter van de Pas - 1.0.0-1
- Initial Fedora package (self-contained)
