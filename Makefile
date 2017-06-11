.PHONY: nuget

MONO_PATH?=/usr/bin
PREFIX?=/usr/local
REAL_PREFIX=$(shell realpath $(PREFIX))

EX_NUGET:=nuget/bin/nuget

XBUILD?=$(MONO_PATH)/xbuild
MONO?=$(MONO_PATH)/mono
GIT?=$(shell which git)

NUGET?=$(EX_NUGET)

dndll:=bin/Release/*

all: binary ;

binary: nuget-packages-restore $(dndll) ;

$(dndll):
	$(XBUILD) DNProj.sln /p:Configuration=Release

# External tools

external-tools: nuget ;

nuget: submodule $(NUGET) ;

submodule:
	$(GIT) submodule update --init --recursive

$(EX_NUGET):
	cd nuget && $(MAKE)

# NuGet

nuget-packages-restore: external-tools
	[ -d packages ] || \
	    $(NUGET) restore DNProj.sln -PackagesDirectory packages ; \

# Install

install: binary 
	mkdir -p $(REAL_PREFIX)/lib/dnproj $(REAL_PREFIX)/bin
	cp bin/Release/* $(REAL_PREFIX)/lib/dnproj/
	echo "#!/bin/sh" > $(REAL_PREFIX)/bin/dnproj
	echo "mono $(REAL_PREFIX)/lib/dnproj/dnproj.exe \"\$$@\"" >> $(REAL_PREFIX)/bin/dnproj
	chmod +x $(REAL_PREFIX)/bin/dnproj
	echo "#!/bin/sh" > $(REAL_PREFIX)/bin/dnsln
	echo "mono $(REAL_PREFIX)/lib/dnproj/dnsln.exe \"\$$@\"" >> $(REAL_PREFIX)/bin/dnsln
	chmod +x $(REAL_PREFIX)/bin/dnsln
	$(REAL_PREFIX)/bin/dnproj --generate-man "$(REAL_PREFIX)"
	$(REAL_PREFIX)/bin/dnsln  --generate-man "$(REAL_PREFIX)"
	@for i in $(REAL_PREFIX)/lib/dnproj/*.dll; do mono --aot $$i; done
	@for i in $(REAL_PREFIX)/lib/dnproj/*.exe; do mono --aot $$i; done

uninstall:
	$(RM) -rf $(REAL_PREFIX)/lib/dnproj/ $(REAL_PREFIX)/bin/dnproj $(REAL_PREFIX)/bin/dnsln

# Clean

clean:
	$(RM) -rf bin/Release DNProj/obj/Release

