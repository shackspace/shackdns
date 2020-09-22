# shackDNS

*Warning:* Work in progress!

## Building

Dependencies:

- Mono 6
- Nuget
- Gnu Make

Build Process:

```sh
make shackDNS.exe
```

Run:

```sh
mono shackDNS.exe /path/to/config.cfg
```

Config File:
```cfg
# Points to a bind configuration file
dns-db = ../dns/bind/pri/db.shack

# Points to a shackles configuration file
# See `shackles.json` in repo
shackles-db = shackles.json

# Points to a REST service with the DHCP leases
leases-api = http://leases.shack/api/leases

# Defines an HTTP endpoint. Multiple bindings are allowed:
binding = http://localhost:8080/
binding = http://localhost:8090/
```

## TODO

- Optionen:
  - `intern` damit ein Shackie nur im Haus sichtbar ist

- Service-Konfiguration designen
  - Anforderungen
    - Eine Konfigurationsdatei für die komplette DNS-Konfig
    - Eintrag-Typ hinterlegen
      - Hardware / Machine
        - Phys. Rechner (Desktop)
        - Phys. Server
        - IoT-Device (ESP32, RPI, …)
        - Laptop / Privatrechner
        - Spezial (C64, …)
        - VM
        - Container
      - Service-Alias
        - Ist ein Alias auf eine physische Maschine
        - Braucht keine konkrete Angabe
    - Kontaktdaten
    - Wikiseite
    - IP-Adressen / CNames
    - Erwarter Zustand: Online/OnlineWhenOpen/Offline
  - Ausgabe der hierarchischen Konfiguration als Grafik
    - [Service] ist in [VM/Container/…] ist in [Server]
    - Datenformat muss hierarchisch schachtelbar sein

  - Statt IP kann auch MAC hinterlegt werdne, diese wird dann
    aus einem statischen Pool bei der Generatation alloziert.

  - VMs/Container können auch eine eine virtuelle IP haben
    (diese ist an den Service gebunden, nicht an den Ort)
    