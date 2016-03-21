MONO_PATH?=/usr/bin
DESTDIR?=/usr/local
DESTDIR2=$(shell realpath $(DESTDIR))

EX_NUGET:=nuget/bin/nuget

XBUILD?=$(MONO_PATH)/xbuild
MONO?=$(MONO_PATH)/mono
GIT?=$(shell which git)

NUGET?=$(EX_NUGET)

all: binary ;

binary: bin/Release/DNProj.Core.dll ;

bin/Release/DNProj.Core.dll: nuget-packages-restore 
	$(XBUILD) DNProj.sln /p:Configuration=Release

# External tools

external-tools: nuget ;

nuget: $(NUGET) ;

submodule:
	$(GIT) submodule update --init --recursive

$(EX_NUGET): submodule
	cd nuget && $(MAKE)

# NuGet

nuget-packages-restore: external-tools
	[ -d packages ] || \
	    $(NUGET) restore -ConfigFile BetaReductionBot/packages.config -PackagesDirectory packages ; \

# Install

install: binary 
	mkdir -p $(DESTDIR2)/lib/dnproj $(DESTDIR2)/bin
	cp bin/Release/* $(DESTDIR2)/lib/dnproj/
	echo "#!/bin/sh" > $(DESTDIR2)/bin/dnproj
	echo "mono $(DESTDIR2)/lib/dnproj/dnproj.exe \$$*" >> $(DESTDIR2)/bin/dnproj
	chmod +x $(DESTDIR2)/bin/dnproj
	echo "#!/bin/sh" > $(DESTDIR2)/bin/dnsln
	echo "mono $(DESTDIR2)/lib/dnproj/dnsln.exe \$$*" >> $(DESTDIR2)/bin/dnsln
	chmod +x $(DESTDIR2)/bin/dnsln

# Clean

clean:
	$(RM) -rf bin/ DNProj/obj

