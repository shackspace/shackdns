



all: pinger.exe

pinger.exe: pinger.cs
	mcs /out:$@ $^

test: pinger.exe ../dns/bind/pri/db.shack
	mono $^

.PHONY: test
.SUFFIXES: