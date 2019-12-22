



all: pinger.exe

pinger.exe: pinger.cs
	mcs /out:$@ $^

test-shack: pinger.exe ../dns/bind/pri/db.shack
	mono $^
	
test-local: pinger.exe db.debug
	mono $^

.PHONY: test-local test-shack
.SUFFIXES: