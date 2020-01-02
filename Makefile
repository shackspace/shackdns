all: shackDNS.exe Cracker.exe

%.exe: %.cs Newtonsoft.Json.dll
	mcs /out:$@ /optimize /r:System.Web.dll /r:Newtonsoft.Json.dll $<

test: shackDNS.exe example.cfg
	mono $^

shackles.json: cracklist.txt
	./regen-shackles.sh

nix:
	nix-build

.PHONY: test nix
.SUFFIXES: