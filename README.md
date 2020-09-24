# shackDNS

*Warning:* Work in progress!

## Building

### Deployment

shackDNS can be run as a docker container. Just run the following command:
```
docker build .
```
to build the container.

### Manual / Development

Dependencies:

- Mono 6
- Nuget
- Gnu Make

Build Process:

```sh
make
```

## Deployment

The `Makefile` will create a new folder called `bin` which contains all required files for deployment. Just copy this folder where you need it.

```sh
make                       # build the project
cp -r bin/* $(INSTALL_DIR) # copy the resulting files to your install destination
```

## Config File

```cfg
# Points to a BIND9 configuration file
dns-db = ../dns/bind/pri/db.shack

# Points to a shackles configuration file
# See `shackles.db` in repo
shackles-db = shackles.db

# Points to a ICS DHCP leases file
leases-db = fake-leases.db

# Points to a JSON file describing the whole
# shack infrastructure
infra-db = godconfig.json

# Multiple URL bindings are allowed
binding = http://localhost:8080/
binding = http://*:8080/

# MQTT Configuration (optional):
mqtt-broker-host = mqtt.shack # 
mqtt-broker-port = 1883       #
mqtt-device-name = shackDNS   # Name of the MQTT device
mqtt-prefix      = shackdns   # the prefix for the mqtt messages

# This is your top level domain name, used for
# completing the links in the device overview.
top-level-domain = shack
```

## shackles Database File

Contains three multi-whitespace-separated columns:
The username displayed on the website, the type of the data field (only `mac` allowed atm) and the value (a mac address, separated by `:`).
Comments can be written with `#`

Each entry will be added to the database, single users can have multiple entries in the database.

Example:
```
# Users      Type  Value
anon         mac   50:7b:9d:67:eb:90
anon         mac   50:7b:9d:67:13:37 # nice
anon         mac   50:7b:9d:67:eb:92

other_anon   mac   50:7b:9d:67:eb:93
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

  - Statt IP kann auch MAC hinterlegt werden, diese wird dann
    aus einem statischen Pool bei der Generatation alloziert.

  - VMs/Container können auch eine eine virtuelle IP haben
    (diese ist an den Service gebunden, nicht an den Ort)
    