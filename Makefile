all: shackDNS.exe Cracker.exe

%.exe: %.cs Newtonsoft.Json.dll
	mcs /out:$@ /optimize /r:System.Web.dll /r:Newtonsoft.Json.dll $<

test: shackDNS.exe example.cfg
	mono $^

Newtonsoft.Json.dll: Newtonsoft.Json.8.0.3/lib/net45/Newtonsoft.Json.dll
	cp $< $@

init:
	nuget install Newtonsoft.Json -Version 8.0.3

.PHONY: test-local test-shack init
.SUFFIXES: