# shackDNS

*Warning:* Work in progress!

## Building

Dependencies:

- Mono 6
- Nuget
- Gnu Make

Build Process:

```sh
make init
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