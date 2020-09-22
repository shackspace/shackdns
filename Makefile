all: bin

bin: bin/shackDNS.exe bin/Newtonsoft.Json.dll bin/Emitter.dll bin/frontend

bin/frontend: $(wildcard frontend/*)
	cp -r frontend $@

bin/%.dll: libs/%.dll
	cp $< $@

bin/shackDNS.exe: src/shackDNS.cs src/DeviceTree.cs libs/Newtonsoft.Json.dll libs/Emitter.dll data/mac-prefixes.tsv
	mkdir -p bin
	mcs /sdk:4.5 \
			/out:$@ \
			/optimize \
			/r:System.Data.dll \
			/r:System.Web.dll \
			/resource:data/mac-prefixes.tsv,MacData.tsv \
			$(addprefix /r:,$(filter %.dll,$^)) \
			$(filter %.cs,$^)

test: bin
	mono bin/shackDNS.exe example.cfg

deploy: Newtonsoft.Json.dll Emitter.dll shackDNS.exe mac-prefixes.tsv frontend/
	scp -r $^ root@infra01:/opt/shackDNS
	ssh root@infra01 systemctl restart shackDNS

# Erstellt die Datei mac-prefixes.tsv aus der Datei oui.txt neu.
# Dies 
mac-prefixes.tsv:
	cat oui.txt | grep "base 16" | sed -E 's/([A-Z0-9]{6})[[:space:]]+\(base 16\)[[:space:]]+(.*)/\1	\2/' | sort > $@

.PHONY: test deploy
.SUFFIXES: