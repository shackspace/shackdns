all: shackDNS.exe Cracker.exe

%.exe: %.cs Newtonsoft.Json.dll
	mcs /sdk:4.5 /out:$@ /optimize /r:System.Web.dll /r:Newtonsoft.Json.dll $<

test: shackDNS.exe example.cfg
	mono $^

deploy: Newtonsoft.Json.dll shackDNS.exe mac-prefixes.tsv frontend/
	scp -r $^ root@infra01:/opt/shackDNS
	ssh root@infra01 systemctl restart shackDNS

.PHONY: test deploy
.SUFFIXES: