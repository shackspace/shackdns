
all: shackDNS.exe

shackDNS.exe: shackDNS.cs Newtonsoft.Json.dll
	mcs /out:$@ /r:System.Web.dll /r:Newtonsoft.Json.8.0.3/lib/net45/Newtonsoft.Json.dll shackDNS.cs

test-shack: shackDNS.exe ../dns/bind/pri/db.shack
	mono $^
	
test-local: shackDNS.exe db.debug
	mono $^

Newtonsoft.Json.dll: Newtonsoft.Json.8.0.3/lib/net45/Newtonsoft.Json.dll
	cp $< $@

init:
	nuget install Newtonsoft.Json -Version 8.0.3

.PHONY: test-local test-shack init
.SUFFIXES: