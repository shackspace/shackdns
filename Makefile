
all: pinger.exe

pinger.exe: pinger.cs Newtonsoft.Json.dll
	mcs /out:$@ /r:System.Web.dll /r:Newtonsoft.Json.8.0.3/lib/net45/Newtonsoft.Json.dll pinger.cs

test-shack: pinger.exe ../dns/bind/pri/db.shack
	mono $^
	
test-local: pinger.exe db.debug
	mono $^

Newtonsoft.Json.dll: Newtonsoft.Json.8.0.3/lib/net45/Newtonsoft.Json.dll
	cp $< $@

init:
	nuget install Newtonsoft.Json -Version 8.0.3

.PHONY: test-local test-shack init
.SUFFIXES: