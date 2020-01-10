all: shackDNS.exe 

%.exe: %.cs DeviceTree.cs Newtonsoft.Json.dll
	mcs /sdk:4.5 /out:$@ /optimize /r:System.Web.dll $(addprefix /r:,$(filter %.dll,$^)) $(filter %.cs,$^)

test: shackDNS.exe example.cfg
	mono $^

deploy: Newtonsoft.Json.dll shackDNS.exe mac-prefixes.tsv frontend/
	scp -r $^ root@infra01:/opt/shackDNS
	ssh root@infra01 systemctl restart shackDNS

mac-prefixes.tsv:
	cat oui.txt | grep "base 16" | sed -E 's/([A-Z0-9]{6})[[:space:]]+\(base 16\)[[:space:]]+(.*)/\1	\2/' | sort > $@

.PHONY: test deploy
.SUFFIXES: